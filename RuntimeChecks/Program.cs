using System;
using System.IO;
using System.Reflection;
using HundredTiles.Runtime;
using HundredTiles.SaveBridge;

class Program
{
    static string gameDirectory,harmonyDirectory;
    static int Main(string[] args)
    {
        if(args.Length<2 || args.Length>4)throw new ArgumentException("Game managed and Harmony directories required.");
        gameDirectory=Path.GetFullPath(args[0]);harmonyDirectory=Path.GetFullPath(args[1]);
        memoryLoad=args.Length>=3;
        rewrittenFile=args.Length==4?args[3]:null;
        AppDomain.CurrentDomain.AssemblyResolve+=Resolve;
        try{return Run();}catch(Exception error){Console.WriteLine("FAIL: "+error);return 1;}
    }
    static bool memoryLoad;
    static string rewrittenFile;
    static Assembly Resolve(object sender,ResolveEventArgs args)
    {
        string filename=new AssemblyName(args.Name).Name+".dll";
        if(filename=="0Harmony.dll")return Assembly.LoadFrom(Path.Combine(harmonyDirectory,"CitiesHarmony.Harmony.dll"));
        if(filename=="BehaviourUpdater.dll" && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HUNDREDTILES_ELT_UPDATER")))return Assembly.LoadFrom(Environment.GetEnvironmentVariable("HUNDREDTILES_ELT_UPDATER"));
        if(filename=="Assembly-CSharp.dll" && rewrittenFile!=null)return Assembly.Load(File.ReadAllBytes(rewrittenFile));
        foreach(string folder in new[]{gameDirectory,harmonyDirectory})
        {string path=Path.Combine(folder,filename);if(File.Exists(path))return memoryLoad && folder==gameDirectory ? Assembly.Load(File.ReadAllBytes(path)) : Assembly.LoadFrom(path);}
        return null;
    }
    static int Run()
    {
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_LEGACY_OWNERSHIP")=="1"){LegacyOwnershipChecks.Run();return 0;}
        HundredTiles.ExperimentalPatch.AssemblyFile.ManagedDirectory=gameDirectory;
        string updater=Environment.GetEnvironmentVariable("HUNDREDTILES_ELT_UPDATER");if(!string.IsNullOrEmpty(updater))Assembly.LoadFrom(updater);
        string reviewFile=Environment.GetEnvironmentVariable("HUNDREDTILES_REVIEW_EXPORT");
        if(!string.IsNullOrEmpty(reviewFile)){
            var plugin=Assembly.LoadFrom(reviewFile);if(Environment.GetEnvironmentVariable("HUNDREDTILES_REVIEW_VALIDATE")=="1"){ReviewedCompatibility.Verify(plugin,reviewFile);var helperHooks=new System.Collections.Generic.List<HookSpec>();if(plugin.GetName().Name=="TerrainLink")foreach(string row in new[]{"TerrainLink.Sync.NetworkSync|Process","TerrainLink.Sync.NetworkSync|WorldToGrid","TerrainLink.Sync.BuildingSync|Process","TerrainLink.Sync.BuildingSync|WorldToGrid"}){var parts=row.Split('|');var target=plugin.GetType(parts[0]).GetMethod(parts[1],BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);new System.Collections.Generic.List<HarmonyLib.CodeInstruction>(ReviewedCompatibility.Grid(HarmonyLib.PatchProcessor.GetOriginalInstructions(target)));}if(plugin.GetName().Name=="TerrainLink"){var target=plugin.GetType("TerrainLink.Sync.DirtyAreas").GetMethod("Add");new System.Collections.Generic.List<HarmonyLib.CodeInstruction>(ReviewedCompatibility.Bounds(HarmonyLib.PatchProcessor.GetOriginalInstructions(target)));}if(plugin.GetName().Name=="RightOfWay")foreach(string name in new[]{"RefreshAround","CellIndex"}){var target=plugin.GetType("RightOfWay.Yielding.YieldScanner").GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);new System.Collections.Generic.List<HarmonyLib.CodeInstruction>(ReviewedCompatibility.Grid(HarmonyLib.PatchProcessor.GetOriginalInstructions(target)));}Console.WriteLine("PASS reviewed compatibility and spatial transforms: "+plugin.GetName().Name);return 0;}var roots=Environment.GetEnvironmentVariable("HUNDREDTILES_REVIEW_TYPES").Split(';');
            Console.WriteLine("assembly\t"+plugin.GetName().Name+"\t"+HundredTiles.ExperimentalPatch.PlannedTranspiler.Hash(File.ReadAllBytes(reviewFile)));
            foreach(var type in plugin.GetTypes()){
                bool selected=false;foreach(string root in roots)if(type.FullName==root || type.FullName.StartsWith(root+"+",StringComparison.Ordinal))selected=true;if(!selected)continue;
                foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))if(method.GetMethodBody()!=null)Console.WriteLine("method\t"+plugin.GetName().Name+"\t"+type.FullName+"\t"+method.Name+"\t"+HundredTiles.ExperimentalPatch.ReviewedMethod.SemanticHash(method));
            }return 0;
        }
        string export=Environment.GetEnvironmentVariable("HUNDREDTILES_ELT_EXPORT");
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_CONSTRUCTION_EXPORT")=="1")
        {
            var method=typeof(NetTool).GetMethod("MakeControlPoint");
            Console.WriteLine(HundredTiles.ExperimentalPatch.ReviewedMethod.SemanticHash(method));return 0;
        }
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TAXI_EXPORT")=="1"){
            foreach(var type in new[]{typeof(ResidentAI),typeof(TouristAI)}){var method=type.GetMethod("GetTaxiProbability",BindingFlags.Instance|BindingFlags.NonPublic);Console.WriteLine(type.Name+"|"+HundredTiles.ExperimentalPatch.ReviewedMethod.SemanticHash(method));} foreach(var method in typeof(CitizenManager).GetMethods())if(method.Name.StartsWith("CreateCitizen") || method.Name=="CreateUnits")Console.WriteLine(method); foreach(var constructor in typeof(CitizenInstance.Frame).GetConstructors())Console.WriteLine(constructor);foreach(var field in typeof(CitizenInstance).GetFields())if(field.Name.Contains("frame"))Console.WriteLine(field);return 0;
        }
        string forceExport=Environment.GetEnvironmentVariable("HUNDREDTILES_FORCE_EXPORT");
        if(!string.IsNullOrEmpty(forceExport)){
            var forceAssembly=Assembly.LoadFrom(forceExport); var type=forceAssembly.GetType("ForceRoadConnect.Connect.ConnectionResolver",true);
            var forceHooks=new System.Collections.Generic.List<HookSpec>(); ForceRoadCompatibility.AddHooks(forceAssembly,forceHooks); if(forceHooks.Count!=3)throw new Exception("ForceRoad hooks missing.");
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static))
                if((method.Name=="FindNearestNode" && method.GetParameters().Length==7) || method.Name=="FindNearestSegment" || method.Name=="GridCoordinate"){var rewritten=new System.Collections.Generic.List<HarmonyLib.CodeInstruction>(ForceRoadCompatibility.SpatialGrid(HarmonyLib.PatchProcessor.GetOriginalInstructions(method)));Console.WriteLine(method.Name+"|"+HundredTiles.ExperimentalPatch.ReviewedMethod.SemanticHash(method)+"|PASS reviewed grid replacement");}
            return 0;
        }
        string spatialExport=Environment.GetEnvironmentVariable("HUNDREDTILES_SPATIAL_EXPORT");
        if(!string.IsNullOrEmpty(spatialExport))
        {
            var assembly=Assembly.LoadFrom(spatialExport);
            string[][] rows=assembly.GetName().Name=="MoveIt"?new[]{new[]{"PropsManager","RaycastHoverInstance"},new[]{"PropsManager","GetMarqueeList"},new[]{"MoveItTool","RaycastHoverInstance"},new[]{"MoveItTool","GetMarqueeList"},new[]{"MoveItTool","FindOwnerBuilding"},new[]{"MoveItTool","UpdateRender"},new[]{"MoveableBuilding","AddToGrid"},new[]{"MoveableBuilding","RemoveFromGrid"},new[]{"NodeState","FindNearestNode"}}:new[]{new[]{"CapacityUpdater","OnBeforeSimulationTick"}};
            foreach(var row in rows)
            {
                var method=assembly.GetType(assembly.GetName().Name+"."+row[0],true).GetMethod(row[1],BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
                Console.WriteLine(row[0]+"|"+row[1]+"|"+HundredTiles.ExperimentalPatch.PlannedTranspiler.Hash(method.GetMethodBody().GetILAsByteArray())+"|"+HundredTiles.ExperimentalPatch.ReviewedMethod.SemanticHash(method));
            }
            return 0;
        }
        if(!string.IsNullOrEmpty(export))
        {
            var source=Assembly.LoadFrom(export).GetType("NaturalResourcesBrush.Detours.TerrainToolDetour",true);
            foreach(string name in new[]{"ResetUndoBuffer","OnEnable","SimulationStep","EndStroke","ApplyUndo","ApplyBrush"})
            {
                var method=source.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
                Console.WriteLine(name+"|"+HundredTiles.ExperimentalPatch.PlannedTranspiler.Hash(method.GetMethodBody().GetILAsByteArray())+"|"+HundredTiles.ExperimentalPatch.ReviewedMethod.SemanticHash(method));
                File.WriteAllText(Path.Combine("research",name+"-semantic.txt"),HundredTiles.ExperimentalPatch.ReviewedMethod.SemanticDescription(method));
            }
            return 0;
        }
        var connections=Installer.Connections();int fieldBindings=0,methodBindings=0;
        string spatialFixture=Environment.GetEnvironmentVariable("HUNDREDTILES_SPATIAL_FIXTURE");
        if(!string.IsNullOrEmpty(spatialFixture))
        {
            var fixture=Assembly.Load(File.ReadAllBytes(spatialFixture));var hooks=new System.Collections.Generic.List<HookSpec>();
            bool reject=Environment.GetEnvironmentVariable("HUNDREDTILES_SPATIAL_REJECT")=="1",rejected=false;
            try{if(fixture.GetName().Name=="MoveIt")MoveItCompatibility.AddReviewedHooks(fixture,Environment.GetEnvironmentVariable("HUNDREDTILES_SPATIAL_ORIGINAL"),hooks);else HeatingCompatibility.AddReviewedHooks(fixture,Environment.GetEnvironmentVariable("HUNDREDTILES_SPATIAL_ORIGINAL"),hooks);}
            catch(InvalidOperationException error){if(!reject || error.Message.IndexOf("helper changed",StringComparison.Ordinal)<0)throw;rejected=true;}
            int expected=fixture.GetName().Name=="MoveIt"?9:1;
            if(reject!=rejected || (!reject && hooks.Count!=expected))throw new Exception("Spatial compatibility integrity check failed.");
            Console.WriteLine(reject?"PASS: changed spatial stride rejected.":"PASS: "+expected+" spatial/heating helpers accepted with complete semantic integrity after metadata reorder.");
        }
        string eltFixture=Environment.GetEnvironmentVariable("HUNDREDTILES_ELT_FIXTURE");
        if(!string.IsNullOrEmpty(eltFixture))
        {
            var fixture=Assembly.Load(File.ReadAllBytes(eltFixture));var hooks=new System.Collections.Generic.List<HookSpec>();
            // Poison the reflection-only cache as a loader/rewriter can do in-game.
            typeof(Assembly).GetMethod("ReflectionOnlyLoad",new[]{typeof(byte[])}).Invoke(null,new object[]{File.ReadAllBytes(eltFixture)});
            bool reject=Environment.GetEnvironmentVariable("HUNDREDTILES_ELT_REJECT")=="1",rejected=false;
            try{LandscapingCompatibility.AddReviewedHooks(fixture,Environment.GetEnvironmentVariable("HUNDREDTILES_ELT_ORIGINAL"),hooks);}
            catch(InvalidOperationException error){if(!reject || error.Message.IndexOf("terrain helper changed: ",StringComparison.Ordinal)<0)throw;rejected=true;}
            if(reject!=rejected || (!reject && hooks.Count!=6))throw new Exception("Extra Landscaping Tools metadata integrity check failed.");
            Console.WriteLine(reject?"PASS: changed terrain stride rejected after metadata rewrite.":"PASS: all six Extra Landscaping Tools helpers accepted after metadata rewrite with instruction/reference/constant integrity preserved.");
        }
        foreach(var connection in connections)
        foreach(var hook in new[]{connection.Prefix,connection.Postfix,connection.Finalizer})
        {
            if(hook==null)continue;
            foreach(var parameter in hook.GetParameters())
            {
                if(!parameter.Name.StartsWith("___",StringComparison.Ordinal))continue;
                var field=connection.Target.DeclaringType.GetField(parameter.Name.Substring(3),BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
                var expected=parameter.ParameterType.IsByRef?parameter.ParameterType.GetElementType():parameter.ParameterType;
                if(field==null || field.FieldType!=expected)throw new InvalidOperationException("Invalid injected field: "+hook.Name+"."+parameter.Name);
                fieldBindings++;
            }
            methodBindings++;
        }
        if(!HundredTiles.ExperimentalPatch.PatchPlan.Embedded().CanActivate)throw new Exception("Reviewed city catalog activation scope missing.");
        Installer.Remove();
        foreach(bool original in new[]{false,true})
        {
            bool edge=original,state;BoundaryHooks.EdgeFog(ref edge,out state);
            if(edge || state!=original)throw new Exception("Edge fog suppression failed.");
            var failure=new InvalidOperationException("native fog failure");
            if(BoundaryHooks.RestoreEdgeFog(failure,ref edge,state)!=failure || edge!=original)throw new Exception("Edge fog restoration changed settings or swallowed failure.");
        }
        BoundaryHooks.CityMode("MainMenu",null);
        BoundaryHooks.CityMode("Game",new SimulationMetaData{m_updateMode=SimulationManager.UpdateMode.LoadGame});
        BoundaryHooks.CityMode("Game",new SimulationMetaData{m_updateMode=SimulationManager.UpdateMode.NewGameFromMap});
        foreach(string scene in new[]{"MapEditor","AssetEditor"})
        {try{BoundaryHooks.CityMode(scene,null);throw new Exception("Unsupported editor allowed.");}catch(NotSupportedException){}}
        foreach(int coordinate in new[]{-16,0,4799,4800,4815})
        {
            int x=coordinate,z=coordinate;BoundaryHooks.DetailCell(ref x,ref z);
            if(x!=Math.Max(0,Math.Min(4799,coordinate)) || z!=x)throw new Exception("Detailed cell padding boundary differs.");
        }
        var camera=typeof(BoundaryHooks).GetMethod("Camera");var cinematic=typeof(BoundaryHooks).GetMethod("Cinematic");
        var vector=camera.GetParameters()[0].ParameterType;
        foreach(float value in new[]{-10000f,-8640f,-1f,0f,960f,8640f,10559f,10560f,12000f})
        {
            var input=Activator.CreateInstance(vector,new object[]{value,123f,value});
            var arguments=new object[]{input,Activator.CreateInstance(vector)};
            if((bool)camera.Invoke(null,arguments))throw new Exception("Expanded camera delegates to the old boundary.");
            float expected=Math.Max(-8640f,Math.Min(10560f,value));
            if((float)vector.GetField("x").GetValue(arguments[1])!=expected || (float)vector.GetField("z").GetValue(arguments[1])!=expected || (float)vector.GetField("y").GetValue(arguments[1])!=123f)throw new Exception("Expanded camera boundary/height differs.");
            arguments=new object[]{input,false};cinematic.Invoke(null,arguments);
            if((bool)arguments[1]!=(value>=-8640f && value<=10560f))throw new Exception("Cinematic camera boundary differs.");
        }
        Console.WriteLine("PASS: actual camera/cinematic prefixes execute against the game's Unity vector/math types; asymmetric limits, extended edge and height preservation. No rendering or game process.");
        Console.WriteLine("PASS: "+connections.Count+" distinct real-game installer targets; "+methodBindings+" hook signatures; "+fieldBindings+" actual private-field bindings; hashes and merged connections; reviewed city activation scope. Reflection only, no game running.");
        return 0;
    }
}
