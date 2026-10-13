using System;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.IO;
using ColossalFramework.Packaging;
using ColossalFramework.Plugins;
using HarmonyLib;
using HundredTiles.Runtime;
using HundredTiles.Utilities;
using UnityEngine;

public static class Bootstrap
{
    static bool started,runtimeInstalled,runnerCreated;
    static bool EntryMode { get { return (Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_MODE")??"").IndexOf("Entry",StringComparison.Ordinal)>=0; } }
    public static volatile bool FatalFailure;
    public static void InstallEarly()
    {
        var harmony=new Harmony("HundredTiles.IsolatedIntegration.Bootstrap");
        harmony.Patch(typeof(Debug).GetMethod("Log",new[]{typeof(object)}),new HarmonyMethod(typeof(Bootstrap).GetMethod("FirstLog")));
        Entry.Log("Waiting for first Unity main-thread managed log before touching game types.");
    }
    public static void FirstLog()
    {
        if(started)return;
        try { var platform=Application.platform; }
        catch(MissingMethodException){return;}
        started=true;
        try{InstallLate();}catch(Exception error){Entry.Log("BOOT FAILURE after Unity initialization: "+error);Application.Quit();}
    }
    static void InstallLate()
    {
        var settings=Path.Combine(Entry.Root,"UserData");Directory.CreateDirectory(settings);
        File.WriteAllText(Path.Combine(settings,"HundredTiles.startup-profile.txt"),Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_MODE")=="EditorEntry"?"editor":"city");
        Application.logMessageReceivedThreaded+=ObserveLog;
        var harmony=new Harmony("HundredTiles.IsolatedIntegration");
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_TERRAIN_REWRITE")=="1")
            harmony.Patch(typeof(Assembly).GetMethod("Load",new[]{typeof(byte[])}),new HarmonyMethod(typeof(Bootstrap).GetMethod("RewriteTerrainAssembly")));
        harmony.Patch(typeof(DataLocation).GetProperty("localApplicationData").GetGetMethod(),new HarmonyMethod(typeof(Bootstrap).GetMethod("LocalData")));
        harmony.Patch(typeof(DataLocation).GetProperty("migrateProductFrom").GetSetMethod(),new HarmonyMethod(typeof(Bootstrap).GetMethod("SkipMigration")));
        harmony.Patch(typeof(Starter).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic),new HarmonyMethod(typeof(Bootstrap).GetMethod("BeforeStarter")),new HarmonyMethod(typeof(Bootstrap).GetMethod("AfterStarter")));
        harmony.Patch(typeof(PluginManager).GetMethod("LoadPlugins"),postfix:new HarmonyMethod(typeof(Bootstrap).GetMethod("EnableEntry")));
        Entry.Log("Isolated bootstrap installed; user saves/settings redirected.");
    }
    static void ObserveLog(string condition,string trace,LogType type)
    {
        if(type!=LogType.Exception && !condition.StartsWith("Simulation error:",StringComparison.Ordinal))return;
        Entry.Log("FAILURE: "+condition+Environment.NewLine+trace);
        File.WriteAllText(Path.Combine(Entry.Root,"failed.marker"),condition+Environment.NewLine+trace);
        FatalFailure=true;
    }
    public static bool LocalData(ref string __result){__result=Path.Combine(Entry.Root,"UserData");Directory.CreateDirectory(__result);return false;}
    public static void RewriteTerrainAssembly(ref byte[] rawAssembly)
    {
        if(HundredTiles.ExperimentalPatch.PlannedTranspiler.Hash(rawAssembly)=="F31ECBDB14A27195C25CB84175286674064E0E9B7E9D483D8ED5DCD3AB673896" && File.Exists(Path.Combine(Entry.Root,"moveit-reordered.dll")))
        {rawAssembly=File.ReadAllBytes(Path.Combine(Entry.Root,"moveit-reordered.dll"));Entry.Log("Injected metadata-reordered Move It assembly with original pinned disk file retained.");return;}
        if(HundredTiles.ExperimentalPatch.PlannedTranspiler.Hash(rawAssembly)!="E8DA4A01A0F0334D30FF8873CAD15E352D1EEB5359AF526E9367FD2BB77DCC88")return;
          string trigger=Path.Combine(Entry.Root,"landscaping-trigger.dll");
          if(File.Exists(trigger))Assembly.LoadFrom(Path.Combine(Entry.Root,"BehaviourUpdater.dll"));
          rawAssembly=File.ReadAllBytes(File.Exists(trigger)?trigger:Path.Combine(Entry.Root,"landscaping-reordered.dll"));
          if(File.Exists(trigger))Entry.Log("Injected reviewed FPS Booster OnEnable RegisterBehaviour trigger with metadata reorder; using real pinned BehaviourUpdater DLL.");
          typeof(Assembly).GetMethod("ReflectionOnlyLoad",new[]{typeof(byte[])}).Invoke(null,new object[]{rawAssembly});
          Entry.Log("Primed reflection-only reference cache with rewritten landscaping DLL; disk bytes remain original.");
          Entry.Log("Injected metadata-reordered Extra Landscaping Tools into actual PluginManager memory loading; original pinned disk DLL retained.");
    }
    public static bool SkipMigration(){return false;}
    public static void BeforeStarter()
    {
        if(runtimeInstalled)return;
        PluginManager.noWorkshop=true;PackageManager.noWorkshop=true;
        if(EntryMode){runtimeInstalled=true;Entry.Log("Testing actual local-plugin IUserMod entry; development patch installer bypass disabled.");return;}
        // Deliberate development-only execution of reviewed connections. The
        // production entry still refuses incomplete coverage and is untouched.
        var connections=Installer.Connections();var harmony=new Harmony("HundredTiles.IsolatedIntegration.Runtime");
        UtilitySaveBridge.Register();ServiceSaveBridge.Register();
        foreach(var hook in connections)
            harmony.Patch(hook.Target,Method(hook.Prefix),Method(hook.Postfix),Method(hook.Transpiler),Method(hook.Finalizer));
        Entry.Log("Applied "+connections.Count+" reviewed connections in isolated Unity test.");
        runtimeInstalled=true;
    }
    static HarmonyMethod Method(MethodInfo method){return method==null?null:new HarmonyMethod(method);}
    public static void EnableEntry()
    {
        if(!EntryMode)return;
        bool moveIt=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_COMPAT")=="1";
        bool terrain=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_TERRAIN")=="1";
        if(terrain)foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
        {
            if(Path.GetFileName(plugin.modPath)=="NaturalResourcesBrush")plugin.isEnabled=true;
            if(Path.GetFileName(plugin.modPath)=="MoveIt")plugin.isEnabled=false;
        }
        if(moveIt)foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
            if(Path.GetFileName(plugin.modPath)=="MoveIt")plugin.isEnabled=true;
        foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
            if(Path.GetFileName(plugin.modPath)=="HundredTiles" || Path.GetFileName(plugin.modPath)=="ForceRoadConnect")plugin.isEnabled=true;
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_REVIEWED")=="1")foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())foreach(var assembly in plugin.GetAssemblies())if(assembly.GetName().Name=="BuildingControl" || assembly.GetName().Name=="FreeUtilities" || assembly.GetName().Name=="TerrainLink" || assembly.GetName().Name=="QuayTools" || assembly.GetName().Name=="CustomZoneMixer" || assembly.GetName().Name=="RightOfWay" || assembly.GetName().Name=="SimulationGuard" || assembly.GetName().Name=="FPSCamera" || assembly.GetName().Name=="HyperLoad" || assembly.GetName().Name=="PassengerLineBuilder" || assembly.GetName().Name=="AutoHistoricalPlop" || assembly.GetName().Name=="AreaPOBuilder" || assembly.GetName().Name=="FreeLotEditor" || assembly.GetName().Name=="BuildingLimitRemoval" || assembly.GetName().Name=="ClearFpsBoost" || assembly.GetName().Name=="LoadTimeRemaining")plugin.isEnabled=true;
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_NETWORK_CAPACITY")=="1")foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())if(Path.GetFileName(plugin.modPath)=="MoreNetworkCapacityRenew")plugin.isEnabled=true;
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SIMULATION_REPAIRS")=="1")foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())if(Path.GetFileName(plugin.modPath)=="TrafficManager" || Path.GetFileName(plugin.modPath)=="RoadUtilityPoles" || Path.GetFileName(plugin.modPath)=="RealismLite")plugin.isEnabled=true;
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_STARTUP_REPAIRS")=="1")foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())if(Path.GetFileName(plugin.modPath)=="CameraSmoothFps" || Path.GetFileName(plugin.modPath)=="TrainRidershipBooster" || Path.GetFileName(plugin.modPath)=="AlwaysTrainCommute")plugin.isEnabled=true;
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_MODE")=="EditorEntry")
        {if(Installer.IsInstalled)throw new Exception("City patches active in editor startup profile.");Entry.Log("PASS: actual enabled plugin selected editor profile without city patches.");}
        else VerifyRuntime();
    }
    public static void VerifyRuntime()
    {
        int owned=0;
        bool terrain=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_TERRAIN")=="1";
        bool moveIt=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_COMPAT")=="1";
        foreach(var target in Harmony.GetAllPatchedMethods())
        {var info=Harmony.GetPatchInfo(target);if(info!=null)foreach(string owner in info.Owners)if(owner==Installer.Owner){owned++;break;}}
        int extra=0;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){if(assembly.GetName().Name=="TerrainLink")extra+=5;if(assembly.GetName().Name=="RightOfWay")extra+=2;if(assembly.GetName().Name=="AutoHistoricalPlop")extra+=1;}
        int expected=(terrain?474:moveIt?468:458)+(ForceRoadCompatibility.Reviewed?3:0)+extra;
        if(owned!=expected){Entry.Log("FAILURE: actual local-plugin entry installed "+owned+" targets, expected "+expected+".");FatalFailure=true;return;}
        Entry.Log("PASS: actual PluginManager/IUserMod/HarmonyHelper entry installed the production city-runtime patches.");
    }
    public static void AfterStarter()
    {
        if(runnerCreated)return;runnerCreated=true;
        var go=new GameObject("HundredTiles isolated verification");UnityEngine.Object.DontDestroyOnLoad(go);go.AddComponent<Runner>();
        Entry.Log("Unity verification runner created.");
    }
}

