using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using ColossalFramework;
using ColossalFramework.Packaging;
using ColossalFramework.UI;
using HundredTiles.Core;
using UnityEngine;

public sealed class Runner : MonoBehaviour
{
    static string Mode { get { return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_MODE")??"Auto"; } }
    static bool Population {get{return Mode.StartsWith("Stress");}}
    static bool Landscape { get { return Mode.StartsWith("Landscape"); } }
    static bool Supply { get { return Mode.StartsWith("Supply") || (Landscape && Mode!="LandscapeEntryFresh"); } }
    static bool Release { get { return Mode=="SupplyEntrySave" || Mode=="SupplyEntryReload" || Mode=="SupplyEntryGraphics" || (Landscape && Mode!="LandscapeEntryFresh"); } }
    static bool Outer { get { return Mode.StartsWith("Outer") || Supply; } }
    static bool StartupCopy { get { return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_STARTUP_COPY")=="1"; } }
    static string SaveName { get { return Population?LiveLargeCityChecks.SaveName:LiveConsumerChecks.Enabled?LiveConsumerChecks.SaveName:Mode.StartsWith("Trial324")?(Live324Trial.Migration?"HundredTiles_324_Migration_Verification":Live324Trial.Far?"HundredTiles_324_Far_Verification":"HundredTiles_324_Verification"):StartupCopy && Release?"HundredTiles_Startup_Repair_Verification":Landscape?"HundredTiles_Landscape_Repair_Verification":Release?"HundredTiles_Release_Verification":Supply?"HundredTiles_OuterSupply_Verification":Outer?"HundredTiles_OuterGameplay_Verification":"HundredTiles_Isolated_Verification"; } }
    static string Record { get { return Population?LiveLargeCityChecks.Record:LiveConsumerChecks.Enabled?(LiveConsumerChecks.LongRun?"trial324-consumers-longrun-saved.marker":LiveConsumerChecks.Wastewater?"trial324-consumers-sewage-saved.marker":"trial324-consumers-saved.marker"):Mode.StartsWith("Trial324")?(Live324Trial.Migration?"trial324-migration-saved.marker":Live324Trial.Far?"trial324-far-saved.marker":"trial324-saved.marker"):StartupCopy && Release?"startup-saved.marker":Landscape?"landscape-saved.marker":Release?"release-saved.marker":Supply?"supply-saved.marker":Outer?"outer-saved.marker":"saved.marker"; } }
    bool landscapeFlow,textureChecksRun,trialGroundPrepared,longConsumerStarted,longPopulationStarted;float trialGroundReady;
    int phase;float began;bool verifyingReload;volatile bool workerCompleted;string inputHash;
    void Awake(){began=Time.realtimeSinceStartup;}
    void Update()
    {
        try
        {
            if(Bootstrap.FatalFailure){phase=9;Application.Quit();return;}
            float timeout=Population?1800:600;
            if(LiveConsumerChecks.LongRun){string seconds=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_LONG_SECONDS");timeout=Math.Max(timeout,(string.IsNullOrEmpty(seconds)?180:float.Parse(seconds,System.Globalization.CultureInfo.InvariantCulture))+480);}
            if(Time.realtimeSinceStartup-began>timeout)throw new TimeoutException("Isolated game test timed out in phase "+phase);
            if(!Singleton<LoadingManager>.exists)return;
            var loading=Singleton<LoadingManager>.instance;
            if(phase==0)
            {
                if(loading.m_currentlyLoading || UIView.GetAView()==null)return;
                if(Mode=="CatalogEntry"){SaveCatalogChecks.Run();phase=9;Application.Quit();return;}
                if(Mode=="EditorEntry" || Mode=="MapEditorEntry")
                {
                    if(HundredTiles.Runtime.Installer.IsInstalled)throw new Exception("City patches active before editor load.");
                    foreach(var metadata in PackageManager.FilterAssets(UserAssetType.SystemMapMetaData))
                    {
                        phase=20;
                        if(Mode=="MapEditorEntry")loading.LoadLevel(metadata,"MapEditor","InMapEditor",new SimulationMetaData{m_gameInstanceIdentifier=Guid.NewGuid().ToString(),m_newMapAppVersion=222554384u,m_updateMode=SimulationManager.UpdateMode.NewMap});
                        else loading.LoadLevel(metadata.Instantiate<SystemMapMetaData>().assetRef,"AssetEditor","InAssetEditor",new SimulationMetaData{m_gameInstanceIdentifier=Guid.NewGuid().ToString(),m_updateMode=SimulationManager.UpdateMode.NewAsset},true);
                        Entry.Log("Requested real editor load with HundredTiles enabled: "+Mode+" / "+metadata.name);return;
                    }
                    throw new Exception("Built-in editor terrain missing.");
                }
                if(!textureChecksRun && Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_STARTUP_REPAIRS")=="1"){LiveTextureDiagnostics.VerifyRepair();textureChecksRun=true;}
                bool reload=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_MODE")!="Create" && File.Exists(Path.Combine(Entry.Root,Record));
                if(Mode=="LandscapeEntryFresh" || Mode=="BaseEntryCreate" || Mode=="Trial324EntryCreate" || Mode=="Trial324EntrySave")reload=false;
                if(Mode=="Trial324EntryMigrate" || Population || (Mode=="Trial324EntrySave" && LiveConsumerChecks.Enabled))reload=true;
                if(Mode=="OuterCreate" || Mode=="SupplyCreate" || Mode=="SupplyEntrySave" || Mode=="LandscapeEntryCreate")reload=true;
                Package.Asset selected=null;
                foreach(var asset in PackageManager.FilterAssets(reload?UserAssetType.SaveGameMetaData:UserAssetType.MapMetaData))
                {
                    string desired=Mode=="Stress324LongRunEntrySave"?LiveLargeCityChecks.ShortSaveName:Mode=="Stress100EntrySave"?Live324Trial.MigrationSource:Mode=="Stress324EntryMigrate"?LiveLargeCityChecks.Source:Mode=="Trial324EntryMigrate"?Live324Trial.MigrationSource:Mode=="LandscapeEntryCreate"?"HundredTiles_Landscape_Verification":Mode=="OuterCreate"?"HundredTiles_Isolated_Verification":Mode=="SupplyCreate"?"HundredTiles_OuterGameplay_Verification":Mode=="SupplyEntrySave"?"HundredTiles_OuterSupply_Verification":LiveConsumerChecks.Enabled && Mode=="Trial324EntrySave"?(LiveConsumerChecks.LongRun?"HundredTiles_324_Consumers_Sewage_Verification":LiveConsumerChecks.Wastewater?LiveConsumerChecks.BaseSaveName:"HundredTiles_324_Far_Verification"):SaveName;
                    if(reload && (asset.name!=desired || !string.Equals(Path.GetFullPath(asset.package.packagePath),Path.GetFullPath(Path.Combine(Entry.Root,"UserData/Saves/"+desired+".crp")),StringComparison.OrdinalIgnoreCase)))continue;
                    selected=asset;break;
                }
                if(selected==null)return;
                if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_STARTUP_REPAIRS")=="1")foreach(var metadata in PackageManager.FilterAssets(UserAssetType.CustomAssetMetaData))
                    if(metadata.package.packageName=="2093619702")
                    {
                        var definition=metadata.Instantiate<CustomAssetMetaData>();
                        if(definition!=null && definition.type==CustomAssetMetaData.Type.SubBuilding){metadata.isEnabled=false;Entry.Log("FIXTURE: disabled standalone embedded sub-building metadata; parent remains enabled to verify dependency recovery.");}
                    }
                if(reload && Mode!="Trial324EntryMigrate" && Mode!="OuterCreate" && Mode!="SupplyCreate" && Mode!="SupplyEntrySave" && Mode!="LandscapeEntryCreate" && !(Population && (Mode.EndsWith("Save") || Mode.EndsWith("Migrate"))) && !(LiveConsumerChecks.Enabled && Mode=="Trial324EntrySave"))
                {
                    inputHash=FileHash();var record=File.ReadAllText(Path.Combine(Entry.Root,Record)).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
                    if(record.Length!=2 || record[1]!=inputHash)throw new Exception("Reload input differs from the recorded completed save.");
                    Entry.Log("Reload input SHA-256: "+inputHash);
                }
                var meta=new SimulationMetaData{m_CityName=SaveName,m_gameInstanceIdentifier=Guid.NewGuid().ToString(),m_currentDateTime=DateTime.Now,m_startingDateTime=DateTime.Now,m_disableAchievements=SimulationMetaData.MetaBool.True,m_updateMode=reload?SimulationManager.UpdateMode.LoadGame:SimulationManager.UpdateMode.NewGameFromMap};
                if(reload){string metadataPath=Path.GetFullPath(selected.package.packagePath);selected=selected.Instantiate<SaveGameMetaData>().assetRef;if(selected==null || !string.Equals(metadataPath,Path.GetFullPath(selected.package.packagePath),StringComparison.OrdinalIgnoreCase))throw new Exception("Save metadata resolved a different package; refusing ambiguous load.");Entry.Log("Verified exact reload package path: "+metadataPath);}
                else{var map=selected.Instantiate<MapMetaData>();meta.m_MapName=map.mapName;meta.m_environment=map.environment;selected=map.assetRef;}
                if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_BUILDING_LOAD_TRACE")=="1")BuildingLoadTrace.Install();phase=reload?3:1;Entry.Log("Loading isolated "+(reload?"saved city":"new map")+": "+selected.name);
                // Keep snapshot/geometry checks independent of elapsed loading
                // time. Normal-operation fixtures explicitly resume afterwards.
                if(Population || Mode.StartsWith("Trial324"))Singleton<SimulationManager>.instance.ForcedSimulationPaused=true;
                loading.LoadLevel(selected,"Game","InGame",meta);return;
            }
            if(loading.m_currentlyLoading || !loading.m_simulationDataLoaded || !loading.m_loadingComplete)return;
            if(phase==1 || phase==3 || phase==20)LiveRunDiagnostics.DisableEdgeScroll();
            if((phase==1 || phase==3) && Population)
            {
                Singleton<SimulationManager>.instance.ForcedSimulationPaused=true;
                if(Mode=="Stress100EntrySave")LiveLargeCityChecks.Prepare();phase=34;workerCompleted=false;
                Singleton<SimulationManager>.instance.AddAction(delegate
                {
                    try
                    {
                        if(!HundredTiles.Runtime.Installer.IsInstalled)throw new Exception("Population fixture runtime missing.");
                        if(Mode=="Stress100EntrySave"){if(HundredTiles.Runtime.BulkUnlock.UnlockAll()!=100)throw new Exception("Population source must have 100 native purchases.");LiveLargeCityChecks.Create();}
                        else if(Mode=="Stress324EntryMigrate"){Live324Trial.CheckShapes();Live324Trial.CheckMigratedTerrain();if(HundredTiles.Runtime.BulkUnlock.UnlockAll()!=324)throw new Exception("Population migration purchases incomplete.");Live324Trial.CheckMigratedWaterAfterUnlock();if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SEA")=="1")LiveSeaChecks.Run();}
                        LiveLargeCityChecks.Check(Mode=="Stress324LongRunEntrySave");
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_LONG_PATH")=="1")LiveLongPathChecks.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_NETWORK_CAPACITY")=="1")LiveNetworkCapacityChecks.Run();
                        if(Mode.EndsWith("Reload") && Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_OUTSIDE_TRAFFIC")=="1")LiveOutsideTrafficChecks.Run();
                        if(Mode.EndsWith("Reload") && FileHash()!=inputHash)throw new Exception("Population reload changed its source file.");
                    }
                    catch(Exception error){Fail(error);}finally{workerCompleted=true;}
                });return;
            }
            if(phase==34 && workerCompleted)
            {
                if(Mode=="Stress324LongRunEntrySave" && !longPopulationStarted){longPopulationStarted=true;phase=35;StartCoroutine(LivePopulationRunChecks.Run(delegate(Exception error){if(error!=null)Fail(error);phase=34;workerCompleted=true;}));return;}
                if(Mode.EndsWith("Reload")){phase=9;if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_OUTSIDE_TRAFFIC")=="1"){Singleton<SimulationManager>.instance.ForcedSimulationPaused=false;Singleton<SimulationManager>.instance.SimulationPaused=false;StartCoroutine(LiveOutsideTrafficChecks.CheckVehicles());}else Application.Quit();return;}
                var panel=UIView.library.Get<SavePanel>("SavePanel");if(panel==null)panel=UIView.library.Show<SavePanel>("SavePanel");if(panel==null || !panel.SaveGame(SaveName))return;phase=5;Entry.Log("Requested native population fixture save.");return;
            }
            if(phase==20)
            {
                LiveStartupProfileChecks.Run(true);
                if(HundredTiles.Runtime.Installer.IsInstalled || !HundredTiles.Runtime.SessionGate.EditorSession)throw new Exception("Editor session gate state incorrect.");
                bool mapEditor=Mode=="MapEditorEntry";
                if((Singleton<ToolManager>.instance.m_properties.m_mode & (mapEditor?ItemClass.Availability.MapEditor:ItemClass.Availability.AssetEditor))==0)throw new Exception("Editor mode missing.");
                if(Singleton<GameAreaManager>.instance.m_areaGrid.Length!=25 || Singleton<TerrainManager>.instance.RawHeights.Length!=1081*1081)throw new Exception("Editor grids were expanded.");
                foreach(var target in HarmonyLib.Harmony.GetAllPatchedMethods())
                {
                    var patches=HarmonyLib.Harmony.GetPatchInfo(target);
                    foreach(string owner in patches.Owners)if(owner==HundredTiles.Runtime.Installer.Owner)throw new Exception("City patch leaked into editor: "+target);
                }
                string message=null;HundredTiles.Runtime.BulkUnlock.Request(delegate(string value){message=value;});
                if(message!="都市を読み込んでから押してください")throw new Exception("Editor unlock action did not refuse safely.");
                if(mapEditor)LiveEditorUndoChecks.Run();
                Entry.Log("PASS: real "+(mapEditor?"map":"asset")+" editor opened with enabled HundredTiles; vanilla 25-area/1081-height grids, no city hooks, unlock safely refused.");
                File.WriteAllText(Path.Combine(Entry.Root,mapEditor?"map-editor-passed.marker":"editor-passed.marker"),"PASS");
                var denied=loading.LoadLevel(null,"Game","InGame",new SimulationMetaData{m_updateMode=SimulationManager.UpdateMode.LoadGame});
                if(denied!=null || loading.m_currentlyLoading || HundredTiles.Runtime.Installer.IsInstalled || Singleton<GameAreaManager>.instance.m_areaGrid.Length!=25)throw new Exception("Editor-to-city switch was not refused before changing grids.");
                UIView.library.Hide("ExceptionPanel");
                Entry.Log("PASS: editor-to-city load safely refused with restart notice; editor grids and loader state unchanged.");
                phase=9;StartCoroutine(EditorScreenshot());return;
            }
            if(phase==1 && Mode=="Trial324EntryCreate")
            {
                if(!loading.m_loadingComplete || !loading.m_simulationDataLoaded || loading.m_currentlyLoading)return;
                Live324Trial.CheckShapes();phase=9;Application.Quit();return;
            }
            if((phase==1 || phase==3) && (Mode=="Trial324EntrySave" || Mode=="Trial324EntryMigrate" || Mode=="Trial324EntryReload"))
            {
                if(!loading.m_loadingComplete || !loading.m_simulationDataLoaded || loading.m_currentlyLoading)return;
                bool trialReload=phase==3 && Mode!="Trial324EntryMigrate" && !(LiveConsumerChecks.Enabled && Mode=="Trial324EntrySave");if(Live324Trial.Far)Live324Trial.PrepareFar();
                if(Live324Trial.Far && !trialReload && !trialGroundPrepared && !LiveConsumerChecks.Enabled){phase=31;workerCompleted=false;Singleton<SimulationManager>.instance.AddAction(delegate{try{if(HundredTiles.Runtime.BulkUnlock.UnlockAll()!=324)throw new Exception("324 preparation purchases incomplete.");Live324Trial.PrepareGround();}catch(Exception error){Fail(error);}finally{workerCompleted=true;}});return;}
                phase=30;workerCompleted=false;
                Singleton<SimulationManager>.instance.AddAction(delegate{
                    try{
                        Live324Trial.CheckShapes();
                        if(trialReload && Live324Trial.Far)LiveSupplyChecks.Observe("before independent probes");
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_NETWORK_CAPACITY")=="1")LiveNetworkCapacityChecks.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SEA")=="1")LiveSeaChecks.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_COMPAT")=="1")LiveCompatibilityChecks.Run();
                        if(trialReload && Live324Trial.Far)LiveSupplyChecks.Observe("after independent probes");
                        if(!trialReload){if(Mode=="Trial324EntryMigrate")Live324Trial.CheckMigratedTerrain();if(HundredTiles.Runtime.BulkUnlock.UnlockAll()!=324)throw new Exception("Native 324 purchases did not complete.");var terrain=Singleton<TerrainManager>.instance;int index=2161*2161-1;terrain.RawHeights[index]=12345;terrain.BlockHeights[index]=12345;terrain.BlockHeightTargets[index]=12345;Entry.Log("PASS: native transactions unlocked all 324 tiles; far terrain sentinel prepared.");if(LiveConsumerChecks.Enabled)LiveConsumerChecks.Create();else if(Live324Trial.Far)Live324Trial.CreateFar();}
                        else{if(Singleton<GameAreaManager>.instance.m_areaCount!=324 || Singleton<TerrainManager>.instance.RawHeights[2161*2161-1]!=12345)throw new Exception("Native 324 save did not preserve ownership/far terrain.");if(FileHash()!=inputHash)throw new Exception("324 reload changed its input save.");if(LiveConsumerChecks.Enabled)LiveConsumerChecks.Reload();if(Live324Trial.Far){LiveTerrainToolChecks.Run();Live324Trial.CheckFar();if(Live324Trial.CompareRender)Live324Trial.CreateRenderComparison();}File.WriteAllText(Path.Combine(Entry.Root,Live324Trial.Far?"trial324-far-reload-passed.marker":"trial324-reload-passed.marker"),"PASS\n"+inputHash);Entry.Log("PASS: separate-process native 324 save reload preserved all 324 purchases and far terrain sentinel; input save unchanged.");}
                    }catch(Exception error){Fail(error);}finally{workerCompleted=true;}
                });return;
            }
            if(phase==31 && workerCompleted){trialGroundReady=Time.realtimeSinceStartup+2;phase=32;return;}
            if(phase==32 && Time.realtimeSinceStartup>=trialGroundReady){trialGroundPrepared=true;phase=1;return;}
            if(phase==30 && workerCompleted)
            {
                if(LiveConsumerChecks.LongRun && !longConsumerStarted){longConsumerStarted=true;phase=33;StartCoroutine(LiveConsumerChecks.NormalRun(delegate(Exception error){if(error!=null)Fail(error);phase=30;workerCompleted=true;}));return;}
                if(Mode=="Trial324EntryReload"){phase=9;StartCoroutine(FinishTrialReload());return;}
                var trialPanel=UIView.library.Get<SavePanel>("SavePanel");if(trialPanel==null)trialPanel=UIView.library.Show<SavePanel>("SavePanel");if(trialPanel==null || !trialPanel.SaveGame(SaveName))return;phase=5;Entry.Log("Requested real 324 trial SavePanel save.");return;
            }
            if(phase==1 || phase==3)
            {
                if(Mode.IndexOf("Entry",StringComparison.Ordinal)>=0)
                {
                    LiveStartupProfileChecks.Run(false);
                    Bootstrap.VerifyRuntime();
                    var denied=loading.LoadLevel(null,"AssetEditor","InAssetEditor",new SimulationMetaData{m_updateMode=SimulationManager.UpdateMode.NewAsset},true);
                    if(denied!=null || loading.m_currentlyLoading || !HundredTiles.Runtime.Installer.IsInstalled || Singleton<GameAreaManager>.instance.m_areaGrid.Length!=100)throw new Exception("City-to-editor switch was not refused before changing grids.");
                    UIView.library.Hide("ExceptionPanel");
                    Entry.Log("PASS: city-to-editor load safely refused with restart notice; expanded grids and loader state unchanged.");
                }
                bool reload=phase==3;verifyingReload=reload;workerCompleted=false;phase=4;
                if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_CONSTRUCTION")=="1" || Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_TAXI")=="1")LiveConstructionChecks.Prepare();
                if(Release)LiveTerrainToolChecks.Prepare();
                Singleton<SimulationManager>.instance.AddAction(delegate
                {
                    try
                    {
                        Singleton<SimulationManager>.instance.ForcedSimulationPaused=true;
                        Inspect(reload);
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_IPT")=="1")LiveIptCacheChecks.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_STARTUP_REPAIRS")=="1")LiveStartupChecks.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_CONSTRUCTION")=="1")LiveConstructionChecks.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_OUTSIDE_TRAFFIC")=="1")LiveOutsideTrafficChecks.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_EXPORT324_SOURCE")=="1")LiveLayoutExport.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SEA")=="1")LiveSeaChecks.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_TAXI")=="1")LiveTaxiChecks.Run();
                        if(Outer){if(Mode=="OuterCreate"){OuterObjects.Create();reload=false;verifyingReload=false;}else OuterObjects.Check();}
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SIMULATION_REPAIRS")=="1")foreach(var hook in HundredTiles.Runtime.Installer.Connections()){var patches=HarmonyLib.Harmony.GetPatchInfo(hook.Target);if(patches!=null)foreach(string owner in patches.Owners)if(owner=="me.tmpe")Entry.Log("TMPE shared target: "+hook.Target.DeclaringType.FullName+"."+hook.Target.Name);}
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SAVE_SAFETY")=="1")LiveSaveSafetyChecks.Run();
                        if(Supply && !Landscape){if(Mode=="SupplyCreate"){LiveSupplyChecks.Create();reload=false;verifyingReload=false;}else LiveSupplyChecks.Check();}
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_NETWORK_CAPACITY")=="1")LiveNetworkCapacityChecks.Run();
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SIMULATION_REPAIRS")=="1")LiveSimulationRepairsChecks.Run();
                        if(Release){LivePathChecks.Run();LiveTerrainToolChecks.Run();}
                        if(Landscape && Mode!="LandscapeEntryFresh"){if(Mode=="LandscapeEntryCreate"){LiveLandscapeChecks.Create();Inspect(false);reload=false;verifyingReload=false;}else LiveLandscapeChecks.Check();}
                        if(Mode=="LandscapeEntryFresh")
                        {
                            if(!Singleton<SimulationManager>.instance.m_serializableDataStorage.ContainsKey(HundredTiles.Runtime.LandscapeGeneration.Key))throw new Exception("Actual loading extension did not generate the new map landscape automatically.");
                            Entry.Log("PASS: actual ICities loading extension automatically generated nature on a fresh expanded map; completion marker set without options or harness generation calls.");
                        }
                        if(Mode=="SupplyEntrySave"){LiveGridChecks.Shapes();LiveGridChecks.ServiceQuery();LiveGridChecks.ServiceCycle();Inspect(false);reload=false;verifyingReload=false;}
                        if(reload){LiveGridChecks.Shapes();LiveGridChecks.ServiceQuery();LiveGridChecks.ServiceCycle();if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_MODE")=="Graphics")LiveGameplayChecks.PurchaseAll();if(FileHash()!=inputHash)throw new Exception("Reload verification changed the input save.");File.WriteAllText(Path.Combine(Entry.Root,"reload-passed.marker"),"PASS\n"+inputHash);Entry.Log("PASS: real Unity saved city reloaded with expanded service sentinel; input save unchanged, SHA-256 "+inputHash);}
                        else{File.WriteAllText(Path.Combine(Entry.Root,"ready-to-save.marker"),"READY");Entry.Log("Expanded live grids checked; service sentinel placed on added corner.");}
                        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_COMPAT")=="1")LiveCompatibilityChecks.Run();
                        workerCompleted=true;
                    }
                    catch(Exception error){Fail(error);}
                });return;
            }
            if(phase==4 && !workerCompleted)return;
            if(phase==4 && Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_COMPAT")=="1")LiveCompatibilityChecks.Hover();
            if(phase==4 && Mode=="LandscapeEntryFresh"){phase=9;StartCoroutine(LiveLandscapeChecks.FreshFlow());return;}
            if(phase==4 && Mode=="LandscapeEntryReload"){phase=9;StartCoroutine(LiveLandscapeChecks.OptionsAndScreenshot());return;}
            if(phase==4 && Mode=="LandscapeEntryCreate" && !landscapeFlow){landscapeFlow=true;phase=6;StartCoroutine(LiveLandscapeChecks.Flow(delegate{Singleton<SimulationManager>.instance.AddAction(delegate{Inspect(false);workerCompleted=true;phase=4;});}));return;}
            if(phase==4 && verifyingReload && Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_OUTSIDE_TRAFFIC")=="1"){phase=9;StartCoroutine(LiveOutsideTrafficChecks.CheckVehicles());return;}
            if(phase==4 && verifyingReload && Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_WATER")=="1"){phase=9;LiveWaterTextureChecks.Run();Application.Quit();return;}
            if(phase==4 && verifyingReload && Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_OPTIONS")=="1"){phase=9;StartCoroutine(LiveOptionsChecks.Run());return;}
            if(phase==4 && verifyingReload && Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SIMULATION_REPAIRS")=="1"){phase=9;StartCoroutine(LiveSimulationRepairsChecks.Measure());return;}
            if(phase==4 && verifyingReload){phase=9;if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_MODE")=="Graphics" || Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_MODE")=="OuterGraphics" || Mode=="SupplyEntryGraphics"){LiveGameplayChecks.ServiceTexture();StartCoroutine(LiveGameplayChecks.Screenshot());}else Application.Quit();return;}
            if(phase==4 && !verifyingReload)
            {
                var panel=UIView.library.Get<SavePanel>("SavePanel");
                if(panel==null)panel=UIView.library.Show<SavePanel>("SavePanel");
                if(panel==null)return;if(!panel.SaveGame(SaveName))return;phase=5;Entry.Log("Requested real game SavePanel save.");
            }
            if(phase==5)
            {
                string file=Path.Combine(Entry.Root,"UserData/Saves/"+SaveName+".crp");
                if(!File.Exists(file) || SavePanel.isSaving)return;
                string hash=FileHash();File.WriteAllText(Path.Combine(Entry.Root,Record),new FileInfo(file).Length+"\n"+hash);phase=9;Entry.Log("Real .crp saved; SHA-256 "+hash+"; exiting for separate-process reload.");Application.Quit();
            }
        }
        catch(Exception error){phase=9;Fail(error);Application.Quit();}
    }
    static System.Collections.IEnumerator FinishTrialReload()
    {
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_COMPAT")=="1")LiveCompatibilityChecks.Hover();
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_CITY_UNDO_BUTTON")=="1")
        {
            var checks=LiveCityUndoButtonChecks.Run();
            while(true){bool next;try{next=checks.MoveNext();}catch(Exception error){Fail(error);Application.Quit();yield break;}if(!next)break;yield return checks.Current;}
        }
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_AREA_OVERLAY")=="1")
        {
            var checks=LiveAreaOverlayChecks.Run();
            while(true){bool next;try{next=checks.MoveNext();}catch(Exception error){Fail(error);Application.Quit();yield break;}if(!next)break;yield return checks.Current;}
        }
        if(Live324Trial.Far){var screenshots=Live324Trial.ScreenshotFar();while(screenshots.MoveNext())yield return screenshots.Current;}
        else Application.Quit();
    }
    static System.Collections.IEnumerator EditorScreenshot()
    {
        yield return new WaitForSeconds(2f);
        yield return new WaitForEndOfFrame();
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Entry.Root,Mode=="MapEditorEntry"?"map-editor.png":"editor.png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        yield return new WaitForSeconds(2f);
        Application.Quit();
    }
    static void Inspect(bool reload)
    {
        var service=Singleton<ImmaterialResourceManager>.instance;
        var final=(ushort[])typeof(ImmaterialResourceManager).GetField("m_localFinalResources",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);
        var temp=(ushort[])typeof(ImmaterialResourceManager).GetField("m_localTempResources",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);
        if(final.Length!=ServiceGrid.Values || temp.Length!=ServiceGrid.Values)throw new Exception("Service grids were not expanded in Unity.");
        int index=(499*500+499)*35+7;
        if(reload){if(final[index]!=32123 || temp[index]!=12345)throw new Exception("Extended corner service values did not survive game restart/reload.");}
        else{final[index]=32123;temp[index]=12345;service.AreaModified(499,499,499,499);}
        Entry.Log("Live service grids: "+final.Length+" values each; corner "+final[index]+"/"+temp[index]);
    }
    static string FileHash(){using(var stream=File.OpenRead(Path.Combine(Entry.Root,"UserData/Saves/"+SaveName+".crp")))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    static void Fail(Exception error){Entry.Log("FAILURE: "+error);File.WriteAllText(Path.Combine(Entry.Root,"failed.marker"),error.ToString());Bootstrap.FatalFailure=true;}
}





