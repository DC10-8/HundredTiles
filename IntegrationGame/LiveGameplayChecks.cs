using System;
using System.Collections;
using System.IO;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

static class LiveGameplayChecks
{
    public static void PurchaseAll()
    {
        var areas=Singleton<GameAreaManager>.instance;
        // Seed the first nine milestone purchases in this disposable city. All
        // remaining purchases execute the real transaction and detailed terrain.
        Array.Clear(areas.m_areaGrid,0,areas.m_areaGrid.Length);
        int[] seed={44,43,42,45,46,47,48,34,54};
        for(int i=0;i<seed.Length;i++)areas.m_areaGrid[seed[i]]=i+1;
        areas.m_areaCount=seed.Length;
        int purchased=0;
        while(areas.m_areaCount<100)
        {
            int before=areas.m_areaCount;
            for(int index=0;index<100;index++)if(areas.m_areaGrid[index]==0 && areas.CanUnlock(index%10,index/10))
            {if(!areas.UnlockArea(index))throw new Exception("Actual 100-tile purchase failed at "+index);purchased++;}
            if(before==areas.m_areaCount)throw new Exception("Expanded purchase adjacency stalled.");
        }
        if(purchased!=91 || areas.m_areaGrid[99]==0 || areas.m_areaGrid[9]==0 || areas.MaxAreaCount!=100)throw new Exception("Actual 100-tile ownership validation failed.");
        Entry.Log("PASS: 91 actual UnlockArea transactions after nine seeded milestones; all 100 areas owned, including added row/column/corner; native detailed terrain initialized.");
    }
    public static void ServiceTexture()
    {
        var manager=Singleton<ImmaterialResourceManager>.instance;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var grid=(ushort[])typeof(ImmaterialResourceManager).GetField("m_localFinalResources",flags).GetValue(manager);
        grid[(499*500+499)*35+7]=32123;
        manager.AreaModified(0,0,499,499);manager.ResourceMapVisible=(ImmaterialResourceManager.Resource)7;
        typeof(ImmaterialResourceManager).GetMethod("UpdateTexture",flags).Invoke(manager,null);
        var texture=(Texture2D)typeof(ImmaterialResourceManager).GetField("m_resourceTexture",flags).GetValue(manager);
        var mapping=Shader.GetGlobalVector("_ImmaterialResourceMapping");
        if(texture.width!=500 || texture.height!=500 || Mathf.Abs(mapping.x-.45f)>1e-6f || Mathf.Abs(mapping.y-.45f)>1e-6f || Mathf.Abs(mapping.z-1f/19200f)>1e-8f)throw new Exception("Actual service texture/shader mapping differs.");
        if(texture.GetPixel(499,499).a<=0)throw new Exception("Service texture failed to update the added corner pixel.");
        var export=new Texture2D(500,500,TextureFormat.RGBA32,false);export.SetPixels(texture.GetPixels());export.Apply();
        File.WriteAllBytes(Path.Combine(Entry.Root,"service-texture.png"),export.EncodeToPNG());UnityEngine.Object.Destroy(export);
        Entry.Log("PASS: actual 500x500 service texture updated through the game renderer; added corner pixel and shader origins verified.");
    }
    public static IEnumerator Screenshot()
    {
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();
        if(camera==null)throw new Exception("Actual game camera was not created.");
        ToolsModifierControl.SetTool<DefaultTool>();
        var terrain=Singleton<TerrainManager>.instance;terrain.RenderTopography=false;
        Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.None,InfoManager.SubInfoMode.Default);
        Singleton<ImmaterialResourceManager>.instance.ResourceMapVisible=ImmaterialResourceManager.Resource.None;
        var point=new Vector3(9900,80,9900);
        float raw=terrain.SampleRawHeightSmooth(point),detail=terrain.SampleDetailHeight(point),water=terrain.WaterLevel(new Vector2(point.x,point.z));
        Entry.Log("Rendering sample: raw="+raw+", detailed="+detail+", water="+water+", raw cell="+terrain.RawHeights[1159*1201+1159]+", render cell="+terrain.RawHeights2[1159*1201+1159]);
        camera.ClearTarget();camera.enabled=true;
        camera.m_targetPosition=camera.m_currentPosition=point;
        camera.m_targetSize=camera.m_currentSize=450;
        camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,70);
        camera.m_targetHeight=camera.m_currentHeight=80;
        var renderCamera=camera.GetComponent<Camera>();
        Entry.Log("Camera diagnostics: enabled="+renderCamera.enabled+", near="+renderCamera.nearClipPlane+", far="+renderCamera.farClipPlane+", mask="+renderCamera.cullingMask+", renderManager enabled="+Singleton<RenderManager>.instance.enabled);
        for(int i=0;i<60;i++)yield return null;
        var cameraInfo=Singleton<RenderManager>.instance.CurrentCameraInfo;
        Entry.Log("Render camera position="+cameraInfo.m_position+", forward="+cameraInfo.m_forward+", bounds="+cameraInfo.m_bounds);
        Entry.Log("Terrain calls="+terrain.m_drawCallData.m_defaultCalls+"/"+terrain.m_drawCallData.m_lodCalls+", terrain layer="+terrain.m_terrainLayer+", layer visible="+((renderCamera.cullingMask&(1<<terrain.m_terrainLayer))!=0));
        foreach(var component in renderCamera.GetComponents<Component>())Entry.Log("Camera component: "+component.GetType().Name+" "+(component is Behaviour?((Behaviour)component).enabled.ToString():""));
        var patch=terrain.GetType().GetField("m_patches",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(terrain) as Array;
        var corner=patch.GetValue(99);
        foreach(var field in corner.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            if(field.Name=="m_bounds" || field.Name=="m_terrainPosition" || field.Name=="m_rndDetailIndex")Entry.Log("Terrain corner "+field.Name+"="+field.GetValue(corner));
        var terrainPatch=(TerrainPatch)corner;
        Color32 gpuHeight=terrainPatch.m_heightMap.GetPixel(331,331);
        float renderedHeight=(gpuHeight.r*256+gpuHeight.g)/64f;
        if(Mathf.Abs(renderedHeight-detail)>.02f)throw new Exception("Actual added-corner GPU height differs from native terrain: "+renderedHeight+" versus "+detail);
        Entry.Log("PASS: actual detailed GPU height texture at added-corner world point matches native height: "+renderedHeight+"m.");
        yield return new WaitForEndOfFrame();
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Entry.Root,"added-area.png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        var net=Singleton<NetManager>.instance;var ids=File.ReadAllLines(Path.Combine(Entry.Root,"outer-objects.txt"));ushort roadId=ushort.Parse(ids[4]);
        var road=net.m_segments.m_buffer[roadId];var roadInfo=road.Info;
        Entry.Log("Road graphics: flags="+road.m_flags+", bounds="+road.m_bounds+", visible="+cameraInfo.Intersect(road.m_bounds)+", draw calls="+net.m_drawCallData.m_defaultCalls+"/"+net.m_drawCallData.m_lodCalls+", layers="+roadInfo.m_netLayers);
        var groups=Singleton<RenderManager>.instance.m_renderedGroups;
        for(int i=0;i<groups.m_size;i++)if(groups.m_buffer[i].m_x>=47 && groups.m_buffer[i].m_z>=47)Entry.Log("Outer render group "+groups.m_buffer[i].m_x+","+groups.m_buffer[i].m_z+" mask="+groups.m_buffer[i].m_instanceMask);
        foreach(var segment in roadInfo.m_segments)Entry.Log("Road mesh: layer="+segment.m_layer+", lod="+segment.m_lodRenderDistance+", material="+segment.m_segmentMaterial.name+", mesh="+segment.m_segmentMesh.name+", bounds="+segment.m_segmentMesh.bounds);
        camera.m_targetSize=camera.m_currentSize=180;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,90);
        for(int i=0;i<60;i++)yield return null;
        yield return new WaitForEndOfFrame();
        var closeImage=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);closeImage.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);closeImage.Apply();
        File.WriteAllBytes(Path.Combine(Entry.Root,"added-area-close.png"),closeImage.EncodeToPNG());UnityEngine.Object.Destroy(closeImage);
        if(Shader.GetGlobalVector("_FogParams0").x!=0)throw new Exception("Original edge fog still covers the added map boundary.");
        Entry.Log("PASS: original boundary fog suppressed with weather fog enabled; native camera/controller captured added region.");
        Entry.Log("Actual game frame captured for rendering review.");
        File.WriteAllText(Path.Combine(Entry.Root,"graphics-passed.marker"),"PASS");Application.Quit();
    }
}
