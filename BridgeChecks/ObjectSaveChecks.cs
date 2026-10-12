using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.SaveBridge;
using UnityEngine;

static class ObjectSaveChecks
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static void Run()
    {
        var tree = Singleton<TreeManager>.instance; var prop = Singleton<PropManager>.instance;
        for (int i = 1; i < tree.m_trees.m_buffer.Length; i++)
        {
            tree.m_trees.m_buffer[i].m_flags = 0xFC1;
            ObjectPositionHooks.TreeSet(ref tree.m_trees.m_buffer[i], new Vector3(-8600 + i % 19000, 3, 10559 - i % 19000));
        }
        for (int i = 1; i < prop.m_props.m_buffer.Length; i++)
        {
            prop.m_props.m_buffer[i].m_flags = 0x341;
            ObjectPositionHooks.PropSet(ref prop.m_props.m_buffer[i], new Vector3(-8600 + i % 19000, 5, 10559 - i % 19000));
        }
        byte[] binding = new byte[32]; binding[0] = 42;
        var originalTrees = (TreeInstance[])tree.m_trees.m_buffer.Clone(); var originalProps = (PropInstance[])prop.m_props.m_buffer.Clone();
        byte[] bytes = ObjectSaveBridge.Capture(binding);
        var projectedTrees = ObjectSaveBridge.WriteTrees(null); var projectedProps = ObjectSaveBridge.WriteProps(null);
        Check(!ReferenceEquals(projectedTrees, tree.m_trees.m_buffer), "Tree snapshot aliases live data");
        Check(projectedTrees.All(t => (t.m_flags & 0x3000) == 0) && projectedProps.All(p => (p.m_flags & 0x3000) == 0), "Vanilla snapshot retains markers");
        Array.Copy(projectedTrees, tree.m_trees.m_buffer, projectedTrees.Length); Array.Copy(projectedProps, prop.m_props.m_buffer, projectedProps.Length);
        // Prefab rebinding and fire normalization performed by the game must survive restoration.
        for (int i = 1; i < tree.m_trees.m_buffer.Length; i++)
        { tree.m_trees.m_buffer[i].m_infoIndex = 999; tree.m_trees.m_buffer[i].m_flags &= unchecked((ushort)~0xC0); }
        for (int i = 1; i < prop.m_props.m_buffer.Length; i++) prop.m_props.m_buffer[i].m_infoIndex = 777;
        var state = ObjectSaveBridge.Prepare(bytes, binding);
        Check(tree.m_trees.m_buffer[^1].m_flags == projectedTrees[^1].m_flags - 0xC0, "Prepare changed live flags");
        var broken = (byte[])bytes.Clone(); broken[400] ^= 1;
        try { ObjectSaveBridge.Prepare(broken, binding); throw new Exception("Corrupt object save accepted"); }
        catch (System.IO.InvalidDataException) { }
        var treeBuffer = tree.m_trees.m_buffer; var propBuffer = prop.m_props.m_buffer; var treeGrid = tree.m_treeGrid; var propGrid = prop.m_propGrid;
        ObjectSaveBridge.Commit(state);
        Check(ReferenceEquals(treeBuffer, tree.m_trees.m_buffer) && ReferenceEquals(propBuffer, prop.m_props.m_buffer) &&
            ReferenceEquals(treeGrid, tree.m_treeGrid) && ReferenceEquals(propGrid, prop.m_propGrid), "Restore replaced live array identities");
        var seenTrees = new bool[originalTrees.Length]; var seenProps = new bool[originalProps.Length];
        for (int cell = 0; cell < treeGrid.Length; cell++)
        for (uint id = treeGrid[cell]; id != 0; id = treeBuffer[id].m_nextGridTree)
        {
            Check(!seenTrees[id], "Tree linked-list loop or duplicate"); seenTrees[id] = true;
            var t = treeBuffer[id]; var a = originalTrees[id];
            Check(t.m_posX == a.m_posX && t.m_posZ == a.m_posZ && t.m_infoIndex == 999 && t.m_flags == (a.m_flags & ~0xC0), "Tree restored data");
        }
        for (int cell = 0; cell < propGrid.Length; cell++)
        for (ushort id = propGrid[cell]; id != 0; id = propBuffer[id].m_nextGridProp)
        {
            Check(!seenProps[id], "Prop linked-list loop or duplicate"); seenProps[id] = true;
            var p = propBuffer[id]; var a = originalProps[id];
            Check(p.m_posX == a.m_posX && p.m_posZ == a.m_posZ && p.m_infoIndex == 777 && p.m_flags == a.m_flags, "Prop restored data");
        }
        Check(seenTrees.Skip(1).All(v => v) && seenProps.Skip(1).All(v => v), "Objects missing from rebuilt grids");
        ObjectSaveBridge.ClearSave();
        try { ObjectSaveBridge.WriteTrees(null); throw new Exception("Expired snapshot accepted"); }
        catch (InvalidOperationException) { }
        Console.WriteLine("PASS: production object bridge against fixture managers; frozen vanilla projection; marker stripping; 327678 active objects restored once; prefab/fire preservation; live array identities; checksum refusal.");
    }
}
