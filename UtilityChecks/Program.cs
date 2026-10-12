using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.SaveBridge;
using HundredTiles.Utilities;

SerializerChecks.Run(args.Length>0?args[0]:@"C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed\Assembly-CSharp.dll");

UtilitySaveBridge.Register();UtilitySaveBridge.Register();var participant=RuntimeSaveBridge.Participant;
var electric=Singleton<ElectricityManager>.instance;var water=Singleton<WaterManager>.instance;
for(int i=0;i<electric.m_electricityGrid.Length;i++)
{
    electric.m_electricityGrid[i]=new ElectricityManager.Cell{m_conductivity=(byte)(i%3==0?0:200),m_currentCharge=(short)i,m_extraCharge=(ushort)(i*3),m_pulseGroup=(ushort)(i%1024),m_electrified=i%2==0,m_tmpElectrified=true};
    water.m_waterGrid[i]=new WaterManager.Cell{m_conductivity=(byte)(i%3==0?0:190),m_conductivity2=(byte)(i%5==0?0:220),m_currentWaterPressure=(short)i,m_currentSewagePressure=(short)-i,m_currentHeatingPressure=(short)(i*3),m_waterPulseGroup=(ushort)(i%1024),m_sewagePulseGroup=(ushort)(i%1024),m_heatingPulseGroup=(ushort)(i%1024),m_closestPipeSegment=(ushort)(i%32768),m_closestPipeSegment2=(ushort)(i%32768),m_pollution=(byte)i,m_hasWater=true,m_hasSewage=true,m_hasHeating=true,m_tmpHasWater=true,m_tmpHasSewage=true,m_tmpHasHeating=true};
}
for(int i=0;i<1024;i++)
{
    ExpandedElectricityManager.PulseGroups[i]=new ExpandedElectricityManager.ExpandedPulseGroup{m_origCharge=(uint)i,m_curCharge=(uint)(i*7),m_mergeIndex=ushort.MaxValue,m_mergeCount=0,m_x=(ushort)(i%508),m_z=(ushort)(507-i%508)};
    water.m_waterPulseGroups[i]=water.m_sewagePulseGroups[i]=water.m_heatingPulseGroups[i]=new WaterManager.PulseGroup{m_origPressure=(uint)i,m_curPressure=(uint)(i*11),m_collectPressure=(uint)(i*5),m_mergeIndex=ushort.MaxValue,m_node=(ushort)i};
}
for(int i=0;i<32768;i++)
{
    ExpandedElectricityManager.PulseUnits[i]=new ExpandedElectricityManager.ExpandedPulseUnit{m_group=(ushort)(i%1024),m_node=(ushort)(i%32768),m_x=(ushort)(i%508),m_z=(ushort)(507-i%508)};
    ExpandedWaterManager.WaterPulseUnits[i]=ExpandedWaterManager.SewagePulseUnits[i]=ExpandedWaterManager.HeatingPulseUnits[i]=new ExpandedWaterManager.ExpandedPulseUnit{m_group=(ushort)(i%1024),m_node=(ushort)(i%32768),m_x=(ushort)(i%508),m_z=(ushort)(507-i%508)};
    electric.m_nodeGroups[i]=(ushort)(i%1024);water.m_nodeData[i]=new WaterManager.Node{m_waterPulseGroup=(ushort)(i%1024),m_sewagePulseGroup=(ushort)(i%1024),m_heatingPulseGroup=(ushort)(i%1024),m_curWaterPressure=(ushort)i,m_curSewagePressure=(ushort)(i*2),m_curHeatingPressure=(ushort)(i*3),m_collectWaterPressure=(ushort)(i*7),m_collectSewagePressure=(ushort)(i*9),m_extraWaterPressure=(ushort)(i*11),m_extraSewagePressure=(ushort)(i*13),m_extraHeatingPressure=(ushort)(i*17),m_pollution=(byte)i};
}
electric.m_pulseGroupCount=1024;electric.m_pulseUnitStart=32760;electric.m_pulseUnitEnd=23;electric.m_processedCells=999;electric.m_conductiveCells=5000;electric.m_canContinue=true;
water.m_waterPulseGroupCount=water.m_sewagePulseGroupCount=water.m_heatingPulseGroupCount=1024;
water.m_waterPulseUnitStart=water.m_sewagePulseUnitStart=water.m_heatingPulseUnitStart=32760;
water.m_waterPulseUnitEnd=water.m_sewagePulseUnitEnd=water.m_heatingPulseUnitEnd=23;
water.m_processedCells=2000;water.m_conductiveCells=9000;water.m_canContinue=true;
var capture=typeof(UtilitySaveBridge).GetMethod("CaptureState",BindingFlags.Static|BindingFlags.NonPublic);
SortedDictionary<string,object> State()=>(SortedDictionary<string,object>)capture.Invoke(null,null);
var expected=UtilitySnapshotCodec.Hash(UtilitySnapshotCodec.Payload(State()));
byte[] binding=Enumerable.Range(0,32).Select(i=>(byte)i).ToArray();byte[] encoded=participant.Capture(binding);
Check(encoded.Length<16711680,"Utility extension exceeds game byte-array limit");
var projectedElectric=UtilitySaveBridge.Freeze<ElectricityManager.Cell[]>(null,"E.m_electricityGrid");
var projectedWater=UtilitySaveBridge.Freeze<WaterManager.Cell[]>(null,"W.m_waterGrid");
Check(projectedElectric.Length==65536 && projectedWater.Length==65536,"Vanilla grids must remain 256x256");
Check(projectedElectric[0].m_currentCharge==electric.m_electricityGrid[103*508+103].m_currentCharge,"Projection shifts cells instead of changing world coordinates");
var freeze=typeof(UtilitySaveBridge).GetMethod("Freeze");
foreach(string prefix in new[]{"E.","W."})foreach(string name in prefix=="E."?UtilitySaveBridge.ElectricityFields:UtilitySaveBridge.WaterFields)
{
    if(name=="m_electricityGrid" || name=="m_waterGrid")continue;
    object owner=prefix=="E."?(object)electric:water;var field=owner.GetType().GetField(name);
    object value=freeze.MakeGenericMethod(field.FieldType).Invoke(null,new[]{field.GetValue(owner),(object)(prefix+name)});
    if(value is Array array)Array.Copy(array,(Array)field.GetValue(owner),array.Length);else field.SetValue(owner,value);
}
// The game's decoder normalizes each ring queue to start=0.
void Normalize(object owner,string stem)
{
    Type type=owner.GetType();var units=(Array)type.GetField(stem+"Units").GetValue(owner);
    int start=(int)type.GetField(stem+"UnitStart").GetValue(owner),end=(int)type.GetField(stem+"UnitEnd").GetValue(owner);
    int count=(end-start+32768)%32768;var copy=(Array)units.Clone();Array.Clear(units);
    for(int i=0;i<count;i++)units.SetValue(copy.GetValue((start+i)%32768),i);
    type.GetField(stem+"UnitStart").SetValue(owner,0);type.GetField(stem+"UnitEnd").SetValue(owner,count);
}
Normalize(electric,"m_pulse");Normalize(water,"m_waterPulse");Normalize(water,"m_sewagePulse");Normalize(water,"m_heatingPulse");
participant.ClearSave();Array.Clear(ExpandedElectricityManager.PulseGroups);Array.Clear(ExpandedElectricityManager.PulseUnits);Array.Clear(ExpandedWaterManager.WaterPulseUnits);Array.Clear(ExpandedWaterManager.SewagePulseUnits);Array.Clear(ExpandedWaterManager.HeatingPulseUnits);
void Read()
{
    participant.BeginLoad();Array.Copy(projectedElectric,UtilitySaveBridge.ReadElectricity(null),65536);Array.Copy(projectedWater,UtilitySaveBridge.ReadWater(null),65536);
    UtilitySaveBridge.ElectricityRead();UtilitySaveBridge.WaterRead();
}
Read();var rawElectric=electric.m_electricityGrid;var rawWater=water.m_waterGrid;
var prepared=participant.Prepare(encoded,binding);participant.ValidateTarget(prepared);participant.Commit(prepared);
Check(UtilitySnapshotCodec.Hash(UtilitySnapshotCodec.Payload(State())).SequenceEqual(expected),"All utility fields including wrapped queues did not round-trip");
Check(ReferenceEquals(rawElectric,electric.m_electricityGrid) && ReferenceEquals(rawWater,water.m_waterGrid),"Utility restore replaced live arrays");
Check(electric.DirtyMax==507 && water.DirtyMax==507,"Entire utility texture must be invalidated");participant.ClearLoad();
var invalidReference=State();((ExpandedWaterManager.ExpandedPulseUnit[])invalidReference["W.m_waterPulseUnits"])[32760].m_group=1024;
Reject<InvalidDataException>(()=>participant.ValidateTarget(invalidReference),"Active supply group index beyond group count accepted");
var broken=(byte[])encoded.Clone();broken[200]^=1;Read();Reject<InvalidDataException>(()=>participant.Prepare(broken,binding),"Corrupt utilities accepted");
var other=(byte[])binding.Clone();other[0]^=1;Reject<InvalidDataException>(()=>participant.Prepare(encoded,other),"Mixed utility/map snapshots accepted");
UtilitySaveBridge.ReadElectricity(null)[0].m_conductivity^=1;Reject<InvalidDataException>(()=>participant.Prepare(encoded,binding),"Different standard cell baseline accepted");participant.ClearLoad();
Read();var legacy=(SortedDictionary<string,object>)participant.Prepare(null,null);participant.ValidateTarget(legacy);
var legacyUnits=(ExpandedElectricityManager.ExpandedPulseUnit[])legacy["E.m_pulseUnits"];
Check(legacyUnits[0].m_x==electric.m_pulseUnits[0].m_x+103,"Legacy pulse coordinate migration offset differs");
var invalid=(SortedDictionary<string,object>)prepared;((ExpandedElectricityManager.ExpandedPulseUnit[])invalid["E.m_pulseUnits"])[0].m_x=508;
Reject<InvalidDataException>(()=>participant.ValidateTarget(invalid),"Out-of-grid utility queue accepted");participant.ClearLoad();
Console.WriteLine($"PASS: production utility save participant; {encoded.Length} bytes; both 508x508 grids; four wrapped queues; 1024 groups per supply; all node fields and counters; frozen 256x256 projection; legacy axis offset; checksum/binding/baseline/coordinate refusal. Fixture managers, no Unity execution.");
static void Check(bool value,string name){if(!value)throw new Exception(name);}
static void Reject<T>(Action action,string name) where T:Exception {try{action();}catch(T){return;}throw new Exception(name);}
