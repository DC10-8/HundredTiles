using System;
using System.Collections.Generic;

namespace HundredTiles.Core
{
    public sealed class RiverExtension
    {
        public readonly ushort[] Heights, Levels;
        public readonly List<int> Sources = new List<int>();
        public int Connections, Obstructed;
        public RiverExtension(ushort[] heights, ushort[] water, ushort[] ground, bool[] occupied, float sea) : this(heights,water,ground,occupied,sea,1080) {}
        public RiverExtension(ushort[] heights,ushort[] water,ushort[] ground,bool[] occupied,float sea,int preservedRawResolution)
        {
            int length=ExpandedMap.HeightStride*ExpandedMap.HeightStride;
            if(heights==null || water==null || ground==null || occupied==null || heights.Length!=length || water.Length!=length || ground.Length!=length || occupied.Length!=length || preservedRawResolution<1 || preservedRawResolution>ExpandedMap.RawResolution || float.IsNaN(sea) || float.IsInfinity(sea))throw new ArgumentException("River extension dimensions differ.");
            Heights=(ushort[])heights.Clone(); Levels=new ushort[heights.Length];
            if(preservedRawResolution==ExpandedMap.RawResolution)return;
            for(int side=0;side<2;side++)
            {
                int p=1;
                while(p<preservedRawResolution)
                {
                    int i=Index(side,preservedRawResolution,p);
                    if(water[i]<32 && heights[i]/64f>=sea){p++;continue;}
                    int start=p++;
                    while(p<preservedRawResolution){i=Index(side,preservedRawResolution,p);if(water[i]<32 && heights[i]/64f>=sea)break;p++;}
                    int end=p-1;
                    if(end-start<2)continue;
                    bool blocked=false;
                    for(int d=(preservedRawResolution+1);d<=ExpandedMap.RawResolution;d++)for(int c=start;c<=end;c++)
                        if(occupied[Index(side,d,c)])blocked=true;
                    // Never create a channel that terminates at a protected road
                    // or building. Keep the complete crossing untouched instead.
                    if(blocked){Obstructed++;continue;}
                    Connections++;
                    int centre=(start+end)/2;
                    int centreIndex=Index(side,preservedRawResolution,centre);
                    int surface=Math.Max((int)Math.Round(sea*64),ground[centreIndex]+water[centreIndex]);
                    for(int d=(preservedRawResolution+1);d<=ExpandedMap.RawResolution;d++)
                    {
                        // The native outermost water cells are fixed to sea level.
                        // Descend to that outlet; a high riverbed at the last
                        // vertex becomes a dry dam after the first water pass.
                        int fall=(int)Math.Round(Math.Max(0,surface-sea*64)*(d-preservedRawResolution)/(double)(ExpandedMap.RawResolution-preservedRawResolution));
                        for(int c=Math.Max(0,start-6);c<=Math.Min(preservedRawResolution,end+6);c++)
                        {
                            i=Index(side,d,c);if(occupied[i])continue;
                            int edge=Index(side,preservedRawResolution,c);
                            int bankDistance=Math.Max(start-c,c-end);
                            double blend=bankDistance<=0?1:Math.Max(0,1-bankDistance/6.0);
                            int bed=Math.Max(0,heights[edge]-fall);
                            Heights[i]=(ushort)Math.Round(Heights[i]+Math.Min(0,bed-Heights[i])*blend);
                            if(c>=start && c<=end)
                            {
                                int localStart=Math.Max((int)Math.Round(sea*64),ground[edge]+water[edge]);
                                int localSurface=(int)Math.Round(localStart+(sea*64-localStart)*(d-preservedRawResolution)/(double)(ExpandedMap.RawResolution-preservedRawResolution));
                                Levels[i]=(ushort)Math.Max(Levels[i],Math.Max(Heights[i],Math.Min(65535,localSurface)));
                            }
                        }
                        if(surface>sea*64+128 && (end-start)<120 && (d-(preservedRawResolution+1))%20==0)
                            Sources.Add(Index(side,d,centre));
                    }
                }
            }
        }
        static int Index(int side,int distance,int cross){return side==0?cross*ExpandedMap.HeightStride+distance:distance*ExpandedMap.HeightStride+cross;}
    }
}

