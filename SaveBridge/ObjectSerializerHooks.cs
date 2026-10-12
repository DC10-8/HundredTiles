using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;

namespace HundredTiles.SaveBridge
{
    public static class ObjectSerializerHooks
    {
        public static IEnumerable<CodeInstruction> Serialize(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            SerializerHooks.Verify(__originalMethod);
            Type owner = __originalMethod.DeclaringType.DeclaringType;
            if (__originalMethod.Name != "Serialize" || (owner != typeof(TreeManager) && owner != typeof(PropManager)))
                throw new InvalidOperationException("Unreviewed object serializer target.");
            // Tree Serialize also reads the burning-list buffer. Match the first
            // occurrence explicitly rather than projecting that different buffer.
            Type expected = owner == typeof(TreeManager) ? typeof(TreeInstance[]) : typeof(PropInstance[]);
            return SerializerTranspiler.TransformBuffer(instructions, expected,
                typeof(ObjectSaveBridge).GetMethod(owner == typeof(TreeManager) ? "WriteTrees" : "WriteProps"));
        }
        public static IList<HookSpec> Specifications()
        {
            var result = new List<HookSpec>();
            foreach (Type owner in new[] { typeof(TreeManager), typeof(PropManager) })
                result.Add(new HookSpec { Target = owner.GetNestedType("Data").GetMethod("Serialize"), Transpiler = typeof(ObjectSerializerHooks).GetMethod("Serialize") });
            return result;
        }
    }
}
