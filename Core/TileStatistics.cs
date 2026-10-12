using System;
using System.IO;
using System.Collections.Generic;

namespace HundredTiles.Core
{
    public static class TileStatistics
    {
        public const int ServiceSlots=42;
        // Preserve the original resource-cell classification, changing only
        // the row stride of the derived per-tile cache.
        public static int LegacyResourceTile(int cellX,int cellZ)
        {
            if(cellX<0 || cellX>=512 || cellZ<0 || cellZ>=512) throw new ArgumentOutOfRangeException();
            return (cellZ*18>>10)*10+(cellX*18>>10);
        }
        public static int ServiceIndex(int x,int z,int slot)
        {
            if(x<0 || x>=10 || z<0 || z>=10 || slot<0 || slot>=ServiceSlots) throw new ArgumentOutOfRangeException();
            return (z*10+x)*ServiceSlots+slot;
        }
        public static T[] ProjectCache<T>(T[] expanded)
        {
            if(expanded==null || expanded.Length!=100) throw new InvalidDataException("Expected 100 resource tiles.");
            // The source resource grid still covers the legacy 9x9 world.
            // Reject unexpected outer-cache contents rather than discard data
            // until that source grid has its own expanded save adapter.
            for(int z=0;z<10;z++) for(int x=0;x<10;x++)
                if((x==9 || z==9) && !EqualityComparer<T>.Default.Equals(expanded[z*10+x],default(T)))
                    throw new InvalidDataException("Outer resource cache requires expanded resource-grid persistence.");
            var result=new T[81];
            for(int z=0;z<9;z++) Array.Copy(expanded,z*10,result,z*9,9);
            return result;
        }
        public static void RestoreCache<T>(T[] legacy,T[] expanded)
        {
            if(legacy==null || legacy.Length!=81 || expanded==null || expanded.Length!=100)
                throw new InvalidDataException("Invalid resource cache layout.");
            Array.Clear(expanded,0,100);
            for(int z=0;z<9;z++) Array.Copy(legacy,z*9,expanded,z*10,9);
        }
    }
}
