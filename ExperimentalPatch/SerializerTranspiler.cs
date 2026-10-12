using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace HundredTiles.ExperimentalPatch
{
    public static class SerializerTranspiler
    {
        public static IList<CodeInstruction> TransformBuffer(IEnumerable<CodeInstruction> source, Type arrayType, MethodInfo helper)
        {
            if (arrayType == null || !arrayType.IsArray || helper == null || !helper.IsStatic || helper.ReturnType != arrayType ||
                helper.GetParameters().Length != 1 || helper.GetParameters()[0].ParameterType != arrayType)
                throw new ArgumentException("Object buffer helper signature differs.");
            var output = new List<CodeInstruction>(); bool found = false;
            foreach (var input in source)
            {
                var code = new CodeInstruction(input); output.Add(code);
                var field = code.operand as FieldInfo;
                if (code.opcode != OpCodes.Ldfld || field == null || field.Name != "m_buffer" || field.FieldType != arrayType) continue;
                if (found) throw new InvalidOperationException("Object serializer buffer read is not unique.");
                found = true; var call = new CodeInstruction(OpCodes.Call, helper);
                for (int i = code.blocks.Count - 1; i >= 0; i--)
                    if (code.blocks[i].blockType == ExceptionBlockType.EndExceptionBlock)
                    { call.blocks.Insert(0, code.blocks[i]); code.blocks.RemoveAt(i); }
                output.Add(call);
            }
            if (!found) throw new InvalidOperationException("Object serializer buffer missing.");
            return output;
        }

        // Inject immediately after the reviewed field load or BeginRead call.
        // Helper signatures consume and return the same value type, so the stack
        // height and the original serializer's lock/finally flow are preserved.
        public static IList<CodeInstruction> Transform(IEnumerable<CodeInstruction> source, IDictionary<string,MethodInfo> helpers)
        {
            var output=new List<CodeInstruction>();
            var used=new HashSet<string>();
            var inputs=new List<CodeInstruction>(source);
            for(int index=0;index<inputs.Count;index++)
            {
                var input=inputs[index];
                var instruction=new CodeInstruction(input);
                output.Add(instruction);
                string key=null;
                var field=instruction.operand as FieldInfo;
                if(instruction.opcode == OpCodes.Ldfld && field!=null) key=field.Name;
                var method=instruction.operand as MethodInfo;
                if((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && method!=null) key=method.Name;
                if(key!=null && helpers.ContainsKey(key+":last"))
                {
                    bool later=false;
                    for(int next=index+1;next<inputs.Count;next++)
                    {
                        var laterField=inputs[next].operand as FieldInfo;
                        if(inputs[next].opcode==OpCodes.Ldfld && laterField!=null && laterField.Name==key) {later=true;break;}
                    }
                    key=later ? null : key+":last";
                }
                MethodInfo helper;
                if(key!=null && helpers.TryGetValue(key,out helper))
                {
                    Type type=field!=null ? field.FieldType : method.ReturnType;
                    var parameters=helper.GetParameters();
                    if(!helper.IsStatic || parameters.Length!=1 || parameters[0].ParameterType!=type || helper.ReturnType!=type)
                        throw new InvalidOperationException("Projection helper signature mismatch: "+key);
                    if(!used.Add(key)) throw new InvalidOperationException("Serializer access is no longer unique: "+key);
                    var call=new CodeInstruction(OpCodes.Call,helper);
                    // A block end on the field load must include the helper in
                    // the same protected region. Other markers stay on the load.
                    for(int i=instruction.blocks.Count-1;i>=0;i--)
                        if(instruction.blocks[i].blockType == ExceptionBlockType.EndExceptionBlock)
                        { call.blocks.Insert(0,instruction.blocks[i]); instruction.blocks.RemoveAt(i); }
                    output.Add(call);
                }
            }
            if(used.Count!=helpers.Count) throw new InvalidOperationException("Required serializer access missing.");
            return output;
        }
    }
}
