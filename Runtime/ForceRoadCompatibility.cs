using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ColossalFramework;
using ColossalFramework.Plugins;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;
namespace HundredTiles.Runtime {
 public static class ForceRoadCompatibility {
  public static IEnumerable<CodeInstruction> SpatialGrid(IEnumerable<CodeInstruction> instructions) {
   var result=new List<CodeInstruction>();int count=0;
   foreach(var source in instructions){var item=new CodeInstruction(source);if(item.opcode==OpCodes.Ldc_I4 || item.opcode==OpCodes.Ldc_I4_S){int value=Convert.ToInt32(item.operand);if(value==269 || value==270){item.opcode=OpCodes.Ldc_I4;item.operand=value==269?299:300;count++;}}result.Add(item);}
   if(count!=1)throw new InvalidOperationException("ForceRoadConnect grid shape changed.");return result;
  }
  public static bool Reviewed;
  public static void AddHooks(Assembly assembly,IList<HookSpec> hooks) {
   string path=assembly.Location;
   if(string.IsNullOrEmpty(path))foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())foreach(var loaded in plugin.GetAssemblies())if(loaded==assembly)path=Path.Combine(plugin.modPath,"ForceRoadConnect.dll");
   if(string.IsNullOrEmpty(path)||!File.Exists(path)||PlannedTranspiler.Hash(File.ReadAllBytes(path))!="17F584B461B7E803106E3BF043180AFC540009AC11ABBA127371E098307E3C27")throw new InvalidOperationException("ForceRoadConnect version differs from reviewed adapter.");
   var type=assembly.GetType("ForceRoadConnect.Connect.ConnectionResolver",true);
   int count=0;
   foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)) {
    string expected=null;
    if(method.Name=="FindNearestNode" && method.GetParameters().Length==7)expected="93E07882BD1D47A46975578F2101BA03F65CC38CF8133061D2C9E208B0F88474";
    if(method.Name=="FindNearestSegment")expected="6D8D65A5BA26D1FF3B0006E6FE0ACA3E59B95DBA3E7CF40AFC4A1A0AB37143CF";
    if(method.Name=="GridCoordinate")expected="C99A8EC6354CFCBD9BF371F7EE334EBF67C3289AC274E5820C5C9E18356D4D4A";
    if(expected==null)continue;
    if(ReviewedMethod.SemanticHash(method)!=expected)throw new InvalidOperationException("ForceRoadConnect spatial helper changed: "+method.Name);
    hooks.Add(new HookSpec{Target=method,Transpiler=typeof(ForceRoadCompatibility).GetMethod("SpatialGrid")});count++;
   }
   if(count!=3)throw new InvalidOperationException("ForceRoadConnect reviewed helpers missing.");
   Reviewed=true;
  }
 }
}

