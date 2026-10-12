using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.SaveBridge;
using UnityEngine;

static class ObjectPositionChecks
{
    static void Check(bool success, string message) { if (!success) throw new Exception(message); }
    public static void Run()
    {
        Singleton<ToolManager>.instance.m_properties.m_mode = ItemClass.Availability.Game;
        var properties=Singleton<ToolManager>.instance.m_properties;
        Singleton<ToolManager>.instance.m_properties=null;
        var loadingProp=new PropInstance{m_posX=17,m_posZ=29};Vector3 loadingPosition=default;
        Check(ObjectPositionHooks.PropX(ref loadingProp)==17 && !ObjectPositionHooks.PropGet(ref loadingProp,ref loadingPosition),"Packed object initialization before tool properties exist");
        Singleton<ToolManager>.instance.m_properties=properties;
        for (int i = -32767; i <= PackedObjectPosition.MaximumCode; i++)
        {
            var position = new Vector3(i * PackedObjectPosition.Step, 7.125f, i * PackedObjectPosition.Step);
            var tree = new TreeInstance { m_flags = 0xCFC1 };
            var prop = new PropInstance { m_flags = 0x83C1 };
            Check(!ObjectPositionHooks.TreeSet(ref tree, position) && !ObjectPositionHooks.PropSet(ref prop, position), "Setter forwarding");
            Check((tree.m_flags & ~ObjectPositionHooks.ExtensionMask) == 0xCFC1 && (prop.m_flags & ~ObjectPositionHooks.ExtensionMask) == 0x83C1, "Existing flags changed");
            var copiedTree = tree; var copiedProp = prop; Vector3 output = default;
            ObjectPositionHooks.TreeGet(ref copiedTree, ref output);
            Check(output.x == position.x && output.z == position.z && output.y == position.y, "Copied tree position");
            ObjectPositionHooks.PropGet(ref copiedProp, ref output);
            Check(output.x == position.x && output.z == position.z && output.y == position.y, "Copied prop position");
            Check(ObjectPositionHooks.TreeX(ref tree) == i && ObjectPositionHooks.PropZ(ref prop) == i, "Direct raw coordinate");
            ObjectPositionHooks.TreeSet(ref tree, new Vector3(0, 0, 0));
            Check((tree.m_flags & ObjectPositionHooks.ExtensionMask) == 0, "Moving inward retained markers");
        }
        var untouched = new TreeInstance { m_flags = 1, m_posX = 12, m_posZ = 34 };
        try { ObjectPositionHooks.TreeSet(ref untouched, new Vector3(10000, 0, float.NaN)); throw new Exception("Invalid position accepted"); }
        catch (ArgumentOutOfRangeException) { }
        Check(untouched.m_flags == 1 && untouched.m_posX == 12 && untouched.m_posZ == 34, "Failed setter partially committed");
        Singleton<ToolManager>.instance.m_properties.m_mode = ItemClass.Availability.AssetEditor;
        Vector3 ignored = default;
        Check(ObjectPositionHooks.TreeGet(ref untouched, ref ignored) && ObjectPositionHooks.TreeSet(ref untouched, ignored), "Asset editor delegation");
        Singleton<ToolManager>.instance.m_properties.m_mode = ItemClass.Availability.Game;
        Console.WriteLine("PASS: gated tree/prop position hooks; 72,817 lattice positions; struct copies; flags; inward moves; invalid setters; asset-editor delegation.");
    }
}

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}
public class ItemClass { public enum Availability { Game, AssetEditor } }
public class ToolManager { public ToolProperties m_properties = new ToolProperties(); }
public class ToolProperties { public ItemClass.Availability m_mode; }
public struct TreeInstance { public short m_posX, m_posZ; public ushort m_posY, m_flags, m_infoIndex; public uint m_nextGridTree; public Vector3 Position { get; set; } }
public struct PropInstance { public short m_posX, m_posZ; public ushort m_posY, m_flags, m_infoIndex; public ushort m_nextGridProp; public Vector3 Position { get; set; } }
public class ObjectArray<T> { public T[] m_buffer; public ObjectArray(int length) { m_buffer = new T[length]; } }
public class TreeManager { public ObjectArray<TreeInstance> m_trees = new ObjectArray<TreeInstance>(262144); public uint[] m_treeGrid = new uint[360000]; }
public class PropManager { public ObjectArray<PropInstance> m_props = new ObjectArray<PropInstance>(65536); public ushort[] m_propGrid = new ushort[90000]; }
