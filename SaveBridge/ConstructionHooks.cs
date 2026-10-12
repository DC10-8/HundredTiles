using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using UnityEngine;

namespace HundredTiles.SaveBridge
{
    public static class ConstructionHooks
    {
        public static HookSpec Specification()
        {
            var target=typeof(NetTool).GetMethod("MakeControlPoint");SerializerHooks.Verify(target);
            if(ReviewedMethod.SemanticHash(target)!="D1110D681D66EAAB4A896BE1728CAEC82F98C11F92C332D2F104081172979D64")throw new InvalidOperationException("Reviewed construction control point changed.");
            return new HookSpec{Target=target,Transpiler=typeof(ConstructionHooks).GetMethod("ControlPointBoundary")};
        }
        public static IEnumerable<CodeInstruction> ControlPointBoundary(IEnumerable<CodeInstruction> instructions)
        {
            var code=new List<CodeInstruction>();foreach(var item in instructions)code.Add(new CodeInstruction(item));
            // Replace only the reviewed empty-ground outside-connection snap.
            // Node/segment snapping, terrain height, elevation, and all checks remain native.
            if(code.Count<620 || code[449].opcode!=OpCodes.Ldc_R4 || (float)code[449].operand!=8640f || code[611].opcode!=OpCodes.Ldarg_S || Convert.ToInt32(code[611].operand)!=9)throw new InvalidOperationException("Construction boundary IL shape changed.");
            var first=new CodeInstruction(OpCodes.Ldarg_2);first.labels.AddRange(code[449].labels);first.blocks.AddRange(code[449].blocks);
            code.RemoveRange(449,162);
            code.InsertRange(449,new[]{first,new CodeInstruction(OpCodes.Ldarg_S,(byte)9),new CodeInstruction(OpCodes.Call,typeof(ConstructionHooks).GetMethod("SnapOutside"))});
            return code;
        }
        public static void SnapOutside(NetInfo info,ref NetTool.ControlPoint point)
        {
            const float min=-8640f,max=10560f,center=960f;
            var position=point.m_position;float width=info.m_halfWidth;
            if(Mathf.Abs(position.x-center)>=Mathf.Abs(position.z-center))
            {
                if(position.x>max-width*3f){position.x=max+width*.8f;position.z=Mathf.Clamp(position.z,min+width,max-width);point.m_outside=true;}
                if(position.x<min+width*3f){position.x=min-width*.8f;position.z=Mathf.Clamp(position.z,min+width,max-width);point.m_outside=true;}
            }
            else
            {
                if(position.z>max-width*3f){position.z=max+width*.8f;position.x=Mathf.Clamp(position.x,min+width,max-width);point.m_outside=true;}
                if(position.z<min+width*3f){position.z=min-width*.8f;position.x=Mathf.Clamp(position.x,min+width,max-width);point.m_outside=true;}
            }
            point.m_position=position;
        }
    }
}
