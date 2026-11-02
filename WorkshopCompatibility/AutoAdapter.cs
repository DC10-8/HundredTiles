using System;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;using System.Text;using ColossalFramework;using ColossalFramework.Plugins;using HarmonyLib;using HundredTiles.Core;using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 // Adapts any enabled mod that walks a manager grid with vanilla constants, without a per-mod review.
 // HundredTiles keeps these grids' origins and scales only their side with the tile count (its own plan edits: 269/270,
 // 539/540, 2159/2160, 53/54, 44/45), so a method that reads one of them gets those two constants per grid remapped.
 // Grids whose origin or cell size also change (zone, district, utilities, resources, areas, terrain) are only reported.
 // Runs once before the first city load, after HundredTiles installed (earlier, vanilla-sized arrays would be over-indexed).
 // Each method is patched separately, and a failure is logged, never thrown.
 public static class AutoAdapter {
  const string Owner="HundredTiles.AutoAdapt";
  // Declaring manager, field, vanilla side.
  static readonly string[][] Simple={
   new[]{"NetManager","m_nodeGrid","270"},new[]{"NetManager","m_segmentGrid","270"},
   new[]{"BuildingManager","m_buildingGrid","270"},new[]{"BuildingManager","m_buildingGrid2","45"},
   new[]{"PropManager","m_propGrid","270"},new[]{"TreeManager","m_treeGrid","540"},
   new[]{"VehicleManager","m_vehicleGrid","540"},new[]{"VehicleManager","m_vehicleGrid2","54"},new[]{"VehicleManager","m_parkedGrid","540"},
   new[]{"CitizenManager","m_citizenGrid","2160"}};
  static readonly Dictionary<MethodBase,Dictionary<int,int>> maps=new Dictionary<MethodBase,Dictionary<int,int>>();
  static bool done;
  static int GridSize {get{return (int)typeof(PackedObjectPosition).Assembly.GetType("HundredTiles.Core.ExpandedMap").GetField("GridSize").GetRawConstantValue();}}

  static int VanillaSide(FieldInfo field){foreach(var s in Simple)if(field.DeclaringType.Name==s[0] && field.Name==s[1])return int.Parse(s[2]);return 0;}
  // Game and framework assemblies, and other map-expansion mods (their grid code targets their own sizes).
  static bool Skipped(string name){foreach(string p in new[]{"HundredTiles","CitiesHarmony","0Harmony","Assembly-CSharp","ColossalManaged","ICities","UnityEngine","Mono.","System","EightyOne","81Tiles","EightyOneTiles"})if(name.StartsWith(p))return true;return false;}

  public static void ApplyOnce(){if(done)return;done=true;
   try{var assemblies=new List<Assembly>();foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())if(plugin.isEnabled)foreach(var a in plugin.GetAssemblies())if(!assemblies.Contains(a))assemblies.Add(a);Debug.Log(Adapt(assemblies));}
   catch(Exception ex){Debug.LogWarning("[HundredTiles] Automatic grid adaptation stopped: "+ex);}
   try{var mods=new List<Assembly>();foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())if(plugin.isEnabled)foreach(var a in plugin.GetAssemblies())if(!mods.Contains(a) && !Skipped(a.GetName().Name))mods.Add(a);Debug.Log(PatchArrays.Apply(mods));}
   catch(Exception ex){Debug.LogWarning("[HundredTiles] Surface line patch arrays not widened: "+ex);}
   try{Debug.Log(DistrictBrush.Apply());}
   catch(Exception ex){Debug.LogWarning("[HundredTiles] District brush origin not corrected: "+ex);}}

  public static string Adapt(IEnumerable<Assembly> assemblies){var watch=System.Diagnostics.Stopwatch.StartNew();int g=GridSize;var report=new StringBuilder();int adapted=0,failed=0;
    foreach(var assembly in assemblies){if(Skipped(assembly.GetName().Name))continue;
     string missing;try{missing=ReviewedBuild.MissingReference(assembly);}catch(Exception ex){missing=ex.GetType().Name;}
     if(missing!=null){report.Append("\n  ").Append(assembly.GetName().Name).Append(": skipped (missing reference ").Append(missing).Append(")");continue;}
     var changed=new List<string>();var other=new List<string>();
     foreach(var method in ReviewedBuild.Methods(assembly)){bool unresolved;List<FieldInfo> fields;try{fields=ReviewedBuild.FieldsRead(method,out unresolved);}catch{continue;}
      var map=new Dictionary<int,int>();var complex=new List<string>();
      foreach(var f in fields){if(!ReviewedBuild.IsManagerGrid(f))continue;int v=VanillaSide(f);if(v==0){complex.Add(f.Name);continue;}int side=v/9*g;map[v]=side;map[v-1]=side-1;}
      string name=method.DeclaringType.FullName+"."+method.Name;
      if(complex.Count!=0)other.Add(name+" ("+string.Join(",",complex.ToArray())+": not adapted)");
      if(map.Count==0)continue;
      if(method.DeclaringType.IsGenericType || method.IsGenericMethod || method.DeclaringType.ContainsGenericParameters){other.Add(name+" (generic type: not adapted)");continue;}
      if(AlreadyAdapted(method))continue;
      int hits;try{hits=Count(method,map);}catch(Exception ex){other.Add(name+" (unreadable: "+ex.GetType().Name+")");continue;}if(hits==0){other.Add(name+" (no vanilla side constants)");continue;}
      try{lock(maps)maps[method]=map;new Harmony(Owner).Patch(method,transpiler:new HarmonyMethod(typeof(AutoAdapter).GetMethod("Transpile")));adapted++;changed.Add(name+" x"+hits);adapted+=AdaptHelpers(method,map,changed,other);}
      catch(Exception ex){failed++;other.Add(name+" (patch failed: "+ex.GetType().Name+")");}}
     if(changed.Count!=0 || other.Count!=0)report.Append("\n  ").Append(assembly.GetName().Name).Append(": ").Append(changed.Count==0?"":"adapted "+string.Join(", ",changed.ToArray())).Append(other.Count==0?"":(changed.Count==0?"":"; ")+string.Join(", ",other.ToArray()));}
    return "[HundredTiles] Automatic grid adaptation for "+g*g+" tiles: "+adapted+" methods adapted, "+failed+" failed, "+watch.ElapsedMilliseconds+" ms."+report;}

  // A grid reader often clamps its cell through a small helper in the same class (Mathf.Clamp(..., 0, 539)). The helper
  // reads no grid, so it gets only the n-1 constants; left alone, it keeps the walk inside the vanilla-sized cells.
  static int AdaptHelpers(MethodBase method,Dictionary<int,int> map,List<string> changed,List<string> other){var clamp=new Dictionary<int,int>();foreach(var kv in map)if(map.ContainsKey(kv.Key+1))clamp[kv.Key]=kv.Value;int n=0;var seen=new List<MethodBase>();
   foreach(var ins in PatchProcessor.GetOriginalInstructions(method)){var callee=ins.operand as MethodInfo;
    if(callee==null || (ins.opcode!=OpCodes.Call && ins.opcode!=OpCodes.Callvirt) || callee.DeclaringType!=method.DeclaringType || !callee.IsStatic || callee.ReturnType!=typeof(int) || callee.IsGenericMethod || seen.Contains(callee))continue;seen.Add(callee);
    string name=callee.DeclaringType.FullName+"."+callee.Name;
    try{bool unresolved;bool grid=false;foreach(var f in ReviewedBuild.FieldsRead(callee,out unresolved))if(ReviewedBuild.IsManagerGrid(f))grid=true;if(grid || AlreadyAdapted(callee))continue;int hits=Count(callee,clamp);if(hits==0)continue;
     lock(maps)maps[callee]=clamp;new Harmony(Owner).Patch(callee,transpiler:new HarmonyMethod(typeof(AutoAdapter).GetMethod("Transpile")));n++;changed.Add(name+" x"+hits);}
    catch(Exception ex){other.Add(name+" (helper not adapted: "+ex.GetType().Name+")");}}
   return n;}
  static bool AlreadyAdapted(MethodBase method){var info=Harmony.GetPatchInfo(method);if(info==null)return false;foreach(var p in info.Transpilers)if(p.owner=="HundredTiles.Runtime" || p.owner==Owner)return true;return false;}
  static int Count(MethodBase method,Dictionary<int,int> map){int n=0;foreach(var ins in PatchProcessor.GetOriginalInstructions(method))if(Remappable(ins,map))n++;return n;}
  static bool Remappable(CodeInstruction ins,Dictionary<int,int> map){return (ins.opcode==OpCodes.Ldc_I4 || ins.opcode==OpCodes.Ldc_I4_S) && ins.operand!=null && map.ContainsKey(Convert.ToInt32(ins.operand));}

  public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod){Dictionary<int,int> map;lock(maps)maps.TryGetValue(__originalMethod,out map);var output=new List<CodeInstruction>();
   foreach(var source in instructions){var ins=new CodeInstruction(source);if(map!=null && Remappable(ins,map)){ins.operand=map[Convert.ToInt32(ins.operand)];ins.opcode=OpCodes.Ldc_I4;}output.Add(ins);}
   return output;}
 }
}
