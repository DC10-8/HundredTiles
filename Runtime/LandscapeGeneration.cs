using System;
using System.Collections.Generic;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.Threading;
using HundredTiles.Core;
using ICities;
using UnityEngine;

namespace HundredTiles.Runtime
{
    public sealed class LandscapeLoading : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            if(Installer.IsInstalled && (mode==LoadMode.NewGame || mode==LoadMode.LoadGame) && HundredTiles.SaveBridge.RuntimeSaveBridge.NeedsLandscape)
                LandscapeGeneration.Request(delegate(string value){Debug.Log("[HundredTiles] "+value);});
        }
    }
    public static class LandscapeGeneration
    {
        public const string Key=ExpandedMap.GridSize==18?"HundredTiles.Landscape324.v1":"HundredTiles.Landscape.v2";
        public const string PreviousKey="HundredTiles.Landscape.v1";
        static int pending;
        public static void Request(Action<string> status)
        {
            if(!Installer.IsInstalled){status(string.IsNullOrEmpty(Installer.FailureReason)?"都市を読み込んでから押してください":"MODの起動に失敗しています。起動失敗の詳細を確認してください");return;}
            if(!Singleton<LoadingManager>.exists || !Singleton<LoadingManager>.instance.m_loadingComplete || !Singleton<TerrainManager>.exists || Singleton<TerrainManager>.instance.RawHeights.Length!=ExpandedMap.HeightStride*ExpandedMap.HeightStride)
            {status("都市を読み込んでから押してください");return;}
            if(Interlocked.CompareExchange(ref pending,1,0)!=0){status("拡張部分の自然を生成中です…");return;}
            status("拡張部分の自然を生成中です…");
            try{Singleton<SimulationManager>.instance.AddAction(delegate {
                string message;
                try{message=Generate();}catch(Exception error){Debug.LogError("[HundredTiles] Landscape: "+error);message="生成に失敗しました。ゲームログを確認してください";}
                finally{Interlocked.Exchange(ref pending,0);}
                ThreadHelper.dispatcher.Dispatch(delegate{status(message);});
            });}catch(Exception error){Interlocked.Exchange(ref pending,0);Debug.LogError("[HundredTiles] Landscape: "+error);status("生成処理を開始できませんでした");}
        }
        public static string Generate()
        {
            var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;
            lock(storage)if(storage.ContainsKey(Key))return "この都市の自然は生成済みです";
            bool upgrade;lock(storage)upgrade=storage.ContainsKey(PreviousKey);
            var terrain=Singleton<TerrainManager>.instance;
            var mask=new bool[ExpandedMap.HeightStride*ExpandedMap.HeightStride];
#if HUNDREDTILES_324
            int preserved=HundredTiles.SaveBridge.RuntimeSaveBridge.PreservedRawResolution;for(int z=0;z<=preserved;z++)for(int x=0;x<=preserved;x++)mask[z*ExpandedMap.HeightStride+x]=true;
#endif
            var buildings=Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            for(int i=1;i<buildings.Length;i++)if((buildings[i].m_flags&Building.Flags.Created)!=0)
                Protect(mask,buildings[i].m_position,Math.Max(128,Math.Max(buildings[i].Width,buildings[i].Length)*8+96));
            var net=Singleton<NetManager>.instance;
            var segments=net.m_segments.m_buffer;
            for(int i=1;i<segments.Length;i++)if((segments[i].m_flags&NetSegment.Flags.Created)!=0)
            {
                // Bounding boxes also cover curved roads, tracks and utility lines.
                var bounds=segments[i].m_bounds;
                ProtectBox(mask,bounds.min.x-128,bounds.min.z-128,bounds.max.x+128,bounds.max.z+128);
            }
            var trees=Singleton<TreeManager>.instance.m_trees.m_buffer;
            var props=Singleton<PropManager>.instance.m_props.m_buffer;
            for(int i=1;i<props.Length;i++)if((props[i].m_flags&(ushort)PropInstance.Flags.Created)!=0)Protect(mask,props[i].Position,48);
            ushort[] heights,levels;int connections,obstructed,sourcesAdded=0;
            // Preserve existing relief on v1 upgrades. A river crossing does not
            // use the hill seam fade, which previously raised a dam at the seam.
            using(var handoff=new LandscapeWater(terrain.WaterSimulation))
            {
                var reliefMask=(bool[])mask.Clone();
                for(int i=1;i<trees.Length;i++)if((trees[i].m_flags&(ushort)TreeInstance.Flags.Created)!=0)Protect(reliefMask,trees[i].Position,32);
                for(int i=0;i<reliefMask.Length;i++)if(handoff.Depth[i]>0 || terrain.RawHeights[i]/64f<terrain.WaterSimulation.m_currentSeaLevel+2)reliefMask[i]=true;
                heights=upgrade?(ushort[])terrain.RawHeights.Clone():Landscape.Generate(terrain.RawHeights,reliefMask,false);
                var extension=new RiverExtension(heights,handoff.Depth,terrain.BlockHeights,mask,terrain.WaterSimulation.m_currentSeaLevel,HundredTiles.SaveBridge.RuntimeSaveBridge.PreservedRawResolution);
                heights=extension.Heights;levels=extension.Levels;connections=extension.Connections;obstructed=extension.Obstructed;
                // Repair the v1 artificial river using its saved source's original
                // elevation, not its now-lowered riverbed as a new water target.
                var waterSources=terrain.WaterSimulation.m_waterSources;
                var cornerSources=new List<WaterSource>();
                for(int n=0;n<waterSources.m_size;n++)
                {
                    var saved=waterSources.m_buffer[n];
                    if(!upgrade || saved.m_type!=WaterSource.TYPE_NATURAL || Math.Abs(saved.m_outputPosition.x-9280)>1 || Math.Abs(saved.m_outputPosition.z-9248)>1)continue;
                    cornerSources.Add(saved);
                }
                if(!upgrade && Landscape.CanRiver(terrain.RawHeights,reliefMask) && Landscape.RiverLevel(terrain.RawHeights)/64f>terrain.WaterSimulation.m_currentSeaLevel+2)
                {
                    cornerSources.Add(new WaterSource{m_target=Landscape.RiverLevel(terrain.RawHeights)});
                    extension.Sources.Add(Landscape.RiverCenterZ*ExpandedMap.HeightStride+Landscape.RiverCenterX);
                }
                foreach(var saved in cornerSources)
                {
                    for(int z=Landscape.RiverStart;z<=ExpandedMap.RawResolution;z++)for(int x=(int)Landscape.RiverX(z)-14;x<=(int)Landscape.RiverX(z)+14;x++)
                    {
                        int i=z*ExpandedMap.HeightStride+x;if(mask[i])continue;
                        double distance=Math.Abs(x-Landscape.RiverX(z));
                        double t=Math.Min(1,Math.Max(0,(distance-3)/9));t=t*t*(3-2*t);
                        int level=(int)Math.Round(saved.m_target+(terrain.WaterSimulation.m_currentSeaLevel*64-saved.m_target)*Math.Max(0,z-Landscape.RiverCenterZ)/82.0);
                        int bed=(int)Math.Round(level-384+640*t);
                        heights[i]=(ushort)Math.Max(0,Math.Min(65535,bed));
                        if(distance<9 && bed<level)levels[i]=(ushort)Math.Max(levels[i],level);
                    }
                    for(int z=(Landscape.RiverCenterZ+4);z<=ExpandedMap.RawResolution;z+=20)extension.Sources.Add(z*ExpandedMap.HeightStride+(int)Math.Round(Landscape.RiverX(z)));
                }
                var created=new List<ushort>();
                try{foreach(int index in extension.Sources)
                {
                    if(levels[index]==0)continue;
                    ushort id;var position=new Vector3(index%ExpandedMap.HeightStride*16-8640,levels[index]/64f,index/ExpandedMap.HeightStride*16-8640);
                    if(!terrain.WaterSimulation.CreateWaterSource(out id,new WaterSource{m_type=WaterSource.TYPE_NATURAL,m_inputPosition=position,m_outputPosition=position,m_target=levels[index],m_inputRate=600,m_outputRate=600}))throw new InvalidOperationException("No water source capacity available.");
                    created.Add(id);
                }}catch{foreach(ushort id in created)terrain.WaterSimulation.ReleaseWaterSource(id);throw;}
                sourcesAdded=created.Count;
            var changed=new bool[heights.Length];
            for(int i=0;i<heights.Length;i++)changed[i]=heights[i]!=terrain.RawHeights[i];
            Array.Copy(heights,terrain.RawHeights,heights.Length);
            // The integer overload uses raw cell coordinates. Process one tile
            // at a time; the native modification scratch buffers cover 128 cells.
            for(int z=0;z<ExpandedMap.GridSize;z++)for(int x=0;x<ExpandedMap.GridSize;x++)if(x>=9 || z>=9)
                TerrainModify.UpdateArea(x*120,z*120,(x+1)*120,(z+1)*120,true,true,true);
            // New dry land has no water volume to displace. Finish the generated
            // bed immediately instead of exposing the old bed to a new source
            // during the native multi-frame landscaping height transition.
            for(int i=0;i<changed.Length;i++)if(changed[i])terrain.BlockHeights[i]=terrain.BlockHeightTargets[i];
                handoff.Seed(levels,terrain.BlockHeights);
            }
            for(uint i=1;i<trees.Length;i++)if((trees[i].m_flags&(ushort)TreeInstance.Flags.Created)!=0)
            {
                var position=trees[i].Position;
                if(position.x>8640 || position.z>8640)
                {
                    float ground=terrain.SampleRawHeightSmooth(position);
                    if(Math.Abs(position.y-ground)>.5f){position.y=ground;Singleton<TreeManager>.instance.MoveTree(i,position);}
                }
                Protect(mask,position,12);
            }
            var random=new Randomizer(104729);int planted=0;
            int prefabCount=PrefabCollection<TreeInfo>.LoadedCount();
            var forest=new List<TreeInfo>();
            for(uint n=0;n<prefabCount;n++){var info=PrefabCollection<TreeInfo>.GetLoaded(n);if(info!=null && info.m_generatedInfo!=null && info.m_generatedInfo.m_size.y*info.m_minScale>=8)forest.Add(info);}
            for(int z=2;z<ExpandedMap.RawResolution;z++)for(int x=2;x<ExpandedMap.RawResolution;x++)
            {
                if((x<=1083 && z<=1083) || mask[z*ExpandedMap.HeightStride+x] || forest.Count==0)continue;
                float wx=x*16-8640+random.Int32(12)-6,wz=z*16-8640+random.Int32(12)-6;
                if(random.Int32(1000)>Landscape.ForestDensity(wx,wz)*1000*(19.0/(ExpandedMap.TileCount-81)))continue;
                float height=heights[z*ExpandedMap.HeightStride+x]/64f;
                if(height<terrain.WaterSimulation.m_currentSeaLevel+2 || levels[z*ExpandedMap.HeightStride+x]!=0)continue;
                if(Math.Abs(heights[z*ExpandedMap.HeightStride+x+1]-heights[z*ExpandedMap.HeightStride+x-1])>640 || Math.Abs(heights[(z+1)*ExpandedMap.HeightStride+x]-heights[(z-1)*ExpandedMap.HeightStride+x])>640)continue;
                var info=forest[random.Int32((uint)forest.Count)];uint id;
                var treePosition=new Vector3(wx,0,wz);treePosition.y=terrain.SampleRawHeightSmooth(treePosition);
                if(terrain.SampleRawHeightSmoothWithWater(treePosition,false,0)>treePosition.y+.1f)continue;
                if(info!=null && Singleton<TreeManager>.instance.CreateTree(out id,ref random,info,treePosition,true))planted++;
            }
            lock(storage)storage[Key]=new byte[]{1};
            HundredTiles.SaveBridge.RuntimeSaveBridge.NeedsLandscape=false;
            string message="自然の生成・修復完了（川の接続 "+connections+"、追加樹木 "+planted+"本、水源 "+sourcesAdded+"、保護物で見送り "+obstructed+"）";
            Debug.Log("[HundredTiles] "+message);return message;
        }
        static void Protect(bool[] mask,Vector3 p,float radius){ProtectBox(mask,p.x-radius,p.z-radius,p.x+radius,p.z+radius);}
        static void ProtectBox(bool[] mask,float minX,float minZ,float maxX,float maxZ)
        {
            int x0=Math.Max(0,(int)Math.Floor((minX+8640)/16)),x1=Math.Min(ExpandedMap.RawResolution,(int)Math.Ceiling((maxX+8640)/16));
            int z0=Math.Max(0,(int)Math.Floor((minZ+8640)/16)),z1=Math.Min(ExpandedMap.RawResolution,(int)Math.Ceiling((maxZ+8640)/16));
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)if(x>1080 || z>1080)mask[z*ExpandedMap.HeightStride+x]=true;
        }
    }
}

