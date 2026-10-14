using System;
using System.IO;
using HundredTiles.Core;

static class Profile324Checks
{
    public static void Run()
    {
        Require(ExpandedMap.TileCount==324 && ExpandedMap.HeightStride==2161,"compiled profile");
        Require(ServiceGrid.Side==900 && DistrictGrid.Side==1800 && ResourceGrid.Side==1024 && UtilityGrid.Side==909,"compiled grids");
        var rows=new int[909];for(int phase=0;phase<128;phase++)for(int row=UtilityGrid.FirstRow(phase);row<=UtilityGrid.LastRow(phase);row++)rows[row]++;
        foreach(int visits in rows)Require(visits==1,"all utility rows calculated once");
        var serviceRows=new int[900];for(uint frame=0;frame<256;frame++)for(int row=ServiceGrid.RowStart(frame);row<ServiceGrid.RowEnd(frame);row++)serviceRows[row]++;
        foreach(int visits in serviceRows)Require(visits==1,"all service rows calculated once");
        Require(WaterGrid.GetMaskCoordinate(2160)==63,"water mask edge without aliasing");
        for(int raw=0;raw<=2160;raw++)Require(WaterGrid.GetMaskCoordinate(raw)<64,"water mask domain");
        foreach(float position in new[]{-8640f,0,10560,17280,25919.9f}){var axis=PackedObjectPosition.Encode(position);Require(Math.Abs(PackedObjectPosition.Decode(axis)-position)<.3,"far packed coordinates");}
        var old=new MapSaveState{Heights=new ushort[1201*1201],BlockOffsets=new ushort[1201*1201],Water=new WaterCellState[1201*1201],WaterFrame=91};
        old.Heights[1200*1201+1200]=3100;old.BlockOffsets[1200*1201+1200]=23;old.Water[1200*1201+1200]=new WaterCellState{Height=40,Pollution=9,VelocityX=-2,VelocityZ=7};
        byte[] bytes100=SaveCodec.Encode(old,MapLayout.Hundred),digest;
        var ownership100=new OwnershipState{Grid=new int[100],Start=44,Count=100};for(int i=0;i<100;i++)ownership100.Grid[i]=i+1;
        byte[] oldOwnership=Ownership.Encode(ownership100,bytes100,MapLayout.Hundred);
        var ownership324=Ownership.Decode(oldOwnership,Ownership.Project(ownership100),bytes100);Require(ownership324.Grid.Length==324 && ownership324.Start==76 && ownership324.Count==100 && ownership324.Grid[9*18+9]==100,"ownership v1-to-v2 migration");
        int order=100;for(int z=0;z<18;z++)for(int x=0;x<18;x++)if(ownership324.Grid[z*18+x]==0)ownership324.Grid[z*18+x]=++order;ownership324.Count=324;
        var migrated=SaveCodec.Decode(bytes100,out digest);migrated.Validate(2161);
        int retained=1200*2161+1200;Require(migrated.Heights[retained]==3100 && migrated.BlockOffsets[retained]==23 && migrated.Water[retained].Height==40 && migrated.Water[retained].Pollution==9 && migrated.Water[retained].VelocityX==-2 && migrated.Water[retained].VelocityZ==7 && migrated.WaterFrame==91,"v1 migration preserves saved values");
        migrated.Heights[2160*2161+2160]=5100;migrated.Water[2160*2161+2160]=new WaterCellState{Height=21};
        byte[] bytes324=SaveCodec.Encode(migrated);Require(bytes324.Length==SaveCodec.EncodedSize,"v2 dimensions");
        var restored=SaveCodec.Decode(bytes324,out digest);Require(restored.Heights[2160*2161+2160]==5100 && restored.Water[2160*2161+2160].Height==21,"v2 far edge round trip");
        byte[] newOwnership=Ownership.Encode(ownership324,bytes324);var restoredOwner=Ownership.Decode(newOwnership,Ownership.Project(ownership324),bytes324);Require(restoredOwner.Count==324 && restoredOwner.Grid[323]>255,"324 purchase orders preserve integers above byte range");
        var storage=new System.Collections.Generic.Dictionary<string,byte[]>();SaveTransport.Publish(storage,"terrain",bytes100,bytes100.Length);Require(SaveTransport.Read(storage,"terrain").Length==bytes100.Length,"100 chunks read in 324 profile");SaveTransport.Publish(storage,"terrain",bytes324);Require(SaveTransport.Read(storage,"terrain").Length==bytes324.Length,"324 chunk transport");
        var resources100=new ResourceState{Cells=new byte[569*569*12],Cache=new byte[4000]};resources100.Cells[(560*569+560)*12]=77;resources100.Cache[3999]=231;
        byte[] resourceBytes=ResourceCodec.Encode(resources100,bytes100,oldOwnership,MapLayout.Hundred);
        var resourceLegacy=new ResourceState{Cells=new byte[512*512*12],Cache=new byte[3240]};
        var resources324=ResourceCodec.Decode(resourceBytes,resourceLegacy,bytes100,oldOwnership);
        Require(resources324.Cells[(560*1024+560)*12]==77 && resources324.Cache[(9*18+9)*40+39]==231 && resources324.Cells[(1023*1024+1023)*12]==0,"resource v1 migration retains remote cells/cache and initializes new cells");
        byte[] resourceV2=ResourceCodec.Encode(resources324,bytes324,newOwnership);
        Require(ResourceCodec.Decode(resourceV2,ResourceCodec.Project(resources324),bytes324,newOwnership).Cells[(560*1024+560)*12]==77,"resource v2 round trip");
        resourceBytes[resourceBytes.Length-1]^=1;bool resourceRejected=false;try{ResourceCodec.Decode(resourceBytes,resourceLegacy,bytes100,oldOwnership);}catch(InvalidDataException){resourceRejected=true;}Require(resourceRejected,"corrupt resource migration rejected");
        bytes324[bytes324.Length/2]^=1;bool rejected=false;try{SaveCodec.Decode(bytes324,out digest);}catch(InvalidDataException){rejected=true;}Require(rejected,"corrupt v2 rejected");
        string sourceDirectory=Environment.GetEnvironmentVariable("HUNDREDTILES_MIGRATION_SOURCE_100");if(!string.IsNullOrEmpty(sourceDirectory))CheckActualSource(sourceDirectory);
        Console.WriteLine("PASS: compiled 324 dimensions, every supply/service row, 64-bit water masks, far packed coordinates, v1-to-v2 terrain/water migration and corrupt v2 rejection. No native game activation.");
    }
    static void Require(bool value,string name){if(!value)throw new Exception(name);}
    static void CheckActualSource(string directory)
    {
        byte[] terrain=File.ReadAllBytes(Path.Combine(directory,"terrain-water.bin")),owner=File.ReadAllBytes(Path.Combine(directory,"ownership.bin")),digest;MapLayout source;
        var original=SaveCodec.DecodeOriginal(terrain,out digest,out source);Require(source.TileSide==10,"actual source must be 100 tiles");
        var migrated=SaveCodec.Decode(terrain,out digest);migrated.Validate(2161);
        for(int z=0;z<1201;z++)for(int x=0;x<1201;x++){int oldIndex=z*1201+x,newIndex=z*2161+x;var a=original.Water[oldIndex];var b=migrated.Water[newIndex];Require(original.Heights[oldIndex]==migrated.Heights[newIndex] && original.BlockOffsets[oldIndex]==migrated.BlockOffsets[newIndex] && a.Height==b.Height && a.Pollution==b.Pollution && a.VelocityX==b.VelocityX && a.VelocityZ==b.VelocityZ,"actual saved terrain/water preserved");}
        var legacy=new OwnershipState{Grid=new int[25]};using(var reader=new BinaryReader(File.OpenRead(Path.Combine(directory,"legacy-ownership.bin")))){legacy.Start=reader.ReadInt32();legacy.Count=reader.ReadInt32();for(int i=0;i<25;i++)legacy.Grid[i]=reader.ReadInt32();Require(reader.BaseStream.Position==reader.BaseStream.Length,"legacy export length");}
        var ownership=Ownership.Decode(owner,legacy,terrain);int[] previous=(int[])ownership.Grid.Clone();
        bool progress=true;while(progress && ownership.Count<324){progress=false;for(int z=0;z<18;z++)for(int x=0;x<18;x++)if(Ownership.CanPurchase(ownership,x,z)){ownership.Grid[z*18+x]=++ownership.Count;progress=true;}}
        Require(ownership.Count==324,"all 324 purchases in migrated state");for(int i=0;i<previous.Length;i++)if(previous[i]!=0)Require(previous[i]==ownership.Grid[i],"existing purchase orders preserved");
        int edge=2160*2161+2160;migrated.Heights[edge]=4200;migrated.Water[edge]=new WaterCellState{Height=18,Pollution=2,VelocityX=-1,VelocityZ=3};
        byte[] newTerrain=SaveCodec.Encode(migrated),newOwner=Ownership.Encode(ownership,newTerrain);
        var restored=SaveCodec.Decode(newTerrain,out digest);var restoredOwner=Ownership.Decode(newOwner,Ownership.Project(ownership),newTerrain);
        Require(restored.Water[edge].Height==18 && restored.Heights[edge]==4200 && restoredOwner.Count==324 && restoredOwner.Grid[323]!=0,"actual-source migration round trip");
        SaveCodec.RequireSame(Hash(terrain),Hash(File.ReadAllBytes(Path.Combine(directory,"terrain-water.bin"))));SaveCodec.RequireSame(Hash(owner),Hash(File.ReadAllBytes(Path.Combine(directory,"ownership.bin"))));
        Console.WriteLine("PASS: native-exported 100-tile save extensions migrate with every original terrain/water cell preserved; 324 logical purchases and v2 round trip; source files unchanged. Native 324 city reload not performed.");
    }
    static byte[] Hash(byte[] value){using(var sha=System.Security.Cryptography.SHA256.Create())return sha.ComputeHash(value);}
}
