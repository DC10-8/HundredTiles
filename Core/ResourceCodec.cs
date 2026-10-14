using System;
using System.IO;
using System.Security.Cryptography;

namespace HundredTiles.Core
{
    public sealed class ResourceState {public byte[] Cells,Cache;}
    public static class ResourceCodec
    {
        public const int EncodedSize=112+ResourceGrid.Cells*12+ResourceGrid.CacheBytes;
        public static int EncodedSizeFor(MapLayout layout){return 112+layout.ResourceSide*layout.ResourceSide*12+layout.TileCount*40;}
        static readonly byte[] Magic={72,84,82,69,83,48,48,49};
        static void Validate(ResourceState state,int side)
        {int cache=side==512?3240:side==569?4000:side==1024?12960:0;if(cache==0 || state==null || state.Cells==null || state.Cells.Length!=side*side*12 || state.Cache==null || state.Cache.Length!=cache)throw new InvalidDataException("Invalid resource state shape.");}
        public static ResourceState Project(ResourceState state)
        {return Project(state,MapLayout.Active);}
        static ResourceState Project(ResourceState state,MapLayout layout)
        {
            Validate(state,layout.ResourceSide);
            var legacy=new ResourceState {Cells=new byte[512*512*12],Cache=new byte[3240]};
            for(int z=0;z<512;z++)Array.Copy(state.Cells,z*layout.ResourceSide*12,legacy.Cells,z*512*12,512*12);
            for(int z=0;z<9;z++)Array.Copy(state.Cache,z*layout.TileSide*40,legacy.Cache,z*9*40,9*40);
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
            var state=new ResourceState {Cells=new byte[ResourceGrid.Cells*12],Cache=new byte[ResourceGrid.CacheBytes]};
            for(int z=0;z<512;z++)Array.Copy(legacy.Cells,z*512*12,state.Cells,z*ResourceGrid.Side*12,512*12);
            for(int z=0;z<9;z++)Array.Copy(legacy.Cache,z*9*40,state.Cache,z*ExpandedMap.GridSize*40,9*40);
            return state;
        }
        static byte[] Digest(ResourceState legacy)
        {
            using(var hash=SHA256.Create())
            {hash.TransformBlock(legacy.Cells,0,legacy.Cells.Length,legacy.Cells,0);hash.TransformFinalBlock(legacy.Cache,0,legacy.Cache.Length);return hash.Hash;}
        }
        static byte[] Binding(byte[] terrain,byte[] owner)
        {
            if(terrain==null || !SaveCodec.IsKnownEncodedSize(terrain.Length) || owner==null || owner.Length!=(terrain.Length==SaveCodec.EncodedSizeFor(MapLayout.Hundred)?516:1412))throw new InvalidDataException("Resource extension pair missing or layouts differ.");
            using(var hash=SHA256.Create())
            {
                var pair=new byte[64];Array.Copy(hash.ComputeHash(terrain),pair,32);Array.Copy(hash.ComputeHash(owner),0,pair,32,32);
                return hash.ComputeHash(pair);
            }
        }
        public static byte[] Encode(ResourceState state,byte[] terrain,byte[] owner)
        {return Encode(state,terrain,owner,MapLayout.Active);}
        public static byte[] Encode(ResourceState state,byte[] terrain,byte[] owner,MapLayout layout)
        {
            Validate(state,layout.ResourceSide);if(terrain==null || terrain.Length!=SaveCodec.EncodedSizeFor(layout))throw new InvalidDataException("Resource terrain layout differs.");
            using(var stream=new MemoryStream(EncodedSizeFor(layout)))using(var hash=SHA256.Create())
            {
                var w=new BinaryWriter(stream);w.Write(Magic);w.Write(layout.TileSide==18?2:1);w.Write(layout.ResourceSide);w.Write(Digest(Project(state,layout)));w.Write(Binding(terrain,owner));
                w.Write(state.Cells);w.Write(state.Cache);w.Flush();w.Write(hash.ComputeHash(stream.ToArray()));w.Flush();return stream.ToArray();
            }
        }
        public static ResourceState Decode(byte[] bytes,ResourceState legacy,byte[] terrain,byte[] owner)
        {
            Validate(legacy,512);
            if(bytes==null || bytes.Length!=EncodedSizeFor(MapLayout.Hundred) && bytes.Length!=EncodedSizeFor(MapLayout.ThreeTwentyFour))throw new InvalidDataException("Invalid resource extension length.");
            var source=bytes.Length==EncodedSizeFor(MapLayout.Hundred)?MapLayout.Hundred:MapLayout.ThreeTwentyFour;
            if(source.TileSide>ExpandedMap.GridSize || terrain==null || terrain.Length!=SaveCodec.EncodedSizeFor(source))throw new InvalidDataException("Resource source/target layouts differ.");
            using(var hash=SHA256.Create())using(var stream=new MemoryStream(bytes,false))
            {
                var tail=new byte[32];Array.Copy(bytes,bytes.Length-32,tail,0,32);SaveCodec.RequireSame(tail,hash.ComputeHash(bytes,0,bytes.Length-32));
                var r=new BinaryReader(stream);foreach(byte b in Magic)if(r.ReadByte()!=b)throw new InvalidDataException("Unknown resource format.");
                int version=r.ReadInt32();if((version!=1 && (version!=2 || source.TileSide!=18)) || r.ReadInt32()!=source.ResourceSide)throw new InvalidDataException("Unsupported resource layout.");
                byte[] baseline=r.ReadBytes(32);Canonicalize(legacy);SaveCodec.RequireSame(baseline,Digest(legacy));SaveCodec.RequireSame(r.ReadBytes(32),Binding(terrain,owner));
                var state=new ResourceState {Cells=r.ReadBytes(source.ResourceSide*source.ResourceSide*12),Cache=r.ReadBytes(source.TileCount*40)};Validate(state,source.ResourceSide);
                SaveCodec.RequireSame(baseline,Digest(Project(state,source)));
                if(source.TileSide==ExpandedMap.GridSize)return state;
                var migrated=new ResourceState{Cells=new byte[ResourceGrid.Cells*12],Cache=new byte[ResourceGrid.CacheBytes]};
                for(int z=0;z<source.ResourceSide;z++)Array.Copy(state.Cells,z*source.ResourceSide*12,migrated.Cells,z*ResourceGrid.Side*12,source.ResourceSide*12);
                for(int z=0;z<source.TileSide;z++)Array.Copy(state.Cache,z*source.TileSide*40,migrated.Cache,z*ExpandedMap.GridSize*40,source.TileSide*40);
                return migrated;
            }
        }
    }
}

