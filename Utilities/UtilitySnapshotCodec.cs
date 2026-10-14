using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace HundredTiles.Utilities
{
    // Fixed, reviewed schema: no CLR memory images, runtime type resolution or
    // attacker-controlled allocation sizes are read from the save file.
    internal static class UtilitySnapshotCodec
    {
        static readonly Dictionary<Type,FieldInfo[]> fields=new Dictionary<Type,FieldInfo[]>();
        static FieldInfo[] Fields(Type type)
        {
            FieldInfo[] result;
            if(!fields.TryGetValue(type,out result))
            {
                result=type.GetFields(BindingFlags.Instance|BindingFlags.Public);
                Array.Sort(result,(a,b)=>string.CompareOrdinal(a.Name,b.Name));fields.Add(type,result);
            }
            return result;
        }
        internal static byte[] Hash(byte[] bytes) {using(var sha=SHA256.Create())return sha.ComputeHash(bytes);}
        static bool Equal(byte[] a,byte[] b)
        {if(a==null || b==null || a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        internal static byte[] Payload(SortedDictionary<string,object> state)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {foreach(var pair in state)Write(writer,pair.Value,pair.Value.GetType());return stream.ToArray();}
        }
        internal static int EncodedLength(SortedDictionary<string,object> state)
        {
            int size=140+(state.ContainsKey(UtilityCapacity.Keys[0])?UtilityCapacity.Keys.Length*4:0);
            foreach(var pair in state){var array=pair.Value as Array;size=checked(size+(array==null?TypeSize(pair.Value.GetType()):checked(array.Length*TypeSize(array.GetType().GetElementType()))));}
            return size;
        }
        internal static int TypeSize(Type type)
        {
            if(type==typeof(byte) || type==typeof(bool))return 1;if(type==typeof(short) || type==typeof(ushort))return 2;if(type==typeof(int) || type==typeof(uint))return 4;if(type==typeof(long))return 8;
            if(!type.IsValueType)throw new InvalidOperationException("Unsupported utility schema type.");int size=0;foreach(var field in Fields(type))size=checked(size+TypeSize(field.FieldType));return size;
        }
        static byte[] Schema(SortedDictionary<string,object> state)
        {
            var text=new StringBuilder();
            foreach(var pair in state)
            {
                text.Append(pair.Key).Append(':');Type type=pair.Value.GetType();
                if(type.IsArray){text.Append(((Array)pair.Value).Length).Append(':');type=type.GetElementType();}
                Describe(text,type);text.Append(';');
            }
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        static void Describe(StringBuilder text,Type type)
        {
            text.Append(type.FullName);
            if(type.IsPrimitive)return;
            foreach(var field in Fields(type)){text.Append('|').Append(field.Name).Append(':');Describe(text,field.FieldType);}
        }
        internal static byte[] Encode(SortedDictionary<string,object> state,byte[] baseline,byte[] binding)
        {
            if(baseline==null || baseline.Length!=32 || binding==null || binding.Length!=32)throw new ArgumentException("Utility snapshot binding missing.");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                bool capacity=state.ContainsKey(UtilityCapacity.Keys[0]);
                writer.Write(Encoding.ASCII.GetBytes("HTUTIL01"));writer.Write(capacity?2:1);
                if(capacity)foreach(string key in UtilityCapacity.Keys){int count=((Array)state[key]).Length;if(!UtilityCapacity.Known(count))throw new InvalidDataException("Unsupported utility capacity.");writer.Write(count);}
                writer.Write(Schema(state));writer.Write(baseline);writer.Write(binding);writer.Write(Payload(state));
                byte[] payload=stream.ToArray();writer.Write(Hash(payload));return stream.ToArray();
            }
        }
        internal static SortedDictionary<string,object> Decode(byte[] bytes,SortedDictionary<string,object> template,byte[] baseline,byte[] binding)
        {
            bool capacity=template.ContainsKey(UtilityCapacity.Keys[0]);
            int maximum=EncodedLength(template);if(capacity){var maximumCounts=new int[UtilityCapacity.Keys.Length];for(int i=0;i<maximumCounts.Length;i++)maximumCounts[i]=65024;maximum=EncodedLength(UtilityCapacity.Shape(template,maximumCounts));}
            if(bytes==null || bytes.Length<140 || bytes.Length>maximum || (!capacity && bytes.Length!=EncodedLength(template)))throw new InvalidDataException("Utility snapshot length differs.");
            var body=new byte[bytes.Length-32];Array.Copy(bytes,body,body.Length);
            var checksum=new byte[32];Array.Copy(bytes,body.Length,checksum,0,32);
            if(!Equal(Hash(body),checksum))throw new InvalidDataException("Utility snapshot checksum differs.");
            using(var stream=new MemoryStream(body,false))using(var reader=new BinaryReader(stream))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(8))!="HTUTIL01")throw new InvalidDataException("Unknown utility snapshot.");
                int version=reader.ReadInt32();
                SortedDictionary<string,object> layout=null;
                if(version==2 && capacity){var counts=new int[UtilityCapacity.Keys.Length];for(int i=0;i<counts.Length;i++)counts[i]=reader.ReadInt32();layout=UtilityCapacity.Shape(template,counts);}
                else if(version!=1)throw new InvalidDataException("Unsupported utility snapshot version.");
                byte[] schema=reader.ReadBytes(32);
                if(version==1 && !capacity)layout=template;
                if(version==1 && capacity)
                {
                    foreach(int e in new[]{32768,65024})foreach(int w in new[]{32768,65024})foreach(int q in new[]{32768,65024})
                    {var candidate=UtilityCapacity.Shape(template,new[]{e,w,q,q,q,q});if(Equal(schema,Schema(candidate)))layout=candidate;}
                }
                if(layout==null || !Equal(schema,Schema(layout)) || !Equal(reader.ReadBytes(32),baseline) || !Equal(reader.ReadBytes(32),binding))throw new InvalidDataException("Utility schema, standard projection or map binding differs.");
                if(stream.Length-stream.Position!=Payload(layout).Length)throw new InvalidDataException("Utility snapshot length differs from its verified capacity.");
                var result=new SortedDictionary<string,object>(StringComparer.Ordinal);
                foreach(var pair in layout)result.Add(pair.Key,Read(reader,pair.Value.GetType(),pair.Value));
                if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing utility payload.");return result;
            }
        }
        static void Write(BinaryWriter writer,object value,Type type)
        {
            if(BitConverter.IsLittleEndian && (type==typeof(ushort[]) || type==typeof(int[]) || type==typeof(long[])))
            {var array=(Array)value;var bytes=new byte[Buffer.ByteLength(array)];Buffer.BlockCopy(array,0,bytes,0,bytes.Length);writer.Write(bytes);return;}
            if(type==typeof(byte)){writer.Write((byte)value);return;}if(type==typeof(short)){writer.Write((short)value);return;}
            if(type==typeof(ushort)){writer.Write((ushort)value);return;}if(type==typeof(int)){writer.Write((int)value);return;}
            if(type==typeof(uint)){writer.Write((uint)value);return;}if(type==typeof(bool)){writer.Write((byte)((bool)value?1:0));return;}
            if(type==typeof(long)){writer.Write((long)value);return;}
            if(type.IsArray){foreach(var element in (Array)value)Write(writer,element,type.GetElementType());return;}
            if(!type.IsValueType)throw new InvalidOperationException("Unsupported utility snapshot type.");
            foreach(var field in Fields(type))Write(writer,field.GetValue(value),field.FieldType);
        }
        static object Read(BinaryReader reader,Type type,object template)
        {
            if(BitConverter.IsLittleEndian && (type==typeof(ushort[]) || type==typeof(int[]) || type==typeof(long[])))
            {var array=Array.CreateInstance(type.GetElementType(),((Array)template).Length);int count=Buffer.ByteLength(array);var bytes=reader.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();Buffer.BlockCopy(bytes,0,array,0,count);return array;}
            if(type==typeof(byte))return reader.ReadByte();if(type==typeof(short))return reader.ReadInt16();if(type==typeof(ushort))return reader.ReadUInt16();
            if(type==typeof(int))return reader.ReadInt32();if(type==typeof(uint))return reader.ReadUInt32();
            if(type==typeof(long))return reader.ReadInt64();
            if(type==typeof(bool)){byte value=reader.ReadByte();if(value>1)throw new InvalidDataException("Invalid utility boolean.");return value==1;}
            if(type.IsArray)
            {
                Type element=type.GetElementType();var result=Array.CreateInstance(element,((Array)template).Length);
                for(int i=0;i<result.Length;i++)result.SetValue(Read(reader,element,null),i);return result;
            }
            object record=Activator.CreateInstance(type);foreach(var field in Fields(type))field.SetValue(record,Read(reader,field.FieldType,null));return record;
        }
    }
}
