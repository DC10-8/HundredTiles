using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Runtime;
using UnityEngine;
using ColossalFramework.UI;

static class LiveLandscapeChecks
{
    static string FileName {get{return Path.Combine(Entry.Root,"landscape-state.txt");}}
    public static void Create()
    {
        var heights=Singleton<TerrainManager>.instance.RawHeights;
        if(!Singleton<SimulationManager>.instance.m_serializableDataStorage.ContainsKey(LandscapeGeneration.PreviousKey))throw new Exception("Expected real saved v1 city for upgrade verification.");
        // Disposable fixture: an existing river reaches the original east edge.
        // Snapshot after fixture preparation so the production repair must retain
        // the complete original 81-tile heightfield and all existing objects.
        var terrain=Singleton<TerrainManager>.instance;
        ushort surface=(ushort)Math.Round((terrain.WaterSimulation.m_currentSeaLevel+20)*64);
        var type=typeof(LandscapeGeneration).Assembly.GetType("HundredTiles.Runtime.LandscapeWater");
        using(var handoff=(IDisposable)Activator.CreateInstance(type,new object[]{terrain.WaterSimulation}))
        {
            var buffers=(WaterSimulation.Cell[][])type.GetField("Buffers").GetValue(handoff);
            for(int z=690;z<=730;z++)for(int x=1076;x<=1080;x++)
            {
                int i=z*1201+x;heights[i]=(ushort)(surface-640);
                for(int b=0;b<buffers.Length;b++)buffers[b][i].m_height=640;
            }
            TerrainModify.UpdateArea(1076,690,1080,730,true,true,true);
            for(int z=690;z<=730;z++)for(int x=1076;x<=1080;x++){int i=z*1201+x;terrain.BlockHeights[i]=terrain.BlockHeightTargets[i];}
        }
        var before=(ushort[])heights.Clone();
        int oldTrees=Trees();
        string message=LandscapeGeneration.Generate();
        for(int z=0;z<=1080;z++)for(int x=0;x<=1080;x++)
            if(before[z*1201+x]!=heights[z*1201+x])throw new Exception("Landscape changed original terrain.");
        int changed=0;for(int i=0;i<heights.Length;i++)if(heights[i]!=before[i])changed++;
        if(changed<4000 || Trees()-oldTrees<10000 || Sources()<5)throw new Exception("Native landscape repair missing: "+message+", changed="+changed+", trees="+Trees()+", sources="+Sources());
        RiverContinuity();
        string fingerprint=Hash();LandscapeGeneration.Generate();
        if(Hash()!=fingerprint)throw new Exception("Repeated generation changed terrain.");
        OuterObjects.Check();
        Entry.Log("PASS: saved v1 upgrade repaired "+changed+" samples; original 81 tiles unchanged; existing corner road/tree/prop/buildings protected; "+(Trees()-oldTrees)+" additional tall trees; "+Sources()+" added-area sources; repeated generation refused.");
    }
    public static IEnumerator Flow(Action done)
    {
        Singleton<SimulationManager>.instance.ForcedSimulationPaused=false;
        Singleton<SimulationManager>.instance.SimulationPaused=false;
        yield return new WaitForSeconds(30);
        Singleton<SimulationManager>.instance.ForcedSimulationPaused=true;
        var terrain=Singleton<TerrainManager>.instance;
        var position=new Vector3(9252,0,9248);
        float water=terrain.SampleRawHeightSmoothWithWater(position,false,0);
        float ground=terrain.SampleRawHeightSmooth(position);
        if(water<=ground+.1f)throw new Exception("Generated source failed to create actual water: "+water+" / "+ground);
        RiverContinuity();
        Entry.Log("PASS: native water source produced water in generated river: surface="+water+"m, bed="+ground+"m.");
        File.WriteAllText(FileName,Hash()+"\n"+Trees()+"\n"+Sources());
        done();
    }
    public static void Check()
    {
        var expected=File.ReadAllLines(FileName);
        if(Hash()!=expected[0] || Trees()!=int.Parse(expected[1]) || Sources()!=int.Parse(expected[2]))throw new Exception("Generated terrain/forest/river source did not survive reload.");
        if(!Singleton<SimulationManager>.instance.m_serializableDataStorage.ContainsKey(LandscapeGeneration.Key))throw new Exception("Landscape completion marker missing.");
        RiverContinuity();
        Entry.Log("PASS: generated terrain hash, forest count, river source and generation marker survived separate-process native save/reload.");
    }
    public static IEnumerator FreshFlow()
    {
        if(Trees()<10000 || Sources()<1)throw new Exception("Fresh automatic landscape missing forest or river.");
        Singleton<SimulationManager>.instance.ForcedSimulationPaused=false;
        Singleton<SimulationManager>.instance.SimulationPaused=false;
        yield return new WaitForSeconds(30);
        Singleton<SimulationManager>.instance.ForcedSimulationPaused=true;
        var terrain=Singleton<TerrainManager>.instance;var water=terrain.WaterSimulation.BeginRead();
        try{for(int z=1118;z<=1200;z++){int i=z*1201+(int)Math.Round(HundredTiles.Core.Landscape.RiverX(z));if(water[i].m_height<32)throw new Exception("Fresh automatic river dry at "+z);}}
        finally{terrain.WaterSimulation.EndRead();}
        Entry.Log("PASS: fresh automatic landscape has "+Trees()+" added-area trees and "+Sources()+" added-area natural sources; entire corner river remains wet to the sea outlet after 30 seconds of actual simulation.");
        var panel=(UIPanel)UIView.GetAView().AddUIComponent(typeof(UIPanel));new Mod().OnSettingsUI(new UIHelper(panel));
        UIButton repair=null;foreach(var candidate in panel.GetComponentsInChildren<UIButton>())if(candidate.text=="川の接続を修復して森林を増やす")repair=candidate;
        if(repair==null)throw new Exception("New repair options button missing.");
        string before=Hash();int trees=Trees(),sources=Sources();repair.SimulateClick();float deadline=Time.realtimeSinceStartup+30;
        while(repair.text!="この都市の自然は生成済みです" && Time.realtimeSinceStartup<deadline)yield return null;
        if(repair.text!="この都市の自然は生成済みです" || Hash()!=before || Trees()!=trees || Sources()!=sources)throw new Exception("Repair UI callback or idempotency failed.");
        Entry.Log("PASS: actual new repair button click and main-thread callback; no duplicate trees, sources or terrain edits.");UnityEngine.Object.Destroy(panel.gameObject);
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();camera.ClearTarget();camera.enabled=true;ToolsModifierControl.SetTool<DefaultTool>();
        Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.None,InfoManager.SubInfoMode.Default);
        float height=terrain.SampleRawHeightSmooth(new Vector3(9300,0,9600));
        camera.m_targetPosition=camera.m_currentPosition=new Vector3(9300,height,9600);
        camera.m_targetHeight=camera.m_currentHeight=height;camera.m_targetSize=camera.m_currentSize=1200;camera.m_targetAngle=camera.m_currentAngle=new Vector2(10,65);
        for(int i=0;i<90;i++)yield return null;yield return new WaitForEndOfFrame();
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Entry.Root,"landscape-fresh.png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        Application.Quit();
    }
    public static IEnumerator OptionsAndScreenshot()
    {
        var panel=(UIPanel)UIView.GetAView().AddUIComponent(typeof(UIPanel));
        new Mod().OnSettingsUI(new UIHelper(panel));
        UIButton button=null;foreach(var candidate in panel.GetComponentsInChildren<UIButton>())if(candidate.text=="追加19タイルに起伏・川・森を生成")button=candidate;
        if(button==null)throw new Exception("Actual landscape options button missing.");
        button.SimulateClick();float deadline=Time.realtimeSinceStartup+30;
        while(button.text!="この都市の自然は生成済みです" && Time.realtimeSinceStartup<deadline)yield return null;
        if(button.text!="この都市の自然は生成済みです")throw new Exception("Landscape options callback failed: "+button.text);
        Check();UnityEngine.Object.Destroy(panel.gameObject);
        Entry.Log("PASS: actual landscape options button click and completion callback; saved city cannot be regenerated accidentally.");
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();camera.ClearTarget();camera.enabled=true;
        ToolsModifierControl.SetTool<DefaultTool>();
        Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.None,InfoManager.SubInfoMode.Default);
        float cameraHeight=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(new Vector3(9500,0,2720));
        camera.m_targetPosition=camera.m_currentPosition=new Vector3(9500,cameraHeight,2720);
        camera.m_targetHeight=camera.m_currentHeight=cameraHeight;camera.m_targetSize=camera.m_currentSize=1400;
        camera.m_targetAngle=camera.m_currentAngle=new Vector2(10,65);
        for(int i=0;i<90;i++)yield return null;
        yield return new WaitForEndOfFrame();
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Entry.Root,"landscape.png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        Entry.Log("Generated landscape rendered with native camera and captured.");Application.Quit();
    }
    static int Trees(){int count=0;foreach(var tree in Singleton<TreeManager>.instance.m_trees.m_buffer)if((tree.m_flags&(ushort)TreeInstance.Flags.Created)!=0 && (tree.Position.x>8640 || tree.Position.z>8640))count++;return count;}
    static void RiverContinuity()
    {
        var terrain=Singleton<TerrainManager>.instance;var water=terrain.WaterSimulation.BeginRead();
        try{
            for(int x=1081;x<=1200;x++){int i=710*1201+x;if(water[i].m_height<32)throw new Exception("Original edge river dry at "+x);if(terrain.RawHeights[i]>terrain.RawHeights[710*1201+1080])throw new Exception("Dam at "+x);}
            for(int z=1118;z<=1200;z++){int i=z*1201+(int)Math.Round(HundredTiles.Core.Landscape.RiverX(z));if(water[i].m_height<32)throw new Exception("Saved v1 corner river dry at "+z);}
        }finally{terrain.WaterSimulation.EndRead();}
        Entry.Log("PASS: complete existing-river crossing to outer edge and complete v1 river contain water at every centreline sample; no raised crossing dam.");
    }
    static int Sources(){int count=0;var sources=Singleton<TerrainManager>.instance.WaterSimulation.m_waterSources;for(int i=0;i<sources.m_size;i++)if(sources.m_buffer[i].m_type==1 && (sources.m_buffer[i].m_outputPosition.x>8640 || sources.m_buffer[i].m_outputPosition.z>8640))count++;return count;}
    static string Hash(){var heights=Singleton<TerrainManager>.instance.RawHeights;var bytes=new byte[heights.Length*2];Buffer.BlockCopy(heights,0,bytes,0,bytes.Length);using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes));}
}
