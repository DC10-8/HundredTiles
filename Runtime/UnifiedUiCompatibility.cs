using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;

namespace HundredTiles.Runtime
{
    // Button destruction must not recreate an already destroyed UI singleton.
    public static class UnifiedUiCompatibility
    {
        public static void AddHooks(Assembly assembly, IList<HookSpec> hooks)
        {
            var target=assembly.GetType("UnifiedUI.GUI.ButtonBase",true).GetMethod("OnDestroy");
            var exists=assembly.GetType("UnifiedUI.GUI.MainPanel",true).GetProperty("Exists").GetGetMethod();
            if(ReviewedMethod.SemanticHash(target)!="95E40D0EB6B85747AD0F13781A98120698951F618EBCC802BE9F0F3C7F5CF1C0" ||
               ReviewedMethod.SemanticHash(exists)!="98BF5D839960722979B80FD88BBCF38E6F79BAC841C45A126905BAF0089987C5")
                throw new InvalidOperationException("Reviewed UnifiedUI destruction method changed.");
            hooks.Add(new HookSpec{Target=target,Transpiler=typeof(UnifiedUiCompatibility).GetMethod("PreserveCleanup")});
        }

        public static IEnumerable<CodeInstruction> PreserveCleanup(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
        {
            var result=new List<CodeInstruction>();
            foreach(var instruction in instructions)result.Add(new CodeInstruction(instruction));
            int getters=0,removals=0,hide=-1;
            for(int i=0;i<result.Count;i++)
            {
                var method=result[i].operand as MethodInfo;if(method==null)continue;
                if(method.DeclaringType.FullName=="UnifiedUI.GUI.MainPanel")
                {
                    if(method.Name=="get_Instance")getters++;
                    if(method.Name.StartsWith("remove_Event",StringComparison.Ordinal))removals++;
                }
                if(method.Name=="Hide" && method.DeclaringType.FullName=="ColossalFramework.UI.UIComponent")hide=i;
            }
            if(getters!=2 || removals!=2 || hide<1 || result[hide-1].opcode!=OpCodes.Ldarg_0 || __originalMethod.GetMethodBody().ExceptionHandlingClauses.Count!=0)
                throw new InvalidOperationException("UnifiedUI cleanup control flow differs from the reviewed method.");
            var tail=generator.DefineLabel();result[hide-1].labels.Add(tail);
            var exists=__originalMethod.DeclaringType.Assembly.GetType("UnifiedUI.GUI.MainPanel",true).GetProperty("Exists").GetGetMethod();
            result.InsertRange(0,new[]{new CodeInstruction(OpCodes.Call,exists),new CodeInstruction(OpCodes.Brfalse,tail)});
            return result;
        }
    }
}
