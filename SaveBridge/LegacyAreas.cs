using System;
using System.IO;
using ColossalFramework.IO;
using HundredTiles.Core;

namespace HundredTiles.SaveBridge
{
    // Read-only migration of the 81 Tiles / 81 Tiles 2 fakeGAM format.
    public sealed class LegacyAreas : IDataContainer
    {
        public int[] Grid;
        public void Serialize(DataSerializer serializer)
        {
            if(Grid==null || Grid.Length!=81)throw new InvalidDataException("Invalid 81 Tiles ownership grid.");
            var encoded=EncodedArray.Byte.BeginWrite(serializer);
            foreach(int value in Grid)encoded.Write((byte)value);
            encoded.EndWrite();
        }
        public void Deserialize(DataSerializer serializer)
        {
            if(serializer.version!=1 && serializer.version!=2)throw new InvalidDataException("Unsupported 81 Tiles ownership version.");
            Grid=new int[81]; var encoded=EncodedArray.Byte.BeginRead(serializer);
            for(int i=0;i<Grid.Length;i++)Grid[i]=encoded.Read();
            encoded.EndRead();
        }
        public void AfterDeserialize(DataSerializer serializer) { }
        public static OwnershipState Read(byte[] bytes,OwnershipState baseline)
        {
            if(bytes==null || bytes.Length==0 || bytes.Length>65536)throw new InvalidDataException("Invalid 81 Tiles ownership data.");
            using(var stream=new MemoryStream(bytes,false))
            {
                var data=DataSerializer.Deserialize<LegacyAreas>(stream,DataSerializer.Mode.Memory,delegate(string name){return typeof(LegacyAreas);});
                for(int z=0;z<5;z++)for(int x=0;x<5;x++)if(data.Grid[(z+2)*9+x+2]!=baseline.Grid[z*5+x])throw new InvalidDataException("81 Tiles ownership differs from standard save data.");
                return Ownership.ImportLegacy(new OwnershipState {Grid=data.Grid,Count=baseline.Count,Start=baseline.Start});
            }
        }
    }
}
