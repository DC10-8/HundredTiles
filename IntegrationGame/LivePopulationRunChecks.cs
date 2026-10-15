using System;
using System.Collections;
using System.IO;
using ColossalFramework;
using UnityEngine;

static class LivePopulationRunChecks
{
    public static bool Enabled {get{return (Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_MODE")??"").IndexOf("Stress324LongRun",StringComparison.Ordinal)>=0;}}
    public static IEnumerator Run(Action<Exception> completed)
    {
        if(HundredTiles.Core.MapLayout.Active.TileCount!=324 || !File.Exists(Path.Combine(Entry.Root,"isolated-test.marker")))throw new Exception("Population operation requires the isolated 324 fixture.");
        float seconds=600;string requested=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_STRESS_SECONDS");if(!string.IsNullOrEmpty(requested))seconds=float.Parse(requested,System.Globalization.CultureInfo.InvariantCulture);
        if(seconds<120 || seconds>1200)throw new Exception("Population operation duration must be 120..1200 real seconds.");
        var simulation=Singleton<SimulationManager>.instance;uint frame=simulation.m_currentFrameIndex;float start=Time.realtimeSinceStartup,next=start+30;
        LiveConsumerChecks.PrepareUnattendedProgression();ToolsModifierControl.SetTool<DefaultTool>();LiveRunDiagnostics.DisableEdgeScroll();
        var camera=UnityEngine.Object.FindObjectOfType<CameraController>();if(camera!=null){camera.ClearTarget();camera.m_targetPosition=camera.m_currentPosition=new Vector3(9600,80,4000);camera.m_targetSize=camera.m_currentSize=1200;camera.m_targetAngle=camera.m_currentAngle=new Vector2(0,70);camera.m_targetHeight=camera.m_currentHeight=80;}
        simulation.SelectedSimulationSpeed=1;simulation.SimulationPaused=false;simulation.ForcedSimulationPaused=false;
        while(Time.realtimeSinceStartup-start<seconds)
        {
            if(Time.realtimeSinceStartup>=next)
            {
                int residents=LiveLargeCityChecks.Residents();
                Entry.Log("Population normal-time sample: elapsed="+(Time.realtimeSinceStartup-start).ToString("F1")+", advanced frames="+(simulation.m_currentFrameIndex-frame)+", native residents="+residents+", vehicles="+Singleton<VehicleManager>.instance.m_vehicleCount+", citizen instances="+Singleton<CitizenManager>.instance.m_instanceCount+", managed bytes="+GC.GetTotalMemory(false)+", paused="+simulation.SimulationPaused+", forced="+simulation.ForcedSimulationPaused);
                next+=30;
                Entry.Log("Population housing diagnostics: "+LiveLargeCityChecks.HousingDiagnostics());
            }
            yield return null;
        }
        simulation.ForcedSimulationPaused=true;Exception error=null;bool done=false;
        simulation.AddAction(delegate
        {
            try
            {
                if(simulation.m_currentFrameIndex-frame<256)throw new Exception("Population normal-time simulation did not advance a complete cycle.");
                LiveLargeCityChecks.CaptureLongState();LiveLargeCityChecks.Check();
                Entry.Log("PASS: generated population city operated for "+seconds+" real seconds / "+(simulation.m_currentFrameIndex-frame)+" native frames without manual building or utility simulation calls. Performance/load fixture; not a mature real-world city or proof of every service and route.");
            }
            catch(Exception failure){error=failure;}finally{done=true;}
        });
        while(!done)yield return null;
        yield return new WaitForEndOfFrame();LiveRunDiagnostics.Screenshot("population-"+LiveLargeCityChecks.Target+"-longrun.png");completed(error);
    }
}
