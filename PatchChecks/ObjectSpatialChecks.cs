extern alias ExternalCecil;
using Cecil=ExternalCecil::Mono.Cecil;
using HarmonyLib;
using System.Reflection.Emit;
using HundredTiles.ExperimentalPatch;

static class ObjectSpatialChecks
{
    public static void Run(Cecil.ModuleDefinition game,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    {
        int queries=0;
        foreach(var type in game.Types)
        foreach(var method in type.Methods.Where(m=>m.HasBody))
        foreach(var owner in new[]{"TreeManager","PropManager"})
        {
            string field=owner=="TreeManager"?"m_treeGrid":"m_propGrid";
            if(!method.Body.Instructions.Any(i=>i.Operand is Cecil.FieldReference f && f.DeclaringType.Name==owner && f.Name==field))continue;
            if(new[]{"InitializeTree","FinalizeTree","InitializeProp","FinalizeProp","Deserialize"}.Contains(method.Name))continue;
            if(!plan.Methods.TryGetValue(method.MetadataToken.ToInt32(),out var reviewed))throw new Exception("Unconnected object consumer: "+method.FullName);
            var output=PlannedTranspiler.Transform(method.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand)),reviewed);
            int width=owner=="TreeManager"?540:270,next=width/9*10;
            foreach(var edit in reviewed.Edits.Where(e=>e.Kind=="int" && (e.Expected==width || e.Expected==width-1)))
            {
                if(edit.Expected==width-1)
                {
                    var call=method.Body.Instructions[edit.Index+1].Operand as Cecil.MethodReference;
                    if(call==null || call.DeclaringType.Name!="Mathf" || !new[]{"Clamp","Min","Max"}.Contains(call.Name))throw new Exception("Object bound is not a spatial clamp: "+method.FullName);
                }
                else if(output[edit.Index+1].opcode!=OpCodes.Mul && output[edit.Index+1].opcode!=OpCodes.Div && !(new[]{"ApplyAntiTreeBrush","ApplyBrush"}.Contains(method.Name) && (output[edit.Index+1].opcode==OpCodes.Stloc_2 || output[edit.Index+1].opcode==OpCodes.Stloc_3)))throw new Exception("Object dimension is not a spatial multiply/divide: "+method.FullName);
                if(edit.Replacement!=(edit.Expected==width?next:next-1))throw new Exception("Wrong object bound/stride.");
            }
            queries++;
        }
        foreach(string owner in new[]{"TreeManager","PropManager"})
        foreach(string method in new[]{"EndRenderingImpl","CalculateGroupData","PopulateGroupData"})
        {
            var body=game.Types.Single(t=>t.Name==owner).Methods.Single(m=>m.Name==method);
            var edits=plan.Methods[body.MetadataToken.ToInt32()].Edits;
            if(edits.Count(e=>e.Expected==45 && e.Replacement==50)!=4)throw new Exception("All object render group boundaries must expand.");
            int width=owner=="TreeManager"?600:300;
            var covered=new bool[width];
            for(int group=0;group<50;group++)for(int cell=group*width/50;cell<(group+1)*width/50;cell++)
            {if(covered[cell])throw new Exception("Overlapping object render groups.");covered[cell]=true;}
            if(covered.Any(c=>!c))throw new Exception("Object render groups omit expanded edge.");
        }
        foreach(string name in new[]{"TreeTool.ApplyBrush","PropTool.ApplyBrush","DistrictTool.ApplyAntiTreeBrush"})
        {
            var reviewed=plan.Methods.Values.Single(m=>m.Name==name);
            var method=(Cecil.MethodDefinition)game.LookupToken(reviewed.Token);
            var output=PlannedTranspiler.Transform(method.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand)),reviewed);
            int expected=name.StartsWith("PropTool")?135:270;
            foreach(var edit in reviewed.Edits.Where(e=>e.Kind=="local"))
            {
                var dynamic=new DynamicMethod("ObjectBrushOrigin",typeof(float),Type.EmptyTypes);var il=dynamic.GetILGenerator();
                foreach(var code in output.Skip(edit.Index).Take(4))
                {if(code.operand is int value)il.Emit(code.opcode,value);else if(code.operand is float f)il.Emit(code.opcode,f);else il.Emit(code.opcode);}
                il.Emit(OpCodes.Ret);
                if(((Func<float>)dynamic.CreateDelegate(typeof(Func<float>)))()!=expected)throw new Exception("Expanded object brush changes world origin.");
            }
        }
        var edge=game.Types.Single(t=>t.Name=="TerrainManager").Methods.Single(m=>m.Name=="RayCastEdge");
        var edgePlan=plan.Methods[edge.MetadataToken.ToInt32()];
        if(edgePlan.Edits.Any(e=>e.Kind!="int") || edgePlan.Edits.Single(e=>e.Expected==1168560).Replacement!=1201*1201-1 || edgePlan.Edits.Count(e=>e.Expected==1167480 && e.Replacement==1200*1201)!=3)throw new Exception("Terrain outer edge dimensions/origin differ.");
        Console.WriteLine("PASS: "+queries+" direct object-grid consumer/manager bodies connected; spatial-bound contexts; six render-group partitions cover all cells; terrain outer edge corners/last row expand with unchanged world origin. No Unity rendering.");
    }
}
