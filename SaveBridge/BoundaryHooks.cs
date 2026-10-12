using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HundredTiles.SaveBridge
{
    public static class BoundaryHooks
    {
        public static bool Camera(Vector3 position,ref Vector3 __result)
        {
            __result=position;
            __result.x=Mathf.Clamp(position.x,-8640f,10560f);
            __result.z=Mathf.Clamp(position.z,-8640f,10560f);
            return false;
        }
        public static IList<HookSpec> Specifications()
        {
            var target=typeof(CameraController).GetMethod("ClampCameraPosition",BindingFlags.Static|BindingFlags.Public);
            if(target==null)throw new MissingMethodException("ClampCameraPosition");SerializerHooks.Verify(target);
            var cinematic=typeof(CinematicCameraController).GetMethod("IsInsideGameAreaLimits",BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if(cinematic==null)throw new MissingMethodException("IsInsideGameAreaLimits");SerializerHooks.Verify(cinematic);
            var result=new List<HookSpec>{new HookSpec{Target=target,Prefix=typeof(BoundaryHooks).GetMethod("Camera")},new HookSpec{Target=cinematic,Prefix=typeof(BoundaryHooks).GetMethod("Cinematic")}};
            result.Add(ConstructionHooks.Specification());
            foreach(string name in new[]{"GetZoneCell","GetSurfaceCell"})
            {
                var cells=typeof(TerrainManager).GetMethod(name,new[]{typeof(int),typeof(int)});
                if(cells==null)throw new MissingMethodException(name);SerializerHooks.Verify(cells);
                result.Add(new HookSpec{Target=cells,Prefix=typeof(BoundaryHooks).GetMethod("DetailCell")});
            }
            var load=typeof(LoadingManager).GetMethod("LoadLevel",new[]{typeof(ColossalFramework.Packaging.Package.Asset),typeof(string),typeof(string),typeof(SimulationMetaData),typeof(bool)});
            if(load==null)throw new MissingMethodException("LoadingManager.LoadLevel");SerializerHooks.Verify(load);
            result.Add(new HookSpec{Target=load,Prefix=typeof(BoundaryHooks).GetMethod("CityMode")});
            foreach(var type in new[]{typeof(FogProperties),typeof(FogEffect)})
            {
                var fog=type.GetMethod(type==typeof(FogProperties)?"Update":"OnRenderImage",BindingFlags.Instance|BindingFlags.NonPublic);
                if(fog==null && type==typeof(FogProperties))fog=type.GetMethod("FpsBoosterUpdate",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
                if(fog==null)throw new MissingMethodException(type.Name+" fog update");SerializerHooks.Verify(fog);
                result.Add(new HookSpec{Target=fog,Prefix=typeof(BoundaryHooks).GetMethod("EdgeFog"),Finalizer=typeof(BoundaryHooks).GetMethod("RestoreEdgeFog")});
            }
            return result;
        }
        // The built-in edge fog covers the original map boundary in the shader.
        // Suppress only that component; preserve weather/pollution fog and the
        // original serialized setting, even when the native update throws.
        public static void EdgeFog(ref bool ___m_edgeFog,out bool __state)
        {__state=___m_edgeFog;___m_edgeFog=false;}
        public static Exception RestoreEdgeFog(Exception __exception,ref bool ___m_edgeFog,bool __state)
        {___m_edgeFog=__state;return __exception;}
        public static void CityMode(string __1,SimulationMetaData __3)
        {
            if(__1!=null && __1.IndexOf("Editor",StringComparison.OrdinalIgnoreCase)>=0)
                throw new NotSupportedException("HundredTiles supports cities only. Disable it and restart the game before opening an editor.");
            if(__1=="Game" && (__3==null || (__3.m_updateMode!=SimulationManager.UpdateMode.LoadGame && __3.m_updateMode!=SimulationManager.UpdateMode.NewGameFromMap)))
                throw new NotSupportedException("HundredTiles supports normal saved cities and new cities from maps; scenarios are unsupported.");
        }
        // Detailed heights include the edge vertex (4800), but zone/surface
        // cells stop at 4799. Refresh requests padding beyond the last tile.
        public static void DetailCell(ref int __0,ref int __1)
        {__0=Math.Max(0,Math.Min(4799,__0));__1=Math.Max(0,Math.Min(4799,__1));}
        public static bool Cinematic(Vector3 position,ref bool __result)
        {__result=position.x>=-8640f && position.x<=10560f && position.z>=-8640f && position.z<=10560f;return false;}
    }
}
