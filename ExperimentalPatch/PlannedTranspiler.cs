using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;

namespace HundredTiles.ExperimentalPatch
{
    public sealed class Edit
    {
        public int Index;
        public string Kind;
        public int Expected;
        public int Replacement;
    }

    public sealed class MethodPlan
    {
        public int Token;
        public string Name;
        public string BodyHash;
        public int InstructionCount;
        public readonly List<Edit> Edits = new List<Edit>();
    }

    public sealed class PatchPlan
    {
        public string AssemblyHash;
        public readonly Dictionary<int, MethodPlan> Methods = new Dictionary<int, MethodPlan>();
        // Activation is scoped to the reviewed city runtime and pinned game.
        // An incomplete or unrecognised catalog never authorizes installation.
        public bool CanActivate { get; private set; }

        public static PatchPlan Load(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            var plan = new PatchPlan();
            bool coverageDeclared = false;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                string[] parts = line.Split('\t');
                if (parts[0] == "assembly" && parts.Length == 2 && plan.AssemblyHash == null) plan.AssemblyHash = parts[1];
                else if (parts[0] == "coverage" && parts.Length == 2 && (parts[1] == "incomplete" || parts[1] == "city-runtime") && !coverageDeclared) { coverageDeclared = true; plan.CanActivate=parts[1]=="city-runtime"; }
                else if (parts[0] == "method" && parts.Length == 5)
                {
                    int token = int.Parse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    plan.Methods.Add(token, new MethodPlan { Token=token, Name=parts[2], InstructionCount=int.Parse(parts[3],CultureInfo.InvariantCulture), BodyHash=parts[4] });
                }
                else if (parts[0] == "edit" && parts.Length == 6)
                {
                    int token = int.Parse(parts[1],NumberStyles.HexNumber,CultureInfo.InvariantCulture);
                    plan.Methods[token].Edits.Add(new Edit { Index=int.Parse(parts[2],CultureInfo.InvariantCulture), Kind=parts[3], Expected=int.Parse(parts[4],CultureInfo.InvariantCulture), Replacement=int.Parse(parts[5],CultureInfo.InvariantCulture) });
                }
                else throw new FormatException("Invalid plan record: " + parts[0]);
            }
            if (!coverageDeclared || !ValidHash(plan.AssemblyHash) || plan.Methods.Count == 0) throw new FormatException("Incomplete plan header");
            foreach (var method in plan.Methods.Values)
            {
                var indices = new HashSet<int>();
                if (method.InstructionCount <= 0 || !ValidHash(method.BodyHash) || method.Edits.Count == 0) throw new FormatException("Invalid method plan");
                foreach (var edit in method.Edits)
                    if (edit.Index < 0 || edit.Index >= method.InstructionCount || !indices.Add(edit.Index) || (edit.Kind != "int" && edit.Kind != "local" && edit.Kind != "float"))
                        throw new FormatException("Invalid or duplicate edit");
            }
            return plan;
        }

        static bool ValidHash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }

        public static PatchPlan Embedded()
        {
            using (var stream = typeof(PatchPlan).Assembly.GetManifestResourceStream("HundredTiles.ExperimentalPatch.ReviewedPlan.tsv"))
            {
                if (stream == null) throw new InvalidOperationException("Embedded plan missing");
                using (var reader = new StreamReader(stream)) return Load(reader);
            }
        }
    }

    public static class PlannedTranspiler
    {
        public static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }

        public static IList<CodeInstruction> Transform(IEnumerable<CodeInstruction> source, MethodPlan plan)
        {
            if (source == null || plan == null) throw new ArgumentNullException();
            var input = new List<CodeInstruction>(source);
            if (input.Count != plan.InstructionCount) throw new InvalidOperationException("Instruction count differs: " + plan.Name);
            // Validate all edits before producing any transformed instructions.
            foreach (var edit in plan.Edits)
            {
                if (edit.Index < 0 || edit.Index >= input.Count) throw new InvalidOperationException("Edit out of bounds");
                CodeInstruction instruction = input[edit.Index];
                int? value = edit.Kind == "int" ? IntConstant(instruction) : edit.Kind == "float" ? FloatBits(instruction) : LocalIndex(instruction);
                if (value != edit.Expected) throw new InvalidOperationException("Unexpected instruction at " + plan.Name + " #" + edit.Index);
            }
            var output = new List<CodeInstruction>();
            foreach (var instruction in input) output.Add(new CodeInstruction(instruction));
            foreach (var edit in plan.Edits)
            {
                output[edit.Index].opcode = edit.Kind=="float" ? OpCodes.Ldc_R4 : OpCodes.Ldc_I4;
                output[edit.Index].operand = edit.Kind=="float" ? (object)BitConverter.ToSingle(BitConverter.GetBytes(edit.Replacement),0) : edit.Replacement;
            }
            // Copy construction preserves labels/exception markers; never mutate
            // input instructions owned by Harmony or by another transpiler.
            return output;
        }

        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var catalog = PatchPlan.Embedded();
            MethodPlan plan;
            plan=ReviewedMethod.Plan(__originalMethod,catalog);
            MethodBody body = __originalMethod.GetMethodBody();
            if (body == null || (Hash(body.GetILAsByteArray()) != plan.BodyHash && !ReviewedMethod.Equivalent(ReviewedMethod.Reference(__originalMethod.Module.Assembly,plan.Token),__originalMethod))) throw new InvalidOperationException("Method body version mismatch");
            using (var file = File.OpenRead(AssemblyFile.Resolve(__originalMethod.Module.Assembly)))
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "") != catalog.AssemblyHash)
                    throw new InvalidOperationException("Game assembly version mismatch");
            return Transform(instructions, plan);
        }

        static int? IntConstant(CodeInstruction i)
        {
            if (i.opcode == OpCodes.Ldc_I4_M1) return -1;
            if (i.opcode.Value >= OpCodes.Ldc_I4_0.Value && i.opcode.Value <= OpCodes.Ldc_I4_8.Value) return i.opcode.Value - OpCodes.Ldc_I4_0.Value;
            if (i.opcode == OpCodes.Ldc_I4 || i.opcode == OpCodes.Ldc_I4_S) return Convert.ToInt32(i.operand,CultureInfo.InvariantCulture);
            return null;
        }
        static int? FloatBits(CodeInstruction i)
        { return i.opcode==OpCodes.Ldc_R4 && i.operand is float ? (int?)BitConverter.ToInt32(BitConverter.GetBytes((float)i.operand),0) : null; }

        static int? LocalIndex(CodeInstruction i)
        {
            if (i.opcode == OpCodes.Ldloc_0) return 0;
            if (i.opcode == OpCodes.Ldloc_1) return 1;
            if (i.opcode == OpCodes.Ldloc_2) return 2;
            if (i.opcode == OpCodes.Ldloc_3) return 3;
            if (i.opcode != OpCodes.Ldloc && i.opcode != OpCodes.Ldloc_S) return null;
            LocalVariableInfo local = i.operand as LocalVariableInfo;
            return local != null ? local.LocalIndex : Convert.ToInt32(i.operand,CultureInfo.InvariantCulture);
        }
    }
}
