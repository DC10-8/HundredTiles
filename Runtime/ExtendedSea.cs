using System;
using ColossalFramework;
namespace HundredTiles.Runtime
{
    public static class ExtendedSea
    {
        // Called on the simulation thread before reporting unlock completion.
        public static int Initialize()
        {
            var terrain=Singleton<TerrainManager>.instance;
            var water=terrain.WaterSimulation;
            if(terrain.BlockHeights.Length!=1201*1201)throw new InvalidOperationException("Expanded terrain required for sea initialization.");
            int sea=Math.Max(0,Math.Min(65535,(int)Math.Round(water.m_currentSeaLevel*64f))),changed=0;
            using(var handoff=new LandscapeWater(water))
            {
                var masks=new ulong[][]{(ulong[])typeof(WaterSimulation).GetField("m_waterExists1",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(water),(ulong[])typeof(WaterSimulation).GetField("m_waterExists2",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(water),(ulong[])typeof(WaterSimulation).GetField("m_waterExists3",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(water)};
                for(int z=0;z<=1200;z++)for(int x=0;x<=1200;x++)
                {
                    if(x<=1080 && z<=1080)continue;
                    int index=z*1201+x,depth=sea-terrain.BlockHeights[index];
                    if(depth<=0)continue;
                    bool filled=false;
                    foreach(var buffer in handoff.Buffers)if(buffer[index].m_height<depth){buffer[index].m_height=(ushort)depth;buffer[index].m_velocityX=0;buffer[index].m_velocityZ=0;filled=true;}
                    if(filled)changed++;
                    foreach(var mask in masks)mask[z/19]|=1UL<<(x/19);
                }
                // Populate the native render maps now, including when paused.
                for(int z=0;z<10;z++)for(int x=0;x<10;x++)if(x==9 || z==9)water.UpdatePatch(x,z);
            }
            UnityEngine.Debug.Log("[HundredTiles] Extended sea initialized: "+changed+" cells; sea level "+water.m_currentSeaLevel+"m.");
            return changed;
        }
    }
}
