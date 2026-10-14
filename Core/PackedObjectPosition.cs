using System;

namespace HundredTiles.Core
{
    // Coordinate primitive only. Extension markers must be saved separately;
    // no game flag bits are reserved or modified by this implementation.
    public struct PackedObjectAxis
    {
        public short Value;
        public bool PositiveExtension;
    }

    public static class PackedObjectPosition
    {
        public const float Step = 0.263671875f;
        public const float LegacyEncodeScale = 3.7925925f;
#if HUNDREDTILES_324
        public const int MaximumCode = 98303;
#else
        public const int MaximumCode = 40049;
#endif

        public static PackedObjectAxis Encode(float world)
        {return Encode(world,MapLayout.Active);}
        public static PackedObjectAxis Encode(float world,MapLayout layout)
        {
            if(layout==null)throw new ArgumentNullException(nameof(layout));
            if (float.IsNaN(world) || float.IsInfinity(world) ||
                world < layout.WorldMin || world >= layout.WorldMax)
                throw new ArgumentOutOfRangeException(nameof(world));
            // Match Mathf.RoundToInt's nearest-even rounding and the original
            // negative clamp. Existing gameplay positions retain their precision.
            int code = (int)Math.Round((double)(world * LegacyEncodeScale), MidpointRounding.ToEven);
            code = Math.Max(-32767, Math.Min(layout.MaximumObjectCode, code));
            return new PackedObjectAxis
            {
                Value = unchecked((short)code),
                PositiveExtension = code > short.MaxValue
            };
        }

        public static float Decode(PackedObjectAxis axis)
        {return Decode(axis,MapLayout.Active);}
        public static float Decode(PackedObjectAxis axis,MapLayout layout)
        {
            if(layout==null)throw new ArgumentNullException(nameof(layout));
            int code = axis.Value;
            if (axis.PositiveExtension)
            {
                code += 65536;
                if (code < 32768 || code > layout.MaximumObjectCode)
                    throw new ArgumentException("Invalid positive extension coordinate.", nameof(axis));
            }
            return code * Step;
        }

        public static short ProjectToLegacy(PackedObjectAxis axis)
        {
            Decode(axis); // Reject malformed extension data before projection.
            return axis.PositiveExtension ? short.MaxValue : axis.Value;
        }

        public static int GridCoordinate(PackedObjectAxis axis, int cellSize)
        {
            if (cellSize != 32 && cellSize != 64)
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            int side = ExpandedMap.GridSize * ExpandedMap.TileSize / cellSize;
            int coordinate = (int)Math.Floor((Decode(axis) - ExpandedMap.WorldMin) / cellSize);
            return Math.Max(0, Math.Min(side - 1, coordinate));
        }
    }
}
