using System;

namespace HundredTiles.Core
{
    public static class DistrictNames
    {
        // Half-resolution name search needs 250000 indices, beyond ushort.
        // Each sample is enqueued once; linear head/tail also handles a queue
        // initially containing every sample without a ring full/empty ambiguity.
        public static int[] Calculate(byte[] ids)
        {
            if(ids==null || ids.Length!=DistrictGrid.Side*DistrictGrid.Side)throw new ArgumentException("District name grid differs.");
            const int side=DistrictGrid.Side/2,count=side*side;
            var distance=new int[count];var queue=new int[count];var sumX=new int[128];var sumZ=new int[128];var divider=new int[128];
            var scores=new int[128];var locations=new int[128];int head=0,tail=0;
            for(int z=0;z<side;z++)for(int x=0;x<side;x++)
            {
                int cell=z*2*DistrictGrid.Side+x*2;byte id=ids[cell];if(id>=128)throw new ArgumentException("District name identifier differs.");
                if(id==0)continue;
                if(x!=0 && z!=0 && x!=side-1 && z!=side-1 && ids[cell-2]==id && ids[cell+2]==id && ids[cell-2*DistrictGrid.Side]==id && ids[cell+2*DistrictGrid.Side]==id)continue;
                int sample=z*side+x;distance[sample]=1;queue[tail++]=sample;sumX[id]+=x;sumZ[id]+=z;divider[id]++;
            }
            for(int i=0;i<128;i++)if(divider[i]!=0){sumX[i]=(sumX[i]+divider[i]/2)/divider[i];sumZ[i]=(sumZ[i]+divider[i]/2)/divider[i];}
            while(head<tail)
            {
                int sample=queue[head++],x=sample%side,z=sample/side,cell=z*2*DistrictGrid.Side+x*2;byte id=ids[cell];
                int dx=x-sumX[id],dz=z-sumZ[id];int score=DistrictGrid.Side*DistrictGrid.Side-(DistrictGrid.Side*DistrictGrid.Side/2)/distance[sample]-dx*dx-dz*dz;
                if(score>scores[id]){scores[id]=score;locations[id]=sample;}
                if(x>0)Visit(sample-1,cell-2,id,distance[sample]+1,ids,distance,queue,ref tail);
                if(x<side-1)Visit(sample+1,cell+2,id,distance[sample]+1,ids,distance,queue,ref tail);
                if(z>0)Visit(sample-side,cell-2*DistrictGrid.Side,id,distance[sample]+1,ids,distance,queue,ref tail);
                if(z<side-1)Visit(sample+side,cell+2*DistrictGrid.Side,id,distance[sample]+1,ids,distance,queue,ref tail);
            }
            return locations;
        }
        static void Visit(int sample,int cell,byte id,int depth,byte[] ids,int[] distance,int[] queue,ref int tail)
        {if(distance[sample]!=0 || ids[cell]!=id)return;distance[sample]=depth;queue[tail++]=sample;}
        public static float WorldX(int sample) {return (sample%(DistrictGrid.Side/2)*2-DistrictGrid.Origin)*DistrictGrid.CellSize;}
        public static float WorldZ(int sample) {return (sample/(DistrictGrid.Side/2)*2-DistrictGrid.Origin)*DistrictGrid.CellSize;}
    }
}
