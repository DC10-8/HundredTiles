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
    static readonly HashSet<uint> Riders=new HashSet<uint>(),Boarded=new HashSet<uint>(),Alighted=new HashSet<uint>();
    static readonly Dictionary<uint,ushort> RiderBuses=new Dictionary<uint,ushort>();
    static readonly Dictionary<uint,ushort> RiderInstances=new Dictionary<uint,ushort>();
    static readonly Dictionary<uint,Vector3> BoardingPositions=new Dictionary<uint,Vector3>();
    static bool PassengerTest {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_PASSENGERS")=="1";}}
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
        Depot=LiveConstructionChecks.PlaceBuilding(prefab.name,new Vector3(23300,0,24500-prefab.m_cellLength*4-8));
        var buildings=Singleton<BuildingManager>.instance.m_buildings.m_buffer;
        var frame=buildings[Depot].GetLastFrameData();frame.m_constructState=255;
        buildings[Depot].SetFrameData(Singleton<SimulationManager>.instance.m_currentFrameIndex,frame);prefab.m_buildingAI.SimulationStep(Depot,ref buildings[Depot]);
        if((buildings[Depot].m_flags&Building.Flags.Completed)==0)throw new Exception("Native far bus depot completion failed.");
        lock(Gate)Arrivals.Clear();
        new Harmony(Owner).Patch(typeof(BusAI).GetMethod("ArriveAtTarget",BindingFlags.Instance|BindingFlags.NonPublic),prefix:new HarmonyMethod(typeof(LiveTransitVehicleChecks).GetMethod("Arriving")));
        if(PassengerTest)
        {
            new Harmony(Owner).Patch(typeof(HumanAI).GetMethod("SetCurrentVehicle"),prefix:new HarmonyMethod(typeof(LiveTransitVehicleChecks).GetMethod("BeforeVehicle")),postfix:new HarmonyMethod(typeof(LiveTransitVehicleChecks).GetMethod("AfterVehicle")));
            new Harmony(Owner).Patch(typeof(TouristAI).GetMethod("StartPathFind",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(ushort),typeof(CitizenInstance).MakeByRefType()},null),postfix:new HarmonyMethod(typeof(LiveTransitVehicleChecks).GetMethod("RouteRequested")));
            foreach(string method in new[]{"PathfindSuccess","PathfindFailure"})new Harmony(Owner).Patch(typeof(HumanAI).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic),prefix:new HarmonyMethod(typeof(LiveTransitVehicleChecks).GetMethod("PathCompleted")));
            ushort destination=LiveConstructionChecks.PlaceBuilding(prefab.name,new Vector3(23450,0,25100+prefab.m_cellLength*4+8));
            frame=buildings[destination].GetLastFrameData();frame.m_constructState=255;buildings[destination].SetFrameData(Singleton<SimulationManager>.instance.m_currentFrameIndex,frame);prefab.m_buildingAI.SimulationStep(destination,ref buildings[destination]);
            CitizenInfo person=null;for(uint i=0;i<PrefabCollection<CitizenInfo>.LoadedCount();i++){var candidate=PrefabCollection<CitizenInfo>.GetLoaded(i);if(candidate!=null && candidate.m_citizenAI is TouristAI){person=candidate;break;}}
            if(person==null)throw new Exception("Native tourist pedestrian prefab missing.");
            var citizens=Singleton<CitizenManager>.instance;var random=new ColossalFramework.Math.Randomizer(324220);
            lock(Gate){Riders.Clear();Boarded.Clear();Alighted.Clear();RiderBuses.Clear();RiderInstances.Clear();BoardingPositions.Clear();}
            for(int i=0;i<48;i++)
            {
                uint citizen;if(!citizens.CreateCitizen(out citizen,100,100,ref random))throw new Exception("Passenger citizen allocation failed.");
                citizens.m_citizens.m_buffer[citizen].m_flags|=Citizen.Flags.Tourist;
                lock(Gate)Riders.Add(citizen);
                if(!((HumanAI)person.m_citizenAI).StartMoving(citizen,ref citizens.m_citizens.m_buffer[citizen],Depot,destination))throw new Exception("Native passenger trip request failed.");
                citizens.m_citizens.m_buffer[citizen].SetVisitplace(citizen,destination,0u);
                lock(Gate)RiderInstances[citizen]=citizens.m_citizens.m_buffer[citizen].m_instance;
                if(i<3)Entry.Log("Native passenger created: citizen="+citizen+", instance="+citizens.m_citizens.m_buffer[citizen].m_instance+", citizen flags="+citizens.m_citizens.m_buffer[citizen].m_flags+", prefab="+person.name);
            }
            Entry.Log("Prepared 48 native tourist trip requests between far depot buildings. Native pedestrians choose walking/transport; no passenger vehicle or path assignment is injected.");
        }
        prefab.m_buildingAI.StartTransfer(Depot,ref buildings[Depot],transport.m_vehicleReason,new TransferManager.TransferOffer{TransportLine=line,Amount=1,Active=false});
        var vehicles=Singleton<VehicleManager>.instance.m_vehicles.m_buffer;Bus=0;
        for(int i=1;i<vehicles.Length;i++)if((vehicles[i].m_flags&Vehicle.Flags.Created)!=0 && vehicles[i].m_transportLine==line && vehicles[i].m_sourceBuilding==Depot){Bus=(ushort)i;break;}
        if(Bus==0)throw new Exception("Native depot dispatch did not create the requested line bus.");
        Entry.Log("Prepared actual far bus "+Bus+" from native depot "+Depot+" on line "+Line+" using DepotAI.StartTransfer. Demand-independent fixture request; no vehicle position/path/frame was assigned.");
    }
    public static void PathCompleted(MethodBase __originalMethod,ref CitizenInstance data)
    {lock(Gate)if(Riders.Contains(data.m_citizen))Entry.Log("Native passenger "+__originalMethod.Name+": citizen="+data.m_citizen+", target="+data.m_targetBuilding+", path="+data.m_path+", flags="+data.m_flags);}
    public static void RouteRequested(ushort instanceID,ref CitizenInstance citizenData,bool __result)
    {
        lock(Gate)if(Riders.Contains(citizenData.m_citizen))Entry.Log("Native passenger path request: instance="+instanceID+", citizen="+citizenData.m_citizen+", accepted="+__result+", source position="+citizenData.GetLastFramePosition()+", start target="+citizenData.m_targetPos+", target building="+citizenData.m_targetBuilding+", path="+citizenData.m_path+", flags="+citizenData.m_flags);
    }
    public static void Arriving(ushort vehicleID,ref Vehicle data)
    {
        if(vehicleID!=Bus || data.m_transportLine!=Line || data.m_targetBuilding==0 || (data.m_flags&Vehicle.Flags.DummyTraffic)!=0)return;
        ushort stop=data.m_targetBuilding;var point=data.GetLastFramePosition();var target=Singleton<NetManager>.instance.m_nodes.m_buffer[stop].m_position;
        point.y=target.y=0;if((point-target).sqrMagnitude>1024)return;
        lock(Gate)if(Arrivals.Count==0 || Arrivals[Arrivals.Count-1]!=stop){Arrivals.Add(stop);Entry.Log("Native far bus actual stop arrival: vehicle="+vehicleID+", stop="+stop+", position="+data.GetLastFramePosition()+", arrivals="+Arrivals.Count);}
    }
    public static void BeforeVehicle(ref CitizenInstance citizenData,out ushort __state)
    {__state=citizenData.m_citizen==0?(ushort)0:Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_vehicle;}
    public static void AfterVehicle(ushort instanceID,ref CitizenInstance citizenData,ushort vehicleID,Vector3 position,ushort __state,bool __result)
    {
        if(!__result)return;uint citizen=citizenData.m_citizen;
        lock(Gate)
        {
            ushort expected;if(!RiderInstances.TryGetValue(citizen,out expected) || expected!=instanceID)return;
            if(vehicleID!=0 && Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].m_transportLine==Line && Boarded.Add(citizen)){RiderBuses[citizen]=vehicleID;BoardingPositions[citizen]=position;Entry.Log("Native passenger boarded test line bus: citizen="+citizen+", instance="+instanceID+", bus="+vehicleID+", position="+position);}
            ushort ridden;Vector3 boarding;
            if(vehicleID==0 && RiderBuses.TryGetValue(citizen,out ridden) && __state==ridden && BoardingPositions.TryGetValue(citizen,out boarding))
            {var distance=position-boarding;distance.y=0;if(distance.sqrMagnitude>200*200 && Alighted.Add(citizen))Entry.Log("Native passenger alighted same test line bus at another stop: citizen="+citizen+", instance="+instanceID+", bus="+ridden+", position="+position);}
        }
    }
    public static IEnumerator Run()
    {
        if(Bus==0 || Line==0)throw new Exception("Actual far bus operation was not prepared.");
        var simulation=Singleton<SimulationManager>.instance;LiveConsumerChecks.PrepareUnattendedProgression();ToolsModifierControl.SetTool<DefaultTool>();
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();if(camera!=null){camera.ClearTarget();camera.m_targetPosition=camera.m_currentPosition=new Vector3(23300,80,24800);camera.m_targetSize=camera.m_currentSize=700;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,70);camera.m_targetHeight=camera.m_currentHeight=80;}
        simulation.SelectedSimulationSpeed=PassengerTest?3:1;simulation.SimulationPaused=false;simulation.ForcedSimulationPaused=false;
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_LOD_ONLY")=="1")
        {
            try{float warmup=Time.realtimeSinceStartup+5;while(Time.realtimeSinceStartup<warmup)yield return null;yield return LiveLodVisualChecks.Run();}
            finally{new Harmony(Owner).UnpatchAll(Owner);Application.Quit();}
            yield break;
        }
        float began=Time.realtimeSinceStartup,next=began+30;Vector3 previous=Singleton<VehicleManager>.instance.m_vehicles.m_buffer[Bus].GetLastFramePosition();float distance=0;bool spawned=false,complete=false;
        bool failed=false;
        try
        {
            while(Time.realtimeSinceStartup-began<(PassengerTest?480:300))
            {
                var data=Singleton<VehicleManager>.instance.m_vehicles.m_buffer[Bus];
                if((data.m_flags&Vehicle.Flags.Spawned)!=0){spawned=true;var point=data.GetLastFramePosition();distance+=Vector3.Distance(previous,point);previous=point;}
                lock(Gate)complete=Arrivals.Count>=3 && Arrivals[0]==Arrivals[2] && Arrivals[0]!=Arrivals[1];
                bool ridersComplete;lock(Gate)ridersComplete=!PassengerTest || Alighted.Count>0;
                if(complete && spawned && distance>2000 && ridersComplete)break;
                if(PassengerTest && Time.realtimeSinceStartup-began>45 && Boarded.Count==0 && !HasPassengerInstances()){Entry.Log("Passenger fixture stopped early: all requested instances ended before any observed boarding.");break;}
                if(Time.realtimeSinceStartup>=next){Entry.Log("Native far bus operation: elapsed="+(Time.realtimeSinceStartup-began)+", vehicle="+Bus+", flags="+data.m_flags+", line="+data.m_transportLine+", target="+data.m_targetBuilding+", path="+data.m_path+", position="+data.GetLastFramePosition()+", distance="+distance);if(PassengerTest)PassengerState();next+=30;}
                yield return null;
            }
            simulation.ForcedSimulationPaused=true;
            if(!complete || !spawned || distance<=2000)
            {Entry.Log("FAILURE: native far bus did not complete a full closed route within the operation deadline; spawned="+spawned+", distance="+distance);failed=true;}
            else Entry.Log("PASS: actual native bus "+Bus+" spawned from its depot, drove "+distance+" metres under normal simulation and arrived at both far stops then returned to its first stop. No manual vehicle movement or simulation steps. Passenger boarding/alighting is not asserted.");
            if(PassengerTest){if(Alighted.Count==0)failed=true;Entry.Log((Alighted.Count>0?"PASS":"FAILURE")+": actual native passenger transitions: boarded="+Boarded.Count+", same citizens alighted="+Alighted.Count+". Fixture origin/destination requests, no forced boarding.");}
            yield return new WaitForEndOfFrame();LiveRunDiagnostics.Screenshot("trial324-bus-vehicle.png");
            if(LiveFarOutsideChecks.Enabled)yield return LiveFarOutsideChecks.Run();
            if(LiveLodVisualChecks.Enabled)yield return LiveLodVisualChecks.Run();
            if(failed || LiveFarOutsideChecks.Failed)Bootstrap.FatalFailure=true;
        }
        finally{new Harmony(Owner).UnpatchAll(Owner);Application.Quit();}
    }
    static void PassengerState()
    {
        int waiting=0,moving=0,vehicle=0,missing=0;var citizens=Singleton<CitizenManager>.instance;
        lock(Gate)foreach(uint id in Riders){var person=citizens.m_citizens.m_buffer[id];if(person.m_vehicle!=0)vehicle++;if(person.m_instance==0){missing++;continue;}moving++;var instance=citizens.m_instances.m_buffer[person.m_instance];if((instance.m_flags&CitizenInstance.Flags.WaitingTransport)!=0)waiting++;}
        Entry.Log("Native passenger status: instances="+moving+", waiting transport="+waiting+", in vehicles="+vehicle+", no instance="+missing+", boarded="+Boarded.Count+", alighted="+Alighted.Count);
    }
    static bool HasPassengerInstances(){lock(Gate)foreach(uint id in Riders)if(Singleton<CitizenManager>.instance.m_citizens.m_buffer[id].m_instance!=0)return true;return false;}
}
