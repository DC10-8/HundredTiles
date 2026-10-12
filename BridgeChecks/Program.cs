using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.SaveBridge;

ObjectPositionChecks.Run();
ObjectSaveChecks.Run();
DistrictSaveChecks.Run();
ServiceSaveChecks.Run();
HundredTiles.Utilities.ServiceSaveBridge.Register();
var participant=new ParticipantFixture();
RuntimeSaveBridge.RegisterParticipant(participant);
Reject<ArgumentException>(()=>RuntimeSaveBridge.RegisterParticipant(participant),"Duplicate participant refused");
Reject<ArgumentException>(()=>RuntimeSaveBridge.RegisterParticipant(new ParticipantFixture("HundredTiles.Services.v1.chunk0")),"Chunk namespace collision refused");
Reject<ArgumentException>(()=>RuntimeSaveBridge.RegisterParticipant(new InvalidChunkParticipant()),"Invalid fixed chunk layout refused before registration");

var terrain=Singleton<TerrainManager>.instance;
var water=terrain.WaterSimulation;
var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;
var areas=Singleton<GameAreaManager>.instance;
var resources=Singleton<NaturalResourceManager>.instance;
for(int z=0;z<9;z++)for(int x=0;x<9;x++)resources.m_areaResources[z*10+x].m_finalOre=(uint)(z*9+x+1);
var resourceSnapshot=TileStatistics.ProjectCache(resources.m_areaResources);
resources.m_areaResources[99].m_finalOre=1000;resources.m_areaResources[99].m_tempWater=30;
for(int i=0;i<ResourceGrid.Cells;i++)resources.m_naturalResources[i]=new NaturalResourceManager.ResourceCell {
    m_ore=(byte)i,m_oil=(byte)(i>>3),m_sand=(byte)(i>>4),m_forest=(byte)(i%3==0?0:i),m_tree=(byte)(i*3),m_fertility=(byte)(i*5),
    m_pollution=(byte)(i*7),m_water=(byte)(i*11),m_shore=(byte)(i*13),m_burned=(byte)(i*17),m_destroyed=(byte)(i*19),m_modified=(byte)(i%4)};
resources.m_naturalResources[0]=new NaturalResourceManager.ResourceCell {m_ore=20,m_tree=39,m_fertility=7,m_modified=3};
resources.m_naturalResources[ResourceGrid.Cells-1]=new NaturalResourceManager.ResourceCell {m_ore=77,m_forest=31,m_tree=19,m_fertility=5,m_modified=3};
var fullResourceCells=(NaturalResourceManager.ResourceCell[])resources.m_naturalResources.Clone();
for(int z=0;z<512;z++)for(int x=0;x<512;x++)
{
    int patched=((z*18>>10)*(512*20)+18*x)>>10;
    Check(patched==TileStatistics.LegacyResourceTile(x,z),"Every original resource cell retains its tile coordinates with a ten-column stride");
}
var serviceIndices=new HashSet<int>();
for(int z=0;z<10;z++)for(int x=0;x<10;x++)for(int slot=0;slot<42;slot++)
    Check(serviceIndices.Add(TileStatistics.ServiceIndex(x,z,slot)),"Service counters never alias between tiles");
Check(serviceIndices.Count==4200 && serviceIndices.Max()==4199,"Service counter bounds include all 100 tiles");
for(int i=0;i<100;i++) areas.m_areaGrid[i]=i+1;
areas.m_areaCount=100; areas.Start=99;
var fullOwnership=new OwnershipState {Grid=(int[])areas.m_areaGrid.Clone(),Count=100,Start=99};
var legacyOwnership=Ownership.Project(fullOwnership);
int size=1201*1201;
for(int i=0;i<size;i++)
{
    terrain.RawHeights[i]=(ushort)(100+i%200);
    terrain.BlockHeightTargets[i]=20;
    terrain.BlockHeights[i]=(ushort)(30+i%70);
    water.Buffers[0][i]=water.Buffers[1][i]=new WaterSimulation.Cell {m_height=(ushort)(i%5==0?0:200),m_pollution=(ushort)(i%101),m_velocityX=(short)(i%101),m_velocityZ=(short)-(i%101)};
}
var originalHeightPointer=water.HeightPointer;
var originalRawPointer=terrain.RawHeights;
RuntimeSaveBridge.BeginSerialize(new object(),out var metadataScope);
Check(metadataScope==null,"Metadata serialization does not capture a city snapshot");
RuntimeSaveBridge.BeginSerialize(new SimulationManager.Data(),out var scope);
RuntimeSaveBridge.BeginSerialize(new object(),out var nestedScope);
Check(nestedScope==null && RuntimeSaveBridge.EndSerialize(null,nestedScope)==null,"Unrelated nested serialization does not end the city snapshot");
Check(RuntimeSaveBridge.WriteAreas(null).Length==25,"Deferred manager serialization retains the frozen city snapshot");
Check(water.Readers==0,"Save read lock drained");
byte[] originalManifest=storage[RuntimeSaveBridge.DataKey];
byte[] encoded=SaveTransport.Read(storage,RuntimeSaveBridge.DataKey);
byte[] ownerEncoded=storage[RuntimeSaveBridge.OwnershipKey];
byte[] resourceEncoded=storage[RuntimeSaveBridge.ResourceKey];
byte[] objectEncoded=storage[RuntimeSaveBridge.ObjectKey];
byte[] districtEncoded=storage["HundredTiles.Districts.v1"];
var serviceEntries=storage.Where(p=>p.Key.StartsWith("HundredTiles.Services.v1",StringComparison.Ordinal)).ToDictionary(p=>p.Key,p=>p.Value);
DistrictFixture.Districts=(DistrictManager.Cell[])DistrictSaveBridge.WriteDistricts(null).Clone();
DistrictFixture.Parks=(DistrictManager.Cell[])DistrictSaveBridge.WriteParks(null).Clone();
ServiceFixture.Freeze();
byte[] participantEncoded=storage[participant.Key];
var fullTrees=(TreeInstance[])Singleton<TreeManager>.instance.m_trees.m_buffer.Clone();
var fullProps=(PropInstance[])Singleton<PropManager>.instance.m_props.m_buffer.Clone();
ObjectFixture.Trees=(TreeInstance[])ObjectSaveBridge.WriteTrees(null).Clone();
ObjectFixture.Props=(PropInstance[])ObjectSaveBridge.WriteProps(null).Clone();
Check(resourceEncoded.Length==ResourceCodec.EncodedSize && resourceEncoded.Length<16711680,"Resource extension fits the game's byte-array limit");
Check(RuntimeSaveBridge.WriteResourceCells(resources.m_naturalResources)[0].m_tree==0,"Vanilla resource omissions canonicalized");
ResourceFixture.Legacy=(NaturalResourceManager.ResourceCell[])RuntimeSaveBridge.WriteResourceCells(resources.m_naturalResources).Clone();
Check(ownerEncoded.Length==Ownership.EncodedSize,"Small ownership extension size");
Check(RuntimeSaveBridge.WriteAreas(areas.m_areaGrid).SequenceEqual(legacyOwnership.Grid),"Contiguous vanilla ownership projection");
Check(RuntimeSaveBridge.WriteAreaCount(100)==25 && RuntimeSaveBridge.WriteStartTile(99)==legacyOwnership.Start,"Projected count and outside start");
Check(RuntimeSaveBridge.WriteResourceTiles(resources.m_areaResources).Select(c=>c.m_finalOre).SequenceEqual(resourceSnapshot.Select(c=>c.m_finalOre)),"Frozen vanilla resource cache projection");
resources.m_areaResources[0].m_finalOre=999;
Check(RuntimeSaveBridge.WriteResourceTiles(resources.m_areaResources)[0].m_finalOre==1,"Resource cache projection remains frozen");
Reject<InvalidDataException>(()=>Ownership.Decode(ownerEncoded,new OwnershipState{Grid=new int[25],Count=0,Start=12},encoded),"Wrong standard ownership refused");
var wrongPair=(byte[])encoded.Clone(); wrongPair[100]^=1;
Reject<InvalidDataException>(()=>Ownership.Decode(ownerEncoded,legacyOwnership,wrongPair),"Mixed terrain/ownership pair refused");
var duplicate=new OwnershipState {Grid=(int[])fullOwnership.Grid.Clone(),Count=100,Start=99}; duplicate.Grid[99]=1;
Reject<InvalidDataException>(()=>Ownership.Validate(duplicate,10),"Duplicate purchase order refused");
var state=SaveCodec.Decode(encoded,out _);
var legacy=LegacyProjection.FromExpanded(state);
Check(RuntimeSaveBridge.WriteHeights(terrain.RawHeights).SequenceEqual(legacy.Heights),"Frozen standard heights");
Check(RuntimeSaveBridge.WriteBlocks(terrain.BlockHeights).SequenceEqual(legacy.BlockOffsets),"Frozen block offsets");
Check(RuntimeSaveBridge.WriteTargets(terrain.BlockHeightTargets).All(v=>v==0),"Standard target offsets normalized");
var projectedWater=RuntimeSaveBridge.WriteWater(water.Buffers[0]);
Check(projectedWater.Length==1081*1081,"Vanilla water shape");
Check(RuntimeSaveBridge.WriteWaterFrame(999)==state.WaterFrame,"Standard frame frozen to snapshot frame");
Check(RuntimeSaveBridge.EndSave(null,scope)==null,"Save completes");
LoadBaseline(legacy);
Check(terrain.RawHeights[0]==legacy.Heights[0],"Terrain baseline published during deserialization");
RuntimeSaveBridge.BeforeAfterDeserialize();
Check(Singleton<TreeManager>.instance.m_trees.m_buffer.Select(t=>(t.m_posX,t.m_posZ,t.m_flags)).SequenceEqual(fullTrees.Select(t=>(t.m_posX,t.m_posZ,t.m_flags))),"Tree coordinates restored before child aggregation");
Check(Singleton<PropManager>.instance.m_props.m_buffer.Select(p=>(p.m_posX,p.m_posZ,p.m_flags)).SequenceEqual(fullProps.Select(p=>(p.m_posX,p.m_posZ,p.m_flags))),"Prop coordinates restored before child aggregation");
Check(participant.Commits==1,"Additional participant restored in root AfterDeserialize");
RuntimeSaveBridge.BeforeManagersUpdate();
Check(participant.Commits==1,"Manager update fallback does not restore twice");
Check(terrain.RawHeights.SequenceEqual(state.Heights),"Expanded terrain restored");
Check(terrain.BlockHeights.SequenceEqual(state.BlockOffsets),"Canonical block offsets restored before reconstruction");
Check(terrain.BlockHeightTargets.All(v=>v==0),"Derived targets reset before reconstruction");
for(int i=0;i<size;i++)
{
    Check(water.Buffers[0][i].m_height==state.Water[i].Height && water.Buffers[1][i].m_velocityZ==state.Water[i].VelocityZ,"Both live water buffers restored");
}
for(int z=0;z<1201;z++)
for(int x=0;x<1201;x++)
    if(state.Water[z*1201+x].Height!=0)
        Check((water.Mask[WaterGrid.GetMaskCoordinate(z)] & WaterGrid.GetMaskBit(x))!=0,"Water mask rebuilt");
Check(ReferenceEquals(water.HeightPointer,originalHeightPointer) && ReferenceEquals(terrain.RawHeights,originalRawPointer),"Live pointers retained");
Check(terrain.FlatnessRebuilds==1,"Flatness rebuilt once before manager update");
Check(water.Frame==state.WaterFrame,"Water frame restored");
Check(areas.m_areaGrid.SequenceEqual(fullOwnership.Grid) && areas.m_areaCount==100 && areas.Start==99,"Full ownership restored before manager rebuild");
Check(resources.m_areaResources[89].m_finalOre==0 && resources.m_areaResources[80].m_finalOre==73,"Resource cache rows migrate to ten columns without inventing outer resources");
Check(resources.m_areaResources[99].m_finalOre==1000 && resources.m_areaResources[99].m_tempWater==30,"Outer resource aggregates restored");
Check(resources.m_naturalResources[ResourceGrid.Cells-1].m_ore==77 && resources.m_naturalResources[ResourceGrid.Cells-1].m_modified==3,"Outer resource cell and pending modification flags restored");
Check(resources.m_naturalResources[0].m_tree==39,"Full resource state retains fields omitted from vanilla projection");
Check(resources.m_naturalResources.SequenceEqual(fullResourceCells),"All 323761 resource cells and all twelve fields round-trip");
// A failed save must restore the preexisting serialized extension byte array.
water.SetStatus(WaterSimulation.Status.Running);
RuntimeSaveBridge.BeginSave(out var failedScope);
var failure=new InvalidOperationException("Simulated serializer failure");
Check(ReferenceEquals(RuntimeSaveBridge.EndSave(failure,failedScope),failure),"Original save exception preserved");
Check(ReferenceEquals(storage[RuntimeSaveBridge.DataKey],originalManifest) && SaveTransport.Read(storage,RuntimeSaveBridge.DataKey).SequenceEqual(encoded),"Failed save restores manifest and all chunks");
Check(ReferenceEquals(storage[RuntimeSaveBridge.OwnershipKey],ownerEncoded),"Failed save restores ownership too");
Check(ReferenceEquals(storage[RuntimeSaveBridge.ResourceKey],resourceEncoded),"Failed save restores resource extension too");
Check(ReferenceEquals(storage[RuntimeSaveBridge.ObjectKey],objectEncoded),"Failed save restores object extension too");
Check(ReferenceEquals(storage["HundredTiles.Districts.v1"],districtEncoded),"Failed save restores district/park extension too");
Check(serviceEntries.All(p=>ReferenceEquals(storage[p.Key],p.Value)),"Failed root save restores service manifest and all five chunks");
Check(ReferenceEquals(storage[participant.Key],participantEncoded),"Failed save restores registered extension too");
// Every participant validates before any full terrain/object state is committed.
participant.FailValidation=true;LoadBaseline(legacy);
var preflightTerrain=(ushort[])terrain.RawHeights.Clone();
Reject<InvalidDataException>(()=>RuntimeSaveBridge.BeforeAfterDeserialize(),"Additional target failure refused");
Check(terrain.RawHeights.SequenceEqual(preflightTerrain) && participant.Commits==1,"Participant failure precedes all commits");
Check(Singleton<TreeManager>.instance.m_trees.m_buffer.Select(t=>t.m_flags).SequenceEqual(ObjectFixture.Trees.Select(t=>t.m_flags)),"Participant failure leaves vanilla objects untouched");
RuntimeSaveBridge.Reset();participant.FailValidation=false;
var damagedObjects=(byte[])objectEncoded.Clone();damagedObjects[400]^=1;
storage[RuntimeSaveBridge.ObjectKey]=damagedObjects;LoadBaseline(legacy);
Reject<InvalidDataException>(()=>RuntimeSaveBridge.BeforeAfterDeserialize(),"Corrupt object extension refused at root boundary");
Check(terrain.RawHeights.SequenceEqual(preflightTerrain),"Corrupt objects cannot publish expanded terrain");
Check(ReferenceEquals(RuntimeSaveBridge.LoadFailure(failure),failure),"Child failure preserves original exception and resets session");
var beforeRefusedSave=storage.ToDictionary(pair=>pair.Key,pair=>pair.Value);
Reject<InvalidOperationException>(()=>RuntimeSaveBridge.BeginSave(out _),"Saving after a failed load is blocked");
Check(storage.Count==beforeRefusedSave.Count && beforeRefusedSave.All(pair=>ReferenceEquals(storage[pair.Key],pair.Value)),"Failed-load save refusal leaves stored data unchanged");
storage[RuntimeSaveBridge.ObjectKey]=objectEncoded;
storage.Remove(RuntimeSaveBridge.ObjectKey);LoadBaseline(legacy);
Reject<InvalidDataException>(()=>RuntimeSaveBridge.BeforeAfterDeserialize(),"Missing object companion refused");
RuntimeSaveBridge.Reset();storage[RuntimeSaveBridge.ObjectKey]=objectEncoded;
var damagedResources=(byte[])resourceEncoded.Clone();damagedResources[100]^=1;storage[RuntimeSaveBridge.ResourceKey]=damagedResources;
LoadBaseline(legacy);
Reject<InvalidDataException>(()=>RuntimeSaveBridge.BeforeManagersUpdate(),"Corrupt resource extension refused");
Check(resources.m_naturalResources[ResourceGrid.Cells-1].m_ore==0,"Corrupt resource extension not applied");
RuntimeSaveBridge.Reset();storage[RuntimeSaveBridge.ResourceKey]=resourceEncoded;
// Corrupt extension is rejected before changing the published baseline.
var corrupt=(byte[])encoded.Clone();corrupt[100]^=1;SaveTransport.Publish(storage,RuntimeSaveBridge.DataKey,corrupt);
LoadBaseline(legacy);
var baseline=(ushort[])terrain.RawHeights.Clone();
Reject<InvalidDataException>(()=>RuntimeSaveBridge.BeforeManagersUpdate(),"Corrupt extension rejected");
Check(terrain.RawHeights.SequenceEqual(baseline),"Corrupt extension not applied");
RuntimeSaveBridge.Reset();SaveTransport.Publish(storage,RuntimeSaveBridge.DataKey,encoded);
LoadBaseline(legacy);water.SetStatus(WaterSimulation.Status.Running);
Reject<InvalidOperationException>(()=>RuntimeSaveBridge.BeforeManagersUpdate(),"Running water worker refused");
RuntimeSaveBridge.Reset();
// Missing extension migrates an old save without inventing water or buildings.
storage.Clear();LoadBaseline(legacy);
RuntimeSaveBridge.BeforeManagersUpdate();
Check(water.Buffers[0][size-1].m_height==0 && terrain.BlockHeights[size-1]==0,"Legacy expansion starts empty");
Check(terrain.RawHeights[size-1]==legacy.Heights[legacy.Heights.Length-1],"Legacy terrain edge extended");
Check(areas.m_areaCount==25 && areas.m_areaGrid[99]==0,"Legacy-only ownership migrates into central 5x5");
RuntimeSaveBridge.Reset();
storage[RuntimeSaveBridge.OwnershipKey]=null!;
LoadBaseline(legacy);
Reject<InvalidDataException>(()=>RuntimeSaveBridge.BeforeManagersUpdate(),"Null orphan ownership is corrupt, not absent");
RuntimeSaveBridge.Reset(); storage.Clear();
SaveTransport.Publish(storage,RuntimeSaveBridge.DataKey,encoded);
storage[RuntimeSaveBridge.ObjectKey]=objectEncoded;storage[participant.Key]=participantEncoded;
storage["HundredTiles.Districts.v1"]=districtEncoded;
foreach(var pair in serviceEntries)storage[pair.Key]=pair.Value;
LoadBaseline(legacy);
Reject<InvalidDataException>(()=>RuntimeSaveBridge.BeforeManagersUpdate(),"Missing ownership companion refused");
RuntimeSaveBridge.Reset();storage[RuntimeSaveBridge.OwnershipKey]=ownerEncoded;LoadBaseline(legacy);
Reject<InvalidDataException>(()=>RuntimeSaveBridge.BeforeManagersUpdate(),"Missing resource extension refused");
RuntimeSaveBridge.Reset();LoadBaseline(legacy);
var mixedOwner=(byte[])ownerEncoded.Clone();mixedOwner[100]^=1;
Reject<InvalidDataException>(()=>ResourceBridge.Prepare(resourceEncoded,encoded,mixedOwner),"Resource extension bound to ownership snapshot");
RuntimeSaveBridge.Reset(); storage.Clear();
Array.Clear(areas.m_areaGrid); areas.m_areaGrid[44]=1; areas.m_areaCount=1;
var unlock=Singleton<UnlockManager>.instance;
bool purchase=false; unlock.Enabled=false;
AreaHooks.CanUnlock(areas,5,4,ref purchase); Check(purchase,"Adjacent purchase available before milestones");
unlock.Enabled=true; AreaHooks.CanUnlock(areas,5,4,ref purchase); Check(purchase,"Adjacent purchase allowed");
areas.m_AreasWrapper.Veto=true; AreaHooks.CanUnlock(areas,5,4,ref purchase); Check(!purchase,"Wrapper veto retained"); areas.m_AreasWrapper.Veto=false;
for(int i=0;i<9;i++)areas.m_areaGrid[40+i]=i+1;
areas.m_areaCount=9; unlock.Enabled=false;
AreaHooks.CanUnlock(areas,9,4,ref purchase); Check(purchase,"Tenth column purchase has no nonexistent milestone");
AreaHooks.CanUnlock(areas,10,4,ref purchase); Check(!purchase,"Outside grid refused");
AreaHooks.CanUnlock(areas,9,9,ref purchase); Check(!purchase,"Disconnected tile refused");
while(areas.m_areaCount<100)
{
    bool progress=false;
    for(int z=0;z<10;z++)for(int x=0;x<10;x++)
    {
        AreaHooks.CanUnlock(areas,x,z,ref purchase);
        if(purchase) {areas.m_areaGrid[z*10+x]=++areas.m_areaCount;progress=true;}
    }
    Check(progress,"All 100 tiles reachable through adjacent purchases");
}
Ownership.Validate(new OwnershipState{Grid=areas.m_areaGrid,Count=100,Start=44},10);
AreaHooks.CanUnlock(areas,9,9,ref purchase);Check(!purchase,"Full grid cannot purchase again");
Check(AreaHooks.Specifications().All(h=>h.Target!=null && h.Prefix!=null),"Concrete area hook targets available");
Console.WriteLine("PASS: production bridge hooks against mock managers; frozen projections; save rollback; restore before rebuild; both buffers/masks; pointer retention; corrupt extension and running worker refusal; legacy-only migration. No game execution.");
Console.WriteLine("PASS: 100 ownership orders/start restore; 25-tile projection; mixed pair and invalid orders refused; ownership rollback; milestones, adjacency and wrapper veto.");
Console.WriteLine("PASS: resource source-grid extension; outer cells/cache/flags restored; canonical vanilla omissions; resource checksum and transaction rollback; all legacy tile indices and service slots.");
Console.WriteLine("PASS: root AfterDeserialize restores objects before aggregation; manager fallback is idempotent; registered extensions share rollback and preflight; corrupt/missing objects refuse before commits; child failures reset load sessions.");

static void LoadBaseline(MapSaveState legacy)
{
    var terrain=Singleton<TerrainManager>.instance;var water=terrain.WaterSimulation;
    water.SetStatus(WaterSimulation.Status.ReadyForLoad);
    RuntimeSaveBridge.BeginLoad();
    Array.Copy(DistrictFixture.Districts,DistrictSaveBridge.ReadDistricts(null),DistrictFixture.Districts.Length);
    Array.Copy(DistrictFixture.Parks,DistrictSaveBridge.ReadParks(null),DistrictFixture.Parks.Length);
    DistrictSaveBridge.ReadComplete();
    ServiceFixture.Read();
    Array.Copy(ObjectFixture.Trees,Singleton<TreeManager>.instance.m_trees.m_buffer,ObjectFixture.Trees.Length);
    Array.Copy(ObjectFixture.Props,Singleton<PropManager>.instance.m_props.m_buffer,ObjectFixture.Props.Length);
    RuntimeSaveBridge.TreesRead();RuntimeSaveBridge.PropsRead();
    var areas=Singleton<GameAreaManager>.instance;
    var full=new OwnershipState {Grid=Enumerable.Range(1,100).ToArray(),Count=100,Start=99};
    var owner=Ownership.Project(full);
    Array.Copy(owner.Grid,RuntimeSaveBridge.ReadAreas(areas.m_areaGrid),25);
    areas.m_areaCount=owner.Count; areas.Start=owner.Start;
    RuntimeSaveBridge.AreasRead();
    var resources=Singleton<NaturalResourceManager>.instance;
    var cache=RuntimeSaveBridge.ReadResourceTiles(resources.m_areaResources);
    for(int i=0;i<81;i++)cache[i].m_finalOre=(uint)(i+1);
    var raw=RuntimeSaveBridge.ReadResourceCells(resources.m_naturalResources);
    Array.Copy(ResourceFixture.Legacy,raw,raw.Length);
    RuntimeSaveBridge.ResourceTilesRead();
    Array.Copy(legacy.Heights,RuntimeSaveBridge.ReadHeights(terrain.RawHeights),legacy.Heights.Length);
    Array.Copy(legacy.BlockOffsets,RuntimeSaveBridge.ReadBlocks(terrain.BlockHeights),legacy.BlockOffsets.Length);
    RuntimeSaveBridge.TerrainRead();
    var cells=RuntimeSaveBridge.ReadWater(water.Buffers)[0];
    for(int i=0;i<cells.Length;i++) cells[i]=new WaterSimulation.Cell {m_height=legacy.Water[i].Height,m_pollution=legacy.Water[i].Pollution,m_velocityX=legacy.Water[i].VelocityX,m_velocityZ=legacy.Water[i].VelocityZ};
    RuntimeSaveBridge.WaterRead();
}
static void Check(bool condition,string name) {if(!condition)throw new Exception(name);}
static void Reject<T>(Action action,string name) where T:Exception {try {action();}catch(T){return;}throw new Exception(name);}
static class ResourceFixture {public static NaturalResourceManager.ResourceCell[] Legacy=Array.Empty<NaturalResourceManager.ResourceCell>();}
static class ObjectFixture
{
    public static TreeInstance[] Trees=Array.Empty<TreeInstance>();
    public static PropInstance[] Props=Array.Empty<PropInstance>();
}
class ParticipantFixture : ISaveParticipant
{
    readonly string key;
    public ParticipantFixture(string key="HundredTiles.Checks.v1"){this.key=key;}
    public string Key=>key;
    public bool FailValidation;
    public int Commits;
    public byte[] Capture(byte[] binding)=> (byte[])binding.Clone();
    public void BeginLoad() { }
    public object Prepare(byte[] bytes,byte[] binding)
    {
        if(bytes==null && binding==null)return new object();
        if(bytes==null || binding==null || !bytes.SequenceEqual(binding))throw new InvalidDataException("Participant snapshot binding differs.");
        return new object();
    }
    public void ValidateTarget(object prepared) {if(FailValidation)throw new InvalidDataException("Fixture target mismatch.");}
    public void Commit(object prepared) {Commits++;}
    public void ClearSave() { }
    public void ClearLoad() { }
}
sealed class InvalidChunkParticipant : ParticipantFixture,IChunkedSaveParticipant
{
    public InvalidChunkParticipant():base("HundredTiles.InvalidChunk.v1"){}
    public int EncodedSize=>0;
}
