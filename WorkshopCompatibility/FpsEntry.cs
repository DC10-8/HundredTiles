using System;using System.IO;using System.Reflection;
public static class FpsEntry {
 public static string Root;
 public static void Main(string[] args){Root=Environment.GetEnvironmentVariable("HUNDREDTILES_SEVEN_ROOT");if(string.IsNullOrEmpty(Root)||!File.Exists(Path.Combine(Root,"isolated-test.marker")))return;AppDomain.CurrentDomain.AssemblyResolve+=Resolve;try{LiveFpsPreloader.Install();Assembly.LoadFrom(Path.Combine(Root,"loader/NativeChecks.dll")).GetType("NativeEntry",true).GetMethod("Main").Invoke(null,new object[]{args});}catch(Exception e){Log("BOOT FAILURE "+e);}}
 static Assembly Resolve(object s,ResolveEventArgs e){foreach(string dir in new[]{"fps-loader","loader"}){string p=Path.Combine(Path.Combine(Root,dir),new AssemblyName(e.Name).Name+".dll");if(File.Exists(p))return Assembly.LoadFrom(p);}return null;}
 public static void Log(string s){File.AppendAllText(Path.Combine(Root,"fps-bootstrap.log"),s+Environment.NewLine);}
}

// Runs the installed preloader against explicitly isolated paths only.
static class LiveFpsPreloader
{
    public static void Install()
    {
        string folder=Path.Combine(FpsEntry.Root,"fps-loader");
        var loader=Assembly.LoadFrom(Path.Combine(folder,"PatchLoader.dll"));
        var pathsType=loader.GetType("PatchLoader.Paths",true);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        string settings=Path.Combine(FpsEntry.Root,"UserData"),mods=Path.Combine(settings,"Addons/Mods");
        var paths=pathsType.GetConstructors(flags)[0].Invoke(new object[]{FpsEntry.Root,folder,Path.Combine(FpsEntry.Root,"Cities_Data/Managed"),Path.Combine(FpsEntry.Root,"Files/Mods"),settings,mods});
        var utils=Assembly.LoadFrom(Path.Combine(folder,"Utils.dll"));
        var loggerType=utils.GetType("Utils.Logger",true);
        var logger=Activator.CreateInstance(loggerType,new object[]{Path.Combine(FpsEntry.Root,"fps-preloader.log")});
        var saved=loader.GetType("PatchLoader.Helpers.SettingsFile",true);
        saved.GetProperty("LocalAppDataPath",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,settings,null);
        var wrapped=Activator.CreateInstance(loader.GetType("PatchLoader.WithPrefixLogger",true),flags,null,new object[]{logger,"PatchLoader.SettingsFile"},null);
        saved.GetProperty("Logger",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,wrapped,null);
        var api=Assembly.LoadFrom(Path.Combine(folder,"Patch.API.dll"));
        var game=Activator.CreateInstance(loader.GetType("PatchLoader.Helpers.GameState",true),true);
        api.GetType("Patch.API.Helpers.GameStateUtil",true).GetProperty("Util",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).SetValue(null,game,null);
        var internalLoader=Activator.CreateInstance(loader.GetType("PatchLoader.InternalLoader",true),flags,null,new object[]{logger,paths},null);
        internalLoader.GetType().GetMethod("Run").Invoke(internalLoader,null);
        FpsEntry.Log("Executed native PatchLoader with isolated settings/mods/managed paths; FPS Booster transformations enabled.");
    }
}

