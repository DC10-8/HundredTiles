using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.Text;
string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
const string resourceName="HundredTiles.Runtime.ReviewedPluginVersions.tsv";
if(args[0]=="mutate"){
 using var input=ModuleDefinition.ReadModule(args[1]);
 var type=input.Types.Single(t=>t.FullName=="RealTimeSync.Harmony.TrainPassengerEventsPatch");
 var method=type.Methods.Single(m=>m.Name=="Prefix");method.Body.Instructions.Insert(0,Instruction.Create(OpCodes.Nop));input.Write(args[2]);return;
}
if(Hash(File.ReadAllBytes(args[0]))!="8E66BC3673D84E2C1152BE5CD4D9D10CE50E345A41014A7E343829A32103AB10")throw new Exception("Unexpected input runtime; refusing patch.");
using var module=ModuleDefinition.ReadModule(args[0]);
if(module.Assembly.Name.HasPublicKey)throw new Exception("Signed binary requires source rebuild.");
var resource=(EmbeddedResource)module.Resources.Single(r=>r.Name==resourceName);
string before=Encoding.UTF8.GetString(resource.GetResourceData());string addition=File.ReadAllText(args[1]);
if(before.Contains("17158C21A5CB1C1ACD48E24B2A31442044E0E7543754B58EBC8082E1D492D339"))throw new Exception("Already patched.");
module.Resources.Remove(resource);module.Resources.Add(new EmbeddedResource(resourceName,resource.Attributes,Encoding.UTF8.GetBytes(before.TrimEnd('\r','\n')+"\n"+addition.TrimEnd('\r','\n')+"\n")));
module.Write(args[2]);
using var output=ModuleDefinition.ReadModule(args[2]);
IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var nested in Types(t.NestedTypes))yield return nested;}}
string Method(MethodDefinition m)=>m.FullName+"|"+m.Attributes+"|"+m.ImplAttributes+"|"+(m.HasBody?string.Join(";",m.Body.Instructions.Select(i=>i.OpCode+":"+i.Operand)):"");
var oldMethods=Types(module.Types).SelectMany(t=>t.Methods).Select(Method).ToArray();
var newMethods=Types(output.Types).SelectMany(t=>t.Methods).Select(Method).ToArray();
if(!oldMethods.SequenceEqual(newMethods))throw new Exception("Runtime method changed.");
foreach(var r in module.Resources.OfType<EmbeddedResource>())if(!r.GetResourceData().SequenceEqual(((EmbeddedResource)output.Resources.Single(x=>x.Name==r.Name)).GetResourceData()))throw new Exception("Resource write differs: "+r.Name);
Console.WriteLine("PASS resource-only patch: "+oldMethods.Length+" method bodies/attributes unchanged; resource payloads verified.");
Console.WriteLine("SHA256 "+Hash(File.ReadAllBytes(args[2])));
