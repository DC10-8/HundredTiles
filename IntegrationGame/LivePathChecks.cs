using System;
using System.IO;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

static class LivePathChecks
{
    public static void Run()
    {
        ushort segment=ushort.Parse(File.ReadAllLines(Path.Combine(Entry.Root,"outer-objects.txt"))[4]);
        var roads=Singleton<NetManager>.instance;var info=roads.m_segments.m_buffer[segment].Info;
        PathUnit.Position nearest;
        if(!PathManager.FindPathPosition(new Vector3(9900,80,9900),ItemClass.Service.Road,NetInfo.LaneType.Vehicle,VehicleInfo.VehicleType.Car,VehicleInfo.VehicleCategory.All,false,false,50,false,false,out nearest) || nearest.m_segment!=segment)throw new Exception("Native path position lookup missed the added-corner road.");
        byte lane=byte.MaxValue;
        for(byte i=0;i<info.m_lanes.Length;i++)if((info.m_lanes[i].m_laneType&NetInfo.LaneType.Vehicle)!=0 && (info.m_lanes[i].m_vehicleType&VehicleInfo.VehicleType.Car)!=0 && (info.m_lanes[i].m_finalDirection&NetInfo.Direction.Forward)!=0){lane=i;break;}
        if(lane==byte.MaxValue)throw new Exception("Outer road has no forward car lane.");
        var start=new PathUnit.Position{m_segment=segment,m_lane=lane,m_offset=32};var end=new PathUnit.Position{m_segment=segment,m_lane=lane,m_offset=224};
        var random=new Randomizer(29);uint path;var manager=Singleton<PathManager>.instance;
        if(!manager.CreatePath(out path,ref random,20,start,end,NetInfo.LaneType.Vehicle,VehicleInfo.VehicleType.Car,VehicleInfo.VehicleCategory.All,1000))throw new Exception("Actual path worker refused the outer road request.");
        try
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while((manager.m_pathUnits.m_buffer[path].m_pathFindFlags&12)==0 && timer.ElapsedMilliseconds<10000)Thread.Sleep(1);
            var result=manager.m_pathUnits.m_buffer[path];
            if((result.m_pathFindFlags&4)==0 || result.m_positionCount==0)throw new Exception("Actual outer-road path failed: flags="+result.m_pathFindFlags);
            Entry.Log("PASS: actual spatial path lookup and asynchronous car path worker succeeded on the saved added-corner road.");
        }
        finally{manager.ReleasePath(path);}
    }
    public static void Far()
    {
        var ids=File.ReadAllLines(Path.Combine(Entry.Root,"trial324-far-objects.txt"));
        foreach(bool train in new[]{false,true})
        {
            ushort segment=ushort.Parse(ids[train?1:0]);var graph=Singleton<NetManager>.instance;var data=graph.m_segments.m_buffer[segment];
            var type=train?VehicleInfo.VehicleType.Train:VehicleInfo.VehicleType.Car;var laneType=train?NetInfo.LaneType.Vehicle:NetInfo.LaneType.Vehicle;
            var position=data.m_middlePosition;PathUnit.Position nearest;
            if(!PathManager.FindPathPosition(position,data.Info.m_class.m_service,laneType,type,VehicleInfo.VehicleCategory.All,false,false,32,false,false,out nearest) || nearest.m_segment!=segment)throw new Exception("Far native path position lookup differs: "+data.Info.name);
            byte lane=byte.MaxValue;
            for(byte i=0;i<data.Info.m_lanes.Length;i++)if((data.Info.m_lanes[i].m_laneType&laneType)!=0 && (data.Info.m_lanes[i].m_vehicleType&type)!=0 && (data.Info.m_lanes[i].m_finalDirection&NetInfo.Direction.Forward)!=0){lane=i;break;}
            if(lane==byte.MaxValue)throw new Exception("Far network has no forward vehicle lane: "+data.Info.name);
            var start=new PathUnit.Position{m_segment=segment,m_lane=lane,m_offset=32};var end=new PathUnit.Position{m_segment=segment,m_lane=lane,m_offset=224};
            var manager=Singleton<PathManager>.instance;var random=new Randomizer(324);uint path;
            if(!manager.CreatePath(out path,ref random,Singleton<SimulationManager>.instance.m_currentBuildIndex,start,end,laneType,type,VehicleInfo.VehicleCategory.All,1000))throw new Exception("Far native path creation failed.");
            try
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();
                while((manager.m_pathUnits.m_buffer[path].m_pathFindFlags&12)==0 && timer.ElapsedMilliseconds<10000)Thread.Sleep(1);
                var result=manager.m_pathUnits.m_buffer[path];
                if((result.m_pathFindFlags&4)==0 || result.m_positionCount==0)throw new Exception("Far native path failed: "+type+" flags="+result.m_pathFindFlags);
                Entry.Log("PASS: 324 far spatial lookup and asynchronous "+type+" path worker; positions="+result.m_positionCount+", elapsed ms="+timer.ElapsedMilliseconds+". Single segment only, not a transit journey.");
            }
            finally{manager.ReleasePath(path);}
        }
    }
}
