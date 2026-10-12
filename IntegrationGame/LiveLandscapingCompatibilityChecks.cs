using System;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

static class LiveLandscapingCompatibilityChecks
{
    static Type detour;
    static void Set(string name,object value){detour.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,value);}
    public static void Run(TerrainTool tool)
    {
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="NaturalResourcesBrush")detour=assembly.GetType("NaturalResourcesBrush.Detours.TerrainToolDetour",true);
        if(detour==null)throw new Exception("Actual Extra Landscaping Tools assembly missing.");
        var terrain=Singleton<TerrainManager>.instance;var original=(ushort[])terrain.RawHeights.Clone();int dirt=terrain.DirtBuffer;
        var controller=(ToolController)typeof(ToolBase).GetField("m_toolController",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(tool);
        var brush=controller.BrushData;var previous=(float[])brush.Clone();for(int i=0;i<brush.Length;i++)brush[i]=1;
        try
        {
            Set("m_sizeMode",Enum.ToObject(detour.GetNestedType("SizeMode"),0));tool.m_brushSize=32;tool.m_strength=1;
            foreach(var point in new[]{new Vector3(2000,0,2000),new Vector3(9840,0,9840),new Vector3(10496,0,10496)})
            {
                int x=Mathf.RoundToInt(point.x/16+540),z=Mathf.RoundToInt(point.z/16+540),index=z*1201+x;
                var rayPoint=new Vector3((x-540)*16,terrain.RawHeights[index]/64f,(z-540)*16);
                Stroke(tool,rayPoint,TerrainTool.Mode.Level,false,false,rayPoint.y-4,index);
            }
            var corner=new Vector3(9840,original[1155*1201+1155]/64f,9840);int cell=1155*1201+1155;
            Stroke(tool,corner,TerrainTool.Mode.Shift,false,false,corner.y+20,cell);
            Stroke(tool,corner,TerrainTool.Mode.Shift,true,false,corner.y-20,cell);
            Stroke(tool,corner,TerrainTool.Mode.Slope,false,false,corner.y-4,cell);
            Stroke(tool,corner,TerrainTool.Mode.Soften,false,false,corner.y,cell);
            Stroke(tool,corner,TerrainTool.Mode.Shift,false,true,float.NaN,cell);
            // EndStroke clears this field; successful ditch editing above also
            // exercises the expanded allocation before the stroke is ended.
            Entry.Log("PASS: real Extra Landscaping Tools redirected SimulationStep/raycast edits original, added and far-edge cells; raise, lower, slope, soften and ditch; every stroke undone with all 1442401 height samples restored.");
        }
        finally
        {
            Array.Copy(original,terrain.RawHeights,original.Length);terrain.DirtBuffer=dirt;Array.Copy(previous,brush,brush.Length);
            Set("isDitch",false);Set("m_mouseLeftDown",false);Set("m_mouseRightDown",false);Set("m_mouseRayValid",false);tool.ResetUndoBuffer();
        }
    }
    static void Stroke(TerrainTool tool,Vector3 point,TerrainTool.Mode mode,bool right,bool ditch,float expected,int index)
    {
        var terrain=Singleton<TerrainManager>.instance;var before=(ushort[])terrain.RawHeights.Clone();tool.ResetUndoBuffer();
        for(int i=0;i<before.Length;i++)if(terrain.BackupHeights[i]!=before[i])throw new Exception("ELT expanded backup missed cell "+i);
        tool.m_mode=mode;Set("isDitch",ditch);Set("m_startPosition",new Vector3(point.x,float.IsNaN(expected)?point.y:expected,point.z));
        if(mode==TerrainTool.Mode.Slope){Set("m_startPosition",new Vector3(point.x-32,expected-4,point.z));Set("m_endPosition",new Vector3(point.x+32,expected+4,point.z));}
        Set("m_mouseRay",new Ray(point+Vector3.up*500,Vector3.down));Set("m_mouseRayLength",1000f);Set("m_mouseRayValid",true);
        Set("m_mouseLeftDown",!right);Set("m_mouseRightDown",right);Set("m_strokeEnded",false);
        tool.SimulationStep();
        if(ditch)
        {var heights=(ushort[])detour.GetField("ditchHeights",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);if(heights==null || heights.Length!=1442401 || terrain.RawHeights[index]==before[index])throw new Exception("ELT expanded ditch failed.");}
        else if(terrain.RawHeights[index]!=(ushort)Mathf.RoundToInt(expected*64))throw new Exception("ELT brush missed world position "+point+": "+terrain.RawHeights[index]+", expected "+expected*64);
        // An edit must not affect any cell outside the brush's raw-cell bounds.
        int cx=index%1201,cz=index/1201;
        for(int i=0;i<before.Length;i++)if((Math.Abs(i%1201-cx)>2 || Math.Abs(i/1201-cz)>2) && terrain.RawHeights[i]!=before[i])throw new Exception("ELT edit corrupted a distant terrain cell "+i);
        Set("m_mouseLeftDown",false);Set("m_mouseRightDown",false);Set("m_strokeEnded",true);tool.SimulationStep();
        tool.Undo();tool.SimulationStep();
        for(int i=0;i<before.Length;i++)if(terrain.RawHeights[i]!=before[i] || terrain.BackupHeights[i]!=before[i])throw new Exception("ELT undo did not restore cell "+i);
        Set("isDitch",false);
    }
}
