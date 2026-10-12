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
}
