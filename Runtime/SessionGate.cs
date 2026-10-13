using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Packaging;
using ColossalFramework.UI;
using HarmonyLib;
using HundredTiles.SaveBridge;
using UnityEngine;

namespace HundredTiles.Runtime
{
    // Startup profile is frozen before Starter creates simulation managers.
    // Expanded arrays and indexing patches must not enter an editor process.
    public static class SessionGate
    {
        public const string Owner="HundredTiles.SessionGate";
        static bool enabled,installed,editorSession,editorProfile;
        public static bool EditorSession {get{return editorSession;}}
        public static void Enable()
        {
            enabled=true;
            if(installed)return;
            editorProfile=StartupProfile.Selected==1;
            var target=typeof(LoadingManager).GetMethod("LoadLevel",new[]{typeof(Package.Asset),typeof(string),typeof(string),typeof(SimulationMetaData),typeof(bool)});
            if(target==null)throw new MissingMethodException("LoadingManager.LoadLevel");
            SerializerHooks.Verify(target);
            new Harmony(Owner).Patch(target,prefix:new HarmonyMethod(typeof(SessionGate).GetMethod("BeforeLoad")){
                priority=Priority.First,before=new[]{"jp.dc108.hyperload","com.example.loadtimeremaining",Installer.Owner}});
            installed=true;
            Debug.Log("[HundredTiles] Session gate ready; startup profile: "+(editorProfile?"editor (normal grids)":"city (100 tiles)"));
        }
        public static void Disable(){enabled=false;Installer.Remove();}
        public static bool BeforeLoad(LoadingManager __instance,string __1,SimulationMetaData __3,ref Coroutine __result)
        {
            // Never change session state for a load the native loader ignores.
            if(__instance.m_currentlyLoading || __instance.m_applicationQuitting)return true;
            bool editor=IsEditor(__1,__3);
            if(editor)
            {
                if(!editorProfile || Installer.IsInstalled)return Refuse(ref __result,"オプションでHundredTilesの起動用途を「エディタ」に変更し、ゲームを再起動してください。MODの無効化は不要です。","Select the Editor startup profile in HundredTiles options and restart the game. The mod can remain enabled.");
                editorSession=true;
                Debug.Log("[HundredTiles] Editor session: no expanded city patches installed.");
                return true;
            }
            if(__1!="Game")return true;
            if(editorProfile || editorSession)return Refuse(ref __result,"オプションでHundredTilesの起動用途を「都市（100タイル）」に変更し、ゲームを再起動してください。MODは有効のままで構いません。","Select the City (100 tiles) startup profile in HundredTiles options and restart the game. The mod can remain enabled.");
            if(!enabled && !Installer.IsInstalled)return true;
            try
            {
                BoundaryHooks.CityMode(__1,__3);
                if(!Installer.IsInstalled)throw new InvalidOperationException("HundredTiles startup failed: "+Installer.FailureReason);
                return true;
            }
            catch(Exception error)
            {
                Installer.FailureReason=error.Message;
                Debug.LogError("[HundredTiles] City activation failed: "+error);
                return Refuse(ref __result,"100タイルの都市を安全に読み込めないため停止しました。\n"+error.Message,"City loading stopped because HundredTiles could not activate safely.\n"+error.Message);
            }
        }
        public static bool IsEditor(string scene,SimulationMetaData metadata)
        {
            return (scene!=null && scene.IndexOf("Editor",StringComparison.OrdinalIgnoreCase)>=0)
                || (metadata!=null && (metadata.m_updateMode==SimulationManager.UpdateMode.NewAsset || metadata.m_updateMode==SimulationManager.UpdateMode.LoadAsset));
        }
        static bool Refuse(ref Coroutine result,string ja,string en)
        {
            result=null;
            string message=UiLanguage.Text(ja,en);
            Debug.LogWarning("[HundredTiles] "+message);
            UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("HundredTiles",message,false);
            return false;
        }
    }
}
