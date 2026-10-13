using ColossalFramework;
using ColossalFramework.Plugins;
using UnityEngine;

namespace HundredTiles.StartupRepairs
{
    public static class OptionalBuildingThemes
    {
        public static bool IsEnabled()
        {
            var manager=Singleton<PluginManager>.instance;
            if(manager==null)return true;
            foreach(var plugin in manager.GetPluginsInfo())
            {
                if(!plugin.isEnabled)continue;
                foreach(var assembly in plugin.GetAssemblies())
                    if(assembly.GetName().Name=="BuildingThemes")return true;
            }
            Debug.Log("ABLC: optional Building Themes integration skipped because the mod is not enabled; normal level control remains available.");
            return false;
        }
    }
}
