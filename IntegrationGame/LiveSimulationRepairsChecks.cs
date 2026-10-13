using System;
using System.Reflection;
using ColossalFramework;
using System.Collections;
using UnityEngine;

static class LiveSimulationRepairsChecks
{
    static Assembly Find(string name){foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name==name)return assembly;throw new Exception("Missing simulation fixture: "+name);}
    public static void Run()
    {
        var realism=Find("RealismLite");var state=realism.GetType("RealismLite.RealismState",true);var settings=realism.GetType("RealismLite.RLSettings",true);
        var inGame=state.GetField("InGame");var construction=settings.GetField("ConstructionEnabled");
        object oldGame=inGame.GetValue(null),oldSetting=construction.GetValue(null);
        var bytes=(byte[])state.GetField("LastConstructState").GetValue(null);
        if(bytes.Length!=65536)throw new Exception("Building state does not cover ushort IDs.");
        var step=realism.GetType("RealismLite.Patches.PrivateBuildingAISimulationStepPatch",true).GetMethod("Postfix");
        try
        {
            inGame.SetValue(null,true);construction.SetValue(null,true);
            foreach(ushort id in new ushort[]{49151,49152,65000,65535})
            {
                byte old=bytes[id];try{bytes[id]=0;var building=new Building{m_flags=Building.Flags.Created};var frame=new Building.Frame();step.Invoke(null,new object[]{id,building,frame});}finally{bytes[id]=old;}
            }
        }
        finally{inGame.SetValue(null,oldGame);construction.SetValue(null,oldSetting);}
        var road=Find("RoadUtilityPoles").GetType("RoadUtilityPoles.Harmony.Patches.ZoneBlockPoleFootprintPatch",true);
        road.GetMethod("TargetMethod",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        var args=new object[]{new ZoneBlock(),(ushort)0,null,new Building(),0ul,0ul,0ul,0ul,0f,0f,0f,0f};
        if(!(bool)road.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args))throw new Exception("Pole footprint null-info delegation differs.");
        var tmpe=Find("TrafficManager").GetType("TrafficManager.Manager.Impl.TrafficLightSimulationManager",true);
        object manager=tmpe.GetField("Instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
        var renew=Find("MoreNetworkCapacityRenew").GetType("MoreNetworkCapacityRenew.TMPEPatches",true);
        renew.GetMethod("TrafficLightSimulationManager_SimulationStep_Postfix").Invoke(null,new object[]{manager});
        var removal=(MethodInfo)renew.GetField("removeNodeMethod",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(removal==null || removal.GetParameters().Length!=1 || removal.GetParameters()[0].ParameterType!=typeof(ushort))throw new Exception("Ambiguous signal removal lookup remains.");
        Entry.Log("PASS: native RealismLite building IDs 49151/49152/65000/65535; RoadUtilityPoles zone prefix JIT and delegation; Renew resolves TMPE ushort signal-removal overload.");
    }
    public static IEnumerator Measure()
    {
        var sim=Singleton<SimulationManager>.instance;
        var pause=typeof(SimulationManager).GetProperty("SimulationPaused");
        object old=pause.GetValue(sim,null);
        pause.SetValue(sim,false,null);sim.ForcedSimulationPaused=false;
        float start=Time.realtimeSinceStartup;uint frame=sim.m_currentFrameIndex;
        try
        {
            while(Time.realtimeSinceStartup-start<20f)yield return null;
            uint advanced=sim.m_currentFrameIndex-frame;float elapsed=Time.realtimeSinceStartup-start;
            if(advanced==0)throw new Exception("Simulation did not advance during sample.");
            Entry.Log("PASS: simulation running sample: "+advanced+" frames in "+elapsed.ToString("F2")+" real seconds; "+(advanced/elapsed).ToString("F2")+" simulation frames/sec. Isolated small city only; no user-city speed claim.");
        }
        finally{sim.ForcedSimulationPaused=true;pause.SetValue(sim,old,null);}
        Application.Quit();
    }
}
