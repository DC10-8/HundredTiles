using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

static class LiveFarOutsideChecks
{
    public static bool Enabled {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_FAR_OUTSIDE")=="1";}}
    static readonly ushort[] Sources=new ushort[2],Targets=new ushort[2];
    static readonly HashSet<ushort> Seen=new HashSet<ushort>();
    static readonly bool[] Spawned=new bool[2],Arrived=new bool[2];
    const string Owner="HundredTiles.Isolated.FarOutside";
    public static bool Failed;
    public static void Prepare()
    {
        if(HundredTiles.Core.ExpandedMap.TileCount!=324 || !System.IO.File.Exists(System.IO.Path.Combine(Entry.Root,"isolated-test.marker")))throw new Exception("Far outside fixture requires isolated 324 runtime.");
        var buildings=Singleton<BuildingManager>.instance;
        var old=buildings.GetOutsideConnections();var oldIds=new ushort[old.m_size];Array.Copy(old.m_buffer,oldIds,oldIds.Length);
        // Free the stock four-connection slots only in this disposable session.
        // The input city is never saved or changed on disk.
        foreach(ushort id in oldIds)buildings.RemoveOutsideConnection(id);
        LiveConstructionChecks.Prepare();
        for(int kind=0;kind<2;kind++)
        {
            float z=kind==0?17000:20000;
            LiveTerrainToolChecks.Prepare();LiveTerrainToolChecks.LevelArea(new Vector3(25700,400,z+1000),2400,400);
            string name=kind==0?"Basic Road":"Train Track";
            Sources[kind]=Connect(name,new Vector3(25600,400,z),new Vector3(25920,400,z));
            Targets[kind]=Connect(name,new Vector3(25600,400,z+1800),new Vector3(25920,400,z+1800));
            Connect(name,new Vector3(25600,400,z),new Vector3(25600,400,z+1800),false);
        }
        var graph=Singleton<NetManager>.instance;
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(graph,new object[]{0});
        for(int i=0;i<2;i++)
        {
            if(Sources[i]==0 || Targets[i]==0)throw new Exception("Native far outside node did not create its building.");
            foreach(ushort id in new[]{Sources[i],Targets[i]})
            {
                var data=buildings.m_buildings.m_buffer[id];
                if(!(data.Info.m_buildingAI is OutsideConnectionAI) || (data.m_flags&(Building.Flags.Incoming|Building.Flags.Outgoing))!=(Building.Flags.Incoming|Building.Flags.Outgoing))throw new Exception("Native outside direction flags missing at "+id+": "+data.m_flags);
                Entry.Log("New outside parent node="+data.FindParentNode(id)+", ground="+Singleton<TerrainManager>.instance.SampleRawHeightSmooth(data.m_position)+", ground/water="+Singleton<TerrainManager>.instance.SampleRawHeightSmoothWithWater(data.m_position,true,0));
            }
            var offer=new TransferManager.TransferOffer{Building=Targets[i],Position=buildings.m_buildings.m_buffer[Targets[i]].m_position,Amount=1,Active=false};
            int accepted=0;for(int wave=0;wave<4;wave++)if(OutsideConnectionAI.StartConnectionTransfer(Sources[i],ref buildings.m_buildings.m_buffer[Sources[i]],i==0?TransferManager.TransferReason.DummyCar:TransferManager.TransferReason.DummyTrain,offer,0,0,0))accepted++;
            if(accepted==0)throw new Exception("Native new outside transfer requests all failed.");
        }
        new Harmony(Owner).Patch(typeof(VehicleManager).GetMethod("ReleaseVehicle",new[]{typeof(ushort)}),prefix:new HarmonyMethod(typeof(LiveFarOutsideChecks).GetMethod("Releasing")));
        foreach(var type in new[]{typeof(PassengerCarAI),typeof(PassengerTrainAI)})new Harmony(Owner).Patch(type.GetMethod("ArriveAtDestination"),prefix:new HarmonyMethod(typeof(LiveFarOutsideChecks).GetMethod("Arriving")));
        Entry.Log("Prepared newly constructed far outside road/rail connections at x=25920 through native NetTool previews/construction and native node buildings. Original outside registrations removed only in this unsaved fixture session.");
    }
    static ushort Connect(string name,Vector3 start,Vector3 end,bool outside=true)
    {
        var info=PrefabCollection<NetInfo>.FindLoaded(name);NetTool.ControlPoint a,b;
        var endRay=end;if(outside)endRay.x-=4;
        if(!NetTool.MakeControlPoint(new Ray(start+Vector3.up*500,Vector3.down),1000,info,false,NetNode.Flags.All,NetSegment.Flags.All,Building.Flags.All,0,false,out a) || !NetTool.MakeControlPoint(new Ray(endRay+Vector3.up*500,Vector3.down),1000,info,false,NetNode.Flags.All,NetSegment.Flags.All,Building.Flags.All,0,false,out b))throw new Exception("Far outside control point ray failed.");
        a.m_position=start;b.m_position=end;a.m_node=ExistingNode(start);b.m_node=ExistingNode(end);b.m_outside=outside;var direction=(end-start).normalized;a.m_direction=b.m_direction=direction;var middle=a;middle.m_node=0;middle.m_position=(start+end)*.5f;
        ushort node,segment;int cost,production;
        var error=NetTool.CreateNode(info,a,middle,b,NetTool.m_nodePositionsSimulation,1000,true,false,true,false,false,false,0,out node,out segment,out cost,out production);
        if(error!=ToolBase.ToolErrors.None)throw new Exception("Far outside native preview rejected "+name+": "+error);
        error=NetTool.CreateNode(info,a,middle,b,NetTool.m_nodePositionsSimulation,1000,false,false,true,false,false,false,0,out node,out segment,out cost,out production);
        if(error!=ToolBase.ToolErrors.None || node==0)throw new Exception("Far outside native construction rejected "+name+": "+error);
        var graph=Singleton<NetManager>.instance;
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(graph,new object[]{0});
        ushort building=graph.m_nodes.m_buffer[node].m_building;
        Entry.Log("Far outside constructed "+name+": node="+node+", position="+graph.m_nodes.m_buffer[node].m_position+", flags="+graph.m_nodes.m_buffer[node].m_flags+", building="+building);
        return building;
    }
    static ushort ExistingNode(Vector3 point)
    {
        var nodes=Singleton<NetManager>.instance.m_nodes.m_buffer;
        for(int id=1;id<nodes.Length;id++)if((nodes[id].m_flags&NetNode.Flags.Created)!=0){var delta=nodes[id].m_position-point;delta.y=0;if(delta.sqrMagnitude<.01f)return (ushort)id;}
        return 0;
    }
    static int Kind(ushort id,ref Vehicle data)
    {
        if(data.Info==null)return -1;
        var point=data.GetLastFramePosition();if(point.x<25000 || point.z<16000 || point.z>23000)return -1;
        for(int i=0;i<2;i++)if(data.m_sourceBuilding==Sources[i] && data.m_targetBuilding==Targets[i])return i;
        uint owner=data.Info.m_vehicleAI.GetOwnerID(id,ref data).Citizen;
        if(owner!=0){var citizens=Singleton<CitizenManager>.instance;ushort instance=citizens.m_citizens.m_buffer[owner].m_instance;if(instance!=0){var person=citizens.m_instances.m_buffer[instance];if(person.m_sourceBuilding==Sources[0] && person.m_targetBuilding==Targets[0])return 0;}}
        return -1;
    }
    public static void Arriving(ushort vehicleID,ref Vehicle vehicleData)
    {
        int kind=Kind(vehicleID,ref vehicleData);if(kind<0 || (vehicleData.m_flags&Vehicle.Flags.Spawned)==0)return;
        Seen.Add(vehicleID);Spawned[kind]=true;
        var target=Singleton<BuildingManager>.instance.m_buildings.m_buffer[Targets[kind]].m_position;var point=vehicleData.GetLastFramePosition();point.y=target.y=0;
        if((point-target).sqrMagnitude<128*128){Arrived[kind]=true;Entry.Log("Native new far outside arrival callback: kind="+kind+", vehicle="+vehicleID+", position="+point);}
    }
    public static void Releasing(ushort vehicle)
    {
        var data=Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicle];int kind=Kind(vehicle,ref data);if(kind<0 || !Seen.Contains(vehicle))return;
        var target=Singleton<BuildingManager>.instance.m_buildings.m_buffer[Targets[kind]].m_position;
        var point=data.GetLastFramePosition();point.y=target.y=0;
        if((point-target).sqrMagnitude<128*128)Entry.Log("New far outside vehicle released near destination boundary: kind="+kind+", vehicle="+vehicle+", position="+point);
    }
    public static IEnumerator Run()
    {
        var sim=Singleton<SimulationManager>.instance;sim.SelectedSimulationSpeed=3;sim.SimulationPaused=false;sim.ForcedSimulationPaused=false;
        int requested=0;Exception requestError=null;
        sim.AddAction(delegate{try{var buildings=Singleton<BuildingManager>.instance;for(int i=0;i<2;i++){var offer=new TransferManager.TransferOffer{Building=Targets[i],Position=buildings.m_buildings.m_buffer[Targets[i]].m_position,Amount=1,Active=false};for(int wave=0;wave<4;wave++)OutsideConnectionAI.StartConnectionTransfer(Sources[i],ref buildings.m_buildings.m_buffer[Sources[i]],i==0?TransferManager.TransferReason.DummyCar:TransferManager.TransferReason.DummyTrain,offer,0,0,0);}}catch(Exception error){requestError=error;}finally{System.Threading.Interlocked.Exchange(ref requested,1);}});
        while(System.Threading.Thread.VolatileRead(ref requested)==0)yield return null;
        if(requestError!=null)throw new Exception("Native outside transfer wave failed.",requestError);
        float began=Time.realtimeSinceStartup,next=began+30;
        while(Time.realtimeSinceStartup-began<300 && !(Arrived[0] && Arrived[1]))
        {
            var vehicles=Singleton<VehicleManager>.instance.m_vehicles.m_buffer;
            for(int id=1;id<vehicles.Length;id++)if((vehicles[id].m_flags&Vehicle.Flags.Spawned)!=0){var data=vehicles[id];int kind=Kind((ushort)id,ref data);if(kind>=0){Seen.Add((ushort)id);Spawned[kind]=true;}}
                if(Time.realtimeSinceStartup>=next){Entry.Log("New far outside operation: car spawned="+Spawned[0]+", train spawned="+Spawned[1]+", destinations reached="+Arrived[0]+"/"+Arrived[1]);int logged=0;for(int id=1;id<vehicles.Length && logged<8;id++){var data=vehicles[id];if((data.m_flags&Vehicle.Flags.Created)==0 || (data.m_sourceBuilding!=Sources[0] && data.m_sourceBuilding!=Sources[1]))continue;logged++;Entry.Log("New outside vehicle state "+id+": flags="+data.m_flags+", path="+data.m_path+", source="+data.m_sourceBuilding+", target="+data.m_targetBuilding+", position="+data.GetLastFramePosition()+", path flags="+(data.m_path==0?0:Singleton<PathManager>.instance.m_pathUnits.m_buffer[data.m_path].m_pathFindFlags));}next+=30;}
            yield return null;
        }
        sim.ForcedSimulationPaused=true;
        if(!Spawned[0] || !Spawned[1] || !Arrived[0] || !Arrived[1]){Entry.Log("FAILURE: new far outside road/rail journeys were not both observed.");Failed=true;}
        else Entry.Log("PASS: newly constructed far outside road and rail connections spawned native vehicles and reached the destination boundary before native release. No manually assigned vehicle position/path.");
        new Harmony(Owner).UnpatchAll(Owner);
    }
}
