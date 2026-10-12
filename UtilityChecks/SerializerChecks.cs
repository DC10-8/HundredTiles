extern alias ExternalCecil;
using Cecil=ExternalCecil::Mono.Cecil;
using Cil=ExternalCecil::Mono.Cecil.Cil;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.Utilities;

static class SerializerChecks
{
    public static void Run(string path)
    {
        using var game=Cecil.ModuleDefinition.ReadModule(path);
        var opcodes=typeof(System.Reflection.Emit.OpCodes).GetFields().Where(f=>f.FieldType==typeof(System.Reflection.Emit.OpCode)).Select(f=>(System.Reflection.Emit.OpCode)f.GetValue(null)).ToDictionary(op=>op.Value);
        foreach(Type owner in new[]{typeof(ElectricityManager),typeof(WaterManager)})
        foreach(string name in new[]{"Serialize","Deserialize"})
        {
            var target=game.Types.Single(t=>t.Name==owner.Name).NestedTypes.Single(t=>t.Name=="Data").Methods.Single(m=>m.Name==name);
            var input=target.Body.Instructions.Select(i=>new CodeInstruction(opcodes[i.OpCode.Value],i.Operand is Cecil.FieldReference f && f.DeclaringType.Name==owner.Name?(object)owner.GetField(f.Name):i.Operand)).ToList();
            // Guide methods need not exist in fixtures: only reviewed data fields
            // are interpreted here; unrelated operands and branches stay intact.
            for(int i=0;i<input.Count;i++)if(target.Body.Instructions[i].Operand is Cecil.FieldReference f && f.DeclaringType.Name==owner.Name && input[i].operand==null)input[i].operand=f;
            input[0].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            int injection=0;
            if(name=="Serialize")
            {
                var expected=new HashSet<string>(owner==typeof(ElectricityManager)?UtilitySaveBridge.ElectricityFields:UtilitySaveBridge.WaterFields);
                injection=input.Count(i=>i.opcode==System.Reflection.Emit.OpCodes.Ldfld && i.operand is FieldInfo f && expected.Contains(f.Name));
                var output=UtilitySerializerHooks.Serialize(input,owner.GetNestedType("Data").GetMethod(name)).ToList();
                Check(output.Count==input.Count+2*injection,"Every utility field read gets its frozen value");
                int j=0;
                foreach(var code in input)
                {
                    var actual=output[j++];Check(actual.opcode==code.opcode && Equals(actual.operand,code.operand) && actual.blocks.SequenceEqual(code.blocks),"Original utility IL retained");
                    if(code.opcode!=System.Reflection.Emit.OpCodes.Ldfld || !(code.operand is FieldInfo field) || !expected.Contains(field.Name))continue;
                    Check(output[j].opcode==System.Reflection.Emit.OpCodes.Ldstr && Equals(output[j++].operand,(owner==typeof(ElectricityManager)?"E.":"W.")+field.Name),"Frozen value lookup key");
                    var helper=(MethodInfo)output[j++].operand;Check(helper.Name=="Freeze" && helper.ReturnType==field.FieldType && helper.GetParameters()[0].ParameterType==field.FieldType,"Frozen helper stack type");
                }
            }
            else
            {
                var output=UtilitySerializerHooks.Deserialize(input,owner.GetNestedType("Data").GetMethod(name)).ToList();
                Check(output.Count==input.Count+1,"Exactly one vanilla scratch array reader injected");
            }
        }
        var hooks=UtilitySerializerHooks.Specifications();
        Check(hooks.Count==4 && hooks.Count(h=>h.Finalizer!=null)==2 && hooks.All(h=>h.Target!=null && h.Transpiler!=null),"Four utility serializer hooks with load cleanup");
        Console.WriteLine("PASS: four actual utility serializer IL bodies; all frozen array/counter field reads; generic stack types; vanilla scratch grid injections; concrete hooks and failure cleanup. Game field types represented by fixtures.");
    }
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
}
