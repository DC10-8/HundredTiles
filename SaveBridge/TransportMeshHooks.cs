using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.Core;
using HundredTiles.ExperimentalPatch;

namespace HundredTiles.SaveBridge
{
    public static class TransportMeshHooks
    {
        public static HookSpec Specification()
        {
            var target = typeof(TransportLine).GetMethod("UpdateMeshData");
            SerializerHooks.Verify(target);
            if (ReviewedMethod.SemanticHash(target) != "78AE449B9252F62B20FE972B63408071256E77EACC39F826368FA4169F69507E")
                throw new InvalidOperationException("Reviewed transport surface mesh method changed.");
            return new HookSpec { Target = target, Transpiler = typeof(TransportMeshHooks).GetMethod("PatchCount") };
        }
        public static IEnumerable<CodeInstruction> PatchCount(IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>();
            int count = 0;
            foreach (var source in instructions)
            {
                var item = new CodeInstruction(source);
                if (item.LoadsConstant(81))
                {
                    item.opcode = OpCodes.Ldc_I4;
                    item.operand = ExpandedMap.TileCount;
                    count++;
                }
                result.Add(item);
            }
            if (count != 1)
                throw new InvalidOperationException("Transport surface mesh patch allocation shape changed.");
            return result;
        }
    }
}
