extern alias ExternalCecil;
using Cecil = ExternalCecil::Mono.Cecil;
using Cil = ExternalCecil::Mono.Cecil.Cil;
using System.Security.Cryptography;

static class SaveLifecycleChecks
{
    static void Check(bool value,string label) {if(!value)throw new Exception(label);}
    public static void Run(Cecil.ModuleDefinition game)
    {
        string runtimePath=Path.Combine(Path.GetDirectoryName(game.FileName),"ColossalManaged.dll");
        const string reviewed="34701F61DEF31F5F9DF79D573CE19E0BC52575879BCC395D51B39F6D746A58B9";
        Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(runtimePath)))==reviewed,"Reviewed serialization runtime hash");
        using var runtime=Cecil.ModuleDefinition.ReadModule(runtimePath);
        var serializer=runtime.Types.Single(t=>t.FullName=="ColossalFramework.IO.DataSerializer");
        var serialize=serializer.Methods.Single(m=>m.Name=="Serialize" && m.Parameters.Count==4);
        var saveIL=serialize.Body.Instructions;
        int childSave=Enumerable.Range(0,saveIL.Count).Single(i=>saveIL[i].Operand is Cecil.MethodReference m && m.DeclaringType.Name=="IDataContainer" && m.Name=="Serialize");
        Check(saveIL.Take(childSave).Any(i=>i.Operand is Cecil.MethodReference m && m.Name=="WriteObject"),"Serializer queues root before serializing containers");
        Check(saveIL.Skip(childSave+1).Any(i=>i.Operand is Cil.Instruction target && saveIL.IndexOf(target)<childSave),"Full serializer loop continues to deferred child containers after root Serialize returns");
        var deserialize=serializer.Methods.Single(m=>m.Name=="Deserialize" && m.HasGenericParameters && m.Parameters.Count==3);
        var il=deserialize.Body.Instructions;
        int FindCall(string owner,string name)=>Enumerable.Range(0,il.Count).Single(i=>il[i].Operand is Cecil.MethodReference m && m.DeclaringType.Name==owner && m.Name==name);
        int root=FindCall("DataSerializer","ReadObject");
        int children=FindCall("IDataContainer","Deserialize");
        int after=FindCall("IDataContainer","AfterDeserialize");
        Check(root<children && children<after,"Root queued before full Deserialize pass, then AfterDeserialize pass");
        Check(il.Skip(children+1).Take(after-children-1).Any(i=>i.Operand is Cil.Instruction target && il.IndexOf(target)<children),"Deserialize loop completes before AfterDeserialize pass");
        Check(il.Skip(children+1).Take(after-children-1).Any(i=>i.OpCode==Cil.OpCodes.Ldc_I4_0),"AfterDeserialize restarts queue at root");
        using var bridge=Cecil.ModuleDefinition.ReadModule("SaveBridge/bin/Release/net35/HundredTiles.SaveBridge.dll");
        var hooks=bridge.Types.Single(t=>t.Name=="SerializerHooks");
        var specs=hooks.Methods.Single(m=>m.Name=="Specifications").Body.Instructions;
        Check(specs.Any(i=>Equals(i.Operand,"BeginSerialize")) && specs.Any(i=>Equals(i.Operand,"EndSerialize")) && !specs.Any(i=>Equals(i.Operand,"BeginSave")),"Save capture and finalizer cover the full serializer queue, rather than only root container Serialize");
        Check(specs.Count(i=>Equals(i.Operand,"LoadFailure"))==9,"All root and child deserialization hooks clean up failures");
        Check(specs.Any(i=>Equals(i.Operand,"BeforeAfterDeserialize")),"Root restoration hook connected");
        Check(specs.Any(i=>i.Operand is Cecil.MethodReference m && m.DeclaringType.Name=="ObjectSerializerHooks" && m.Name=="Specifications"),"Frozen object serializers included in complete save hooks");
        foreach(string owner in new[]{"SimulationManager","TreeManager","PropManager"})
        {
            var data=game.Types.Single(t=>t.Name==owner).NestedTypes.Single(t=>t.Name=="Data");
            Check(data.Methods.Any(m=>m.Name=="Deserialize") && data.Methods.Any(m=>m.Name=="AfterDeserialize"),owner+" concrete lifecycle targets");
        }
        Console.WriteLine("PASS: reviewed ColossalManaged serializer IL; root restoration before child AfterDeserialize; object/district save hooks included; nine load-failure finalizers. No Unity execution.");
    }
}
