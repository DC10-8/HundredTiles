using System;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Threading;

namespace HundredTiles.Runtime
{
    public static class BulkUnlock
    {
        static int pending;
        public static void Request(Action<string> status)
        {
            if(!Installer.IsInstalled){status("MODの起動に失敗しています。起動失敗の詳細を確認してください");UnityEngine.Debug.LogError("[HundredTiles] Unlock unavailable: "+Installer.FailureReason);return;}
            if(!Ready()){status("都市を読み込んでから押してください");return;}
            if(Interlocked.CompareExchange(ref pending,1,0)!=0){status("全タイルを開放中です…");return;}
            status("全タイルを開放中です…");
            try {
                Singleton<SimulationManager>.instance.AddAction(delegate {
                    string message;
                    try {int count=UnlockAll();message=count==100 ? "100タイルすべて開放済みです" : "開放できた区画: "+count+" / 100（再実行できます）";}
                    catch(Exception error){UnityEngine.Debug.LogError("[HundredTiles] Unlock all: "+error);message="開放に失敗しました。ゲームログを確認してください";}
                    finally{Interlocked.Exchange(ref pending,0);}
                    ThreadHelper.dispatcher.Dispatch(delegate {status(message);});
                });
            }
            catch(Exception error){Interlocked.Exchange(ref pending,0);UnityEngine.Debug.LogError("[HundredTiles] "+error);status("開放処理を開始できませんでした");}
        }
        static bool Ready()
        {
            if(!Installer.IsInstalled || !Singleton<LoadingManager>.exists || !Singleton<GameAreaManager>.exists || !Singleton<ToolManager>.exists)return false;
            var loading=Singleton<LoadingManager>.instance;
            return loading.m_loadingComplete && loading.m_simulationDataLoaded && !loading.m_currentlyLoading
                && (Singleton<ToolManager>.instance.m_properties.m_mode & ItemClass.Availability.Game)!=0
                && Singleton<GameAreaManager>.instance.m_areaGrid.Length==100;
        }
        // Called on the simulation thread; use the patched native transaction
        // so terrain detail, zones and wrapper notifications stay consistent.
        public static int UnlockAll()
        {
            if(!Ready())throw new InvalidOperationException("An expanded city must be loaded.");
            var areas=Singleton<GameAreaManager>.instance;
            bool progress=true;
            while(areas.m_areaCount<100 && progress)
            {
                progress=false;
                for(int i=0;i<100;i++)
                    if(areas.m_areaGrid[i]==0 && areas.CanUnlock(i%10,i/10) && areas.UnlockArea(i))progress=true;
            }
            if(areas.m_areaCount==100)ExtendedSea.Initialize();
            UnityEngine.Debug.Log("[HundredTiles] Unlock all completed: "+areas.m_areaCount+" / 100.");
            return areas.m_areaCount;
        }
    }
}
