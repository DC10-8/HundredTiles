using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

static class LiveCompatibilityChecks
{
    static readonly System.Collections.Generic.List<ushort> Buildings=new System.Collections.Generic.List<ushort>();
    public static void Run()
    {
        Assembly moveIt=null;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="MoveIt")moveIt=assembly;
        if(moveIt==null)throw new Exception("Actual Move It assembly missing.");
        var type=moveIt.GetType("MoveIt.MoveableBuilding",true);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var add=type.GetMethod("AddToGrid",flags);var remove=type.GetMethod("RemoveFromGrid",flags);
        var manager=Singleton<BuildingManager>.instance;var random=new Randomizer(817);
        foreach(float x in new[]{2000f,9900f})
        {
            ushort id;var position=new Vector3(x,80,9600);
            if(x==2000)position.z=2000;
            position.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(position);
            if(!manager.CreateBuilding(out id,ref random,PrefabCollection<BuildingInfo>.FindLoaded("Wind Turbine"),position,0,0,88))throw new Exception("Compatibility fixture placement failed.");
            int index=(int)(position.z/64+135)*300+(int)(position.x/64+135);
            if(!Contains(manager,index,id))throw new Exception("Native initial building grid entry missing.");
            object[] args={id,manager.m_buildings.m_buffer[id]};remove.Invoke(null,args);manager.m_buildings.m_buffer[id]=(Building)args[1];
            if(Contains(manager,index,id))throw new Exception("Move It failed to remove building at "+position);
            position.x+=128;var data=manager.m_buildings.m_buffer[id];data.m_position=position;args[1]=data;add.Invoke(null,args);manager.m_buildings.m_buffer[id]=(Building)args[1];
            int next=(int)(position.z/64+135)*300+(int)(position.x/64+135);
            if(!Contains(manager,next,id) || Contains(manager,index,id))throw new Exception("Move It relocation corrupted grid links.");
            manager.m_buildings.m_buffer[id].m_flags|=Building.Flags.Completed;
            manager.UpdateBuildingRenderer(id,true);
            Buildings.Add(id);
        }
        moveIt.GetType("MoveIt.MoveItTool",true).GetMethod("UpdateRender",flags).Invoke(null,new object[]{new Bounds(new Vector3(10000,80,10000),new Vector3(128,100,128))});
        Entry.Log("PASS: actual Move It removal/insertion at original and added tiles and expanded render update.");
        Heating();
        NoUtilityHeating();
    }
    static void NoUtilityHeating()
    {
        Assembly assembly=null;foreach(var item in AppDomain.CurrentDomain.GetAssemblies())if(item.GetName().Name=="NoUtilityNetworks")assembly=item;
        if(assembly==null)throw new Exception("No Utility Networks test assembly missing.");
        var heating=assembly.GetType("NoUtilityNetworks.HeatingManagerMod",true);
        heating.GetMethod("Init").Invoke(null,null);
        var updater=assembly.GetType("NoUtilityNetworks.CapacityUpdater",true);var instance=Activator.CreateInstance(updater);
        // Reproduce the zero-demand production stall, then call the real updater.
        heating.GetField("HeatingCapacity").SetValue(null,0);
        if((int)heating.GetMethod("DumpHeating").Invoke(null,new object[]{300})!=0)throw new Exception("Zero-capacity reproduction failed.");
        updater.GetMethod("OnBeforeSimulationTick").Invoke(instance,null);
        if((int)heating.GetField("HeatingCapacity").GetValue(null)<100000 || (int)heating.GetField("CurrentHeating").GetValue(null)!=0)throw new Exception("Heating reserve correction created heat or failed.");
        assembly.GetType("NoUtilityNetworks.Patcher",true).GetMethod("PatchAll").Invoke(null,null);
        var water=Singleton<WaterManager>.instance;bool connected,available;
        if(water.TryFetchHeating(new Vector3(10000,80,10100),100,100,out connected)!=0)throw new Exception("Empty No Utility pool supplied free heat.");
        if(water.TryDumpHeating(1,300,300)!=300 || water.TryFetchHeating(new Vector3(10000,80,10100),100,100,out connected)!=100 || !connected)throw new Exception("No Utility production/consumption failed after capacity update.");
        water.CheckHeating(new Vector3(10000,80,10100),out available);if(!available)throw new Exception("No Utility heating check failed after production.");
        Entry.Log("PASS: reproduced zero-capacity No Utility heating stall; actual updater preserves an empty reserve, real supply accepts 300 units and consumer receives 100; no heat created by adapter.");
    }
    public static void Hover()
    {
        Assembly moveIt=null;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="MoveIt")moveIt=assembly;
        var type=moveIt.GetType("MoveIt.MoveItTool",true);var tool=type.GetField("instance").GetValue(null);
        if(tool==null)throw new Exception("Actual Move It tool did not initialize.");
        ToolsModifierControl.toolController.CurrentTool=(ToolBase)tool;
        ((ToolBase)tool).enabled=true;
        var raycast=type.GetMethod("RaycastHoverInstance",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(ushort id in Buildings)
        {
            var position=Singleton<BuildingManager>.instance.m_buildings.m_buffer[id].m_position;
            raycast.Invoke(tool,new object[]{new Ray(position+new Vector3(0,250,0),Vector3.down)});
            var hovered=type.GetField("m_hoverInstance").GetValue(tool);
            if(hovered==null)throw new Exception("Move It did not hover building "+id+" at "+position);
            var instanceId=(InstanceID)hovered.GetType().GetProperty("id").GetValue(hovered,null);
            if(instanceId.Building!=id)throw new Exception("Move It selected a different instance: "+instanceId);
        }
        Entry.Log("PASS: actual Move It hover ray selects buildings at original and added tiles after relocation.");
    }
    static bool Contains(BuildingManager manager,int index,ushort id)
    {
        ushort current=manager.m_buildingGrid[index];int count=0;
        while(current!=0){if(current==id)return true;current=manager.m_buildings.m_buffer[current].m_nextGridBuilding;if(++count>manager.m_buildings.m_buffer.Length)throw new Exception("Building grid cycle.");}return false;
    }
    static void Heating()
    {
        NetInfo pipe=null;for(uint i=0;i<PrefabCollection<NetInfo>.LoadedCount();i++)
        {var info=PrefabCollection<NetInfo>.GetLoaded(i);if(info!=null && info.m_class.m_service==ItemClass.Service.Water && info.m_class.m_level==ItemClass.Level.Level2){pipe=info;break;}}
        if(pipe==null)throw new Exception("Heating pipe prefab missing.");
        var manager=Singleton<NetManager>.instance;var random=new Randomizer(876);ushort a,b,segment;
        if(!manager.CreateNode(out a,ref random,pipe,new Vector3(9900,75,10100),90) || !manager.CreateNode(out b,ref random,pipe,new Vector3(10100,75,10100),91) || !manager.CreateSegment(out segment,ref random,pipe,a,b,Vector3.right,Vector3.left,92,92,false))throw new Exception("Heating pipe fixture failed.");
        var water=Singleton<WaterManager>.instance;water.UpdateGrid(9800,10000,10200,10200);
        var simulation=Singleton<SimulationManager>.instance;uint frame=simulation.m_currentFrameIndex;
        var step=typeof(WaterManager).GetMethod("SimulationStepImpl",BindingFlags.NonPublic|BindingFlags.Instance);
        try{for(uint i=0;i<1024;i++){simulation.m_currentFrameIndex=i;water.TryDumpHeating(a,30000,30000);step.Invoke(water,new object[]{1});}}
        finally{simulation.m_currentFrameIndex=frame;}
        bool connected,heating;int fetched=water.TryFetchHeating(new Vector3(10000,80,10100),100,100,out connected);water.CheckHeating(new Vector3(10000,80,10100),out heating);
        if(!connected || fetched!=100 || !heating)throw new Exception("Expanded heating supply failed: connected="+connected+", fetched="+fetched+", CheckHeating="+heating);
        Entry.Log("PASS: native heating pipe propagation and consumer fetch deliver 100 units in added tiles.");
    }
}
