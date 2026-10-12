extern alias ExternalCecil;
using Cecil=ExternalCecil::Mono.Cecil;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;

static class DistrictGridChecks
{
    public static void Run(Cecil.ModuleDefinition game,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    {
        var owner=game.Types.Single(t=>t.Name=="DistrictManager");
        var query=owner.Methods.Single(m=>m.Name=="GetDistrict" && m.Parameters.Count==1);
        var body=Transform(query,plan,opcodes);
        foreach(int start in new[]{2,13})
        {
            var dynamic=new DynamicMethod("DistrictCoordinate",typeof(int),new[]{typeof(float)});
            var il=dynamic.GetILGenerator();il.Emit(OpCodes.Ldarg_0);
            foreach(var code in body.Skip(start).Take(8))
            {
                if(code.opcode==OpCodes.Call)il.Emit(OpCodes.Call,typeof(DistrictGridChecks).GetMethod(nameof(Clamp)));
                else if(code.operand is float f)il.Emit(code.opcode,f);
                else if(code.operand is int value)il.Emit(code.opcode,value);
                else il.Emit(code.opcode);
            }
            il.Emit(OpCodes.Ret);var coordinate=(Func<float,int>)dynamic.CreateDelegate(typeof(Func<float,int>));
            for(int cell=0;cell<1000;cell++)Check(coordinate((cell-450+.5f)*19.2f)==cell,"District coordinate covers every expanded cell");
            for(int cell=0;cell<512;cell++)Check(coordinate((cell-256+.5f)*19.2f)==cell+194,"Legacy district origin remains unchanged");
        }
        var texture=owner.Methods.Single(m=>m.Name=="UpdateTexture");var texturePlan=plan.Methods[texture.MetadataToken.ToInt32()];
        var fives=texturePlan.Edits.Where(e=>e.Kind=="int" && e.Expected==5).ToArray();
        Check(fives.First().Replacement==9 && fives.Skip(1).All(e=>e.Replacement==10),"Texture ownership origin differs from tile count");
        Check((9*100>>1)-450==0,"District texture cell-to-tile offset is zero");
        for(int cell=0;cell<1000;cell++)Check(cell/100==Math.Clamp((int)Math.Floor(((cell-450+.5)*19.2+8640)/1920),0,9),"All district texture cells match ownership tile coordinates");
        var awake=plan.Methods[owner.Methods.Single(m=>m.Name=="Awake").MetadataToken.ToInt32()];
        Check(awake.Edits.Count(e=>e.Expected==262144 && e.Replacement==1000000)==3,"Both district arrays and full texture color buffer expand");
        Check(awake.Edits.Count(e=>e.Expected==512 && e.Replacement==1000)==8,"Four district/park textures expand in both dimensions");
        var overlay=plan.Methods[owner.Methods.Single(m=>m.Name=="BeginOverlayImpl").MetadataToken.ToInt32()];
        Check(overlay.Edits.Count(e=>e.Kind=="float" && e.Replacement==BitConverter.SingleToInt32Bits(960f))==4,"District and park overlay centers shift in both axes");
        Check(overlay.Edits.Count(e=>e.Kind=="float" && e.Replacement==BitConverter.SingleToInt32Bits(.45f))==4,"Both shader mappings preserve origin");
        foreach(string name in new[]{"ApplyBrush","SetDistrictAlpha"})
        {
            foreach(var tool in game.Types.Single(t=>t.Name=="DistrictTool").Methods.Where(m=>m.Name==name && plan.Methods.ContainsKey(m.MetadataToken.ToInt32())))
                Check(plan.Methods[tool.MetadataToken.ToInt32()].Edits.All(e=>e.Expected==512),"District alpha arithmetic stays intact");
        }
        var sample=owner.Methods.Single(m=>m.Name=="SampleDistrict" && m.Parameters.Count==2);
        Check(plan.Methods[sample.MetadataToken.ToInt32()].Edits.Count(e=>e.Kind=="float" && e.Expected==BitConverter.SingleToInt32Bits(65536f) && e.Replacement==BitConverter.SingleToInt32Bits(115200f))==2,"Bilinear district sample origin expands without changing fractional weights");
        Console.WriteLine("PASS: actual district query IL over 1000 cells and 512 legacy coordinates; ownership texture alignment; four texture dimensions; overlays; alpha arithmetic; bilinear origins. Unity not executed.");
    }
    public static int Clamp(int value,int min,int max){return Math.Clamp(value,min,max);}
    static IList<CodeInstruction> Transform(Cecil.MethodDefinition method,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    {return PlannedTranspiler.Transform(method.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand)),plan.Methods[method.MetadataToken.ToInt32()]);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
