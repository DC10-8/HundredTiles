using System;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

static class LiveEditorUndoChecks
{
    public static void Run()
    {
        var terrain=Singleton<TerrainManager>.instance;
        if(terrain.RawHeights.Length!=1081*1081 || HundredTiles.Runtime.Installer.IsInstalled)throw new Exception("Map editor must retain native dimensions and unpatched city state.");
        var before=(ushort[])terrain.RawHeights.Clone();var tool=ToolsModifierControl.SetTool<TerrainTool>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var controller=(ToolController)typeof(ToolBase).GetField("m_toolController",flags).GetValue(tool);
        var brush=controller.BrushData;var previous=(float[])brush.Clone();
        try
        {
            for(int i=0;i<brush.Length;i++)brush[i]=1;
            const int cell=665;float height=before[cell*1081+cell]/64f+4;
            tool.ResetUndoBuffer();tool.m_mode=TerrainTool.Mode.Level;tool.m_brushSize=32;tool.m_strength=1;
            LiveTerrainToolChecks.PointBrush(tool,new Vector3(2000,height,2000),height);
            typeof(TerrainTool).GetMethod("ApplyBrush",flags).Invoke(tool,null);typeof(TerrainTool).GetMethod("EndStroke",flags).Invoke(tool,null);
            if(terrain.RawHeights[cell*1081+cell]!=(ushort)Mathf.RoundToInt(height*64) || !tool.IsUndoAvailable())throw new Exception("Real map editor brush/Undo recording failed.");
            tool.ApplyUndo();
            for(int i=0;i<before.Length;i++)if(terrain.RawHeights[i]!=before[i])throw new Exception("Real map editor Undo missed native sample "+i);
            Entry.Log("PASS: real map editor scene, native terrain stroke/EndStroke and ApplyUndo restored all 1,168,561 samples while HundredTiles remained enabled with city patches inactive. Physical mouse/keyboard input is not asserted.");
        }
        finally{Array.Copy(before,terrain.RawHeights,before.Length);Array.Copy(previous,brush,brush.Length);tool.ResetUndoBuffer();TerrainModify.RefreshAllModifications();}
    }
}
