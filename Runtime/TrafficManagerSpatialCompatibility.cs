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
    public static class TrafficManagerSpatialCompatibility
    {
        public static bool IsSpatialTarget(string key)
        {
            return key=="TrafficManager.Manager.Impl.ExtPathManager.FindPathPositionWithSpiralLoop" ||
                   key=="TrafficManager.Manager.Impl.ExtPathManager.<FindPathPositionWithSpiralLoop>g__FindHelper|12_0" ||
                   key=="TrafficManager.Manager.Impl.AdvancedParkingManager.<FindParkingSpaceAtRoadSide>g__LoopHandler|32_0" ||
                   key=="TrafficManager.Manager.Impl.AdvancedParkingManager.<FindParkingSpaceBuilding>g__LoopHandler|33_0";
        }
        public static void AddHooks(Assembly assembly,IList<HookSpec> hooks)
        {
            string path=assembly.Location;
            if(string.IsNullOrEmpty(path))foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())foreach(var loaded in plugin.GetAssemblies())if(loaded==assembly)path=Path.Combine(plugin.modPath,"TrafficManager.dll");
            ReviewedCompatibility.Verify(assembly,path);
            Add(assembly,hooks,"ExtPathManager","FindPathPositionWithSpiralLoop","58FDC5DFF98005F4462342255ED03161EB4B8ACC71B5473128CFEF644AFBDB25");
            Add(assembly,hooks,"ExtPathManager","<FindPathPositionWithSpiralLoop>g__FindHelper|12_0","8F525129730B64A038E4038EF6CF35AE8457317F45FB7A296138C949916E0D60");
            Add(assembly,hooks,"AdvancedParkingManager","<FindParkingSpaceAtRoadSide>g__LoopHandler|32_0","B894CFF8572175779E49D74472EF988BF6E37CB8A8E4784FEA5FF7B85CF3913B");
            Add(assembly,hooks,"AdvancedParkingManager","<FindParkingSpaceBuilding>g__LoopHandler|33_0","C7DAEBC4F3AEE44BD79922F1EE16A455806A369D903E29EDFBCE7356797F61D5");
        }
        static void Add(Assembly assembly,IList<HookSpec> hooks,string type,string name,string hash)
        {
            MethodInfo found=null;
            foreach(var method in assembly.GetType("TrafficManager.Manager.Impl."+type,true).GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                if(method.Name==name && ReviewedMethod.SemanticHash(method)==hash){if(found!=null)throw new InvalidOperationException("Ambiguous TM:PE spatial adapter.");found=method;}
            if(found==null)throw new InvalidOperationException("Reviewed TM:PE spatial helper changed: "+type+"."+name);
            hooks.Add(new HookSpec{Target=found,Transpiler=typeof(TrafficManagerSpatialCompatibility).GetMethod("Grid")});
        }
        public static IEnumerable<CodeInstruction> Grid(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {
            bool parent=__originalMethod.Name=="FindPathPositionWithSpiralLoop";int constants=0,grids=0;
            var output=new List<CodeInstruction>();
            foreach(var source in instructions)
            {
                var item=new CodeInstruction(source);var field=item.operand as FieldInfo;
                if(field!=null && (field.Name=="m_segmentGrid" || field.Name=="m_buildingGrid"))grids++;
                if(item.LoadsConstant(parent?269:270)){item.opcode=OpCodes.Ldc_I4;item.operand=ExpandedMap.GridSize*30-(parent?1:0);constants++;}
                output.Add(item);
            }
            if(constants!=(parent?2:3) || grids!=(parent?0:1))throw new InvalidOperationException("TM:PE spatial grid control flow differs.");
            return output;
        }
    }
}
