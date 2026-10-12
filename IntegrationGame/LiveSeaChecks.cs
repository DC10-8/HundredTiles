using System;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Runtime;
static class LiveSeaChecks
{
 public static void Run()
 {
  var terrain=Singleton<TerrainManager>.instance;var water=terrain.WaterSimulation;
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var runtime=typeof(ExtendedSea).Assembly;
  using(var handoff=(IDisposable)Activator.CreateInstance(runtime.GetType("HundredTiles.Runtime.LandscapeWater"),new object[]{water}))
  {
   var buffers=(WaterSimulation.Cell[][])typeof(WaterSimulation).GetField("m_waterBuffers",flags).GetValue(water);
   int sea=(int)Math.Round(water.m_currentSeaLevel*64f);if(sea<2)throw new Exception("Verification map needs positive sea level.");
   int dry=800*1201+1150,wet=dry+1,land=dry+2,old=800*1201+800;
   var indices=new[]{dry,wet,land,old};var heights=new ushort[4];var saved=new WaterSimulation.Cell[buffers.Length][];
   for(int b=0;b<buffers.Length;b++){saved[b]=new WaterSimulation.Cell[4];for(int j=0;j<4;j++)saved[b][j]=buffers[b][indices[j]];}
   for(int j=0;j<4;j++)heights[j]=terrain.BlockHeights[indices[j]];
   try{
    terrain.BlockHeights[dry]=(ushort)(sea-1);terrain.BlockHeights[wet]=(ushort)(sea-1);terrain.BlockHeights[land]=(ushort)Math.Min(65535,sea+100);terrain.BlockHeights[old]=(ushort)(sea-1);
    foreach(var buffer in buffers){buffer[dry].m_height=0;buffer[wet].m_height=50;buffer[wet].m_velocityX=123;buffer[land].m_height=0;buffer[old].m_height=0;}
    int owned=BulkUnlock.UnlockAll();if(owned!=100)throw new Exception("Sea initialization unlock failed.");
    foreach(var buffer in buffers)if(buffer[dry].m_height!=1 || buffer[wet].m_height!=50 || buffer[wet].m_velocityX!=123 || buffer[land].m_height!=0 || buffer[old].m_height!=0)throw new Exception("Unlock sea depth / preserved river / dry land / original area differs.");
    ExtendedSea.Initialize();foreach(var buffer in buffers)if(buffer[dry].m_height!=1 || buffer[wet].m_height!=50)throw new Exception("Repeated sea initialization changed water.");
    if(!water.WaterExists(1150,800,1150,800))throw new Exception("Extended sea render mask missing.");
    Entry.Log("PASS: actual bulk unlock seeds both water buffers immediately with water worker stopped; original area, high ground and deeper flowing water preserved; repeated initialization stable; native render maps refreshed.");
   }finally{for(int j=0;j<4;j++)terrain.BlockHeights[indices[j]]=heights[j];for(int b=0;b<buffers.Length;b++)for(int j=0;j<4;j++)buffers[b][indices[j]]=saved[b][j];}
  }
 }
}
