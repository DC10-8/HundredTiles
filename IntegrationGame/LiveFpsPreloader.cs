using System;
using System.IO;
using System.Reflection;

// Runs the installed preloader against explicitly isolated paths only.
static class LiveFpsPreloader
{
    public static void Install()
    {
        string folder=Path.Combine(Entry.Root,"fps-loader");
        var loader=Assembly.LoadFrom(Path.Combine(folder,"PatchLoader.dll"));
        var pathsType=loader.GetType("PatchLoader.Paths",true);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        string settings=Path.Combine(Entry.Root,"UserData"),mods=Path.Combine(settings,"Addons/Mods");
        var paths=pathsType.GetConstructors(flags)[0].Invoke(new object[]{Entry.Root,folder,Path.Combine(Entry.Root,"Cities_Data/Managed"),Path.Combine(Entry.Root,"Files/Mods"),settings,mods});
        var utils=Assembly.LoadFrom(Path.Combine(folder,"Utils.dll"));
        var loggerType=utils.GetType("Utils.Logger",true);
        var logger=Activator.CreateInstance(loggerType,new object[]{Path.Combine(Entry.Root,"fps-preloader.log")});
        var saved=loader.GetType("PatchLoader.Helpers.SettingsFile",true);
        saved.GetProperty("LocalAppDataPath",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,settings,null);
        var wrapped=Activator.CreateInstance(loader.GetType("PatchLoader.WithPrefixLogger",true),flags,null,new object[]{logger,"PatchLoader.SettingsFile"},null);
        saved.GetProperty("Logger",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,wrapped,null);
        var api=Assembly.LoadFrom(Path.Combine(folder,"Patch.API.dll"));
        var game=Activator.CreateInstance(loader.GetType("PatchLoader.Helpers.GameState",true),true);
        api.GetType("Patch.API.Helpers.GameStateUtil",true).GetProperty("Util",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,game,null);
        var internalLoader=Activator.CreateInstance(loader.GetType("PatchLoader.InternalLoader",true),flags,null,new object[]{logger,paths},null);
        internalLoader.GetType().GetMethod("Run").Invoke(internalLoader,null);
        Entry.Log("Executed native PatchLoader with isolated settings/mods/managed paths; FPS Booster transformations enabled.");
    }
}
