using System;
using System.IO;
using ColossalFramework;
using HundredTiles.Core;

namespace HundredTiles.SaveBridge
{
    public static class DistrictSaveBridge
    {
        static DistrictManager.Cell[] saveDistricts,saveParks,readDistricts,readParks;
        static bool read;
        public sealed class Prepared {internal DistrictManager.Cell[] Districts,Parks;}
        internal static byte[] Pack(DistrictManager.Cell[] cells)
        {
            var bytes=new byte[cells.Length*8];
            for(int i=0;i<cells.Length;i++){int k=i*8;var cell=cells[i];bytes[k]=cell.m_district1;bytes[k+1]=cell.m_district2;bytes[k+2]=cell.m_district3;bytes[k+3]=cell.m_district4;bytes[k+4]=cell.m_alpha1;bytes[k+5]=cell.m_alpha2;bytes[k+6]=cell.m_alpha3;bytes[k+7]=cell.m_alpha4;}return bytes;
        }
        internal static DistrictManager.Cell[] Unpack(byte[] bytes)
        {
            var cells=new DistrictManager.Cell[bytes.Length/8];
            for(int i=0;i<cells.Length;i++){int k=i*8;cells[i]=new DistrictManager.Cell{m_district1=bytes[k],m_district2=bytes[k+1],m_district3=bytes[k+2],m_district4=bytes[k+3],m_alpha1=bytes[k+4],m_alpha2=bytes[k+5],m_alpha3=bytes[k+6],m_alpha4=bytes[k+7]};}return cells;
        }
        public static DistrictManager.Cell[] WriteDistricts(DistrictManager.Cell[] original)
        {if(saveDistricts==null)throw new InvalidOperationException("District save missing.");return saveDistricts;}
        public static DistrictManager.Cell[] WriteParks(DistrictManager.Cell[] original)
        {if(saveParks==null)throw new InvalidOperationException("Park save missing.");return saveParks;}
        public static DistrictManager.Cell[] ReadDistricts(DistrictManager.Cell[] original)
        {if(readDistricts==null)throw new InvalidOperationException("District load missing.");return readDistricts;}
        public static DistrictManager.Cell[] ReadParks(DistrictManager.Cell[] original)
        {if(readParks==null)throw new InvalidOperationException("Park load missing.");return readParks;}
        public static void ReadComplete()
        {
            if(readDistricts==null || read)throw new InvalidOperationException("District read stage differs.");
            var manager=Singleton<DistrictManager>.instance;RequireTarget();
            Array.Copy(Unpack(DistrictGrid.Expand(Pack(readDistricts))),manager.m_districtGrid,DistrictGrid.Side*DistrictGrid.Side);
            Array.Copy(Unpack(DistrictGrid.Expand(Pack(readParks))),manager.m_parkGrid,DistrictGrid.Side*DistrictGrid.Side);read=true;
        }
        static void RequireTarget()
        {var manager=Singleton<DistrictManager>.instance;if(manager.m_districtGrid.Length!=DistrictGrid.Side*DistrictGrid.Side || manager.m_parkGrid.Length!=DistrictGrid.Side*DistrictGrid.Side)throw new InvalidDataException("District targets are not expanded.");}
#if HUNDREDTILES_324
        internal sealed class Participant : IChunkedSaveParticipant, IMigratingSaveParticipant
#else
        internal sealed class Participant : ISaveParticipant
#endif
        {
            public int EncodedSize {get{return DistrictGrid.EncodedSize;}}
            public bool CanReadSize(int size) {return DistrictGrid.CanReadSize(size);}
            public string Key {get{return "HundredTiles.Districts.v1";}}
            public byte[] Capture(byte[] binding)
            {
                RequireTarget();var manager=Singleton<DistrictManager>.instance;
                var districts=Pack(manager.m_districtGrid);var parks=Pack(manager.m_parkGrid);
                byte[] bytes=DistrictGrid.Encode(districts,parks,binding);saveDistricts=Unpack(DistrictGrid.Project(districts));saveParks=Unpack(DistrictGrid.Project(parks));return bytes;
            }
            public void BeginLoad() {readDistricts=new DistrictManager.Cell[512*512];readParks=new DistrictManager.Cell[512*512];read=false;}
            public object Prepare(byte[] bytes,byte[] binding)
            {
                if(!read)throw new InvalidOperationException("District standard data incomplete.");
                byte[] districts,parks;
                if(bytes==null){districts=DistrictGrid.Expand(Pack(readDistricts));parks=DistrictGrid.Expand(Pack(readParks));}
                else DistrictGrid.Decode(bytes,Pack(readDistricts),Pack(readParks),binding,out districts,out parks);
                return new Prepared{Districts=Unpack(districts),Parks=Unpack(parks)};
            }
            public void ValidateTarget(object prepared) {RequireTarget();if(!(prepared is Prepared))throw new InvalidDataException("District prepared state missing.");}
            public void Commit(object prepared)
            {var state=(Prepared)prepared;var manager=Singleton<DistrictManager>.instance;Array.Copy(state.Districts,manager.m_districtGrid,state.Districts.Length);Array.Copy(state.Parks,manager.m_parkGrid,state.Parks.Length);}
            public void ClearSave() {saveDistricts=saveParks=null;}
            public void ClearLoad() {readDistricts=readParks=null;read=false;}
        }
    }
}
