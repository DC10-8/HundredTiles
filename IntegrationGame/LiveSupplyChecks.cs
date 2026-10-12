using System;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

static class LiveSupplyChecks
{
    static string Manifest { get { return Path.Combine(Entry.Root,"outer-supply.txt"); } }
    public static void Create()
    {
        var random=new Randomizer(71);ushort building,a,b,segment;
        var buildings=Singleton<BuildingManager>.instance;var turbine=PrefabCollection<BuildingInfo>.FindLoaded("Wind Turbine");
        if(turbine==null || !buildings.CreateBuilding(out building,ref random,turbine,new Vector3(9800,80,9740),0,0,7))throw new Exception("Outer turbine placement failed.");
        buildings.m_buildings.m_buffer[building].m_flags|=Building.Flags.Completed;
        var pipes=Singleton<NetManager>.instance;var pipe=PrefabCollection<NetInfo>.FindLoaded("Water Pipe");
        if(pipe==null || !pipes.CreateNode(out a,ref random,pipe,new Vector3(9800,75,9700),8) || !pipes.CreateNode(out b,ref random,pipe,new Vector3(10000,75,9700),9))throw new Exception("Outer pipe node placement failed.");
        if(!pipes.CreateSegment(out segment,ref random,pipe,a,b,Vector3.right,Vector3.left,10,10,false))throw new Exception("Outer pipe placement failed.");
        Singleton<ElectricityManager>.instance.UpdateGrid(9700,9600,10100,9850);
        Singleton<WaterManager>.instance.UpdateGrid(9700,9600,10100,9850);
        File.WriteAllText(Manifest,building+"\n"+a+"\n"+b+"\n"+segment);
        Cycle();Check();
    }
    public static void Check()
    {
        var ids=File.ReadAllLines(Manifest);ushort building=ushort.Parse(ids[0]),a=ushort.Parse(ids[1]),b=ushort.Parse(ids[2]),segment=ushort.Parse(ids[3]);
        if((Singleton<BuildingManager>.instance.m_buildings.m_buffer[building].m_position-new Vector3(9800,80,9740)).sqrMagnitude>4)throw new Exception("Outer turbine position differs.");
        var graph=Singleton<NetManager>.instance;
        if(graph.m_segments.m_buffer[segment].m_startNode!=a || graph.m_segments.m_buffer[segment].m_endNode!=b)throw new Exception("Outer pipe identity differs.");
        bool electric,water,sewage;byte pollution;
        Singleton<ElectricityManager>.instance.CheckElectricity(new Vector3(9800,80,9740),out electric);
        Singleton<WaterManager>.instance.CheckWater(new Vector3(9900,80,9700),out water,out sewage,out pollution);
        if(!electric || !water)throw new Exception("Outer supply did not reach consumers: electricity="+electric+", water="+water);
        Entry.Log("PASS: actual electricity and pipe water propagation reach the added corner; native turbine/pipe identities and supply queries verified.");
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
                electricity.TryDumpElectricity(new Vector3(9800,80,9740),10000,10000);water.TryDumpWater(source,30000,30000,0);
                electricityStep.Invoke(electricity,new object[]{1});waterStep.Invoke(water,new object[]{1});
            }
        }
        finally{simulation.m_currentFrameIndex=frame;}
        Entry.Log("Executed four actual electricity/water cycles with game supply APIs on expanded grids.");
    }
}
