using System;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;using HarmonyLib;using HundredTiles.Core;using HundredTiles.SaveBridge;using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 // Reviewed patches of Transfer Manager CE 3.1.39, Intersection Marking Tool 1.15, Campus Parks r2.0.0 and Lifecycle
 // Rebalance Revisited 1.6.9 on HundredTiles targets. Each build is pinned by its manifest; an unreviewed build leaves its
 // adapter off without stopping HundredTiles, so saving stays blocked for that mod.
 // - Transfer Manager CE walks manager grids itself in nine helpers; their vanilla sides are remapped (origins stay).
 //   ShipAI.CanSpawnAt is replaced by its prefix, which uses one of those helpers.
 // - Lifecycle Rebalance replaces ResidentAI.GetTaxiProbability and calls PointOutOfArea directly; that call is routed through
 //   HundredTiles' TaxiHooks.Unavailable, the same map-bounds check HundredTiles adds to the original.
 // - Intersection Marking Tool and Campus Parks transpile HundredTiles targets without grid code. HundredTiles' transpiler
 //   requires the original IL, so they are allowed only when it runs before theirs.
 public static class ModAdapters {
  sealed class Mod { public string Assembly, Owner; public string[][] Grids=new string[0][]; public string[][] Calls=new string[0][]; public string[][] Patches; public bool Adapted; }
  const string Any="*";
  // Grids: type, method, kind (citizen|net|vehicle2), expected constants. Patches: type, method, phase, target type, target method.
  static readonly Mod[] Mods={
   new Mod{Assembly="TransferManagerCE",Owner="Sleepy.TransferManagerCE",
    Grids=new[]{
     new[]{"SleepyCommon.CitiesUtils","CalculatePassengerCount","citizen","3"},
     new[]{"TransferManagerCore.TransportStationAIPatches","HasReachedPassengerLimit","citizen","3"},
     new[]{"TransferManagerCore.CheckRoadAccessPatches","FindTrainAccess","net","3"},
     new[]{"TransferManagerCore.FindCargoStationPatch","FindCargoStation","net","3"},
     new[]{"TransferManagerCore.PathNode","FindRoadAccess","net","3"},
     new[]{"TransferManagerCore.SelectionTool","FindNearestNode","net","3"},
     new[]{"TransferManagerCore.BuildingUtils","EnumerateNearbyBuildings","net","5"},
     new[]{"TransferManagerCore.NodeUtils","EnumerateNearbyNodes","net","3"},
     new[]{"TransferManagerCore.ShipSpawnPatches","CanSpawnAt","vehicle2","3"}},
    Patches=new[]{
     new[]{"TransferManagerCore.AircraftSpawnPatches","AircraftAICanSpawnAtPrefix","prefix","AircraftAI","CanSpawnAt"},
     new[]{"TransferManagerCore.ShipSpawnPatches","ShipAICanSpawnAtPrefix","prefix","ShipAI","CanSpawnAt"},
     new[]{"TransferManagerCore.CheckPassengersPatches","PassengerShipAICheckPassengers","postfix","PassengerShipAI","CheckPassengers"},
     new[]{"TransferManagerCore.CheckPassengersPatches","PassengerPlaneAICheckPassengers","postfix","PassengerPlaneAI","CheckPassengers"},
     new[]{"TransferManagerCore.CheckPassengersPatches","PassengerTrainAICheckPassengers","postfix","PassengerTrainAI","CheckPassengers"},
     new[]{"TransferManagerCore.PathDistancePatches","UpdateNotifications","postfix","BuildingManager","UpdateNotifications"}}},
   new Mod{Assembly="IntersectionMarkingTool",Owner="IntersectionMarkingTool",
    Patches=new[]{
     new[]{"IMT.Mod","NetNode_UpdateNodeRenderer_Transpiler","transpiler","NetManager","UpdateNodeRenderer"},
     new[]{"IMT.Mod","NetSegment_UpdateSegmentRenderer_Transpiler","transpiler","NetManager","UpdateSegmentRenderer"}}},
   new Mod{Assembly="CampusParks",Owner="github.com/bloodypenguin/Skylines-CampusParks",
    Patches=new[]{new[]{"CampusParks.ParkBuildingAIPatch","Transpiler","transpiler","ParkBuildingAI",Any}}},
   new Mod{Assembly="Lifecycle Rebalance Revisited",Owner="com.github.algernon-A.csl.lifecyclerebalancerevisited",
    Calls=new[]{new[]{"LifecycleRebalance.ResidentAITransportPatches","GetTaxiProbability"}},
    Patches=new[]{new[]{"LifecycleRebalance.ResidentAITransportPatches","GetTaxiProbability","prefix","ResidentAI","GetTaxiProbability"}}},
  };
  const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
  static int GridSize {get{return (int)typeof(PackedObjectPosition).Assembly.GetType("HundredTiles.Core.ExpandedMap").GetField("GridSize").GetRawConstantValue();}}
  static readonly Dictionary<MethodBase,int> expected=new Dictionary<MethodBase,int>();

  static MethodInfo Single(Assembly assembly,string type,string name){MethodInfo found=null;foreach(var m in assembly.GetType(type,true).GetMethods(All))if(m.Name==name){if(found!=null)throw new InvalidOperationException("Overloaded adapter target: "+type+"."+name);found=m;}
   if(found==null)throw new InvalidOperationException("Adapter target missing: "+type+"."+name);return found;}

  public static void Prepare(IList<HookSpec> hooks){foreach(var mod in Mods){Assembly assembly=null;foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name==mod.Assembly)assembly=a;if(assembly==null)continue;
    try{ReviewedOverlays.VerifyModule(assembly);}catch(Exception ex){Debug.LogWarning("[HundredTiles] "+mod.Assembly+" adapter not applied: "+ex.Message);continue;}
    foreach(var g in mod.Grids){var method=Single(assembly,g[0],g[1]);expected[method]=int.Parse(g[3]);hooks.Add(new HookSpec{Target=method,Transpiler=typeof(ModAdapters).GetMethod(g[2]=="citizen"?"CitizenGrid":g[2]=="net"?"NetGrid":"VehicleGrid2")});}
    foreach(var c in mod.Calls)hooks.Add(new HookSpec{Target=Single(assembly,c[0],c[1]),Transpiler=typeof(ModAdapters).GetMethod("TaxiArea")});
    mod.Adapted=true;Debug.Log("[HundredTiles] "+mod.Assembly+" adapter applied: "+mod.Grids.Length+" grid methods, "+mod.Calls.Length+" area checks.");}}

  // Vanilla sides: citizen 2160 (8 m), node/segment/building 270 (64 m), vehicle grid2 54 (320 m).
  public static IEnumerable<CodeInstruction> CitizenGrid(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod){return GridRemap.Sides(instructions,2160,GridSize*240,expected[__originalMethod],__originalMethod.DeclaringType.Name+"."+__originalMethod.Name+" citizen");}
  public static IEnumerable<CodeInstruction> NetGrid(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod){return GridRemap.Sides(instructions,270,GridSize*30,expected[__originalMethod],__originalMethod.DeclaringType.Name+"."+__originalMethod.Name+" network");}
  public static IEnumerable<CodeInstruction> VehicleGrid2(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod){return GridRemap.Sides(instructions,54,GridSize*6,expected[__originalMethod],__originalMethod.DeclaringType.Name+"."+__originalMethod.Name+" vehicle");}
  public static IEnumerable<CodeInstruction> TaxiArea(IEnumerable<CodeInstruction> instructions){var area=typeof(GameAreaManager).GetMethod("PointOutOfArea",new[]{typeof(Vector3)});int changes=0;var output=new List<CodeInstruction>();
   foreach(var source in instructions){var ins=new CodeInstruction(source);if(ins.Calls(area)){ins.opcode=OpCodes.Call;ins.operand=typeof(TaxiHooks).GetMethod("Unavailable");changes++;}output.Add(ins);}
   if(changes!=1)throw new InvalidOperationException("Lifecycle Rebalance taxi area check shape changed.");return output;}

  // HundredTiles' transpiler must see the original IL: it runs first when it has higher priority, or equal priority and an earlier index.
  static bool RunsAfterHundredTiles(Patch patch,Patches patches){if(patch.before!=null && Array.IndexOf(patch.before,"HundredTiles.Runtime")>=0)return false;
   foreach(var ht in patches.Transpilers)if(ht.owner=="HundredTiles.Runtime")return patch.priority<ht.priority || (patch.priority==ht.priority && patch.index>ht.index);return true;}

  // -1: not one of these owners; 1: every patch of the owner on the target is reviewed; 0: refuse.
  public static int Evaluate(string owner,MethodBase target,Patches patches){Mod mod=null;foreach(var m in Mods)if(m.Owner==owner)mod=m;if(mod==null)return -1;if(!mod.Adapted)return 0;bool found=false;
   foreach(var pair in new[]{new KeyValuePair<string,IEnumerable<Patch>>("prefix",patches.Prefixes),new KeyValuePair<string,IEnumerable<Patch>>("postfix",patches.Postfixes),new KeyValuePair<string,IEnumerable<Patch>>("transpiler",patches.Transpilers),new KeyValuePair<string,IEnumerable<Patch>>("finalizer",patches.Finalizers)})
    foreach(var patch in pair.Value)if(patch.owner==owner){var pm=patch.PatchMethod;if(pm.Module.Assembly.GetName().Name!=mod.Assembly)return 0;bool ok=false;
     foreach(var row in mod.Patches)if(pm.DeclaringType.FullName==row[0] && pm.Name==row[1] && pair.Key==row[2] && target.DeclaringType.Name==row[3] && (row[4]==Any || target.Name==row[4]))ok=true;
     if(!ok || (pair.Key=="transpiler" && !RunsAfterHundredTiles(patch,patches)))return 0;ReviewedOverlays.VerifyModule(pm.Module.Assembly);found=true;}
   return found?1:0;}
 }
}
