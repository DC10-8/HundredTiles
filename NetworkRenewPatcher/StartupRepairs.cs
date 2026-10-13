using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;

static class StartupRepairs
{
    const string ReviewedNetworkExtensions="D65C0618D23D398DB27D98B750AA66F552E8EE10E2982DF3316B843B27F46474";
    const string ReviewedBuildingControl="4C37C9B79102ABC257A56E46C33AF01131855134936378A2C32AD0A767EFA93F";
    public static void Run(string input,string output,string helper,string managed)
    {
        if(File.Exists(output) || Path.GetFullPath(input)==Path.GetFullPath(output))throw new InvalidOperationException("Use a new output file.");
        string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input)));
        bool optional=hash==ReviewedBuildingControl;
        if(hash!=ReviewedNetworkExtensions && !optional)throw new InvalidOperationException("Unreviewed startup repair input.");
        using var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(input))!);resolver.AddSearchDirectory(managed);resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(helper))!);
        using var module=ModuleDefinition.ReadModule(input,new ReaderParameters{AssemblyResolver=resolver});
        using var source=ModuleDefinition.ReadModule(helper,new ReaderParameters{AssemblyResolver=resolver});
        string helperName=optional?"OptionalBuildingThemes":"RecoverableTypeScan";
        var original=source.Types.Single(t=>t.FullName=="HundredTiles.StartupRepairs."+helperName).Methods.Single(m=>m.Name==(optional?"IsEnabled":"GetTypes"));
        var type=new TypeDefinition("HundredTiles.StartupRepairs",helperName,TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed,module.TypeSystem.Object);
        module.Types.Add(type);
        var method=new MethodDefinition(original.Name,MethodAttributes.Public|MethodAttributes.Static,module.ImportReference(original.ReturnType));
        type.Methods.Add(method);
        foreach(var parameter in original.Parameters)method.Parameters.Add(new ParameterDefinition(parameter.Name,parameter.Attributes,module.ImportReference(parameter.ParameterType)));
        method.Body.InitLocals=original.Body.InitLocals;
        method.Body.MaxStackSize=original.Body.MaxStackSize;
        foreach(var variable in original.Body.Variables)method.Body.Variables.Add(new VariableDefinition(module.ImportReference(variable.VariableType)));
        var map=new Dictionary<Instruction,Instruction>();
        foreach(var instruction in original.Body.Instructions){var copy=Instruction.Create(OpCodes.Nop);copy.OpCode=instruction.OpCode;map.Add(instruction,copy);method.Body.Instructions.Add(copy);}
        foreach(var instruction in original.Body.Instructions)
        {
            map[instruction].Operand=instruction.Operand switch {
                Instruction target=>map[target],
                Instruction[] targets=>targets.Select(t=>map[t]).ToArray(),
                VariableDefinition variable=>method.Body.Variables[variable.Index],
                ParameterDefinition parameter=>method.Parameters[parameter.Index],
                MethodReference reference=>module.ImportReference(reference),
                FieldReference reference=>module.ImportReference(reference),
                TypeReference reference=>module.ImportReference(reference),
                _=>instruction.Operand
            };
        }
        foreach(var handler in original.Body.ExceptionHandlers)
            method.Body.ExceptionHandlers.Add(new ExceptionHandler(handler.HandlerType){CatchType=handler.CatchType==null?null:module.ImportReference(handler.CatchType),TryStart=map[handler.TryStart],TryEnd=handler.TryEnd==null?null:map[handler.TryEnd],HandlerStart=map[handler.HandlerStart],HandlerEnd=handler.HandlerEnd==null?null:map[handler.HandlerEnd]});
        if(optional)
        {
            var target=module.Types.Single(t=>t.FullName=="ABLC.Patcher").Methods.Single(m=>m.Name=="PatchBuildingThemes");
            var first=target.Body.Instructions[0];
            var il=target.Body.GetILProcessor();
            il.InsertBefore(first,Instruction.Create(OpCodes.Call,method));
            il.InsertBefore(first,Instruction.Create(OpCodes.Brtrue,first));
            il.InsertBefore(first,Instruction.Create(OpCodes.Ret));
        }
        else
        {
            var sites=Types(module.Types).Where(t=>t.FullName.StartsWith("Transit.Framework.Prerequisites.ModPrerequisites",StringComparison.Ordinal)).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.Operand is MethodReference r && r.DeclaringType.FullName=="System.Reflection.Assembly" && r.Name=="GetTypes").ToArray();
            if(sites.Length!=1)throw new InvalidOperationException("Reviewed prerequisite scan site differs.");
            foreach(var site in sites){site.OpCode=OpCodes.Call;site.Operand=method;}
        }
        if(module.AssemblyReferences.Any(r=>r.Name==source.Assembly.Name.Name))throw new InvalidOperationException("Repair unexpectedly requires helper assembly.");
        module.Write(output);
        Console.WriteLine("Prerequisite scan repaired; SHA256="+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))));
    }
    static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> roots){foreach(var type in roots){yield return type;foreach(var nested in Types(type.NestedTypes))yield return nested;}}
}
