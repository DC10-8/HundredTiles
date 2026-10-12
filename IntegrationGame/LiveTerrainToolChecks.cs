using System;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

static class LiveTerrainToolChecks
{
    static TerrainTool tool;
    public static void Prepare()
    {
        tool=ToolsModifierControl.SetTool<TerrainTool>();
        if(tool==null)throw new Exception("Native landscaping tool missing.");
    }
    public static void Run()
    {
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_TERRAIN")=="1"){LiveLandscapingCompatibilityChecks.Run(tool);return;}
        var terrain=Singleton<TerrainManager>.instance;
        var before=(ushort[])terrain.RawHeights.Clone();int dirt=terrain.DirtBuffer;
        tool.ResetUndoBuffer();
        for(int i=0;i<before.Length;i++)if(terrain.BackupHeights[i]!=before[i])throw new Exception("Expanded landscaping backup differs at "+i);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var controller=(ToolController)typeof(ToolBase).GetField("m_toolController",flags).GetValue(tool);
        var brush=controller.BrushData;var previous=(float[])brush.Clone();for(int i=0;i<brush.Length;i++)brush[i]=1;
        try
        {
            tool.m_mode=TerrainTool.Mode.Level;tool.m_brushSize=32;tool.m_strength=1;
            typeof(TerrainTool).GetField("m_mousePosition",flags).SetValue(tool,new Vector3(9840,80,9840));
            typeof(TerrainTool).GetField("m_startPosition",flags).SetValue(tool,new Vector3(9840,76,9840));
            typeof(TerrainTool).GetMethod("ApplyBrush",flags).Invoke(tool,null);
            int index=1155*1201+1155;
            if(terrain.RawHeights[index]!=4864)throw new Exception("Actual outer landscaping brush missed the requested world point: "+terrain.RawHeights[index]);
            typeof(TerrainTool).GetMethod("EndStroke",flags).Invoke(tool,null);
            Entry.Log("PASS: actual city landscaping brush edits the added corner at its original world origin; all 1442401 backup samples verified.");
        }
        finally
        {
            Array.Copy(before,terrain.RawHeights,before.Length);terrain.DirtBuffer=dirt;Array.Copy(previous,brush,brush.Length);
            tool.ResetUndoBuffer();TerrainModify.UpdateArea(9800f,9800f,9900f,9900f,true,true,true);
        }
    }
}
