using System;
using System.IO;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;

static class BuildingLoadTrace
{
    public static void Install()
    {
        var harmony=new Harmony("HundredTiles.Isolated.BuildingLoadTrace");
        var type=typeof(BuildingManager).GetNestedType("Data",BindingFlags.Public|BindingFlags.NonPublic);
        foreach(string name in new[]{"Deserialize","AfterDeserialize"})harmony.Patch(type.GetMethod(name),postfix:new HarmonyMethod(typeof(BuildingLoadTrace).GetMethod("Stage")));
        harmony.Patch(typeof(BuildingManager).GetMethod("UpdateData"),postfix:new HarmonyMethod(typeof(BuildingLoadTrace).GetMethod("Stage")));
    }
    public static void Stage(MethodBase __originalMethod)
    {
        var ids=File.ReadAllLines(Path.Combine(Entry.Root,"trial324-consumers.txt"));
        for(int i=0;i<4;i++)
        {
            ushort id=ushort.Parse(ids[i]);var data=Singleton<BuildingManager>.instance.m_buildings.m_buffer[id];
            Entry.Log("TRACE building load "+__originalMethod.DeclaringType.FullName+"."+__originalMethod.Name+" id="+id+", flags="+data.m_flags+" ("+(uint)data.m_flags+"), position="+data.m_position+", units="+data.m_citizenUnits);
        }
    }
}
