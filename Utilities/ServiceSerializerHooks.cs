using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;

namespace HundredTiles.Utilities
{
    public static class ServiceSerializerHooks
    {
        public static IEnumerable<CodeInstruction> Serialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {
            SerializerHooks.Verify(__originalMethod);
            var helpers=new Dictionary<string,MethodInfo>();
            foreach(var pair in new[]{new[]{"m_localFinalResources","WriteFinal"},new[]{"m_localTempResources","WriteTemp"},new[]{"m_globalFinalResources","WriteGlobalFinal"},new[]{"m_globalTempResources","WriteGlobalTemp"},new[]{"m_totalFinalResources","WriteTotalFinal"},new[]{"m_totalTempResources","WriteTotalTemp"},new[]{"m_totalTempResourcesMul","WriteTotalMul"}})helpers.Add(pair[0],typeof(ServiceSaveBridge).GetMethod(pair[1]));
            return SerializerTranspiler.Transform(instructions,helpers);
        }
        public static IEnumerable<CodeInstruction> Deserialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {SerializerHooks.Verify(__originalMethod);return SerializerTranspiler.Transform(instructions,new Dictionary<string,MethodInfo>{{"m_localFinalResources",typeof(ServiceSaveBridge).GetMethod("ReadFinal")},{"m_localTempResources",typeof(ServiceSaveBridge).GetMethod("ReadTemp")}});}
        public static IList<HookSpec> Specifications()
        {
            var data=typeof(ImmaterialResourceManager).GetNestedType("Data");
            return new List<HookSpec>{new HookSpec{Target=data.GetMethod("Serialize"),Transpiler=typeof(ServiceSerializerHooks).GetMethod("Serialize")},new HookSpec{Target=data.GetMethod("Deserialize"),Transpiler=typeof(ServiceSerializerHooks).GetMethod("Deserialize"),Postfix=typeof(ServiceSaveBridge).GetMethod("ReadComplete"),Finalizer=typeof(RuntimeSaveBridge).GetMethod("LoadFailure")}};
        }
    }
}
