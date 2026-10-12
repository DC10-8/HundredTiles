using System;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;
static class LiveTaxiChecks
{
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static CitizenInstance At(Vector3 position){var data=new CitizenInstance();data.m_frame0.m_position=position;data.m_frame1=data.m_frame0;data.m_frame2=data.m_frame0;data.m_frame3=data.m_frame0;data.m_targetPos=position;return data;}
 public static void Run()
 {
  CitizenInfo resident=null,tourist=null;for(uint i=0;i<PrefabCollection<CitizenInfo>.LoadedCount();i++){var info=PrefabCollection<CitizenInfo>.GetLoaded(i);if(info.m_citizenAI is ResidentAI)resident=info;if(info.m_citizenAI is TouristAI)tourist=info;}
  if(resident==null || tourist==null)throw new Exception("Taxi citizen prefabs missing.");
  var residentProbability=typeof(ResidentAI).GetMethod("GetTaxiProbability",Flags);var touristProbability=typeof(TouristAI).GetMethod("GetTaxiProbability",Flags);
  foreach(var position in new[]{new Vector3(-2000,0,-2000),new Vector3(2000,800,2000),new Vector3(9800,0,10300),new Vector3(10300,900,9800),new Vector3(10500,0,10500)}){
   var data=At(position);foreach(var row in new[]{new[]{0,0},new[]{1,2},new[]{2,2},new[]{3,4},new[]{4,6}}){int value=(int)residentProbability.Invoke(resident.m_citizenAI,new object[]{(ushort)1,data,(Citizen.AgeGroup)row[0]});if(value!=row[1])throw new Exception("Resident taxi probability incorrect at "+position+": "+value);}
   if((int)touristProbability.Invoke(tourist.m_citizenAI,new object[]{(ushort)1,data})!=20)throw new Exception("Tourist taxi probability incorrect at "+position);
  }
  foreach(var position in new[]{new Vector3(-8620,50,0),new Vector3(10540,50,0),new Vector3(0,50,-8620),new Vector3(0,50,10540),new Vector3(12000,50,9800)}){
   var data=At(position);if((int)residentProbability.Invoke(resident.m_citizenAI,new object[]{(ushort)1,data,Citizen.AgeGroup.Adult})!=0 || (int)touristProbability.Invoke(tourist.m_citizenAI,new object[]{(ushort)1,data})!=0)throw new Exception("Map-edge taxi request allowed.");
  }
  var outsideTarget=At(new Vector3(9800,50,10300));outsideTarget.m_targetPos=new Vector3(12000,50,10300);if((int)touristProbability.Invoke(tourist.m_citizenAI,new object[]{(ushort)1,outsideTarget})!=0)throw new Exception("Tourist outside destination allowed.");
  var areas=Singleton<GameAreaManager>.instance;int old=areas.m_areaGrid[99];try{areas.m_areaGrid[99]=0;var data=At(new Vector3(9800,50,10300));if((int)residentProbability.Invoke(resident.m_citizenAI,new object[]{(ushort)1,data,Citizen.AgeGroup.Adult})!=0)throw new Exception("Unpurchased taxi origin allowed.");}finally{areas.m_areaGrid[99]=old;}
  Entry.Log("PASS: native resident and tourist taxi probability across negative/original/added coordinates and different heights; vanilla age rates; all four outer edges and outside tourist target refused; unpurchased origin refused.");
  LiveConstructionChecks.BuildNetwork("Basic Road",new Vector3(9700,0,10300));
  typeof(NetManager).GetMethod("SimulationStepImpl",Flags).Invoke(Singleton<NetManager>.instance,new object[]{0});typeof(ZoneManager).GetMethod("SimulationStepImpl",Flags).Invoke(Singleton<ZoneManager>.instance,new object[]{0});
  BuildingInfo depotInfo=null;for(uint i=0;i<PrefabCollection<BuildingInfo>.LoadedCount();i++){var info=PrefabCollection<BuildingInfo>.GetLoaded(i);var ai=info.m_buildingAI as DepotAI;if(ai!=null && ai.m_transportInfo!=null && ai.m_transportInfo.m_transportType==TransportInfo.TransportType.Taxi){depotInfo=info;break;}}
  if(depotInfo==null)throw new Exception("Taxi depot prefab missing (After Dark required).");
  ushort depot=LiveConstructionChecks.PlaceBuilding(depotInfo.name,new Vector3(9760,0,10272));
  var citizens=Singleton<CitizenManager>.instance;var buildings=Singleton<BuildingManager>.instance;var vehicles=Singleton<VehicleManager>.instance;var random=new Randomizer(6201);uint citizen;ushort instance;
  if(!citizens.CreateCitizen(out citizen,30,30,ref random) || !citizens.CreateCitizenInstance(out instance,ref random,resident,citizen))throw new Exception("Taxi passenger creation failed.");
  ushort vehicle=0;try{
   var pickup=new Vector3(9900,0,10308);pickup.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(pickup);
   var data=At(pickup);data.m_citizen=citizen;data.Info=resident;data.m_flags=CitizenInstance.Flags.Created|CitizenInstance.Flags.Character|CitizenInstance.Flags.WaitingTaxi;citizens.m_instances.m_buffer[instance]=data;citizens.m_citizens.m_buffer[citizen].m_instance=instance;
   depotInfo.m_buildingAI.StartTransfer(depot,ref buildings.m_buildings.m_buffer[depot],TransferManager.TransferReason.Taxi,new TransferManager.TransferOffer{Citizen=citizen,Position=pickup,Amount=1,Active=false});
   vehicle=buildings.m_buildings.m_buffer[depot].m_ownVehicles;if(vehicle==0 || !(vehicles.m_vehicles.m_buffer[vehicle].Info.m_vehicleAI is TaxiAI) || citizens.m_citizens.m_buffer[citizen].m_vehicle!=vehicle)throw new Exception("Taxi depot did not dispatch and assign passenger.");
   var taxi=(TaxiAI)vehicles.m_vehicles.m_buffer[vehicle].Info.m_vehicleAI;WaitPath(vehicles.m_vehicles.m_buffer[vehicle].m_path);
   Entry.Log("PASS: actual added-area taxi depot StartTransfer creates taxi, assigns citizen and finds pickup path with native asynchronous worker.");
   PathUnit.Position rideStart,rideEnd;var drop=pickup;drop.x=9780;
   if(!PathManager.FindPathPosition(pickup,ItemClass.Service.Road,NetInfo.LaneType.Vehicle,VehicleInfo.VehicleType.Car,VehicleInfo.VehicleCategory.Taxi,false,false,64,false,false,out rideStart) || !PathManager.FindPathPosition(drop,ItemClass.Service.Road,NetInfo.LaneType.Vehicle,VehicleInfo.VehicleType.Car,VehicleInfo.VehicleCategory.Taxi,false,false,64,false,false,out rideEnd))throw new Exception("Taxi passenger route endpoints missing.");
   uint ride;if(!Singleton<PathManager>.instance.CreatePath(out ride,ref random,20,rideStart,rideEnd,NetInfo.LaneType.Vehicle|NetInfo.LaneType.TransportVehicle,VehicleInfo.VehicleType.Car,VehicleInfo.VehicleCategory.Taxi,2000))throw new Exception("Taxi passenger route creation failed.");WaitPath(ride);citizens.m_instances.m_buffer[instance].m_path=ride;citizens.m_instances.m_buffer[instance].m_pathPositionIndex=255;citizens.m_instances.m_buffer[instance].m_targetBuilding=depot;
   taxi.ArriveAtDestination(vehicle,ref vehicles.m_vehicles.m_buffer[vehicle]);
   if(vehicles.m_vehicles.m_buffer[vehicle].m_path!=ride)throw new Exception("Taxi did not take passenger route at pickup.");
   Entry.Log("PASS: native Taxi-category passenger path computed inside added tiles and handed from citizen to taxi at pickup.");
   if((citizens.m_instances.m_buffer[instance].m_flags&CitizenInstance.Flags.WaitingTaxi)!=0 || (citizens.m_instances.m_buffer[instance].m_flags&CitizenInstance.Flags.EnteringVehicle)==0)throw new Exception("Native taxi pickup did not start boarding.");
   resident.m_citizenAI.SetCurrentVehicle(instance,ref citizens.m_instances.m_buffer[instance],vehicle,0,new Vector4(pickup.x,pickup.y,pickup.z,0));
   if(citizens.m_citizens.m_buffer[citizen].m_vehicle!=vehicle)throw new Exception("Taxi boarding lost passenger.");
   var passengerData=Singleton<TransportManager>.instance.m_passengers[(int)taxi.m_transportInfo.m_transportType];uint before=passengerData.m_residentPassengers.m_tempCount;
   vehicles.m_vehicles.m_buffer[vehicle].m_flags|=Vehicle.Flags.Parking;taxi.ArriveAtDestination(vehicle,ref vehicles.m_vehicles.m_buffer[vehicle]);
   if(citizens.m_citizens.m_buffer[citizen].m_vehicle!=0 || Singleton<TransportManager>.instance.m_passengers[(int)taxi.m_transportInfo.m_transportType].m_residentPassengers.m_tempCount!=before+1)throw new Exception("Native taxi drop-off / passenger statistics failed.");
   WaitPath(vehicles.m_vehicles.m_buffer[vehicle].m_path);
   Entry.Log("PASS: native taxi pickup clears WaitingTaxi, citizen boards, drop-off clears vehicle and counts passenger; return-to-depot path succeeds. Arrival callbacks invoked deterministically; autonomous driving duration not measured.");
  }finally{if(vehicle!=0)vehicles.ReleaseVehicle(vehicle);citizens.ReleaseCitizen(citizen);buildings.ReleaseBuilding(depot);}
 }
 static void WaitPath(uint path){if(path==0)throw new Exception("Taxi path absent.");var manager=Singleton<PathManager>.instance;var timer=System.Diagnostics.Stopwatch.StartNew();while((manager.m_pathUnits.m_buffer[path].m_pathFindFlags&12)==0 && timer.ElapsedMilliseconds<10000)Thread.Sleep(1);if((manager.m_pathUnits.m_buffer[path].m_pathFindFlags&4)==0)throw new Exception("Taxi path worker failed: "+manager.m_pathUnits.m_buffer[path].m_pathFindFlags);}
}
