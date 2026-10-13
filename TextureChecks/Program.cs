using HyperLoad.Core.Content;
using UnityEngine;

byte[] Block(ushort first,ushort second,bool alpha=false,byte alpha0=255,byte alpha1=0)
{
    var block=new byte[alpha?16:8];int start=alpha?8:0;
    block[start]=(byte)first;block[start+1]=(byte)(first>>8);block[start+2]=(byte)second;block[start+3]=(byte)(second>>8);
    for(int i=0;i<4;i++)block[start+4+i]=0xE4;
    if(alpha){block[0]=alpha0;block[1]=alpha1;ulong selectors=0;for(int i=0;i<16;i++)selectors|=(ulong)(i%8)<<(i*3);for(int i=0;i<6;i++)block[2+i]=(byte)(selectors>>(i*8));}
    return block;
}
void Expect(byte[] pixels,int pixel,byte channel,byte alpha)
{
    int offset=pixel*4;if(pixels[offset]!=channel || pixels[offset+1]!=channel || pixels[offset+2]!=channel || pixels[offset+3]!=alpha)throw new Exception("Unexpected decoded palette/alpha at pixel "+pixel);
}
var four=DxtExpansion.Expand(Block(65535,0),0,8,4,4,1,TextureFormat.DXT1);
byte[] fourColors={255,0,170,85};for(int i=0;i<16;i++)Expect(four,i,fourColors[i%4],255);
var three=DxtExpansion.Expand(Block(0,65535),0,8,4,4,1,TextureFormat.DXT1);
byte[] threeColors={0,255,127,0};for(int i=0;i<16;i++)Expect(three,i,threeColors[i%4],(byte)(i%4==3?0:255));
byte[] alphaEight={240,16,208,176,144,112,80,48},alphaSix={16,240,61,106,150,195,0,255};
byte[] forcedFour={0,255,85,170};
foreach(bool eight in new[]{false,true})
{
    var block=Block(0,65535,true,(byte)(eight?240:16),(byte)(eight?16:240));
    var pixels=DxtExpansion.Expand(block,0,16,4,4,1,TextureFormat.DXT5);var expected=eight?alphaEight:alphaSix;
    for(int i=0;i<16;i++)Expect(pixels,i,forcedFour[i%4],expected[i%8]);
}
var chain=Block(65535,0).Concat(Block(65535,0)).ToArray();var cropped=DxtExpansion.Expand(chain,0,16,3,2,2,TextureFormat.DXT1);
if(cropped.Length!=28)throw new Exception("NPOT mip chain length changed.");
for(int y=0;y<2;y++)for(int x=0;x<3;x++)Expect(cropped,y*3+x,fourColors[x],255);Expect(cropped,6,255,255);
void Reject(Action test){try{test();}catch(InvalidDataException){return;}throw new Exception("Malformed DXT input accepted.");}
Reject(()=>DxtExpansion.Expand(new byte[7],0,7,4,4,1,TextureFormat.DXT1));
Reject(()=>DxtExpansion.Expand(new byte[8],0,8,3,2,2,TextureFormat.DXT1));
Reject(()=>DxtExpansion.Expand(new byte[8],-1,8,4,4,1,TextureFormat.DXT1));
Reject(()=>DxtExpansion.Expand(new byte[8],0,8,4,4,1,TextureFormat.RGBA32));
Reject(()=>DxtExpansion.Expand(new byte[8],0,8,16384,16383,16,TextureFormat.DXT1));
Console.WriteLine("PASS: BC1 four/three-color and transparency; BC3 six/eight-alpha palettes and forced four-color; NPOT clipping and mip offsets; malformed inputs refused.");
