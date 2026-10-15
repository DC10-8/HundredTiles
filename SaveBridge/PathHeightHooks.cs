using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.ExperimentalPatch;
using UnityEngine;

namespace HundredTiles.SaveBridge
{
    public static class PathHeightHooks
    {
        public static IList<HookSpec> Specifications()
        {
            var hooks=new List<HookSpec>();
            foreach(var type in new[]{typeof(ShipPathAI),typeof(FlightPathAI)})
            {
                var target=type.GetMethod("CheckHeight",BindingFlags.Instance|BindingFlags.NonPublic);
                SerializerHooks.Verify(target);
                string expected=type==typeof(ShipPathAI)?"AEC1C25E002B9962590ED01EE1A2F3A37949D4630E66F750AD1FBE837A8AB370":"6F8561B7B4FD8FB3198BD724508634561E0F99199C23E1DC7CB8E83CF383AE67";
                if(ReviewedMethod.SemanticHash(target)!=expected)throw new InvalidOperationException("Reviewed outside path height method changed: "+type.Name);
                hooks.Add(new HookSpec{Target=target,Prefix=typeof(PathHeightHooks).GetMethod(type==typeof(ShipPathAI)?"Ship":"Flight")});
            }
            return hooks;
        }
        public static Vector3 SamplePoint(Vector3 position)
        {
            position.x=Mathf.Clamp(position.x,ExpandedMap.WorldMin+33,ExpandedMap.WorldMax-33);
            position.z=Mathf.Clamp(position.z,ExpandedMap.WorldMin+33,ExpandedMap.WorldMax-33);
            return position;
        }
        static void Update(ushort nodeID,ref NetNode data)
        {
            // Terrain.UpdateData notifies networks before adding the rebuilt
            // ground heights to saved block offsets. Water depth alone is not
            // a world elevation at that stage. Keep saved route positions until
            // normal simulation can sample the completed terrain/water state.
            if(Singleton<LoadingManager>.exists && Singleton<LoadingManager>.instance.m_currentlyLoading)return;
            var sample=SamplePoint(data.m_position);
            float height=Singleton<TerrainManager>.instance.SampleRawHeightSmoothWithWater(sample,false,0);
            if(Mathf.Abs(height-data.m_position.y)>7)
            {var position=data.m_position;position.y=height;Singleton<NetManager>.instance.MoveNode(nodeID,position);}
        }
        public static bool Ship(ushort nodeID,ref NetNode data){Update(nodeID,ref data);return false;}
        public static bool Flight(FlightPathAI __instance,ushort nodeID,ref NetNode data)
        {if(!__instance.m_info.m_useFixedHeight)Update(nodeID,ref data);return false;}
    }
}
