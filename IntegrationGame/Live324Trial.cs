using System;
using System.IO;
using ColossalFramework;
using HundredTiles.Core;
using UnityEngine;
using System.Reflection;

static class Live324Trial
{
    public static bool Far {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_324_FAR")=="1";}}
    public static bool CompareRender {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_324_RENDER_COMPARE")=="1";}}
    public static void CreateRenderComparison()
    {
        LiveTerrainToolChecks.LevelArea(new Vector3(2050,400,2650),900,400);
        LiveConstructionChecks.BuildNetwork("Basic Road",new Vector3(2000,0,2700));
        LiveConstructionChecks.BuildNetwork("Train Track",new Vector3(2000,0,2900));
        LiveTerrainToolChecks.LevelArea(new Vector3(25450,80,23350),900,80);
        LiveConstructionChecks.BuildNetwork("Basic Road",new Vector3(25400,0,23400));
        LiveConstructionChecks.BuildNetwork("Train Track",new Vector3(25400,0,23600));
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Singleton<NetManager>.instance,new object[]{0});
        TerrainModify.RefreshAllModifications();
        foreach(var point in new[]{new Vector3(2200,400,2700),new Vector3(25600,80,23400),new Vector3(25600,400,24700)})
        {
            // Direct CreateNode is not the interactive tool coroutine. Complete its
            // terrain surface update before judging the mesh against the ground.
            TerrainModify.UpdateArea(point.x-300,point.z-80,point.x+100,point.z+280,true,true,true);
            TerrainModify.RefreshAllModifications();
        }
        Entry.Log("Prepared matched road/rail render controls: original area at 400m, far area at 80m, existing far area at 400m. No save requested.");
    }
    public static bool Migration {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_324_MIGRATION")=="1";}}
    public const string MigrationSource="HundredTiles_Startup_Repair_Verification";
    public static void CheckMigratedTerrain()
    {
        var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;
        byte[] bytes;lock(storage)bytes=SaveTransport.Read(storage,HundredTiles.SaveBridge.RuntimeSaveBridge.DataKey);
        byte[] digest;MapLayout layout;var source=SaveCodec.DecodeOriginal(bytes,out digest,out layout);
        if(layout.TileCount!=100)throw new Exception("Migration source is not a 100-tile save.");
        var terrain=Singleton<TerrainManager>.instance;
        for(int z=0;z<1201;z++)for(int x=0;x<1201;x++)if(terrain.RawHeights[z*2161+x]!=source.Heights[z*1201+x])throw new Exception("Migrated native terrain differs at "+x+","+z);
        Entry.Log("PASS: complete 100-tile city loaded into native 324 managers; all 1,442,401 original terrain heights preserved. Supply/consumer operation not asserted.");
    }
    public static void CheckMigratedWaterAfterUnlock()
    {
        var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;byte[] bytes;
        lock(storage)bytes=SaveTransport.Read(storage,HundredTiles.SaveBridge.RuntimeSaveBridge.DataKey);
        byte[] digest;MapLayout layout;var source=SaveCodec.DecodeOriginal(bytes,out digest,out layout);
        if(layout.TileCount!=100)throw new Exception("Expected the 100-tile source water snapshot.");
        var water=Singleton<TerrainManager>.instance.WaterSimulation;
        using(var handoff=(IDisposable)Activator.CreateInstance(typeof(HundredTiles.Runtime.ExtendedSea).Assembly.GetType("HundredTiles.Runtime.LandscapeWater"),new object[]{water}))
        {
            var buffers=(WaterSimulation.Cell[][])typeof(WaterSimulation).GetField("m_waterBuffers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(water);
            foreach(var buffer in buffers)for(int z=0;z<1201;z++)for(int x=0;x<1201;x++)
            {
                var actual=buffer[z*2161+x];var expected=source.Water[z*1201+x];
                if(actual.m_height!=expected.Height || actual.m_pollution!=expected.Pollution || actual.m_velocityX!=expected.VelocityX || actual.m_velocityZ!=expected.VelocityZ)throw new Exception("100-to-324 unlock altered source water at "+x+","+z);
            }
        }
        Entry.Log("PASS: all 1,442,401 source-100 water cells, pollution and both velocity axes preserved in both native buffers after 324 unlock/sea initialization.");
    }
    public static void CheckSavedWater()
    {
        var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;byte[] bytes;
        lock(storage)bytes=SaveTransport.Read(storage,HundredTiles.SaveBridge.RuntimeSaveBridge.DataKey);
        byte[] digest;MapLayout layout;var source=SaveCodec.DecodeOriginal(bytes,out digest,out layout);
        if(layout.TileCount!=HundredTiles.Core.ExpandedMap.TileCount)throw new Exception("Water round-trip profile differs.");
        var terrain=Singleton<TerrainManager>.instance;
        using(var handoff=(IDisposable)Activator.CreateInstance(typeof(HundredTiles.Runtime.ExtendedSea).Assembly.GetType("HundredTiles.Runtime.LandscapeWater"),new object[]{terrain.WaterSimulation}))
        {
            var buffers=(WaterSimulation.Cell[][])typeof(WaterSimulation).GetField("m_waterBuffers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(terrain.WaterSimulation);
            for(int i=0;i<source.Heights.Length;i++)
            {
                if(terrain.RawHeights[i]!=source.Heights[i] || unchecked((ushort)(terrain.BlockHeights[i]-terrain.BlockHeightTargets[i]))!=source.BlockOffsets[i])throw new Exception("Native saved terrain/offset differs at "+i);
                foreach(var buffer in buffers){var actual=buffer[i];var expected=source.Water[i];if(actual.m_height!=expected.Height || actual.m_pollution!=expected.Pollution || actual.m_velocityX!=expected.VelocityX || actual.m_velocityZ!=expected.VelocityZ)throw new Exception("Native saved water differs at "+i);}
            }
        }
        Entry.Log("PASS: full native saved terrain, derived block offsets and both water buffers preserve "+source.Heights.Length+" cells, depth/pollution/X/Z velocity after separate-process reload.");
    }
    static string Objects {get{return Path.Combine(Entry.Root,"trial324-far-objects.txt");}}
    public static void PrepareFar()
    {LiveTerrainToolChecks.Prepare();LiveConstructionChecks.Prepare();if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SURFACE_TRACE")=="1")TerrainSurfaceTrace.Install();}
    public static void PrepareGround(){LiveTerrainToolChecks.LevelArea(new Vector3(25450,400,24650),900,400);}
    public static void CreateFar()
    {
        // Reproduce the reported location without first editing its terrain.
        ushort untouchedRoad=LiveConstructionChecks.BuildNetwork("Basic Road",new Vector3(8958,0,11515),new Vector3(9260,0,11479));
        File.WriteAllText(Path.Combine(Entry.Root,"roadbed-untouched-road.txt"),untouchedRoad.ToString());
        // Native terrain edits keep detailed terrain and tool raycasts consistent.
        LiveTerrainToolChecks.Run();
        ushort road=LiveConstructionChecks.BuildNetwork("Basic Road",new Vector3(25400,0,24700));
        ushort rail=LiveConstructionChecks.BuildNetwork("Train Track",new Vector3(25400,0,24900));
        typeof(NetManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Singleton<NetManager>.instance,new object[]{0});
        typeof(ZoneManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Singleton<ZoneManager>.instance,new object[]{0});
        ushort building=LiveConstructionChecks.PlaceBuilding("Wind Turbine",new Vector3(25700,0,24600));
        File.WriteAllText(Objects,road+"\n"+rail+"\n"+building);
        LiveSupplyChecks.Create();CheckFar();
    }
    public static void CheckNewRoadSurfaces()
    {
        AssertRoadSurface(ushort.Parse(File.ReadAllText(Path.Combine(Entry.Root,"roadbed-untouched-road.txt"))));
        AssertRoadSurface(ushort.Parse(File.ReadAllLines(Objects)[0]));
    }
    static void AssertRoadSurface(ushort segmentId)
    {
        var net=Singleton<NetManager>.instance;var segment=net.m_segments.m_buffer[segmentId];
        var point=segment.m_middlePosition;var terrain=Singleton<TerrainManager>.instance;
        // Called after SimulationManager's native terrain batch has completed.
        // No forced simulation step or manual terrain refresh is performed.
        var cell=terrain.GetSurfaceCell((int)((point.x+8640f)/4f),(int)((point.z+8640f)/4f));
        var patches=(TerrainPatch[])typeof(TerrainManager).GetField("m_patches",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(terrain);
        int patch=terrain.GetPatchIndex(point);
        int cx=(int)((point.x+8640f)/4f),cz=(int)((point.z+8640f)/4f);
        var detailSurface=(TerrainManager.SurfaceCell[])typeof(TerrainManager).GetField("m_detailSurface",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(terrain);
        var rawCell=detailSurface[(patches[patch].m_simDetailIndex-1)*230400+(cz%480)*480+(cx%480)];
        int gx=(int)(point.x/64f+135),gz=(int)(point.z/64f+135);
        ushort cursor=net.m_segmentGrid[gz*540+gx];bool found=false;
        for(int n=0;cursor!=0 && n<65024;n++){if(cursor==segmentId){found=true;break;}cursor=net.m_segments.m_buffer[cursor].m_nextGridSegment;}
        Entry.Log("Road surface after native segment update: position="+point+", patch="+patch+", detail="+patches[patch].m_simDetailIndex+", registered="+found+", nodes="+net.m_nodes.m_buffer[segment.m_startNode].m_flags+"/"+net.m_nodes.m_buffer[segment.m_endNode].m_flags+", lower="+segment.Info.m_netAI.GetSegmentLowerTerrain()+", surface resolution="+terrain.m_surfaceModified.m_resolution+", clip="+cell.m_clipped+", pavement="+cell.m_pavementA+"/"+cell.m_pavementB+", raw clip="+rawCell.m_clipped+", raw pavement="+rawCell.m_pavementA);
        if(cell.m_clipped==0 || (segment.Info.m_createPavement && cell.m_pavementA==0 && cell.m_pavementB==0))
            throw new Exception("A newly-built expanded road has no native pavement after the native segment update.");
        Entry.Log("PASS: new expanded road pavement exists after the native segment update, without manual terrain refresh; clip="+cell.m_clipped+", pavement="+cell.m_pavementA+"/"+cell.m_pavementB+".");
    }
    public static void CheckFar()
    {
        var ids=File.ReadAllLines(Objects);var net=Singleton<NetManager>.instance;
        for(int i=0;i<2;i++){var segment=net.m_segments.m_buffer[ushort.Parse(ids[i])];
            if(segment.m_flags==NetSegment.Flags.None || segment.Info.name!=(i==0?"Basic Road":"Train Track") || net.m_nodes.m_buffer[segment.m_startNode].m_position.x<24000)throw new Exception("Far 324 network identity/position differs.");}
        var building=Singleton<BuildingManager>.instance.m_buildings.m_buffer[ushort.Parse(ids[2])];
        if(building.m_flags==Building.Flags.None || building.Info.name!="Wind Turbine" || Math.Abs(building.m_position.x-25700)>8)throw new Exception("Far 324 building identity/position differs.");
        LiveSupplyChecks.Check();Entry.Log("PASS: far 324 native road/rail/building identities and electricity/water supply at x/z over 24000.");
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_SURFACE_TRACE")=="1"){TerrainModify.RefreshAllModifications();TerrainModify.UpdateArea(25300f,24600f,25800f,25000f,true,true,true);TerrainModify.RefreshAllModifications();Entry.Log("TRACE: immediate CPU clip="+Singleton<TerrainManager>.instance.GetSurfaceCell(8563,8335).m_clipped);}
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_REAL_POWER")=="1")LiveSupplyChecks.CheckRealElectricityProduction();
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_REAL_WATER")=="1")LiveSupplyChecks.CheckRealWaterProduction();
    }
    public static System.Collections.IEnumerator ScreenshotFar()
    {
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();if(camera==null)throw new Exception("324 camera missing.");
        ToolsModifierControl.SetTool<DefaultTool>();var terrain=Singleton<TerrainManager>.instance;
        terrain.RenderTopography=false;Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.None,InfoManager.SubInfoMode.Default);
        Singleton<ImmaterialResourceManager>.instance.ResourceMapVisible=ImmaterialResourceManager.Resource.None;
        camera.ClearTarget();camera.enabled=true;camera.m_targetPosition=camera.m_currentPosition=new Vector3(25600,400,24600);
        camera.m_targetSize=camera.m_currentSize=700;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,70);camera.m_targetHeight=camera.m_currentHeight=400;
        for(int i=0;i<60;i++)yield return null;
        var patches=(TerrainPatch[])typeof(TerrainManager).GetField("m_patches",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(terrain);
        Color32 pixel=patches[323].m_heightMap.GetPixel(416,166);float gpu=(pixel.r*256+pixel.g)/64f;
        float detail=terrain.SampleDetailHeight(new Vector3(25600,0,24600));
        if(Math.Abs(gpu-detail)>.02f)throw new Exception("324 far GPU/native height differs: "+gpu+" / "+detail);
        var cameraInfo=Singleton<RenderManager>.instance.CurrentCameraInfo;var graph=Singleton<NetManager>.instance;var objectIds=File.ReadAllLines(Objects);
        for(int i=0;i<2;i++)
        {
            var segment=graph.m_segments.m_buffer[ushort.Parse(objectIds[i])];
            var position=(graph.m_nodes.m_buffer[segment.m_startNode].m_position+graph.m_nodes.m_buffer[segment.m_endNode].m_position)*.5f;
            var cpuSurface=terrain.GetSurfaceCell((int)((position.x+8640)/4),(int)((position.z+8640)/4));
            if(cpuSurface.m_clipped==0 || (i==0 && cpuSurface.m_pavementA==0))throw new Exception("Far native road/rail surface mask is missing.");
            Entry.Log("324 surface state: sim="+patches[323].m_simDetailIndex+", tmp="+patches[323].m_tmpDetailIndex+", rnd="+patches[323].m_rndDetailIndex+", cpu clip="+cpuSurface.m_clipped+", pavement="+cpuSurface.m_pavementA);
            int gridX=(int)(segment.m_middlePosition.x/64f+135),gridZ=(int)(segment.m_middlePosition.z/64f+135);ushort cursor=graph.m_segmentGrid[gridZ*540+gridX];bool registered=false;int visited=0;
            while(cursor!=0 && visited++<65024){if(cursor==ushort.Parse(objectIds[i])){registered=true;break;}cursor=graph.m_segments.m_buffer[cursor].m_nextGridSegment;}
            Vector3 corner,direction;bool smooth;segment.CalculateCorner(ushort.Parse(objectIds[i]),true,true,true,out corner,out direction,out smooth);
            Entry.Log("324 road surface diagnostic "+segment.Info.name+": flags="+segment.m_flags+", node flags="+graph.m_nodes.m_buffer[segment.m_startNode].m_flags+", middle="+segment.m_middlePosition+", registered="+registered+", grid="+gridX+","+gridZ+", corner="+corner+", lower="+segment.Info.m_netAI.GetSegmentLowerTerrain()+", clip="+segment.Info.m_clipTerrain+", pavement="+segment.Info.m_createPavement);
            Texture height;Vector4 heightMapping,surfaceMapping;
            terrain.GetHeightMapping(position,out height,out heightMapping,out surfaceMapping);
            float u=position.x*surfaceMapping.z+surfaceMapping.x,v=position.z*surfaceMapping.z+surfaceMapping.y;
            var texture=height as Texture2D;if(texture==null)throw new Exception("Network height mapping has no readable texture.");
            Color32 sample=texture.GetPixel((int)(u*texture.width),(int)(v*texture.height));float mapped=(sample.r*256+sample.g)/64f;
            Entry.Log("324 network terrain mapping "+segment.Info.name+": patch323="+ReferenceEquals(height,patches[323].m_heightMap)+", height="+heightMapping+", surface="+surfaceMapping+", uv="+u+","+v+", mappedHeight="+mapped+", nativeHeight="+terrain.SampleDetailHeight(position)+", requiresHeight="+segment.Info.m_requireHeightMap+", requiresSurface="+segment.Info.m_requireSurfaceMaps);
            if(!ReferenceEquals(height,patches[323].m_heightMap) || Math.Abs(mapped-terrain.SampleDetailHeight(position))>.05f)throw new Exception("324 network terrain mapping differs from native height.");
            Texture surfaceA,surfaceB;Vector4 actualSurfaceMapping;
            terrain.GetSurfaceMapping(position,out surfaceA,out surfaceB,out actualSurfaceMapping);
            if(!ReferenceEquals(surfaceA,patches[323].m_surfaceMapA) || !ReferenceEquals(surfaceB,patches[323].m_surfaceMapB) || actualSurfaceMapping!=surfaceMapping)throw new Exception("324 network surface mapping differs from selected patch.");
            Entry.Log("324 network surface shader inputs "+segment.Info.name+": patch323=True, surfaceA="+((Texture2D)surfaceA).GetPixel((int)(u*surfaceA.width),(int)(v*surfaceA.height))+", surfaceB="+((Texture2D)surfaceB).GetPixel((int)(u*surfaceB.width),(int)(v*surfaceB.height)));
        }
        for(int i=0;i<2;i++){var segment=graph.m_segments.m_buffer[ushort.Parse(objectIds[i])];Entry.Log("324 network render "+segment.Info.name+": bounds="+segment.m_bounds+", camera intersects="+cameraInfo.Intersect(segment.m_bounds)+", start="+graph.m_nodes.m_buffer[segment.m_startNode].m_position+", end="+graph.m_nodes.m_buffer[segment.m_endNode].m_position+", calls="+graph.m_drawCallData.m_defaultCalls+"/"+graph.m_drawCallData.m_lodCalls);foreach(var mesh in segment.Info.m_segments)Entry.Log("324 network mesh: layer="+mesh.m_layer+", bounds="+mesh.m_segmentMesh.bounds+", material="+mesh.m_segmentMaterial.name+", lod distance="+mesh.m_lodRenderDistance);}
        var renderedGroups=Singleton<RenderManager>.instance.m_renderedGroups;for(int i=0;i<renderedGroups.m_size;i++){var group=renderedGroups.m_buffer[i];if(group.m_x>=85 && group.m_z>=84)Entry.Log("324 render group "+group.m_x+","+group.m_z+": mask="+group.m_instanceMask);}
        yield return new WaitForEndOfFrame();var captured=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        captured.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);captured.Apply();File.WriteAllBytes(Path.Combine(Entry.Root,"trial324-far-render.png"),captured.EncodeToPNG());UnityEngine.Object.Destroy(captured);
        camera.m_targetPosition=camera.m_currentPosition=new Vector3(25600,400,24700);camera.m_targetSize=camera.m_currentSize=180;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,90);
        for(int i=0;i<60;i++)yield return null;yield return new WaitForEndOfFrame();
        var close=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);close.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);close.Apply();File.WriteAllBytes(Path.Combine(Entry.Root,"trial324-far-render-close.png"),close.EncodeToPNG());UnityEngine.Object.Destroy(close);
        Entry.Log("324 close network render calls="+graph.m_drawCallData.m_defaultCalls+"/"+graph.m_drawCallData.m_lodCalls);
        camera.m_targetPosition=camera.m_currentPosition=new Vector3(25600,400,24900);
        for(int i=0;i<60;i++)yield return null;yield return new WaitForEndOfFrame();
        var railClose=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);railClose.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);railClose.Apply();File.WriteAllBytes(Path.Combine(Entry.Root,"trial324-far-rail-close.png"),railClose.EncodeToPNG());UnityEngine.Object.Destroy(railClose);
        if(File.Exists(Path.Combine(Entry.Root,"roadbed-untouched-road.txt")))
        {
            var position=new Vector3(9100,0,11495);position.y=terrain.SampleDetailHeight(position);
            camera.m_targetPosition=camera.m_currentPosition=position;camera.m_targetHeight=camera.m_currentHeight=position.y;
            camera.m_targetSize=camera.m_currentSize=300;
            for(int i=0;i<60;i++)yield return null;yield return new WaitForEndOfFrame();
            var frame=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);frame.Apply();
            File.WriteAllBytes(Path.Combine(Entry.Root,"roadbed-surfacepainter-reported-area.png"),frame.EncodeToPNG());UnityEngine.Object.Destroy(frame);
        }
        if(CompareRender)
        {
            foreach(var point in new[]{new Vector3(2200,400,2700),new Vector3(25600,80,23400)})
            {
                camera.m_targetPosition=camera.m_currentPosition=point;camera.m_targetHeight=camera.m_currentHeight=point.y;
                for(int i=0;i<60;i++)yield return null;yield return new WaitForEndOfFrame();
                var frame=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);frame.Apply();
                File.WriteAllBytes(Path.Combine(Entry.Root,point.x<9000?"trial324-render-control-original400.png":"trial324-render-control-far80.png"),frame.EncodeToPNG());UnityEngine.Object.Destroy(frame);
            }
        }
        Entry.Log("PASS: 324 far GPU height texture agrees with native detail height; captured rendered corner for visual review.");
        File.WriteAllText(Path.Combine(Entry.Root,"trial324-far-graphics-passed.marker"),"PASS");Application.Quit();
    }
    public static void CheckShapes()
    {
        if(MapLayout.Active.TileCount!=324 || !HundredTiles.Runtime.Installer.IsInstalled)throw new Exception("324 trial runtime did not activate.");
        if(Singleton<GameAreaManager>.instance.m_areaGrid.Length!=324 || Singleton<TerrainManager>.instance.RawHeights.Length!=2161*2161)throw new Exception("Native 324 ownership/terrain shapes differ.");
        if(Singleton<NetManager>.instance.m_nodeGrid.Length!=540*540 || Singleton<BuildingManager>.instance.m_buildingGrid.Length!=540*540)throw new Exception("Native 324 network/building shapes differ.");
        if(Singleton<TreeManager>.instance.m_treeGrid.Length!=1080*1080)throw new Exception("Native 324 tree shape differs.");
        Entry.Log("PASS: isolated native 324 city loaded; 324 ownership, 2161-square terrain, 540-square network/building and 1080-square tree grids. No 324 save/reload or construction asserted.");
        File.WriteAllText(Path.Combine(Entry.Root,"trial324-initialization-passed.marker"),"PASS");
    }
}

