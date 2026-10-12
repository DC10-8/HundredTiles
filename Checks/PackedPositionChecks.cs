using System;
using HundredTiles.Core;

static class PackedPositionChecks
{
    static void Require(bool value, string label)
    { if (!value) throw new Exception(label); }

    public static void Run()
    {
        for (int code = short.MinValue; code <= short.MaxValue; code++)
        {
            var axis = new PackedObjectAxis { Value = (short)code };
            float world = code * PackedObjectPosition.Step;
            Require(PackedObjectPosition.Decode(axis) == world, "Legacy decoding changed");
            Require(PackedObjectPosition.ProjectToLegacy(axis) == code, "Legacy projection changed");
            var encoded = PackedObjectPosition.Encode(world);
            Require(!encoded.PositiveExtension && encoded.Value == Math.Max(-32767, code), "Legacy setter changed");
        }
        for (int code = 32768; code <= PackedObjectPosition.MaximumCode; code++)
        {
            float world = code * PackedObjectPosition.Step;
            var encoded = PackedObjectPosition.Encode(world);
            Require(encoded.PositiveExtension && encoded.Value == unchecked((short)code), "Extension encoding");
            Require(PackedObjectPosition.Decode(encoded) == world, "Extension round trip");
            Require(PackedObjectPosition.ProjectToLegacy(encoded) == short.MaxValue, "Extension projection");
        }
        foreach (int cell in new[] { 32, 64 })
        for (int i = 0; i < 19200 / cell; i++)
        {
            float world = ExpandedMap.WorldMin + (i + 0.5f) * cell;
            Require(PackedObjectPosition.GridCoordinate(PackedObjectPosition.Encode(world), cell) == i, "Spatial cell identity");
        }
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -8641f, 10560f })
        {
            try { PackedObjectPosition.Encode(bad); throw new Exception("Invalid world accepted"); }
            catch (ArgumentOutOfRangeException) { }
        }
        foreach (short bad in new short[] { 0, 32767, -1, -25486 })
        {
            try { PackedObjectPosition.Decode(new PackedObjectAxis { Value = bad, PositiveExtension = true }); throw new Exception("Invalid marker accepted"); }
            catch (ArgumentException) { }
        }
        float last = PackedObjectPosition.Decode(PackedObjectPosition.Encode(10559.999f));
        Require(last < 10560 && Math.Abs(last - 10559.999f) <= PackedObjectPosition.Step, "Exclusive maximum rounding");
        Console.WriteLine("PASS: all 65,536 legacy codes, 8,282 extension codes, 600/300 spatial cells, invalid inputs and exclusive edge rounding.");
        SaveRoundTrip();
    }

    static void SaveRoundTrip()
    {
        var trees = new ObjectPositionRecord[ObjectPositionCodec.TreeCapacity];
        var props = new ObjectPositionRecord[ObjectPositionCodec.PropCapacity];
        foreach (var records in new[] { trees, props })
        for (int i = 1; i < records.Length; i++)
            records[i] = new ObjectPositionRecord { Flags = (ushort)(1 | ((i % 16) << 8)),
                X = PackedObjectPosition.Encode(-8600 + (i % 19000)),
                Z = PackedObjectPosition.Encode(10559 - (i % 19000)) };
        var binding = new byte[32]; binding[31] = 123;
        props[100] = new ObjectPositionRecord { X = new PackedObjectAxis { Value = 123 }, Z = new PackedObjectAxis { Value = -321 } };
        byte[] bytes = ObjectPositionCodec.Encode(trees, props, binding);
        Require(bytes.Length == ObjectPositionCodec.EncodedLength, "Object format size");
        var legacyTrees = ObjectPositionCodec.Project(trees); var legacyProps = ObjectPositionCodec.Project(props);
        Require(legacyProps[100].X.Value == 0 && legacyProps[100].Z.Value == 0, "Released slot vanilla projection");
        ObjectPositionCodec.Decode(bytes, legacyTrees, legacyProps, binding, out var restoredTrees, out var restoredProps);
        foreach (var pair in new[] { (trees, restoredTrees), (props, restoredProps) })
        for (int i = 0; i < pair.Item1.Length; i++)
        {
            var a = pair.Item1[i]; var b = pair.Item2[i];
            Require(a.Flags == b.Flags && a.X.Value == b.X.Value && a.Z.Value == b.Z.Value &&
                a.X.PositiveExtension == b.X.PositiveExtension && a.Z.PositiveExtension == b.Z.PositiveExtension, "Object slot restoration");
        }
        var broken = (byte[])bytes.Clone(); broken[100] ^= 1;
        Reject(broken, legacyTrees, legacyProps, binding);
        var otherBinding = (byte[])binding.Clone(); otherBinding[0] = 1;
        Reject(bytes, legacyTrees, legacyProps, otherBinding);
        legacyProps[42].X.Value++;
        Reject(bytes, legacyTrees, legacyProps, binding);
        Console.WriteLine($"PASS: {trees.Length + props.Length} object slots; {bytes.Length} byte save; corruption, mixed snapshots and legacy mismatch rejected.");
    }

    static void Reject(byte[] bytes, ObjectPositionRecord[] trees, ObjectPositionRecord[] props, byte[] binding)
    {
        try { ObjectPositionCodec.Decode(bytes, trees, props, binding, out _, out _); throw new Exception("Bad object save accepted"); }
        catch (System.IO.InvalidDataException) { }
    }
}
