using System;
using System.IO;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.SaveBridge;

static class LiveLayoutExport
{
    public static void Run()
    {
        if(MapLayout.Active.TileCount!=100)throw new Exception("Source export requires the existing 100-tile runtime.");
        var directory=Path.Combine(Entry.Root,"migration-source-100");Directory.CreateDirectory(directory);
        var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;
        lock(storage){File.WriteAllBytes(Path.Combine(directory,"terrain-water.bin"),SaveTransport.Read(storage,RuntimeSaveBridge.DataKey));File.WriteAllBytes(Path.Combine(directory,"ownership.bin"),(byte[])storage[RuntimeSaveBridge.OwnershipKey].Clone());}
        var areas=Singleton<GameAreaManager>.instance;
        var legacy=Ownership.Project(new OwnershipState{Grid=areas.m_areaGrid,Count=areas.m_areaCount,Start=(int)typeof(GameAreaManager).GetField("m_startTile",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(areas)});
        using(var writer=new BinaryWriter(File.Create(Path.Combine(directory,"legacy-ownership.bin")))){writer.Write(legacy.Start);writer.Write(legacy.Count);foreach(int value in legacy.Grid)writer.Write(value);}
        Entry.Log("PASS: exported terrain/water and ownership extension from the actual loaded 100-tile isolated save without saving or changing storage.");
    }
}

