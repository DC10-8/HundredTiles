using System;
using System.Collections;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;

static class LiveLodVisualChecks
{
    public static bool Enabled {get{return Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_LOD_VISUAL")=="1";}}
    public static void Prepare()
    {
        var random=new ColossalFramework.Math.Randomizer(324301);var tree=PrefabCollection<TreeInfo>.GetLoaded(0);var prop=PrefabCollection<PropInfo>.GetLoaded(0);
        if(tree==null || prop==null)throw new Exception("Native tree/prop visual fixture prefabs missing.");
        for(int index=0;index<12;index++)
        {
            uint id;var point=new Vector3(23240+index*12,80,24580);point.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(point);
            if(!Singleton<TreeManager>.instance.CreateTree(out id,ref random,tree,point,true))throw new Exception("Native far LOD tree creation failed.");
        }
        for(int index=0;index<6;index++)
        {
            ushort id;var point=new Vector3(23260+index*12,80,24548);point.y=Singleton<TerrainManager>.instance.SampleRawHeightSmooth(point);
            if(!Singleton<PropManager>.instance.CreateProp(out id,ref random,prop,point,0,true))throw new Exception("Native far LOD prop creation failed.");
        }
        Entry.Log("Native far visual fixture: 12 trees ("+tree.name+"), 6 props ("+prop.name+") near real bus depot/road. No mesh or LOD threshold is changed.");
    }
    public static IEnumerator Run()
    {
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();if(camera==null)throw new Exception("LOD camera missing.");
        var view=UIView.GetAView();bool visible=view.enabled;var day=DayNightProperties.instance;float hour=day.m_TimeOfDay;
        var simulation=Singleton<SimulationManager>.instance;bool cycle=simulation.m_enableDayNight,night=simulation.m_isNightTime;float nativeHour=simulation.m_currentDayTimeHour;
        uint offset=simulation.m_dayTimeOffsetFrames,dayFrame=simulation.m_dayTimeFrame;
        var weather=Singleton<WeatherManager>.instance;float[] originalWeather={weather.m_currentRain,weather.m_targetRain,weather.m_currentFog,weather.m_targetFog,weather.m_currentCloud,weather.m_targetCloud};
        Singleton<SimulationManager>.instance.ForcedSimulationPaused=true;
        ToolsModifierControl.SetTool<DefaultTool>();camera.ClearTarget();
        var terrain=Singleton<TerrainManager>.instance;terrain.RenderTopography=false;
        Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.None,InfoManager.SubInfoMode.Default);
        Singleton<ImmaterialResourceManager>.instance.ResourceMapVisible=ImmaterialResourceManager.Resource.None;
        try
        {
            UIView.Show(false);simulation.m_enableDayNight=false;simulation.m_isNightTime=false;simulation.m_currentDayTimeHour=12;
            foreach(var point in new[]{new Vector3(10000,80,4000),new Vector3(23300,80,24520),new Vector3(25600,80,20700)})
            foreach(float size in new[]{125f,350f,1000f,3000f,8000f})
            {
                var focus=point;
                if(point.x>25000){ushort rail;focus=ClosestRailPoint(point,out rail);Entry.Log("LOD rail camera follows actual constructed geometry: segment="+rail+", requested="+point+", actual="+focus);}
                focus.y=terrain.SampleRawHeightSmoothWithWater(focus,true,0)+10;
                camera.m_targetPosition=camera.m_currentPosition=focus;camera.m_targetHeight=camera.m_currentHeight=focus.y;
                camera.m_targetSize=camera.m_currentSize=size;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,70);
                for(int frame=0;frame<60;frame++){UIView.Show(false);simulation.m_dayTimeOffsetFrames=((SimulationManager.DAYTIME_FRAMES>>1)-simulation.m_referenceFrameIndex)&(SimulationManager.DAYTIME_FRAMES-1);simulation.m_dayTimeFrame=SimulationManager.DAYTIME_FRAMES>>1;simulation.m_isNightTime=false;weather.m_currentRain=weather.m_targetRain=weather.m_currentFog=weather.m_targetFog=weather.m_currentCloud=weather.m_targetCloud=0;day.m_TimeOfDay=12;day.Refresh();yield return null;}
                yield return new WaitForEndOfFrame();
                if(point.x>25000)DescribeTrack(camera,focus);
                string name="trial324-lod-"+(int)point.x+"-"+(int)size+".png";LiveRunDiagnostics.Screenshot(name);
                Entry.Log("LOD capture "+name+": native camera position="+camera.transform.position+", size="+camera.m_currentSize+", sun="+day.DayTime);
                foreach(object manager in new object[]{Singleton<NetManager>.instance,Singleton<BuildingManager>.instance,Singleton<TreeManager>.instance,Singleton<PropManager>.instance})
                {
                    var field=manager.GetType().GetField("m_drawCallData",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(field==null)continue;
                    object calls=field.GetValue(manager);string values="";foreach(var member in calls.GetType().GetFields())values+=" "+member.Name+"="+member.GetValue(calls);
                    Entry.Log("LOD native draw counters "+manager.GetType().Name+":"+values);
                }
            }
            Entry.Log("LOD capture matrix completed: three regions, five camera distances. Images require visual review; capture completion alone is not a visual PASS.");
        }
        finally{UIView.Show(visible);simulation.m_dayTimeOffsetFrames=offset;simulation.m_dayTimeFrame=dayFrame;simulation.m_enableDayNight=cycle;simulation.m_isNightTime=night;simulation.m_currentDayTimeHour=nativeHour;weather.m_currentRain=originalWeather[0];weather.m_targetRain=originalWeather[1];weather.m_currentFog=originalWeather[2];weather.m_targetFog=originalWeather[3];weather.m_currentCloud=originalWeather[4];weather.m_targetCloud=originalWeather[5];day.m_TimeOfDay=hour;day.Refresh();}
    }
    static void DescribeTrack(CameraController camera,Vector3 focus)
    {
        var graph=Singleton<NetManager>.instance;ushort closest;var selected=ClosestRailPoint(focus,out closest);
        var segment=graph.m_segments.m_buffer[closest];var surface=Singleton<TerrainManager>.instance.GetSurfaceCell((int)((selected.x+8640)/4),(int)((selected.z+8640)/4));
        var worldCamera=camera.GetComponent<Camera>();
        Entry.Log("LOD nearest rail: segment="+closest+", info="+segment.Info.name+", point="+selected+", flags="+segment.m_flags+", bounds="+segment.m_bounds+", camera intersects="+Singleton<RenderManager>.instance.CurrentCameraInfo.Intersect(segment.m_bounds)+", screen="+(worldCamera==null?Vector3.zero:worldCamera.WorldToScreenPoint(selected))+", surface clip="+surface.m_clipped+", underground="+segment.Info.m_netAI.IsUnderground());
        foreach(var mesh in segment.Info.m_segments)Entry.Log("LOD rail mesh: layer="+mesh.m_layer+", distance="+mesh.m_lodRenderDistance+", material="+mesh.m_segmentMaterial.name);
    }
    static Vector3 ClosestRailPoint(Vector3 focus,out ushort closest)
    {
        var graph=Singleton<NetManager>.instance;float distance=float.MaxValue;closest=0;Vector3 selected=Vector3.zero;
        for(int id=1;id<graph.m_segments.m_buffer.Length;id++)
        {
            var data=graph.m_segments.m_buffer[id];if((data.m_flags&NetSegment.Flags.Created)==0 || data.Info==null || !(data.Info.m_netAI is TrainTrackBaseAI))continue;
            var a=graph.m_nodes.m_buffer[data.m_startNode].m_position;var b=graph.m_nodes.m_buffer[data.m_endNode].m_position;var direction=b-a;var point=a+direction*Mathf.Clamp01(Vector3.Dot(focus-a,direction)/Math.Max(.01f,direction.sqrMagnitude));var delta=point-focus;delta.y=0;
            if(delta.sqrMagnitude<distance){distance=delta.sqrMagnitude;closest=(ushort)id;selected=point;}
        }
        if(closest==0)throw new Exception("No native rail segment near LOD target.");
        return selected;
    }
}
