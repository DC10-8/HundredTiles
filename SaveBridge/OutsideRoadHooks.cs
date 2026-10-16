using System;
using UnityEngine;
using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.ExperimentalPatch;

namespace HundredTiles.SaveBridge
{
    public static class OutsideRoadHooks
    {
        static BuildingInfo fallback;
        public static HookSpec Specification()
        {
            var target=typeof(RoadBaseAI).GetMethod("GetNodeBuilding");SerializerHooks.Verify(target);
            if(ReviewedMethod.SemanticHash(target)!="C4E02A1E86C4DABDA9218E192F8E54CDBEBFC1F3AE854C18C39E5FDA32D80DAC")
                throw new InvalidOperationException("Reviewed road outside building method changed.");
            return new HookSpec{Target=target,Postfix=typeof(OutsideRoadHooks).GetMethod("SupplyMissingConnection")};
        }
        public static bool IsMapEdge(Vector3 point)
        {
            if(!(point.x>=ExpandedMap.WorldMin-8 && point.x<=ExpandedMap.WorldMax+8 && point.z>=ExpandedMap.WorldMin-8 && point.z<=ExpandedMap.WorldMax+8))return false;
            return Math.Abs(point.x-ExpandedMap.WorldMin)<=8 || Math.Abs(point.x-ExpandedMap.WorldMax)<=8 ||
                   Math.Abs(point.z-ExpandedMap.WorldMin)<=8 || Math.Abs(point.z-ExpandedMap.WorldMax)<=8;
        }
        public static void SupplyMissingConnection(ref NetNode data,ref BuildingInfo building,ref float heightOffset)
        {
            if(building!=null || (data.m_flags&NetNode.Flags.Outside)==0)return;
            // A saved connection can become interior after a 100-to-324 migration.
            // Keep its native outside building rather than deleting it on update.
            if(data.m_building!=0 && Singleton<BuildingManager>.exists)
            {
                var existing=Singleton<BuildingManager>.instance.m_buildings.m_buffer[data.m_building];
                if((existing.m_flags&(Building.Flags.Created|Building.Flags.Deleted))==Building.Flags.Created && existing.Info!=null && existing.Info.m_buildingAI is OutsideConnectionAI && existing.Info.m_class.m_service==ItemClass.Service.Road)
                {building=existing.Info;heightOffset=existing.m_position.y-data.m_position.y;return;}
            }
            if(!IsMapEdge(data.m_position))return;
            if(fallback==null)
                for(uint index=0;index<PrefabCollection<NetInfo>.LoadedCount();index++)
                {
                    var info=PrefabCollection<NetInfo>.GetLoaded(index);var road=info==null?null:info.m_netAI as RoadBaseAI;
                    var candidate=road==null?null:road.m_outsideConnection;
                    if(candidate!=null && candidate.m_buildingAI is OutsideConnectionAI && candidate.m_class.m_service==ItemClass.Service.Road){fallback=candidate;break;}
                }
            if(fallback==null)throw new InvalidOperationException("No native road outside connection prefab is loaded.");
            building=fallback;heightOffset=-2;
        }
    }
}
