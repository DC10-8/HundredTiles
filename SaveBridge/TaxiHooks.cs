using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using HundredTiles.ExperimentalPatch;
namespace HundredTiles.SaveBridge
{
 public static class TaxiHooks
 {
  public static IList<HookSpec> Specifications()
  {
   var hooks=new List<HookSpec>();
   foreach(var type in new[]{typeof(ResidentAI),typeof(TouristAI)}){
    var target=type.GetMethod("GetTaxiProbability",BindingFlags.Instance|BindingFlags.NonPublic);SerializerHooks.Verify(target);
    string expected=type==typeof(ResidentAI)?"5B6A2417B1923D7A1BAC950753F3FF8A2C9A307F1B09D4F32DC7B048DDE53888":"90EA57C2CC0D91EA0AB2748170BA1EBE3DF42582F217B185D3B466D9FFD5CF6B";
    if(ReviewedMethod.SemanticHash(target)!=expected)throw new InvalidOperationException("Reviewed taxi probability method changed: "+type.Name);
    hooks.Add(new HookSpec{Target=target,Transpiler=typeof(TaxiHooks).GetMethod("AreaCheck")});
   }
   return hooks;
  }
  public static bool Unavailable(GameAreaManager areas,Vector3 position)
  {
   const float min=HundredTiles.Core.ExpandedMap.WorldMin+40f,max=HundredTiles.Core.ExpandedMap.WorldMax-40f;
   return position.x<min || position.x>max || position.z<min || position.z>max || areas.PointOutOfArea(position);
  }
  public static IEnumerable<CodeInstruction> AreaCheck(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
  {
   var result=new List<CodeInstruction>();int count=0;
   var target=typeof(GameAreaManager).GetMethod("PointOutOfArea",new[]{typeof(Vector3)});
   foreach(var source in instructions){var item=new CodeInstruction(source);if(item.Calls(target)){item.opcode=OpCodes.Call;item.operand=typeof(TaxiHooks).GetMethod("Unavailable");count++;}result.Add(item);}
   if(count!=(__originalMethod.DeclaringType==typeof(ResidentAI)?1:2))throw new InvalidOperationException("Taxi area check shape changed.");return result;
  }
 }
}
