using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.SaveBridge;

public class DistrictManager
{
    public struct Cell {public byte m_district1,m_district2,m_district3,m_district4,m_alpha1,m_alpha2,m_alpha3,m_alpha4;}
    public Cell[] m_districtGrid=new Cell[1000000],m_parkGrid=new Cell[1000000];
}
static class DistrictFixture {public static DistrictManager.Cell[] Districts,Parks;}
static class DistrictSaveChecks
{
    public static void Run()
    {
        var manager=Singleton<DistrictManager>.instance;
        for(int i=0;i<1000000;i++)
        {
            manager.m_districtGrid[i]=new DistrictManager.Cell{m_district1=(byte)(i%128),m_district2=(byte)((i+1)%128),m_district3=(byte)((i+2)%128),m_district4=(byte)((i+3)%128),m_alpha1=200,m_alpha2=20,m_alpha3=20,m_alpha4=15};
            manager.m_parkGrid[i]=new DistrictManager.Cell{m_district1=(byte)((i*3)%128),m_district2=1,m_district3=2,m_district4=3,m_alpha1=230,m_alpha2=10,m_alpha3=10,m_alpha4=5};
        }
        var expectedDistricts=(DistrictManager.Cell[])manager.m_districtGrid.Clone();var expectedParks=(DistrictManager.Cell[])manager.m_parkGrid.Clone();
        var participant=new DistrictSaveBridge.Participant();byte[] binding=new byte[32];binding[0]=37;
        byte[] bytes=participant.Capture(binding);Check(bytes.Length==DistrictGrid.EncodedSize && bytes.Length<SaveTransport.MaximumEntrySize,"District save fits the game limit");
        var vanillaDistricts=(DistrictManager.Cell[])DistrictSaveBridge.WriteDistricts(null).Clone();var vanillaParks=(DistrictManager.Cell[])DistrictSaveBridge.WriteParks(null).Clone();participant.ClearSave();
        void Read(){participant.BeginLoad();Array.Copy(vanillaDistricts,DistrictSaveBridge.ReadDistricts(null),vanillaDistricts.Length);Array.Copy(vanillaParks,DistrictSaveBridge.ReadParks(null),vanillaParks.Length);DistrictSaveBridge.ReadComplete();}
        Read();var targetDistricts=manager.m_districtGrid;var targetParks=manager.m_parkGrid;
        Check(manager.m_districtGrid[0].m_district1==0 && manager.m_districtGrid[0].m_district2==1 && manager.m_districtGrid[0].m_alpha1==255,"Legacy outer district cell has game default weights");
        var state=participant.Prepare(bytes,binding);participant.ValidateTarget(state);participant.Commit(state);participant.ClearLoad();
        Check(manager.m_districtGrid.SequenceEqual(expectedDistricts) && manager.m_parkGrid.SequenceEqual(expectedParks),"All two million district/park cells restored");
        Check(ReferenceEquals(targetDistricts,manager.m_districtGrid) && ReferenceEquals(targetParks,manager.m_parkGrid),"District restore retains live array identities");
        Read();var broken=(byte[])bytes.Clone();broken[200]^=1;Reject(()=>participant.Prepare(broken,binding),"District corruption accepted");
        var mixed=(byte[])binding.Clone();mixed[1]=1;Reject(()=>participant.Prepare(bytes,mixed),"Mixed district/map binding accepted");
        DistrictSaveBridge.ReadDistricts(null)[0].m_alpha1^=1;Reject(()=>participant.Prepare(bytes,binding),"Mismatched standard districts accepted");participant.ClearLoad();
        Array.Copy(expectedDistricts,manager.m_districtGrid,1000000);Array.Copy(expectedParks,manager.m_parkGrid,1000000);
        Console.WriteLine($"PASS: production district/park save participant; {bytes.Length} bytes; two million cells; frozen 512x512 projections; game-default exterior; live identities; checksum/binding/baseline refusal. Fixture managers, no Unity execution.");
    }
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    static void Reject(Action action,string name){try{action();}catch(InvalidDataException){return;}throw new Exception(name);}
}
