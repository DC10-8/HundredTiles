using System;
using System.IO;
using System.Security.Cryptography;

namespace HundredTiles.Core
{
    public struct WaterCellState
    {
        public ushort Height;
        public ushort Pollution;
        public short VelocityX;
        public short VelocityZ;
    }

    // Canonical serialized state: raw terrain, block-height differences and the
    // authoritative water buffer. Derived terrain/LOD data must be rebuilt later.
    public sealed class MapSaveState
    {
        public ushort[] Heights;
        public ushort[] BlockOffsets;
        public WaterCellState[] Water;
        public uint WaterFrame;

        public void Validate(int stride)
        {
            int length = checked(stride * stride);
            if (Heights == null || Heights.Length != length || BlockOffsets == null || BlockOffsets.Length != length || Water == null || Water.Length != length)
                throw new InvalidDataException("Terrain/water dimensions disagree.");
        }
    }

    public static class LegacyProjection
    {
        public const int Stride = 1081;

        public static T[] Crop<T>(T[] expanded)
        {
            if (expanded == null || expanded.Length != ExpandedMap.HeightStride * ExpandedMap.HeightStride)
                throw new InvalidDataException("Expected expanded data.");
            var legacy = new T[Stride * Stride];
            for (int z = 0; z < Stride; z++)
                Array.Copy(expanded, z * ExpandedMap.HeightStride, legacy, z * Stride, Stride);
            return legacy;
        }

        public static MapSaveState FromExpanded(MapSaveState expanded)
        {
            expanded.Validate(ExpandedMap.HeightStride);
            var legacy = new MapSaveState { Heights=Crop(expanded.Heights), BlockOffsets=Crop(expanded.BlockOffsets), Water=Crop(expanded.Water), WaterFrame=expanded.WaterFrame };
            Canonicalize(legacy);
            return legacy;
        }

        // Mirror what the vanilla serializer actually retains. Empty-cell
        // pollution is omitted; velocity is retained if the adjacent cell is wet.
        public static void Canonicalize(MapSaveState legacy)
        {
            legacy.Validate(Stride);
            for (int z=0;z<Stride;z++)
            for (int x=0;x<Stride;x++)
            {
                int i = z * Stride + x;
                if (legacy.Heights[i] == 0) legacy.BlockOffsets[i] = 0;
                var cell = legacy.Water[i];
                if (cell.Height == 0)
                {
                    cell.Pollution = 0;
                    if (x == Stride-1 || legacy.Water[i+1].Height == 0) cell.VelocityX = 0;
                    if (z == Stride-1 || legacy.Water[i+Stride].Height == 0) cell.VelocityZ = 0;
                }
                legacy.Water[i] = cell;
            }
        }

        public static MapSaveState Expand(MapSaveState legacy)
        {
            legacy.Validate(Stride);
            return new MapSaveState {
                Heights=ExpandedMap.ExtendHeights(legacy.Heights),
                // Block differences describe buildings/terrain clipping, not
                // geological terrain. Do not duplicate them onto empty new land.
                BlockOffsets=WaterGrid.ExtendBuffer(legacy.BlockOffsets),
                Water=WaterGrid.ExtendBuffer(legacy.Water), WaterFrame=legacy.WaterFrame
            };
        }
    }

    public static class SaveCodec
    {
        public const int Version = 1;
        public const int HeaderSize = 64;
        public const int PayloadSize = ExpandedMap.HeightStride * ExpandedMap.HeightStride * 12;
        public const int EncodedSize = HeaderSize + PayloadSize + 32;
        static readonly byte[] Magic = { 72,84,73,76,69,83,48,49 }; // HTILES01

        public static byte[] Encode(MapSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.Validate(ExpandedMap.HeightStride);
            using (var stream = new MemoryStream(EncodedSize))
            {
                var writer = new BinaryWriter(stream);
                writer.Write(Magic); writer.Write(Version); writer.Write(ExpandedMap.HeightStride);
                writer.Write(ExpandedMap.WorldMin); writer.Write(ExpandedMap.TileSize);
                writer.Write(state.WaterFrame); writer.Write(PayloadSize);
                writer.Write(Digest(LegacyProjection.FromExpanded(state)));
                WriteState(writer,state);
                writer.Flush();
                byte[] bytes = stream.ToArray();
                using (var hash = SHA256.Create()) writer.Write(hash.ComputeHash(bytes));
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static MapSaveState Decode(byte[] bytes, out byte[] legacyDigest)
        {
            legacyDigest = null;
            if (bytes == null || bytes.Length != EncodedSize) throw new InvalidDataException("Invalid expanded save length.");
            using (var hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(bytes,0,bytes.Length-32);
                for (int i=0;i<32;i++) if (digest[i] != bytes[bytes.Length-32+i]) throw new InvalidDataException("Expanded save checksum mismatch.");
            }
            using (var stream = new MemoryStream(bytes,false))
            {
                var reader = new BinaryReader(stream);
                foreach (byte b in Magic) if (reader.ReadByte() != b) throw new InvalidDataException("Unknown expanded save format.");
                if (reader.ReadInt32() != Version || reader.ReadInt32() != ExpandedMap.HeightStride ||
                    reader.ReadInt32() != ExpandedMap.WorldMin || reader.ReadInt32() != ExpandedMap.TileSize)
                    throw new InvalidDataException("Unsupported save version or map layout.");
                uint frame = reader.ReadUInt32();
                if (reader.ReadInt32() != PayloadSize) throw new InvalidDataException("Invalid expanded payload dimensions.");
                legacyDigest = reader.ReadBytes(32);
                int count = ExpandedMap.HeightStride * ExpandedMap.HeightStride;
                var state = new MapSaveState { Heights=new ushort[count],BlockOffsets=new ushort[count],Water=new WaterCellState[count],WaterFrame=frame };
                for (int i=0;i<count;i++)
                {
                    state.Heights[i]=reader.ReadUInt16(); state.BlockOffsets[i]=reader.ReadUInt16();
                    state.Water[i]=new WaterCellState { Height=reader.ReadUInt16(),Pollution=reader.ReadUInt16(),VelocityX=reader.ReadInt16(),VelocityZ=reader.ReadInt16() };
                }
                if (stream.Position != HeaderSize + PayloadSize) throw new InvalidDataException("Expanded payload length mismatch.");
                // Also validate internal linkage before returning decoded state.
                RequireSame(Digest(LegacyProjection.FromExpanded(state)),legacyDigest);
                return state;
            }
        }

        public static byte[] Digest(MapSaveState state)
        {
            using (var hash = SHA256.Create())
            using (var stream = new CryptoStream(Stream.Null,hash,CryptoStreamMode.Write))
            {
                var writer = new BinaryWriter(stream);
                WriteState(writer,state); writer.Flush(); stream.FlushFinalBlock();
                return (byte[])hash.Hash.Clone();
            }
        }

        public static void RequireSame(byte[] expected,byte[] actual)
        {
            if (expected == null || actual == null || expected.Length != 32 || actual.Length != 32)
                throw new InvalidDataException("Invalid legacy fingerprint.");
            int differences=0;
            for (int i=0;i<32;i++) differences |= expected[i] ^ actual[i];
            if (differences != 0) throw new InvalidDataException("Expanded data does not belong to the loaded vanilla terrain/water.");
        }

        static void WriteState(BinaryWriter writer,MapSaveState state)
        {
            for (int i=0;i<state.Heights.Length;i++)
            {
                writer.Write(state.Heights[i]); writer.Write(state.BlockOffsets[i]);
                var cell=state.Water[i]; writer.Write(cell.Height); writer.Write(cell.Pollution); writer.Write(cell.VelocityX); writer.Write(cell.VelocityZ);
            }
        }
    }

    public sealed class LoadSession
    {
        MapSaveState legacy;
        bool terrainRead,waterRead,resolved;

        public LoadSession()
        {
            int size=LegacyProjection.Stride * LegacyProjection.Stride;
            legacy=new MapSaveState { Heights=new ushort[size],BlockOffsets=new ushort[size],Water=new WaterCellState[size] };
        }
        public ushort[] Heights { get { RequireOpen(); return legacy.Heights; } }
        public ushort[] BlockOffsets { get { RequireOpen(); return legacy.BlockOffsets; } }
        public WaterCellState[] Water { get { RequireOpen(); return legacy.Water; } }
        public void TerrainCompleted() { RequireOpen(); if(terrainRead) throw new InvalidOperationException("Terrain read twice."); terrainRead=true; }
        public void WaterCompleted(uint frame) { RequireOpen(); if(waterRead) throw new InvalidOperationException("Water read twice."); legacy.WaterFrame=frame; waterRead=true; }
        public MapSaveState Resolve(byte[] extension)
        {
            RequireOpen();
            if (!terrainRead || !waterRead) throw new InvalidOperationException("Legacy deserialization is not complete.");
            MapSaveState state;
            if (extension == null) state=LegacyProjection.Expand(legacy);
            else
            {
                byte[] fingerprint;
                state=SaveCodec.Decode(extension,out fingerprint);
                LegacyProjection.Canonicalize(legacy);
                SaveCodec.RequireSame(SaveCodec.Digest(legacy),fingerprint);
                if (state.WaterFrame != legacy.WaterFrame) throw new InvalidDataException("Expanded and vanilla water frames disagree.");
            }
            resolved=true;
            return state;
        }
        void RequireOpen() { if(resolved) throw new InvalidOperationException("Load session already committed."); }
    }
}
