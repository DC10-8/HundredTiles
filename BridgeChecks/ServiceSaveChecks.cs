using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.Utilities;

public class ImmaterialResourceManager
{
    private ushort[] m_localFinalResources=new ushort[ServiceGrid.Values],m_localTempResources=new ushort[ServiceGrid.Values];
    private int[] m_globalFinalResources=new int[35],m_globalTempResources=new int[35],m_totalFinalResources=new int[35],m_totalTempResources=new int[35];
    private long[] m_totalTempResourcesMul=new long[35];
    public Array Get(string name){return (Array)GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this)!;}
    public void AreaModified(int x1,int z1,int x2,int z2){DirtyMax=x2;}
    public int DirtyMax;
    public int UsedFields=>m_localFinalResources.Length+m_localTempResources.Length+m_globalFinalResources.Length+m_globalTempResources.Length+m_totalFinalResources.Length+m_totalTempResources.Length+m_totalTempResourcesMul.Length;
}
static class ServiceFixture
{
    public static ushort[] Final=null!,Temp=null!;
    public static int[] GlobalFinal=null!,GlobalTemp=null!,TotalFinal=null!,TotalTemp=null!;
    public static long[] TotalMul=null!;
    public static void Freeze()
    {Final=(ushort[])ServiceSaveBridge.WriteFinal(null!).Clone();Temp=(ushort[])ServiceSaveBridge.WriteTemp(null!).Clone();GlobalFinal=(int[])ServiceSaveBridge.WriteGlobalFinal(null!).Clone();GlobalTemp=(int[])ServiceSaveBridge.WriteGlobalTemp(null!).Clone();TotalFinal=(int[])ServiceSaveBridge.WriteTotalFinal(null!).Clone();TotalTemp=(int[])ServiceSaveBridge.WriteTotalTemp(null!).Clone();TotalMul=(long[])ServiceSaveBridge.WriteTotalMul(null!).Clone();}
    public static void Read()
    {
        var manager=Singleton<ImmaterialResourceManager>.instance;
        Array.Copy(Final,ServiceSaveBridge.ReadFinal(null!),Final.Length);Array.Copy(Temp,ServiceSaveBridge.ReadTemp(null!),Temp.Length);
        foreach(var row in new[]{("m_globalFinalResources",GlobalFinal),("m_globalTempResources",GlobalTemp),("m_totalFinalResources",TotalFinal),("m_totalTempResources",TotalTemp)})Array.Copy(row.Item2,manager.Get(row.Item1),35);
        Array.Copy(TotalMul,manager.Get("m_totalTempResourcesMul"),35);ServiceSaveBridge.ReadComplete();
    }
}
static class ServiceSaveChecks
{
    public static void Run()
    {
        var manager=Singleton<ImmaterialResourceManager>.instance;
        var final=(ushort[])manager.Get("m_localFinalResources");var temp=(ushort[])manager.Get("m_localTempResources");
        for(int i=0;i<ServiceGrid.Values;i++){final[i]=(ushort)(i*17);temp[i]=(ushort)(i*31);}
        foreach(string name in new[]{"m_globalFinalResources","m_globalTempResources","m_totalFinalResources","m_totalTempResources"}){var values=(int[])manager.Get(name);for(int i=0;i<35;i++)values[i]=i*700+19;}
        var multipliers=(long[])manager.Get("m_totalTempResourcesMul");for(int i=0;i<35;i++)multipliers[i]=(1L<<40)+i;
        var participant=new ServiceSaveBridge.Participant();byte[] binding=new byte[32];binding[0]=41;
        var bytes=participant.Capture(binding);Check(bytes.Length==ServiceGrid.EncodedSize && bytes.Length==35000980,"Service fixed schema size");ServiceFixture.Freeze();participant.ClearSave();
        var storage=new Dictionary<string,byte[]>();var backup=SaveTransport.Publish(storage,participant.Key,bytes,participant.EncodedSize);
        Check(storage.Count==6 && storage.Values.All(value=>value.Length<SaveTransport.MaximumEntrySize),"Five chunks and manifest fit the game entry limit");
        Check(SaveTransport.Read(storage,participant.Key,participant.EncodedSize)!.SequenceEqual(bytes),"Service chunk round trip");
        var missing=storage[SaveTransport.ChunkKey(participant.Key,4)];storage.Remove(SaveTransport.ChunkKey(participant.Key,4));Reject(()=>SaveTransport.Read(storage,participant.Key,participant.EncodedSize),"Missing service chunk accepted");storage[SaveTransport.ChunkKey(participant.Key,4)]=missing;
        storage[SaveTransport.ChunkKey(participant.Key,1)][10]^=1;Reject(()=>SaveTransport.Read(storage,participant.Key,participant.EncodedSize),"Mixed service chunks accepted");storage[SaveTransport.ChunkKey(participant.Key,1)][10]^=1;
        backup.Restore(storage);Check(storage.Count==0,"Service chunk transaction rollback removes manifest and all five chunks");
        void Read(){participant.BeginLoad();ServiceFixture.Read();}
        Read();Check(final[0]==0 && temp[0]==0,"Legacy service exterior is empty");var state=participant.Prepare(bytes,binding);participant.ValidateTarget(state);participant.Commit(state);participant.ClearLoad();
        Check(ReferenceEquals(final,manager.Get("m_localFinalResources")) && manager.DirtyMax==499,"Live service grids retain identities and whole map invalidates");
        var reencoded=participant.Capture(binding);Check(reencoded.SequenceEqual(bytes),"All 17.5 million ushort values and counters including 64-bit products restore exactly");participant.ClearSave();
        Read();var mixed=(byte[])binding.Clone();mixed[1]=1;Reject(()=>participant.Prepare(bytes,mixed),"Mixed service/map binding accepted");
        ServiceSaveBridge.ReadFinal(null!)[0]^=1;Reject(()=>participant.Prepare(bytes,binding),"Mismatched vanilla service snapshot accepted");participant.ClearLoad();
        Read();var broken=(byte[])bytes.Clone();broken[200]^=1;Reject(()=>participant.Prepare(broken,binding),"Corrupt service snapshot accepted");participant.ClearLoad();
        // Restore full state after negative cases for the integrated root save.
        Read();state=participant.Prepare(bytes,binding);participant.Commit(state);participant.ClearLoad();
        Console.WriteLine("PASS: production service participant; 35000980 byte snapshot; 17.5 million values and 64-bit counters; five bounded chunks; frozen projection; live identity; missing/mixed/corrupt data and binding refusal; transaction rollback. Fixtures, no Unity.");
    }
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
    static void Reject(Action action,string name){try{action();}catch(InvalidDataException){return;}throw new Exception(name);}
}
