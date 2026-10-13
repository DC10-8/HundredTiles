using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;

static class SimulationRepairs
{
    public static void Run(string input,string output)
    {
        if(File.Exists(output) || Path.GetFullPath(input)==Path.GetFullPath(output))throw new InvalidOperationException("Use a new output file.");
        string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input)));
        using var module=ModuleDefinition.ReadModule(input);
        if(hash=="E84ED6D3900EB948DFF0B1D8E9E16153DDE712764E42D9FC8D7EA88165030907")
        {
            var method=module.Types.Single(t=>t.FullName=="RoadUtilityPoles.Harmony.Patches.ZoneBlockPoleFootprintPatch").Methods.Single(m=>m.Name=="Prefix");
            var sites=method.Body.Instructions.Where(i=>i.Operand is MethodReference r && r.DeclaringType.FullName=="System.Reflection.MethodInfo" && r.Name=="op_Equality").ToArray();
            if(sites.Length!=1 || sites[0].Previous.OpCode!=OpCodes.Ldnull)throw new InvalidOperationException("Reviewed null comparison differs.");
            sites[0].OpCode=OpCodes.Ceq;sites[0].Operand=null;
        }
        else if(hash=="4D70FA1E6684E4095F4E6944AC3003F7BBAE6DDEF123BDE72991F96A86D91217")
        {
            var type=module.Types.Single(t=>t.FullName=="RealismLite.RealismState");
            var method=type.Methods.Single(m=>m.Name==".cctor");
            var site=method.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stsfld && i.Operand is FieldReference f && f.Name=="LastConstructState");
            if(site.Previous.OpCode!=OpCodes.Newarr || site.Previous.Previous.OpCode!=OpCodes.Ldc_I4 || (int)site.Previous.Previous.Operand!=49152)throw new InvalidOperationException("Reviewed building state allocation differs.");
            site.Previous.Previous.Operand=65536;
        }
        else throw new InvalidOperationException("Unknown input version; no patch applied.");
        module.Write(output);
        Console.WriteLine(Path.GetFileName(input)+" SHA256="+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))));
    }
    public static void FixRenew(ModuleDefinition module)
    {
        var method=module.Types.Single(t=>t.FullName=="MoreNetworkCapacityRenew.TMPEPatches").Methods.Single(m=>m.Name=="TrafficLightSimulationManager_SimulationStep_Postfix");
        var site=method.Body.Instructions.Single(i=>i.Operand is MethodReference r && r.Name=="GetMethod" && r.Parameters.Count==2 && i.Previous.Previous.OpCode==OpCodes.Ldstr && (string)i.Previous.Previous.Operand=="RemoveNodeFromSimulation");
        var old=(MethodReference)site.Operand;
        var type=old.DeclaringType;
        var get=new MethodReference("GetMethod",old.ReturnType,type){HasThis=true};
        get.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));get.Parameters.Add(new ParameterDefinition(old.Parameters[1].ParameterType));
        get.Parameters.Add(new ParameterDefinition(new TypeReference("System.Reflection","Binder",module,module.TypeSystem.CoreLibrary)));
        get.Parameters.Add(new ParameterDefinition(new ArrayType(type)));
        get.Parameters.Add(new ParameterDefinition(new ArrayType(new TypeReference("System.Reflection","ParameterModifier",module,module.TypeSystem.CoreLibrary){IsValueType=true})));
        var fromHandle=new MethodReference("GetTypeFromHandle",type,type){HasThis=false};fromHandle.Parameters.Add(new ParameterDefinition(new TypeReference("System","RuntimeTypeHandle",module,module.TypeSystem.CoreLibrary){IsValueType=true}));
        var extra=new[]{Instruction.Create(OpCodes.Ldnull),Instruction.Create(OpCodes.Ldc_I4_1),Instruction.Create(OpCodes.Newarr,type),Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Ldtoken,module.TypeSystem.UInt16),Instruction.Create(OpCodes.Call,fromHandle),Instruction.Create(OpCodes.Stelem_Ref),Instruction.Create(OpCodes.Ldnull)};
        foreach(var instruction in method.Body.Instructions){if(instruction.Operand==site)instruction.Operand=extra[0];else if(instruction.Operand is Instruction[] targets)for(int i=0;i<targets.Length;i++)if(targets[i]==site)targets[i]=extra[0];}
        foreach(var handler in method.Body.ExceptionHandlers){if(handler.TryStart==site)handler.TryStart=extra[0];if(handler.TryEnd==site)handler.TryEnd=extra[0];if(handler.HandlerStart==site)handler.HandlerStart=extra[0];if(handler.HandlerEnd==site)handler.HandlerEnd=extra[0];if(handler.FilterStart==site)handler.FilterStart=extra[0];}
        foreach(var i in extra)method.Body.GetILProcessor().InsertBefore(site,i);
        site.Operand=get;
    }
}

