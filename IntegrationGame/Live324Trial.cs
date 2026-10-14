using System;
using System.IO;
using ColossalFramework;
using HundredTiles.Core;
using UnityEngine;
using System.Reflection;

static class Live324Trial
{
    public static bool Far {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_324_FAR")=="1";}}
    static string Objects {get{return Path.Combine(Entry.Root,"trial324-far-objects.txt");}}
    public static void PrepareFar()
    {LiveTerrainToolChecks.Prepare();LiveConstructionChecks.Prepare();}
    public static void PrepareGround(){LiveTerrainToolChecks.LevelArea(new Vector3(25450,400,24650),900,400);}
    public static void CreateFar()
    {
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
    public static void CheckFar()
    {
        var ids=File.ReadAllLines(Objects);var net=Singleton<NetManager>.instance;
        for(int i=0;i<2;i++){var segment=net.m_segments.m_buffer[ushort.Parse(ids[i])];
            if(segment.m_flags==NetSegment.Flags.None || segment.Info.name!=(i==0?"Basic Road":"Train Track") || net.m_nodes.m_buffer[segment.m_startNode].m_position.x<24000)throw new Exception("Far 324 network identity/position differs.");}
        var building=Singleton<BuildingManager>.instance.m_buildings.m_buffer[ushort.Parse(ids[2])];
        if(building.m_flags==Building.Flags.None || building.Info.name!="Wind Turbine" || Math.Abs(building.m_position.x-25700)>8)throw new Exception("Far 324 building identity/position differs.");
        LiveSupplyChecks.Check();Entry.Log("PASS: far 324 native road/rail/building identities and electricity/water supply at x/z over 24000.");
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
        for(int i=0;i<2;i++){var segment=graph.m_segments.m_buffer[ushort.Parse(objectIds[i])];Entry.Log("324 network render "+segment.Info.name+": bounds="+segment.m_bounds+", camera intersects="+cameraInfo.Intersect(segment.m_bounds)+", start="+graph.m_nodes.m_buffer[segment.m_startNode].m_position+", end="+graph.m_nodes.m_buffer[segment.m_endNode].m_position+", calls="+graph.m_drawCallData.m_defaultCalls+"/"+graph.m_drawCallData.m_lodCalls);foreach(var mesh in segment.Info.m_segments)Entry.Log("324 network mesh: layer="+mesh.m_layer+", bounds="+mesh.m_segmentMesh.bounds+", material="+mesh.m_segmentMaterial.name+", lod distance="+mesh.m_lodRenderDistance);}
        var renderedGroups=Singleton<RenderManager>.instance.m_renderedGroups;for(int i=0;i<renderedGroups.m_size;i++){var group=renderedGroups.m_buffer[i];if(group.m_x>=85 && group.m_z>=84)Entry.Log("324 render group "+group.m_x+","+group.m_z+": mask="+group.m_instanceMask);}
        yield return new WaitForEndOfFrame();var captured=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        captured.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);captured.Apply();File.WriteAllBytes(Path.Combine(Entry.Root,"trial324-far-render.png"),captured.EncodeToPNG());UnityEngine.Object.Destroy(captured);
        camera.m_targetPosition=camera.m_currentPosition=new Vector3(25600,400,24700);camera.m_targetSize=camera.m_currentSize=180;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,90);
        for(int i=0;i<60;i++)yield return null;yield return new WaitForEndOfFrame();
        var close=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);close.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);close.Apply();File.WriteAllBytes(Path.Combine(Entry.Root,"trial324-far-render-close.png"),close.EncodeToPNG());UnityEngine.Object.Destroy(close);
        Entry.Log("324 close network render calls="+graph.m_drawCallData.m_defaultCalls+"/"+graph.m_drawCallData.m_lodCalls);
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

