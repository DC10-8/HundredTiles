using System;
using System.Reflection;
using System.Collections.Generic;
using ColossalFramework;
using HundredTiles.Utilities;

static class LiveNetworkCapacityChecks
{
    public static SortedDictionary<string,object> CaptureUtilityState()
    {return (SortedDictionary<string,object>)typeof(UtilitySaveBridge).GetMethod("CaptureState",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);}
    public static void RestoreUtilityState(SortedDictionary<string,object> state)
    {
        var e=Singleton<ElectricityManager>.instance;var w=Singleton<WaterManager>.instance;
        var expanded=typeof(UtilitySaveBridge).GetMethod("ExpandedArray",BindingFlags.Static|BindingFlags.NonPublic);
        foreach(var item in state)
        {
            object owner=item.Key.StartsWith("E.")?(object)e:w;
            var field=owner.GetType().GetField(item.Key.Substring(2),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if(item.Value is Array saved){var target=(Array)expanded.Invoke(null,new object[]{item.Key})??(Array)field.GetValue(owner);Array.Copy(saved,target,saved.Length);}
            else field.SetValue(owner,item.Value);
        }
    }
    public static void Run()
    {
        var e=Singleton<ElectricityManager>.instance;
        var w=Singleton<WaterManager>.instance;
        var sim=Singleton<SimulationManager>.instance;
        var state=CaptureUtilityState();
        foreach(string key in new[]{"E.m_nodeGroups","W.m_nodeData","E.m_pulseUnits","W.m_waterPulseUnits","W.m_sewagePulseUnits","W.m_heatingPulseUnits"})
            if(((Array)state[key]).Length!=65024)throw new Exception("Capacity differs: "+key);
        if(Singleton<NetManager>.instance.m_nodes.m_buffer.Length!=65024)throw new Exception("Network capacity not enabled.");
        uint frame=sim.m_currentFrameIndex;
        var nodes=Singleton<NetManager>.instance.m_nodes.m_buffer;int high=65000;
        while(high>=32768 && nodes[high].m_flags!=0)high--;
        if(high<32768)throw new Exception("No unused extended network node is available for the reversible capacity probe.");
        try
        {
            e.m_nodeGroups[high]=0;
            w.m_nodeData[high].m_curWaterPressure=123;
            sim.m_currentFrameIndex=(frame & ~255u)|(uint)(high*128/nodes.Length);
            Invoke("ElectricityStep",e);Invoke("WaterStep",w);
            if(e.m_nodeGroups[high]!=ushort.MaxValue || w.m_nodeData[high].m_curWaterPressure!=0)throw new Exception("High nodes omitted by utility phase.");
        }
        finally
        {
            sim.m_currentFrameIndex=frame;
            RestoreUtilityState(state);
        }
        Entry.Log("PASS: native 65024 network nodes and six utility arrays; electricity/water phase reaches unused node "+high+"; full utility state restored.");
    }
    static void Invoke(string name,object owner)
    {
        var method=typeof(UtilityHooks).GetMethod(name);var parameters=method.GetParameters();var values=new object[parameters.Length];
        for(int i=0;i<values.Length;i++)values[i]=parameters[i].Name=="__instance"?owner:parameters[i].Name=="subStep"?(object)1:owner.GetType().GetField(parameters[i].Name.Substring(3),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(owner);
        method.Invoke(null,values);
        for(int i=0;i<values.Length;i++)if(parameters[i].ParameterType.IsByRef)owner.GetType().GetField(parameters[i].Name.Substring(3),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(owner,values[i]);
    }
}
