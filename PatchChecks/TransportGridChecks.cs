extern alias ExternalCecil;
using Cecil=ExternalCecil::Mono.Cecil;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;

static class TransportGridChecks
{
    public static void Run(Cecil.ModuleDefinition game,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    {
        int bodies=0,bounds=0,strides=0;
        var grids=new Dictionary<string,string[]>{["VehicleManager"]=new[]{"m_vehicleGrid","m_vehicleGrid2","m_parkedGrid"},["CitizenManager"]=new[]{"m_citizenGrid"}};
        var visited=new HashSet<int>();
        foreach(var type in game.Types)foreach(var method in type.Methods.Where(m=>m.HasBody))
        {
            bool direct=method.Body.Instructions.Any(i=>i.Operand is Cecil.FieldReference f && grids.ContainsKey(f.DeclaringType.Name) && grids[f.DeclaringType.Name].Contains(f.Name));
            bool adapter=method.Body.Instructions.Any(i=>i.Operand is Cecil.MethodReference m && grids.ContainsKey(m.DeclaringType.Name) && (m.Name=="AddToGrid" || m.Name=="RemoveFromGrid")) &&
                method.Body.Instructions.Any(i=>i.Operand is int v && new[]{539,53,2159}.Contains(v) || i.Operand is sbyte b && b==53);
            if(!direct && !adapter)continue;
            Check(plan.Methods.ContainsKey(method.MetadataToken.ToInt32()),"Every direct vehicle/citizen consumer has a reviewed plan");visited.Add(method.MetadataToken.ToInt32());
        }
        // Include adapters without a direct array read: these derive gridX/Z
        // before calling the insertion/removal overload with a direct read.
        foreach(var item in plan.Methods.Values)
        {
            if(!visited.Contains(item.Token) && !item.Name.StartsWith("VehicleManager.AddToGrid") && !item.Name.StartsWith("VehicleManager.RemoveFromGrid") && !item.Name.StartsWith("CitizenManager.AddToGrid") && !item.Name.StartsWith("CitizenManager.RemoveFromGrid"))continue;
            var method=(Cecil.MethodDefinition)game.LookupToken(item.Token);
            var input=method.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand)).ToList();
            var output=PlannedTranspiler.Transform(input,item);
            foreach(var edit in item.Edits.Where(e=>e.Kind=="int" && new[]{539,53,2159}.Contains(e.Expected)))
            {
                int side=edit.Expected==539?600:edit.Expected==53?60:2400;
                Check(edit.Replacement==side-1,"Transport upper grid bound");
                var call=(Cecil.MethodReference)output[edit.Index+1].operand;
                var dynamic=new DynamicMethod("ActualTransportBound",typeof(int),new[]{typeof(int)});var il=dynamic.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);if(call.Name=="Clamp")il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(output[edit.Index].opcode,(int)output[edit.Index].operand);
                il.Emit(OpCodes.Call,typeof(Math).GetMethod(call.Name,Enumerable.Repeat(typeof(int),call.Parameters.Count).ToArray()));il.Emit(OpCodes.Ret);
                var bound=(Func<int,int>)dynamic.CreateDelegate(typeof(Func<int,int>));
                for(int c=0;c<side;c++)Check(bound(c)==c,"Every added transport coordinate passes actual upper clamp");
                Check(bound(side+50)==side-1,"Transport bound clamps beyond added edge");bounds++;
            }
            foreach(var edit in item.Edits.Where(e=>e.Kind=="int" && new[]{540,54,2160}.Contains(e.Expected)))
            {
                int side=edit.Expected==540?600:edit.Expected==54?60:2400;
                Check(edit.Replacement==side && output[edit.Index+1].opcode==OpCodes.Mul,"Transport row stride");
                var dynamic=new DynamicMethod("ActualTransportStride",typeof(int),new[]{typeof(int),typeof(int)});var il=dynamic.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);il.Emit(output[edit.Index].opcode,(int)output[edit.Index].operand);il.Emit(output[edit.Index+1].opcode);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Add);il.Emit(OpCodes.Ret);
                var index=(Func<int,int,int>)dynamic.CreateDelegate(typeof(Func<int,int,int>));
                for(int z=0;z<side;z++)for(int x=0;x<side;x++)Check(index(z,x)==z*side+x,"Transport row/column lookup addresses full expanded grid");strides++;
            }
            foreach(var origin in input.Where(i=>i.opcode==OpCodes.Ldc_R4 && i.operand is float f && new[]{270f,27f,1080f}.Contains(f)))
                Check(output[input.IndexOf(origin)].opcode==origin.opcode && Equals(output[input.IndexOf(origin)].operand,origin.operand),"Transport world origins stay unchanged");
            bodies++;
        }
        foreach(string owner in grids.Keys)
        {
            var awake=plan.Methods.Values.Single(m=>m.Name==owner+".Awake");
            Check(owner=="VehicleManager"?awake.Edits.Count(e=>e.Expected==291600 && e.Replacement==360000)==2 && awake.Edits.Count(e=>e.Expected==2916 && e.Replacement==3600)==1:
                awake.Edits.Single().Expected==4665600 && awake.Edits.Single().Replacement==5760000,"Transport grid allocations match all query strides");
            var data=game.Types.Single(t=>t.Name==owner).NestedTypes.Single(t=>t.Name=="Data");
            Check(data.Methods.Single(m=>m.Name=="Deserialize").Body.Instructions.Any(i=>i.OpCode.Code==ExternalCecil::Mono.Cecil.Cil.Code.Ldlen),"Transport decoder clears dynamic grid sizes");
            if(owner=="VehicleManager")Check(data.Methods.Single(m=>m.Name=="AfterDeserialize").Body.Instructions.Any(i=>i.Operand is Cecil.MethodReference m && m.Name=="AddToGrid"),"Vehicle decoder rebuilds through patched insertion adapters");
            else
            {
                Check(data.Methods.Single(m=>m.Name=="Deserialize").Body.Instructions.Any(i=>i.Operand is Cecil.MethodReference m && m.Name=="InitializeInstance"),"Citizen decoder rebuilds instances");
                Check(game.Types.Single(t=>t.Name==owner).Methods.Single(m=>m.Name=="InitializeInstance").Body.Instructions.Any(i=>i.Operand is Cecil.MethodReference m && m.Name=="AddToGrid"),"Citizen initialization uses patched insertion adapter");
            }
            var ray=(Cecil.MethodDefinition)game.LookupToken(plan.Methods.Values.Single(m=>m.Name==owner+".RayCast").Token);
            var edits=plan.Methods[ray.MetadataToken.ToInt32()].Edits.Where(e=>e.Kind=="float").ToArray();
            Check(edits.Count(e=>e.Replacement==BitConverter.SingleToInt32Bits(960f))==2 && edits.Count(e=>e.Replacement==BitConverter.SingleToInt32Bits(19200f))==2,"Transport raycast clips expanded asymmetric map");
        }
        Check(bodies==67 && bounds==152 && strides==70,$"Reviewed transport consumer/adapter coverage {bodies}/{bounds}/{strides}");
        Console.WriteLine($"PASS: {bodies} transport grid bodies; {bounds} actual upper clamps; {strides} actual strides over all 600/60/2400 grid cells; original world origins; raycast bounds; standard-save dynamic grid rebuild. Full traffic, pathfinding and Unity not executed.");
    }
    static void Check(bool value,string name){if(!value)throw new Exception(name+" (transport)");}
}
