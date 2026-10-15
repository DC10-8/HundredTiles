using System;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using HarmonyLib;
using UnityEngine;

// Isolated test fixtures only. This assembly is never distributed to players.
static class LiveConsumerChecks
{
    public const string BaseSaveName="HundredTiles_324_Consumers_Verification";
    public static bool Wastewater {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_WASTEWATER")=="1";}}
    public static bool LongRun {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_LONG_RUN")=="1";}}
    public static string SaveName {get{return LongRun?"HundredTiles_324_Consumers_LongRun_Verification":Wastewater?"HundredTiles_324_Consumers_Sewage_Verification":BaseSaveName;}}
    public static bool Enabled {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_CONSUMERS")=="1";}}
    static string Manifest {get{return Path.Combine(Entry.Root,"trial324-consumers.txt");}}
    static ushort[] homes;
    static int[] electricity,water;static int[] sewageOutput;
    static ushort sewageNode,treatmentBuilding;static int sewageFetched,sewageDisposed;static int goodsTrace,fetchTrace;
    public static void Create()
    {
        if(LongRun){PrepareRealTime();PrepareUnattendedProgression();return;}
        if(Wastewater){UpgradeWastewater();return;}
        var manager=Singleton<BuildingManager>.instance;var random=new Randomizer(32418);
        ushort road=LiveConstructionChecks.BuildNetwork("Basic Road",new Vector3(25400,0,24300));
        LiveConstructionChecks.BuildNetwork("Power Line",new Vector3(25400,0,24400));
        ZoneRoad();
        ushort tower=LiveConstructionChecks.PlaceBuilding("Water Tower",new Vector3(25460,0,24320));
        Complete(tower);
        ushort target=ushort.Parse(File.ReadAllLines(Path.Combine(Entry.Root,"trial324-far-supply.txt"))[1]);
        var graph=Singleton<NetManager>.instance;ushort node=manager.m_buildings.m_buffer[tower].m_netNode,pipe;
        if(node==0)throw new Exception("Native water tower connection missing.");
        var direction=(graph.m_nodes.m_buffer[target].m_position-graph.m_nodes.m_buffer[node].m_position).normalized;
        if(!graph.CreateSegment(out pipe,ref random,PrefabCollection<NetInfo>.FindLoaded("Water Pipe"),node,target,direction,-direction,21,21,false))throw new Exception("Consumer fixture water connection failed.");
        BuildingInfo residential=null;
        for(uint i=0;i<PrefabCollection<BuildingInfo>.LoadedCount();i++)
        {
            var info=PrefabCollection<BuildingInfo>.GetLoaded(i);
            if(info!=null && info.m_buildingAI is ResidentialBuildingAI && info.m_class.isResidentialLowGeneric && info.m_class.m_level==ItemClass.Level.Level1 && info.m_cellWidth<=4 && info.m_cellLength<=4){residential=info;break;}
        }
        if(residential==null)throw new Exception("Native residential prefab missing.");
        homes=new ushort[2];var citizens=Singleton<CitizenManager>.instance;
        for(int i=0;i<homes.Length;i++)
        {
            var position=new Vector3(25528+i*80,400,24308+residential.m_cellLength*4);
            if(!manager.CreateBuilding(out homes[i],ref random,residential,position,Mathf.PI,residential.m_cellLength,Singleton<SimulationManager>.instance.m_currentBuildIndex++))throw new Exception("Native residential construction failed.");
            if(!manager.m_buildings.m_buffer[homes[i]].CheckZoning(ItemClass.Zone.ResidentialLow,ItemClass.Zone.None,false))throw new Exception("Far native residential zoning does not cover its footprint.");
            Complete(homes[i]);uint unit=manager.m_buildings.m_buffer[homes[i]].m_citizenUnits;
            while(unit!=0 && (citizens.m_units.m_buffer[unit].m_flags&CitizenUnit.Flags.Home)==0)unit=citizens.m_units.m_buffer[unit].m_nextUnit;
            if(unit==0)throw new Exception("Native residential household unit missing.");
            for(int member=0;member<4;member++)
            {
                uint id;if(!citizens.CreateCitizen(out id,member<2?35+member*5:10+member*2,i+1,ref random))throw new Exception("Native citizen allocation failed.");
                citizens.m_citizens.m_buffer[id].SetHome(id,homes[i],unit);
                if(citizens.m_citizens.m_buffer[id].m_homeBuilding!=homes[i])throw new Exception("Native citizen home binding failed.");
            }
        }
        ushort turbine=ushort.Parse(File.ReadAllLines(Path.Combine(Entry.Root,"trial324-far-supply.txt"))[0]);
        File.WriteAllText(Manifest,turbine+"\n"+tower+"\n"+homes[0]+"\n"+homes[1]+"\n"+road+"\n"+pipe);
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(graph,new object[]{0});
        TerrainModify.RefreshAllModifications();
        Entry.Log("Created native far household fixture: prefab="+residential.name+", eight citizens in two native home units; real wind and water providers.");
        Run(true);
    }
    static void ZoneRoad()
    {
        var graph=Singleton<NetManager>.instance;
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(graph,new object[]{0});
        var blocks=Singleton<ZoneManager>.instance.m_blocks.m_buffer;var selected=new System.Collections.Generic.List<ushort>();
        for(int i=1;i<blocks.Length;i++)if((blocks[i].m_flags&3)==1 && blocks[i].m_position.x>25300 && blocks[i].m_position.x<25800 && blocks[i].m_position.z>24200 && blocks[i].m_position.z<24400)selected.Add((ushort)i);
        foreach(ushort id in selected)blocks[id].CalculateBlock1(id);
        foreach(ushort id in selected)blocks[id].CalculateBlock2(id);
        foreach(ushort id in selected){blocks[id].CalculateBlock3(id);for(int z=0;z<blocks[id].RowCount;z++)for(int x=0;x<4;x++)blocks[id].SetZone(x,z,ItemClass.Zone.ResidentialLow);Entry.Log("Far native zoned block "+id+": position="+blocks[id].m_position+", angle="+blocks[id].m_angle+", rows="+blocks[id].RowCount+", valid="+blocks[id].m_valid);}
        if(selected.Count==0)throw new Exception("Native far road has no residential zone blocks.");
    }
    public static void PrepareUnattendedProgression()
    {
        // Fixture-only setup: normal milestones would open modal dialogs and
        // force-pause an unattended simulation. No player runtime change.
        var unlock=Singleton<UnlockManager>.instance;
        foreach(var milestone in unlock.m_allMilestones.Values)milestone.m_openUnlockPanel=false;
        unlock.UnlockAllProgressionMilestones();
        Entry.Log("FIXTURE: native progression milestones unlocked; notification panels disabled for unattended operation in the copied test city only.");
    }
    public static void Reload()
    {
        if(!LongRun){Run(false);return;}
        PrepareRealTime(false);PrepareUnattendedProgression();
    }
    static void PrepareRealTime(bool coldStart=true)
    {
        Singleton<SimulationManager>.instance.ForcedSimulationPaused=true;
        var ids=File.ReadAllLines(Manifest);if(ids.Length!=7)throw new Exception("Real-time test requires the persisted full provider fixture.");
        homes=new[]{ushort.Parse(ids[2]),ushort.Parse(ids[3])};var buildings=Singleton<BuildingManager>.instance;
        foreach(ushort home in homes){var data=buildings.m_buildings.m_buffer[home];if((data.m_flags&Building.Flags.Created)==0 || !(data.Info.m_buildingAI is ResidentialBuildingAI) || !buildings.m_buildings.m_buffer[home].CheckZoning(ItemClass.Zone.ResidentialLow,ItemClass.Zone.None,false))throw new Exception("Real-time input home or zoning differs.");}
        sewageNode=buildings.m_buildings.m_buffer[ushort.Parse(ids[6])].m_netNode;
        treatmentBuilding=ushort.Parse(ids[6]);
        if(sewageNode==0)throw new Exception("Persisted treatment connection missing.");
        foreach(ushort home in homes)
        {
            int count=0;var seen=new System.Collections.Generic.HashSet<uint>();var citizens=Singleton<CitizenManager>.instance;uint unit=buildings.m_buildings.m_buffer[home].m_citizenUnits;
            for(int visited=0;unit!=0 && visited<524288;visited++,unit=citizens.m_units.m_buffer[unit].m_nextUnit)
            {
                var data=citizens.m_units.m_buffer[unit];string members="";
                for(int member=0;member<5;member++)
                {
                    uint citizen=data.GetCitizen(member);members+=" "+citizen;
                    if((data.m_flags&CitizenUnit.Flags.Home)!=0 && citizen!=0 && citizens.m_citizens.m_buffer[citizen].m_homeBuilding==home)
                    {if(!seen.Add(citizen))throw new Exception("Duplicate native Home-unit membership: "+home+" / "+citizen);count++;}
                }
                if(visited<12)Entry.Log("Native cohort home="+home+", unit="+unit+", flags="+data.m_flags+", members="+members);
            }
            Entry.Log("Native unique Home-unit resident count: "+home+" = "+count);
            if(count<4 || coldStart && count!=4)throw new Exception("Real-time persisted native household differs: "+home+", count="+count);
        }
        if(!coldStart){Entry.Log("PASS: separate-process real-time cohort restored native homes, zoning, at least eight unique Home-unit residents and provider connections; no grids, inventories or production settings reset and no manual simulation steps called. Exact initial citizen identities are not asserted by this older fixture.");return;}
        var economy=Singleton<EconomyManager>.instance;
        foreach(int index in new[]{0,1,6})
        {
            ushort id=ushort.Parse(ids[index]);var info=buildings.m_buildings.m_buffer[id].Info;var ai=info.m_buildingAI as PlayerBuildingAI;
            Entry.Log("Persisted provider preparation "+id+": class="+info.name+", budget="+(ai==null?-1:ai.GetBudget(id,ref buildings.m_buildings.m_buffer[id]))+", cash="+economy.InternalCashAmount);
        }
        // Keep this copied test city funded; do not bypass native production or consumption.
        economy.AddResource(EconomyManager.Resource.RewardAmount,100000000,buildings.m_buildings.m_buffer[ushort.Parse(ids[1])].Info.m_class);
        foreach(var service in new[]{ItemClass.Service.Water,ItemClass.Service.Electricity})foreach(bool night in new[]{false,true})economy.SetBudget(service,ItemClass.SubService.None,100,night);
        buildings.m_buildings.m_buffer[ushort.Parse(ids[6])].m_customBuffer2=0;buildings.m_buildings.m_buffer[ushort.Parse(ids[6])].m_sewageBuffer=0;
        // A native 1% production setting is still ample for eight residents and
        // lets their small flow exceed the facility's native processing reserve.
        // At 100%, filling its 15,000-unit reserve would take over an hour.
        buildings.m_buildings.m_buffer[treatmentBuilding].m_productionRate=1;
        // Start the copied fixture with empty utility grids. Otherwise a prior
        // accelerated test's spare pressure can satisfy this tiny cohort while
        // native providers recycle that pressure and return zero new output.
        ResetGrids();
        ushort tower=ushort.Parse(ids[1]);buildings.m_buildings.m_buffer[tower].m_customBuffer1=0;buildings.m_buildings.m_buffer[tower].m_waterBuffer=0;
        foreach(ushort home in homes){buildings.m_buildings.m_buffer[home].m_electricityBuffer=0;buildings.m_buildings.m_buffer[home].m_waterBuffer=0;}
        Entry.Log("Prepared persisted real-time cohort without advancing artificial frames or calling building/utility simulation steps.");
        Entry.Log("FIXTURE: funding and day/night service budgets set; utility grids and stored water/sewage cleared once before normal-time operation. No utility simulation steps were called.");
        Entry.Log("FIXTURE: inland treatment uses native 1% production for this eight-resident cohort; prefab capacity and production code are unchanged.");
    }
    static void UpgradeWastewater()
    {
        var ids=File.ReadAllLines(Manifest);if(ids.Length!=6)throw new Exception("Expected the original household fixture for wastewater upgrade.");
        BuildingInfo plant=null;
        for(uint i=0;i<PrefabCollection<BuildingInfo>.LoadedCount();i++)
        {
            var info=PrefabCollection<BuildingInfo>.GetLoaded(i);var ai=info==null?null:info.m_buildingAI as WaterFacilityAI;
            if(ai!=null && ai.m_sewageOutlet>0 && ai.m_useGroundWater && ai.m_sewageStorage==0){Entry.Log("Native inland sewage candidate: "+info.name);if(plant==null || info.m_cellWidth*info.m_cellLength<plant.m_cellWidth*plant.m_cellLength)plant=info;}
        }
        if(plant==null)throw new Exception("No native inland sewage treatment prefab loaded; coastal outlet requires a separate shoreline fixture.");
        ushort building=LiveConstructionChecks.PlaceBuilding(plant.name,new Vector3(25560,0,24260));Complete(building);
        var buildings=Singleton<BuildingManager>.instance;var graph=Singleton<NetManager>.instance;var random=new Randomizer(650);
        ushort node=buildings.m_buildings.m_buffer[building].m_netNode,target=ushort.Parse(File.ReadAllLines(Path.Combine(Entry.Root,"trial324-far-supply.txt"))[1]),segment;
        if(node==0)throw new Exception("Inland treatment native connection node missing.");
        var direction=(graph.m_nodes.m_buffer[target].m_position-graph.m_nodes.m_buffer[node].m_position).normalized;
        if(!graph.CreateSegment(out segment,ref random,PrefabCollection<NetInfo>.FindLoaded("Water Pipe"),node,target,direction,-direction,22,22,false))throw new Exception("Inland treatment pipe connection failed.");
        File.WriteAllText(Manifest,string.Join("\n",ids)+"\n"+building);
        Run(true);
    }
    static void Complete(ushort id)
    {
        var manager=Singleton<BuildingManager>.instance;var frame=manager.m_buildings.m_buffer[id].GetLastFrameData();frame.m_constructState=255;
        manager.m_buildings.m_buffer[id].SetFrameData(Singleton<SimulationManager>.instance.m_currentFrameIndex,frame);
        manager.m_buildings.m_buffer[id].Info.m_buildingAI.SimulationStep(id,ref manager.m_buildings.m_buffer[id]);
        if((manager.m_buildings.m_buffer[id].m_flags&Building.Flags.Completed)==0)throw new Exception("Native fixture completion failed.");
    }
    static void ResetGrids()
    {
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        // A cold fixture must discard the pending propagation queues as well as
        // cells. Otherwise the saved cycle can put its old spare pressure back
        // into freshly cleared nodes on the next simulation frame.
        var bridge=typeof(HundredTiles.Utilities.UtilitySaveBridge);
        var expanded=bridge.GetMethod("ExpandedArray",BindingFlags.Static|BindingFlags.NonPublic);
        foreach(string prefix in new[]{"E.","W."})
        {
            object owner=prefix=="E."?(object)Singleton<ElectricityManager>.instance:Singleton<WaterManager>.instance;
            var names=(string[])bridge.GetField(prefix=="E."?"ElectricityFields":"WaterFields",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            foreach(string name in names)
            {
                var field=owner.GetType().GetField(name,flags);var original=field.GetValue(owner) as Array;
                if(original!=null){var actual=(Array)expanded.Invoke(null,new object[]{prefix+name})??original;Array.Clear(actual,0,actual.Length);if(!ReferenceEquals(actual,original))Array.Clear(original,0,original.Length);}
                else field.SetValue(owner,field.FieldType==typeof(bool)?(object)false:0);
            }
        }
        var e=Singleton<ElectricityManager>.instance;var eg=(ElectricityManager.Cell[])typeof(ElectricityManager).GetField("m_electricityGrid",flags).GetValue(e);
        for(int i=0;i<eg.Length;i++)eg[i]=new ElectricityManager.Cell{m_pulseGroup=ushort.MaxValue};
        for(int i=0;i<e.m_nodeGroups.Length;i++)e.m_nodeGroups[i]=ushort.MaxValue;
        var w=Singleton<WaterManager>.instance;var wg=(WaterManager.Cell[])typeof(WaterManager).GetField("m_waterGrid",flags).GetValue(w);
        for(int i=0;i<wg.Length;i++)wg[i]=new WaterManager.Cell{m_waterPulseGroup=ushort.MaxValue,m_sewagePulseGroup=ushort.MaxValue,m_heatingPulseGroup=ushort.MaxValue};
        var wn=(WaterManager.Node[])typeof(WaterManager).GetField("m_nodeData",flags).GetValue(w);
        for(int i=0;i<wn.Length;i++)wn[i]=new WaterManager.Node{m_waterPulseGroup=ushort.MaxValue,m_sewagePulseGroup=ushort.MaxValue,m_heatingPulseGroup=ushort.MaxValue};
        e.UpdateGrid(25300,24200,25800,24500);w.UpdateGrid(25300,24200,25800,24500);
        Entry.Log("FIXTURE: complete electricity/water state cleared, including native/expanded queues, pulse groups and scheduling cursors; no pressure injected.");
    }
    public static void Run(bool clear)
    {
        var ids=File.ReadAllLines(Manifest);homes=new[]{ushort.Parse(ids[2]),ushort.Parse(ids[3])};electricity=new int[2];water=new int[2];
        var buildings=Singleton<BuildingManager>.instance;var citizens=Singleton<CitizenManager>.instance;
        foreach(ushort home in homes)
        {
            if(!(buildings.m_buildings.m_buffer[home].Info.m_buildingAI is ResidentialBuildingAI) || buildings.m_buildings.m_buffer[home].m_position.x<24000 || (buildings.m_buildings.m_buffer[home].m_flags&Building.Flags.Created)==0)throw new Exception("Far consumer identity differs.");
            int count=0;uint unit=buildings.m_buildings.m_buffer[home].m_citizenUnits;
            for(int visited=0;unit!=0 && visited<524288;visited++,unit=citizens.m_units.m_buffer[unit].m_nextUnit)
                for(int i=0;i<5;i++){uint id=citizens.m_units.m_buffer[unit].GetCitizen(i);if(id!=0 && citizens.m_citizens.m_buffer[id].m_homeBuilding==home)count++;}
            if(count!=4)throw new Exception("Native home lost its four residents: "+home+" count="+count);
            if(clear){buildings.m_buildings.m_buffer[home].m_electricityBuffer=0;buildings.m_buildings.m_buffer[home].m_waterBuffer=0;}
        }
        if(clear)ResetGrids();
        var harmony=new Harmony("HundredTiles.Isolated.ConsumerMeter");var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        harmony.Patch(typeof(ElectricityManager).GetMethod("TryFetchElectricity",new[]{typeof(Vector3),typeof(int),typeof(int)}),postfix:new HarmonyMethod(typeof(LiveConsumerChecks).GetMethod("ElectricityFetched")));
        harmony.Patch(typeof(WaterManager).GetMethod("TryFetchWater",new[]{typeof(Vector3),typeof(int),typeof(int),typeof(byte).MakeByRefType()}),postfix:new HarmonyMethod(typeof(LiveConsumerChecks).GetMethod("WaterFetched")));
        sewageNode=ids.Length>6?buildings.m_buildings.m_buffer[ushort.Parse(ids[6])].m_netNode:(ushort)0;sewageFetched=0;
        if(sewageNode!=0)harmony.Patch(typeof(WaterManager).GetMethod("TryFetchSewage",new[]{typeof(ushort),typeof(int),typeof(int)}),postfix:new HarmonyMethod(typeof(LiveConsumerChecks).GetMethod("SewageFetched")));
        var simulation=Singleton<SimulationManager>.instance;uint original=simulation.m_currentFrameIndex;var clock=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var eStep=typeof(ElectricityManager).GetMethod("SimulationStepImpl",flags);var wStep=typeof(WaterManager).GetMethod("SimulationStepImpl",flags);
            for(uint offset=0;offset<4096;offset++)
            {
                simulation.m_currentFrameIndex=(original&~255u)+offset;
                if((offset&63)==0)
                {
                    for(int i=0;i<4;i++){ushort id=ushort.Parse(ids[i]);buildings.m_buildings.m_buffer[id].Info.m_buildingAI.SimulationStep(id,ref buildings.m_buildings.m_buffer[id]);}
                    if(sewageNode!=0){ushort id=ushort.Parse(ids[6]);buildings.m_buildings.m_buffer[id].Info.m_buildingAI.SimulationStep(id,ref buildings.m_buildings.m_buffer[id]);}
                }
                eStep.Invoke(Singleton<ElectricityManager>.instance,new object[]{1});wStep.Invoke(Singleton<WaterManager>.instance,new object[]{1});
            }
            for(int i=0;i<homes.Length;i++)
            {
                var data=buildings.m_buildings.m_buffer[homes[i]];
                Entry.Log("Actual far household "+homes[i]+": electricity fetched="+electricity[i]+", water fetched="+water[i]+", buffers="+data.m_electricityBuffer+"/"+data.m_waterBuffer+", flags="+data.m_flags+", problems="+data.m_problems);
                if(electricity[i]<=0 || water[i]<=0 || (data.m_flags&Building.Flags.Created)==0 || (data.m_flags&(Building.Flags.Abandoned|Building.Flags.Collapsed|Building.Flags.Demolishing))!=0 || !buildings.m_buildings.m_buffer[homes[i]].CheckZoning(ItemClass.Zone.ResidentialLow,ItemClass.Zone.None,false))throw new Exception("Actual residential consumption/supply/zoning failed.");
                if(sewageNode!=0){bool supplied,drained;byte pollution;Singleton<WaterManager>.instance.CheckWater(data.m_position,out supplied,out drained,out pollution);if(!drained || sewageFetched==0)throw new Exception("Native household wastewater treatment failed.");}
            }
            Entry.Log("PASS: two far residential AIs with eight native citizens repeatedly fetch electricity and water from real providers over sixteen cycles; elapsed ms="+clock.ElapsedMilliseconds+". Not a normal-time long-duration or large-city benchmark.");
            if(sewageNode!=0)Entry.Log("PASS: actual inland treatment AI fetched household sewage="+sewageFetched+"; both far homes have native drainage coverage.");
        }
        finally{harmony.UnpatchAll(harmony.Id);simulation.m_currentFrameIndex=original;}
        LivePathChecks.Far();
    }
    public static void ElectricityFetched(Vector3 __0,int __result){Meter(__0,__result,electricity);}
    public static void WaterFetched(Vector3 __0,int __result){Meter(__0,__result,water);}
    public static void SewageFetched(ushort __0,int __1,int __2,int __result){if(fetchTrace++<16)Entry.Log("SEWAGE FETCH API: node="+__0+", expected="+sewageNode+", rate/max="+__1+"/"+__2+", result="+__result);if(__0==sewageNode && __result>0)sewageFetched+=__result;}
    public static void SewageDumped(Vector3 __0,int __result){Meter(__0,__result,sewageOutput);}
    public static void ProviderGoods(WaterFacilityAI __instance,ushort __0,ref Building __1,int __3,int __4,out int[] __state)
    {
        __state=__0==treatmentBuilding?new[]{__1.m_customBuffer2*1000+__1.m_sewageBuffer,sewageFetched}:null;
        if(goodsTrace>=24)return;var ids=File.ReadAllLines(Manifest);
        if(__0==ushort.Parse(ids[1]) || __0==ushort.Parse(ids[6])){goodsTrace++;ushort node=Singleton<BuildingManager>.instance.m_buildings.m_buffer[__0].m_netNode;int links=0;if(node!=0)for(int i=0;i<8;i++)if(Singleton<NetManager>.instance.m_nodes.m_buffer[node].GetSegment(i)!=0)links++;var state=Singleton<WaterManager>.instance.m_nodeData[node];Entry.Log("PROVIDER GOODS: id="+__0+", requested="+__3+", effective="+__4+", frame="+Singleton<SimulationManager>.instance.m_currentFrameIndex+", node="+node+", actual ref node="+__1.m_netNode+", actual ref flags="+__1.m_flags+", connected segments="+links+", intake/outlet/sewage="+__instance.m_waterIntake+"/"+__instance.m_waterOutlet+"/"+__instance.m_sewageOutlet+", ground="+__instance.m_useGroundWater+", node pressure w/s="+state.m_curWaterPressure+"/"+state.m_curSewagePressure);}
    }
    public static void ProviderGoodsFinished(ref Building __1,int[] __state)
    {
        if(__state==null)return;
        int disposed=__state[0]+sewageFetched-__state[1]-(__1.m_customBuffer2*1000+__1.m_sewageBuffer);
        if(disposed>0)sewageDisposed+=disposed;
    }
    public static System.Collections.IEnumerator NormalRun(Action<Exception> completed)
    {
        var sim=Singleton<SimulationManager>.instance;var pause=typeof(SimulationManager).GetProperty("SimulationPaused");
        object previous=pause.GetValue(sim,null);bool forced=sim.ForcedSimulationPaused;
        var harmony=new Harmony("HundredTiles.Isolated.ConsumerMeter");electricity=new int[2];water=new int[2];sewageOutput=new int[2];sewageFetched=0;sewageDisposed=0;
        harmony.Patch(typeof(ElectricityManager).GetMethod("TryFetchElectricity",new[]{typeof(Vector3),typeof(int),typeof(int)}),postfix:new HarmonyMethod(typeof(LiveConsumerChecks).GetMethod("ElectricityFetched")));
        harmony.Patch(typeof(WaterManager).GetMethod("TryFetchWater",new[]{typeof(Vector3),typeof(int),typeof(int),typeof(byte).MakeByRefType()}),postfix:new HarmonyMethod(typeof(LiveConsumerChecks).GetMethod("WaterFetched")));
        harmony.Patch(typeof(WaterManager).GetMethod("TryFetchSewage",new[]{typeof(ushort),typeof(int),typeof(int)}),postfix:new HarmonyMethod(typeof(LiveConsumerChecks).GetMethod("SewageFetched")));
        harmony.Patch(typeof(WaterManager).GetMethod("TryDumpSewage",new[]{typeof(Vector3),typeof(int),typeof(int)}),postfix:new HarmonyMethod(typeof(LiveConsumerChecks).GetMethod("SewageDumped")));
        goodsTrace=0;fetchTrace=0;harmony.Patch(typeof(WaterFacilityAI).GetMethod("ProduceGoods",BindingFlags.Instance|BindingFlags.NonPublic),prefix:new HarmonyMethod(typeof(LiveConsumerChecks).GetMethod("ProviderGoods")),postfix:new HarmonyMethod(typeof(LiveConsumerChecks).GetMethod("ProviderGoodsFinished")));
        float seconds=180;string requested=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_LONG_SECONDS");if(!string.IsNullOrEmpty(requested))seconds=float.Parse(requested,System.Globalization.CultureInfo.InvariantCulture);
        if(seconds<120 || seconds>3600)throw new Exception("Normal-time test duration must be 120..3600 seconds.");
        uint frame=sim.m_currentFrameIndex;float start=Time.realtimeSinceStartup,next=start+30;Exception error=null;bool inspected=false;
        ToolsModifierControl.SetTool<DefaultTool>();pause.SetValue(sim,false,null);sim.ForcedSimulationPaused=false;
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();
        if(camera!=null){camera.ClearTarget();camera.enabled=true;camera.m_targetPosition=camera.m_currentPosition=new Vector3(25560,400,24320);camera.m_targetSize=camera.m_currentSize=500;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,70);camera.m_targetHeight=camera.m_currentHeight=400;}
        LiveRunDiagnostics.Memory();
        try
        {
            while(Time.realtimeSinceStartup-start<seconds)
            {
                if(Time.realtimeSinceStartup>=next){Entry.Log("Normal-time far consumers: elapsed="+(Time.realtimeSinceStartup-start).ToString("F1")+", advanced frames="+(sim.m_currentFrameIndex-frame)+", paused="+pause.GetValue(sim,null)+", forced="+sim.ForcedSimulationPaused+", electricity="+electricity[0]+"/"+electricity[1]+", water="+water[0]+"/"+water[1]+", sewage="+sewageFetched+", dumped="+sewageOutput[0]+"/"+sewageOutput[1]);var providerIds=File.ReadAllLines(Manifest);foreach(int index in new[]{0,1,6}){var data=Singleton<BuildingManager>.instance.m_buildings.m_buffer[ushort.Parse(providerIds[index])];Entry.Log("Normal provider "+providerIds[index]+": flags="+data.m_flags+", problems="+data.m_problems+", rate="+data.m_productionRate+", buffers e/w/s="+data.m_electricityBuffer+"/"+data.m_waterBuffer+"/"+data.m_sewageBuffer+", custom="+data.m_customBuffer1+"/"+data.m_customBuffer2+", node="+data.m_netNode);}yield return new WaitForEndOfFrame();LiveRunDiagnostics.Screenshot("trial324-normal-state.png");next+=30;}
                yield return null;
            }
            sim.ForcedSimulationPaused=true;
            Singleton<SimulationManager>.instance.AddAction(delegate
            {
                try
                {
                    var buildings=Singleton<BuildingManager>.instance;
                    for(int i=0;i<homes.Length;i++)
                    {
                        var data=buildings.m_buildings.m_buffer[homes[i]];bool supplied,drained;byte pollution;
                        Singleton<WaterManager>.instance.CheckWater(data.m_position,out supplied,out drained,out pollution);
                        Entry.Log("Normal-time far home "+homes[i]+": flags="+data.m_flags+", problems="+data.m_problems+", fetch="+electricity[i]+"/"+water[i]+", drainage="+drained);
                        if(electricity[i]<=0 || water[i]<=0 || !drained || (data.m_flags&Building.Flags.Created)==0 || (data.m_flags&(Building.Flags.Abandoned|Building.Flags.Collapsed|Building.Flags.Demolishing))!=0)throw new Exception("Normal-time residential operation failed.");
                    }
                    if(sim.m_currentFrameIndex==frame || sewageFetched==0 || sewageDisposed==0)throw new Exception("Normal-time simulation or sewage collection/disposal did not operate; collected="+sewageFetched+", disposed="+sewageDisposed);
                    Entry.Log("PASS: normal-time far residential operation: "+(sim.m_currentFrameIndex-frame)+" frames over "+seconds+" real seconds; real electricity/water fetch, sewage collected="+sewageFetched+", disposed="+sewageDisposed+" by native inland treatment. Small cohort only.");
                }
                catch(Exception failure){error=failure;}
                finally{inspected=true;}
            });
            while(!inspected)yield return null;
        }
        finally{sim.ForcedSimulationPaused=forced;pause.SetValue(sim,previous,null);harmony.UnpatchAll(harmony.Id);}
        completed(error);
    }
    static void Meter(Vector3 position,int amount,int[] totals)
    {if(amount<=0)return;var buildings=Singleton<BuildingManager>.instance;for(int i=0;i<homes.Length;i++)if((position-buildings.m_buildings.m_buffer[homes[i]].m_position).sqrMagnitude<1)totals[i]+=amount;}
}
