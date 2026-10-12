extern alias ExternalCecil;
using Cecil=ExternalCecil::Mono.Cecil;
using Cil=ExternalCecil::Mono.Cecil.Cil;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;

static class ZoneGridChecks
{
    public static void Run(Cecil.ModuleDefinition game,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    {
        int queries=0,origins=0;
        foreach(var owner in game.Types)
        foreach(var method in owner.Methods.Where(m=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is Cecil.FieldReference f && f.DeclaringType.Name=="ZoneManager" && f.Name=="m_zoneGrid")))
        {
            var reviewed=plan.Methods[method.MetadataToken.ToInt32()];
            if(method.Name=="Awake"){Check(reviewed.Edits.Single().Replacement==90000,"Zone grid allocation");continue;}
            var input=method.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand)).ToList();
            var output=PlannedTranspiler.Transform(input,reviewed);
            Check(reviewed.Edits.Count(e=>e.Expected==149 && e.Replacement==299)==2 && reviewed.Edits.Count(e=>e.Expected==150 && e.Replacement==300)==1,"Zone upper bounds and row stride");
            foreach(var edit in reviewed.Edits.Where(e=>e.Kind=="float"))
            {
                Check(edit.Expected==BitConverter.SingleToInt32Bits(75f) && edit.Replacement==BitConverter.SingleToInt32Bits(135f),"Zone origin preserves old world coordinates");
                int start=edit.Index-2,end=edit.Index+3;
                Check(output[start].opcode==OpCodes.Ldc_R4 && (float)output[start].operand==64f && output[start+1].opcode==OpCodes.Div,"Zone cell size stays 64m");
                while(output[end].opcode!=OpCodes.Call)end++;
                var dynamic=new DynamicMethod("ActualZoneCoordinate",typeof(int),new[]{typeof(float)});var il=dynamic.GetILGenerator();il.Emit(OpCodes.Ldarg_0);
                foreach(var code in output.Skip(start).Take(end-start+1))
                {
                    if(code.opcode==OpCodes.Call)
                    {var call=(Cecil.MethodReference)code.operand;Check(new[]{"Min","Max","Clamp"}.Contains(call.Name),"Zone coordinate integer clamp");il.Emit(OpCodes.Call,typeof(Math).GetMethod(call.Name,Enumerable.Repeat(typeof(int),call.Parameters.Count).ToArray()));}
                    else if(code.operand==null)il.Emit(code.opcode);
                    else if(code.operand is float f)il.Emit(code.opcode,f);
                    else if(code.operand is int v)il.Emit(code.opcode,v);
                    else if(code.operand is sbyte b)il.Emit(code.opcode,b);
                    else throw new Exception("Unexpected zone coordinate operand");
                }
                il.Emit(OpCodes.Ret);var coordinate=(Func<float,int>)dynamic.CreateDelegate(typeof(Func<float,int>));
                for(int c=0;c<300;c++)Check(coordinate((c-135+0.5f)*64f)==c,"Actual query covers all 300 zone coordinates");
                for(int c=0;c<150;c++)Check(coordinate((c-75+0.5f)*64f)==c+60,"Legacy zone position unchanged");origins++;
            }
            int stride=reviewed.Edits.Single(e=>e.Expected==150).Index;
            Check(output[stride+1].opcode==OpCodes.Mul && output[stride+3].opcode==OpCodes.Add,"Zone row/column lookup shape");queries++;
        }
        var reader=game.Types.Single(t=>t.Name=="ZoneManager").NestedTypes.Single(t=>t.Name=="Data").Methods.Single(m=>m.Name=="Deserialize");
        Check(reader.Body.Instructions.Any(i=>i.OpCode==Cil.OpCodes.Ldlen) && reader.Body.Instructions.Any(i=>i.Operand is Cecil.MethodReference m && m.Name=="InitializeBlock"),"Standard zones clear dynamic grid and rebuild through expanded initialization");
        Check(queries==13 && origins==48,"All direct zone consumers connected");
        Console.WriteLine("PASS: 13 actual zone manager/building/tool query bodies; 48 coordinate slices over all 300 cells; legacy origin offset 60; 90000 allocation; dynamic standard-save grid rebuild. Full zoning and Unity not executed.");
    }
    static void Check(bool value,string name){if(!value)throw new Exception(name);}
}
