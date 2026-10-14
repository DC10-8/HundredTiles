using System;

namespace HundredTiles.Core
{
    public static class UtilityGrid
    {
#if HUNDREDTILES_324
        public const int Side = 909;
#else
        public const int Side = 508;
#endif
        public const int Origin = 231, LegacySide = 256, LegacyOrigin = 128;
        public const float CellSize = 38.25f;
        public const int Offset = Origin - LegacyOrigin;
        public static float CellToWorld(int coordinate) { return (coordinate - Origin + 0.5f) * CellSize; }
        public static int WorldToCell(float world)
        {
            if (float.IsNaN(world) || float.IsInfinity(world)) throw new ArgumentOutOfRangeException(nameof(world));
            return Math.Max(0, Math.Min(Side - 1, (int)(world / CellSize + Origin)));
        }
        public static T[] Expand<T>(T[] original)
        {
            if (original == null || original.Length != LegacySide * LegacySide) throw new ArgumentException("Expected the vanilla utility grid.");
            var result = new T[Side * Side];
            for (int z = 0; z < LegacySide; z++) Array.Copy(original, z * LegacySide, result, (z + Offset) * Side + Offset, LegacySide);
            return result;
        }
        public static T[] Project<T>(T[] expanded)
        {
            if (expanded == null || expanded.Length != Side * Side) throw new ArgumentException("Expected the expanded utility grid.");
            var result = new T[LegacySide * LegacySide];
            for (int z = 0; z < LegacySide; z++) Array.Copy(expanded, (z + Offset) * Side + Offset, result, z * LegacySide, LegacySide);
            return result;
        }
        public static int FirstRow(int phase)
        { if (phase < 0 || phase >= 128) throw new ArgumentOutOfRangeException(nameof(phase));
#if HUNDREDTILES_324
          return phase*Side/128;
#else
          return phase*4;
#endif
        }
        public static int LastRow(int phase) {
#if HUNDREDTILES_324
            FirstRow(phase);return (phase+1)*Side/128-1;
#else
            return Math.Min(FirstRow(phase) + 3, Side - 1);
#endif
        }
    }
}
