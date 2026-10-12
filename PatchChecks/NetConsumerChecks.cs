extern alias ExternalCecil;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using ExternalCecil::Mono.Cecil;
using VariableDefinition=ExternalCecil::Mono.Cecil.Cil.VariableDefinition;
using Codes=System.Reflection.Emit.OpCodes;

public static class NetConsumerChecks
{
    public static void Run(ModuleDefinition module,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    { RunConsumers(module,plan,opcodes,"NetManager",40);RunConsumers(module,plan,opcodes,"BuildingManager",31); }
    static void RunConsumers(ModuleDefinition module,PatchPlan plan,Dictionary<ushort,OpCode> opcodes,string owner,int expectedCount)
    {
        int consumers=0;
        var grid=Enumerable.Range(0,90000).Select(i=>(ushort)(i%65535+1)).ToArray();
        foreach(var type in module.Types.Where(t=>t.Name!=owner))
        foreach(var method in type.Methods.Where(m=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name==owner && (owner=="BuildingManager"?f.Name=="m_buildingGrid":f.Name=="m_nodeGrid" || f.Name=="m_segmentGrid"))))
        {
            var reviewed=plan.Methods.Values.Single(m=>m.Token==method.MetadataToken.ToInt32());
            int expectedClamps=owner=="BuildingManager" && type.Name.StartsWith("Structure")?4:2;
            int additional=owner=="NetManager" && type.Name=="WaterManager" && method.Name=="UpdateGrid"?19:0;
            if(type.Name=="DistrictManager")
            {
                var districtEdits=reviewed.Edits.Where(e=>e.Kind=="int" && e.Expected==511 && e.Replacement==999 || e.Kind=="float" && e.Expected==BitConverter.SingleToInt32Bits(256f) && e.Replacement==BitConverter.SingleToInt32Bits(450f)).ToArray();
                Require(districtEdits.Count(e=>e.Kind=="int")==2 && districtEdits.Count(e=>e.Kind=="float")==6,"Park transfer uses reviewed district bounds and world origin");
                additional=districtEdits.Length;
            }
            if(type.Name=="CommonBuildingAI" || type.Name=="WeatherManager" || type.Name=="TreeManager")
                additional+=reviewed.Edits.Count(e=>e.Kind=="int" && (e.Expected==539 && e.Replacement==599 || e.Expected==540 && e.Replacement==600));
            Require(reviewed.Edits.Count==expectedClamps+1+additional && reviewed.Edits.Count(e=>e.Expected==269 && e.Replacement==299)==expectedClamps && reviewed.Edits.Single(e=>e.Expected==270).Replacement==300,"Reviewed road clamps/stride and separately reviewed utility edits");
            if(additional!=0 && type.Name=="WaterManager")
                Require(reviewed.Edits.Count(e=>e.Kind=="int" && e.Expected==255 && e.Replacement==507)==2 &&
                    reviewed.Edits.Count(e=>e.Kind=="int" && e.Expected==256 && e.Replacement==508)==3 &&
                    reviewed.Edits.Count(e=>e.Kind=="float" && e.Expected==BitConverter.SingleToInt32Bits(128f) && e.Replacement==BitConverter.SingleToInt32Bits(231f))==14,"Exact WaterManager utility edits");
            var input=method.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand is VariableDefinition v?v.Index:i.Operand)).ToList();
            var output=PlannedTranspiler.Transform(input,reviewed);
            foreach(var edit in reviewed.Edits.Where(e=>e.Expected==269).Take(2))
            {
                // Run the original arithmetic with the reviewed new upper bound.
                var dm=new DynamicMethod("ConsumerClamp",typeof(int),new[]{typeof(float)});
                var il=dm.GetILGenerator();il.Emit(Codes.Ldarg_0);
                Require(output[edit.Index-5].opcode==Codes.Ldc_R4 && (float)output[edit.Index-5].operand==64f && output[edit.Index-3].opcode==Codes.Ldc_R4 && (float)output[edit.Index-3].operand==135f,"Original cell size and origin");
                foreach(var code in output.Skip(edit.Index-5).Take(7))
                {
                    if(code.opcode==Codes.Call)
                    {
                        var call=(MethodReference)code.operand;Require(call.Name=="Min" && call.DeclaringType.FullName=="UnityEngine.Mathf","Integer upper clamp");
                        il.Emit(code.opcode,typeof(Math).GetMethod("Min",new[]{typeof(int),typeof(int)}));
                    }
                    else if(code.operand==null)il.Emit(code.opcode);
                    else if(code.operand is float value)il.Emit(code.opcode,value);
                    else il.Emit(code.opcode,Convert.ToInt32(code.operand));
                }
                il.Emit(Codes.Ret);var clamp=(Func<float,int>)dm.CreateDelegate(typeof(Func<float,int>));
                for(int cell=0;cell<300;cell++)Require(clamp((cell-135+0.5f)*64)==cell,"Consumer covers old and added cells");
                Require(clamp(8640f)==270 && clamp(10560f)==299 && clamp(11000f)==299,"Consumer upper boundary");
            }
            foreach(var edit in reviewed.Edits.Where(e=>e.Expected==269).Skip(2))
            {
                var ring=new DynamicMethod("SearchRing",typeof(int),new[]{typeof(int)});var ringIL=ring.GetILGenerator();ringIL.Emit(Codes.Ldarg_0);
                foreach(var code in output.Skip(edit.Index-2).Take(4))
                {
                    if(code.opcode==Codes.Call)ringIL.Emit(code.opcode,typeof(Math).GetMethod("Min",new[]{typeof(int),typeof(int)}));
                    else if(code.operand==null)ringIL.Emit(code.opcode);else ringIL.Emit(code.opcode,Convert.ToInt32(code.operand));
                }
                ringIL.Emit(Codes.Ret);var advance=(Func<int,int>)ring.CreateDelegate(typeof(Func<int,int>));
                for(int cell=0;cell<300;cell++)Require(advance(cell)==Math.Min(cell+1,299),"Disaster search ring reaches the new outer cells");
            }
            int index=reviewed.Edits.Single(e=>e.Expected==270).Index;
            Require(IsLocal(input[index-1]) && IsLocal(input[index+2]) && output[index+1].opcode==Codes.Mul && output[index+3].opcode==Codes.Add && output[index+4].opcode==Codes.Ldelem_U2,"Original row/column array lookup shape");
            var lookupMethod=new DynamicMethod("ConsumerLookup",typeof(ushort),new[]{typeof(ushort[]),typeof(int),typeof(int)});
            var generator=lookupMethod.GetILGenerator();
            // Replace only the slice's local inputs with test arguments.
            generator.Emit(Codes.Ldarg_0);generator.Emit(Codes.Ldarg_1);
            generator.Emit(output[index].opcode,(int)output[index].operand);generator.Emit(output[index+1].opcode);
            generator.Emit(Codes.Ldarg_2);generator.Emit(output[index+3].opcode);generator.Emit(output[index+4].opcode);generator.Emit(Codes.Ret);
            var lookup=(Func<ushort[],int,int,ushort>)lookupMethod.CreateDelegate(typeof(Func<ushort[],int,int,ushort>));
            for(int z=0;z<300;z++)for(int x=0;x<300;x++)Require(lookup(grid,z,x)==grid[z*300+x],"Caller reads the same row stride as NetManager");
            consumers++;
        }
        Require(consumers==expectedCount,"All direct external spatial consumers reviewed: "+owner);
        Console.WriteLine($"PASS: {consumers} external {owner} consumers; actual upper-clamp IL slices over all 300 coordinates and array lookups over all 90000 cells; disaster ring clamps. Complete AI/tool/service/path routines were not executed.");
    }
    static bool IsLocal(CodeInstruction c)=>c.opcode==Codes.Ldloc || c.opcode==Codes.Ldloc_S || c.opcode==Codes.Ldloc_0 || c.opcode==Codes.Ldloc_1 || c.opcode==Codes.Ldloc_2 || c.opcode==Codes.Ldloc_3;
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
