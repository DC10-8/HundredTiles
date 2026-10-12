using System;
using System.IO;
using System.Security.Cryptography;

namespace HundredTiles.Core
{
    public sealed class OwnershipState
    {
        public int[] Grid;
        public int Count, Start;
    }

    public static class Ownership
    {
        public const int EncodedSize=516;
        static readonly byte[] Magic={72,84,79,87,78,48,48,49}; // HTOWN001
        public static void Validate(OwnershipState state,int side)
        {
            if(state==null || state.Grid==null || state.Grid.Length!=side*side || state.Start<0 || state.Start>=side*side || state.Count<0 || state.Count>side*side)
                throw new InvalidDataException("Invalid ownership layout.");
            var seen=new bool[state.Count+1]; int count=0;
            foreach(int order in state.Grid)
            {
                if(order<0 || order>state.Count || (order!=0 && seen[order])) throw new InvalidDataException("Invalid purchase order.");
                if(order!=0) {seen[order]=true; count++;}
            }
            if(count!=state.Count) throw new InvalidDataException("Ownership count differs from grid.");
        }
        public static OwnershipState Project(OwnershipState expanded)
        {
            Validate(expanded,10);
            var legacy=new OwnershipState {Grid=new int[25],Start=12};
            // Preserve relative purchase order, removing purchases outside the
            // vanilla 5x5 region. Vanilla requires contiguous order values.
            for(int order=1;order<=expanded.Count;order++)
                for(int z=0;z<5;z++) for(int x=0;x<5;x++)
                    if(expanded.Grid[(z+2)*10+x+2]==order) legacy.Grid[z*5+x]=++legacy.Count;
            int sx=expanded.Start%10-2,sz=expanded.Start/10-2;
            if(sx>=0 && sx<5 && sz>=0 && sz<5) legacy.Start=sz*5+sx;
            else for(int i=0;i<25;i++) if(legacy.Grid[i]==1) {legacy.Start=i; break;}
            return legacy;
        }
        public static OwnershipState Expand(OwnershipState legacy)
        {
            Validate(legacy,5);
            var state=new OwnershipState {Grid=new int[100],Count=legacy.Count,Start=(legacy.Start/5+2)*10+legacy.Start%5+2};
            for(int z=0;z<5;z++) for(int x=0;x<5;x++) state.Grid[(z+2)*10+x+2]=legacy.Grid[z*5+x];
            return state;
        }
        public static OwnershipState ImportLegacy(OwnershipState saved)
        {
            if(saved==null || saved.Grid==null || (saved.Grid.Length!=25 && saved.Grid.Length!=81) || saved.Start<0 || saved.Start>=saved.Grid.Length || saved.Count<0 || saved.Count>100)
                throw new InvalidDataException("Invalid legacy ownership layout: cells="+(saved==null || saved.Grid==null?-1:saved.Grid.Length)+", start="+(saved==null?-1:saved.Start)+", count="+(saved==null?-1:saved.Count)+".");
            int side=saved.Grid.Length==25?5:9;
            int offset=side==5?2:0;
            var seen=new bool[101]; var result=new OwnershipState {Grid=new int[100],Start=(saved.Start/side+offset)*10+saved.Start%side+offset};
            foreach(int order in saved.Grid)
            {
                if(order<0 || order>100 || (order!=0 && seen[order]))throw new InvalidDataException("Invalid legacy purchase order.");
                if(order!=0)seen[order]=true;
            }
            for(int order=1;order<=100;order++)for(int z=0;z<side;z++)for(int x=0;x<side;x++)
                if(saved.Grid[z*side+x]==order)result.Grid[(z+(side==5?2:0))*10+x+(side==5?2:0)]=++result.Count;
            if(saved.Count<result.Count || (side==9 && saved.Count!=result.Count))throw new InvalidDataException("Legacy ownership count differs from grid.");
            Validate(result,10); return result;
        }
        public static bool CanPurchase(OwnershipState state,int x,int z)
        {
            Validate(state,10);
            if(x<0 || x>=10 || z<0 || z>=10 || state.Count>=100 || state.Grid[z*10+x]!=0) return false;
            return (x>0 && state.Grid[z*10+x-1]>0) || (x<9 && state.Grid[z*10+x+1]>0) ||
                (z>0 && state.Grid[(z-1)*10+x]>0) || (z<9 && state.Grid[(z+1)*10+x]>0);
        }
        static byte[] Fingerprint(OwnershipState state)
        {
            using(var stream=new MemoryStream())
            {
                var w=new BinaryWriter(stream); w.Write(state.Start); w.Write(state.Count);
                foreach(int order in state.Grid) w.Write(order); w.Flush();
                using(var hash=SHA256.Create()) return hash.ComputeHash(stream.ToArray());
            }
        }
        public static byte[] Encode(OwnershipState state,byte[] terrainWater)
        {
            Validate(state,10);
            if(terrainWater==null || terrainWater.Length!=SaveCodec.EncodedSize) throw new InvalidDataException("Terrain snapshot missing.");
            using(var stream=new MemoryStream(EncodedSize))
            using(var hash=SHA256.Create())
            {
                var w=new BinaryWriter(stream); w.Write(Magic); w.Write(1); w.Write(state.Start); w.Write(state.Count);
                foreach(int order in state.Grid) w.Write(order);
                w.Write(Fingerprint(Project(state))); w.Write(hash.ComputeHash(terrainWater)); w.Flush();
                w.Write(hash.ComputeHash(stream.ToArray())); w.Flush(); return stream.ToArray();
            }
        }
        public static OwnershipState Decode(byte[] bytes,OwnershipState legacy,byte[] terrainWater)
        {
            Validate(legacy,5);
            if(bytes==null || bytes.Length!=EncodedSize || terrainWater==null || terrainWater.Length!=SaveCodec.EncodedSize)
                throw new InvalidDataException("Ownership extension pair missing or invalid.");
            using(var hash=SHA256.Create())
            using(var stream=new MemoryStream(bytes,false))
            {
                byte[] digest=new byte[32]; Array.Copy(bytes,bytes.Length-32,digest,0,32);
                SaveCodec.RequireSame(digest,hash.ComputeHash(bytes,0,bytes.Length-32));
                var r=new BinaryReader(stream);
                foreach(byte b in Magic) if(r.ReadByte()!=b) throw new InvalidDataException("Unknown ownership data.");
                if(r.ReadInt32()!=1) throw new InvalidDataException("Unsupported ownership version.");
                var state=new OwnershipState {Start=r.ReadInt32(),Count=r.ReadInt32(),Grid=new int[100]};
                for(int i=0;i<100;i++) state.Grid[i]=r.ReadInt32(); Validate(state,10);
                byte[] baseline=r.ReadBytes(32);
                SaveCodec.RequireSame(baseline,Fingerprint(legacy));
                SaveCodec.RequireSame(baseline,Fingerprint(Project(state)));
                SaveCodec.RequireSame(r.ReadBytes(32),hash.ComputeHash(terrainWater));
                return state;
            }
        }
    }
}
