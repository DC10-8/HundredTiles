using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

// Infrastructure for the generated load fixture only. No utility pressure or
// residential inventory is assigned, and abandonment remains enabled.
static class LivePopulationInfrastructureChecks
{
    public static bool Enabled {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_POPULATION_POWER")=="1";}}
    static readonly List<ushort> Plants=new List<ushort>();
    const string MeterOwner="HundredTiles.Isolated.PopulationPower";
    static long accepted;
    public static void Prepare()
    {
        if(!Enabled)return;
        if(HundredTiles.Core.MapLayout.Active.TileCount!=324 || !File.Exists(Path.Combine(Entry.Root,"isolated-test.marker")))throw new Exception("Population power requires the isolated 324 fixture.");
        var info=PrefabCollection<BuildingInfo>.FindLoaded("Nuclear Power Plant");
        var ai=info==null?null:info.m_buildingAI as PowerPlantAI;
        if(ai==null || ai.m_resourceType!=TransferManager.TransferReason.None)throw new Exception("Native fuel-independent nuclear power prefab missing.");
        var manager=Singleton<BuildingManager>.instance;var graph=Singleton<NetManager>.instance;
        for(int i=1;i<manager.m_buildings.m_buffer.Length;i++)
        {
            var data=manager.m_buildings.m_buffer[i];
            if((data.m_flags&Building.Flags.Created)!=0 && ((data.m_position.x>=10700 && data.m_position.x<=12520 && data.m_position.z>=8500 && data.m_position.z<=10400) || (data.m_position.x>=10344 && data.m_position.x<=10856 && data.m_position.z>=8144 && data.m_position.z<=8656)))throw new Exception("Power fixture would overlap an existing building: "+i);
        }
        LiveTerrainToolChecks.Prepare();LiveTerrainToolChecks.LevelArea(new Vector3(11600,80,9500),1760,80);
        LiveTerrainToolChecks.LevelArea(new Vector3(10600,80,8400),512,80);
        var economy=Singleton<EconomyManager>.instance;economy.AddResource(EconomyManager.Resource.RewardAmount,100000000,info.m_class);
        foreach(bool night in new[]{false,true})economy.SetBudget(ItemClass.Service.Electricity,ItemClass.SubService.None,100,night);
        var random=new Randomizer(6500324);var road=PrefabCollection<NetInfo>.FindLoaded("Basic Road");var power=PrefabCollection<NetInfo>.FindLoaded("Power Line");
        float collector=-1;for(int i=1;i<graph.m_nodes.m_buffer.Length;i++){var node=graph.m_nodes.m_buffer[i];if((node.m_flags&NetNode.Flags.Created)!=0 && node.Info==road && Math.Abs(node.m_position.x-8800)<.01f && node.m_position.z>=0 && node.m_position.z<8500)collector=Math.Max(collector,node.m_position.z);}
        if(collector<0)throw new Exception("Original housing collector road endpoint missing.");
        float street=9000-info.m_cellLength*4-8;
        Chain(graph,road,ref random,new Vector3(10800,80,street),new Vector3(12440,80,street));
        // The collector joins the city's existing north/south residential road.
        Chain(graph,road,ref random,new Vector3(8800,80,collector),new Vector3(8800,80,8400));
        Chain(graph,road,ref random,new Vector3(8800,80,8400),new Vector3(10800,80,8400));
        Chain(graph,road,ref random,new Vector3(10800,80,8400),new Vector3(10800,80,street));
        for(int i=0;i<8;i++)
        {
            ushort id;var position=new Vector3(10840+i*180,80,9000);
            if(!manager.CreateBuilding(out id,ref random,info,position,Mathf.PI,info.m_cellLength,Singleton<SimulationManager>.instance.m_currentBuildIndex++))throw new Exception("Power fixture building capacity exhausted.");
            var frame=manager.m_buildings.m_buffer[id].GetLastFrameData();frame.m_constructState=255;
            manager.m_buildings.m_buffer[id].SetFrameData(Singleton<SimulationManager>.instance.m_currentFrameIndex,frame);
            info.m_buildingAI.SimulationStep(id,ref manager.m_buildings.m_buffer[id]);Plants.Add(id);
            if((manager.m_buildings.m_buffer[id].m_flags&Building.Flags.Completed)==0)throw new Exception("Native power fixture completion failed.");
        }
        float busbar=9000+info.m_cellLength*4+24;
        Chain(graph,power,ref random,new Vector3(8816,80,0),new Vector3(8816,80,8400));
        Chain(graph,power,ref random,new Vector3(8816,80,8400),new Vector3(10768,80,8400));
        Chain(graph,power,ref random,new Vector3(10768,80,8400),new Vector3(10768,80,busbar));
        Chain(graph,power,ref random,new Vector3(10768,80,busbar),new Vector3(12440,80,busbar));
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(graph,new object[]{0});
        typeof(BuildingManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,new object[]{0});TerrainModify.RefreshAllModifications();
        Singleton<ElectricityManager>.instance.UpdateGrid(8700,-100,12550,10400);
        accepted=0;
        new HarmonyLib.Harmony(MeterOwner).Patch(typeof(ElectricityManager).GetMethod("TryDumpElectricity",new[]{typeof(Vector3),typeof(int),typeof(int)}),postfix:new HarmonyLib.HarmonyMethod(typeof(LivePopulationInfrastructureChecks).GetMethod("Dumped")));
        Entry.Log("FIXTURE: eight native "+info.name+" buildings, rated production each="+ai.m_electricityProduction+", footprint="+info.m_cellWidth+"x"+info.m_cellLength+", roadside access and power-line spine added before normal operation. No electricity grid pressure or household buffer was assigned.");
    }
    static void Chain(NetManager graph,NetInfo info,ref Randomizer random,Vector3 start,Vector3 end)
    {
        int pieces=Mathf.CeilToInt(Vector3.Distance(start,end)/128);ushort previous=0;
        for(int i=0;i<=pieces;i++)
        {
            var point=Vector3.Lerp(start,end,(float)i/pieces);ushort node=0;
            // Reuse coincident live endpoints so these are connected networks.
            for(int j=1;j<graph.m_nodes.m_buffer.Length;j++)if((graph.m_nodes.m_buffer[j].m_flags&NetNode.Flags.Created)!=0 && graph.m_nodes.m_buffer[j].Info==info && (graph.m_nodes.m_buffer[j].m_position-point).sqrMagnitude<.01f){node=(ushort)j;break;}
            if(node==0 && !graph.CreateNode(out node,ref random,info,point,Singleton<SimulationManager>.instance.m_currentBuildIndex++))throw new Exception("Power fixture node capacity exhausted.");
            if(previous!=0){ushort segment;var direction=(point-graph.m_nodes.m_buffer[previous].m_position).normalized;if(!graph.CreateSegment(out segment,ref random,info,previous,node,direction,-direction,Singleton<SimulationManager>.instance.m_currentBuildIndex++,Singleton<SimulationManager>.instance.m_currentBuildIndex,false))throw new Exception("Power fixture segment capacity exhausted.");}previous=node;
        }
    }
    public static void Observe()
    {
        if(!Enabled)return;int supplied=0,total=0;var buildings=Singleton<BuildingManager>.instance;var electricity=Singleton<ElectricityManager>.instance;
        foreach(string line in File.ReadAllLines(Path.Combine(Entry.Root,"population-"+LiveLargeCityChecks.Target+"-homes.txt")))
        {ushort id=ushort.Parse(line.Split('\t')[0]);bool connected;electricity.CheckElectricity(buildings.m_buildings.m_buffer[id].m_position,out connected);if(connected)supplied++;total++;}
        long production=0;var states=new List<string>();foreach(ushort id in Plants){var data=buildings.m_buildings.m_buffer[id];production+=data.Info.m_buildingAI.GetElectricityRate(id,ref buildings.m_buildings.m_buffer[id]);states.Add(id+":"+data.m_flags+":"+data.m_problems);}
        Entry.Log("Population native power: supplied homes="+supplied+" / "+total+", current plant rate="+production+", actual accepted generation="+accepted+", plants="+string.Join("; ",states.ToArray()));
    }
    public static void Dumped(Vector3 __0,int __result)
    {
        if(__result<=0)return;var buildings=Singleton<BuildingManager>.instance.m_buildings.m_buffer;
        foreach(ushort id in Plants)if((buildings[id].m_position-__0).sqrMagnitude<.1f){accepted+=__result;break;}
    }
    public static void Finish()
    {
        if(!Enabled)return;new HarmonyLib.Harmony(MeterOwner).UnpatchAll(MeterOwner);
        if(accepted<=0)throw new Exception("Population plants delivered no native electricity.");
        int supplied=0,total=0;var buildings=Singleton<BuildingManager>.instance.m_buildings.m_buffer;
        foreach(string line in File.ReadAllLines(Path.Combine(Entry.Root,"population-"+LiveLargeCityChecks.Target+"-homes.txt")))
        {ushort id=ushort.Parse(line.Split('\t')[0]);bool connected;Singleton<ElectricityManager>.instance.CheckElectricity(buildings[id].m_position,out connected);if(connected)supplied++;total++;}
        if(supplied<total*.95)throw new Exception("Actual population power network did not reach 95% of original homes: "+supplied+" / "+total);
        Entry.Log("PASS: real nuclear plant AI delivered "+accepted+" native electricity units during normal-time operation; "+supplied+" / "+total+" original home positions supplied. Diagnostic metering removed before saving.");
    }
}
