using System;
using System.IO;
using ColossalFramework.IO;
using HundredTiles.Core;
using HundredTiles.SaveBridge;

static class LegacyOwnershipChecks
{
    public static void Run()
    {
        var fixture=new LegacyAreas {Grid=new int[81]};for(int i=0;i<81;i++)fixture.Grid[i]=i+1;
        var baseline=new OwnershipState {Grid=new int[25],Count=81,Start=12};
        for(int z=0;z<5;z++)for(int x=0;x<5;x++)baseline.Grid[z*5+x]=fixture.Grid[(z+2)*9+x+2];
        foreach(uint version in new uint[]{1,2})using(var stream=new MemoryStream())
        {
            DataSerializer.Serialize(stream,DataSerializer.Mode.Memory,version,fixture);
            var bytes=stream.ToArray();var state=LegacyAreas.Read(bytes,baseline);
            if(state.Count!=81 || state.Grid[88]!=81 || state.Grid[99]!=0 || state.Start!=44)throw new Exception("Native legacy ownership migration failed.");
            baseline.Grid[0]++;bool rejected=false;try{LegacyAreas.Read(bytes,baseline);}catch(InvalidDataException){rejected=true;}finally{baseline.Grid[0]--;}
            if(!rejected)throw new Exception("Mismatched legacy ownership baseline accepted.");
        }
        Console.WriteLine("PASS: native DataSerializer and EncodedArray fakeGAM versions 1/2; 81 purchases preserved; mismatched baseline refused.");
    }
}
