using System;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;

static class NetworkLoadTrace
{
    public static void Install()
    {
        var harmony=new Harmony("HundredTiles.Isolated.NetworkLoadTrace");
        var data=typeof(NetManager).GetNestedType("Data",BindingFlags.Public|BindingFlags.NonPublic);
        foreach(string name in new[]{"Deserialize","AfterDeserialize"})harmony.Patch(data.GetMethod(name),postfix:new HarmonyMethod(typeof(NetworkLoadTrace).GetMethod("Stage")));
        harmony.Patch(typeof(NetManager).GetMethod("UpdateData"),postfix:new HarmonyMethod(typeof(NetworkLoadTrace).GetMethod("Stage")));
        harmony.Patch(typeof(NetManager).GetMethod("MoveNode",new[]{typeof(ushort),typeof(UnityEngine.Vector3)}),prefix:new HarmonyMethod(typeof(NetworkLoadTrace).GetMethod("Moving")));
        harmony.Patch(typeof(TerrainManager).GetMethod("UpdateData"),postfix:new HarmonyMethod(typeof(NetworkLoadTrace).GetMethod("Stage")));
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="TerrainLink")
            harmony.Patch(assembly.GetType("TerrainLink.Sync.NetworkSync",true).GetMethod("ProcessNode",BindingFlags.Static|BindingFlags.NonPublic),prefix:new HarmonyMethod(typeof(NetworkLoadTrace).GetMethod("BeforeTerrainLink")),postfix:new HarmonyMethod(typeof(NetworkLoadTrace).GetMethod("AfterTerrainLink")));
    }
    public static void Stage(MethodBase __originalMethod)
    {
        foreach(ushort id in new ushort[]{124,2957})
        {
            var point=Singleton<NetManager>.instance.m_nodes.m_buffer[id].m_position;var terrain=Singleton<TerrainManager>.instance;
            int index=(int)(point.z/16+540)*HundredTiles.Core.ExpandedMap.HeightStride+(int)(point.x/16+540);
            var cells=terrain.WaterSimulation.BeginRead();
            try{Entry.Log("TRACE network load "+__originalMethod.DeclaringType.FullName+"."+__originalMethod.Name+" node="+id+", position="+point+", raw="+terrain.RawHeights[index]+", block="+terrain.BlockHeights[index]+", target="+terrain.BlockHeightTargets[index]+", water="+cells[index].m_height);}
            finally{terrain.WaterSimulation.EndRead();}
        }
    }
    public static void Moving(ushort node,UnityEngine.Vector3 position)
    {if(node==124 || node==2957)Entry.Log("TRACE MoveNode "+node+" -> "+position+"; "+Environment.StackTrace);}
    public static void BeforeTerrainLink(ref NetNode node,out float __state){__state=node.m_position.y;}
    public static void AfterTerrainLink(ushort nodeId,ref NetNode node,float __state)
    {if((nodeId==124 || nodeId==2957) && Math.Abs(node.m_position.y-__state)>.01f)Entry.Log("TRACE TerrainLink changed node="+nodeId+", y="+__state+" -> "+node.m_position.y+"; "+Environment.StackTrace);}
}
