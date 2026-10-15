using System;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

static class LiveSupplyChecks
{
    static bool Far {get{return HundredTiles.Core.MapLayout.Active.TileCount==324 && Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_324_FAR")=="1";}}
    static float X(float x){return Far?x+15600:x;} static float Z(float z){return Far?z+14600:z;} static Vector3 Point(float x,float y,float z){return new Vector3(X(x),Far?y+320:y,Z(z));}
    static string Manifest { get { return Path.Combine(Entry.Root,Far?"trial324-far-supply.txt":"outer-supply.txt"); } }
    public static void Create()
    {
        var random=new Randomizer(71);ushort building,a,b,segment;
        var buildings=Singleton<BuildingManager>.instance;var turbine=PrefabCollection<BuildingInfo>.FindLoaded("Wind Turbine");
        if(turbine==null || !buildings.CreateBuilding(out building,ref random,turbine,Point(9800,80,9740),0,0,7))throw new Exception("Outer turbine placement failed.");
        buildings.m_buildings.m_buffer[building].m_flags|=Building.Flags.Completed;
        var pipes=Singleton<NetManager>.instance;var pipe=PrefabCollection<NetInfo>.FindLoaded("Water Pipe");
        if(pipe==null || !pipes.CreateNode(out a,ref random,pipe,Point(9800,75,9700),8) || !pipes.CreateNode(out b,ref random,pipe,Point(10000,75,9700),9))throw new Exception("Outer pipe node placement failed.");
        if(!pipes.CreateSegment(out segment,ref random,pipe,a,b,Vector3.right,Vector3.left,10,10,false))throw new Exception("Outer pipe placement failed.");
        Singleton<ElectricityManager>.instance.UpdateGrid(X(9700),Z(9600),X(10100),Z(9850));
        Singleton<WaterManager>.instance.UpdateGrid(X(9700),Z(9600),X(10100),Z(9850));
        File.WriteAllText(Manifest,building+"\n"+a+"\n"+b+"\n"+segment);
        Cycle();Check();
    }
    public static void Check()
    {
        var ids=File.ReadAllLines(Manifest);ushort building=ushort.Parse(ids[0]),a=ushort.Parse(ids[1]),b=ushort.Parse(ids[2]),segment=ushort.Parse(ids[3]);
        if((Singleton<BuildingManager>.instance.m_buildings.m_buffer[building].m_position-Point(9800,80,9740)).sqrMagnitude>4)throw new Exception("Outer turbine position differs.");
        var graph=Singleton<NetManager>.instance;
        if(graph.m_segments.m_buffer[segment].m_startNode!=a || graph.m_segments.m_buffer[segment].m_endNode!=b)throw new Exception("Outer pipe identity differs.");
        bool electric,water,sewage;byte pollution;
        Singleton<ElectricityManager>.instance.CheckElectricity(Point(9800,80,9740),out electric);
        Singleton<WaterManager>.instance.CheckWater(Point(9900,80,9700),out water,out sewage,out pollution);
        if(!electric || !water)throw new Exception("Outer supply did not reach consumers: electricity="+electric+", water="+water);
        Entry.Log("PASS: actual electricity and pipe water propagation reach the added corner; native turbine/pipe identities and supply queries verified.");
    }
    public static void Observe(string stage)
    {
        var point=Point(9900,80,9700);var manager=Singleton<WaterManager>.instance;bool water,sewage;byte pollution;
        manager.CheckWater(point,out water,out sewage,out pollution);
        var grid=(WaterManager.Cell[])typeof(WaterManager).GetField("m_waterGrid",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(manager);
        int x=HundredTiles.Core.UtilityGrid.WorldToCell(point.x),z=HundredTiles.Core.UtilityGrid.WorldToCell(point.z);var cell=grid[z*HundredTiles.Core.UtilityGrid.Side+x];
        Entry.Log("Supply snapshot "+stage+": water="+water+", sewage="+sewage+", conductivity="+cell.m_conductivity+", hasWater="+cell.m_hasWater+", temporary="+cell.m_tmpHasWater+", pressure="+cell.m_currentWaterPressure+", frame="+Singleton<SimulationManager>.instance.m_currentFrameIndex);
    }
    public static void CheckRealElectricityProduction()
    {
        var ids=File.ReadAllLines(Manifest);ushort source=ushort.Parse(ids[0]);
        var manager=Singleton<ElectricityManager>.instance;var graph=Singleton<BuildingManager>.instance;
        var fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var grid=(ElectricityManager.Cell[])typeof(ElectricityManager).GetField("m_electricityGrid",fields).GetValue(manager);
        for(int i=0;i<grid.Length;i++)grid[i]=new ElectricityManager.Cell{m_pulseGroup=ushort.MaxValue};
        foreach(string name in new[]{"m_pulseGroupCount","m_pulseUnitStart","m_pulseUnitEnd","m_processedCells","m_conductiveCells"})typeof(ElectricityManager).GetField(name,fields).SetValue(manager,0);
        manager.UpdateGrid(X(9700),Z(9600),X(10100),Z(9850));
        var simulation=Singleton<SimulationManager>.instance;uint originalFrame=simulation.m_currentFrameIndex;
        var step=typeof(ElectricityManager).GetMethod("SimulationStepImpl",fields);
        try
        {
            for(uint offset=0;offset<2048;offset++)
            {
                simulation.m_currentFrameIndex=(originalFrame&~255u)+offset;
                if((offset&63)==0)graph.m_buildings.m_buffer[source].Info.m_buildingAI.SimulationStep(source,ref graph.m_buildings.m_buffer[source]);
                step.Invoke(manager,new object[]{1});
            }
            bool supplied;manager.CheckElectricity(graph.m_buildings.m_buffer[source].m_position,out supplied);
            Entry.Log("Actual turbine AI production: wind="+Singleton<WeatherManager>.instance.SampleWindSpeed(graph.m_buildings.m_buffer[source].m_position,false)+", flags="+graph.m_buildings.m_buffer[source].m_flags+", supplied="+supplied);
            if(!supplied)throw new Exception("Actual turbine AI did not produce electricity after clearing the injected snapshot.");
            Entry.Log("PASS: native turbine AI produces electricity at the far position over eight utility cycles; no harness TryDumpElectricity injection. Actual consumer operation not asserted.");
        }
        finally{simulation.m_currentFrameIndex=originalFrame;}
    }
    public static void CheckRealWaterProduction()
    {
        if(!Far)throw new Exception("Actual water production check requires the isolated far fixture.");
        ushort tower=LiveConstructionChecks.PlaceBuilding("Water Tower",new Vector3(25460,0,24320));
        var buildings=Singleton<BuildingManager>.instance;var graph=Singleton<NetManager>.instance;
        buildings.m_buildings.m_buffer[tower].m_flags|=Building.Flags.Completed;
        ushort node=buildings.m_buildings.m_buffer[tower].m_netNode;
        Entry.Log("Actual water tower constructed: building="+tower+", native pipe node="+node);
        if(node==0)throw new Exception("Native water tower has no connection node; automatic connection remains unverified.");
        ushort target=ushort.Parse(File.ReadAllLines(Manifest)[1]),segment;var random=new Randomizer(182);
        var direction=(graph.m_nodes.m_buffer[target].m_position-graph.m_nodes.m_buffer[node].m_position).normalized;
        if(!graph.CreateSegment(out segment,ref random,PrefabCollection<NetInfo>.FindLoaded("Water Pipe"),node,target,direction,-direction,20,20,false))throw new Exception("Actual water tower pipe connection failed.");
        var manager=Singleton<WaterManager>.instance;var fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var grid=(WaterManager.Cell[])typeof(WaterManager).GetField("m_waterGrid",fields).GetValue(manager);
        for(int i=0;i<grid.Length;i++)grid[i]=new WaterManager.Cell{m_waterPulseGroup=ushort.MaxValue,m_sewagePulseGroup=ushort.MaxValue,m_heatingPulseGroup=ushort.MaxValue};
        var nodes=(WaterManager.Node[])typeof(WaterManager).GetField("m_nodeData",fields).GetValue(manager);
        for(int i=0;i<nodes.Length;i++)nodes[i]=new WaterManager.Node{m_waterPulseGroup=ushort.MaxValue,m_sewagePulseGroup=ushort.MaxValue,m_heatingPulseGroup=ushort.MaxValue};
        manager.UpdateGrid(25300,24200,25700,24450);
        var simulation=Singleton<SimulationManager>.instance;uint originalFrame=simulation.m_currentFrameIndex;
        var step=typeof(WaterManager).GetMethod("SimulationStepImpl",fields);
        try
        {
            for(uint offset=0;offset<2048;offset++)
            {
                simulation.m_currentFrameIndex=(originalFrame&~255u)+offset;
                if((offset&63)==0)buildings.m_buildings.m_buffer[tower].Info.m_buildingAI.SimulationStep(tower,ref buildings.m_buildings.m_buffer[tower]);
                step.Invoke(manager,new object[]{1});
            }
            bool water,sewage;byte pollution;manager.CheckWater(Point(9900,80,9700),out water,out sewage,out pollution);
            Entry.Log("Actual water tower production: flags="+buildings.m_buildings.m_buffer[tower].m_flags+", water="+water+", pollution="+pollution);
            if(!water)throw new Exception("Actual water tower AI did not supply the cleared far pipe grid.");
            Entry.Log("PASS: native water tower AI supplies the far pipe grid over eight cycles without harness TryDumpWater injection. Residential consumption not asserted.");
        }
        finally{simulation.m_currentFrameIndex=originalFrame;}
    }
    static void Cycle()
    {
        ushort source=ushort.Parse(File.ReadAllLines(Manifest)[1]);var simulation=Singleton<SimulationManager>.instance;uint frame=simulation.m_currentFrameIndex;
        var electricity=Singleton<ElectricityManager>.instance;var water=Singleton<WaterManager>.instance;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var electricityStep=typeof(ElectricityManager).GetMethod("SimulationStepImpl",flags);var waterStep=typeof(WaterManager).GetMethod("SimulationStepImpl",flags);
        try
        {
            for(uint i=0;i<1024;i++)
            {
                simulation.m_currentFrameIndex=i;
                electricity.TryDumpElectricity(Point(9800,80,9740),10000,10000);water.TryDumpWater(source,30000,30000,0);
                electricityStep.Invoke(electricity,new object[]{1});waterStep.Invoke(water,new object[]{1});
            }
        }
        finally{simulation.m_currentFrameIndex=frame;}
        Entry.Log("Executed four actual electricity/water cycles with game supply APIs on expanded grids.");
    }
}
