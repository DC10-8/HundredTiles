extern alias ExternalCecil;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using ExternalCecil::Mono.Cecil;
using VariableDefinition=ExternalCecil::Mono.Cecil.Cil.VariableDefinition;
using EmitCodes=System.Reflection.Emit.OpCodes;

public static class NetSpatialChecks
{
    public static void Run(ModuleDefinition module,PatchPlan plan,Dictionary<ushort,OpCode> opcodes)
    {
        var manager=new NetFixture();
        var addNode=(NodeAction)Compile("InitializeNode",typeof(NodeAction));
        var removeNode=(NodeAction)Compile("FinalizeNode",typeof(NodeAction));
        var addSegment=(SegmentAction)Compile("InitializeSegment",typeof(SegmentAction));
        var removeSegment=(SegmentAction)Compile("FinalizeSegment",typeof(SegmentAction));
        for(int z=0;z<300;z++)for(int x=0;x<300;x++)
        {
            int index=z*300+x;
            var position=new NetVector {x=(x-135+0.5f)*64,z=(z-135+0.5f)*64};
            manager.m_nodes.m_buffer[1].m_position=position;manager.m_nodes.m_buffer[2].m_position=position;
            addNode(manager,1,ref manager.m_nodes.m_buffer[1]);addNode(manager,2,ref manager.m_nodes.m_buffer[2]);
            Require(manager.m_nodeGrid[index]==2 && manager.m_nodes.m_buffer[2].m_nextGridNode==1,"Node insertion/head links");
            removeNode(manager,1,ref manager.m_nodes.m_buffer[1]);
            Require(manager.m_nodeGrid[index]==2 && manager.m_nodes.m_buffer[2].m_nextGridNode==0,"Node predecessor unlink");
            removeNode(manager,2,ref manager.m_nodes.m_buffer[2]);Require(manager.m_nodeGrid[index]==0,"Node head unlink");
            manager.m_segments.m_buffer[1]=new NetSegmentFixture {m_startNode=1,m_endNode=2};
            manager.m_segments.m_buffer[2]=new NetSegmentFixture {m_startNode=1,m_endNode=2};
            addSegment(manager,1,ref manager.m_segments.m_buffer[1]);addSegment(manager,2,ref manager.m_segments.m_buffer[2]);
            Require(manager.m_segmentGrid[index]==2 && manager.m_segments.m_buffer[2].m_nextGridSegment==1,"Segment insertion/head links");
            removeSegment(manager,1,ref manager.m_segments.m_buffer[1]);
            Require(manager.m_segmentGrid[index]==2 && manager.m_segments.m_buffer[2].m_nextGridSegment==0,"Segment predecessor unlink");
            removeSegment(manager,2,ref manager.m_segments.m_buffer[2]);Require(manager.m_segmentGrid[index]==0,"Segment head unlink");
        }
        Require(manager.m_nodeGrid.All(v=>v==0) && manager.m_segmentGrid.All(v=>v==0),"No leaked grid heads");
        foreach(var sample in new[]{(-9000f,0),(-8640f,0),(8639f,269),(8640f,270),(10559f,299),(11000f,299)})
        {
            manager.m_nodes.m_buffer[1].m_position=new NetVector {x=sample.Item1,z=sample.Item1};
            addNode(manager,1,ref manager.m_nodes.m_buffer[1]);Require(manager.m_nodeGrid[sample.Item2*300+sample.Item2]==1,"Legacy origin and extended edge clamp");
            removeNode(manager,1,ref manager.m_nodes.m_buffer[1]);
        }
        var awake=plan.Methods.Values.Single(m=>m.Name=="NetManager.Awake");
        Require(awake.Edits.Count(e=>e.Expected==72900 && e.Replacement==90000)==2,"Both derived grid allocations expanded");
        var ray=plan.Methods.Values.Single(m=>m.Name=="NetManager.RayCast");
        Require(ray.Edits.Where(e=>e.Kind=="float").Select(e=>e.Index).SequenceEqual(new[]{1,3,5,7}),"Only clipping-box float positions change");
        Require(ray.Edits.Single(e=>e.Index==1).Replacement==BitConverter.SingleToInt32Bits(960f) && ray.Edits.Single(e=>e.Index==5).Replacement==BitConverter.SingleToInt32Bits(19200f),"Asymmetric road raycast bounds");
        Console.WriteLine("PASS: four complete transformed NetManager insertion/removal IL bodies executed over all 90000 cells; linked-list head/predecessor removal; legacy origin and extended edge clamps. Unity fields/math represented by fixtures.");

        Delegate Compile(string name,Type delegateType)
        {
            var target=module.GetType("NetManager").Methods.Single(m=>m.Name==name);
            Type data=name.EndsWith("Node")?typeof(NetNodeFixture):typeof(NetSegmentFixture);
            var dm=new DynamicMethod(name,typeof(void),new[]{typeof(NetFixture),typeof(ushort),data.MakeByRefType()});
            var il=dm.GetILGenerator();
            var locals=target.Body.Variables.Select(v=>il.DeclareLocal(MapType(v.VariableType))).ToArray();
            var labels=target.Body.Instructions.ToDictionary(i=>i,_=>il.DefineLabel());
            var input=target.Body.Instructions.Select(i=>{
                object operand=i.Operand;
                if(operand is FieldReference f)
                {
                    Type owner=f.DeclaringType.Name.StartsWith("Array16")?typeof(NetBuffer<>).MakeGenericType(MapType(((GenericInstanceType)f.DeclaringType).GenericArguments[0])):MapType(f.DeclaringType);
                    operand=owner.GetField(f.Name) ?? throw new Exception("Unknown fixture field "+f.FullName);
                }
                else if(operand is MethodReference m)
                    operand=m.Name=="Clamp"?typeof(NetSpatialChecks).GetMethod("Clamp"):m.Name=="Error"?typeof(NetSpatialChecks).GetMethod("Error"):
                        m.DeclaringType.FullName=="UnityEngine.Vector3"?typeof(NetVector).GetMethod(m.Name):
                        m.DeclaringType.FullName=="System.Environment"?typeof(Environment).GetProperty("StackTrace").GetGetMethod():
                        m.DeclaringType.FullName=="System.String"?typeof(string).GetMethod(m.Name,m.Parameters.Select(p=>MapType(p.ParameterType)).ToArray()):throw new Exception("Unknown fixture method "+m.FullName);
                else if(operand is VariableDefinition v)operand=v.Index;
                else if(operand is ExternalCecil::Mono.Cecil.Cil.Instruction branch)operand=labels[branch];
                else if(operand is TypeReference type)operand=MapType(type);
                var code=new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)],operand);code.labels.Add(labels[i]);return code;
            }).ToList();
            var output=PlannedTranspiler.Transform(input,plan.Methods.Values.Single(m=>m.Name=="NetManager."+name));
            foreach(var code in output)
            {
                foreach(var label in code.labels)il.MarkLabel(label);
                if(code.operand==null)il.Emit(code.opcode);
                else if(code.operand is FieldInfo f)il.Emit(code.opcode,f);
                else if(code.operand is MethodInfo m)il.Emit(code.opcode,m);
                else if(code.operand is Type t)il.Emit(code.opcode,t);
                else if(code.operand is Label label)il.Emit(code.opcode,label);
                else if(code.operand is float value)il.Emit(code.opcode,value);
                else if(code.operand is string text)il.Emit(code.opcode,text);
                else if(code.opcode.OperandType==OperandType.ShortInlineVar || code.opcode.OperandType==OperandType.InlineVar)il.Emit(code.opcode,locals[Convert.ToInt32(code.operand)]);
                else if(code.opcode.OperandType==OperandType.ShortInlineI)il.Emit(code.opcode,Convert.ToSByte(code.operand));
                else il.Emit(code.opcode,Convert.ToInt32(code.operand));
            }
            return dm.CreateDelegate(delegateType);
        }
    }
    static Type MapType(TypeReference type)=>type.FullName switch {
        "NetManager"=>typeof(NetFixture),"NetNode"=>typeof(NetNodeFixture),"NetSegment"=>typeof(NetSegmentFixture),
        "UnityEngine.Vector3"=>typeof(NetVector),"System.Int32"=>typeof(int),"System.UInt16"=>typeof(ushort),"System.String"=>typeof(string),
        _=>throw new Exception("Unknown fixture type "+type.FullName)
    };
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static int Clamp(int value,int min,int max)=>Math.Clamp(value,min,max);
    public static void Error(int channel,string message)=>throw new Exception("Actual game list guard fired: "+message);
}
public delegate void NodeAction(NetFixture manager,ushort id,ref NetNodeFixture node);
public delegate void SegmentAction(NetFixture manager,ushort id,ref NetSegmentFixture segment);
public struct NetVector {
    public float x,z;
    public static NetVector operator +(NetVector a,NetVector b)=>new() {x=a.x+b.x,z=a.z+b.z};
    public static NetVector operator *(NetVector a,float b)=>new() {x=a.x*b,z=a.z*b};
}
public struct NetNodeFixture {public NetVector m_position;public ushort m_nextGridNode;}
public struct NetSegmentFixture {public ushort m_startNode,m_endNode,m_nextGridSegment;}
public sealed class NetBuffer<T> {public T[] m_buffer=new T[8];}
public sealed class NetFixture {
    public NetBuffer<NetNodeFixture> m_nodes=new();public NetBuffer<NetSegmentFixture> m_segments=new();
    public ushort[] m_nodeGrid=new ushort[90000],m_segmentGrid=new ushort[90000];
}
