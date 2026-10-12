using System;
using ColossalFramework;
using HarmonyLib;
using HundredTiles.SaveBridge;

static class LiveSaveSafetyChecks
{
    public static bool HarmlessPrefix() { return true; }
    public static void Run()
    {
        var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;
        int count=storage.Count;
        var harmony=new Harmony("HundredTiles.UnreviewedConflictFixture");
        var target=typeof(Building).GetMethod("AfterTerrainUpdated");
        try
        {
            harmony.Patch(target,prefix:new HarmonyMethod(typeof(LiveSaveSafetyChecks).GetMethod("HarmlessPrefix")));
            bool rejected=false;
            try {RuntimeSaveBridge.BeginSave(out var scope);throw new Exception("Unknown late patch accepted for saving.");}
            catch(InvalidOperationException error) {if(error.Message.IndexOf("new patch conflict",StringComparison.Ordinal)<0)throw;rejected=true;}
            if(!rejected || storage.Count!=count)throw new Exception("Save conflict refusal changed storage.");
        }
        finally {harmony.UnpatchAll(harmony.Id);}
        RuntimeSaveBridge.ValidateSave();
        Entry.Log("PASS: unknown patch added after startup blocks saving before capture; removing it restores validation; extension storage count unchanged.");
    }
}
