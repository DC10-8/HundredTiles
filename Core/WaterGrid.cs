using System;

namespace HundredTiles.Core
{
    public static class WaterGrid
    {
        public const int MaskResolution = 64;
        // Includes the last vertex at index 1200. 17 cells/bit would overflow
        // the 64-bit mask; C# masks shift counts, silently aliasing distant water.
        public const int CellsPerBit = (ExpandedMap.HeightStride + MaskResolution - 1) / MaskResolution;

        public static int GetMaskCoordinate(int rawCoordinate)
        {
            if (rawCoordinate < 0 || rawCoordinate > ExpandedMap.RawResolution)
                throw new ArgumentOutOfRangeException(nameof(rawCoordinate));
            return rawCoordinate / CellsPerBit;
        }

        public static ulong GetMaskBit(int rawCoordinate)
        {
            return 1UL << GetMaskCoordinate(rawCoordinate);
        }

        public static double RawToWorld(int rawCoordinate)
        {
            if (rawCoordinate < 0 || rawCoordinate > ExpandedMap.RawResolution)
                throw new ArgumentOutOfRangeException(nameof(rawCoordinate));
            return ExpandedMap.WorldMin + rawCoordinate * ExpandedMap.RawCellSize;
        }

        // Keep the original world minimum; replacing width/2 with 9600 shifts
        // water sources by 960m relative to the unchanged old terrain lattice.
        public static double WorldToRaw(double worldCoordinate)
        {
            if (double.IsNaN(worldCoordinate) || double.IsInfinity(worldCoordinate))
                throw new ArgumentOutOfRangeException(nameof(worldCoordinate));
            return (worldCoordinate - ExpandedMap.WorldMin) / ExpandedMap.RawCellSize;
        }

        // Preserve the water state in BOTH simulation buffers at their original
        // coordinates. New cells start empty: copying edge water would create mass.
        // This routine must run before the water thread starts or under a full
        // simulation pause with read locks drained; it does not manage game threads.
        public static T[] ExtendBuffer<T>(T[] original)
        {
            const int stride = 1081;
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (original.Length != stride * stride)
                throw new ArgumentException("Expected a 1081x1081 buffer.", nameof(original));
            var expanded = new T[ExpandedMap.HeightStride * ExpandedMap.HeightStride];
            for (int z = 0; z < stride; z++)
                Array.Copy(original, z * stride, expanded, z * ExpandedMap.HeightStride, stride);
            return expanded;
        }
    }
}
