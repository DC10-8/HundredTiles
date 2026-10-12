using System;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.Utilities;
using UnityEngine;

static class LiveGridChecks
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Array Grid(object manager,string name,int length)
    {
        var field=manager.GetType().GetField(name,Fields);
        if(field==null)throw new MissingFieldException(manager.GetType().Name,name);
        var array=(Array)field.GetValue(manager);
        if(array==null || array.Length!=length)throw new Exception(manager.GetType().Name+"."+name+" has unexpected live dimensions.");
        Entry.Log("Live "+manager.GetType().Name+"."+name+": "+array.Length);return array;
    }
    public static void Shapes()
    {
        var terrain=Singleton<TerrainManager>.instance;
        if(terrain.RawHeights.Length!=1201*1201 || terrain.m_patches.Length!=100)throw new Exception("Expanded terrain dimensions differ in Unity.");
        Grid(Singleton<GameAreaManager>.instance,"m_areaGrid",100);
        Grid(Singleton<NetManager>.instance,"m_nodeGrid",90000);Grid(Singleton<NetManager>.instance,"m_segmentGrid",90000);
        Grid(Singleton<BuildingManager>.instance,"m_buildingGrid",90000);Grid(Singleton<BuildingManager>.instance,"m_buildingGrid2",2500);
        Grid(Singleton<ZoneManager>.instance,"m_zoneGrid",90000);
        Grid(Singleton<TreeManager>.instance,"m_treeGrid",360000);Grid(Singleton<PropManager>.instance,"m_propGrid",90000);
        Grid(Singleton<VehicleManager>.instance,"m_vehicleGrid",360000);Grid(Singleton<VehicleManager>.instance,"m_parkedGrid",360000);
        Grid(Singleton<CitizenManager>.instance,"m_citizenGrid",5760000);
        Grid(Singleton<DistrictManager>.instance,"m_districtGrid",1000000);Grid(Singleton<DistrictManager>.instance,"m_parkGrid",1000000);
        Grid(Singleton<NaturalResourceManager>.instance,"m_naturalResources",323761);Grid(Singleton<NaturalResourceManager>.instance,"m_areaResources",100);
        Grid(Singleton<ElectricityManager>.instance,"m_electricityGrid",258064);Grid(Singleton<WaterManager>.instance,"m_waterGrid",258064);
        Grid(Singleton<RenderManager>.instance,"m_groups",2500);Grid(Singleton<RenderManager>.instance,"m_megaGroups",100);
        Entry.Log("PASS: expanded live terrain, ownership, network, building, zone, object, transport, district, resource, supply and render arrays in Unity.");
    }
    public static void ServiceQuery()
    {
        int local,total;var position=new Vector3((499-225+.5f)*38.4f,0,(499-225+.5f)*38.4f);
        Singleton<ImmaterialResourceManager>.instance.CheckResource((ImmaterialResourceManager.Resource)7,position,out local,out total);
        if(local!=32123)throw new Exception("Real expanded service query did not reach the saved corner.");
        Entry.Log("PASS: actual ImmaterialResourceManager.CheckResource resolves saved added-corner value at "+position);
    }
    public static void ServiceCycle()
    {
        var manager=Singleton<ImmaterialResourceManager>.instance;
        var temp=(ushort[])Grid(manager,"m_localTempResources",ServiceGrid.Values);
        var final=(ushort[])Grid(manager,"m_localFinalResources",ServiceGrid.Values);
        var globalTemp=(int[])Grid(manager,"m_globalTempResources",35);var globalFinal=(int[])Grid(manager,"m_globalFinalResources",35);
        var totalTemp=(int[])Grid(manager,"m_totalTempResources",35);var totalFinal=(int[])Grid(manager,"m_totalFinalResources",35);
        var mul=(long[])Grid(manager,"m_totalTempResourcesMul",35);
        uint frame=Singleton<SimulationManager>.instance.m_currentFrameIndex;
        try
        {
            for(uint i=0;i<256;i++)
            {
                Singleton<SimulationManager>.instance.m_currentFrameIndex=i;
                ServiceHooks.Step(manager,1,temp,final,globalTemp,globalFinal,totalTemp,totalFinal,mul);
            }
        }
        finally{Singleton<SimulationManager>.instance.m_currentFrameIndex=frame;}
        foreach(ushort value in temp)if(value!=0)throw new Exception("Actual service cycle failed to consume an expanded temporary cell.");
        foreach(int value in totalTemp)if(value!=0)throw new Exception("Actual service totals failed to reset.");
        foreach(long value in mul)if(value!=0)throw new Exception("Actual service weighted totals failed to reset.");
        Entry.Log("PASS: all 256 service phases / 250000 cells executed using actual game calculation, district/terrain queries and StatisticsManager; temporary values and counters cleared.");
    }
}
