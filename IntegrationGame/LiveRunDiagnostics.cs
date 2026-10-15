using System;
using System.IO;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

static class LiveRunDiagnostics
{
    public static void DisableEdgeScroll()
    {
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();if(camera==null)return;
        var setting=typeof(CameraController).GetField("m_edgeScrolling",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(camera);
        setting.GetType().GetProperty("value").SetValue(setting,false,null);
        Entry.Log("FIXTURE: edge scrolling disabled in redirected test settings so an unattended mouse cannot move screenshot targets.");
    }
    public static void Memory()
    {
        var process=System.Diagnostics.Process.GetCurrentProcess();
        Entry.Log("MEMORY: managed="+GC.GetTotalMemory(false)+", private="+process.PrivateMemorySize64+", working="+process.WorkingSet64+", background="+Application.runInBackground);
        foreach(object manager in new object[]{Singleton<TerrainManager>.instance,Singleton<TerrainManager>.instance.WaterSimulation,Singleton<ImmaterialResourceManager>.instance,Singleton<DistrictManager>.instance,Singleton<ElectricityManager>.instance,Singleton<WaterManager>.instance,Singleton<RenderManager>.instance})
            foreach(var field in manager.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {var array=field.GetValue(manager) as Array;if(array!=null && array.LongLength>1000000)Entry.Log("MEMORY ARRAY: "+manager.GetType().Name+"."+field.Name+" = "+array.LongLength+" of "+array.GetType().GetElementType().FullName);}
    }
    public static void Screenshot(string name)
    {
        var frame=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);frame.Apply();File.WriteAllBytes(Path.Combine(Entry.Root,name),frame.EncodeToPNG());UnityEngine.Object.Destroy(frame);
    }
}
