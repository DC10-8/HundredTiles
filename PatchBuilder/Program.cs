using System.Security.Cryptography;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TypeDefinition = Mono.Cecil.TypeDefinition;

if (args.Length < 2) throw new ArgumentException("Usage: <game assembly> <plan output> [dump Type.Method | net-audit report.csv | building-audit report.csv | rendering-audit report.csv]");
using var module = Mono.Cecil.ModuleDefinition.ReadModule(args[0]);
if (args.Length == 4 && args[2] == "dump")
{
    foreach (var type in AllTypes(module.Types))
    foreach (var method in type.Methods.Where(m => m.HasBody && TypeName(type) + "." + m.Name == args[3]))
    {
        Console.WriteLine(method.FullName);
        foreach (var instruction in method.Body.Instructions)
            Console.WriteLine($"{method.Body.Instructions.IndexOf(instruction)}\t{instruction}");
    }
    return;
}
const string reviewedAssemblyHash = "DF557994A03C93D8BB4A14F80B1FBCC0FB29D547658442445D078D069BCDD9FE";
if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))) != reviewedAssemblyHash)
    throw new InvalidOperationException("Assembly differs from the reviewed game build. Regenerating a trusted plan is refused.");
if(args.Length==4 && args[2]=="packed-audit")
{
    var rows=new List<string>{"type,method,token,axis_reads"};
    foreach(var type in AllTypes(module.Types))foreach(var method in type.Methods.Where(m=>m.HasBody))
    {
        var reads=method.Body.Instructions.Where(i=>i.Operand is FieldReference f && (f.DeclaringType.Name=="TreeInstance" || f.DeclaringType.Name=="PropInstance") && (f.Name=="m_posX" || f.Name=="m_posZ") && i.OpCode.Code==Code.Ldfld).Select(i=>((FieldReference)i.Operand).FullName).ToArray();
        if(reads.Length!=0)rows.Add(string.Join(',',new[]{TypeName(type),method.Name,method.MetadataToken.ToInt32().ToString("X8"),string.Join(';',reads)}.Select(v=>"\""+v.Replace("\"","\"\"")+"\"")));
    }
    File.WriteAllLines(args[3],rows);Console.WriteLine("Packed-axis read audit: "+(rows.Count-1)+" bodies.");return;
}
if(args.Length==4 && args[2]=="spatial-audit")
{
    var owners=new Dictionary<string,string[]>{["DistrictManager"]=new[]{"m_districtGrid","m_parkGrid"},["ZoneManager"]=new[]{"m_zoneGrid"},["VehicleManager"]=new[]{"m_vehicleGrid","m_vehicleGrid2","m_parkedGrid"},["CitizenManager"]=new[]{"m_citizenGrid"},["TreeManager"]=new[]{"m_treeGrid"},["PropManager"]=new[]{"m_propGrid"}};
    var rows=new List<string>{"owner,type,method,token,fields,constants"};
    foreach(var type in AllTypes(module.Types))foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var owner in owners)
    {
        var fields=method.Body.Instructions.Where(i=>i.Operand is FieldReference f && f.DeclaringType.Name==owner.Key && owner.Value.Contains(f.Name)).Select(i=>((FieldReference)i.Operand).Name).Distinct().ToArray();
        if(owner.Key=="VehicleManager" || owner.Key=="CitizenManager")fields=fields.Concat(method.Body.Instructions.Where(i=>i.Operand is MethodReference m && m.DeclaringType.Name==owner.Key && (m.Name=="AddToGrid" || m.Name=="RemoveFromGrid")).Select(i=>"call:"+((MethodReference)i.Operand).Name)).Distinct().ToArray();
        if(fields.Length==0)continue;
        var values=method.Body.Instructions.Where(i=>IntConstant(i) is int v && new[]{512,511,510,256,262144,65536,150,149,22500,540,539,54,53,291600,2916,2160,2159,4665600,270,269,72900,45,44}.Contains(v) || i.OpCode.Code==Code.Ldc_R4 && new[]{256f,9830.4f,4915.2f,75f,1080f,270f,27f,22.5f}.Contains((float)i.Operand)).Select(i=>i.Operand?.ToString()??i.OpCode.Name);
        rows.Add(string.Join(',',new[]{owner.Key,TypeName(type),method.Name,method.MetadataToken.ToInt32().ToString("X8"),string.Join(';',fields),string.Join(';',values)}.Select(v=>"\""+v.Replace("\"","\"\"")+"\"")));
    }
    File.WriteAllLines(args[3],rows);Console.WriteLine($"Remaining spatial audit: {rows.Count-1} manager/consumer entries.");return;
}
if(args.Length==4 && args[2]=="rendering-audit")
{
    var planned=File.ReadLines(args[1]).Where(l=>l.StartsWith("method\t")).Select(l=>l.Split('\t')[1]).ToHashSet();
    var callbacks=new HashSet<string>(new[]{"EndRenderingImpl","CalculateGroupData","PopulateGroupData"});
    var rows=new List<string>{"type,method,token,legacy_constants,plan_status"};
    foreach(var type in AllTypes(module.Types))foreach(var method in type.Methods.Where(m=>m.HasBody))
    {
        bool callback=callbacks.Contains(method.Name);
        bool update=method.Body.Instructions.Any(i=>i.Operand is MethodReference call && call.DeclaringType.Name=="RenderManager" && (call.Name=="UpdateGroup" || call.Name=="UpdateMegaGroup" || call.Name=="UpdateMegaGroupLayers"));
        if(!callback && !update)continue;
        var constants=method.Body.Instructions.Select(IntConstant).Where(v=>v==45 || v==44 || v==270 || v==269 || v==540 || v==539).Select(v=>v.ToString()).ToArray();
        if(constants.Length==0)continue;
        string token=method.MetadataToken.ToInt32().ToString("X8");
        rows.Add(string.Join(',',new[]{TypeName(type),method.Name,token,string.Join(';',constants),planned.Contains(token)?"partial-reviewed-plan":"requires-review"}.Select(v=>"\""+v.Replace("\"","\"\"")+"\"")));
    }
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);File.WriteAllLines(args[3],rows);
    Console.WriteLine($"Rendering dependency candidates: {rows.Count-1} bodies. Numeric matches are candidates, not approved edits.");return;
}
if(args.Length==4 && (args[2]=="net-audit" || args[2]=="building-audit"))
{
    string owner=args[2]=="net-audit"?"NetManager":"BuildingManager";
    string[] fields=owner=="NetManager"?new[]{"m_nodeGrid","m_segmentGrid"}:new[]{"m_buildingGrid","m_buildingGrid2"};
    var planned=File.ReadLines(args[1]).Where(l=>l.StartsWith("method\t")).Select(l=>l.Split('\t')[1]).ToHashSet();
    var rows=new List<string>{"type,method,token,grid_accesses,grid_constants,plan_status"};
    foreach(var type in AllTypes(module.Types))
    foreach(var method in type.Methods.Where(m=>m.HasBody))
    {
        int accesses=method.Body.Instructions.Count(i=>i.Operand is FieldReference f && f.DeclaringType.Name==owner && fields.Contains(f.Name));
        if(accesses==0)continue;
        string token=method.MetadataToken.ToInt32().ToString("X8");
        var constants=method.Body.Instructions.Select(IntConstant).Where(v=>v==270 || v==269 || v==72900 || owner=="BuildingManager" && (v==45 || v==44 || v==2025)).Select(v=>v.ToString()).ToArray();
        string status=planned.Contains(token)?type.Name==owner?"partial-reviewed-plan":"reviewed-spatial-query":TypeName(type)==owner+"+Data" && method.Name=="Deserialize"?"reviewed-dynamic-rebuild":"unconnected";
        rows.Add(string.Join(',',new[]{TypeName(type),method.Name,token,accesses.ToString(),string.Join(';',constants),status}.Select(v=>"\""+v.Replace("\"","\"\"")+"\"")));
    }
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
    File.WriteAllLines(args[3],rows);
    Console.WriteLine($"{owner} spatial dependency audit: {rows.Count-1} method bodies. Selected plan entries do not imply complete rendering or caller coverage.");
    return;
}
using var peStream = File.OpenRead(args[0]);
using var pe = new PEReader(peStream);
var metadata = pe.GetMetadataReader();
var netConsumers=new HashSet<string>(new[]{
    "AirportAuxBuildingAI.FindRoadAccess","AirportCargoGateAI.FindRoadAccess","AirportGateAI.FindRoadAccess",
    "CampusBuildingAI.FindRoadAccess","CommonBuildingAI.FindRoadAccess","CommonBuildingAI.FindEventAccess",
    "MuseumAI.FindRoadAccess","ParkBuildingAI.FindRoadAccess","PlayerBuildingAI.FindRoadAccess",
    "RaceBuildingAI.FindRoadAccess","RaceStandAI.FindEventRoad","Building.FindAccessLane","Building.FindParentNode",
    "BuildingTool.SnapToCanal","BuildingTool.SnapToPath","DisasterHelpers.DestroyNetSegments",
    "DistrictManager.MoveParkSegments","DistrictManager.MoveParkNodes","EventManager.UpdateProp",
    "CoverageManager.FindFireCoverage","ElectricityManager.ConductToNodes","WaterManager.UpdateGrid",
    "CanalAI.CheckBuildPosition","CargoPathAI.UpdateLanes","ConcourseBaseAI.UpdateLanes","FlightPathAI.UpdateLanes",
    "MetroTrackBaseAI.UpdateLanes","PedestrianBridgeAI.UpdateLanes","PedestrianPathAI.UpdateLanes",
    "PedestrianTunnelAI.UpdateLanes","PedestrianWayAI.UpdateLanes","RoadBaseAI.UpdateLanes","ShipPathAI.UpdateLanes",
    "TaxiwayAI.UpdateLanes","TrainTrackBaseAI.UpdateLanes","TransportPathAI.UpdateLanes",
    "NetTool.RefreshHelperLines","PathManager.FindPathPosition","TransportTool.GetLineCount","ZoneBlock.CalculateBlock1"
});
var buildingConsumers=new HashSet<string>(new[]{
    "CommonBuildingAI.HandleFireSpread","PrivateBuildingAI.CheckNearbyBuildingZones","WildlifeAI.IsFreePosition",
    "StructureCollapseAI.FindClosestBuilding","StructureFireAI.FindClosestBuilding","DisasterHelpers.DestroyBuildings",
    "DisasterHelpers.UpgradeBuildings","DistrictManager.MoveParkBuildings","EventManager.UpdateBuildings","EventManager.UpdateStands",
    "ElectricityManager.UpdateGrid","WeatherManager.FindStrikeTarget","RoadBaseAI.CheckBuildings","TourLineAI.TourCoverageChanged",
    "NetNode.FindOwnerBuilding","NetSegment.FindOwnerBuilding","NetTool.RenderOverlay","NotificationManager.AddWaveEvent",
    "TreeManager.HandleFireSpread","BankVanAI.TryCollectCash","BlimpAI.GetFlightHeight","GarbageTruckAI.TryCollectGarbage",
    "HelicopterAI.GetFlightHeight","PassengerCarAI.FindParkingSpaceBuilding","PassengerHelicopterAI.GetFlightHeight",
    "PoliceCarAI.TryCollectCrime","PoliceCopterAI.TryCollectCrime","PostVanAI.TryCollectMail",
    "ZoneBlock.CalculateBlock3","ZoneBlock.IsGoodPlace"
});
// Explicit per-method semantics. No global numeric substitutions.
var selectors = new Dictionary<string, Dictionary<int,int>> {
    ["TerrainManager.Awake"] = new() { [1168561]=1442401,[1166400]=1440000,[3505684]=4327204,[1080]=1200,[1081]=1201,[81]=100,[9]=10 },
    ["TerrainManager.OnDestroy"] = new() { [9]=10 },
    ["TerrainManager.EndRenderingImpl"] = new() { [81]=100,[9]=10 },
    ["TerrainManager.BeginOverlayImpl"] = new() { [9]=10 },
    ["TerrainManager.GetSurfaceMapping"] = new() { [8]=9,[9]=10 },
    ["TerrainManager.GetHeightMapping"] = new() { [8]=9,[9]=10 },
    ["TerrainManager.GetWaterMapping"] = new() { [8]=9,[9]=10 },
    ["TerrainManager.GetBounds"] = new() { [9]=10 },
    ["TerrainManager.UpdateBounds"] = new() { [144]=160,[1080]=1200,[1081]=1201 },
    ["TerrainPatch..ctor"] = new() { [1080]=1200 },
    ["TerrainPatch.Refresh"] = new() { [1080]=1200,[1081]=1201,[1079]=1199,[4319]=4799,[4320]=4800,[9]=10 },
    ["WaterSimulation.Awake"] = new() { [1080]=1200,[81]=100,[17]=19 },
    ["WaterSimulation.UpdatePatch"] = new() { [9]=10,[540]=600 },
    ["WaterSimulation.SimulateWater"] = new() { [1080]=1200,[81]=100,[9]=10,[540]=600 },
    ["WaterSimulation.FillHeightMap"] = new() { [1080]=1200 },
    ["WaterSimulation.FillSurfaceMap"] = new() { [1080]=1200 },
    ["TerrainManager.RefreshPatchFlatness"] = new() { [9]=10,[1080]=1200,[1081]=1201,[1082]=1202 },
    ["TerrainManager.UpdateData"] = new() { [9]=10,[1081]=1201 },
    ["TerrainManager.LateUpdateData"] = new() { [80]=99 },
    ["TerrainManager+Data.AfterDeserialize"] = new() { [9]=10 },
    ["TerrainManager+<UpdateTerrainPatch>c__Iterator0.MoveNext"] = new() { [9]=10 },
    ["WaterSimulation+Data.AfterDeserialize"] = new() { [1080]=1200 }
};
// Reviewed terrain queries use raw-grid indices; world-space origins (540f,
// 2160f and 8640f) remain unchanged because expansion is on the positive axes.
foreach (string name in new[] {
    "CalculateWaterProximity", "CalculateAreaHeight", "SampleRawHeight",
    "SampleFinalHeight", "SampleBlockHeight", "SampleRawHeightSmooth",
    "SampleOriginalRawHeightSmooth", "SampleFinalHeightSmooth", "SampleBlockHeightSmooth",
    "SampleRawHeightSmoothWithWater", "SampleBlockHeightSmoothWithWater",
    "SampleRawHeightWithWater", "GetSurfaceHeight", "GetSurfaceHeight2",
    "HasWater", "WaterLevel", "SampleWaterData", "CountWaterCoverage",
    "GetClosestWaterPos", "GetShorePos", "CalculateWaterFlow", "SimulationStepImpl"
}) selectors.Add("TerrainManager." + name, new() { [1080]=1200, [1081]=1201 });
foreach (string name in new[] {
    "GetZoneCell", "GetSurfaceCell", "GetPatchIndex", "HasDetailMapping",
    "GetDetailHeight", "GetBlockHeight", "GetDetailHeightSmooth",
    "SampleDetailHeight", "SampleDetailHeightSmooth"
}) selectors.Add("TerrainManager." + name, new() { [8]=9, [9]=10, [4320]=4800 });
selectors.Add("TerrainManager.SetDetailedPatch", new() { [9]=10 });
selectors.Add("TerrainManager.SampleDetailSurface", new() { [4320]=4800 });
selectors.Add("TerrainManager.SampleRawSurface", new() { [2211839]=2457599, [1079]=1199, [1080]=1200 });
selectors.Add("TerrainModify.UpdateAreaImplementation", new() { [9]=10, [1080]=1200, [1081]=1201 });
selectors.Add("TerrainModify.ApplyQuad", new() { [1081]=1201 });
foreach(string name in new[]{"ResetUndoBuffer","OnEnable","EndStroke","ApplyUndo","ApplyBrush"})
    selectors.Add("TerrainTool."+name,new(){[1080]=1200,[1081]=1201});
selectors.Add("GameAreaManager.Awake",new() { [25]=100,[12]=44,[8]=16 });
foreach(string name in new[]{"IsUnlocked","GetStartTile","SetStartTile","GetTileIndex","GetTileXZ","GetAreaIndex","GetAreaPositionSmooth","UpdateData","UnlockArea"})
    selectors.Add("GameAreaManager."+name,new() { [5]=10 });
selectors["GameAreaManager.GetTileXZ"].Add(4,9);
selectors.Add("GameAreaManager.GetAreaBounds",new());
selectors.Add("GameAreaManager.UpdateAreaTexture",new() { [8]=15,[5]=10 });
selectors.Add("GameAreaManager.GetFreeBounds",new() { [5]=10 });
selectors.Add("GameAreaManager.BeginOverlayImpl",new() { [5]=10 });
selectors.Add("GameAreaManager.CalculateTilePrice",new() { [5]=10 });
selectors.Add("NetManager.Awake",new() { [1050]=4200 });
selectors["NetManager.Awake"].Add(72900,90000);
// 64m cells, unchanged +135-cell origin; append 30 rows/columns.
// Render-group ratios and callers in other types need separate review.
foreach(string name in new[]{"SampleSmoothHeight","InitializeNode","FinalizeNode","InitializeSegment","FinalizeSegment","TerrainUpdated","AfterTerrainUpdate","GetClosestSegments","OverlapQuad","RayCast"})
    selectors.Add("NetManager."+name,new() { [270]=300,[269]=299 });
foreach(string name in new[]{"EndRenderingImpl","CalculateGroupData","PopulateGroupData","UpdateSegmentRenderer","UpdateNodeRenderer","UpdateNodeNotifications","UpdateSegmentNotifications"})
    selectors.Add("NetManager."+name,new(){[270]=300,[269]=299,[45]=50});
selectors.Add("RenderManager.Awake",new(){[2025]=2500,[81]=100});
selectors.Add("RenderManager.LateUpdate",new(){[44]=49,[45]=50,[9]=10});
selectors.Add("RenderManager.UpdateGroup",new(){[45]=50});
selectors.Add("RenderManager.UpdateMegaGroup",new(){[9]=10});
selectors.Add("RenderManager.UpdateMegaGroupLayers",new(){[9]=10});
selectors.Add("MegaRenderGroup.UpdateMesh",new(){[45]=50});
selectors.Add("BuildingManager.Awake",new(){[72900]=90000,[2025]=2500});
selectors.Add("TreeManager.Awake",new(){[291600]=360000});
selectors.Add("PropManager.Awake",new(){[72900]=90000});
var zoneMethods=new HashSet<string>(new[]{"Building.CheckZoning","BuildingTool.SimulationStep","ZoneBlock.CalculateBlock2","ZoneBlock.SimulationStep",
    "ZoneManager.InitializeBlock","ZoneManager.ReleaseBlockImplementation","ZoneManager.UpdateBlocks","ZoneManager.CheckSpace","ZoneManager.TerrainUpdated",
    "ZoneTool.CalculateFillBuffer","ZoneTool.ApplyFill","ZoneTool.ApplyZoning","ZoneTool.ApplyBrush"});
selectors.Add("ZoneManager.Awake",new(){[22500]=90000});
foreach(string name in zoneMethods)selectors.Add(name,new(){[149]=299,[150]=300});
var transportMethods=new HashSet<string>(new[]{
    "CitizenAI.SimulationStep","VehicleAI.SimulationStep",
    "CitizenAI.CheckCollisions","ResidentAI.SpawnVehicle","TouristAI.SpawnVehicle","DisasterHelpers.AddWindVehicles","EventManager.InvalidatePathsOnSegment",
    "MetroTrackBaseAI.CheckOverlap","RoadBaseAI.FloodParkedCars","TrainTrackBaseAI.CheckOverlap","AircraftAI.CanSpawnAt","AircraftAI.CheckOverlap",
    "BlimpAI.CheckOverlap","CableCarBaseAI.CheckOtherVehicles","CableCarBaseAI.CheckOverlap","CarAI.CheckOtherVehicles","CarAI.CheckOverlap",
    "FerryAI.CheckOtherVehicles","FerryAI.CheckOverlap","HelicopterAI.CanSpawnAt","HelicopterAI.CheckOverlap","PassengerCarAI.CheckOverlap",
    "PassengerHelicopterAI.CheckOverlap","ShipAI.CheckOtherVehicles","ShipAI.CanSpawnAt","ShipAI.CheckOverlap","TrainAI.CheckOverlap","TramBaseAI.CheckOverlap",
    "VehicleManager.EndRenderingImpl","VehicleManager.PlayAudioImpl","VehicleManager.AddToGrid","VehicleManager.RemoveFromGrid","VehicleManager.RayCast","VehicleManager.UpdateParkedVehicles",
    "BuildingAI.IsSomeBodyThere","CitizenManager.EndRenderingImpl","CitizenManager.AddToGrid","CitizenManager.RemoveFromGrid","CitizenManager.RayCast",
    "DisasterHelpers.AddWindCitizens","TransportLine.CalculatePassengerCount","BusAI.LoadPassengers","CableCarAI.LoadPassengers","PassengerBlimpAI.LoadPassengers",
    "PassengerFerryAI.LoadPassengers","PassengerHelicopterAI.LoadPassengers","PassengerPlaneAI.CheckPassengers","PassengerPlaneAI.LoadPassengers",
    "PassengerShipAI.CheckPassengers","PassengerShipAI.LoadPassengers","PassengerTrainAI.CheckPassengers","PassengerTrainAI.LoadPassengers",
    "TramAI.CheckPassengers","TramAI.LoadPassengers","TrolleybusAI.CheckPassengers","TrolleybusAI.LoadPassengers"});
selectors.Add("VehicleManager.Awake",new(){[291600]=360000,[2916]=3600});
selectors.Add("CitizenManager.Awake",new(){[4665600]=5760000});
foreach(string name in transportMethods)selectors.Add(name,new(){[539]=599,[540]=600,[53]=59,[54]=60,[2159]=2399,[2160]=2400});
var utilityMethods=new HashSet<string>();
foreach(string owner in new[]{"ElectricityManager","WaterManager"})
{
    selectors.Add(owner+".Awake",new(){[65536]=258064,[256]=508,[255]=507});
    selectors.Add(owner+".Update"+(owner=="ElectricityManager"?"Electricity":"Water")+"Mapping",new());
    utilityMethods.Add(owner+".UpdateTexture");
    selectors.Add(owner+".UpdateTexture",owner=="WaterManager"?new(){[256]=508,[255]=507,[2048]=3696}:new(){[256]=508,[255]=507});
}
foreach(string name in new[]{"TryDumpElectricity","TryFetchElectricity","CheckElectricity","CheckConductivity"})
{string key="ElectricityManager."+name;utilityMethods.Add(key);selectors.Add(key,new(){[256]=508,[255]=507});}
foreach(string name in new[]{"TryDumpSewage","TryDumpSewageImpl","TryFetchWater","TryFetchWaterImpl","TryFetchHeating","TryFetchHeatingImpl","CheckWater","CheckWaterImpl","CheckHeating","CheckHeatingImpl"})
{string key="WaterManager."+name;utilityMethods.Add(key);selectors.Add(key,new(){[256]=508,[255]=507});}
// Already selected for its road-grid query. Expand its utility coordinate
// origin/stride too; byte conductivity saturation constants remain unchanged.
utilityMethods.Add("WaterManager.UpdateGrid");
selectors.Add("Building.AfterTerrainUpdated",new(){[269]=299,[270]=300,[45]=50});
selectors.Add("NotificationManager.Awake",new(){[25]=50});
foreach(string name in new[]{"EndRenderingImpl","CalculateGroupData","PopulateGroupData"})
    selectors.Add("NotificationManager."+name,new(){[25]=50,[45]=50});
selectors.Add("Notification.PopulateGroupData",new(){[25]=50,[45]=50});
foreach(string name in new[]{"EndRenderingImpl","PlayAudioImpl","CalculateServiceProximity","SampleSmoothHeight","AddToGrid","AddToGrid2","RemoveFromGrid","RemoveFromGrid2","UpdateBuildingRenderer","UpdateBuildingInfo","UpdateNotifications","OverlapQuad","GetWalkingBuilding","FindBuilding","FindTransportBuilding","RayCast","TerrainUpdated","AfterTerrainUpdate","ZonesUpdated","CalculateGroupData","PopulateGroupData","FindNodeOwner"})
    selectors.Add("BuildingManager."+name,new(){[270]=300,[269]=299,[45]=50,[44]=49});
selectors.Add("TerrainManager.GetTileFlatness",new() { [2]=0,[9]=10 });
selectors.Add("TerrainManager.GetUnlockableTerrainFlatness",new() { [2]=0,[5]=10 });
selectors.Add("NaturalResourceManager.Awake",new() { [81]=100 });
selectors.Add("NaturalResourceManager.GetTileResources",new() { [2]=0 });
selectors.Add("NaturalResourceManager.GetTileResourcesImpl",new() { [9]=10 });
selectors.Add("NaturalResourceManager.CalculateUnlockedResources",new() { [2]=0,[5]=10 });
selectors.Add("NaturalResourceManager.CalculateUnlockableResources",new() { [2]=0,[5]=10 });
selectors.Add("NaturalResourceManager.CalculateTotalResources",new() { [9]=10 });
selectors["NaturalResourceManager.Awake"]=new() { [81]=100,[262144]=323761,[512]=569,[511]=568 };
foreach(string name in new[]{"UpdateTexture","UpdateTextureB","AddResource","CalculateForestProximity","AreaModified","AreaModifiedB","TreesModified","CountResource","CheckPollution","CheckForest","AverageWater","AveragePollutionAndWater","AveragePollutionAndWaterAndTrees"})
    selectors.Add("NaturalResourceManager."+name,new() { [512]=569,[511]=568 });
selectors.Add("NaturalResourceManager+Data.AfterDeserialize",new() { [511]=568 });
// Editor batches contain 16 consecutive update calls. The old 8-bit tick
// mask restarted their frame sequence every 4096 calls, midway through the
// new 1138-call resource cycle. Keep the full 28 bits that fit before <<4.
selectors.Add("NaturalResourceManager.SimulationStepImpl",new() { [255]=268435455 });
selectors.Add("ResourceWrapper.get_gridResolution",new() { [512]=569 });
selectors.Add("GameAreaManager.PointOutOfArea",new());
selectors.Add("GameAreaManager.QuadOutOfArea",new());
selectors.Add("GameAreaManager.ClampPoint",new() { [5]=10 });
selectors.Add("TerrainManager.RayCast",new() { [1079]=1199,[1081]=1201 });
selectors.Add("TerrainManager.RayCastEdge",new(){[1080]=1200,[1081]=1201,[1167480]=1441200,[1168560]=1442400});
var areaCoordinates=new HashSet<string>(new[]{"GameAreaManager.GetAreaBounds","GameAreaManager.GetAreaPositionSmooth","GameAreaManager.GetTileXZ","GameAreaManager.GetAreaIndex","GameAreaManager.UnlockArea","GameAreaManager.GetFreeBounds","GameAreaManager.BeginOverlayImpl"});
areaCoordinates.UnionWith(new[]{"GameAreaManager.PointOutOfArea","GameAreaManager.QuadOutOfArea","GameAreaManager.ClampPoint"});
var lines = new List<string> {
    "# HundredTiles reviewed city-runtime patch plan v1; pinned game assembly; editors unsupported",
    "assembly\t" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),
    "coverage\tcity-runtime"
};
var found = new HashSet<string>();
foreach(string name in netConsumers)selectors.Add(name,new(){[269]=299,[270]=300});
foreach(string name in buildingConsumers)selectors.Add(name,new(){[269]=299,[270]=300});
selectors["WaterManager.UpdateGrid"]=new(){[269]=299,[270]=300,[256]=508,[255]=507};
var districtMethods=new HashSet<string>(new[]{"Awake","set_HighlightPolicy","GetDistrict","GetDistrictArea","GetPark","GetParkArea","ModifyCell","ModifyParkCell","MoveParkBuildings","MoveParkNodes","MoveParkProps","MoveParkSegments","MoveParkTrees","SetCityPolicy","UnsetCityPolicy","SampleDistrict"}.Select(n=>"DistrictManager."+n));
foreach(string name in districtMethods)
{
    if(!selectors.TryGetValue(name,out var values))selectors[name]=values=new();
    values[512]=1000;values[511]=999;
    if(name=="DistrictManager.Awake")values[262144]=1000000;
    if(name=="DistrictManager.MoveParkProps"){values[269]=299;values[270]=300;}
    if(name=="DistrictManager.MoveParkTrees"){values[539]=599;values[540]=600;}
}
foreach(string name in new[]{"ApplyBrush","CheckNeighbourCells","ForceDistrictAlpha","SetDistrictAlpha"})selectors["DistrictTool."+name]=new(){[512]=1000};
selectors["DistrictManager+Data.AfterDeserialize"]=new(){[511]=999};
selectors["DistrictManager.UpdateTexture"]=new(){[512]=1000,[510]=998,[256]=450,[65536]=250000,[5]=10};
selectors["DistrictManager.BeginOverlayImpl"]=new();
selectors["DistrictManager.UpdateNames"]=new();
var serviceMethods=new HashSet<string>(new[]{"Awake","AddLocalResource","AddObstructedResource","AddResource","AreaModified","CalculateLocalResources","CheckLocalResource","CheckLocalResources","CheckResource","UpdateResourceMapping","UpdateTexture"}.Select(n=>"ImmaterialResourceManager."+n));
foreach(string name in serviceMethods)
{
    selectors[name]=new(){[256]=500};
    if(name=="ImmaterialResourceManager.CalculateLocalResources")selectors[name][254]=498;
    else {selectors[name][255]=499;selectors[name][253]=497;}
    if(name=="ImmaterialResourceManager.Awake")selectors[name][2293760]=8750000;
    if(name=="ImmaterialResourceManager.AddObstructedResource"){selectors[name][1080]=1200;selectors[name][1081]=1201;}
}
selectors["ImmaterialResourceManager+Data.AfterDeserialize"]=new(){[255]=499};
foreach(string name in new[]{"SampleSmoothHeight","UpdateTrees","OverlapQuad","RayCast","HandleFireSpread","TerrainUpdated","AfterTerrainUpdate","CalculateAreaHeight","EndRenderingImpl","CalculateGroupData","PopulateGroupData"})
{
    string key="TreeManager."+name;if(!selectors.TryGetValue(key,out var values))selectors[key]=values=new();
    values[539]=599;values[540]=600;
    if(name=="EndRenderingImpl" || name=="CalculateGroupData" || name=="PopulateGroupData")values[45]=50;
}
foreach(string name in new[]{"SampleSmoothHeight","UpdateProps","OverlapQuad","RayCast","TerrainUpdated","AfterTerrainUpdate","EndRenderingImpl","CalculateGroupData","PopulateGroupData"})
{
    string key="PropManager."+name;if(!selectors.TryGetValue(key,out var values))selectors[key]=values=new();
    values[269]=299;values[270]=300;
    if(name=="EndRenderingImpl" || name=="CalculateGroupData" || name=="PopulateGroupData")values[45]=50;
}
foreach(string key in new[]{"CommonBuildingAI.HandleFireSpread","ForestFireAI.FindClosestTree","DisasterHelpers.DestroyTrees","NaturalResourceManager.TreesModified","WeatherManager.FindStrikeTarget","FireCopterAI.FindBurningTree","DistrictTool.ApplyAntiTreeBrush","TreeTool.ApplyBrush"})
{if(!selectors.TryGetValue(key,out var values))selectors[key]=values=new();values[539]=599;values[540]=600;}
foreach(string key in new[]{"DisasterHelpers.DestroyProps","EventManager.UpdateProps","PropTool.ApplyBrush"})
{if(!selectors.TryGetValue(key,out var values))selectors[key]=values=new();values[269]=299;values[270]=300;}
int replacements = 0;
foreach (var type in AllTypes(module.Types))
foreach (var method in type.Methods.Where(m => m.HasBody))
{
    string key = TypeName(type) + "." + method.Name;
    if (!selectors.TryGetValue(key, out var map)) continue;
    var instructions = method.Body.Instructions;
    if(transportMethods.Contains(key))
    {
        foreach(var item in instructions.Select((code,index)=>(code,index)))
        {
            int? value=IntConstant(item.code);
            if(value==539 || value==53 || value==2159)
            {if(!(instructions[item.index+1].Operand is MethodReference clamp) || clamp.DeclaringType.FullName!="UnityEngine.Mathf" || (clamp.Name!="Min" && clamp.Name!="Clamp"))throw new Exception("Transport bound is not a reviewed spatial clamp: "+key);}
            if(value==540 || value==54 || value==2160)
            {if(instructions[item.index+1].OpCode.Code!=Code.Mul)throw new Exception("Transport stride is not a spatial multiply: "+key);}
        }
    }
    if(zoneMethods.Contains(key))
    {
        if(!instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name=="ZoneManager" && f.Name=="m_zoneGrid"))continue;
        int origins=instructions.Count(i=>i.OpCode.Code==Code.Ldc_R4 && (float)i.Operand==75f);
        if(origins!=(key=="ZoneManager.InitializeBlock" || key=="ZoneManager.ReleaseBlockImplementation"?2:4) ||
            instructions.Count(i=>IntConstant(i)==149)!=2 || instructions.Count(i=>IntConstant(i)==150)!=1)
            throw new Exception("Zone spatial shape differs: "+key);
    }
    if(netConsumers.Contains(key))
    {
        // Delegating overloads have no direct spatial-array access.
        if(!instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name=="NetManager" && (f.Name=="m_nodeGrid" || f.Name=="m_segmentGrid")))continue;
        ValidateNetConsumer(method);
    }
    if(buildingConsumers.Contains(key))
    {
        if(!instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name=="BuildingManager" && f.Name=="m_buildingGrid"))continue;
        ValidateNetConsumer(method,"BuildingManager",key.StartsWith("Structure")?4:2);
    }
    var edits = new List<(int Index,string Kind,int Old,int New)>();
    if(key=="TerrainTool.ApplyBrush")
    {
        for(int index=0;index<instructions.Count-3;index++)
            if(instructions[index].OpCode.Code==Code.Ldloc_S && ((VariableDefinition)instructions[index].Operand).Index==7 && instructions[index+1].OpCode.Code==Code.Conv_R4 && instructions[index+2].OpCode.Code==Code.Ldc_R4 && (float)instructions[index+2].Operand==.5f && instructions[index+3].OpCode.Code==Code.Mul)
                edits.Add((index,"local",7,1080));
        if(edits.Count!=8)throw new Exception("Terrain brush origin shape differs.");
    }
    if(key=="TreeTool.ApplyBrush" || key=="PropTool.ApplyBrush" || key=="DistrictTool.ApplyAntiTreeBrush")
    {
        int local=key=="DistrictTool.ApplyAntiTreeBrush"?2:3,oldWidth=key=="PropTool.ApplyBrush"?270:540;
        for(int index=0;index<instructions.Count-3;index++)
        {
            bool load=local==2?instructions[index].OpCode.Code==Code.Ldloc_2:instructions[index].OpCode.Code==Code.Ldloc_3;
            if(load && instructions[index+1].OpCode.Code==Code.Conv_R4 && instructions[index+2].OpCode.Code==Code.Ldc_R4 && (float)instructions[index+2].Operand==.5f && instructions[index+3].OpCode.Code==Code.Mul)
                edits.Add((index,"local",local,oldWidth));
        }
        if(edits.Count!=(local==2?4:8))throw new Exception("Object brush origin shape differs: "+key);
    }
    for (int index=0;index<instructions.Count;index++)
        if (IntConstant(instructions[index]) is int old && map.TryGetValue(old, out int next))
        {
            if(serviceMethods.Contains(key))
            {
                if(key=="ImmaterialResourceManager.Awake" && old==256 && instructions[index+1].Operand is Mono.Cecil.TypeReference arrayType && arrayType.FullName=="System.Single")continue;
                if(old==255 && (key=="ImmaterialResourceManager.Awake" || key=="ImmaterialResourceManager.UpdateResourceMapping") && index==instructions.Select((code,position)=>(code,position)).First(item=>IntConstant(item.code)==255).position)continue;
                if(old==255 && key=="ImmaterialResourceManager.AddObstructedResource")continue;
                if(old==256 && key=="ImmaterialResourceManager.AddObstructedResource" && instructions[index+1].OpCode.Code!=Code.Mul)continue;
            }
            if(key=="WaterManager.UpdateGrid" && old==255 &&
                !(index>0 && instructions[index-1].OpCode.Code==Code.Conv_I4 && index+1<instructions.Count &&
                    instructions[index+1].Operand is MethodReference bound && bound.Name=="Min" &&
                    instructions.Skip(Math.Max(0,index-6)).Take(6).Any(i=>i.OpCode.Code==Code.Ldc_R4 && (float)i.Operand==128f)))continue;
            edits.Add((index,"int",old,next));
        }
    if(serviceMethods.Contains(key))
    {
        for(int index=0;index<instructions.Count;index++)
        {
            if(instructions[index].OpCode.Code!=Code.Ldc_R4)continue;
            float old=(float)instructions[index].Operand,next=old;
            if(old==128f)next=225f;
            if(key=="ImmaterialResourceManager.UpdateResourceMapping")
            {if(old==.5f)next=.45f;if(old==.00010172526f)next=1f/19200;}
            if(next!=old)edits.Add((index,"float",BitConverter.SingleToInt32Bits(old),BitConverter.SingleToInt32Bits(next)));
        }
    }
    if(key=="DistrictManager.UpdateTexture")
    {
        var five=edits.Where(e=>e.Old==5).First();
        if(instructions[five.Index+2].OpCode.Code!=Code.Mul)throw new Exception("District texture ownership origin differs.");
        edits[edits.IndexOf(five)]=(five.Index,"int",5,9);
    }
    if(districtMethods.Contains(key))
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4)
            {
                float old=(float)instructions[index].Operand;
                if(old==256f || key=="DistrictManager.SampleDistrict" && old==65536f)
                    edits.Add((index,"float",BitConverter.SingleToInt32Bits(old),BitConverter.SingleToInt32Bits(old==256f?450f:115200f)));
            }
    if(key=="DistrictManager.BeginOverlayImpl" || key=="DistrictManager.UpdateNames")
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4)
            {
                float old=(float)instructions[index].Operand;
                float next=old==9830.4f?19200f:old;
                if(key=="DistrictManager.BeginOverlayImpl")
                {
                    if(old==0.5f)next=.45f;
                    if(old==0.00010172526f)next=1f/19200;
                    if(old==0f && ((index+1<instructions.Count && instructions[index+1].OpCode.Code==Code.Ldc_R4 && (float)instructions[index+1].Operand==512f) || (index>0 && instructions[index-1].OpCode.Code==Code.Ldc_R4 && (float)instructions[index-1].Operand==512f)))next=960f;
                }
                if(next!=old)edits.Add((index,"float",BitConverter.SingleToInt32Bits(old),BitConverter.SingleToInt32Bits(next)));
            }
    if(utilityMethods.Contains(key))
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4 && (float)instructions[index].Operand==128f)
                edits.Add((index,"float",BitConverter.SingleToInt32Bits(128f),BitConverter.SingleToInt32Bits(231f)));
    if(zoneMethods.Contains(key))
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4 && (float)instructions[index].Operand==75f)
                edits.Add((index,"float",BitConverter.SingleToInt32Bits(75f),BitConverter.SingleToInt32Bits(135f)));
    if(key=="VehicleManager.RayCast" || key=="CitizenManager.RayCast" || key=="TreeManager.RayCast" || key=="PropManager.RayCast")
    {
        int offset=key=="VehicleManager.RayCast"?10:0;
        foreach(var item in new (int Index,float Old,float New)[]{(1,0f,960f),(3,0f,960f),(5,17280f,19200f),(7,17280f,19200f)})
        {
            int index=offset+item.Index;
            if(instructions[index].OpCode.Code!=Code.Ldc_R4 || (float)instructions[index].Operand!=item.Old)throw new Exception("Transport raycast clipping box differs: "+key);
            edits.Add((index,"float",BitConverter.SingleToInt32Bits(item.Old),BitConverter.SingleToInt32Bits(item.New)));
        }
    }
    if(key=="ElectricityManager.UpdateElectricityMapping" || key=="WaterManager.UpdateWaterMapping")
    {
        var mapping=new Dictionary<float,float>{[0.000102124184f]=1f/(508*38.25f),[0.5f]=231f/508,[0.00390625f]=1f/508};
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4 && mapping.TryGetValue((float)instructions[index].Operand,out float next))
                edits.Add((index,"float",BitConverter.SingleToInt32Bits((float)instructions[index].Operand),BitConverter.SingleToInt32Bits(next)));
    }
    if(areaCoordinates.Contains(key))
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4 && (float)instructions[index].Operand==2.5f)
                edits.Add((index,"float",BitConverter.SingleToInt32Bits(2.5f),BitConverter.SingleToInt32Bits(4.5f)));
    if(key=="GameAreaManager.BeginOverlayImpl")
    {
        var mapping=new Dictionary<float,float> { [6.510417E-05f]=1f/(16*1920),[0.4375f]=5.5f/16,[0.125f]=1f/16 };
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4 && mapping.TryGetValue((float)instructions[index].Operand,out float next))
                edits.Add((index,"float",BitConverter.SingleToInt32Bits((float)instructions[index].Operand),BitConverter.SingleToInt32Bits(next)));
    }
    if(key=="NaturalResourceManager.Awake")
    {
        var mapping=new Dictionary<float,float> { [5.787037E-05f]=1f/(569*33.75f),[0.5f]=256f/569 };
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4 && mapping.TryGetValue((float)instructions[index].Operand,out float next))
                edits.Add((index,"float",BitConverter.SingleToInt32Bits((float)instructions[index].Operand),BitConverter.SingleToInt32Bits(next)));
    }
    if(key=="TerrainManager.GetUnlockableTerrainFlatness")
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4 && (float)instructions[index].Operand==25f)
                edits.Add((index,"float",BitConverter.SingleToInt32Bits(25f),BitConverter.SingleToInt32Bits(100f)));
    if(key=="NaturalResourceManager.CalculateForestProximity" || key=="NaturalResourceManager.CountResource")
    {
        // The width also fed origin calculations. Keep the original 256-cell
        // origin while allowing integer strides and clamps to use 569.
        for(int index=0;index+3<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldloc_1 && instructions[index+1].OpCode.Code==Code.Conv_R4 &&
                instructions[index+2].OpCode.Code==Code.Ldc_R4 && (float)instructions[index+2].Operand==0.5f && instructions[index+3].OpCode.Code==Code.Mul)
                edits.Add((index,"local",1,512));
    }
    if(key=="GameAreaManager.ClampPoint")
    {
        var bounds=new Dictionary<float,float> {[-4800f]=-8640f,[4800f]=10560f,[8640f]=10560f};
        for(int index=0;index<instructions.Count;index++)
            if(instructions[index].OpCode.Code==Code.Ldc_R4 && bounds.TryGetValue((float)instructions[index].Operand,out float next))
                edits.Add((index,"float",BitConverter.SingleToInt32Bits((float)instructions[index].Operand),BitConverter.SingleToInt32Bits(next)));
    }
    if(key=="TerrainManager.RayCast")
    {
        // Change the clipping box, not coordinate origins or unrelated zeros.
        foreach(var item in new (int Index,float Old,float New)[]{(13,0f,960f),(15,0f,960f),(17,17280f,19200f),(19,17280f,19200f)})
        {
            if(instructions[item.Index].OpCode.Code!=Code.Ldc_R4 || (float)instructions[item.Index].Operand!=item.Old)
                throw new Exception("Terrain raycast clipping box differs from reviewed IL.");
            edits.Add((item.Index,"float",BitConverter.SingleToInt32Bits(item.Old),BitConverter.SingleToInt32Bits(item.New)));
        }
    }
    if(key=="NetManager.RayCast" && method.Parameters.Count==16)
    {
        foreach(var item in new (int Index,float Old,float New)[]{(1,0f,960f),(3,0f,960f),(5,17280f,19200f),(7,17280f,19200f)})
        {
            if(instructions[item.Index].OpCode.Code!=Code.Ldc_R4 || (float)instructions[item.Index].Operand!=item.Old)
                throw new Exception("Net raycast clipping box differs from reviewed IL.");
            edits.Add((item.Index,"float",BitConverter.SingleToInt32Bits(item.Old),BitConverter.SingleToInt32Bits(item.New)));
        }
    }
    if(key=="BuildingManager.RayCast")
    {
        foreach(var item in new (int Index,float Old,float New)[]{(1,0f,960f),(3,0f,960f),(5,17280f,19200f),(7,17280f,19200f)})
        {
            if(instructions[item.Index].OpCode.Code!=Code.Ldc_R4 || (float)instructions[item.Index].Operand!=item.Old)
                throw new Exception("Building raycast clipping box differs from reviewed IL.");
            edits.Add((item.Index,"float",BitConverter.SingleToInt32Bits(item.Old),BitConverter.SingleToInt32Bits(item.New)));
        }
    }
    if(key=="RenderManager.Awake")
    {
        // Layer materials still have 32 entries. Only the group dirty bitmap
        // grows to ceil(2500 / 64) = 40 words.
        if(IntConstant(instructions[29])!=32 || instructions[30].OpCode.Code!=Code.Newarr || instructions[31].Operand is not FieldReference field || field.Name!="m_updatedGroups1")
            throw new Exception("Reviewed render-group bitmap allocation changed.");
        edits.Add((29,"int",32,40));
    }
    if(key=="GameAreaManager.UnlockArea" || key=="GameAreaManager.UpdateData")
    {
        int index=key.EndsWith("UnlockArea")?107:232;
        if(IntConstant(instructions[index])!=2 || instructions[index+1].OpCode.Code!=Code.Stloc_S)
            throw new Exception("Reviewed detail patch offset changed.");
        edits.Add((index,"int",2,0));
    }
    if (key == "WaterSimulation.SimulateWater")
    {
        // IL_0060 loads local 7 (expanded resolution) to calculate the origin
        // width, then conv.r4, ldloc.s 6 (16m), mul, stloc.s 9. Preserve 17280m
        // in this calculation only; all cell loops still use 1200.
        var origin = instructions[43];
        if (origin.OpCode.Code != Code.Ldloc_S || ((VariableDefinition)origin.Operand).Index != 7 ||
            instructions[44].OpCode.Code != Code.Conv_R4 || instructions[46].OpCode.Code != Code.Mul)
            throw new Exception("Water origin calculation does not match reviewed IL");
        edits.Add((43,"local",7,1080));
    }
    if (key == "TerrainModify.UpdateAreaImplementation")
    {
        // Only these two 8s clamp patch coordinates. Other 8s in this body
        // encode flags and bit shifts, so they must retain their values.
        foreach (int index in new[] {96,112})
        {
            if (IntConstant(instructions[index]) != 8 ||
                instructions[index+6].Operand is not MethodReference call || call.Name != "Min")
                throw new Exception("Terrain modification patch clamp differs from reviewed IL");
            edits.Add((index,"int",8,9));
        }
    }
    // Delegating overloads contain no grid accesses and require no transform.
    // Each selected body is independently pinned by token, hash and offsets.
    if (edits.Count == 0) continue;
    found.Add(key);
    // Cecil decodes bodies without loading Unity or running game static initializers.
    // Original method hash is read directly from the PE using metadata in Audit;
    // a raw instruction signature adds a deterministic dry-run check here.
    var methodDefinition = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle((int)method.MetadataToken.RID));
    string signature = Convert.ToHexString(SHA256.HashData(pe.GetMethodBody(methodDefinition.RelativeVirtualAddress).GetILBytes()!));
    lines.Add(string.Join('\t',"method",method.MetadataToken.ToInt32().ToString("X8"),key,instructions.Count,signature));
    foreach (var edit in edits)
    {
        lines.Add(string.Join('\t',"edit",method.MetadataToken.ToInt32().ToString("X8"),edit.Index,edit.Kind,edit.Old,edit.New));
        replacements++;
    }
    // Verify original scalar types, preserve branch target objects and exception
    // handlers, and ensure instruction count is unchanged after transformation.
    foreach (var edit in edits)
    {
        var instruction = instructions[edit.Index];
        instruction.OpCode = edit.Kind=="float" ? OpCodes.Ldc_R4 : OpCodes.Ldc_I4;
        instruction.Operand = edit.Kind=="float" ? (object)BitConverter.Int32BitsToSingle(edit.New) : edit.New;
    }
    foreach (var edit in edits)
        if (edit.Kind=="float" ? instructions[edit.Index].OpCode.Code!=Code.Ldc_R4 || BitConverter.SingleToInt32Bits((float)instructions[edit.Index].Operand)!=edit.New : IntConstant(instructions[edit.Index]) != edit.New) throw new Exception("Edit verification failed");
}
if (found.Count != selectors.Count) throw new Exception("Missing planned methods: " + string.Join(",",selectors.Keys.Except(found)));
if(replacements!=1909)throw new Exception("Reviewed edit count changed: "+replacements);
string plan = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(plan)!);
File.WriteAllLines(plan,lines);
Console.WriteLine($"PASS: {lines.Count(l => l.StartsWith("method\t"))} method bodies in {found.Count} named groups; {replacements} scalar edits validated. Original assembly untouched. Scope: normal city runtime; editors and scenarios unsupported.");

static int? IntConstant(Instruction i) => i.OpCode.Code switch {
    Code.Ldc_I4_M1 => -1, Code.Ldc_I4_0 => 0, Code.Ldc_I4_1 => 1, Code.Ldc_I4_2 => 2,
    Code.Ldc_I4_3 => 3, Code.Ldc_I4_4 => 4, Code.Ldc_I4_5 => 5, Code.Ldc_I4_6 => 6,
    Code.Ldc_I4_7 => 7, Code.Ldc_I4_8 => 8, Code.Ldc_I4_S => Convert.ToInt32(i.Operand),
    Code.Ldc_I4 => (int)i.Operand, _ => null
};
static void ValidateNetConsumer(Mono.Cecil.MethodDefinition method,string owner="NetManager",int expectedClamps=2)
{
    var codes=method.Body.Instructions;
    var clamps=codes.Select((c,i)=>(c,i)).Where(p=>IntConstant(p.c)==269).Select(p=>p.i).ToArray();
    var strides=codes.Select((c,i)=>(c,i)).Where(p=>IntConstant(p.c)==270).Select(p=>p.i).ToArray();
    if(clamps.Length!=expectedClamps || strides.Length!=1 || codes.Count(c=>c.Operand is FieldReference f && f.DeclaringType.Name==owner && (owner=="BuildingManager"?f.Name=="m_buildingGrid":f.Name=="m_nodeGrid" || f.Name=="m_segmentGrid"))!=1)
        throw new Exception("Net consumer shape changed: "+method.FullName);
    foreach(int i in clamps.Take(2))
        if(i<5 || i+1>=codes.Count || codes[i-5].OpCode.Code!=Code.Ldc_R4 || (float)codes[i-5].Operand!=64f ||
            codes[i-4].OpCode.Code!=Code.Div || codes[i-3].OpCode.Code!=Code.Ldc_R4 || (float)codes[i-3].Operand!=135f ||
            codes[i-2].OpCode.Code!=Code.Add || codes[i-1].OpCode.Code!=Code.Conv_I4 || codes[i+1].OpCode.Code!=Code.Call ||
            codes[i+1].Operand is not MethodReference call || call.DeclaringType.FullName!="UnityEngine.Mathf" || call.Name!="Min" || call.ReturnType.FullName!="System.Int32")
            throw new Exception("Net consumer clamp differs from reviewed 64m/origin135 pattern: "+method.FullName);
    foreach(int i in clamps.Skip(2))
        if(i<3 || !IsLocalLoad(codes[i-3]) || IntConstant(codes[i-2])!=1 || codes[i-1].OpCode.Code!=Code.Add ||
            codes[i+1].Operand is not MethodReference ringCall || ringCall.Name!="Min" || ringCall.DeclaringType.FullName!="UnityEngine.Mathf")
            throw new Exception("Building search-ring upper clamp changed: "+method.FullName);
    int stride=strides[0];
    if(stride<1 || stride+4>=codes.Count || !IsLocalLoad(codes[stride-1]) || codes[stride+1].OpCode.Code!=Code.Mul ||
        !IsLocalLoad(codes[stride+2]) || codes[stride+3].OpCode.Code!=Code.Add || codes[stride+4].OpCode.Code!=Code.Ldelem_U2)
        throw new Exception("Net consumer row-stride array lookup changed: "+method.FullName);
}
static bool IsLocalLoad(Instruction code)=>code.OpCode.Code is Code.Ldloc or Code.Ldloc_S or Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3;
static string TypeName(TypeDefinition type) => type.DeclaringType == null ? type.Name : TypeName(type.DeclaringType)+"+"+type.Name;
static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
{
    foreach(var type in types) { yield return type; foreach(var nested in AllTypes(type.NestedTypes)) yield return nested; }
}
