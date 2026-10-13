using System;
using System.Reflection;
using HarmonyLib;
using ColossalFramework;
using UnityEngine;

public static class LiveStartupChecks
{
    public static void Run()
    {
        int routes=0;NetInfo train=null,bus=null,metro=null;
        for(uint i=0;i<PrefabCollection<NetInfo>.LoadedCount();i++)
        {
            var info=PrefabCollection<NetInfo>.GetLoaded(i);
            if(info==null || info.m_lanes==null)continue;
            foreach(var lane in info.m_lanes)
            {
                if(lane==null || (lane.m_laneType & NetInfo.LaneType.PublicTransport)==0)continue;
                var line=info.m_netAI as TransportLineAI;
                Entry.Log("TRANSIT ROUTE: "+info.name+" type="+lane.m_vehicleType+" category="+lane.vehicleCategory+" routeType="+(line==null?"unknown":line.m_vehicleType.ToString()));
                if(line!=null){if(line.m_vehicleType==VehicleInfo.VehicleType.Train)train=info;else if(line.m_vehicleType==VehicleInfo.VehicleType.Metro)metro=info;else if(line.m_vehicleType==VehicleInfo.VehicleType.Car)bus=info;}
                routes++;
            }
        }
        if(routes==0)throw new Exception("No public transport route lanes loaded in the native fixture.");
        var filter=Find("AlwaysTrainCommute.Core.RailRouteFilter");
        if(!(bool)filter.GetProperty("Available").GetValue(null,null))throw new Exception("Rail route filter is not installed on the actual path finder.");
        if(train==null || bus==null || metro==null)throw new Exception("Required native transport route prefabs missing.");
        var manager=Singleton<NetManager>.instance;
        ushort free=0;for(int i=1;i<manager.m_segments.m_buffer.Length;i++)if(manager.m_segments.m_buffer[i].m_flags==0){free=(ushort)i;break;}
        if(free==0)throw new Exception("No isolated segment available for route filter validation.");
        var saved=manager.m_segments.m_buffer[free];
        try
        {
            PathUnit data=new PathUnit();data.m_laneTypes=(byte)(NetInfo.LaneType.Pedestrian|NetInfo.LaneType.PublicTransport);data.m_vehicleCategories=(long)VehicleInfo.VehicleCategory.PassengerTrain;
            filter.GetMethod("Begin").Invoke(null,new object[]{data});
            foreach(var info in new[]{train,bus,metro})
            {
                manager.m_segments.m_buffer[free].m_infoIndex=(ushort)info.m_prefabDataIndex;
                bool allowed=(bool)filter.GetMethod("Allow").Invoke(null,new object[]{free});
                if(allowed!=(info==train))throw new Exception("Rail route filter classification failed: "+info.name);
            }
            filter.GetMethod("End").Invoke(null,null);
            if(!(bool)filter.GetMethod("Allow").Invoke(null,new object[]{free}))throw new Exception("Rail filter changed an unrestricted path.");
        }
        finally{filter.GetMethod("End").Invoke(null,null);manager.m_segments.m_buffer[free]=saved;}
        Entry.Log("PASS: actual native train/bus/metro route prefabs classify correctly; unrestricted paths unaffected; temporary segment restored.");
        var helper=Find("HundredTiles.StartupRepairs.RecoverableTypeScan");
        var scan=helper.GetMethod("GetTypes");
        var serialization=Assembly.Load("System.Runtime.Serialization, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
        int expected=0;bool partialFailure=false;
        try{expected=serialization.GetTypes().Length;}
        catch(ReflectionTypeLoadException error){partialFailure=true;foreach(var type in error.Types)if(type!=null)expected++;}
        Type[] partial=(Type[])scan.Invoke(null,new object[]{serialization});
        if(partial.Length!=expected)throw new Exception("TFW recoverable scan lost loadable types.");
        foreach(var type in partial)if(type==null)throw new Exception("TFW recoverable scan retained a null type.");
        Type[] normal=(Type[])scan.Invoke(null,new object[]{typeof(LiveStartupChecks).Assembly});
        if(normal.Length==0)throw new Exception("TFW normal type scan failed.");
        Entry.Log("PASS: embedded TFW helper executes on the native Mono runtime; partial load failure reproduced="+partialFailure+"; retained types="+expected+"; normal enumeration preserved.");
        var optional=Find("HundredTiles.StartupRepairs.OptionalBuildingThemes");
        if((bool)optional.GetMethod("IsEnabled").Invoke(null,null))throw new Exception("No-theme fixture unexpectedly enabled Building Themes integration.");
        var patcher=Find("ABLC.Patcher");patcher.GetMethod("PatchBuildingThemes").Invoke(Activator.CreateInstance(patcher),null);
        Entry.Log("PASS: ABLC optional integration exits cleanly when Building Themes is not enabled.");
        foreach(string name in new[]{"2093619702.New Asset_Data","3797340534.Niigata Office_Data"})
        {
            var info=PrefabCollection<BuildingInfo>.FindLoaded(name);
            if(info==null || !info.m_prefabInitialized)throw new Exception("Repaired asset prefab unavailable: "+name);
            Entry.Log("ASSET DIAGNOSTIC: "+name+" lodMesh="+(info.m_lodMesh!=null)+" lodACI="+(info.m_lodMaterial!=null && info.m_lodMaterial.GetTexture("_ACIMap")!=null));
            if(info.m_subBuildings!=null)foreach(var sub in info.m_subBuildings)
            {
                if(sub==null || sub.m_buildingInfo==null || !sub.m_buildingInfo.m_prefabInitialized)throw new Exception("Referenced embedded sub-building unavailable: "+name);
                Entry.Log("ASSET SUB: "+sub.m_buildingInfo.name);
            }
            if(name=="3797340534.Niigata Office_Data" && (info.m_lodMesh!=null || info.m_mesh==null || info.m_minLodDistance<100000f))throw new Exception("Reviewed LOD fallback did not retain the full-detail model at distance.");
        }
        Entry.Log("PASS: apartment embedded building initialized without replacement; reviewed missing-ACI office retains its original full-detail model at distance.");
        foreach(var target in Harmony.GetAllPatchedMethods())
        {
            if(target.DeclaringType!=typeof(CameraController) && target.DeclaringType.FullName!="TrafficManager.Custom.PathFinding.CustomPathFind")continue;
            var patches=Harmony.GetPatchInfo(target);
            foreach(var owner in patches.Owners)
                if(owner.IndexOf("camera",StringComparison.OrdinalIgnoreCase)>=0 || owner.IndexOf("ridership",StringComparison.OrdinalIgnoreCase)>=0)
                    Entry.Log("STARTUP PATCH: "+target.DeclaringType.FullName+"."+target.Name+" owner="+owner);
        }
        Entry.Log("PASS: native startup diagnostic scan completed, transport route lanes="+routes);
    }
    static Type Find(string name){foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){var type=assembly.GetType(name);if(type!=null)return type;}throw new Exception("Fixture type unavailable: "+name);}
}
