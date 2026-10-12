extern alias ExternalCecil;
using Cecil=ExternalCecil::Mono.Cecil;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;

static class ServiceSpatialChecks
{
    public static void Run(Cecil.ModuleDefinition game,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    {
        var owner=game.Types.Single(t=>t.Name=="ImmaterialResourceManager");
        foreach(var query in owner.Methods.Where(m=>m.Name=="CheckResource" || m.Name=="CheckLocalResource" && m.Parameters.Count==3))
        {
            var body=Transform(query,plan,opcodes);
            foreach(int start in new[]{2,13})
            {
                var dynamic=new DynamicMethod("ServiceCoordinate",typeof(int),new[]{typeof(float)});
                var il=dynamic.GetILGenerator();il.Emit(OpCodes.Ldarg_0);
                foreach(var code in body.Skip(start).Take(8))
                {
                    if(code.opcode==OpCodes.Call)il.Emit(OpCodes.Call,typeof(ServiceSpatialChecks).GetMethod(nameof(Clamp)));
                    else if(code.operand is float f)il.Emit(code.opcode,f);
                    else if(code.operand is int value)il.Emit(code.opcode,value);
                    else il.Emit(code.opcode);
                }
                il.Emit(OpCodes.Ret);var coordinate=(Func<float,int>)dynamic.CreateDelegate(typeof(Func<float,int>));
                for(int cell=0;cell<500;cell++)Check(coordinate((cell-225+.5f)*38.4f)==cell,"Service query covers every expanded cell");
                for(int cell=0;cell<256;cell++)Check(coordinate((cell-128+.5f)*38.4f)==cell+97,"Service legacy world origin is preserved");
                Check(coordinate(-20000)==0 && coordinate(20000)==499,"Service outer query clamps");
            }
            Check(body[23].opcode==OpCodes.Ldc_I4 && (int)body[23].operand==500,"Service query row stride expands");
            for(int z=0;z<500;z++)for(int x=0;x<500;x++)for(int channel=0;channel<35;channel++)
                Check(((z*500+x)*35+channel)<8750000,"All service channel indices are in bounds");
        }
        var awake=owner.Methods.Single(m=>m.Name=="Awake");var awakeBody=Transform(awake,plan,opcodes);
        Check(awakeBody.Count(c=>c.opcode==OpCodes.Ldc_I4 && Equals(c.operand,8750000))==2,"Both 35-channel service allocations expand");
        foreach(var pair in awakeBody.Select((code,index)=>(code,index)).Where(p=>p.code.opcode==OpCodes.Newarr && p.code.operand is Cecil.TypeReference t && t.FullName=="System.Single"))
            Check(Integer(awakeBody[pair.index-1])==256,"Angular float sector arrays remain 256 entries");
        var obstruction=owner.Methods.Single(m=>m.Name=="AddObstructedResource");var ob=Transform(obstruction,plan,opcodes);
        int oldAngles=0,oldMasks=0;
        for(int i=0;i<ob.Count-1;i++)
        {
            if(Integer(ob[i])==256 && ob[i+1].opcode!=OpCodes.Mul)oldAngles++;
            if(Integer(ob[i])==255 && ob[i+1].opcode==OpCodes.And)oldMasks++;
        }
        Check(oldAngles>0 && oldMasks>0,"Angular traversal and wrap masks retain 256 sectors");
        var obstructionPlan=plan.Methods[obstruction.MetadataToken.ToInt32()];
        Check(obstructionPlan.Edits.Count(e=>e.Expected==1081 && e.Replacement==1201)==2,"Obstruction terrain height lookup uses expanded row stride");
        var mapping=plan.Methods[owner.Methods.Single(m=>m.Name=="UpdateResourceMapping").MetadataToken.ToInt32()];
        Check(mapping.Edits.Count(e=>e.Kind=="float" && e.Replacement==BitConverter.SingleToInt32Bits(.45f))==2,"Service shader mapping preserves world origin in both axes");
        Check(mapping.Edits.Count(e=>e.Kind=="float" && e.Replacement==BitConverter.SingleToInt32Bits(1f/19200f))==1,"Service texture spans expanded width");
        var calculation=plan.Methods[owner.Methods.Single(m=>m.Name=="CalculateLocalResources").MetadataToken.ToInt32()];
        Check(!calculation.Edits.Any(e=>e.Expected==255),"Service effect saturation remains unchanged");
        Console.WriteLine("PASS: actual service query IL executed over 500 expanded / 256 legacy coordinates and all 35 channels; allocations, preserved angular sectors/masks, terrain strides and shader origin. Unity not executed.");
    }
    public static int Clamp(int value,int min,int max)=>Math.Clamp(value,min,max);
    static int? Integer(CodeInstruction c)
    {if(c.opcode==OpCodes.Ldc_I4)return (int)c.operand;if(c.opcode==OpCodes.Ldc_I4_S)return Convert.ToInt32(c.operand);return null;}
    static IList<CodeInstruction> Transform(Cecil.MethodDefinition method,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    =>PlannedTranspiler.Transform(method.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand)),plan.Methods[method.MetadataToken.ToInt32()]);
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
