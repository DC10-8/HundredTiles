using System;

namespace HundredTiles.Core
{
    // Planning/migration primitives only. No game managers are patched by this library.
    public static class ExpandedMap
    {
        public const int TileCount = 100;
        public const int GridSize = 10;
        public const int TileSize = 1920;
        public const int RawCellSize = 16;
        public const int RawResolution = 1200;
        public const int HeightStride = RawResolution + 1;
        // Retain the original 9x9 lattice and every existing world coordinate.
        // The extra row/column extend toward positive X and Z.
        public const int WorldMin = -8640;
        public const int WorldMax = WorldMin + GridSize * TileSize;

        public static bool TryGetTile(double x, double z, out int index)
        {
            index = -1;
            if (double.IsNaN(x) || double.IsNaN(z) ||
                x < WorldMin || z < WorldMin || x >= WorldMax || z >= WorldMax)
                return false;
            int tx = (int)Math.Floor((x - WorldMin) / TileSize);
            int tz = (int)Math.Floor((z - WorldMin) / TileSize);
            index = tz * GridSize + tx;
            return true;
        }

        public static int[] MigrateAreaGrid(int[] original)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            int side = original.Length == 25 ? 5 : original.Length == 81 ? 9 : 0;
            if (side == 0) throw new ArgumentException("Expected a 5x5 or 9x9 ownership grid.", nameof(original));
            int offset = side == 5 ? 2 : 0;
            var expanded = new int[TileCount];
            for (int z = 0; z < side; z++)
                Array.Copy(original, z * side, expanded, (z + offset) * GridSize + offset, side);
            return expanded;
        }

        // Edge replication is an explicit prototype policy, not generated terrain.
        // Existing samples remain bit-for-bit unchanged; newly added land is flat
        // along the nearest original edge. Water/surface/resources need separate migration.
        public static ushort[] ExtendHeights(ushort[] original)
        {
            const int oldStride = 1081;
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (original.Length != oldStride * oldStride)
                throw new ArgumentException("Expected a 1081x1081 height grid.", nameof(original));
            var expanded = new ushort[HeightStride * HeightStride];
            for (int z = 0; z < HeightStride; z++)
            {
                int sourceRow = Math.Min(z, oldStride - 1) * oldStride;
                int targetRow = z * HeightStride;
                Array.Copy(original, sourceRow, expanded, targetRow, oldStride);
                ushort edge = original[sourceRow + oldStride - 1];
                for (int x = oldStride; x < HeightStride; x++) expanded[targetRow + x] = edge;
            }
            return expanded;
        }
    }
}
