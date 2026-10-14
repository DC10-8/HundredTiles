using System;
using System.IO;
using ColossalFramework;
using HundredTiles.Core;

namespace HundredTiles.SaveBridge
{
    // ObjectParticipant connects this snapshot to the root save transaction,
    // restoring coordinates before child AfterDeserialize aggregation.
    public static class ObjectSaveBridge
    {
        static TreeInstance[] saveTrees;
        static PropInstance[] saveProps;
        public sealed class Prepared
        {
            internal TreeInstance[] Trees;
            internal PropInstance[] Props;
            internal uint[] TreeGrid;
            internal ushort[] PropGrid;
        }

        public static byte[] Capture(byte[] binding)
        {
            if (saveTrees != null || saveProps != null) throw new InvalidOperationException("Nested object capture.");
            var trees = (TreeInstance[])Singleton<TreeManager>.instance.m_trees.m_buffer.Clone();
            var props = (PropInstance[])Singleton<PropManager>.instance.m_props.m_buffer.Clone();
            byte[] bytes = ObjectPositionCodec.Encode(Pack(trees), Pack(props), binding);
            var projectedTrees = ObjectPositionCodec.Project(Pack(trees));
            var projectedProps = ObjectPositionCodec.Project(Pack(props));
            for (int i = 0; i < trees.Length; i++)
            { trees[i].m_posX = projectedTrees[i].X.Value; trees[i].m_posZ = projectedTrees[i].Z.Value; trees[i].m_flags = projectedTrees[i].Flags; }
            for (int i = 0; i < props.Length; i++)
            { props[i].m_posX = projectedProps[i].X.Value; props[i].m_posZ = projectedProps[i].Z.Value; props[i].m_flags = projectedProps[i].Flags; }
            saveTrees = trees; saveProps = props; return bytes;
        }

        public static TreeInstance[] WriteTrees(TreeInstance[] original)
        { if (saveTrees == null) throw new InvalidOperationException("Object save capture missing."); return saveTrees; }
        public static PropInstance[] WriteProps(PropInstance[] original)
        { if (saveProps == null) throw new InvalidOperationException("Object save capture missing."); return saveProps; }
        public static void ClearSave() { saveTrees = null; saveProps = null; }

        public static Prepared Prepare(byte[] bytes, byte[] binding)
        {
            var trees = (TreeInstance[])Singleton<TreeManager>.instance.m_trees.m_buffer.Clone();
            var props = (PropInstance[])Singleton<PropManager>.instance.m_props.m_buffer.Clone();
            ObjectPositionRecord[] treeRecords, propRecords;
            if (bytes == null)
            {
                treeRecords = Pack(trees); propRecords = Pack(props);
                foreach (var records in new[] { treeRecords, propRecords })
                    foreach (var record in records)
                        if (record.X.PositiveExtension || record.Z.PositiveExtension)
                            throw new InvalidDataException("Legacy objects contain reserved extension flags.");
            }
            else ObjectPositionCodec.Decode(bytes, Pack(trees), Pack(props), binding, out treeRecords, out propRecords);
            var state = new Prepared { Trees = trees, Props = props, TreeGrid = new uint[(ExpandedMap.GridSize*60) * (ExpandedMap.GridSize*60)], PropGrid = new ushort[(ExpandedMap.GridSize*30) * (ExpandedMap.GridSize*30)] };
            for (int i = 0; i < trees.Length; i++)
            {
                var record = treeRecords[i];
                trees[i].m_posX = record.X.Value; trees[i].m_posZ = record.Z.Value;
                trees[i].m_flags = RestoreFlags(trees[i].m_flags, record); trees[i].m_nextGridTree = 0;
                if (i == 0 || trees[i].m_flags == 0) continue;
                int index = PackedObjectPosition.GridCoordinate(record.Z, 32) * (ExpandedMap.GridSize*60) + PackedObjectPosition.GridCoordinate(record.X, 32);
                trees[i].m_nextGridTree = state.TreeGrid[index]; state.TreeGrid[index] = (uint)i;
            }
            for (int i = 0; i < props.Length; i++)
            {
                var record = propRecords[i];
                props[i].m_posX = record.X.Value; props[i].m_posZ = record.Z.Value;
                props[i].m_flags = RestoreFlags(props[i].m_flags, record); props[i].m_nextGridProp = 0;
                if (i == 0 || props[i].m_flags == 0) continue;
                int index = PackedObjectPosition.GridCoordinate(record.Z, 64) * (ExpandedMap.GridSize*30) + PackedObjectPosition.GridCoordinate(record.X, 64);
                props[i].m_nextGridProp = state.PropGrid[index]; state.PropGrid[index] = (ushort)i;
            }
            return state;
        }

        public static void ValidateTarget(Prepared state)
        {
            var tree = Singleton<TreeManager>.instance; var prop = Singleton<PropManager>.instance;
            if (state == null || tree.m_treeGrid.Length != (ExpandedMap.GridSize*60)*(ExpandedMap.GridSize*60) || prop.m_propGrid.Length != (ExpandedMap.GridSize*30)*(ExpandedMap.GridSize*30) ||
                tree.m_trees.m_buffer.Length != ObjectPositionCodec.TreeCapacity || prop.m_props.m_buffer.Length != ObjectPositionCodec.PropCapacity)
                throw new InvalidDataException("Object restore targets differ.");
        }

        public static void Commit(Prepared state)
        {
            ValidateTarget(state);
            var tree = Singleton<TreeManager>.instance; var prop = Singleton<PropManager>.instance;
            // Caller must hold the simulation in its load phase before counts,
            // render data or worker updates are rebuilt.
            lock (tree.m_treeGrid) lock (prop.m_propGrid)
            {
                Array.Copy(state.Trees, tree.m_trees.m_buffer, state.Trees.Length);
                Array.Copy(state.Props, prop.m_props.m_buffer, state.Props.Length);
                Array.Copy(state.TreeGrid, tree.m_treeGrid, state.TreeGrid.Length);
                Array.Copy(state.PropGrid, prop.m_propGrid, state.PropGrid.Length);
            }
        }

        static ushort RestoreFlags(ushort original, ObjectPositionRecord record)
        { return (ushort)((original & ~ObjectPositionHooks.ExtensionMask) | (record.X.PositiveExtension ? ObjectPositionHooks.XExtension : 0) | (record.Z.PositiveExtension ? ObjectPositionHooks.ZExtension : 0)); }
        static ObjectPositionRecord Record(short x, short z, ushort flags)
        { return new ObjectPositionRecord { Flags = (ushort)(flags & ~ObjectPositionHooks.ExtensionMask),
            X = new PackedObjectAxis { Value = x, PositiveExtension = (flags & ObjectPositionHooks.XExtension) != 0 },
            Z = new PackedObjectAxis { Value = z, PositiveExtension = (flags & ObjectPositionHooks.ZExtension) != 0 } }; }
        static ObjectPositionRecord[] Pack(TreeInstance[] trees)
        { var result = new ObjectPositionRecord[trees.Length]; for (int i = 0; i < result.Length; i++) result[i] = Record(trees[i].m_posX, trees[i].m_posZ, trees[i].m_flags); return result; }
        static ObjectPositionRecord[] Pack(PropInstance[] props)
        { var result = new ObjectPositionRecord[props.Length]; for (int i = 0; i < result.Length; i++) result[i] = Record(props[i].m_posX, props[i].m_posZ, props[i].m_flags); return result; }
    }
}


