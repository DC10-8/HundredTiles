using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HundredTiles.Core
{
    public struct ObjectPositionRecord
    {
        public PackedObjectAxis X, Z;
        public ushort Flags;
    }

    // A proposed extension format; not yet connected to the game serializer.
    public static class ObjectPositionCodec
    {
        public const int TreeCapacity = 262144, PropCapacity = 65536;
        public const int EncodedLength = 84 + (TreeCapacity + PropCapacity) * 7 + 32;
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("HTOBJ001");

        public static byte[] Encode(ObjectPositionRecord[] trees, ObjectPositionRecord[] props, byte[] binding)
        {
            Validate(trees, props, binding);
            using (var stream = new MemoryStream(EncodedLength))
            {
                var writer = new BinaryWriter(stream);
                writer.Write(Magic); writer.Write(1); writer.Write(TreeCapacity); writer.Write(PropCapacity);
                writer.Write(Fingerprint(Project(trees), Project(props)));
                writer.Write(binding);
                Write(writer, trees, false); Write(writer, props, false); writer.Flush();
                writer.Write(Hash(stream.ToArray())); writer.Flush();
                return stream.ToArray();
            }
        }

        public static void Decode(byte[] bytes, ObjectPositionRecord[] legacyTrees, ObjectPositionRecord[] legacyProps,
            byte[] binding, out ObjectPositionRecord[] trees, out ObjectPositionRecord[] props)
        {
            Validate(legacyTrees, legacyProps, binding);
            if (bytes == null || bytes.Length != EncodedLength) throw new InvalidDataException("Object extension size differs.");
            var body = new byte[bytes.Length - 32]; Array.Copy(bytes, body, body.Length);
            var checksum = new byte[32]; Array.Copy(bytes, body.Length, checksum, 0, 32);
            if (!Equal(Hash(body), checksum)) throw new InvalidDataException("Object extension checksum differs.");
            using (var stream = new MemoryStream(body, false))
            {
                var reader = new BinaryReader(stream);
                if (!Equal(reader.ReadBytes(8), Magic) || reader.ReadInt32() != 1 ||
                    reader.ReadInt32() != TreeCapacity || reader.ReadInt32() != PropCapacity)
                    throw new InvalidDataException("Object extension header differs.");
                byte[] expectedLegacy = reader.ReadBytes(32);
                if (!Equal(expectedLegacy, Fingerprint(legacyTrees, legacyProps)) || !Equal(reader.ReadBytes(32), binding))
                    throw new InvalidDataException("Object extension belongs to another snapshot.");
                var preparedTrees = Read(reader, TreeCapacity); var preparedProps = Read(reader, PropCapacity);
                Validate(preparedTrees, preparedProps, binding);
                if (!Equal(expectedLegacy, Fingerprint(Project(preparedTrees), Project(preparedProps))))
                    throw new InvalidDataException("Object extension projection differs.");
                trees = preparedTrees; props = preparedProps;
            }
        }

        public static ObjectPositionRecord[] Project(ObjectPositionRecord[] records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            var result = new ObjectPositionRecord[records.Length];
            for (int i = 0; i < result.Length; i++)
            {
                // Vanilla omits coordinates for released slots and slot zero.
                if (i == 0 || records[i].Flags == 0) continue;
                result[i] = new ObjectPositionRecord { Flags = records[i].Flags,
                    X = new PackedObjectAxis { Value = PackedObjectPosition.ProjectToLegacy(records[i].X) },
                    Z = new PackedObjectAxis { Value = PackedObjectPosition.ProjectToLegacy(records[i].Z) } };
            }
            return result;
        }

        static void Validate(ObjectPositionRecord[] trees, ObjectPositionRecord[] props, byte[] binding)
        {
            if (trees == null || trees.Length != TreeCapacity || props == null || props.Length != PropCapacity ||
                binding == null || binding.Length != 32) throw new ArgumentException("Object snapshot shape differs.");
            foreach (var records in new[] { trees, props })
            for (int i = 0; i < records.Length; i++)
            {
                var record = records[i]; PackedObjectPosition.Decode(record.X); PackedObjectPosition.Decode(record.Z);
                if ((i == 0 || record.Flags == 0) && (record.X.PositiveExtension || record.Z.PositiveExtension))
                    throw new InvalidDataException("Unused object slot has an extension marker.");
            }
        }

        static byte[] Fingerprint(ObjectPositionRecord[] trees, ObjectPositionRecord[] props)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                Write(writer, trees, true); Write(writer, props, false); writer.Flush();
                return Hash(stream.ToArray());
            }
        }

        static void Write(BinaryWriter writer, ObjectPositionRecord[] records, bool normalizeTreeFire)
        {
            foreach (var record in records)
            {
                writer.Write(record.X.Value); writer.Write(record.Z.Value);
                writer.Write(normalizeTreeFire ? (ushort)(record.Flags & ~0xC0) : record.Flags);
                writer.Write((byte)((record.X.PositiveExtension ? 1 : 0) | (record.Z.PositiveExtension ? 2 : 0)));
            }
        }

        static ObjectPositionRecord[] Read(BinaryReader reader, int count)
        {
            var records = new ObjectPositionRecord[count];
            for (int i = 0; i < count; i++)
            {
                short x = reader.ReadInt16(), z = reader.ReadInt16(); ushort flags = reader.ReadUInt16(); byte markers = reader.ReadByte();
                if (markers > 3) throw new InvalidDataException("Unknown coordinate marker.");
                records[i] = new ObjectPositionRecord { Flags = flags,
                    X = new PackedObjectAxis { Value = x, PositiveExtension = (markers & 1) != 0 },
                    Z = new PackedObjectAxis { Value = z, PositiveExtension = (markers & 2) != 0 } };
            }
            return records;
        }

        static byte[] Hash(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
        static bool Equal(byte[] a, byte[] b)
        { if (a.Length != b.Length) return false; int diff = 0; for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i]; return diff == 0; }
    }
}
