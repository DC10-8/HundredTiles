extern alias ExternalCecil;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using HundredTiles.Core;
using ExternalCecil::Mono.Cecil;
using ExternalCecil::Mono.Cecil.Cil;
using EmitOpCode = System.Reflection.Emit.OpCode;
using EmitOpCodes = System.Reflection.Emit.OpCodes;

if (args.Length != 1) throw new ArgumentException("Usage: <reviewed Assembly-CSharp.dll>");
var plan = PatchPlan.Embedded();
Check(plan.CanActivate,"Reviewed city runtime catalog authorizes activation");
using(var embedded=typeof(PatchPlan).Assembly.GetManifestResourceStream("HundredTiles.ExperimentalPatch.ReviewedPlan.tsv"))
using(var catalog=new StreamReader(embedded!))
using(var incomplete=new StringReader(catalog.ReadToEnd().Replace("coverage\tcity-runtime","coverage\tincomplete")))
    Check(!PatchPlan.Load(incomplete).CanActivate,"Incomplete catalog must not authorize activation");
Check(PlannedTranspiler.Hash(File.ReadAllBytes(args[0])) == plan.AssemblyHash,"Reviewed assembly hash");
using var module = ExternalCecil::Mono.Cecil.ModuleDefinition.ReadModule(args[0]);
ObjectGridChecks.Run(module);
SaveLifecycleChecks.Run(module);
UtilityWiringChecks.Run(module, plan);
using var file = File.OpenRead(args[0]);
using var pe = new PEReader(file);
var metadata = pe.GetMetadataReader();
var opcodes = typeof(EmitOpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(EmitOpCode)).Select(f => (EmitOpCode)f.GetValue(null))
    .ToDictionary(op => unchecked((ushort)op.Value));
ServiceSpatialChecks.Run(module,plan,opcodes);
int editsChecked = 0;
foreach (var item in plan.Methods.Values)
{
    var method = (ExternalCecil::Mono.Cecil.MethodDefinition)module.LookupToken(item.Token);
    var definition = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(item.Token & 0xffffff));
    Check(PlannedTranspiler.Hash(pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes()) == item.BodyHash,"Original body hash");
    var generator = new DynamicMethod("Labels",typeof(void),Type.EmptyTypes).GetILGenerator();
    var labels = method.Body.Instructions.ToDictionary(i => i, _ => generator.DefineLabel());
    var input = method.Body.Instructions.Select(i => {
        object operand = i.Operand;
        if (operand is VariableDefinition variable) operand = variable.Index;
        else if (operand is Instruction target) operand = labels[target];
        else if (operand is Instruction[] targets) operand = targets.Select(t => labels[t]).ToArray();
        var code = new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],operand);
        code.labels.Add(labels[i]);
        return code;
    }).ToList();
    // Verify preservation of Harmony exception markers as well as branch labels.
    input[0].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
    var output = PlannedTranspiler.Transform(input,item);
    Check(output.Count == input.Count,"Instruction count retained");
    var editedIndices = item.Edits.Select(e => e.Index).ToHashSet();
    for (int i=0;i<input.Count;i++)
    {
        Check(!ReferenceEquals(input[i],output[i]),"Input instruction never mutated");
        Check(input[i].labels.SequenceEqual(output[i].labels),"All labels retained");
        Check(input[i].blocks.SequenceEqual(output[i].blocks),"All exception markers retained");
        if (!editedIndices.Contains(i))
            Check(input[i].opcode == output[i].opcode && Equals(input[i].operand,output[i].operand),"Unplanned instruction retained");
    }
    foreach (var edit in item.Edits)
    {
        Check(edit.Kind=="float"
            ? output[edit.Index].opcode==EmitOpCodes.Ldc_R4 && BitConverter.SingleToInt32Bits((float)output[edit.Index].operand)==edit.Replacement
            : output[edit.Index].opcode == EmitOpCodes.Ldc_I4 && (int)output[edit.Index].operand == edit.Replacement,"Planned replacement");
        Check(!Equals(input[edit.Index].operand,output[edit.Index].operand) || input[edit.Index].opcode!=output[edit.Index].opcode,"Original operand retained");
        editsChecked++;
    }
    // Every reviewed method rejects an unexpected input before transformation.
    int wrongIndex = item.Edits[0].Index;
    var wrong = input.Select(i => new CodeInstruction(i)).ToList();
    wrong[wrongIndex].opcode = EmitOpCodes.Nop;
    wrong[wrongIndex].operand = null;
    Reject(() => PlannedTranspiler.Transform(wrong,item),"Changed method instruction rejected");
    Reject(() => PlannedTranspiler.Transform(input.Take(input.Count-1),item),"Truncated body rejected");
}
Check(plan.Methods.Count == 405 && editsChecked == 1909,"Reviewed catalog size");
var waterRefresh=plan.Methods.Values.Single(p=>p.Name=="TerrainPatch.Refresh");
Check(waterRefresh.Edits.Count(e=>e.Kind=="int" && e.Expected==9 && e.Replacement==10)==8,"Both water frames and all surface maps use ten-column patch addressing");
var coroutine=module.Types.Single(t=>t.Name=="TerrainManager").NestedTypes.Single(t=>t.Name=="<UpdateTerrainPatch>c__Iterator0").Methods.Single(m=>m.Name=="MoveNext");
var coroutineBody=PlannedTranspiler.Transform(coroutine.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand)),plan.Methods[coroutine.MetadataToken.ToInt32()]);
foreach(var slice in new[]{(Index:16,Operation:EmitOpCodes.Rem),(Index:22,Operation:EmitOpCodes.Div)})
{
    var calculation=new DynamicMethod("TerrainCoroutineCoordinate",typeof(int),new[]{typeof(int)});var coroutineIL=calculation.GetILGenerator();coroutineIL.Emit(EmitOpCodes.Ldarg_0);
    Check(coroutineBody[slice.Index].opcode==EmitOpCodes.Ldc_I4 && (int)coroutineBody[slice.Index].operand==10 && coroutineBody[slice.Index+1].opcode==slice.Operation,"Terrain coroutine retains reviewed modulo/division");
    coroutineIL.Emit(coroutineBody[slice.Index].opcode,(int)coroutineBody[slice.Index].operand);coroutineIL.Emit(coroutineBody[slice.Index+1].opcode);coroutineIL.Emit(EmitOpCodes.Ret);
    var coordinate=(Func<int,int>)calculation.CreateDelegate(typeof(Func<int,int>));
    for(int patch=0;patch<100;patch++)Check(coordinate(patch)==(slice.Operation==EmitOpCodes.Rem?patch%10:patch/10),"Terrain coroutine addresses all 100 water patches");
}
Console.WriteLine("PASS: actual deferred terrain coroutine coordinate IL executed for all 100 patches; water patch indices remain 0..99, including the added row and column.");
NetSpatialChecks.Run(module,plan,opcodes);
NetConsumerChecks.Run(module,plan,opcodes);
ZoneGridChecks.Run(module,plan,opcodes);
DistrictGridChecks.Run(module,plan,opcodes);
ObjectSpatialChecks.Run(module,plan,opcodes);
TransportGridChecks.Run(module,plan,opcodes);
RenderSpatialChecks.Run(module,plan,opcodes);
Check(plan.Methods.Values.Single(m=>m.Name=="NaturalResourceManager.Awake").Edits.Count(e=>e.Kind=="float")==3,"Natural-resource shader mapping retains the original world origin");
Check(plan.Methods.Values.Single(m=>m.Name=="NaturalResourceManager.SimulationStepImpl").Edits.Single().Replacement==268435455,"Editor batches do not restart the odd-width resource cycle at 4096 calls");
var raycast=plan.Methods.Values.Single(m=>m.Name=="TerrainManager.RayCast");
Check(raycast.Edits.Where(e=>e.Kind=="float").Select(e=>e.Index).Order().SequenceEqual(new[]{13,15,17,19}),"Only reviewed clipping-box float positions change");
Check(raycast.Edits.Single(e=>e.Index==13).Replacement==BitConverter.SingleToInt32Bits(960f) &&
    raycast.Edits.Single(e=>e.Index==17).Replacement==BitConverter.SingleToInt32Bits(19200f),"Raycast clips the asymmetric world without shifting the legacy origin");
Check(plan.Methods.Values.Count(m=>m.Name=="GameAreaManager.PointOutOfArea")==2,"Both point/radius ownership boundary overloads connected");
Check(!plan.Methods.Values.Any(m=>m.Name=="NaturalResourceManager.SimulationStepImpl2"),"Resource schedule has one dedicated transpiler, with no overlapping scalar plan");
foreach(string name in new[]{"CalculateForestProximity","CountResource"})
    Check(plan.Methods.Values.Single(m=>m.Name=="NaturalResourceManager."+name).Edits.Count(e=>e.Kind=="local" && e.Replacement==512)==6,"All resource origins retain 256 cells: "+name);
Check(plan.Methods.Values.Single(m=>m.Name=="NetManager.Awake").Edits.Single(e=>e.Expected==1050).Replacement==4200,"100 tiles times 42 service slots");
var overlay=plan.Methods.Values.Single(m=>m.Name=="GameAreaManager.BeginOverlayImpl");
Check(overlay.Edits.Count(e=>e.Kind=="float" && e.Expected==BitConverter.SingleToInt32Bits(0.4375f))==4,"Both texture mappings use 5.5/16 offset");
Check(overlay.Edits.Count(e=>e.Kind=="float" && e.Expected==BitConverter.SingleToInt32Bits(0.125f))==2,"Both texture mappings use 1/16 texel size");
var unlockArea=plan.Methods.Values.Single(m=>m.Name=="GameAreaManager.UnlockArea");
Check(unlockArea.Edits.Single(e=>e.Index==107).Replacement==0,"Detail patch offset removed without changing achievement enums");
Check(!unlockArea.Edits.Any(e=>e.Expected==9),"Nine-tile achievement retained");
Check(unlockArea.Edits.Count(e=>e.Kind=="float")==4,"All purchase quad coordinates use original world origin");
var terrainEdit = plan.Methods.Values.Single(m => m.Name == "TerrainModify.UpdateAreaImplementation");
Check(terrainEdit.Edits.Where(e => e.Expected == 8).Select(e => e.Index).Order().SequenceEqual(new[]{96,112}),
    "Only reviewed patch clamps change; flags and shifts remain intact");
foreach (string name in new[]{"GetDetailHeight","GetBlockHeight","GetDetailHeightSmooth","GetZoneCell","GetPatchIndex"})
{
    var body=plan.Methods.Values.Single(m=>m.Name=="TerrainManager."+name);
    Check(body.Edits.Any(e=>e.Expected==8 && e.Replacement==9),"Tenth patch reachable: "+name);
    Check(body.Edits.Any(e=>e.Expected==9 && e.Replacement==10),"Ten-column patch stride: "+name);
}
foreach (string name in new[]{"SampleRawHeight","SampleFinalHeight","SampleBlockHeight","SampleRawHeightSmooth"})
{
    var bodies=plan.Methods.Values.Where(m=>m.Name=="TerrainManager."+name).ToArray();
    Check(bodies.Length==1,"Delegating overload left untouched: "+name);
    Check(bodies[0].Edits.Any(e=>e.Expected==1080 && e.Replacement==1200),"Expanded height boundary: "+name);
    Check(bodies[0].Edits.Any(e=>e.Expected==1081 && e.Replacement==1201),"Expanded height stride: "+name);
}
Check(plan.Methods.Values.Count(m=>m.Name=="TerrainManager.GetSurfaceHeight")==2,"Both water surface overloads reviewed separately");
Check(!plan.Methods.Values.Any(m=>m.Name.StartsWith("TerrainManager.SetHeightMap") || m.Name=="TerrainManager.SetRawHeightMap"),
    "Legacy import formats require a separate adapter");
var water = plan.Methods.Values.Single(m => m.Name == "WaterSimulation.SimulateWater");
Check(water.Edits.Single(e => e.Kind == "local").Replacement == 1080,"Water origin uses original width");
Check(water.Edits.Any(e => e.Expected == 1080 && e.Replacement == 1200),"Water cell grid expanded independently");
RejectFormat("assembly\t"+plan.AssemblyHash+"\ncoverage\tcomplete\n","False complete coverage rejected");
RejectFormat("unknown\tvalue\n","Unknown record rejected");
var serializerCases=new[] {
    (Type:"TerrainManager",Method:"Serialize",Keys:new[]{"m_rawHeights","m_blockHeights","m_blockHeights2"}),
    (Type:"TerrainManager",Method:"Deserialize",Keys:new[]{"m_rawHeights","m_blockHeights"}),
    (Type:"WaterSimulation",Method:"Serialize",Keys:new[]{"BeginRead","m_waterFrameIndex:last"}),
    (Type:"WaterSimulation",Method:"Deserialize",Keys:new[]{"m_waterBuffers"}),
    (Type:"GameAreaManager",Method:"Serialize",Keys:new[]{"m_areaGrid","m_areaCount","m_startTile"}),
    (Type:"GameAreaManager",Method:"Deserialize",Keys:new[]{"m_areaGrid"}),
    (Type:"NaturalResourceManager",Method:"Serialize",Keys:new[]{"m_areaResources","m_naturalResources"}),
    (Type:"NaturalResourceManager",Method:"Deserialize",Keys:new[]{"m_areaResources","m_naturalResources"}),
    (Type:"DistrictManager",Method:"Serialize",Keys:new[]{"m_districtGrid","m_parkGrid"}),
    (Type:"DistrictManager",Method:"Deserialize",Keys:new[]{"m_districtGrid","m_parkGrid"})
    ,(Type:"ImmaterialResourceManager",Method:"Serialize",Keys:new[]{"m_localFinalResources","m_localTempResources","m_globalFinalResources","m_globalTempResources","m_totalFinalResources","m_totalTempResources","m_totalTempResourcesMul"})
    ,(Type:"ImmaterialResourceManager",Method:"Deserialize",Keys:new[]{"m_localFinalResources","m_localTempResources"})
};
int injected=0;
foreach(var test in serializerCases)
{
    var target=module.GetType(test.Type).NestedTypes.Single(t=>t.Name=="Data").Methods.Single(m=>m.Name==test.Method);
    var input=new List<CodeInstruction>();
    var generator=new DynamicMethod("SerializerLabels",typeof(void),Type.EmptyTypes).GetILGenerator();
    var labels=target.Body.Instructions.ToDictionary(i=>i,_=>generator.DefineLabel());
    foreach(var instruction in target.Body.Instructions)
    {
        object operand=instruction.Operand;
        if(operand is FieldReference field && test.Keys.Any(k=>k.Split(':')[0]==field.Name)) operand=typeof(SerializerFixture).GetField(field.Name);
        else if(operand is MethodReference method && test.Keys.Contains(method.Name)) operand=typeof(SerializerFixture).GetMethod(method.Name);
        else if(operand is Instruction branch) operand=labels[branch];
        else if(operand is Instruction[] branches) operand=branches.Select(t=>labels[t]).ToArray();
        var code=new CodeInstruction(opcodes[unchecked((ushort)instruction.OpCode.Value)],operand);
        code.labels.Add(labels[instruction]); input.Add(code);
    }
    var helpers=test.Keys.ToDictionary(k=>k,k=>typeof(SerializerFixture).GetMethod(k=="m_totalTempResourcesMul"?"ProjectLongs":k=="m_districtGrid" || k=="m_parkGrid"?"ProjectDistricts":k.StartsWith("m_global") || k=="m_totalFinalResources" || k=="m_totalTempResources" || k=="m_areaGrid" || k=="m_areaResources" || k=="m_naturalResources"?"ProjectAreas":k=="m_areaCount" || k=="m_startTile"?"ProjectInteger":k=="BeginRead"?"ProjectCells":k=="m_waterBuffers"?"ProjectBuffers":k.EndsWith(":last")?"ProjectFrame":"ProjectHeights"));
    // Attach an end marker to a projected field access to test movement after
    // the helper, keeping helper exceptions inside the protected region.
    int marker=input.FindIndex(i=>i.operand is FieldInfo);
    if(marker>=0) input[marker].blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
    var transformed=SerializerTranspiler.Transform(input,helpers);
    Check(transformed.Count==input.Count+helpers.Count,"Exactly one helper per reviewed access");
    int cursor=0;
    for(int i=0;i<input.Count;i++)
    {
        var output=transformed[cursor++];
        Check(output.opcode==input[i].opcode && Equals(output.operand,input[i].operand),"Original serializer instruction retained");
        Check(output.labels.SequenceEqual(input[i].labels),"Serializer branch labels retained");
        string key=input[i].operand is FieldInfo f?f.Name:input[i].operand is MethodInfo m?m.Name:null;
        if(key!=null && helpers.ContainsKey(key+":last"))
            key=input.Skip(i+1).Any(c=>c.opcode==EmitOpCodes.Ldfld && c.operand is FieldInfo later && later.Name==key) ? null : key+":last";
        if(key!=null && helpers.TryGetValue(key,out var helper))
        {
            var call=transformed[cursor++];
            Check(call.opcode==EmitOpCodes.Call && Equals(call.operand,helper),"Correct projection helper");
            if(i==marker) Check(call.blocks.Any(b=>b.blockType==ExceptionBlockType.EndExceptionBlock) && output.blocks.Count==0,"End marker includes projection helper");
            injected++;
        }
    }
    Reject(()=>SerializerTranspiler.Transform(Array.Empty<CodeInstruction>(),helpers),"Missing projection access rejected");
    var duplicated=input.Concat(input.Where(i=>i.operand is MethodInfo || i.operand is FieldInfo fi && helpers.ContainsKey(fi.Name)));
    Reject(()=>SerializerTranspiler.Transform(duplicated,helpers),"Duplicate projection access rejected");
}
Check(injected==29,"Twenty-nine serializer projection/frame/ownership/resource/district/service calls");
foreach(string name in new[]{"W2G","G2W"})
{
    var method=module.GetType("ResourceWrapper").Methods.Single(m=>m.Name==name);
    var input=method.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],
        i.Operand is MethodReference called ? typeof(SerializerFixture).GetProperty(called.Name.Substring(4)).GetGetMethod() : i.Operand)).ToList();
    var transformed=SerializerTranspiler.Transform(input,new Dictionary<string,MethodInfo>{{"get_gridResolution",typeof(ResourceGrid).GetMethod("LegacyOrigin")}});
    Check(transformed.Count==input.Count+1,"One resource API origin projection per coordinate conversion");
    var dynamic=new DynamicMethod(name=="W2G"?"WorldToResource":"ResourceToWorld",name=="W2G"?typeof(int):typeof(float),
        new[]{typeof(SerializerFixture),name=="W2G"?typeof(float):typeof(int)});
    var generator=dynamic.GetILGenerator();
    foreach(var code in transformed)
    {
        if(code.operand==null)generator.Emit(code.opcode);
        else if(code.operand is MethodInfo called)generator.Emit(code.opcode,called);
        else if(code.operand is float value)generator.Emit(code.opcode,value);
        else generator.Emit(code.opcode,Convert.ToInt32(code.operand));
    }
    var fixture=new SerializerFixture();
    if(name=="W2G")
    {
        var convert=(Func<SerializerFixture,float,int>)dynamic.CreateDelegate(typeof(Func<SerializerFixture,float,int>));
        for(int cell=0;cell<569;cell++)Check(convert(fixture,(float)ResourceGrid.WorldPosition(cell))==cell,"Resource API world-to-grid retains the legacy origin");
    }
    else
    {
        var convert=(Func<SerializerFixture,int,float>)dynamic.CreateDelegate(typeof(Func<SerializerFixture,int,float>));
        for(int cell=0;cell<569;cell++)Check(convert(fixture,cell)==(float)ResourceGrid.WorldPosition(cell),"Resource API grid-to-world retains the legacy origin");
    }
}
Console.WriteLine("PASS: two actual resource-wrapper coordinate IL bodies executed for all 569 cells; original world origin retained.");
// Execute the actual transformed game's scheduling prefix as CLR IL. The
// first 42 instructions use only integers, so Unity need not be loaded.
var scheduleMethod=module.GetType("NaturalResourceManager").Methods.Single(m=>m.Name=="SimulationStepImpl2");
var scheduleInput=scheduleMethod.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand is VariableDefinition v ? v.Index : i.Operand)).ToList();
var scheduled=ResourceScheduleTranspiler.Transform(scheduleInput);
Check(scheduled.Count==scheduleInput.Count,"Schedule instruction count retained");
for(int i=0;i<scheduled.Count;i++)Check(!ReferenceEquals(scheduled[i],scheduleInput[i]),"Schedule inputs never mutated");
var dm=new DynamicMethod("ExecuteResourceSchedule",typeof(int[]),new[]{typeof(object),typeof(uint)});
var il=dm.GetILGenerator();var localSlots=Enumerable.Range(0,8).Select(_=>il.DeclareLocal(typeof(int))).ToArray();
foreach(var code in scheduled.Take(42))
{
    if(code.operand==null)il.Emit(code.opcode);
    else if(code.operand is MethodInfo method)il.Emit(code.opcode,method);
    else if(code.opcode.OperandType==System.Reflection.Emit.OperandType.ShortInlineVar || code.opcode.OperandType==System.Reflection.Emit.OperandType.InlineVar)il.Emit(code.opcode,localSlots[Convert.ToInt32(code.operand)]);
    else if(code.opcode.OperandType==System.Reflection.Emit.OperandType.ShortInlineI)il.Emit(code.opcode,Convert.ToSByte(code.operand));
    else il.Emit(code.opcode,Convert.ToInt32(code.operand));
}
il.Emit(EmitOpCodes.Ldc_I4_4);il.Emit(EmitOpCodes.Newarr,typeof(int));
for(int i=0;i<4;i++){il.Emit(EmitOpCodes.Dup);il.Emit(EmitOpCodes.Ldc_I4,i);il.Emit(EmitOpCodes.Ldloc,localSlots[i]);il.Emit(EmitOpCodes.Stelem_I4);}
il.Emit(EmitOpCodes.Ret);
var instanceExecute=(Func<object,uint,int[]>)dm.CreateDelegate(typeof(Func<object,uint,int[]>));
Func<uint,int[]> execute=frame=>instanceExecute(null,frame);
var seenCells=new bool[ResourceGrid.Cells];
for(uint frame=0;frame<ResourceGrid.Cycle;frame++)
{
    var slice=execute(frame);Check(slice[0]==frame && slice[1]<569 && slice[2]>=0 && slice[3]<569,"Valid schedule slice");
    for(int x=slice[2];x<=slice[3];x++)
    {int index=slice[1]*569+x;Check(!seenCells[index],"No duplicate resource updates");seenCells[index]=true;Check((ResourceGrid.RowBase(slice[1])+18*x>>10)<100,"Resource cache indices stay inside 100 tiles");}
}
Check(seenCells.All(v=>v),"All 323761 resource cells updated exactly once");
Check(execute(uint.MaxValue)[0]==ResourceGrid.Phase(uint.MaxValue),"Unsigned frame wrap handled");
var broken=scheduleInput.Select(i=>new CodeInstruction(i)).ToList();broken[9].opcode=EmitOpCodes.Stloc_0;
Reject(()=>ResourceScheduleTranspiler.Transform(broken),"Changed schedule store refused");
Console.WriteLine("PASS: actual resource schedule IL executed for all 1138 phases; 323761 cells visited exactly once; cache bounds and uint wrap; modified prefix refusal.");
Console.WriteLine("PASS: twelve real serializer IL bodies / twenty-nine helper injections; branch labels; exception end markers; missing/duplicate access refusal. Game-specific array member types represented by fixture members.");
Console.WriteLine($"PASS: real Harmony instruction transformation for {plan.Methods.Count} game methods / {editsChecked} edits; labels, exception markers, input isolation, version hashes and negative cases. No methods patched in a running game.");

static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
static void Reject(Action action,string message) {
    try { action(); } catch(InvalidOperationException) { return; }
    throw new Exception(message);
}
static void RejectFormat(string text,string message) {
    try { PatchPlan.Load(new StringReader(text)); } catch(FormatException) { return; }
    throw new Exception(message);
}

public sealed class SerializerFixture
{
    public int gridResolution=>569;
    public float cellSize=>33.75f;
    public ushort[] m_rawHeights=new ushort[1];
    public ushort[] m_blockHeights=new ushort[1];
    public ushort[] m_blockHeights2=new ushort[1];
    public int[][] m_waterBuffers=new[]{new int[1]};
    public uint m_waterFrameIndex=128;
    public int[] m_areaGrid=new int[100];
    public int[] m_areaResources=new int[100];
    public int[] m_naturalResources=new int[569*569];
    public int m_areaCount=1,m_startTile=44;
    public DistrictCellFixture[] m_districtGrid=new DistrictCellFixture[1],m_parkGrid=new DistrictCellFixture[1];
    public static DistrictCellFixture[] ProjectDistricts(DistrictCellFixture[] input)=>input;
    public ushort[] m_localFinalResources=new ushort[1],m_localTempResources=new ushort[1];
    public int[] m_globalFinalResources=new int[35],m_globalTempResources=new int[35],m_totalFinalResources=new int[35],m_totalTempResources=new int[35];
    public long[] m_totalTempResourcesMul=new long[35];
    public static long[] ProjectLongs(long[] input)=>input;
    public static int[] ProjectAreas(int[] input)=>input;
    public static int ProjectInteger(int input)=>input;
    public int[] BeginRead() => new int[1];
    public static ushort[] ProjectHeights(ushort[] input) => input;
    public static int[] ProjectCells(int[] input) => input;
    public static int[][] ProjectBuffers(int[][] input) => input;
    public static uint ProjectFrame(uint input) => input;
}
public struct DistrictCellFixture { public byte Id1,Id2,Id3,Id4,Alpha1,Alpha2,Alpha3,Alpha4; }
