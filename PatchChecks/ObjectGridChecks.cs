extern alias ExternalCecil;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using Cecil = ExternalCecil::Mono.Cecil;
using Cil = ExternalCecil::Mono.Cecil.Cil;

static class ObjectGridChecks
{
    public struct Fixture { public short m_posX, m_posZ; public ushort flags; }
    delegate int Index(object manager, uint id, ref Fixture data, bool editor);
    public static int X(ref Fixture data) => data.m_posX + ((data.flags & 0x1000) != 0 ? 65536 : 0);
    public static int Z(ref Fixture data) => data.m_posZ + ((data.flags & 0x2000) != 0 ? 65536 : 0);
    public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    public class BufferFixture { public Fixture[] m_buffer = new Fixture[1]; }
    public class BurningFixture { public int[] m_buffer = new int[1]; }
    public static Fixture[] Project(Fixture[] buffer) => buffer;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); }

    public static void Run(Cecil.ModuleDefinition module)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(op => unchecked((ushort)op.Value));
        foreach (string manager in new[] { "TreeManager", "PropManager" })
        foreach (string operation in new[] { "Initialize", "Finalize", "Update" })
        {
            string noun = manager == "TreeManager" ? "Tree" : "Prop";
            var method = module.Types.Single(t => t.Name == manager).Methods.Single(m => m.Name == operation + noun + (operation=="Update" ? "Renderer" : ""));
            var dynamic = new DynamicMethod("ObjectGrid", typeof(int), new[] { typeof(object), typeof(uint), typeof(Fixture).MakeByRefType(), typeof(bool) }, typeof(ObjectGridChecks).Module, true);
            var il = dynamic.GetILGenerator();
            var labels = method.Body.Instructions.ToDictionary(i => i, _ => il.DefineLabel());
            var input = method.Body.Instructions.Select(i =>
            {
                object operand = i.Operand;
                if (operand is Cil.Instruction target) operand = labels[target];
                else if (operand is Cecil.FieldReference field && field.DeclaringType.Name == noun + "Instance" && (field.Name == "m_posX" || field.Name == "m_posZ"))
                    operand = typeof(Fixture).GetField(field.Name);
                else if (operand is Cecil.MethodReference call && call.DeclaringType.Name == "Mathf" && call.Name == "Clamp")
                    operand = typeof(ObjectGridChecks).GetMethod("Clamp");
                var code = new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)], operand); code.labels.Add(labels[i]); return code;
            }).ToList();
            int oldSide = noun == "Tree" ? 540 : 270, side = oldSide / 9 * 10;
            var output = ObjectGridTranspiler.Transform(input, oldSide, typeof(Fixture), typeof(ObjectGridChecks).GetMethod("X"), typeof(ObjectGridChecks).GetMethod("Z"),operation=="Update");
            Check(output.Count == input.Count, "Object grid instruction count");
            for (int i = 0; i < input.Count; i++)
                Check(!ReferenceEquals(input[i], output[i]) && input[i].labels.SequenceEqual(output[i].labels), "Object grid input/labels");
            var broken = input.Select(i => new CodeInstruction(i)).ToList();
            broken.First(i => i.opcode == OpCodes.Ldc_I4 && Equals(i.operand, oldSide - 1)).operand = 42;
            try { ObjectGridTranspiler.Transform(broken, oldSide, typeof(Fixture), typeof(ObjectGridChecks).GetMethod("X"), typeof(ObjectGridChecks).GetMethod("Z"),operation=="Update"); throw new Exception("Changed object grid accepted"); }
            catch (InvalidOperationException) { }
            if (operation != "Initialize") continue; // Finalize has singleton mode lookup; only its shape is checked here.
            for (int i = 0; i < 3; i++) il.DeclareLocal(typeof(int));
            int end = output.ToList().FindIndex(c => c.opcode == OpCodes.Stloc_2);
            for (int i = 0; i <= end; i++)
            {
                var code = output[i]; foreach (var label in code.labels) il.MarkLabel(label);
                if (code.operand == null) il.Emit(code.opcode);
                else if (code.operand is int number) il.Emit(code.opcode, number);
                else if (code.operand is sbyte small) il.Emit(code.opcode, small);
                else if (code.operand is Label label) il.Emit(code.opcode, label);
                else if (code.operand is FieldInfo field) il.Emit(code.opcode, field);
                else if (code.operand is MethodInfo call) il.Emit(code.opcode, call);
                else throw new Exception("Unhandled object prefix operand");
            }
            il.Emit(OpCodes.Ldloc_2); il.Emit(OpCodes.Ret);
            var index = (Index)dynamic.CreateDelegate(typeof(Index));
            for (int z = 0; z < side; z++) for (int x = 0; x < side; x++)
            {
                int rawX = (int)Math.Round(((x + .5) * 19200 / side - 8640) / .263671875);
                int rawZ = (int)Math.Round(((z + .5) * 19200 / side - 8640) / .263671875);
                var data = new Fixture { m_posX = unchecked((short)rawX), m_posZ = unchecked((short)rawZ), flags = (ushort)((rawX > 32767 ? 0x1000 : 0) | (rawZ > 32767 ? 0x2000 : 0)) };
                Check(index(null, 1, ref data, false) == z * side + x, "Object spatial cell identity");
            }
            for (int code = -32767; code <= 32767; code++)
            {
                var data = new Fixture { m_posX = (short)code, m_posZ = (short)-code };
                int x = Clamp((code / 16 + 32768) * oldSide / 65536, 0, oldSide - 1);
                int z = Clamp((-code / 16 + 32768) * oldSide / 65536, 0, oldSide - 1);
                Check(index(null, 1, ref data, true) == z * side + x, "Asset grid coordinates changed");
            }
        }
        Console.WriteLine("PASS: six actual tree/prop insertion/removal/renderer IL transformations; initialization coordinate prefixes across 360000/90000 cells; editor coordinates; malformed body rejection. Full linked-list routines and Unity not executed.");
        foreach (string manager in new[] { "TreeManager", "PropManager" })
        {
            var serialize = module.Types.Single(t => t.Name == manager).NestedTypes.Single(t => t.Name == "Data").Methods.Single(m => m.Name == "Serialize");
            var input = serialize.Body.Instructions.Select(i =>
            {
                object operand = i.Operand;
                if (operand is Cecil.FieldReference field && field.Name == "m_buffer" && field.DeclaringType is Cecil.GenericInstanceType generic)
                    operand = generic.GenericArguments[0].Name == (manager == "TreeManager" ? "TreeInstance" : "PropInstance")
                        ? typeof(BufferFixture).GetField("m_buffer") : typeof(BurningFixture).GetField("m_buffer");
                return new CodeInstruction(opcodes[unchecked((ushort)i.OpCode.Value)], operand);
            }).ToList();
            int target = input.FindIndex(i => i.operand is FieldInfo field && field.DeclaringType == typeof(BufferFixture));
            Check(target >= 0, "Object serializer fixture field resolution");
            input[target].blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
            var output = SerializerTranspiler.TransformBuffer(input, typeof(Fixture[]), typeof(ObjectGridChecks).GetMethod("Project"));
            Check(output.Count == input.Count + 1 && output.Count(i => i.operand is MethodInfo call && call.Name == "Project") == 1, "Object snapshot injection count");
            Check(input[target].blocks.Count == 1 && output[target].blocks.Count == 0 && output[target + 1].blocks.Count == 1, "Object serializer exception boundary");
            foreach (var bad in new[] { input.Where((_, i) => i != target).ToList(), input.Concat(new[] { input[target] }).ToList() })
            {
                try { SerializerTranspiler.TransformBuffer(bad, typeof(Fixture[]), typeof(ObjectGridChecks).GetMethod("Project")); throw new Exception("Malformed object serializer accepted"); }
                catch (InvalidOperationException) { }
            }
        }
        Console.WriteLine("PASS: two actual object serializer IL buffers projected; burning-list buffer excluded; exception-boundary transfer; missing/duplicate buffer refusal. No game serialization executed.");
    }
}
