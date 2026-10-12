using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 2) throw new ArgumentException("Usage: HundredTiles.Audit <Assembly-CSharp.dll> <output directory>");
string assemblyPath = Path.GetFullPath(args[0]);
string output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
using var stream = File.OpenRead(assemblyPath);
using var pe = new PEReader(stream);
var metadata = pe.GetMetadataReader();
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(op => unchecked((ushort)op.Value));
string TypeName(EntityHandle handle)
{
    switch (handle.Kind)
    {
        case HandleKind.TypeDefinition:
            var definition = metadata.GetTypeDefinition((TypeDefinitionHandle)handle);
            var parent = definition.GetDeclaringType();
            return parent.IsNil ? metadata.GetString(definition.Name) : TypeName(parent) + "+" + metadata.GetString(definition.Name);
        case HandleKind.TypeReference:
            var reference = metadata.GetTypeReference((TypeReferenceHandle)handle);
            return metadata.GetString(reference.Name);
        case HandleKind.TypeSpecification: return "<TypeSpec>";
        default: return "<" + handle.Kind + ">";
    }
}
string MemberName(EntityHandle handle)
{
    switch (handle.Kind)
    {
        case HandleKind.MemberReference:
            var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
            return TypeName(member.Parent) + "." + metadata.GetString(member.Name);
        case HandleKind.MethodDefinition:
            var method = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
            return TypeName(method.GetDeclaringType()) + "." + metadata.GetString(method.Name);
        case HandleKind.FieldDefinition:
            var field = metadata.GetFieldDefinition((FieldDefinitionHandle)handle);
            return TypeName(field.GetDeclaringType()) + "." + metadata.GetString(field.Name);
        case HandleKind.MethodSpecification:
            return MemberName(metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method);
        default: return TypeName(handle);
    }
}
var strong = new HashSet<double> { 1079,1080,1081,1166400,1168561,3505684,8640,-8640,17280,4319,4320,4321,4800,9600,1920,540,270,135 };
var weak = new HashSet<double> { 5,8,9,17,25,81,450,512,513,540,900,1024,2048,231361,230400 };
string[] families = { "Terrain", "WaterSimulation", "GameArea", "AreaTool", "CameraController", "District", "Electricity", "WaterManager", "NaturalResource", "ImmaterialResource", "Zone", "NetManager", "BuildingManager", "TreeManager", "PropManager", "Disaster", "PathManager", "OutsideConnection" };
bool Relevant(string type) => families.Any(type.Contains) || Path.GetFileName(assemblyPath)=="MoveIt.dll" || Path.GetFileName(assemblyPath)=="NoUtilityNetworks.dll";
var constants = new List<ConstantHit>();
var edges = new List<ReferenceHit>();
var methods = new List<MethodRecord>();
foreach (var typeHandle in metadata.TypeDefinitions)
{
    string typeName = TypeName(typeHandle);
    var type = metadata.GetTypeDefinition(typeHandle);
    foreach (var methodHandle in type.GetMethods())
    {
        var method = metadata.GetMethodDefinition(methodHandle);
        if (method.RelativeVirtualAddress == 0) continue;
        string methodName = metadata.GetString(method.Name);
        int token = MetadataTokens.GetToken(methodHandle);
        byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes() ?? Array.Empty<byte>();
        methods.Add(new(typeName, methodName, token, Convert.ToHexString(SHA256.HashData(il)), il.Length));
        for (int position = 0; position < il.Length;)
        {
            int offset = position;
            ushort code = il[position++];
            if (code == 0xfe) code = (ushort)(0xfe00 | il[position++]);
            if (!opcodes.TryGetValue(code, out var opcode)) throw new BadImageFormatException($"Unknown opcode {code:X4}");
            double? value = null;
            if (opcode == OpCodes.Ldc_I4_M1) value = -1;
            else if (code >= unchecked((ushort)OpCodes.Ldc_I4_0.Value) && code <= unchecked((ushort)OpCodes.Ldc_I4_8.Value))
                value = code - unchecked((ushort)OpCodes.Ldc_I4_0.Value);
            else if (opcode == OpCodes.Ldc_I4_S) value = (sbyte)il[position];
            else if (opcode == OpCodes.Ldc_I4) value = BitConverter.ToInt32(il, position);
            else if (opcode == OpCodes.Ldc_R4) value = BitConverter.ToSingle(il, position);
            else if (opcode == OpCodes.Ldc_R8) value = BitConverter.ToDouble(il, position);
            if (value.HasValue && (strong.Contains(value.Value) || (Relevant(typeName) && weak.Contains(value.Value))))
                constants.Add(new(typeName, methodName, token, offset, value.Value, strong.Contains(value.Value) ? "spatial-candidate" : "context-required"));
            if (opcode.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
            {
                string target = MemberName(MetadataTokens.EntityHandle(BitConverter.ToInt32(il, position)));
                if (Relevant(target) || Relevant(typeName)) edges.Add(new(typeName, methodName, token, offset, target));
            }
            int size = opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => checked(4 + BitConverter.ToInt32(il, position) * 4),
                _ => 4
            };
            position = checked(position + size);
            if (position > il.Length) throw new BadImageFormatException($"Truncated IL: {typeName}.{methodName}");
        }
    }
}
string Csv(params object[] values) => string.Join(",", values.Select(v => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture)!.Replace("\"", "\"\"") + "\""));
File.WriteAllLines(Path.Combine(output, "constants.csv"), new[] { "type,method,token,il_offset,value,classification" }
    .Concat(constants.Select(c => Csv(c.Type,c.Method,$"0x{c.Token:X8}",$"0x{c.Offset:X4}",c.Value,c.Classification))));
File.WriteAllLines(Path.Combine(output, "references.csv"), new[] { "type,method,token,il_offset,target" }
    .Concat(edges.Select(e => Csv(e.Type,e.Method,$"0x{e.Token:X8}",$"0x{e.Offset:X4}",e.Target))));
var manifest = new {
    Schema = 1, Assembly = Path.GetFileName(assemblyPath),
    Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))),
    Mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid),
    MethodsScanned = methods.Count, ConstantCandidates = constants.Count, References = edges.Count,
    Coverage = "Managed method bodies in the input assembly only; numeric candidates require semantic review. Shaders, other assemblies and native code are not covered.",
    Methods = methods.Where(m => Relevant(m.Type)).ToArray()
};
File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllLines(Path.Combine(output, "summary.md"), new[] {
    "# Managed IL audit", "", $"Assembly SHA256: `{manifest.Sha256}`", "",
    $"Scanned {methods.Count} method bodies; {constants.Count} numeric candidates; {edges.Count} relevant references.", "",
    "Numeric matches are audit candidates, not approved replacements. No game code was modified.", "",
    "| Type | Candidate constants |", "| --- | ---: |"
}.Concat(constants.GroupBy(c => c.Type).OrderByDescending(g => g.Count()).Select(g => $"| {g.Key} | {g.Count()} |")));
Console.WriteLine($"Scanned {methods.Count} methods; {constants.Count} constants; {edges.Count} references. Reports: {output}");
record ConstantHit(string Type, string Method, int Token, int Offset, double Value, string Classification);
record ReferenceHit(string Type, string Method, int Token, int Offset, string Target);
record MethodRecord(string Type, string Method, int Token, string Sha256, int ILBytes);
