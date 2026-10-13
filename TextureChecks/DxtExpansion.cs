using System;
using System.IO;
using UnityEngine;

namespace HyperLoad.Core.Content
{
    // BC1/BC3 layouts follow Microsoft's block-compression specification.
    // Pure byte processing: no GPU readback and no image resizing.
    internal static class DxtExpansion
    {
        const long MaxExpandedBytes=512L*1024*1024;
        internal static byte[] Expand(byte[] data,int offset,int length,int width,int height,int mipCount,TextureFormat format)
        {
            if(data==null || offset<0 || length<0 || offset>data.Length-length || width<=0 || height<=0 || width>16384 || height>16384 || mipCount<1 || mipCount>16
                || (format!=TextureFormat.DXT1 && format!=TextureFormat.DXT5))throw new InvalidDataException("Invalid DXT expansion input.");
            bool alpha=format==TextureFormat.DXT5;int blockSize=alpha?16:8,w=width,h=height;
            long required=0,expanded=0;
            for(int level=0;level<mipCount;level++){required+=(long)((w+3)/4)*((h+3)/4)*blockSize;expanded+=(long)w*h*4;w=Math.Max(1,w/2);h=Math.Max(1,h/2);}
            if(required>length || expanded>MaxExpandedBytes)throw new InvalidDataException("Truncated or oversized DXT mip chain.");
            var result=new byte[(int)expanded];var colors=new byte[16];var opacity=new byte[8];
            int cursor=offset,destination=0;w=width;h=height;
            for(int level=0;level<mipCount;level++)
            {
                for(int by=0;by<(h+3)/4;by++)for(int bx=0;bx<(w+3)/4;bx++)
                {
                    int colorStart=cursor+(alpha?8:0);
                    int endpoint0=data[colorStart]|data[colorStart+1]<<8,endpoint1=data[colorStart+2]|data[colorStart+3]<<8;
                    Endpoint(endpoint0,colors,0);Endpoint(endpoint1,colors,4);
                    bool four=alpha || endpoint0>endpoint1;
                    for(int channel=0;channel<3;channel++)
                    {
                        int a=colors[channel],b=colors[4+channel];
                        colors[8+channel]=(byte)(four?(2*a+b+1)/3:(a+b)/2);
                        colors[12+channel]=(byte)(four?(a+2*b+1)/3:0);
                    }
                    colors[11]=255;colors[15]=(byte)(four?255:0);
                    uint selectors=(uint)(data[colorStart+4]|data[colorStart+5]<<8|data[colorStart+6]<<16|data[colorStart+7]<<24);
                    ulong alphaSelectors=0;
                    if(alpha)
                    {
                        opacity[0]=data[cursor];opacity[1]=data[cursor+1];
                        int count=opacity[0]>opacity[1]?6:4,denominator=count+1;
                        for(int i=0;i<count;i++)opacity[i+2]=(byte)(((count-i)*opacity[0]+(i+1)*opacity[1]+denominator/2)/denominator);
                        if(count==4){opacity[6]=0;opacity[7]=255;}
                        for(int i=0;i<6;i++)alphaSelectors|=(ulong)data[cursor+2+i]<<(8*i);
                    }
                    for(int py=0;py<4;py++)for(int px=0;px<4;px++)
                    {
                        int x=bx*4+px,y=by*4+py,pixel=py*4+px;
                        if(x>=w || y>=h)continue;
                        int color=(int)((selectors>>(pixel*2))&3)*4,index=destination+(y*w+x)*4;
                        result[index]=colors[color];result[index+1]=colors[color+1];result[index+2]=colors[color+2];
                        result[index+3]=alpha?opacity[(int)((alphaSelectors>>(pixel*3))&7)]:colors[color+3];
                    }
                    cursor+=blockSize;
                }
                destination+=w*h*4;w=Math.Max(1,w/2);h=Math.Max(1,h/2);
            }
            return result;
        }
        static void Endpoint(int value,byte[] colors,int offset)
        {
            int r=(value>>11)&31,g=(value>>5)&63,b=value&31;
            colors[offset]=(byte)((r<<3)|(r>>2));colors[offset+1]=(byte)((g<<2)|(g>>4));colors[offset+2]=(byte)((b<<3)|(b>>2));colors[offset+3]=255;
        }
    }
}
