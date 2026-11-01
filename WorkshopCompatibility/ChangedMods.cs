using System;using System.Collections.Generic;using System.Reflection;using HundredTiles.SaveBridge;using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 // Runtime's per-mod compatibility code verifies a mod's exact code and throws if it differs, which stopped every city
 // load whenever a subscribed mod updated (RightOfWay 0.3.0.0). The patcher routes those calls here. A mod that no longer
 // matches keeps running, without its reviewed hooks, and AutoAdapter adapts its simple grid code before the first city load.
 // NetworkRecalculationCompatibility and NetworkCapacityCompatibility.Configure are not routed here: they decide array
 // capacities that must match the save.
 public static class ChangedMods {
  static readonly HashSet<Assembly> changed=new HashSet<Assembly>();
  const BindingFlags Any=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static Type Runtime(string name){foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="HundredTiles.Runtime")return a.GetType("HundredTiles.Runtime."+name,true);throw new InvalidOperationException("HundredTiles runtime not loaded.");}
  static string Reason(Exception e){return e is TargetInvocationException && e.InnerException!=null?e.InnerException.Message:e.Message;}
  static void Report(string mod,Exception e){Debug.LogWarning("[HundredTiles] "+mod+" differs from the reviewed code, so its reviewed hooks were skipped and automatic grid adaptation applies. "+Reason(e));}

  public static void Verify(Assembly assembly,string path){try{Runtime("ReviewedCompatibility").GetMethod("Verify",Any).Invoke(null,new object[]{assembly,path});}catch(Exception e){changed.Add(assembly);Report(assembly.GetName().Name,e);}}
  public static void Add(Assembly assembly,IList<HookSpec> hooks,string type,string name,string adapter){if(changed.Contains(assembly))return;Runtime("ReviewedCompatibility").GetMethod("Add",Any).Invoke(null,new object[]{assembly,hooks,type,name,adapter});}
  // Removes whatever a failing adapter already queued, so a partly adapted mod is not left half hooked.
  public static void AddHooks(Assembly assembly,IList<HookSpec> hooks,string adapter){int count=hooks.Count;
   try{FindAdapter(adapter).GetMethod("AddHooks",Any,null,new[]{typeof(Assembly),typeof(IList<HookSpec>)},null).Invoke(null,new object[]{assembly,hooks});}
   catch(Exception e){while(hooks.Count>count)hooks.RemoveAt(hooks.Count-1);changed.Add(assembly);Report(assembly.GetName().Name,e);}}
  static Type FindAdapter(string fullName){foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="HundredTiles.Runtime")return a.GetType(fullName,true);throw new InvalidOperationException("HundredTiles runtime not loaded.");}
 }
}
