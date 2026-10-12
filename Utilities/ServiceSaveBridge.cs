using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.SaveBridge;

namespace HundredTiles.Utilities
{
    public static class ServiceSaveBridge
    {
        static readonly string[] Names={"m_localFinalResources","m_localTempResources","m_globalFinalResources","m_globalTempResources","m_totalFinalResources","m_totalTempResources","m_totalTempResourcesMul"};
        static SortedDictionary<string,object> saving;
        static ushort[] readFinal,readTemp;
        static bool read,registered;
        static FieldInfo Field(string name){var field=typeof(ImmaterialResourceManager).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(field==null)throw new MissingFieldException(name);return field;}
        static Array Live(string name){return (Array)Field(name).GetValue(Singleton<ImmaterialResourceManager>.instance);}
        static SortedDictionary<string,object> State(bool clone=true)
        {var state=new SortedDictionary<string,object>(StringComparer.Ordinal);foreach(string name in Names){var array=Live(name);state.Add(name,clone?array.Clone():array);}Validate(state);return state;}
        static void Validate(SortedDictionary<string,object> state)
        {foreach(string name in Names){var array=(Array)state[name];int length=name.StartsWith("m_local",StringComparison.Ordinal)?ServiceGrid.Values:ServiceGrid.Channels;Type type=name=="m_totalTempResourcesMul"?typeof(long[]):name.StartsWith("m_local",StringComparison.Ordinal)?typeof(ushort[]):typeof(int[]);if(array.GetType()!=type || array.Length!=length)throw new InvalidDataException("Service snapshot shape differs: "+name);}}
        static SortedDictionary<string,object> Project(SortedDictionary<string,object> state)
        {var result=new SortedDictionary<string,object>(state,StringComparer.Ordinal);result[Names[0]]=ServiceGrid.Project((ushort[])state[Names[0]]);result[Names[1]]=ServiceGrid.Project((ushort[])state[Names[1]]);return result;}
        static byte[] Baseline(SortedDictionary<string,object> state)
        {return UtilitySnapshotCodec.Hash(UtilitySnapshotCodec.Payload(state));}
        public static void Register(){if(registered)return;RuntimeSaveBridge.RegisterParticipant(new Participant());registered=true;}
        static T Freeze<T>(string name){if(saving==null)throw new InvalidOperationException("Service freeze outside save.");return (T)saving[name];}
        public static ushort[] WriteFinal(ushort[] input){return Freeze<ushort[]>(Names[0]);}
        public static ushort[] WriteTemp(ushort[] input){return Freeze<ushort[]>(Names[1]);}
        public static int[] WriteGlobalFinal(int[] input){return Freeze<int[]>(Names[2]);}
        public static int[] WriteGlobalTemp(int[] input){return Freeze<int[]>(Names[3]);}
        public static int[] WriteTotalFinal(int[] input){return Freeze<int[]>(Names[4]);}
        public static int[] WriteTotalTemp(int[] input){return Freeze<int[]>(Names[5]);}
        public static long[] WriteTotalMul(long[] input){return Freeze<long[]>(Names[6]);}
        public static ushort[] ReadFinal(ushort[] input){if(readFinal==null)throw new InvalidOperationException("Service read outside load.");return readFinal;}
        public static ushort[] ReadTemp(ushort[] input){if(readTemp==null)throw new InvalidOperationException("Service read outside load.");return readTemp;}
        public static void ReadComplete()
        {State(false);Array.Copy(ServiceGrid.Expand(readFinal),Live(Names[0]),ServiceGrid.Values);Array.Copy(ServiceGrid.Expand(readTemp),Live(Names[1]),ServiceGrid.Values);read=true;}
        public sealed class Participant : IChunkedSaveParticipant
        {
            public string Key {get{return "HundredTiles.Services.v1";}}
            public int EncodedSize {get{return ServiceGrid.EncodedSize;}}
            public byte[] Capture(byte[] binding)
            {var state=State();saving=Project(state);var bytes=UtilitySnapshotCodec.Encode(state,Baseline(saving),binding);if(bytes.Length!=EncodedSize)throw new InvalidDataException("Service encoded length differs.");return bytes;}
            public void BeginLoad(){readFinal=new ushort[ServiceGrid.LegacyValues];readTemp=new ushort[ServiceGrid.LegacyValues];read=false;}
            public object Prepare(byte[] bytes,byte[] binding)
            {
                if(!read)throw new InvalidOperationException("Standard service data incomplete.");
                var state=State();var baseline=new SortedDictionary<string,object>(state,StringComparer.Ordinal);baseline[Names[0]]=readFinal;baseline[Names[1]]=readTemp;
                if(bytes==null){state[Names[0]]=ServiceGrid.Expand(readFinal);state[Names[1]]=ServiceGrid.Expand(readTemp);return state;}
                return UtilitySnapshotCodec.Decode(bytes,state,Baseline(baseline),binding);
            }
            public void ValidateTarget(object prepared){Validate((SortedDictionary<string,object>)prepared);foreach(string name in Names)if(Live(name).Length!=((Array)((SortedDictionary<string,object>)prepared)[name]).Length)throw new InvalidDataException("Service target differs.");}
            public void Commit(object prepared){var state=(SortedDictionary<string,object>)prepared;foreach(string name in Names)Array.Copy((Array)state[name],Live(name),Live(name).Length);Singleton<ImmaterialResourceManager>.instance.AreaModified(0,0,499,499);}
            public void ClearSave(){saving=null;}
            public void ClearLoad(){readFinal=readTemp=null;read=false;}
        }
    }
}
