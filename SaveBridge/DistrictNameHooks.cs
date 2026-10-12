using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using UnityEngine;

namespace HundredTiles.SaveBridge
{
    public static class DistrictNameHooks
    {
        public static bool Names(DistrictManager __instance,ref bool ___m_namesModified)
        {
            var locations=Locations(__instance.m_districtGrid);
            for(int i=0;i<128;i++)__instance.m_districts.m_buffer[i].m_nameLocation=Position(locations[i]);
            ___m_namesModified=true;return false;
        }
        public static bool Parks(DistrictManager __instance,ref bool ___m_namesModified)
        {
            var locations=Locations(__instance.m_parkGrid);
            for(int i=0;i<128;i++)__instance.m_parks.m_buffer[i].m_nameLocation=Position(locations[i]);
            ___m_namesModified=true;return false;
        }
        static int[] Locations(DistrictManager.Cell[] grid)
        {var ids=new byte[grid.Length];for(int i=0;i<ids.Length;i++)ids[i]=grid[i].m_district1;return DistrictNames.Calculate(ids);}
        static Vector3 Position(int location)
        {var position=new Vector3(DistrictNames.WorldX(location),0,DistrictNames.WorldZ(location));position.y=Singleton<TerrainManager>.instance.SampleRawHeightSmoothWithWater(position,false,0f);return position;}
        public static void MeshBounds(Mesh ___m_nameMesh,Mesh ___m_iconMesh)
        {
            var bounds=new Bounds(new Vector3(960f,0f,960f),new Vector3(19200f,1024f,19200f));
            if(___m_nameMesh!=null)___m_nameMesh.bounds=bounds;
            if(___m_iconMesh!=null)___m_iconMesh.bounds=bounds;
        }
        public static IList<HookSpec> Specifications()
        {
            var result=new List<HookSpec>();
            foreach(var row in new[]{new[]{"NamesModified","Names"},new[]{"ParkNamesModified","Parks"}})
            {var target=typeof(DistrictManager).GetMethod(row[0],BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);if(target==null)throw new MissingMethodException(row[0]);SerializerHooks.Verify(target);result.Add(new HookSpec{Target=target,Prefix=typeof(DistrictNameHooks).GetMethod(row[1])});}
            var update=typeof(DistrictManager).GetMethod("UpdateNames",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
            if(update==null)throw new MissingMethodException("UpdateNames");SerializerHooks.Verify(update);
            result.Add(new HookSpec{Target=update,Postfix=typeof(DistrictNameHooks).GetMethod("MeshBounds")});return result;
        }
    }
}
