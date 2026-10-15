using System;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Threading;
using UnityEngine;

namespace HundredTiles.Runtime
{
    public static class CityTerrainUndo
    {
        static int pending;
        public static void Request(Action<string> status)
        {
            if(!Installer.IsInstalled || !Singleton<LoadingManager>.exists || !Singleton<ToolManager>.exists ||
                !Singleton<LoadingManager>.instance.m_loadingComplete || Singleton<LoadingManager>.instance.m_currentlyLoading ||
                (Singleton<ToolManager>.instance.m_properties.m_mode & ItemClass.Availability.Game)==0)
            {status(UiLanguage.Text("都市を読み込んでから押してください","Load a city first."));return;}
            var tool=UnityEngine.Object.FindObjectOfType<TerrainTool>();
            if(tool==null){status(UiLanguage.Text("地形ツールが見つかりません","Terrain tool is unavailable."));return;}
            var stroke=typeof(TerrainTool).GetField("m_strokeInProgress",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            if(stroke==null)throw new MissingFieldException("TerrainTool.m_strokeInProgress");
            if(StrokeInProgress(tool,stroke)){status(UiLanguage.Text("ブラシ操作を終了してから押してください","Finish the brush stroke first."));return;}
            if(Interlocked.CompareExchange(ref pending,1,0)!=0)return;
            try
            {
                Singleton<SimulationManager>.instance.AddAction(delegate
                {
                    string message;
                    try
                    {
                        if(StrokeInProgress(tool,stroke))message=UiLanguage.Text("ブラシ操作を終了してから押してください","Finish the brush stroke first.");
                        else if(!tool.IsUndoAvailable())message=UiLanguage.Text("戻せる地形編集がありません","No terrain stroke to undo.");
                        else
                        {
                            tool.ApplyUndo();TerrainModify.RefreshAllModifications();
                            message=UiLanguage.Text("地形を戻しました（費用の返金はありません）","Terrain restored; costs are not refunded.");
                        }
                    }
                    catch(Exception error){Debug.LogError("[HundredTiles] City terrain Undo: "+error);message=UiLanguage.Text("地形を戻せませんでした。ログを確認してください","Terrain Undo failed; check the log.");}
                    finally{Interlocked.Exchange(ref pending,0);}
                    ThreadHelper.dispatcher.Dispatch(delegate{status(message);});
                });
            }
            catch{Interlocked.Exchange(ref pending,0);throw;}
        }
        public static bool StrokeInProgress(TerrainTool tool,System.Reflection.FieldInfo nativeStroke)
        {
            if((bool)nativeStroke.GetValue(tool))return true;
            // The reviewed Extra Landscaping Tools redirect keeps its own state.
            // Native state alone cannot protect an in-progress redirected stroke.
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if(assembly.GetName().Name=="NaturalResourcesBrush")
                {
                    var type=assembly.GetType("NaturalResourcesBrush.Detours.TerrainToolDetour",true);
                    var field=type.GetField("m_strokeInProgress",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                    if(field==null || field.FieldType!=typeof(bool))throw new MissingFieldException(type.FullName,"m_strokeInProgress");
                    if((bool)field.GetValue(null))return true;
                }
            return false;
        }
    }
}
