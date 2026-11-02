using System;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;using HarmonyLib;using HundredTiles.Core;using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 // DistrictTool.ApplyBrush takes the grid origin from its width local (width*0.5). HundredTiles widens that local but grows
 // the grid toward +x/+z from origin 450, so every stroke landed (Side/2-450) cells off: 960 m on 100 tiles, 8640 m on 324.
 // The tree, prop and terrain brushes have plan edits for this; the plan missed this one. The width stays for the bounds.
 public static class DistrictBrush {
  const string Owner="HundredTiles.AutoAdapt";
  const int OriginUses=6;
  static int Constant(string name){return (int)typeof(PackedObjectPosition).Assembly.GetType("HundredTiles.Core.DistrictGrid").GetField(name).GetRawConstantValue();}

  public static string Apply(){MethodInfo target=null;
   foreach(var method in typeof(DistrictTool).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))if(method.Name=="ApplyBrush" && method.GetParameters().Length==6)target=method;
   if(target==null)throw new MissingMethodException("DistrictTool","ApplyBrush");
   new Harmony(Owner).Patch(target,transpiler:new HarmonyMethod(typeof(DistrictBrush).GetMethod("Transpile")));
   return "[HundredTiles] District brush origin set to "+Constant("Origin")+".";}

  // Leaves the method alone unless the whole shape matches, including after HundredTiles' own transpiler has been removed.
  public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions){var list=new List<CodeInstruction>(instructions);int side=Constant("Side"),width=-1;
   for(int i=0;i+1<list.Count && width<0;i++)if(CodeInstructionExtensions.LoadsConstant(list[i],(long)side))width=StoredLocal(list[i+1]);
   var loads=new List<CodeInstruction>();
   if(width>=0)for(int i=0;i+3<list.Count;i++)if(LoadedLocal(list[i])==width && list[i+1].opcode==OpCodes.Conv_R4 && list[i+2].opcode==OpCodes.Ldc_R4 && (float)list[i+2].operand==.5f && list[i+3].opcode==OpCodes.Mul)loads.Add(list[i]);
   if(loads.Count!=OriginUses){ChangedMods.Warn("District brush origin not corrected: found "+loads.Count+" uses.");return list;}
   foreach(var load in loads){load.opcode=OpCodes.Ldc_I4;load.operand=Constant("Origin")*2;}
   return list;}

  static int StoredLocal(CodeInstruction ins){var op=ins.opcode;if(op==OpCodes.Stloc_0)return 0;if(op==OpCodes.Stloc_1)return 1;if(op==OpCodes.Stloc_2)return 2;if(op==OpCodes.Stloc_3)return 3;return op==OpCodes.Stloc_S || op==OpCodes.Stloc?Index(ins.operand):-1;}
  static int LoadedLocal(CodeInstruction ins){var op=ins.opcode;if(op==OpCodes.Ldloc_0)return 0;if(op==OpCodes.Ldloc_1)return 1;if(op==OpCodes.Ldloc_2)return 2;if(op==OpCodes.Ldloc_3)return 3;return op==OpCodes.Ldloc_S || op==OpCodes.Ldloc?Index(ins.operand):-1;}
  static int Index(object operand){var local=operand as LocalBuilder;return local!=null?local.LocalIndex:Convert.ToInt32(operand);}
 }
}
