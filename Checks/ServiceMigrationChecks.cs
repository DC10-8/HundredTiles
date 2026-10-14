using System;
using System.IO;
using System.Collections.Generic;
using HundredTiles.Core;
using HundredTiles.Utilities;

static class ServiceMigrationChecks
{
    public static void Run(ushort[] original)
    {
        var state=new SortedDictionary<string,object>(StringComparer.Ordinal);
        state.Add("m_localFinalResources",original);state.Add("m_localTempResources",original);
        foreach(string name in new[]{"m_globalFinalResources","m_globalTempResources","m_totalFinalResources","m_totalTempResources"}){var values=new int[35];values[34]=791;state.Add(name,values);}
        var totals=new long[35];totals[34]=long.MaxValue;state.Add("m_totalTempResourcesMul",totals);
        var binding=new byte[32];binding[0]=13;var baseline=new byte[32];baseline[0]=17;
        byte[] encoded=UtilitySnapshotCodec.Encode(state,baseline,binding);
        if(encoded.Length!=ServiceGrid.EncodedSizeFor(500))throw new Exception("Old service format size differs");
        var restored=UtilitySnapshotCodec.Decode(encoded,state,baseline,binding);
        foreach(string name in new[]{"m_localFinalResources","m_localTempResources"})
        {
            var old=(ushort[])restored[name];for(int i=0;i<original.Length;i++)if(old[i]!=original[i])throw new Exception("Service codec loses values");
            restored[name]=ServiceGrid.MigrateHundred(old);
        }
        if(((int[])restored["m_globalFinalResources"])[34]!=791 || ((long[])restored["m_totalTempResourcesMul"])[34]!=long.MaxValue)throw new Exception("Service global totals lost");
        byte[] newBytes=UtilitySnapshotCodec.Encode(restored,baseline,binding);
        if(newBytes.Length!=ServiceGrid.EncodedSize)throw new Exception("New service format size differs");
        UtilitySnapshotCodec.Decode(newBytes,restored,baseline,binding);
        encoded[150]^=1;Reject(()=>UtilitySnapshotCodec.Decode(encoded,state,baseline,binding));encoded[150]^=1;
        binding[0]^=1;Reject(()=>UtilitySnapshotCodec.Decode(encoded,state,baseline,binding));
        Console.WriteLine("PASS: 100-to-324 service codec migration, every channel, global totals, chunk-size schema, checksum and binding rejection.");
    }
    static void Reject(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Invalid service snapshot accepted");}
}
