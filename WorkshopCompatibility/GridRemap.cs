using System;using System.Collections.Generic;using System.Reflection.Emit;using HarmonyLib;
namespace HundredTiles.WorkshopCompatibility {
 // Other mods that walk a manager grid themselves hard-code the vanilla side (n and n-1). HundredTiles keeps each grid's
 // origin and enlarges only its side, matching its own plan edits (269/270, 2159/2160, 53/54), so only those two constants move.
 public static class GridRemap {
  public static List<CodeInstruction> Sides(IEnumerable<CodeInstruction> instructions,int vanillaSide,int side,int expected,string what){int changes=0;var output=new List<CodeInstruction>();
   foreach(var source in instructions){var ins=new CodeInstruction(source);
    if((ins.opcode==OpCodes.Ldc_I4 || ins.opcode==OpCodes.Ldc_I4_S) && ins.operand!=null){int n=Convert.ToInt32(ins.operand);if(n==vanillaSide-1 || n==vanillaSide){ins.opcode=OpCodes.Ldc_I4;ins.operand=n==vanillaSide?side:side-1;changes++;}}
    output.Add(ins);}
   if(changes!=expected)throw new InvalidOperationException(what+" grid shape changed ("+changes+" of "+expected+" constants).");return output;}
 }
}
