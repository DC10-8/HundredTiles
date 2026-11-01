using System;using System.Collections.Generic;using System.IO;using System.Reflection;using System.Reflection.Emit;using System.Security.Cryptography;using ColossalFramework;using ColossalFramework.Plugins;using HarmonyLib;using HundredTiles.SaveBridge;using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 // Runtime's per-mod compatibility code verifies a mod's exact code and throws if it differs, which stopped every city
 // load whenever a subscribed mod updated (RightOfWay 0.3.0.0). The patcher routes those calls here. A mod that no longer
 // matches keeps running, without its reviewed hooks, and AutoAdapter adapts its simple grid code before the first city load.
 public static class ChangedMods {
  static readonly HashSet<Assembly> changed=new HashSet<Assembly>();
  static readonly HashSet<MethodBase> failed=new HashSet<MethodBase>();
  const BindingFlags Any=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static Type Runtime(string name){foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="HundredTiles.Runtime")return a.GetType("HundredTiles.Runtime."+name,true);throw new InvalidOperationException("HundredTiles runtime not loaded.");}
  static string Reason(Exception e){return e is TargetInvocationException && e.InnerException!=null?e.InnerException.Message:e.Message;}
  public static void Warn(string text){try{Debug.LogWarning("[HundredTiles] "+text);}catch(Exception){}}
  static void Report(string mod,Exception e){Warn(mod+" differs from the reviewed code, so its reviewed hooks were skipped and automatic grid adaptation applies. "+Reason(e));}

  public static void Verify(Assembly assembly,string path){try{Runtime("ReviewedCompatibility").GetMethod("Verify",Any).Invoke(null,new object[]{assembly,path});}catch(Exception e){changed.Add(assembly);Report(assembly.GetName().Name,e);}}
  public static void Add(Assembly assembly,IList<HookSpec> hooks,string type,string name,string adapter){if(changed.Contains(assembly))return;try{Runtime("ReviewedCompatibility").GetMethod("Add",Any).Invoke(null,new object[]{assembly,hooks,type,name,adapter});}catch(Exception e){Report(assembly.GetName().Name,e);}}
  // Removes whatever a failing adapter already queued, so a partly adapted mod is not left half hooked.
  public static void AddHooks(Assembly assembly,IList<HookSpec> hooks,string adapter){int count=hooks.Count;
   try{FindAdapter(adapter).GetMethod("AddHooks",Any,null,new[]{typeof(Assembly),typeof(IList<HookSpec>)},null).Invoke(null,new object[]{assembly,hooks});}
   catch(Exception e){while(hooks.Count>count)hooks.RemoveAt(hooks.Count-1);changed.Add(assembly);Report(assembly.GetName().Name,e);}}
  // More Network Capacity: its reviewed wrapper check throws on any update, but HundredTiles only needs the capacity the
  // mod asks for, so that is read from the mod itself, or from the live segment buffer if the mod's flag is gone.
  public static void Configure(){try{Runtime("NetworkCapacityCompatibility").GetMethod("Configure",Any).Invoke(null,null);}
   catch(Exception e){Assembly core=null;foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="MoreNetworkCapacity")core=a;Report("More Network Capacity",e);if(core==null)return;
    PropertyInfo enabled=null;var state=core.GetType("MoreNetworkCapacity.PatchState");if(state!=null)enabled=state.GetProperty("ExpansionEnabled",Any);
    Func<int> capacity=delegate{if(enabled!=null)return (bool)enabled.GetValue(null,null)?65024:32768;return Singleton<NetManager>.exists && Singleton<NetManager>.instance.m_segments.m_size>32768u?65024:32768;};
    foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="HundredTiles.Utilities")foreach(var t in a.GetTypes())if(t.Name=="UtilitySaveBridge"){t.GetField("RequestedCapacity",Any).SetValue(null,capacity);return;}}}
  // The deferral itself only needs the two reflective Invoke calls, so it stays on while they are still there.
  public static void AddRecalculation(Assembly assembly,IList<HookSpec> hooks){var adapter=Runtime("NetworkRecalculationCompatibility");
   try{adapter.GetMethod("AddHooks",Any).Invoke(null,new object[]{assembly,hooks});}
   catch(Exception e){try{var type=assembly.GetType("MoreNetworkCapacityRenew.TMPECompatibility");MethodInfo method=type==null?null:type.GetMethod("EnsureSegmentEndManagersCapacity");int calls=0;
     if(method!=null)foreach(var ins in PatchProcessor.GetOriginalInstructions(method)){var called=ins.operand as MethodInfo;if(called!=null && called.Name=="Invoke" && called.DeclaringType==typeof(MethodBase) && called.GetParameters().Length==2)calls++;}
     if(calls==2)hooks.Add(new HookSpec{Target=method,Transpiler=adapter.GetMethod("DeferCalls",Any)});else Report(assembly.GetName().Name,e);}
    catch(Exception inner){Report(assembly.GetName().Name,inner);}}}
  // Replaces Runtime's inline check, which read only the local Mods path and threw on any other build.
  const string HistoricalHash="43B17D6F29F813548BCEC717D8BFACC4F7A5A9352E2AB34ED5C1AC13C9AD089D";
  public static void AddHistorical(IList<HookSpec> hooks){try{Assembly mod=null;foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="AutoHistoricalPlop")mod=a;if(mod==null)return;
    string hash=null;foreach(var p in PluginManager.instance.GetPluginsInfo())if(p.GetAssemblies().Contains(mod)){string file=Path.Combine(p.modPath,"AutoHistoricalPlop.dll");if(File.Exists(file))using(var h=SHA256.Create())hash=BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(file))).Replace("-","");}
    if(hash!=HistoricalHash)throw new InvalidOperationException("Build differs.");
    hooks.Add(new HookSpec{Target=mod.GetType("AutoHistoricalPlop.Managers.ZoneRemovalGuard",true).GetMethod("ClearZonesUpdatedIn"),Transpiler=Runtime("Compatibility").GetMethod("HistoricalGrid",Any)});}
   catch(Exception e){Report("AutoHistoricalPlop",e);}}
  // A reviewed adapter that no longer fits a mod's code must not undo the whole install. Game targets stay strict: if
  // one of them fails, the map expansion itself is incomplete.
  public static MethodInfo Patch(Harmony harmony,MethodBase target,HarmonyMethod prefix,HarmonyMethod postfix,HarmonyMethod transpiler,HarmonyMethod finalizer){
   string module=target.Module.Assembly.GetName().Name;if(module=="Assembly-CSharp" || module=="ColossalManaged" || module=="ICities" || module.StartsWith("UnityEngine"))return harmony.Patch(target,prefix,postfix,transpiler,finalizer);
   try{return harmony.Patch(target,prefix,postfix,transpiler,finalizer);}catch(Exception e){lock(failed)failed.Add(target);Report(module,e);return null;}}
  // SaveSafety treats a listed target without a HundredTiles patch as removed and blocks saving, so a failed adapter leaves the list.
  public static void DropFailed(IList<HookSpec> hooks){lock(failed)for(int i=hooks.Count-1;i>=0;i--)if(failed.Contains(hooks[i].Target))hooks.RemoveAt(i);}
  // A serialization patch from a mod HundredTiles already knows, whose code has changed: 1 reviewed and fine, 2 known but
  // changed (allowed with a warning), -1 an owner HundredTiles has never reviewed.
  public static int KnownOwner(string owner,MethodBase target,Patches patches){var reviewed=Runtime("ReviewedCompatibility");
   try{if((bool)reviewed.GetMethod("Allowed",Any).Invoke(null,new object[]{owner,target,patches}))return 1;}catch(Exception){}
   if((bool)Runtime("LatePatchCompatibility").GetMethod("Handles",Any).Invoke(null,new object[]{owner}))return 2;
   return reviewed.GetMethod("AssemblyFor",Any).Invoke(null,new object[]{owner})!=null?2:-1;}
  static Type FindAdapter(string fullName){foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="HundredTiles.Runtime")return a.GetType(fullName,true);throw new InvalidOperationException("HundredTiles runtime not loaded.");}
 }
}
