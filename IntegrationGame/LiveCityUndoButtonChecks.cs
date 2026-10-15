using System;
using System.Collections;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.UI;
using HundredTiles.Runtime;
using UnityEngine;

// Exercises the shipped UI callback, rather than calling ApplyUndo directly.
// SimulateClick is a game UI event; it is not a physical mouse-input test.
static class LiveCityUndoButtonChecks
{
    public static IEnumerator Run()
    {
        var terrain=Singleton<TerrainManager>.instance;
        var before=(ushort[])terrain.RawHeights.Clone();
        var tool=ToolsModifierControl.SetTool<TerrainTool>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var controller=(ToolController)typeof(ToolBase).GetField("m_toolController",flags).GetValue(tool);
        var brush=controller.BrushData;var previous=(float[])brush.Clone();
        int dirt=terrain.DirtBuffer;Exception failure=null;bool ready=false;
        bool realOptions=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_REAL_OPTIONS")=="1";
        bool forced=Singleton<SimulationManager>.instance.ForcedSimulationPaused;
        UIPanel panel;
        if(realOptions){var options=UIView.library.ShowModal<OptionsMainPanel>("OptionsPanel");options.SelectMod(new Mod().Name);panel=(UIPanel)options.component;}
        else{panel=(UIPanel)UIView.GetAView().AddUIComponent(typeof(UIPanel));panel.width=700;panel.height=900;}
        try
        {
            if(!realOptions)new Mod().OnSettingsUI(new UIHelper(panel));UIButton undo=null;
            foreach(var button in panel.GetComponentsInChildren<UIButton>(true))if(button.name=="HundredTiles.TerrainUndo")undo=button;
            if(undo==null)throw new Exception("Shipped city Undo button is missing.");
            Singleton<SimulationManager>.instance.AddAction(delegate
            {
                try
                {
                    for(int i=0;i<brush.Length;i++)brush[i]=1;
                    tool.ResetUndoBuffer();tool.m_mode=TerrainTool.Mode.Level;tool.m_brushSize=32;tool.m_strength=1;
                    LiveTerrainToolChecks.PointBrush(tool,new Vector3(25600,400,24640),396);
                    typeof(TerrainTool).GetMethod("ApplyBrush",flags).Invoke(tool,null);
                    typeof(TerrainTool).GetMethod("EndStroke",flags).Invoke(tool,null);
                    if(!tool.IsUndoAvailable() || terrain.RawHeights[2080*HundredTiles.Core.ExpandedMap.HeightStride+2140]!=(ushort)(396*64))throw new Exception("City Undo button fixture edit failed.");
                }
                catch(Exception error){failure=error;}finally{ready=true;}
            });
            float deadline=Time.realtimeSinceStartup+30;while(!ready && Time.realtimeSinceStartup<deadline)yield return null;
            if(!ready)throw new TimeoutException("City Undo fixture edit timed out.");if(failure!=null)throw failure;
            if(realOptions)
            {
                for(UIComponent parent=undo.parent;parent!=null;parent=parent.parent)if(parent is UIScrollablePanel){((UIScrollablePanel)parent).ScrollIntoView(undo);break;}
                for(int i=0;i<30;i++)yield return null;
                yield return new WaitForEndOfFrame();LiveRunDiagnostics.Screenshot("trial324-real-options-undo-before.png");
            }
            undo.SimulateClick();deadline=Time.realtimeSinceStartup+30;
            string expected=UiLanguage.Text("地形を戻しました（費用の返金はありません）","Terrain restored; costs are not refunded.");
            while(undo.text!=expected && Time.realtimeSinceStartup<deadline)yield return null;
            if(undo.text!=expected)throw new Exception("City Undo UI callback failed: "+undo.text);
            for(int i=0;i<before.Length;i++)if(terrain.RawHeights[i]!=before[i])throw new Exception("City Undo button missed terrain sample "+i);
            if(realOptions){yield return new WaitForEndOfFrame();LiveRunDiagnostics.Screenshot("trial324-real-options-undo-after.png");Entry.Log("PASS: actual game OptionsPanel selected HundredTiles, scrolled its real Undo button into view and executed its UI callback; before/after frames captured.");}
            Entry.Log("PASS: shipped city Undo button SimulateClick queued its real callback and restored every 324-profile height sample. Physical mouse interaction is not asserted.");
        }
        finally
        {
            Array.Copy(previous,brush,brush.Length);Array.Copy(before,terrain.RawHeights,before.Length);terrain.DirtBuffer=dirt;
            tool.ResetUndoBuffer();TerrainModify.RefreshAllModifications();if(realOptions)UIView.library.Hide("OptionsPanel");else UnityEngine.Object.Destroy(panel.gameObject);Singleton<SimulationManager>.instance.ForcedSimulationPaused=forced;
        }
    }
}
