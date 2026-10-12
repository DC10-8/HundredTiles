using System;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using HundredTiles.ExperimentalPatch;
using HundredTiles.Utilities;
namespace HundredTiles.Runtime
{
    public static class NetworkCapacityCompatibility
    {
        public static void Configure()
        {
            foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
            {
                string path=Path.Combine(plugin.modPath,"MoreNetworkCapacity.Core.dll");
                if(!File.Exists(path))continue;
                string wrapper=Path.Combine(plugin.modPath,"MoreNetworkCapacityRenew.dll");
                if(!File.Exists(wrapper))throw new InvalidOperationException("Missing More Network Capacity Renew wrapper."); string wrapperHash=PlannedTranspiler.Hash(File.ReadAllBytes(wrapper)); if(wrapperHash!="33977CE2A2C2D83AA14BFB4432244EBEBA2F9ACF29FDB8B9CAD7E53A4A0C5A07" && wrapperHash!="C4334AD302F232644E147EC6FC2397C0C328EF26A02BCA8E1CE71A5F0183338B")throw new InvalidOperationException("Unreviewed More Network Capacity Renew wrapper.");
                Assembly core=null;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="MoreNetworkCapacity")core=assembly;
                if(core==null)throw new InvalidOperationException("More Network Capacity Renew core is not loaded.");
                ReviewedCompatibility.Verify(core,path);
                var enabled=core.GetType("MoreNetworkCapacity.PatchState",true).GetProperty("ExpansionEnabled",BindingFlags.Static|BindingFlags.Public);
                UtilitySaveBridge.RequestedCapacity=delegate{return (bool)enabled.GetValue(null,null)?65024:32768;};
            }
        }
    }
}

