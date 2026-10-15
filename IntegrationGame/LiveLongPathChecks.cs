using System;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

static class LiveLongPathChecks
{
    public static void Run()
    {
        if(HundredTiles.Core.MapLayout.Active.TileCount!=324)throw new Exception("Long route fixture requires 324 dimensions.");
        var graph=Singleton<NetManager>.instance;var random=new Randomizer(324239);var simulation=Singleton<SimulationManager>.instance;
        var previousZones=NetTool.m_zoneGridFlags;NetTool.m_zoneGridFlags=(NetSegment.Flags2)0;
        try
        {
            foreach(bool train in new[]{false,true})
            {
                var info=PrefabCollection<NetInfo>.FindLoaded(train?"Train Track":"Basic Road");
                var type=train?VehicleInfo.VehicleType.Train:VehicleInfo.VehicleType.Car;
                ushort previous=0,first=0,last=0;const int pieces=192;
                for(int i=0;i<=pieces;i++)
                {
                    float coordinate=8000+17000f*i/pieces;
                    var point=new Vector3(coordinate+(train?256:0),0,coordinate);point.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(point);
                    ushort node;if(!graph.CreateNode(out node,ref random,info,point,simulation.m_currentBuildIndex++))throw new Exception("Long path fixture node allocation failed.");
                    if(previous!=0)
                    {
                        ushort segment;var direction=(point-graph.m_nodes.m_buffer[previous].m_position);direction.y=0;direction.Normalize();
                        if(!graph.CreateSegment(out segment,ref random,info,previous,node,direction,-direction,simulation.m_currentBuildIndex++,simulation.m_currentBuildIndex,false))throw new Exception("Long path fixture segment allocation failed.");
                        if(first==0)first=segment;last=segment;
                    }
                    previous=node;
                }
                typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(graph,new object[]{0});
                byte lane=byte.MaxValue;
                for(byte i=0;i<info.m_lanes.Length;i++)if((info.m_lanes[i].m_laneType&NetInfo.LaneType.Vehicle)!=0 && (info.m_lanes[i].m_vehicleType&type)!=0 && (info.m_lanes[i].m_finalDirection&NetInfo.Direction.Forward)!=0){lane=i;break;}
                if(lane==byte.MaxValue)throw new Exception("Long route has no forward vehicle lane.");
                PathUnit.Position nearest;
                if(!PathManager.FindPathPosition(graph.m_segments.m_buffer[last].m_middlePosition,info.m_class.m_service,NetInfo.LaneType.Vehicle,type,VehicleInfo.VehicleCategory.All,false,false,16,false,false,out nearest) || nearest.m_segment!=last)throw new Exception("Long route far-end spatial lookup differs.");
                var start=new PathUnit.Position{m_segment=first,m_lane=lane,m_offset=32};var end=new PathUnit.Position{m_segment=last,m_lane=lane,m_offset=224};
                var paths=Singleton<PathManager>.instance;uint path;
                if(!paths.CreatePath(out path,ref random,simulation.m_currentBuildIndex,start,end,NetInfo.LaneType.Vehicle,type,VehicleInfo.VehicleCategory.All,100000))throw new Exception("Long route native path request refused.");
                try
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    while((paths.m_pathUnits.m_buffer[path].m_pathFindFlags&12)==0 && timer.ElapsedMilliseconds<30000)Thread.Sleep(1);
                    if((paths.m_pathUnits.m_buffer[path].m_pathFindFlags&4)==0)throw new Exception("Long route native worker failed: "+type+" flags="+paths.m_pathUnits.m_buffer[path].m_pathFindFlags);
                    Entry.Log("PASS: native "+type+" spatial lookup and asynchronous route over "+pieces+" connected segments from original world coordinates near 8000 to far coordinates near 25000; both axes cross 8640/10560/16384; elapsed ms="+timer.ElapsedMilliseconds+". Manager-level test network; no vehicle journey or station stop is asserted.");
                }
                finally{paths.ReleasePath(path);}
                if(!train && Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_TRANSIT")=="1")BusRoute(graph,ref random);
            }
        }
        finally{NetTool.m_zoneGridFlags=previousZones;}
    }
    static void BusRoute(NetManager graph,ref Randomizer random)
    {
        // A closed street circuit avoids depending on a traffic mod's policy
        // for bus U-turns at the ends of the separate diagonal path fixture.
        var road=PrefabCollection<NetInfo>.FindLoaded("Basic Road");ushort firstNode=0,previousNode=0;
        var corners=new[]{new Vector3(23000,0,24500),new Vector3(23600,0,24500),new Vector3(23600,0,25100),new Vector3(23000,0,25100),new Vector3(23000,0,24500)};
        for(int side=0;side<4;side++)for(int step=side==0?0:1;step<=5;step++)
        {
            var point=Vector3.Lerp(corners[side],corners[side+1],step/5f);point.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(point);ushort node;
            if(side==3 && step==5)node=firstNode;
            else if(!graph.CreateNode(out node,ref random,road,point,Singleton<SimulationManager>.instance.m_currentBuildIndex++))throw new Exception("Far bus street node creation failed.");
            if(firstNode==0)firstNode=node;
            if(previousNode!=0){ushort segment;var direction=(graph.m_nodes.m_buffer[node].m_position-graph.m_nodes.m_buffer[previousNode].m_position);direction.y=0;direction.Normalize();if(!graph.CreateSegment(out segment,ref random,road,previousNode,node,direction,-direction,Singleton<SimulationManager>.instance.m_currentBuildIndex++,Singleton<SimulationManager>.instance.m_currentBuildIndex,false))throw new Exception("Far bus street segment creation failed.");}
            previousNode=node;
        }
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(graph,new object[]{0});
        var manager=Singleton<TransportManager>.instance;var info=manager.GetTransportInfo(TransportInfo.TransportType.Bus);ushort line;
        if(info==null || !manager.CreateLine(out line,ref random,info,true))throw new Exception("Native far bus line creation failed.");
        var first=new Vector3(23150,0,24500);first.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(first);
        var second=new Vector3(23450,0,25100);second.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(second);
        if(!manager.m_lines.m_buffer[line].AddStop(line,-1,first,false) || !manager.m_lines.m_buffer[line].AddStop(line,-1,second,false) || !manager.m_lines.m_buffer[line].AddStop(line,-1,first,false) || !manager.m_lines.m_buffer[line].Complete)throw new Exception("Native far bus stops/loop creation failed.");
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(graph,new object[]{0});
        var timer=System.Diagnostics.Stopwatch.StartNew();bool ready=false;int segments=0;
        while(timer.ElapsedMilliseconds<30000)
        {
            manager.m_lines.m_buffer[line].UpdatePaths(line);ushort stop=manager.m_lines.m_buffer[line].m_stops;ready=true;segments=0;
            for(int visited=0;stop!=0 && visited<8;visited++)
            {
                ushort next=0;
                for(int i=0;i<8;i++){ushort segment=graph.m_nodes.m_buffer[stop].GetSegment(i);if(segment==0 || graph.m_segments.m_buffer[segment].m_startNode!=stop)continue;uint path=graph.m_segments.m_buffer[segment].m_path;segments++;if(path==0 || (Singleton<PathManager>.instance.m_pathUnits.m_buffer[path].m_pathFindFlags&4)==0)ready=false;next=graph.m_segments.m_buffer[segment].m_endNode;break;}
                stop=next;if(stop==manager.m_lines.m_buffer[line].m_stops)break;
            }
            if(ready && segments==2)break;Thread.Sleep(1);
        }
        if(!ready || segments!=2)throw new Exception("Native far bus line paths did not become ready: line="+line+", legs="+segments);
        Entry.Log("PASS: native bus TransportManager line "+line+" and two far stops on a connected closed street circuit at x=23000..23600/z=24500..25100 form a complete loop; both native transit path legs ready. Vehicle dispatch, passenger boarding and full rides are not asserted.");
    }
}
