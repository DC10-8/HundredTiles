using System;
using HundredTiles.Core;

static class LandscapeChecks
{
    public static void Run()
    {
        var original=new ushort[1201*1201];for(int i=0;i<original.Length;i++)original[i]=5120;
        var mask=new bool[original.Length];mask[1160*1201+1160]=true;
        var result=Landscape.Generate(original,mask,true);
        int changed=0;
        for(int z=0;z<=1200;z++)for(int x=0;x<=1200;x++)
        {
            int i=z*1201+x;
            if((x<=1080 && z<=1080 || mask[i]) && result[i]!=original[i])throw new Exception("Landscape modified protected terrain.");
            if(result[i]!=original[i])changed++;
        }
        if(changed<200000 || result[1150*1201+(int)Landscape.RiverX(1150)]>=Landscape.RiverLevel(original))throw new Exception("Relief or river bed missing.");
        if(!Landscape.CanRiver(original,mask))throw new Exception("Clear river rejected.");
        mask[1150*1201+(int)Landscape.RiverX(1150)]=true;
        if(Landscape.CanRiver(original,mask))throw new Exception("River accepted occupied site.");
        var again=Landscape.Generate(original,new bool[original.Length],false);
        if(again[400*1201+1190]==5120)throw new Exception("Outer hill missing.");
        Console.WriteLine("PASS: generated relief and river bed; original 81 tiles and protected sites unchanged; occupied river refused.");
        var water=new ushort[original.Length];var ground=(ushort[])original.Clone();
        for(int c=400;c<=420;c++){water[c*1201+1080]=640;ground[c*1201+1080]=original[c*1201+1080]=4480;}
        var continuation=new RiverExtension(original,water,ground,new bool[original.Length],40);
        for(int x=1081;x<=1200;x++)if(continuation.Heights[410*1201+x]>4480 || continuation.Levels[410*1201+x]<=continuation.Heights[410*1201+x])throw new Exception("Disconnected river or raised river dam.");
        if(continuation.Levels[410*1201+1200]!=40*64 || continuation.Heights[410*1201+1200]>=40*64)throw new Exception("River must reach the native sea-level outlet.");
        var shallowOriginal=(ushort[])original.Clone();var shallowGround=(ushort[])ground.Clone();var shallowWater=(ushort[])water.Clone();
        for(int c=400;c<=420;c++){shallowWater[c*1201+1080]=48;shallowGround[c*1201+1080]=shallowOriginal[c*1201+1080]=40*64+16;}
        var shallow=new RiverExtension(shallowOriginal,shallowWater,shallowGround,new bool[original.Length],40);
        if(shallow.Heights[410*1201+1200]>=40*64 || shallow.Levels[410*1201+1200]!=40*64)throw new Exception("Shallow river outlet above sea level.");
        var occupied=new bool[original.Length];occupied[410*1201+1140]=true;
        var blocked=new RiverExtension(original,water,ground,occupied,40);
        if(blocked.Obstructed!=1 || blocked.Connections!=0)throw new Exception("Partially blocked river should not be carved.");
        for(int z=0;z<=1080;z++)for(int x=0;x<=1080;x++)if(continuation.Heights[z*1201+x]!=original[z*1201+x])throw new Exception("River repair changed original map.");
        Console.WriteLine("PASS: river continuation to outer boundary, continuous wet bed, protected crossings refused, original map retained.");
    }
}
