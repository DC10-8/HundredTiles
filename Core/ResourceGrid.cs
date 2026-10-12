using System;
using System.IO;

namespace HundredTiles.Core
{
    public static class ResourceGrid
    {
        public const int Side=569,LegacySide=512,CellBytes=12,CacheBytes=4000;
        public const int Cells=Side*Side,Cycle=Side*2;
        public static int LegacyOrigin(int resolution) {return LegacySide;}
        public static int Phase(uint frame) {return (int)(frame%Cycle);}
        public static int Row(int phase) {CheckPhase(phase);return phase/2;}
        public static int StartX(int phase) {CheckPhase(phase);return (phase&1)==0?0:Side/2;}
        public static int EndX(int phase) {CheckPhase(phase);return (phase&1)==0?Side/2-1:Side-1;}
        public static int RowBase(int row) {if(row<0 || row>=Side)throw new ArgumentOutOfRangeException();return (row*18>>10)*10*1024;}
        static void CheckPhase(int phase) {if(phase<0 || phase>=Cycle)throw new ArgumentOutOfRangeException();}
        public static double WorldPosition(int cell) {if(cell<0 || cell>=Side)throw new ArgumentOutOfRangeException();return (cell-256+0.5)*33.75;}
        public static T[] Crop<T>(T[] expanded)
        {
            if(expanded==null || expanded.Length!=Cells)throw new InvalidDataException("Expected expanded resource grid.");
            var result=new T[LegacySide*LegacySide];
            for(int z=0;z<LegacySide;z++)Array.Copy(expanded,z*Side,result,z*LegacySide,LegacySide);
            return result;
        }
        public static void Expand<T>(T[] legacy,T[] target)
        {
            if(legacy==null || legacy.Length!=LegacySide*LegacySide || target==null || target.Length!=Cells)throw new InvalidDataException("Invalid resource migration layout.");
            Array.Clear(target,0,target.Length);
            for(int z=0;z<LegacySide;z++)Array.Copy(legacy,z*LegacySide,target,z*Side,LegacySide);
        }
    }
}
