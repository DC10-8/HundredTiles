using System;
using System.IO;
using System.Security.Cryptography;

namespace HundredTiles.Core
{
    public sealed class ResourceState {public byte[] Cells,Cache;}
    public static class ResourceCodec
    {
        public const int EncodedSize=112+ResourceGrid.Cells*12+4000;
        static readonly byte[] Magic={72,84,82,69,83,48,48,49};
        static void Validate(ResourceState state,int side)
        {if(state==null || state.Cells==null || state.Cells.Length!=side*side*12 || state.Cache==null || state.Cache.Length!=(side==512?3240:4000))throw new InvalidDataException("Invalid resource state shape.");}
        public static ResourceState Project(ResourceState state)
        {
            Validate(state,569);
            var legacy=new ResourceState {Cells=new byte[512*512*12],Cache=new byte[3240]};
            for(int z=0;z<512;z++)Array.Copy(state.Cells,z*569*12,legacy.Cells,z*512*12,512*12);
            for(int z=0;z<9;z++)Array.Copy(state.Cache,z*10*40,legacy.Cache,z*9*40,9*40);
            Canonicalize(legacy); return legacy;
        }
        public static void Canonicalize(ResourceState legacy)
        {
            Validate(legacy,512);
            for(int i=0;i<legacy.Cells.Length;i+=12)
            {
                if(legacy.Cells[i+3]==0)legacy.Cells[i+4]=0; else legacy.Cells[i+5]=0;
                legacy.Cells[i+11]=0; // modification flags are not serialized by vanilla
            }
        }
        public static ResourceState Expand(ResourceState legacy)
        {
            Validate(legacy,512);
            var state=new ResourceState {Cells=new byte[ResourceGrid.Cells*12],Cache=new byte[4000]};
            for(int z=0;z<512;z++)Array.Copy(legacy.Cells,z*512*12,state.Cells,z*569*12,512*12);
            for(int z=0;z<9;z++)Array.Copy(legacy.Cache,z*9*40,state.Cache,z*10*40,9*40);
            return state;
        }
        static byte[] Digest(ResourceState legacy)
        {
            using(var hash=SHA256.Create())
            {hash.TransformBlock(legacy.Cells,0,legacy.Cells.Length,legacy.Cells,0);hash.TransformFinalBlock(legacy.Cache,0,legacy.Cache.Length);return hash.Hash;}
        }
        static byte[] Binding(byte[] terrain,byte[] owner)
        {
            if(terrain==null || terrain.Length!=SaveCodec.EncodedSize || owner==null || owner.Length!=Ownership.EncodedSize)throw new InvalidDataException("Resource extension pair missing.");
            using(var hash=SHA256.Create())
            {
                var pair=new byte[64];Array.Copy(hash.ComputeHash(terrain),pair,32);Array.Copy(hash.ComputeHash(owner),0,pair,32,32);
                return hash.ComputeHash(pair);
            }
        }
        public static byte[] Encode(ResourceState state,byte[] terrain,byte[] owner)
        {
            Validate(state,569);
            using(var stream=new MemoryStream(EncodedSize))using(var hash=SHA256.Create())
            {
                var w=new BinaryWriter(stream);w.Write(Magic);w.Write(1);w.Write(569);w.Write(Digest(Project(state)));w.Write(Binding(terrain,owner));
                w.Write(state.Cells);w.Write(state.Cache);w.Flush();w.Write(hash.ComputeHash(stream.ToArray()));w.Flush();return stream.ToArray();
            }
        }
        public static ResourceState Decode(byte[] bytes,ResourceState legacy,byte[] terrain,byte[] owner)
        {
            Validate(legacy,512);
            if(bytes==null || bytes.Length!=EncodedSize)throw new InvalidDataException("Invalid resource extension length.");
            using(var hash=SHA256.Create())using(var stream=new MemoryStream(bytes,false))
            {
                var tail=new byte[32];Array.Copy(bytes,bytes.Length-32,tail,0,32);SaveCodec.RequireSame(tail,hash.ComputeHash(bytes,0,bytes.Length-32));
                var r=new BinaryReader(stream);foreach(byte b in Magic)if(r.ReadByte()!=b)throw new InvalidDataException("Unknown resource format.");
                if(r.ReadInt32()!=1 || r.ReadInt32()!=569)throw new InvalidDataException("Unsupported resource layout.");
                byte[] baseline=r.ReadBytes(32);Canonicalize(legacy);SaveCodec.RequireSame(baseline,Digest(legacy));SaveCodec.RequireSame(r.ReadBytes(32),Binding(terrain,owner));
                var state=new ResourceState {Cells=r.ReadBytes(ResourceGrid.Cells*12),Cache=r.ReadBytes(4000)};Validate(state,569);
                SaveCodec.RequireSame(baseline,Digest(Project(state)));return state;
            }
        }
    }
}
