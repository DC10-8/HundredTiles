using System;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using HarmonyLib;

static class TerrainSurfaceTrace
{
    static ushort road,rail;static bool installed;static int getterCalls;
    public static void Install()
    {
        if(installed)return;installed=true;
        var ids=File.ReadAllLines(Path.Combine(Entry.Root,"trial324-far-objects.txt"));road=ushort.Parse(ids[0]);rail=ushort.Parse(ids[1]);
        var harmony=new Harmony("HundredTiles.Isolated.SurfaceTrace");
        harmony.Patch(typeof(NetSegment).GetMethod("TerrainUpdated"),prefix:new HarmonyMethod(typeof(TerrainSurfaceTrace).GetMethod("Segment")));
        var method=typeof(TerrainModify).GetMethod("ApplyQuad",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(Quad2),typeof(TerrainModify.Edges),typeof(TerrainModify.Surface)},null);
        if(method==null)throw new Exception("Native terrain surface rasterizer missing.");
        harmony.Patch(method,prefix:new HarmonyMethod(typeof(TerrainSurfaceTrace).GetMethod("Quad")),postfix:new HarmonyMethod(typeof(TerrainSurfaceTrace).GetMethod("QuadResult")));
        harmony.Patch(typeof(TerrainManager).GetMethod("Managers_AfterTerrainUpdate"),prefix:new HarmonyMethod(typeof(TerrainSurfaceTrace).GetMethod("AfterCopy")));
        harmony.Patch(typeof(TerrainManager).GetMethod("GetSurfaceCell"),prefix:new HarmonyMethod(typeof(TerrainSurfaceTrace).GetMethod("GetterInput")),postfix:new HarmonyMethod(typeof(TerrainSurfaceTrace).GetMethod("GetterResult")));
    }
    public static void Segment(ushort segmentID,float minX,float minZ,float maxX,float maxZ)
    {if(segmentID==road || segmentID==rail)Entry.Log("TRACE: segment terrain update "+segmentID+" bounds="+minX+","+minZ+","+maxX+","+maxZ);}
    public static void GetterInput(int x,int z){if(getterCalls++<12 || (x==8563 && z==8335))Entry.Log("TRACE: getter arguments "+x+","+z);}
    public static void GetterResult(int x,int z,TerrainManager.SurfaceCell __result){if(x==8563 && z==8335)Entry.Log("TRACE: getter result in patch "+__result.m_clipped);}
    public static void Quad(Quad2 quad,TerrainModify.Edges edges,TerrainModify.Surface surface)
    {
        if(quad.a.x<24000 || quad.a.y<24000)return;
        var terrain=Singleton<TerrainManager>.instance;
        Entry.Log("TRACE: far surface quad "+quad.a+" / "+quad.b+" / "+quad.c+" / "+quad.d+" flags="+surface+", edges="+edges+", modified="+terrain.m_surfaceModified.m_minX+","+terrain.m_surfaceModified.m_minZ+","+terrain.m_surfaceModified.m_maxX+","+terrain.m_surfaceModified.m_maxZ);
    }
    public static void QuadResult(Quad2 quad,TerrainModify.Surface surface)
    {
        if(quad.a.x<24000 || quad.a.y<24000 || (surface & TerrainModify.Surface.Clip)==0)return;
        var terrain=Singleton<TerrainManager>.instance;var area=terrain.m_surfaceModified;var point=(quad.a+quad.b+quad.c+quad.d)*.25f;
        int x=(int)((point.x+8640)/4),z=(int)((point.y+8640)/4),width=((area.m_maxX-area.m_minX)<<2)+1;
        int index=(z-(area.m_minZ<<2))*width+x-(area.m_minX<<2);
        if(index<0 || index>=terrain.m_tempSurface.Length)throw new Exception("Trace sample outside scratch buffer.");
        Entry.Log("TRACE: clip raster result at "+point+" = "+terrain.m_tempSurface[index].m_clipped);
    }
    public static void AfterCopy()
    {
        var terrain=Singleton<TerrainManager>.instance;var area=terrain.m_surfaceModified;
        if(area.m_minX<2000 || area.m_minX>area.m_maxX)return;
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        bool modifying=(bool)typeof(TerrainManager).GetField("m_modifyingSurface",flags).GetValue(terrain);
        Entry.Log("TRACE: after surface copy: modifying="+modifying+", cpu clip="+terrain.GetSurfaceCell(8563,8335).m_clipped);
        var reflected=(TerrainManager.SurfaceCell)typeof(TerrainManager).GetMethod("GetSurfaceCell").Invoke(terrain,new object[]{8563,8335});
        Entry.Log("TRACE: reflection surface clip="+reflected.m_clipped);
        var patches=(TerrainPatch[])typeof(TerrainManager).GetField("m_patches",flags).GetValue(terrain);
        var details=(TerrainManager.SurfaceCell[])typeof(TerrainManager).GetField("m_detailSurface",flags).GetValue(terrain);
        int local=175*480+403;
        for(int i=0;i<patches.Length;i++){int detail=patches[i].m_simDetailIndex;if(detail>0 && details[(detail-1)*230400+local].m_clipped!=0)Entry.Log("TRACE: populated comparison cell at patch "+i+" detail="+detail+" clip="+details[(detail-1)*230400+local].m_clipped);}
    }
}
