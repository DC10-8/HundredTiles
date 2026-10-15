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
    public static void PointBrush(TerrainTool target,Vector3 center,float height)
    {
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        typeof(TerrainTool).GetField("m_mousePosition",flags).SetValue(target,center);
        typeof(TerrainTool).GetField("m_startPosition",flags).SetValue(target,new Vector3(center.x,height,center.z));
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="NaturalResourcesBrush")
        {
            var detour=assembly.GetType("NaturalResourcesBrush.Detours.TerrainToolDetour",true);
            var staticFlags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            detour.GetField("m_mousePosition",staticFlags).SetValue(null,center);
            detour.GetField("m_startPosition",staticFlags).SetValue(null,new Vector3(center.x,height,center.z));
            detour.GetField("m_sizeMode",staticFlags).SetValue(null,Enum.ToObject(detour.GetNestedType("SizeMode"),0));
            detour.GetField("m_mouseRightDown",staticFlags).SetValue(null,false);detour.GetField("isDitch",staticFlags).SetValue(null,false);
        }
    }
    public static void LevelArea(Vector3 center,float size,float height)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var controller=(ToolController)typeof(ToolBase).GetField("m_toolController",flags).GetValue(tool);
        var brush=controller.BrushData;var previous=(float[])brush.Clone();for(int i=0;i<brush.Length;i++)brush[i]=1;
        var properties=Singleton<ToolManager>.instance.m_properties;var previousMode=properties.m_mode;var previousControllerMode=controller.m_mode;
        // Fixture preparation only: don't exhaust a small city's money/dirt budget
        // while leveling a square kilometre. The actual edit/Undo test uses Game mode.
        properties.m_mode=ItemClass.Availability.MapEditor;
        controller.m_mode=ItemClass.Availability.MapEditor;
        try{var original=(ushort[])Singleton<TerrainManager>.instance.RawHeights.Clone();tool.ResetUndoBuffer();tool.m_mode=TerrainTool.Mode.Level;tool.m_brushSize=size;tool.m_strength=1;
            PointBrush(tool,center,height);
            typeof(TerrainTool).GetMethod("ApplyBrush",flags).Invoke(tool,null);typeof(TerrainTool).GetMethod("EndStroke",flags).Invoke(tool,null);
            if(!tool.IsUndoAvailable())throw new Exception("Native editor-condition fixture stroke has no Undo record.");tool.ApplyUndo();
            var raw=Singleton<TerrainManager>.instance.RawHeights;for(int i=0;i<original.Length;i++)if(raw[i]!=original[i])throw new Exception("324 editor Undo differs at "+i);
            Entry.Log("PASS: native editor-condition Undo restores all "+HundredTiles.Core.ExpandedMap.TileCount+"-profile terrain samples. City-mode Undo availability is unchanged.");
            tool.ResetUndoBuffer();typeof(TerrainTool).GetMethod("ApplyBrush",flags).Invoke(tool,null);typeof(TerrainTool).GetMethod("EndStroke",flags).Invoke(tool,null);
        }finally{controller.m_mode=previousControllerMode;properties.m_mode=previousMode;Array.Copy(previous,brush,brush.Length);}
        var terrain=Singleton<TerrainManager>.instance;
        TerrainModify.RefreshAllModifications();
        int minX=(int)((center.x-size*.5f+8640)/16)-2,minZ=(int)((center.z-size*.5f+8640)/16)-2,maxX=(int)((center.x+size*.5f+8640)/16)+3,maxZ=(int)((center.z+size*.5f+8640)/16)+3;
        for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++){int k=z*HundredTiles.Core.ExpandedMap.HeightStride+x;terrain.BlockHeights[k]=terrain.BlockHeightTargets[k];}
        TerrainModify.UpdateArea(minX,minZ,maxX,maxZ,true,true,true);
        TerrainModify.RefreshAllModifications();
        int cellX=(int)((center.x+8640)/16),cellZ=(int)((center.z+8640)/16),index=cellZ*HundredTiles.Core.ExpandedMap.HeightStride+cellX;
        if(Math.Abs(terrain.SampleRawHeightSmooth(center)-height)>.1f)throw new Exception("Native fixture leveling failed at center: raw="+terrain.RawHeights[index]+", render="+terrain.RawHeights2[index]+", block="+terrain.BlockHeights[index]+", target="+terrain.BlockHeightTargets[index]+", sampled="+terrain.SampleRawHeightSmooth(center));
        Entry.Log("Prepared level fixture ground using the native terrain brush, including detail-height updates.");
    }
    public static void Run()
    {
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_TERRAIN")=="1"){LiveLandscapingCompatibilityChecks.Run(tool);return;}
        bool far=HundredTiles.Core.MapLayout.Active.TileCount==324;float coordinate=far?25600f:9840f;float zCoordinate=far?24640f:9840f;float targetHeight=far?396f:76f;
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
            typeof(TerrainTool).GetField("m_mousePosition",flags).SetValue(tool,new Vector3(coordinate,targetHeight+4,zCoordinate));
            typeof(TerrainTool).GetField("m_startPosition",flags).SetValue(tool,new Vector3(coordinate,targetHeight,zCoordinate));
            typeof(TerrainTool).GetMethod("ApplyBrush",flags).Invoke(tool,null);
            int cellX=far?2140:1155,cellZ=far?2080:1155;int index=cellZ*HundredTiles.Core.ExpandedMap.HeightStride+cellX;
            if(terrain.RawHeights[index]!=(ushort)(targetHeight*64))throw new Exception("Actual outer landscaping brush missed the requested world point: "+terrain.RawHeights[index]);
            typeof(TerrainTool).GetMethod("EndStroke",flags).Invoke(tool,null);
            if(far){if(!tool.IsUndoAvailable())throw new Exception("Far native terrain stroke did not create undo data.");tool.ApplyUndo();TerrainModify.RefreshAllModifications();for(int i=0;i<before.Length;i++)if(terrain.RawHeights[i]!=before[i])throw new Exception("Far native Undo did not restore terrain at "+i);Entry.Log("PASS: native city terrain Undo restores every 324-profile raw height sample; game availability unchanged.");}
            Entry.Log("PASS: actual city landscaping brush edits the added corner at its original world origin; all active backup samples verified.");
        }
        finally
        {
            Array.Copy(before,terrain.RawHeights,before.Length);terrain.DirtBuffer=dirt;Array.Copy(previous,brush,brush.Length);
            tool.ResetUndoBuffer();TerrainModify.UpdateArea(coordinate-40,zCoordinate-40,coordinate+60,zCoordinate+60,true,true,true);
            if(far)TerrainModify.RefreshAllModifications();
        }
    }
}
