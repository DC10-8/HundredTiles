using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;

namespace HundredTiles.Runtime
{
    public static class HeatingCompatibility
    {
        static FieldInfo capacity;
        public static void AddHooks(Assembly assembly,IList<HookSpec> hooks)
        {
            string path=assembly.Location;
            if(string.IsNullOrEmpty(path))foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                foreach(var loaded in plugin.GetAssemblies())if(loaded==assembly)path=Path.Combine(plugin.modPath,"NoUtilityNetworks.dll");
            AddReviewedHooks(assembly,path,hooks);
        }
        public static void AddReviewedHooks(Assembly assembly,string path,IList<HookSpec> hooks)
        {
            if(string.IsNullOrEmpty(path) || !File.Exists(path) || PlannedTranspiler.Hash(File.ReadAllBytes(path))!="8F6FE8B7FA3D83C99B0C1195438F504AB298C23FA9C82743A8ADEC052D3F8683")
                throw new InvalidOperationException("No Utility Networks version differs from reviewed heating compatibility adapter.");
            var target=assembly.GetType("NoUtilityNetworks.CapacityUpdater",true).GetMethod("OnBeforeSimulationTick");
            capacity=assembly.GetType("NoUtilityNetworks.HeatingManagerMod",true).GetField("HeatingCapacity",BindingFlags.Public|BindingFlags.Static);
            if(target==null || target.GetMethodBody()==null || ReviewedMethod.SemanticHash(target)!="BFBBB8BFD82C34DD800D4079A58C696FA549491ABD492D8ED648FFF5CB17F192" || capacity==null || capacity.FieldType!=typeof(int))throw new InvalidOperationException("No Utility Networks heating capacity helper changed.");
            hooks.Add(new HookSpec{Target=target,Postfix=typeof(HeatingCompatibility).GetMethod("HeatingReserve")});
        }
        public static void HeatingReserve()
        {
            // Keep the mod's own Init reserve. A zero demand sample must not
            // prevent real heating producers from starting to fill the pool.
            int value=(int)capacity.GetValue(null);
            if(value<100000)capacity.SetValue(null,100000);
        }
    }
}

