using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using ColossalFramework;
using ColossalFramework.Plugins;
using HarmonyLib;
using HundredTiles.Core;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;

namespace HundredTiles.Runtime
{
    public static class SurfacePainterCompatibility
    {
        public static void AddHooks(Assembly assembly,IList<HookSpec> hooks)
        {
            string path=assembly.Location;
            if(string.IsNullOrEmpty(path))foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                foreach(var loaded in plugin.GetAssemblies())if(loaded==assembly)path=Path.Combine(plugin.modPath,"SurfacePainter.dll");
            AddReviewedHooks(assembly,path,hooks);
        }
        public static void AddReviewedHooks(Assembly assembly,string path,IList<HookSpec> hooks)
        {
            if(string.IsNullOrEmpty(path) || !File.Exists(path) || PlannedTranspiler.Hash(File.ReadAllBytes(path))!="627A7CFA571D32071689DEC6E979995178D4BB3DDEE05410114A8F8E6E885133")
                throw new InvalidOperationException("Surface Painter version differs from reviewed compatibility adapter.");
            var type=assembly.GetType("SurfacePainter.Detours.TerrainManagerDetour",true);
            var getter=type.GetMethod("GetSurfaceCell",new[]{typeof(int),typeof(int)});
            var update=type.GetMethod("UpdateCell",BindingFlags.NonPublic|BindingFlags.Static);
            var setter=assembly.GetType("SurfacePainter.SurfaceManager",true).GetMethod("SetSurfaceItem");
            if(getter==null || update==null || setter==null)throw new InvalidOperationException("Reviewed Surface Painter methods missing.");
            if(ReviewedMethod.SemanticHash(getter)!="FB9A3F7C4C0B030C0022DFE8771A5595A3D8922B1EBF60AFCCF1E367694B7D70" ||
               ReviewedMethod.SemanticHash(update)!="07F8F486B211901B4C2C5B04F43C2739313DED1D055583604B7CD1E4B7166FE6" ||
               ReviewedMethod.SemanticHash(setter)!="CDE5F7E12C18CC23D6E1C404026989F9F34735046C20F644732B7B6631C30FA2")
                throw new InvalidOperationException("Reviewed Surface Painter terrain/paint helper changed.");
            hooks.Add(new HookSpec{Target=getter,Prefix=typeof(BoundaryHooks).GetMethod("DetailCell"),Transpiler=typeof(SurfacePainterCompatibility).GetMethod("Grid")});
            hooks.Add(new HookSpec{Target=update,Prefix=typeof(SurfacePainterCompatibility).GetMethod("UpdateCell")});
            hooks.Add(new HookSpec{Target=setter,Prefix=typeof(SurfacePainterCompatibility).GetMethod("PaintCoordinates")});
        }
        public static IEnumerable<CodeInstruction> Grid(IEnumerable<CodeInstruction> instructions)
        {
            var result=new List<CodeInstruction>();int bounds=0,strides=0;
            foreach(var source in instructions)
            {
                var item=new CodeInstruction(source);
                if(item.LoadsConstant(8)){item.opcode=OpCodes.Ldc_I4;item.operand=ExpandedMap.GridSize-1;bounds++;}
                else if(item.LoadsConstant(9)){item.opcode=OpCodes.Ldc_I4;item.operand=ExpandedMap.GridSize;strides++;}
                result.Add(item);
            }
            if(bounds!=4 || strides!=3)throw new InvalidOperationException("Surface Painter terrain lookup shape changed.");
            return result;
        }
        // Keep Surface Painter's legacy paint grid and save encoding intact.
        // Outside it, retain the native road/terrain surface instead of reading
        // the clamped last painted row or column from a different location.
        public static bool PaintCoordinates(int z,int x){return x>=0 && z>=0 && x<4320 && z<4320;}
        public static bool UpdateCell(TerrainManager.SurfaceCell surfaceCell,int x,int z,ref TerrainManager.SurfaceCell __result)
        {if(PaintCoordinates(z,x))return true;__result=surfaceCell;return false;}
    }
}
