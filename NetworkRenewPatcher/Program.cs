using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
if(args.Length==5 && args[0]=="--startup-repair"){StartupRepairs.Run(args[1],args[2],args[3],args[4]);return;}
if(args.Length==3 && args[0]=="--simulation-repair"){SimulationRepairs.Run(args[1],args[2]);return;}
if(args.Length!=3)throw new ArgumentException("<original Renew DLL> <new output DLL> <CitiesHarmony.Harmony.dll>");
if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0])))!="C4334AD302F232644E147EC6FC2397C0C328EF26A02BCA8E1CE71A5F0183338B")throw new InvalidOperationException("Unreviewed Renew version; patch refused.");
if(Path.GetFullPath(args[0])==Path.GetFullPath(args[1]) || File.Exists(args[1]))throw new InvalidOperationException("Use a new output file; originals are never overwritten.");
using var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
using var module=ModuleDefinition.ReadModule(args[0],new ReaderParameters{AssemblyResolver=resolver});
using var modern=ModuleDefinition.ReadModule(args[2]);
var api=modern.Types.Single(t=>t.FullName=="HarmonyLib.Harmony");
var create=api.Methods.Single(m=>m.IsConstructor && m.Parameters.Count==1 && m.Parameters[0].ParameterType.FullName=="System.String");
var patch=api.Methods.Single(m=>m.Name=="Patch" && m.Parameters.Count==5);
var reference=new AssemblyNameReference(modern.Assembly.Name.Name,modern.Assembly.Name.Version);
module.AssemblyReferences.Add(reference);
foreach(var type in module.GetTypeReferences())if(type.Namespace=="Harmony"){type.Namespace="HarmonyLib";if(type.Name=="HarmonyInstance")type.Name="Harmony";type.Scope=reference;}
IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> roots){foreach(var type in roots){yield return type;foreach(var nested in Types(type.NestedTypes))yield return nested;}}
int creates=0,patches=0;
foreach(var type in Types(module.Types))foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions.ToArray())
{
    if(instruction.Operand is not MethodReference target || target.DeclaringType.FullName!="HarmonyLib.Harmony")continue;
    if(target.Name=="Create"){instruction.OpCode=OpCodes.Newobj;instruction.Operand=module.ImportReference(create);creates++;}
    else if(target.Name=="Patch"){
        if(target.Parameters.Count!=4)throw new InvalidOperationException("Unreviewed legacy Patch signature.");
        var extra=Instruction.Create(OpCodes.Ldnull); method.Body.GetILProcessor().InsertBefore(instruction,extra);
        foreach(var jump in method.Body.Instructions){if(jump.Operand==instruction)jump.Operand=extra;else if(jump.Operand is Instruction[] targets)for(int i=0;i<targets.Length;i++)if(targets[i]==instruction)targets[i]=extra;}
        foreach(var handler in method.Body.ExceptionHandlers){if(handler.TryStart==instruction)handler.TryStart=extra;if(handler.TryEnd==instruction)handler.TryEnd=extra;if(handler.HandlerStart==instruction)handler.HandlerStart=extra;if(handler.HandlerEnd==instruction)handler.HandlerEnd=extra;if(handler.FilterStart==instruction)handler.FilterStart=extra;}
        instruction.Operand=module.ImportReference(patch);patches++;
    }
}
if(creates!=1 || patches!=16)throw new InvalidOperationException($"Unexpected patch sites: create={creates}, patch={patches}.");
foreach(var old in module.AssemblyReferences.Where(r=>r.Name=="0Harmony").ToArray())module.AssemblyReferences.Remove(old);
SimulationRepairs.FixRenew(module);
module.Write(args[1]);Console.WriteLine($"Reviewed Renew Harmony and signal-lookup repairs: create={creates}, patch={patches}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1])))}");




