using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 public static class CleanupAdapters {
  public static ToolBase CurrentTool(ToolController controller){return controller==null?null:controller.CurrentTool;}
  static bool Alive(object value){return value!=null && (!(value is UnityEngine.Object) || (UnityEngine.Object)value!=null);}
  public static UnityEngine.Object Button(object tool){if(!Alive(tool))return null;return (UnityEngine.Object)tool.GetType().GetField("m_button",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(tool);}
  public static void DisableUi(object tool){if(Alive(tool))tool.GetType().GetMethod("DisableUUI",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(tool,null);}
  // Preserve all remaining cleanup. Only dereferences of already destroyed
  // Unity objects are guarded; exceptions from live cleanup are not swallowed.
  public static IEnumerable<CodeInstruction> MoveItUninstall(IEnumerable<CodeInstruction> source){int mask=0;var output=new List<CodeInstruction>();foreach(var item in source){var ins=new CodeInstruction(item);var method=ins.operand as MethodInfo;var field=ins.operand as FieldInfo;
   if(method!=null && method.DeclaringType==typeof(ToolController) && method.Name=="get_CurrentTool"){ins.opcode=OpCodes.Call;ins.operand=typeof(CleanupAdapters).GetMethod("CurrentTool");mask|=1;}
   else if(field!=null && field.DeclaringType.FullName=="MoveIt.MoveItTool" && field.Name=="m_button" && ins.opcode==OpCodes.Ldfld){ins.opcode=OpCodes.Call;ins.operand=typeof(CleanupAdapters).GetMethod("Button");mask|=2;}
   else if(method!=null && method.DeclaringType.FullName=="MoveIt.MoveItTool" && method.Name=="DisableUUI"){ins.opcode=OpCodes.Call;ins.operand=typeof(CleanupAdapters).GetMethod("DisableUi");mask|=4;}
   output.Add(ins);
  }if(mask!=7)throw new InvalidOperationException("Move It cleanup shape changed.");return output;}
 }
}
