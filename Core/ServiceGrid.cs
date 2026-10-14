using System;
namespace HundredTiles.Core
{
    public static class ServiceGrid
    {
        public const int Side=ExpandedMap.GridSize*50,Origin=225,LegacySide=256,LegacyOrigin=128,Offset=97,Channels=35;
        public const float CellSize=38.4f;
        public const int Values=Side*Side*Channels,LegacyValues=LegacySide*LegacySide*Channels;
        public const int EncodedSize=Values*4+Channels*24+140;
        public static int EncodedSizeFor(int side)
        {if(side!=500 && side!=Side)throw new ArgumentException("Unsupported service dimensions.");return side*side*Channels*4+Channels*24+140;}
        public static ushort[] MigrateHundred(ushort[] source)
        {
            if(source==null || source.Length!=500*500*Channels || Side<500)throw new ArgumentException("100-tile service grid shape differs.");
            var result=new ushort[Values];
            for(int z=0;z<500;z++)Array.Copy(source,z*500*Channels,result,z*Side*Channels,500*Channels);
            return result;
        }
        public static ushort[] Project(ushort[] source)
        {
            if(source==null || source.Length!=Values)throw new ArgumentException("Expanded service grid shape differs.");
            var result=new ushort[LegacyValues];
            for(int z=0;z<LegacySide;z++)Array.Copy(source,((z+Offset)*Side+Offset)*Channels,result,z*LegacySide*Channels,LegacySide*Channels);
            return result;
        }
        public static ushort[] Expand(ushort[] source)
        {
            if(source==null || source.Length!=LegacyValues)throw new ArgumentException("Legacy service grid shape differs.");
            var result=new ushort[Values];
            for(int z=0;z<LegacySide;z++)Array.Copy(source,z*LegacySide*Channels,result,((z+Offset)*Side+Offset)*Channels,LegacySide*Channels);
            return result;
        }
        // Retain a 256-frame statistics cycle; process 1 or 2 complete rows.
        public static int RowStart(uint frame){return (int)(frame&255)*Side/256;}
        public static int RowEnd(uint frame){return ((int)(frame&255)+1)*Side/256;}
    }
}
