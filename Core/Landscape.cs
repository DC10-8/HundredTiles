using System;

namespace HundredTiles.Core
{
    // All changes are outside the original 1081-square height field. A protected
    // site has a smooth, 128m transition as well as an unchanged inner footprint.
    public static class Landscape
    {
        public static double RiverX(int z) { return 1120 + 5*Math.Sin((z-1118)*0.075); }
        public static ushort RiverLevel(ushort[] heights) { return (ushort)Math.Max(512,heights[1118*1201+1120]-256); }
        public static bool CanRiver(ushort[] heights, bool[] protectedSites)
        {
            if(heights[1118*1201+1120]<1024)return false;
            for(int z=1102;z<=1200;z++)for(int x=(int)RiverX(z)-14;x<=(int)RiverX(z)+14;x++)
                if(protectedSites[z*1201+x])return false;
            return true;
        }
        public static ushort[] Generate(ushort[] original, bool[] protectedSites, bool river)
        {
            if(original.Length!=1201*1201 || protectedSites.Length!=original.Length)throw new ArgumentException("Expanded landscape dimensions required.");
            var result=(ushort[])original.Clone();
            double level=RiverLevel(original)/64.0;
            for(int z=0;z<=1200;z++)for(int x=0;x<=1200;x++)
            {
                if(x<=1080 && z<=1080)continue;
                int index=z*1201+x;
                if(protectedSites[index])continue;
                double fade=Smooth(Math.Min(1,Math.Max(x-1080,z-1080)/24.0));
                int distance=8;
                for(int dz=-8;dz<=8;dz++)for(int dx=-8;dx<=8;dx++)
                    if(x+dx>=0 && x+dx<=1200 && z+dz>=0 && z+dz<=1200 && protectedSites[(z+dz)*1201+x+dx])
                        distance=Math.Min(distance,Math.Max(Math.Abs(dx),Math.Abs(dz)));
                fade*=Smooth(distance/8.0);
                double baseHeight=original[index]/64.0;
                // Large hills plus smaller ridges. Phase follows the map's corner
                // height, so a given source map always produces the same result.
                double seed=original[1080*1201+1080]*0.001;
                double relief=30*Math.Sin(x*.032+seed)*Math.Cos(z*.025-seed)
                    +12*Math.Sin(x*.076+z*.051)+5*Math.Cos(x*.151-z*.117);
                // Keep seabeds submerged and keep dry edge land dry.
                double target=baseHeight<8?Math.Max(0,baseHeight+relief*.08):Math.Max(8,baseHeight+relief);
                if(river && z>=1102)
                {
                    double d=Math.Abs(x-RiverX(z));
                    double cap=Smooth(Math.Min(1,(z-1102)/16.0));
                    if(d<14)
                    {
                        double bed=level-5-3*Math.Max(0,z-1118)/82.0;
                        double channel=bed+(level+4-bed)*Smooth(Math.Min(1,Math.Max(0,d-3)/8));
                        target=target*(1-cap*Smooth(Math.Min(1,(14-d)/3)))+channel*cap*Smooth(Math.Min(1,(14-d)/3));
                    }
                }
                result[index]=(ushort)Math.Max(0,Math.Min(65535,Math.Round((baseHeight+(target-baseHeight)*fade)*64)));
            }
            return result;
        }
        static double Smooth(double t){return t*t*(3-2*t);}
        public static double ForestDensity(double x,double z)
        {return .25+.6*Math.Max(0,Math.Sin(x*.0017)*Math.Cos(z*.0021));}
    }
}
