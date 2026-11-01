using System;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;using HarmonyLib;using HundredTiles.Core;using HundredTiles.SaveBridge;using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 // Improved Public Transport 3.0.1 (Workshop 3690061052). Its sub-modules each call PatchAll on the whole assembly, so the
 // same attribute patches appear under eight Harmony IDs. The patches on HundredTiles targets keep no grid code of their own,
 // but BetterBoarding replaces LoadPassengers and, like three other IPT3 methods, walks the citizen/node grid with vanilla
 // constants (2160/1080, 270/135). Those four methods are transpiled to the expanded grid; only then are the reviewed patches allowed.
 public static class Ipt3Adapter {
  const string Module="ImprovedPublicTransport3";
  static readonly string[] Owners={"github.com/bloodypenguin/ImprovedPublicTransport","IPT3.BetterBoarding","com.vectorial1024.cities.ebs","llunak.BetterBusStopPosition","com.ipt3.mileagetaxi","com.vectorial1024.cities.ptu","github.com/bloodypenguin/Skylines-IntercityBusController","com.IPT.FlightTracker"};
  static readonly Type[] Boarding={typeof(BusAI),typeof(TrolleybusAI),typeof(TramAI),typeof(PassengerTrainAI),typeof(PassengerPlaneAI),typeof(PassengerHelicopterAI),typeof(PassengerBlimpAI),typeof(PassengerFerryAI),typeof(PassengerShipAI)};
  // type, method, phase, target type ("*" = any LoadPassengers above, VehicleAI = VehicleAI.SimulationStep)
  static readonly string[][] Reviewed={
   new[]{"BetterBoarding.LoadPassengers_BusAI","LoadPassengersBetter","prefix","BusAI"},
   new[]{"BetterBoarding.LoadPassengers_TrolleybusAI","LoadPassengersBetter","prefix","TrolleybusAI"},
   new[]{"BetterBoarding.LoadPassengers_TramAI","LoadPassengersBetter","prefix","TramAI"},
   new[]{"BetterBoarding.LoadPassengers_PassengerTrainAI","LoadPassengersBetter","prefix","PassengerTrainAI"},
   new[]{"BetterBoarding.LoadPassengers_PassengerHelicopterAI","LoadPassengersBetter","prefix","PassengerHelicopterAI"},
   new[]{"BetterBoarding.LoadPassengers_PassengerBlimpAI","LoadPassengersBetter","prefix","PassengerBlimpAI"},
   new[]{"BetterBoarding.LoadPassengers_PassengerFerryAI","LoadPassengersBetter","prefix","PassengerFerryAI"},
   new[]{"ExpressBusServices.Patch_BusLoadsPassengers","HandleBusAboutToLoadPassengers","prefix","BusAI"},
   new[]{"ExpressBusServices.Patch_BusLoadsPassengers","HandleBusAlreadyLoadedPassengers","postfix","BusAI"},
   new[]{"ExpressBusServices.Patch_TrolleyBusLoadsPassengers","HandleBusAboutToLoadPassengers","prefix","TrolleybusAI"},
   new[]{"ExpressBusServices.Patch_TrolleyBusLoadsPassengers","HandleBusAlreadyLoadedPassengers","postfix","TrolleybusAI"},
   new[]{"ExpressBusServices.Patch_TramLoadsPassengers","HandleTramAboutToLoadPassengers","prefix","TramAI"},
   new[]{"ExpressBusServices.Patch_TramLoadsPassengers","HandleTramAlreadyLoadedPassengers","postfix","TramAI"},
   new[]{"ExpressBusServices.Patch_VehicleAI_SimulationStep","PreSimulationStep","prefix","VehicleAI"},
   new[]{"ImprovedPublicTransport.HarmonyPatches.XYZVehicleAIPatches.LoadPassengersPatch","LoadPassengersPre","prefix","*"},
   new[]{"ImprovedPublicTransport.HarmonyPatches.XYZVehicleAIPatches.LoadPassengersPatch","LoadPassengersPost","postfix","*"},
  };
  static readonly string[][] Grids={
   new[]{"BetterBoarding.DataTypes.PassengerWaitingInfo","FindWaitingPassengers","CitizenGrid"},
   new[]{"ExpressBusServices.TransportLineUtil","DoWithEachPassengerWaiting","CitizenGrid"},
   new[]{"ImprovedPublicTransport.Query.WaitingPassengerCountQuery","Query","CitizenGrid"},
   new[]{"ImprovedPublicTransport.UI.PublicTransportStopWorldInfoPanel","ProcessNodes","NodeGrid"},
  };
  static bool adapted;
  static int GridSize {get{return (int)typeof(PackedObjectPosition).Assembly.GetType("HundredTiles.Core.ExpandedMap").GetField("GridSize").GetRawConstantValue();}}

  public static void Prepare(IList<HookSpec> hooks){Assembly ipt=null;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name==Module)ipt=assembly;if(ipt==null)return;
   // An unreviewed IPT3 build must not stop HundredTiles from starting; its patches simply stay unapproved, so saving remains blocked.
   var readers=new List<string>();foreach(var g in Grids)readers.Add(g[0]+"::"+g[1]);var build=ReviewedBuild.Verify(ipt,readers);
   if(!build.Ok){Debug.LogWarning("[HundredTiles] Improved Public Transport adapter not applied: "+build.Detail);return;}
   Debug.Log("[HundredTiles] Improved Public Transport reviewed: "+build.Detail);
   foreach(var grid in Grids){var type=ipt.GetType(grid[0],true);MethodInfo method=type.GetMethod(grid[1],BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
    if(method==null)throw new InvalidOperationException("Improved Public Transport grid method missing: "+grid[0]+"."+grid[1]);hooks.Add(new HookSpec{Target=method,Transpiler=typeof(Ipt3Adapter).GetMethod(grid[2])});}
   adapted=true;Debug.Log("[HundredTiles] Improved Public Transport adapter applied: "+Grids.Length+" grid methods.");}

  // HundredTiles keeps the vanilla origin (WorldMin -8640) and grows the map toward +x/+z, so a grid keeps its origin offset
  // (1080 for the 8 m citizen grid, 135 for the 64 m node grid) and only its side changes: 2159/2160 and 269/270.
  public static IEnumerable<CodeInstruction> CitizenGrid(IEnumerable<CodeInstruction> instructions){return GridRemap.Sides(instructions,2160,GridSize*240,3,"Improved Public Transport citizen");}
  public static IEnumerable<CodeInstruction> NodeGrid(IEnumerable<CodeInstruction> instructions){return GridRemap.Sides(instructions,270,GridSize*30,5,"Improved Public Transport node");}

  // -1: not an IPT3 owner; 1: every patch of this owner on the target is reviewed; 0: refuse.
  public static int Evaluate(string owner,MethodBase target,Patches patches){if(Array.IndexOf(Owners,owner)<0)return -1;if(!adapted)return 0;
   bool loading=target.Name=="LoadPassengers" && Array.IndexOf(Boarding,target.DeclaringType)>=0;var p=target.GetParameters();
   bool step=target.DeclaringType==typeof(VehicleAI) && target.Name=="SimulationStep" && p.Length==5;if(!loading && !step)return 0;bool found=false;
   foreach(var pair in new[]{new KeyValuePair<string,IEnumerable<Patch>>("prefix",patches.Prefixes),new KeyValuePair<string,IEnumerable<Patch>>("postfix",patches.Postfixes),new KeyValuePair<string,IEnumerable<Patch>>("transpiler",patches.Transpilers),new KeyValuePair<string,IEnumerable<Patch>>("finalizer",patches.Finalizers)})
    foreach(var patch in pair.Value)if(patch.owner==owner){var pm=patch.PatchMethod;if(pm.Module.Assembly.GetName().Name!=Module)return 0;bool ok=false;
     foreach(var row in Reviewed)if(pm.DeclaringType.FullName==row[0] && pm.Name==row[1] && pair.Key==row[2] && (row[3]=="*"?loading:row[3]=="VehicleAI"?step:loading && target.DeclaringType.Name==row[3]))ok=true;
     if(!ok)return 0;found=true;}
   return found?1:0;}
 }
}
