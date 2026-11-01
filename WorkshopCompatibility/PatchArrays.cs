using System;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;using System.Text;using HarmonyLib;using HundredTiles.Core;using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 // TerrainManager.GetPatchIndex returns up to GridSize*GridSize-1 once HundredTiles expands the map, but a surface line's
 // per-patch list is allocated as new TransportLine.TempUpdateMeshData[81]. HundredTiles' own hook covers only
 // TransportLine.UpdateMeshData. Elsewhere, a ship's path shown past patch 80 raises IndexOutOfRange on the simulation
 // thread every step. These methods only walk the array by data.Length, so widening the allocation is enough.
 public static class PatchArrays {
  const string Owner="HundredTiles.AutoAdapt";
  static int GridSize {get{return (int)typeof(PackedObjectPosition).Assembly.GetType("HundredTiles.Core.ExpandedMap").GetField("GridSize").GetRawConstantValue();}}

  // Returns one line per method for the startup log. A failure is logged and never stops the game.
  public static string Apply(IEnumerable<Assembly> mods){var report=new StringBuilder();var harmony=new Harmony(Owner);var targets=new List<MethodBase>();
   foreach(var type in new[]{typeof(PathVisualizer),typeof(NetAdjust)})foreach(var game in type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))if(Allocates(game))targets.Add(game);
   foreach(var assembly in mods){if(ReviewedBuild.MissingReference(assembly)!=null)continue;foreach(var method in ReviewedBuild.Methods(assembly)){try{if(!method.DeclaringType.IsGenericType && !method.IsGenericMethod && !method.DeclaringType.ContainsGenericParameters && Allocates(method))targets.Add(method);}catch(Exception){}}}
   foreach(var target in targets){string name=target.DeclaringType.FullName+"."+target.Name;
    try{harmony.Patch(target,transpiler:new HarmonyMethod(typeof(PatchArrays).GetMethod("Widen")));report.Append("\n  ").Append(name);}
    catch(Exception ex){report.Append("\n  ").Append(name).Append(" (patch failed: ").Append(ex.GetType().Name).Append(")");Debug.LogWarning("[HundredTiles] Patch array not widened: "+name+": "+ex.Message);}}
   return "[HundredTiles] Surface line patch arrays widened to "+GridSize*GridSize+" entries:"+report;}

  // ldc.i4.s 81 or ldc.i4 81, then newarr TempUpdateMeshData.
  public static bool Allocates(MethodBase method){var body=method.GetMethodBody();if(body==null)return false;byte[] il=body.GetILAsByteArray();
   for(int i=0;i<il.Length-6;i++){int next=-1;if(il[i]==0x1F && il[i+1]==81)next=i+2;else if(il[i]==0x20 && il[i+1]==81 && il[i+2]==0 && il[i+3]==0 && il[i+4]==0)next=i+5;
    if(next<0 || il[next]!=0x8D)continue;try{if(method.Module.ResolveType(BitConverter.ToInt32(il,next+1))==typeof(TransportLine.TempUpdateMeshData))return true;}catch(Exception){}}
   return false;}

  public static IEnumerable<CodeInstruction> Widen(IEnumerable<CodeInstruction> instructions){var list=new List<CodeInstruction>(instructions);int count=0;
   for(int i=0;i+1<list.Count;i++)if(CodeInstructionExtensions.LoadsConstant(list[i],81L) && list[i+1].opcode==OpCodes.Newarr && (Type)list[i+1].operand==typeof(TransportLine.TempUpdateMeshData)){list[i].opcode=OpCodes.Ldc_I4;list[i].operand=GridSize*GridSize;count++;}
   if(count==0)throw new InvalidOperationException("Surface line patch allocation not found.");
   return list;}
 }
}
