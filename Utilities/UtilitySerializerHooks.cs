using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.SaveBridge;
using HundredTiles.ExperimentalPatch;

namespace HundredTiles.Utilities
{
    public static class UtilitySerializerHooks
    {
        public static IList<HookSpec> Specifications()
        {
            var result=new List<HookSpec>();
            foreach(Type owner in new[]{typeof(ElectricityManager),typeof(WaterManager)})
            {
                Type data=owner.GetNestedType("Data");
                result.Add(new HookSpec{Target=data.GetMethod("Serialize"),Transpiler=typeof(UtilitySerializerHooks).GetMethod("Serialize")});
                result.Add(new HookSpec{Target=data.GetMethod("Deserialize"),Transpiler=typeof(UtilitySerializerHooks).GetMethod("Deserialize"),
                    Postfix=typeof(UtilitySaveBridge).GetMethod(owner==typeof(ElectricityManager)?"ElectricityRead":"WaterRead"),Finalizer=typeof(RuntimeSaveBridge).GetMethod("LoadFailure")});
            }
            return result;
        }
        public static IEnumerable<CodeInstruction> Serialize(IEnumerable<CodeInstruction> source,MethodBase __originalMethod)
        {
            SerializerHooks.Verify(__originalMethod);Type owner=__originalMethod.DeclaringType.DeclaringType;
            bool electricity=owner==typeof(ElectricityManager);
            if(__originalMethod.Name!="Serialize" || (!electricity && owner!=typeof(WaterManager)))throw new InvalidOperationException("Unreviewed utility serializer.");
            string prefix=electricity?"E.":"W.";
            var expected=new HashSet<string>(electricity?UtilitySaveBridge.ElectricityFields:UtilitySaveBridge.WaterFields);
            var used=new HashSet<string>();var result=new List<CodeInstruction>();
            foreach(var original in source)
            {
                var code=new CodeInstruction(original);result.Add(code);var field=code.operand as FieldInfo;
                if(code.opcode!=OpCodes.Ldfld || field==null || field.DeclaringType!=owner || !expected.Contains(field.Name))continue;
                used.Add(field.Name);var key=new CodeInstruction(OpCodes.Ldstr,prefix+field.Name);
                var call=new CodeInstruction(OpCodes.Call,typeof(UtilitySaveBridge).GetMethod("Freeze").MakeGenericMethod(field.FieldType));
                for(int i=code.blocks.Count-1;i>=0;i--)if(code.blocks[i].blockType==ExceptionBlockType.EndExceptionBlock){call.blocks.Insert(0,code.blocks[i]);code.blocks.RemoveAt(i);}
                result.Add(key);result.Add(call);
            }
            if(!used.SetEquals(expected))throw new InvalidOperationException("Utility serializer field coverage differs.");return result;
        }
        public static IEnumerable<CodeInstruction> Deserialize(IEnumerable<CodeInstruction> source,MethodBase __originalMethod)
        {
            SerializerHooks.Verify(__originalMethod);Type owner=__originalMethod.DeclaringType.DeclaringType;
            if(__originalMethod.Name!="Deserialize" || (owner!=typeof(ElectricityManager) && owner!=typeof(WaterManager)))throw new InvalidOperationException("Unreviewed utility reader.");
            bool electricity=owner==typeof(ElectricityManager);
            var helpers=new Dictionary<string,MethodInfo>();helpers.Add(electricity?"m_electricityGrid":"m_waterGrid",typeof(UtilitySaveBridge).GetMethod(electricity?"ReadElectricity":"ReadWater"));
            return SerializerTranspiler.Transform(source,helpers);
        }
    }
}
