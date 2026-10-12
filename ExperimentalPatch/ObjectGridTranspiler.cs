using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace HundredTiles.ExperimentalPatch
{
    public static class ObjectGridTranspiler
    {
        // Keep the original raw-position scale. Only upper bounds and row
        // stride expand. Replacing all occurrences of 540/270 would move objects.
        public static IList<CodeInstruction> Transform(IEnumerable<CodeInstruction> source, int oldSide,
            Type instanceType, MethodInfo readX, MethodInfo readZ, bool rendererOnly = false)
        {
            if (oldSide != 540 && oldSide != 270) throw new ArgumentOutOfRangeException(nameof(oldSide));
            foreach (var helper in new[] { readX, readZ })
                if (helper == null || !helper.IsStatic || helper.ReturnType != typeof(int) || helper.GetParameters().Length != 1 ||
                    helper.GetParameters()[0].ParameterType != instanceType.MakeByRefType())
                    throw new ArgumentException("Raw coordinate helper signature differs.");
            var input = new List<CodeInstruction>(source);
            var output = new List<CodeInstruction>();
            int reads = 0, bounds = 0, strides = 0, scales = 0, renderDivisors = 0, renderScales = 0;
            for (int i = 0; i < input.Count; i++)
            {
                var code = new CodeInstruction(input[i]);
                var field = code.operand as FieldInfo;
                if (code.opcode == OpCodes.Ldfld && field != null && field.DeclaringType == instanceType &&
                    (field.Name == "m_posX" || field.Name == "m_posZ"))
                {
                    code.opcode = OpCodes.Call; code.operand = field.Name == "m_posX" ? readX : readZ; reads++;
                }
                if (code.opcode == OpCodes.Ldc_I4 && code.operand is int)
                {
                    int value = (int)code.operand;
                    if (value == oldSide - 1)
                    {
                        if (i + 1 >= input.Count || input[i + 1].opcode != OpCodes.Call ||
                            !(input[i + 1].operand is MethodInfo) || ((MethodInfo)input[i + 1].operand).Name != "Clamp")
                            throw new InvalidOperationException("Object grid bound is not a clamp.");
                        code.operand = oldSide / 9 * 10 - 1; bounds++;
                    }
                    else if (value == oldSide)
                    {
                        if (i > 0 && input[i - 1].opcode == OpCodes.Add && i + 1 < input.Count && input[i + 1].opcode == OpCodes.Mul)
                            scales++;
                        else if (i > 0 && input[i - 1].opcode == OpCodes.Ldloc_1 && i + 3 < input.Count &&
                            input[i + 1].opcode == OpCodes.Mul && input[i + 2].opcode == OpCodes.Ldloc_0 && input[i + 3].opcode == OpCodes.Add)
                        { code.operand = oldSide / 9 * 10; strides++; }
                        else if (i > 0 && input[i - 1].opcode == OpCodes.Mul && i + 1 < input.Count && input[i + 1].opcode == OpCodes.Div)
                        { code.operand = oldSide / 9 * 10; renderDivisors++; }
                        else throw new InvalidOperationException("Unreviewed object grid width use.");
                    }
                }
                if (code.opcode == OpCodes.Ldc_I4_S && Convert.ToInt32(code.operand) == 45 && i > 0 &&
                    (input[i - 1].opcode == OpCodes.Ldloc_0 || input[i - 1].opcode == OpCodes.Ldloc_1) &&
                    i + 3 < input.Count && input[i + 1].opcode == OpCodes.Mul &&
                    input[i + 2].opcode == OpCodes.Ldc_I4 && Equals(input[i + 2].operand, oldSide) && input[i + 3].opcode == OpCodes.Div)
                { code.opcode = OpCodes.Ldc_I4; code.operand = 50; renderScales++; }
                output.Add(code);
            }
            if (reads != 4 || bounds != 4 || strides != (rendererOnly ? 0 : 1) || scales != 4 ||
                (renderDivisors != 0 && renderDivisors != 2) || renderDivisors != renderScales)
                throw new InvalidOperationException("Object grid coordinate shape differs.");
            return output;
        }
    }
}
