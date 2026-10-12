using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;

namespace HundredTiles.SaveBridge
{
    public static class ObjectGridHooks
    {
        public static IEnumerable<CodeInstruction> Coordinates(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            SerializerHooks.Verify(__originalMethod);
            bool tree = __originalMethod.DeclaringType == typeof(TreeManager);
            if ((!tree && __originalMethod.DeclaringType != typeof(PropManager)) ||
                (__originalMethod.Name != "InitializeTree" && __originalMethod.Name != "FinalizeTree" &&
                 __originalMethod.Name != "InitializeProp" && __originalMethod.Name != "FinalizeProp" &&
                 __originalMethod.Name != "UpdateTreeRenderer" && __originalMethod.Name != "UpdatePropRenderer"))
                throw new InvalidOperationException("Unreviewed object grid target.");
            string prefix = tree ? "Tree" : "Prop";
            return ObjectGridTranspiler.Transform(instructions, tree ? 540 : 270, tree ? typeof(TreeInstance) : typeof(PropInstance),
                typeof(ObjectPositionHooks).GetMethod(prefix + "X"), typeof(ObjectPositionHooks).GetMethod(prefix + "Z"), __originalMethod.Name.StartsWith("Update",StringComparison.Ordinal));
        }
        public static IList<HookSpec> Specifications()
        {
            var result = new List<HookSpec>();
            foreach (Type type in new[] { typeof(TreeManager), typeof(PropManager) })
            foreach (string operation in new[] { "Initialize", "Finalize", "Update" })
            {
                var method = type.GetMethod(operation + (type == typeof(TreeManager) ? "Tree" : "Prop") + (operation=="Update" ? "Renderer" : ""), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (method == null) throw new MissingMethodException(type.FullName, operation);
                result.Add(new HookSpec { Target = method, Transpiler = typeof(ObjectGridHooks).GetMethod("Coordinates") });
            }
            return result;
        }
    }
}
