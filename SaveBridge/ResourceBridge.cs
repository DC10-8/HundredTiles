using System;
using System.IO;
using ColossalFramework;
using HundredTiles.Core;

namespace HundredTiles.SaveBridge
{
    public static class ResourceBridge
    {
        static NaturalResourceManager.ResourceCell[] saveCells,readCells;
        static NaturalResourceManager.AreaCell[] saveCache,readCache;
        static ResourceState legacyRead;
        public sealed class Prepared
        {internal NaturalResourceManager.ResourceCell[] Cells;internal NaturalResourceManager.AreaCell[] Cache;}
        public static byte[] Capture(byte[] terrain,byte[] owner)
        {
            var manager=Singleton<NaturalResourceManager>.instance;
            var state=Pack(manager.m_naturalResources,manager.m_areaResources);
            byte[] bytes=ResourceCodec.Encode(state,terrain,owner);
            var legacy=Unpack(ResourceCodec.Project(state));saveCells=legacy.Cells;saveCache=legacy.Cache;
            return bytes;
        }
        public static NaturalResourceManager.ResourceCell[] WriteCells(NaturalResourceManager.ResourceCell[] original)
        {if(saveCells==null)throw new InvalidOperationException("Resource snapshot missing.");return saveCells;}
        public static NaturalResourceManager.AreaCell[] WriteCache(NaturalResourceManager.AreaCell[] original)
        {if(saveCache==null)throw new InvalidOperationException("Resource snapshot missing.");return saveCache;}
        public static void BeginLoad() {readCells=new NaturalResourceManager.ResourceCell[512*512];readCache=new NaturalResourceManager.AreaCell[81];legacyRead=null;}
        public static NaturalResourceManager.ResourceCell[] ReadCells(NaturalResourceManager.ResourceCell[] original)
        {if(readCells==null)throw new InvalidOperationException("Resource read session missing.");return readCells;}
        public static NaturalResourceManager.AreaCell[] ReadCache(NaturalResourceManager.AreaCell[] original)
        {if(readCache==null)throw new InvalidOperationException("Resource read session missing.");return readCache;}
        public static void ReadComplete()
        {
            if(readCells==null || legacyRead!=null)throw new InvalidOperationException("Invalid resource read stage.");
            legacyRead=Pack(readCells,readCache);ResourceCodec.Canonicalize(legacyRead);
            Commit(Unpack(ResourceCodec.Expand(legacyRead)));
        }
        public static Prepared Prepare(byte[] bytes,byte[] terrain,byte[] owner)
        {
            if(legacyRead==null)throw new InvalidOperationException("Resource deserialization incomplete.");
            var state=bytes==null?ResourceCodec.Expand(legacyRead):ResourceCodec.Decode(bytes,legacyRead,terrain,owner);
            var manager=Singleton<NaturalResourceManager>.instance;
            if(manager.m_naturalResources.Length!=ResourceGrid.Cells || manager.m_areaResources.Length!=ExpandedMap.TileCount)
                throw new InvalidDataException("Resource restore target is not expanded.");
            return Unpack(state);
        }
        public static void Commit(Prepared state)
        {
            var manager=Singleton<NaturalResourceManager>.instance;
            if(manager.m_naturalResources.Length!=state.Cells.Length || manager.m_areaResources.Length!=state.Cache.Length)
                throw new InvalidDataException("Resource restore target shape differs.");
            Array.Copy(state.Cells,manager.m_naturalResources,state.Cells.Length);Array.Copy(state.Cache,manager.m_areaResources,state.Cache.Length);
            manager.AreaModified(0,0,ResourceGrid.Side-1,ResourceGrid.Side-1);manager.AreaModifiedB(0,0,ResourceGrid.Side-1,ResourceGrid.Side-1);
        }
        public static void ClearSave() {saveCells=null;saveCache=null;}
        public static void ClearLoad() {readCells=null;readCache=null;legacyRead=null;}
        static ResourceState Pack(NaturalResourceManager.ResourceCell[] cells,NaturalResourceManager.AreaCell[] cache)
        {
            var bytes=new byte[cells.Length*12];
            for(int i=0;i<cells.Length;i++)
            {
                var c=cells[i];int p=i*12;
                bytes[p]=c.m_ore;bytes[p+1]=c.m_oil;bytes[p+2]=c.m_sand;bytes[p+3]=c.m_forest;bytes[p+4]=c.m_tree;bytes[p+5]=c.m_fertility;
                bytes[p+6]=c.m_pollution;bytes[p+7]=c.m_water;bytes[p+8]=c.m_shore;bytes[p+9]=c.m_burned;bytes[p+10]=c.m_destroyed;bytes[p+11]=c.m_modified;
            }
            using(var stream=new MemoryStream(cache.Length*40))
            {
                var w=new BinaryWriter(stream);
                foreach(var c in cache)
                {w.Write(c.m_tempOre);w.Write(c.m_tempOil);w.Write(c.m_tempForest);w.Write(c.m_tempFertility);w.Write(c.m_tempWater);w.Write(c.m_finalOre);w.Write(c.m_finalOil);w.Write(c.m_finalForest);w.Write(c.m_finalFertility);w.Write(c.m_finalWater);}
                w.Flush();return new ResourceState {Cells=bytes,Cache=stream.ToArray()};
            }
        }
        static Prepared Unpack(ResourceState state)
        {
            var cells=new NaturalResourceManager.ResourceCell[state.Cells.Length/12];
            for(int i=0;i<cells.Length;i++)
            {
                int p=i*12;var b=state.Cells;
                cells[i]=new NaturalResourceManager.ResourceCell {m_ore=b[p],m_oil=b[p+1],m_sand=b[p+2],m_forest=b[p+3],m_tree=b[p+4],m_fertility=b[p+5],m_pollution=b[p+6],m_water=b[p+7],m_shore=b[p+8],m_burned=b[p+9],m_destroyed=b[p+10],m_modified=b[p+11]};
            }
            var cache=new NaturalResourceManager.AreaCell[state.Cache.Length/40];
            using(var stream=new MemoryStream(state.Cache,false))
            {
                var r=new BinaryReader(stream);
                for(int i=0;i<cache.Length;i++)cache[i]=new NaturalResourceManager.AreaCell {m_tempOre=r.ReadUInt32(),m_tempOil=r.ReadUInt32(),m_tempForest=r.ReadUInt32(),m_tempFertility=r.ReadUInt32(),m_tempWater=r.ReadUInt32(),m_finalOre=r.ReadUInt32(),m_finalOil=r.ReadUInt32(),m_finalForest=r.ReadUInt32(),m_finalFertility=r.ReadUInt32(),m_finalWater=r.ReadUInt32()};
            }
            return new Prepared {Cells=cells,Cache=cache};
        }
    }
}

