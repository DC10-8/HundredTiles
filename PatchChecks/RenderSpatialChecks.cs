extern alias ExternalCecil;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using ExternalCecil::Mono.Cecil;
using VariableDefinition=ExternalCecil::Mono.Cecil.Cil.VariableDefinition;
using Codes=System.Reflection.Emit.OpCodes;

public static class RenderSpatialChecks
{
    public static void Run(ModuleDefinition module,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    {
        var render=new RenderFixture();
        var update=(Action<RenderFixture,int,int,int>)Compile("UpdateGroup",typeof(Action<RenderFixture,int,int,int>),new[]{typeof(RenderFixture),typeof(int),typeof(int),typeof(int)});
        var mega=(Action<RenderFixture,int,int,int>)Compile("UpdateMegaGroup",typeof(Action<RenderFixture,int,int,int>),new[]{typeof(RenderFixture),typeof(int),typeof(int),typeof(int)});
        var step=(Action<RenderFixture,int>)Compile("SimulationStepImpl",typeof(Action<RenderFixture,int>),new[]{typeof(RenderFixture),typeof(int)});
        for(int z=0;z<50;z++)for(int x=0;x<50;x++)update(render,x,z,7);
        Require(render.m_groups.All(g=>g!=null && g.Dirty==1),"All 2500 groups allocated and dirtied");
        Require(render.m_updatedGroups1.Take(39).All(w=>w==ulong.MaxValue) && render.m_updatedGroups1[39]==15,"2500 bits fit 40 words with unused bits clear");
        step(render,1);
        Require(render.m_groups.All(g=>g.MeshUpdates==1) && render.m_updatedGroups1.All(w=>w==0) && !render.m_groupsUpdated1,"Actual simulation step drains all 2500 groups once");
        step(render,1);Require(render.m_groups.All(g=>g.MeshUpdates==1),"No stale updates");
        update(render,49,49,7);step(render,1);
        Require(render.m_groups[2499].MeshUpdates==2 && render.m_groups.Take(2499).All(g=>g.MeshUpdates==1),"Last group independently updates");
        for(int z=0;z<10;z++)for(int x=0;x<10;x++)mega(render,x,z,7);
        Require(render.m_megaGroups.All(g=>g!=null && g.Dirty==1),"100 mega groups allocated");
        var awake=plan.Methods.Values.Single(m=>m.Name=="RenderManager.Awake");
        Require(awake.Edits.Single(e=>e.Index==29).Replacement==40 && !awake.Edits.Any(e=>e.Index==25),"Only dirty bitmap grows; 32 material layers retained");
        foreach(string name in new[]{"AddToGrid","AddToGrid2"})
        {
            var target=module.GetType("BuildingManager").Methods.Single(m=>m.Name==name);
            var dm=new DynamicMethod(name,typeof(int),new[]{typeof(object),typeof(ushort),typeof(NetNodeFixture).MakeByRefType()});var il=dm.GetILGenerator();
            var slots=Enumerable.Range(0,3).Select(_=>il.DeclareLocal(typeof(int))).ToArray();
            var input=target.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand)).ToList();
            var output=PlannedTranspiler.Transform(input,plan.Methods.Values.Single(m=>m.Name=="BuildingManager."+name));
            foreach(var code in output.Take(30))
            {
                if(code.operand is FieldReference f)il.Emit(code.opcode,(f.DeclaringType.Name=="Building"?typeof(NetNodeFixture):typeof(NetVector)).GetField(f.Name));
                else if(code.operand is MethodReference)il.Emit(code.opcode,typeof(NetSpatialChecks).GetMethod("Clamp"));
                else if(code.operand==null)il.Emit(code.opcode);
                else if(code.operand is float v)il.Emit(code.opcode,v);
                else il.Emit(code.opcode,Convert.ToInt32(code.operand));
            }
            il.Emit(Codes.Ldloc_2);il.Emit(Codes.Ret);
            var locate=(BuildingIndex)dm.CreateDelegate(typeof(BuildingIndex));
            int side=name=="AddToGrid"?300:50;float cell=name=="AddToGrid"?64:384;float origin=name=="AddToGrid"?135:22.5f;
            for(int z=0;z<side;z++)for(int x=0;x<side;x++)
            {var building=new NetNodeFixture {m_position=new NetVector {x=(x-origin+0.5f)*cell,z=(z-origin+0.5f)*cell}};Require(locate(null,1,ref building)==z*side+x,"Actual building primary/secondary coordinate prefix covers every cell");}
        }
        foreach(var name in new[]{("NotificationManager","EndRenderingImpl"),("NotificationManager","CalculateGroupData"),("NotificationManager","PopulateGroupData"),("Notification","PopulateGroupData")})
        {
            var target=module.GetType(name.Item1).Methods.Single(m=>m.Name==name.Item2);
            var reviewed=plan.Methods.Values.Single(m=>m.Token==target.MetadataToken.ToInt32());
            int first=reviewed.Edits.Single(e=>e.Expected==25).Index;
            var input=target.Body.Instructions.Select(i=>new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],i.Operand is VariableDefinition v?v.Index:i.Operand)).ToList();
            var output=PlannedTranspiler.Transform(input,reviewed);
            var dm=new DynamicMethod("NotificationGrid",typeof(int[]),Type.EmptyTypes);var il=dm.GetILGenerator();
            var slots=Enumerable.Range(0,target.Body.Variables.Count).Select(_=>il.DeclareLocal(typeof(int))).ToArray();
            foreach(var code in output.Skip(first).Take(8))
            {
                if(code.operand==null)il.Emit(code.opcode);
                else if(code.opcode.OperandType==OperandType.ShortInlineVar || code.opcode.OperandType==OperandType.InlineVar)il.Emit(code.opcode,slots[Convert.ToInt32(code.operand)]);
                else if(code.opcode.OperandType==OperandType.ShortInlineI)il.Emit(code.opcode,Convert.ToSByte(code.operand));
                else il.Emit(code.opcode,Convert.ToInt32(code.operand));
            }
            int LocalIndex(CodeInstruction c)=>c.opcode==Codes.Stloc_0?0:c.opcode==Codes.Stloc_1?1:c.opcode==Codes.Stloc_2?2:c.opcode==Codes.Stloc_3?3:Convert.ToInt32(c.operand);
            il.Emit(Codes.Ldc_I4_2);il.Emit(Codes.Newarr,typeof(int));
            for(int i=0;i<2;i++){il.Emit(Codes.Dup);il.Emit(Codes.Ldc_I4,i);il.Emit(Codes.Ldloc,slots[LocalIndex(output[first+(i==0?1:7)])]);il.Emit(Codes.Stelem_I4);}
            il.Emit(Codes.Ret);var shape=((Func<int[]>)dm.CreateDelegate(typeof(Func<int[]>)))();
            Require(shape[0]==50 && shape[1]==0,"Actual notification group shape spans all 50 groups without centering offset");
        }
        Require(plan.Methods.Values.Single(m=>m.Name=="NotificationManager.Awake").Edits.Single().Replacement==50,"Notification arrays have 2500 groups and retain 77 problem slots");
        Console.WriteLine("PASS: complete RenderManager group/mega-group updates and simulation IL executed; 2500 dirty bits, 40 words, last-cell update; actual building coordinate prefixes over 90000 primary / 2500 secondary cells. Fixture mesh methods used; no Unity rendering.");

        Delegate Compile(string name,Type delegateType,Type[] args)
        {
            var target=module.GetType("RenderManager").Methods.Single(m=>m.Name==name);
            var dm=new DynamicMethod(name,typeof(void),args);var il=dm.GetILGenerator();
            var locals=target.Body.Variables.Select(v=>il.DeclareLocal(MapType(v.VariableType))).ToArray();
            var labels=target.Body.Instructions.ToDictionary(i=>i,_=>il.DefineLabel());
            var input=target.Body.Instructions.Select(i=>{
                object operand=i.Operand;
                if(operand is FieldReference f)operand=MapType(f.DeclaringType).GetField(f.Name) ?? throw new Exception("Unknown render field "+f.FullName);
                else if(operand is MethodReference m)operand=m.Name==".ctor"?(object)MapType(m.DeclaringType).GetConstructor(new[]{typeof(int),typeof(int)}):MapType(m.DeclaringType).GetMethod(m.Name);
                else if(operand is VariableDefinition v)operand=v.Index;
                else if(operand is ExternalCecil::Mono.Cecil.Cil.Instruction branch)operand=labels[branch];
                else if(operand is TypeReference t)operand=MapType(t);
                var code=new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],operand);code.labels.Add(labels[i]);return code;
            }).ToList();
            var output=plan.Methods.TryGetValue(target.MetadataToken.ToInt32(),out var reviewed)?PlannedTranspiler.Transform(input,reviewed):input;
            foreach(var code in output)
            {
                foreach(var label in code.labels)il.MarkLabel(label);
                if(code.operand==null)il.Emit(code.opcode);
                else if(code.operand is FieldInfo f)il.Emit(code.opcode,f);
                else if(code.operand is MethodInfo m)il.Emit(code.opcode,m);
                else if(code.operand is ConstructorInfo c)il.Emit(code.opcode,c);
                else if(code.operand is Type t)il.Emit(code.opcode,t);
                else if(code.operand is Label label)il.Emit(code.opcode,label);
                else if(code.operand is long value)il.Emit(code.opcode,value);
                else if(code.opcode.OperandType==OperandType.ShortInlineVar || code.opcode.OperandType==OperandType.InlineVar)il.Emit(code.opcode,locals[Convert.ToInt32(code.operand)]);
                else if(code.opcode.OperandType==OperandType.ShortInlineI)il.Emit(code.opcode,Convert.ToSByte(code.operand));
                else il.Emit(code.opcode,Convert.ToInt32(code.operand));
            }
            return dm.CreateDelegate(delegateType);
        }
    }
    static Type MapType(TypeReference t)=>t.FullName switch {"RenderManager"=>typeof(RenderFixture),"RenderGroup"=>typeof(GroupFixture),"MegaRenderGroup"=>typeof(MegaFixture),"System.Int32"=>typeof(int),"System.UInt64"=>typeof(ulong),_=>throw new Exception("Unknown render type "+t.FullName)};
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
public delegate int BuildingIndex(object manager,ushort id,ref NetNodeFixture building);
public sealed class RenderFixture {public GroupFixture[] m_groups=new GroupFixture[2500];public MegaFixture[] m_megaGroups=new MegaFixture[100];public ulong[] m_updatedGroups1=new ulong[40];public bool m_groupsUpdated1;}
public sealed class GroupFixture {public int Dirty,MeshUpdates;public GroupFixture(int x,int z){}public void SetLayerDataDirty(int layer){Dirty++;}public void UpdateMeshData(){MeshUpdates++;}}
public sealed class MegaFixture {public int Dirty;public MegaFixture(int x,int z){}public void SetLayerDirty(int layer){Dirty++;}}
