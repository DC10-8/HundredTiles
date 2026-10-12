using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace HundredTiles.Core
{
    public sealed class TransportBackup
    {
        internal readonly Dictionary<string,byte[]> Previous=new Dictionary<string,byte[]>();
        internal readonly HashSet<string> Present=new HashSet<string>();
        public void Restore(IDictionary<string,byte[]> storage)
        {
            foreach(var entry in Previous)
                if(Present.Contains(entry.Key)) storage[entry.Key]=entry.Value;
                else storage.Remove(entry.Key);
        }
    }

    public static class SaveTransport
    {
        // DataSerializer.WriteByteArray rejects sizes >= 16,711,680 bytes.
        public const int ChunkSize=8*1024*1024;
        // DataSerializer rejects byte arrays whose length is at least this value.
        public const int MaximumEntrySize=16711680;
        public const int ChunkCount=(SaveCodec.EncodedSize+ChunkSize-1)/ChunkSize;
        public const int ManifestSize=52;
        static readonly byte[] Magic={72,84,67,72,78,75,48,49}; // HTCHNK01

        // Caller holds the serializable-data storage lock for the whole publish.
        // All allocations and validation finish before touching storage.
        public static TransportBackup Publish(IDictionary<string,byte[]> storage,string key,byte[] bytes)
        {return Publish(storage,key,bytes,SaveCodec.EncodedSize);}
        public static TransportBackup Publish(IDictionary<string,byte[]> storage,string key,byte[] bytes,int expectedSize)
        {
            if(storage==null || string.IsNullOrEmpty(key)) throw new ArgumentException("Storage and key required.");
            if(expectedSize<=0 || bytes==null || bytes.Length!=expectedSize) throw new InvalidDataException("Invalid transport payload length.");
            int chunkCount=(expectedSize-1)/ChunkSize+1;
            var updates=new Dictionary<string,byte[]>();
            using(var stream=new MemoryStream(ManifestSize))
            {
                var writer=new BinaryWriter(stream);
                writer.Write(Magic); writer.Write(1); writer.Write(bytes.Length); writer.Write(chunkCount);
                using(var hash=SHA256.Create()) writer.Write(hash.ComputeHash(bytes));
                writer.Flush(); updates[key]=stream.ToArray();
            }
            for(int i=0;i<chunkCount;i++)
            {
                int length=Math.Min(ChunkSize,bytes.Length-i*ChunkSize);
                var chunk=new byte[length]; Array.Copy(bytes,i*ChunkSize,chunk,0,length);
                updates[ChunkKey(key,i)]=chunk;
            }
            var backup=new TransportBackup();
            foreach(string name in updates.Keys)
            {
                byte[] previous;
                if(storage.TryGetValue(name,out previous)) backup.Present.Add(name);
                backup.Previous[name]=previous;
            }
            try { foreach(var update in updates) storage[update.Key]=update.Value; }
            catch { backup.Restore(storage); throw; }
            return backup;
        }

        // Caller holds storage lock so root and chunks are read as one version.
        public static byte[] Read(IDictionary<string,byte[]> storage,string key)
        {return Read(storage,key,SaveCodec.EncodedSize);}
        public static byte[] Read(IDictionary<string,byte[]> storage,string key,int expectedSize)
        {
            if(storage==null || string.IsNullOrEmpty(key))throw new ArgumentException("Storage and key required.");
            if(expectedSize<=0)throw new ArgumentOutOfRangeException(nameof(expectedSize));
            int chunkCount=(expectedSize-1)/ChunkSize+1;
            byte[] manifest;
            if(!storage.TryGetValue(key,out manifest))
            {
                for(int i=0;i<chunkCount;i++) if(storage.ContainsKey(ChunkKey(key,i))) throw new InvalidDataException("Expanded save manifest is missing.");
                return null;
            }
            if(manifest==null || manifest.Length!=ManifestSize) throw new InvalidDataException("Invalid chunk manifest.");
            byte[] expected;
            using(var stream=new MemoryStream(manifest,false))
            {
                var reader=new BinaryReader(stream);
                foreach(byte b in Magic) if(reader.ReadByte()!=b) throw new InvalidDataException("Unknown chunk manifest.");
                if(reader.ReadInt32()!=1 || reader.ReadInt32()!=expectedSize || reader.ReadInt32()!=chunkCount)
                    throw new InvalidDataException("Unsupported chunk layout.");
                expected=reader.ReadBytes(32);
            }
            var chunks=new byte[chunkCount][];
            for(int i=0;i<chunkCount;i++)
            {
                if(!storage.TryGetValue(ChunkKey(key,i),out chunks[i]) || chunks[i]==null || chunks[i].Length!=Math.Min(ChunkSize,expectedSize-i*ChunkSize))
                    throw new InvalidDataException("Expanded save chunk missing or truncated.");
            }
            var payload=new byte[expectedSize];
            for(int i=0;i<chunkCount;i++) Array.Copy(chunks[i],0,payload,i*ChunkSize,chunks[i].Length);
            using(var hash=SHA256.Create()) SaveCodec.RequireSame(expected,hash.ComputeHash(payload));
            return payload;
        }
        public static string ChunkKey(string key,int index) { return key+".chunk"+index; }
    }
}
