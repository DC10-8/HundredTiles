extern alias ExternalCecil;
using Cecil = ExternalCecil::Mono.Cecil;
using Cil = ExternalCecil::Mono.Cecil.Cil;
using HundredTiles.ExperimentalPatch;

static class UtilityWiringChecks
{
    static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    public static void Run(Cecil.ModuleDefinition game, PatchPlan plan)
    {
        using var utilities = Cecil.ModuleDefinition.ReadModule("Utilities/bin/Release/net35/HundredTiles.Utilities.dll");
        var hooks = utilities.Types.Single(t => t.FullName == "HundredTiles.Utilities.UtilityHooks");
        foreach (var pair in new[] {
            ("ElectricityStep", "ElectricityManager", "SimulationStepImpl"),
            ("ElectricityUpdate", "ElectricityManager", "UpdateGrid"),
            ("WaterStep", "WaterManager", "SimulationStepImpl") })
        {
            var prefix = hooks.Methods.Single(m => m.Name == pair.Item1);
            var owner = game.Types.Single(t => t.Name == pair.Item2);
            var target = owner.Methods.Single(m => m.Name == pair.Item3);
            foreach (var parameter in prefix.Parameters)
            {
                if (parameter.Name.StartsWith("___"))
                {
                    var field = owner.Fields.Single(f => f.Name == parameter.Name[3..]);
                    var expected = parameter.ParameterType is Cecil.ByReferenceType reference ? reference.ElementType : parameter.ParameterType;
                    Check(field.FieldType.FullName == expected.FullName, "Utility private-field injection type");
                }
                else if (parameter.Name == "__instance") Check(parameter.ParameterType.FullName == owner.FullName, "Utility instance type");
                else Check(target.Parameters.Any(p => p.Name == parameter.Name && p.ParameterType.FullName == parameter.ParameterType.FullName), "Utility method-argument injection");
            }
        }
        int axes = 0;
        foreach (var type in utilities.Types.Where(t => t.Name == "ExpandedElectricityManager" || t.Name == "ExpandedWaterManager"))
        foreach (var structure in type.NestedTypes.Where(t => t.Name.Contains("ExpandedPulse")))
        foreach (var field in structure.Fields.Where(f => f.Name == "m_x" || f.Name == "m_z"))
        { Check(field.FieldType.MetadataType == Cecil.MetadataType.UInt16, "Expanded supply queue still byte-sized"); axes++; }
        Check(axes == 6, "All three expanded supply structs expose both axes");
        var update = game.Types.Single(t => t.Name == "WaterManager").Methods.Single(m => m.Name == "UpdateGrid");
        var updatePlan = plan.Methods[update.MetadataToken.ToInt32()];
        var saturation = update.Body.Instructions.Select((i, index) => (i, index))
            .Where(p => p.i.OpCode.Code == Cil.Code.Ldc_I4 && Equals(p.i.Operand, 255)).Select(p => p.index).ToArray();
        Check(saturation.Length > 2 && updatePlan.Edits.Count(e => e.Expected == 255 && e.Kind == "int") == 2, "Conductivity saturation was changed with coordinate bounds");
        Check(saturation.Count(i => !updatePlan.Edits.Any(e => e.Index == i)) == saturation.Length - 2, "Water grid byte saturation not retained");
        var texture = game.Types.Single(t => t.Name == "WaterManager").Methods.Single(m => m.Name == "UpdateTexture");
        var texturePlan = plan.Methods[texture.MetadataToken.ToInt32()];
        Check(texturePlan.Edits.Count(e => e.Kind == "int" && e.Expected == 2048 && e.Replacement == 3696) == 4, "Pipe texture origin offsets");
        Check(!texturePlan.Edits.Any(e => e.Kind == "int" && e.Expected == 128), "Pipe relative-coordinate normalization changed");
        Console.WriteLine("PASS: three utility prefix connections match real private fields and method parameters; three expanded supply structs use ushort axes; conductivity saturation and pipe relative coordinates retained. Utility simulation not executed.");
    }
}
