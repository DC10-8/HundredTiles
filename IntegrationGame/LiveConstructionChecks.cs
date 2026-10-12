using System;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

static class LiveConstructionChecks
{
    static BuildingTool building;
    public static void Prepare(){building=ToolsModifierControl.SetTool<BuildingTool>();}
    static void Set(string name,object value){typeof(BuildingTool).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(building,value);}
    public static void Run()
    {
        foreach(string name in new[]{"Basic Road","Train Track"})
        {
            var info=PrefabCollection<NetInfo>.FindLoaded(name);if(info==null)throw new Exception("Construction prefab missing: "+name);
            foreach(var point in new[]{new Vector3(2000,0,2400),new Vector3(9800,0,10300),new Vector3(10300,0,9800)})
            {
                var position=point;position.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(position);
                NetTool.ControlPoint control;
                bool hit=NetTool.MakeControlPoint(new Ray(position+Vector3.up*500,Vector3.down),1000,info,false,NetNode.Flags.All,NetSegment.Flags.All,Building.Flags.All,0,false,out control);
                Entry.Log("Construction control "+name+" at "+position+": hit="+hit+", result="+control.m_position+", outside="+control.m_outside);
                if(!hit || control.m_outside || Math.Abs(control.m_position.x-position.x)>.1f || Math.Abs(control.m_position.z-position.z)>.1f)throw new Exception("Construction control point moved away from requested ground: "+name);
            }
        }
        building.m_prefab=PrefabCollection<BuildingInfo>.FindLoaded("Wind Turbine");
        foreach(var point in new[]{new Vector3(2000,0,2400),new Vector3(9800,0,10300),new Vector3(10300,0,9800)})
        {
            var position=point;position.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(position);
            Set("m_mouseRay",new Ray(position+Vector3.up*500,Vector3.down));Set("m_mouseRayLength",1000f);Set("m_mouseRayValid",true);
            building.SimulationStep();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            Entry.Log("Construction building at "+position+": result="+typeof(BuildingTool).GetField("m_mousePosition",flags).GetValue(building)+", errors="+typeof(BuildingTool).GetField("m_placementErrors",flags).GetValue(building));
            if((ToolBase.ToolErrors)typeof(BuildingTool).GetField("m_placementErrors",flags).GetValue(building)!=ToolBase.ToolErrors.None)throw new Exception("Ground building placement rejected.");
        }
        BuildNetwork("Basic Road",new Vector3(9700,0,10300));
        BuildNetwork("Train Track",new Vector3(9700,0,10100));
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Singleton<NetManager>.instance,new object[]{0});
        typeof(ZoneManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Singleton<ZoneManager>.instance,new object[]{0});
        PlaceBuilding("Wind Turbine",new Vector3(10300,0,9800));
        string roadside=null; for(uint i=0;i<PrefabCollection<BuildingInfo>.LoadedCount();i++){var prefab=PrefabCollection<BuildingInfo>.GetLoaded(i);if(prefab!=null && prefab.m_buildingAI is PoliceStationAI && prefab.m_placementMode==BuildingInfo.PlacementMode.Roadside){if(roadside==null || prefab.m_cellLength<PrefabCollection<BuildingInfo>.FindLoaded(roadside).m_cellLength)roadside=prefab.name;}} if(roadside==null)throw new Exception("No roadside police building prefab loaded."); PlaceBuilding(roadside,new Vector3(9800,0,10272));
        Entry.Log("PASS: real road/rail control points remain on added ground; native NetTool construction builds segments; BuildingTool simulation and placement build ground and roadside buildings in added tiles.");
    }
    public static ushort BuildNetwork(string name,Vector3 position)
    {
        var info=PrefabCollection<NetInfo>.FindLoaded(name);NetTool.ControlPoint a,b;
        position.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(position);
        if(!NetTool.MakeControlPoint(new Ray(position+Vector3.up*500,Vector3.down),1000,info,false,NetNode.Flags.All,NetSegment.Flags.All,Building.Flags.All,0,false,out a))throw new Exception("Network start ray failed.");
        position.x+=256;position.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(position);
        if(!NetTool.MakeControlPoint(new Ray(position+Vector3.up*500,Vector3.down),1000,info,false,NetNode.Flags.All,NetSegment.Flags.All,Building.Flags.All,0,false,out b))throw new Exception("Network end ray failed.");
        a.m_direction=Vector3.right;b.m_direction=Vector3.right;var middle=a;middle.m_position=(a.m_position+b.m_position)*.5f;
        ushort node,segment;int cost,production;
        var errors=NetTool.CreateNode(info,a,middle,b,NetTool.m_nodePositionsSimulation,1000,true,false,true,false,false,false,0,out node,out segment,out cost,out production);
        if(errors!=ToolBase.ToolErrors.None)throw new Exception("Network preview rejected "+name+": "+errors);
        errors=NetTool.CreateNode(info,a,middle,b,NetTool.m_nodePositionsSimulation,1000,false,false,true,false,false,false,0,out node,out segment,out cost,out production);
        if(errors!=ToolBase.ToolErrors.None || segment==0)throw new Exception("Network construction failed "+name+": "+errors);
        var data=Singleton<NetManager>.instance.m_segments.m_buffer[segment];
        if(data.Info!=info || Singleton<NetManager>.instance.m_nodes.m_buffer[data.m_startNode].m_position.x<9000)throw new Exception("Network created at wrong position.");
        Entry.Log("PASS: native NetTool preview and construction for "+name+" segment "+segment+" in added tiles.");return segment;
    }
    public static ushort PlaceBuilding(string name,Vector3 position)
    {
        building.m_prefab=PrefabCollection<BuildingInfo>.FindLoaded(name);if(building.m_prefab==null)throw new Exception("Building prefab missing: "+name);
        position.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(position);
        Set("m_mouseRay",new Ray(position+Vector3.up*500,Vector3.down));Set("m_mouseRayLength",1000f);Set("m_mouseRayValid",true);building.SimulationStep();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var errors=(ToolBase.ToolErrors)typeof(BuildingTool).GetField("m_placementErrors",flags).GetValue(building);
        if(errors!=ToolBase.ToolErrors.None)throw new Exception("Building preview rejected "+name+": "+errors);
        uint before=Singleton<SimulationManager>.instance.m_currentBuildIndex;
        var create=typeof(BuildingTool).GetMethod("CreateBuilding",flags,null,Type.EmptyTypes,null);
        var coroutine=(System.Collections.IEnumerator)create.Invoke(building,null);while(coroutine.MoveNext()){}
        ushort found=0;var buildings=Singleton<BuildingManager>.instance.m_buildings.m_buffer;
        for(int i=1;i<buildings.Length;i++)if(buildings[i].m_buildIndex>=before && buildings[i].m_flags!=Building.Flags.None && buildings[i].Info==building.m_prefab && (buildings[i].m_position-position).sqrMagnitude<4096){found=(ushort)i;break;}
        if(found==0)throw new Exception("Native BuildingTool coroutine did not construct "+name);
        Entry.Log("PASS: native BuildingTool preview and construction for "+name+" in added tiles.");return found;
    }
}




