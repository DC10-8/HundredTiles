using System;

namespace HundredTiles.Core
{
    // All changes are outside the original 1081-square height field. A protected
    // site has a smooth, 128m transition as well as an unchanged inner footprint.
    public static class Landscape
    {
        public const int RiverCenterX=ExpandedMap.RawResolution-80,RiverCenterZ=ExpandedMap.RawResolution-82,RiverStart=ExpandedMap.RawResolution-98;
        public static double RiverX(int z) { return RiverCenterX + 5*Math.Sin((z-RiverCenterZ)*0.075); }
        public static ushort RiverLevel(ushort[] heights) { return (ushort)Math.Max(512,heights[RiverCenterZ*ExpandedMap.HeightStride+RiverCenterX]-256); }
        public static bool CanRiver(ushort[] heights, bool[] protectedSites)
        {
            if(heights[RiverCenterZ*ExpandedMap.HeightStride+RiverCenterX]<1024)return false;
            for(int z=RiverStart;z<=ExpandedMap.RawResolution;z++)for(int x=(int)RiverX(z)-14;x<=(int)RiverX(z)+14;x++)
                if(protectedSites[z*ExpandedMap.HeightStride+x])return false;
            return true;
        }
        public static ushort[] Generate(ushort[] original, bool[] protectedSites, bool river)
        {
            if(original.Length!=ExpandedMap.HeightStride*ExpandedMap.HeightStride || protectedSites.Length!=original.Length)throw new ArgumentException("Expanded landscape dimensions required.");
            var result=(ushort[])original.Clone();
            var distances=ProtectionDistances(protectedSites);
            double level=RiverLevel(original)/64.0;
            for(int z=0;z<=ExpandedMap.RawResolution;z++)for(int x=0;x<=ExpandedMap.RawResolution;x++)
            {
                if(x<=1080 && z<=1080)continue;
                int index=z*ExpandedMap.HeightStride+x;
                if(protectedSites[index])continue;
                double fade=Smooth(Math.Min(1,Math.Max(x-1080,z-1080)/24.0));
                int distance=distances[index];
                fade*=Smooth(distance/8.0);
                double baseHeight=original[index]/64.0;
                // Large hills plus smaller ridges. Phase follows the map's corner
                // height, so a given source map always produces the same result.
                double seed=original[1080*ExpandedMap.HeightStride+1080]*0.001;
                double relief=30*Math.Sin(x*.032+seed)*Math.Cos(z*.025-seed)
                    +12*Math.Sin(x*.076+z*.051)+5*Math.Cos(x*.151-z*.117);
                // Keep seabeds submerged and keep dry edge land dry.
                double target=baseHeight<8?Math.Max(0,baseHeight+relief*.08):Math.Max(8,baseHeight+relief);
                if(river && z>=RiverStart)
                {
                    double d=Math.Abs(x-RiverX(z));
                    double cap=Smooth(Math.Min(1,(z-RiverStart)/16.0));
                    if(d<14)
                    {
                        double bed=level-5-3*Math.Max(0,z-RiverCenterZ)/82.0;
                        double channel=bed+(level+4-bed)*Smooth(Math.Min(1,Math.Max(0,d-3)/8));
                        target=target*(1-cap*Smooth(Math.Min(1,(14-d)/3)))+channel*cap*Smooth(Math.Min(1,(14-d)/3));
                    }
                }
                result[index]=(ushort)Math.Max(0,Math.Min(65535,Math.Round((baseHeight+(target-baseHeight)*fade)*64)));
            }
            return result;
        }
        static byte[] ProtectionDistances(bool[] mask)
        {
            int stride=ExpandedMap.HeightStride;var distance=new byte[mask.Length];var queue=new int[mask.Length];int head=0,tail=0;
            for(int i=0;i<mask.Length;i++){distance[i]=mask[i]?(byte)0:(byte)8;if(mask[i])queue[tail++]=i;}
            while(head<tail){int index=queue[head++],next=distance[index]+1;if(next>=8)continue;int x=index%stride,z=index/stride;for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++){int nx=x+dx,nz=z+dz;if(nx<0 || nz<0 || nx>=stride || nz>=stride)continue;int neighbor=nz*stride+nx;if(distance[neighbor]<=next)continue;distance[neighbor]=(byte)next;queue[tail++]=neighbor;}}
            return distance;
        }
        static double Smooth(double t){return t*t*(3-2*t);}
        public static double ForestDensity(double x,double z)
        {return .25+.6*Math.Max(0,Math.Sin(x*.0017)*Math.Cos(z*.0021));}
    }
}

