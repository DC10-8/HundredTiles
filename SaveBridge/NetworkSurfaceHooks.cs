using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using UnityEngine;
using HundredTiles.Core;

namespace HundredTiles.SaveBridge
{
    /// <summary>
    /// Some network tools complete NetTool.CreateNode without leaving the
    /// terrain pavement mask refreshed in expanded tiles. Rebuild only the
    /// surface mask for a newly-created ground segment when its center is
    /// missing the native clip/pavement data. Height and water remain native.
    /// </summary>
    public static class NetworkSurfaceHooks
    {
        const float LegacyMax = -8640f + 10f * ExpandedMap.TileSize;

        public static HookSpec Specification()
        {
            MethodInfo target = null;
            foreach (var candidate in typeof(NetTool).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (candidate.Name != "CreateNode") continue;
                var parameters = candidate.GetParameters();
                // The native overload used by the interactive tools. The
                // overload with testEnds has one additional bool parameter.
                if (parameters.Length == 17 && parameters[6].ParameterType == typeof(bool) &&
                    parameters[7].ParameterType == typeof(bool) && parameters[14].IsOut &&
                    parameters[14].ParameterType == typeof(ushort).MakeByRefType())
                { target = candidate; break; }
            }
            if (target == null) throw new MissingMethodException("NetTool.CreateNode interactive overload");
            SerializerHooks.Verify(target);
            return new HookSpec { Target = target, Postfix = typeof(NetworkSurfaceHooks).GetMethod("AfterCreate") };
        }

        public static void AfterCreate(NetInfo info, bool test, bool visualize,
            ref ushort segment, ToolBase.ToolErrors __result)
        {
            if (ExpandedMap.GridSize != 18 || test || visualize || __result != ToolBase.ToolErrors.None || segment == 0 || info == null)
                return;
            var manager = Singleton<NetManager>.instance;
            if (manager == null || segment >= manager.m_segments.m_buffer.Length) return;
            var data = manager.m_segments.m_buffer[segment];
            var actualInfo = data.Info;
            if (data.m_flags == NetSegment.Flags.None || actualInfo == null) return;
            if (!(actualInfo.m_createPavement || actualInfo.m_createGravel || actualInfo.m_createRuining || actualInfo.m_clipTerrain)) return;
            if (actualInfo.m_netAI != null && actualInfo.m_netAI.IsUnderground()) return;
            Bounds bounds = data.m_bounds;
            if (bounds.max.x <= LegacyMax && bounds.max.z <= LegacyMax) return;

            var middle = data.m_middlePosition;
            int x = Mathf.Clamp((int)((middle.x - ExpandedMap.WorldMin) / 4f), 0, ExpandedMap.GridSize * 480 - 1);
            int z = Mathf.Clamp((int)((middle.z - ExpandedMap.WorldMin) / 4f), 0, ExpandedMap.GridSize * 480 - 1);
            var terrain = Singleton<TerrainManager>.instance;
            if (terrain == null) return;
            // Interactive construction runs inside SimulationManager's terrain
            // batch. Its derived cells are populated by EndUpdateArea; an early
            // sample is not a missing surface and must not trigger a refresh.
            if (terrain.m_modifyingLevel != 0) return;
            var cell = terrain.GetSurfaceCell(x, z);
            bool missingClip = actualInfo.m_clipTerrain && cell.m_clipped == 0;
            bool missingFinish = (actualInfo.m_createPavement && cell.m_pavementA == 0 && cell.m_pavementB == 0) ||
                (actualInfo.m_createGravel && cell.m_gravel == 0) ||
                (actualInfo.m_createRuining && cell.m_ruined == 0);
            if (!missingClip && !missingFinish) return;

            // Re-run the game's own network-to-terrain rasterizer for this
            // segment. It updates the native surface cells and GPU patch data.
            TerrainModify.UpdateArea(bounds.min.x - 2f, bounds.min.z - 2f,
                bounds.max.x + 2f, bounds.max.z + 2f, false, true, false);
            cell = terrain.GetSurfaceCell(x, z);
            if ((actualInfo.m_clipTerrain && cell.m_clipped == 0) ||
                (actualInfo.m_createPavement && cell.m_pavementA == 0 && cell.m_pavementB == 0) ||
                (actualInfo.m_createGravel && cell.m_gravel == 0) ||
                (actualInfo.m_createRuining && cell.m_ruined == 0))
                Debug.LogWarning("[HundredTiles] Native road surface refresh still has an empty center cell for segment " + segment + " (" + middle.x + ", " + middle.z + ").");
            else
                Debug.Log("[HundredTiles] Refreshed missing expanded network surface for segment " + segment + ".");
        }
    }
}
