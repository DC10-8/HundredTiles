using System;
using HundredTiles.Core;

static class MapLayoutChecks
{
    public static void Run()
    {
        var layout=MapLayout.ThreeTwentyFour;
        Require(layout.TileCount==324 && layout.WorldWidth==34560 && layout.WorldMax==25920 && layout.HeightStride==2161,"324 geometry");
        Require(layout.ServiceSide==900 && layout.DistrictSide==1800 && layout.UtilitySide==909 && layout.ResourceSide==1024,"324 grid coverage");
        for(int z=0;z<18;z++)for(int x=0;x<18;x++){int index;Require(layout.TryGetTile(-8640+x*1920+960,-8640+z*1920+960,out index) && index==z*18+x,"324 tile center");}
        int ignored;Require(!layout.TryGetTile(25920,0,out ignored) && !layout.TryGetTile(double.NaN,0,out ignored),"324 boundary");
        var old=new OwnershipState{Grid=new int[100],Start=44,Count=100};for(int i=0;i<100;i++)old.Grid[i]=i+1;
        var expanded=layout.ImportOwnership(old);Require(expanded.Start==76 && expanded.Count==100,"100 ownership metadata migration");
        for(int z=0;z<18;z++)for(int x=0;x<18;x++)Require(expanded.Grid[z*18+x]==(z<10 && x<10?z*10+x+1:0),"100 ownership placement migration");
        for(int i=0;i<100;i++)Require(old.Grid[i]==i+1,"source ownership mutation");
        var terrain=new ushort[1201*1201];for(int i=0;i<terrain.Length;i++)terrain[i]=(ushort)(i%65536);
        var migrated=layout.ExtendSquare(terrain,1201,true);
        for(int z=0;z<2161;z++)for(int x=0;x<2161;x++)Require(migrated[z*2161+x]==terrain[Math.Min(z,1200)*1201+Math.Min(x,1200)],"terrain migration including all new edges");
        foreach(float world in new[]{-8640f,0f,10559f,17280f,20000f,25919.9f}){var packed=PackedObjectPosition.Encode(world,layout);Require(Math.Abs(PackedObjectPosition.Decode(packed,layout)-world)<.3f,"324 coordinate round trip");}
        bool rejected=false;try{PackedObjectPosition.Decode(PackedObjectPosition.Encode(20000,layout));}catch(ArgumentException){rejected=true;}Require(rejected,"100 decoder rejects 324-only coordinates");
        var channels=new ushort[500*500*35];channels[((499*500)+499)*35+34]=1234;
        var services=MapLayout.ExtendChannels(channels,500,900,35);Require(services[((499*900)+499)*35+34]==1234 && services[((899*900)+899)*35+34]==0,"service migration preserves channel and does not create supplies");
        var utility=new byte[508*508];utility[507*508+507]=17;
        var utilities=MapLayout.ExtendChannels(utility,508,909,1);Require(utilities[507*909+507]==17 && utilities[908*909+908]==0,"utility migration preserves position");
        var water=new WaterCellState[1201*1201];water[1200*1201+1200]=new WaterCellState{Height=42,Pollution=13,VelocityX=-7,VelocityZ=11};
        var offsets=new ushort[water.Length];offsets[offsets.Length-1]=19;
        var state=layout.ImportTerrainWater(new MapSaveState{Heights=terrain,BlockOffsets=offsets,Water=water,WaterFrame=78},1201);
        state.Validate(2161);var cell=state.Water[1200*2161+1200];Require(cell.Height==42 && cell.Pollution==13 && cell.VelocityX==-7 && cell.VelocityZ==11 && state.BlockOffsets[1200*2161+1200]==19 && state.WaterFrame==78,"terrain/water snapshot migration");
        Require(state.Water[2160*2161+2160].Height==0 && state.BlockOffsets[2160*2161+2160]==0,"new cells remain uninitialized for native sea setup");
        Console.WriteLine("PASS: 324 tile geometry, 100-to-324 ownership and full terrain migration, extended packed coordinates. No runtime activation or game verification.");
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
