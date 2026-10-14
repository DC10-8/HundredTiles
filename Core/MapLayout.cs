using System;

namespace HundredTiles.Core
{
    // Geometry and migration model only. This does not authorize runtime patches.
    public sealed class MapLayout
    {
        public static readonly MapLayout Hundred = new MapLayout(10);
        public static readonly MapLayout ThreeTwentyFour = new MapLayout(18);
        public static MapLayout Active {get{return ExpandedMap.GridSize==18?ThreeTwentyFour:Hundred;}}
        public readonly int TileSide,TileCount,WorldMin,WorldMax,WorldWidth,HeightStride;
        public readonly int ObjectSide32,ObjectSide64,ServiceSide,DistrictSide,ResourceSide,UtilitySide;
        public readonly int MaximumObjectCode;
        public MapLayout(int tileSide)
        {
            if(tileSide!=10 && tileSide!=18)throw new ArgumentOutOfRangeException(nameof(tileSide));
            TileSide=tileSide;TileCount=checked(tileSide*tileSide);WorldMin=-8640;
            WorldWidth=checked(tileSide*1920);WorldMax=WorldMin+WorldWidth;
            HeightStride=WorldWidth/16+1;
            ObjectSide32=WorldWidth/32;ObjectSide64=WorldWidth/64;
            ServiceSide=WorldWidth*10/384;DistrictSide=WorldWidth*10/192;
            ResourceSide=(int)Math.Ceiling(WorldWidth/33.75);
            UtilitySide=(int)Math.Floor(WorldMax/38.25)+231+1;
            MaximumObjectCode=(int)Math.Ceiling(WorldMax/(double)PackedObjectPosition.Step)-1;
            if(MaximumObjectCode>98303)throw new ArgumentOutOfRangeException(nameof(tileSide),"Packed coordinates exceed the 17-bit extension.");
        }
        public bool TryGetTile(double x,double z,out int index)
        {
            index=-1;if(double.IsNaN(x) || double.IsNaN(z) || x<WorldMin || z<WorldMin || x>=WorldMax || z>=WorldMax)return false;
            index=(int)Math.Floor((z-WorldMin)/1920)*TileSide+(int)Math.Floor((x-WorldMin)/1920);return true;
        }
        // Preserve original samples and world coordinates; new samples replicate edges.
        public T[] ExtendSquare<T>(T[] source,int sourceSide,bool replicateEdges)
        {
            if(source==null || sourceSide<1 || source.Length!=checked(sourceSide*sourceSide) || sourceSide>HeightStride)throw new ArgumentException("Source square dimensions differ.");
            var result=new T[checked(HeightStride*HeightStride)];
            for(int z=0;z<(replicateEdges?HeightStride:sourceSide);z++)
            {
                int sourceRow=Math.Min(z,sourceSide-1)*sourceSide,targetRow=z*HeightStride;
                Array.Copy(source,sourceRow,result,targetRow,sourceSide);
                if(replicateEdges)for(int x=sourceSide;x<HeightStride;x++)result[targetRow+x]=source[sourceRow+sourceSide-1];
            }
            return result;
        }
        public OwnershipState ImportOwnership(OwnershipState source)
        {
            if(source==null || source.Grid==null)throw new ArgumentException("Missing ownership.");
            int side=source.Grid.Length==25?5:source.Grid.Length==81?9:source.Grid.Length==100?10:source.Grid.Length==324?18:0;
            if(side==0 || side>TileSide)throw new ArgumentException("Unsupported ownership migration.");
            Ownership.Validate(source,side);int offset=side==5?2:0;
            var result=new OwnershipState{Grid=new int[TileCount],Count=source.Count,Start=(source.Start/side+offset)*TileSide+source.Start%side+offset};
            for(int z=0;z<side;z++)Array.Copy(source.Grid,z*side,result.Grid,(z+offset)*TileSide+offset,side);
            Ownership.Validate(result,TileSide);return result;
        }
        public MapSaveState ImportTerrainWater(MapSaveState source,int sourceStride)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));source.Validate(sourceStride);
            if(sourceStride!=1081 && sourceStride!=1201 && sourceStride!=2161)throw new ArgumentException("Unsupported terrain migration.");
            return new MapSaveState{Heights=ExtendSquare(source.Heights,sourceStride,true),BlockOffsets=ExtendSquare(source.BlockOffsets,sourceStride,false),Water=ExtendSquare(source.Water,sourceStride,false),WaterFrame=source.WaterFrame};
        }
        // Origins of the 100-tile grids remain fixed; existing cells copy to the
        // same row/column. New cells initialize empty, without inventing supplies.
        public static T[] ExtendChannels<T>(T[] source,int sourceSide,int targetSide,int channels)
        {
            if(source==null || sourceSide<1 || targetSide<sourceSide || channels<1 || source.Length!=checked(sourceSide*sourceSide*channels))throw new ArgumentException("Channel grid dimensions differ.");
            var result=new T[checked(targetSide*targetSide*channels)];
            for(int z=0;z<sourceSide;z++)Array.Copy(source,z*sourceSide*channels,result,z*targetSide*channels,sourceSide*channels);
            return result;
        }
    }
}
