using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

static class LiveTransitVehicleChecks
{
    public static bool Enabled {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_TRANSIT_VEHICLE")=="1";}}
    const string Owner="HundredTiles.Isolated.TransitVehicle";
    static ushort Line,Bus,Depot;
    static readonly List<ushort> Arrivals=new List<ushort>();
    static readonly object Gate=new object();
    public static void Prepare(ushort line,TransportInfo transport)
    {
        if(!File.Exists(Path.Combine(Entry.Root,"isolated-test.marker")))throw new Exception("Transit operation requires the isolated fixture.");
        Line=line;BuildingInfo prefab=null;
        for(uint i=0;i<PrefabCollection<BuildingInfo>.LoadedCount();i++)
        {
            var candidate=PrefabCollection<BuildingInfo>.GetLoaded(i);var ai=candidate==null?null:candidate.m_buildingAI as DepotAI;
            if(ai!=null && ai.m_transportInfo==transport && candidate.m_class.m_level==ItemClass.Level.Level1){prefab=candidate;if(candidate.name=="Bus Depot")break;}
        }
        if(prefab==null)throw new Exception("Standard native city bus depot prefab missing.");
        var economy=Singleton<EconomyManager>.instance;economy.AddResource(EconomyManager.Resource.RewardAmount,100000000,prefab.m_class);
        foreach(bool night in new[]{false,true})economy.SetBudget(transport.m_class.m_service,transport.m_class.m_subService,100,night);
        Singleton<TransportManager>.instance.m_lines.m_buffer[line].SetActive(true,true);
        Singleton<TransportManager>.instance.m_lines.m_buffer[line].m_budget=100;
        LiveConstructionChecks.Prepare();
        Depot=LiveConstructionChecks.PlaceBuilding(prefab.name,new Vector3(23300,0,24500+prefab.m_cellLength*4+8));
        var buildings=Singleton<BuildingManager>.instance.m_buildings.m_buffer;
        var frame=buildings[Depot].GetLastFrameData();frame.m_constructState=255;
        buildings[Depot].SetFrameData(Singleton<SimulationManager>.instance.m_currentFrameIndex,frame);prefab.m_buildingAI.SimulationStep(Depot,ref buildings[Depot]);
        if((buildings[Depot].m_flags&Building.Flags.Completed)==0)throw new Exception("Native far bus depot completion failed.");
        lock(Gate)Arrivals.Clear();
        new Harmony(Owner).Patch(typeof(BusAI).GetMethod("ArriveAtTarget",BindingFlags.Instance|BindingFlags.NonPublic),prefix:new HarmonyMethod(typeof(LiveTransitVehicleChecks).GetMethod("Arriving")));
        prefab.m_buildingAI.StartTransfer(Depot,ref buildings[Depot],transport.m_vehicleReason,new TransferManager.TransferOffer{TransportLine=line,Amount=1,Active=false});
        var vehicles=Singleton<VehicleManager>.instance.m_vehicles.m_buffer;Bus=0;
        for(int i=1;i<vehicles.Length;i++)if((vehicles[i].m_flags&Vehicle.Flags.Created)!=0 && vehicles[i].m_transportLine==line && vehicles[i].m_sourceBuilding==Depot){Bus=(ushort)i;break;}
        if(Bus==0)throw new Exception("Native depot dispatch did not create the requested line bus.");
        Entry.Log("Prepared actual far bus "+Bus+" from native depot "+Depot+" on line "+Line+" using DepotAI.StartTransfer. Demand-independent fixture request; no vehicle position/path/frame was assigned.");
    }
    public static void Arriving(ushort vehicleID,ref Vehicle data)
    {
        if(vehicleID!=Bus || data.m_transportLine!=Line || data.m_targetBuilding==0 || (data.m_flags&Vehicle.Flags.DummyTraffic)!=0)return;
        ushort stop=data.m_targetBuilding;var point=data.GetLastFramePosition();var target=Singleton<NetManager>.instance.m_nodes.m_buffer[stop].m_position;
        point.y=target.y=0;if((point-target).sqrMagnitude>1024)return;
        lock(Gate)if(Arrivals.Count==0 || Arrivals[Arrivals.Count-1]!=stop){Arrivals.Add(stop);Entry.Log("Native far bus actual stop arrival: vehicle="+vehicleID+", stop="+stop+", position="+data.GetLastFramePosition()+", arrivals="+Arrivals.Count);}
    }
    public static IEnumerator Run()
    {
        if(Bus==0 || Line==0)throw new Exception("Actual far bus operation was not prepared.");
        var simulation=Singleton<SimulationManager>.instance;LiveConsumerChecks.PrepareUnattendedProgression();ToolsModifierControl.SetTool<DefaultTool>();
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();if(camera!=null){camera.ClearTarget();camera.m_targetPosition=camera.m_currentPosition=new Vector3(23300,80,24800);camera.m_targetSize=camera.m_currentSize=700;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,70);camera.m_targetHeight=camera.m_currentHeight=80;}
        simulation.SelectedSimulationSpeed=1;simulation.SimulationPaused=false;simulation.ForcedSimulationPaused=false;
        float began=Time.realtimeSinceStartup,next=began+30;Vector3 previous=Singleton<VehicleManager>.instance.m_vehicles.m_buffer[Bus].GetLastFramePosition();float distance=0;bool spawned=false,complete=false;
        try
        {
            while(Time.realtimeSinceStartup-began<300)
            {
                var data=Singleton<VehicleManager>.instance.m_vehicles.m_buffer[Bus];
                if((data.m_flags&Vehicle.Flags.Spawned)!=0){spawned=true;var point=data.GetLastFramePosition();distance+=Vector3.Distance(previous,point);previous=point;}
                lock(Gate)complete=Arrivals.Count>=3 && Arrivals[0]==Arrivals[2] && Arrivals[0]!=Arrivals[1];
                if(complete && spawned && distance>2000)break;
                if(Time.realtimeSinceStartup>=next){Entry.Log("Native far bus operation: elapsed="+(Time.realtimeSinceStartup-began)+", vehicle="+Bus+", flags="+data.m_flags+", line="+data.m_transportLine+", target="+data.m_targetBuilding+", path="+data.m_path+", position="+data.GetLastFramePosition()+", distance="+distance);next+=30;}
                yield return null;
            }
            simulation.ForcedSimulationPaused=true;
            if(!complete || !spawned || distance<=2000)
            {Entry.Log("FAILURE: native far bus did not complete a full closed route within 300 seconds; spawned="+spawned+", distance="+distance);Bootstrap.FatalFailure=true;}
            else Entry.Log("PASS: actual native bus "+Bus+" spawned from its depot, drove "+distance+" metres under normal simulation and arrived at both far stops then returned to its first stop. No manual vehicle movement or simulation steps. Passenger boarding/alighting is not asserted.");
            yield return new WaitForEndOfFrame();LiveRunDiagnostics.Screenshot("trial324-bus-vehicle.png");
        }
        finally{new Harmony(Owner).UnpatchAll(Owner);Application.Quit();}
    }
}
