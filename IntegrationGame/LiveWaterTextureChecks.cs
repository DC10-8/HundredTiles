using System;
using ColossalFramework;
using UnityEngine;

static class LiveWaterTextureChecks
{
    static void Fill(byte[] bytes,int tag){for(int i=0;i<bytes.Length;i++)bytes[i]=(byte)(tag+(i%4)*17);}
    static void Equal(Texture2D texture,byte[] expected){var actual=texture.GetRawTextureData();if(actual.Length!=expected.Length)throw new Exception("Water texture byte count mismatch.");for(int i=0;i<actual.Length;i++)if(actual[i]!=expected[i])throw new Exception("Water texture read from a different patch at byte "+i);}
    public static void Run()
    {
        var terrain=Singleton<TerrainManager>.instance;var water=terrain.WaterSimulation;
        for(int i=0;i<100;i++)
        {
            Fill(water.m_heightMaps[i],i+1);Fill(water.m_surfaceMapsA[i],i+31);Fill(water.m_surfaceMapsB[i],i+61);
            water.m_heightBounds[i]=new Vector4(i+20,i+21,i+20,i+21);
        }
        for(int i=0;i<100;i++)
        {
            var patch=terrain.m_patches[i];patch.m_waterInitialized=false;patch.Refresh(true,192);
            for(int frame=0;frame<2;frame++){Equal(patch.m_waterHeight[frame].m_waterHeight,water.m_heightMaps[i]);if(patch.m_waterHeight[frame].m_waterMinY!=i+20 || patch.m_waterHeight[frame].m_waterMaxY!=i+21)throw new Exception("Water bounds read from a different patch.");}
            for(int frame=0;frame<3;frame++){Equal(patch.m_waterSurfaceA[frame],water.m_surfaceMapsA[i]);Equal(patch.m_waterSurfaceB[frame],water.m_surfaceMapsB[i]);}
            Fill(water.m_heightMaps[i],i+2);Fill(water.m_surfaceMapsA[i],i+32);Fill(water.m_surfaceMapsB[i],i+62);
            patch.m_waterExists=1;patch.Refresh(true,256);
            Equal(patch.m_waterHeight[0].m_waterHeight,water.m_heightMaps[i]);Equal(patch.m_waterSurfaceA[1],water.m_surfaceMapsA[i]);Equal(patch.m_waterSurfaceB[1],water.m_surfaceMapsB[i]);
        }
        Entry.Log("PASS: actual TerrainPatch.Refresh for all 100 patches; both initial height frames, six surface textures, bounds, and subsequent height/surface updates match their own patch bytes.");
    }
}
