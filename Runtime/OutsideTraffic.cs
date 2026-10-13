using System;
using System.IO;
using System.Threading;
using ColossalFramework;
using ColossalFramework.IO;
using ColossalFramework.Threading;
using ICities;
using UnityEngine;

namespace HundredTiles.Runtime
{
    // Native dummy transfers retain native routing, despawning and vehicle limits.
    // No persistent changes to connection flags or resident demand are made.
    public static class OutsideTraffic
    {
        static int preference=-1,pending;
        static string FilePath {get{return Path.Combine(DataLocation.localApplicationData,"HundredTiles.outside-traffic.txt");}}
        public static bool Automatic {get{if(preference<0){preference=1;try{if(File.Exists(FilePath))preference=File.ReadAllText(FilePath).Trim()=="off"?0:1;}catch(Exception error){Debug.LogWarning("[HundredTiles] Outside traffic preference: "+error.Message);}}return preference==1;}}
        public static void SetAutomatic(bool value){File.WriteAllText(FilePath,value?"on":"off");preference=value?1:0;}
        public static bool Ready(){return Installer.IsInstalled && !SessionGate.EditorSession && Singleton<LoadingManager>.exists && Singleton<LoadingManager>.instance.m_loadingComplete && Singleton<LoadingManager>.instance.m_simulationDataLoaded && !Singleton<LoadingManager>.instance.m_currentlyLoading && Singleton<ToolManager>.exists && (Singleton<ToolManager>.instance.m_properties.m_mode&ItemClass.Availability.Game)!=0;}
        public static void Request(Action<string> status)
        {
            if(!Ready()){status(UiLanguage.Text("都市を読み込んでから押してください","Load a city first."));return;}
            if(Interlocked.CompareExchange(ref pending,1,0)!=0)return;
            try{Singleton<SimulationManager>.instance.AddAction(delegate{string message;try{int count=DispatchWave();message=UiLanguage.Text("外部交通の発生を依頼: ","Outside traffic requests: ")+count+UiLanguage.Text("（接続先と通行可能な経路が必要です）"," (a destination and usable route are required)");}catch(Exception error){Debug.LogError("[HundredTiles] Outside traffic: "+error);message=UiLanguage.Text("発生に失敗しました。ゲームログを確認してください","Request failed. Check the game log.");}finally{Interlocked.Exchange(ref pending,0);}ThreadHelper.dispatcher.Dispatch(delegate{status(message);});});}catch{Interlocked.Exchange(ref pending,0);throw;}
        }
        public static int DispatchWave()
        {
            if(!Ready())return 0;
            var buildings=Singleton<BuildingManager>.instance;var connections=buildings.GetOutsideConnections();
            var ids=new ushort[connections.m_size];lock(connections)Array.Copy(connections.m_buffer,ids,ids.Length);
            int count=0;
            foreach(bool train in new[]{false,true})
            {
                foreach(ushort source in ids)
                {
                    if(!Eligible(source,train,Building.Flags.Incoming))continue;
                    ushort target=0;foreach(ushort candidate in ids)if(candidate!=source && Eligible(candidate,train,Building.Flags.Outgoing) && (buildings.m_buildings.m_buffer[candidate].m_position-buildings.m_buildings.m_buffer[source].m_position).sqrMagnitude>40000f){target=candidate;break;}
                    if(target==0)continue;
                    var offer=new TransferManager.TransferOffer{Building=target,Position=buildings.m_buildings.m_buffer[target].m_position,Amount=1,Active=false};
                    OutsideConnectionAI.StartConnectionTransfer(source,ref buildings.m_buildings.m_buffer[source],train?TransferManager.TransferReason.DummyTrain:TransferManager.TransferReason.DummyCar,offer,0,0,0);
                    count++;break; // At most one car transfer and one train transfer per wave.
                }
            }
            return count;
        }
        static bool Eligible(ushort id,bool train,Building.Flags direction)
        {
            var buffer=Singleton<BuildingManager>.instance.m_buildings.m_buffer;if(id==0 || id>=buffer.Length)return false;
            var data=buffer[id];var info=data.Info;
            return (data.m_flags&(Building.Flags.Created|Building.Flags.Deleted|direction))==(Building.Flags.Created|direction) && info!=null && info.m_buildingAI is OutsideConnectionAI && (train?info.m_class.m_subService==ItemClass.SubService.PublicTransportTrain:info.m_class.m_service==ItemClass.Service.Road);
        }
    }
    public sealed class OutsideTrafficThreading : ThreadingExtensionBase
    {
        int lastCount=-1;uint next;bool queued;
        public override void OnBeforeSimulationTick()
        {
            if(!OutsideTraffic.Ready())return;
            var sim=Singleton<SimulationManager>.instance;if((sim.m_currentFrameIndex&63)!=0)return;
            int count=Singleton<BuildingManager>.instance.m_buildingCount;
            if(count>lastCount)queued=true;lastCount=count;
            if(!OutsideTraffic.Automatic){queued=false;return;}
            if(!queued || sim.m_currentFrameIndex<next)return;
            queued=false;next=sim.m_currentFrameIndex+1024;
            try{OutsideTraffic.DispatchWave();}catch(Exception error){Debug.LogError("[HundredTiles] Automatic outside traffic: "+error);}
        }
    }
}
