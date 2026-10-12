using System;
using System.IO;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

static class OuterObjects
{
    static string Manifest { get { return Path.Combine(Entry.Root,"outer-objects.txt"); } }
    public static void Create()
    {
        LiveGameplayChecks.PurchaseAll();
        var terrain=Singleton<TerrainManager>.instance;
        for(int z=1135;z<=1175;z++)for(int x=1135;x<=1175;x++)terrain.RawHeights[z*1201+x]=5120;
        TerrainModify.UpdateArea(9520f,9520f,10160f,10160f,true,true,true);
        var random=new Randomizer(17);
        uint tree;ushort prop,a,b,segment;byte district;
        if(!Singleton<TreeManager>.instance.CreateTree(out tree,ref random,PrefabCollection<TreeInfo>.GetLoaded(0),new Vector3(9800,80,9800),true))throw new Exception("Outer tree creation failed.");
        if(!Singleton<PropManager>.instance.CreateProp(out prop,ref random,PrefabCollection<PropInfo>.GetLoaded(0),new Vector3(9840,80,9800),0,true))throw new Exception("Outer prop creation failed.");
        var roads=Singleton<NetManager>.instance;var info=PrefabCollection<NetInfo>.FindLoaded("Basic Road");
        if(info==null)throw new Exception("Basic Road prefab missing.");
        if(!roads.CreateNode(out a,ref random,info,new Vector3(9800,80,9900),1) || !roads.CreateNode(out b,ref random,info,new Vector3(10000,80,9900),2))throw new Exception("Outer road node creation failed.");
        if(!roads.CreateSegment(out segment,ref random,info,a,b,Vector3.right,Vector3.left,3,3,false))throw new Exception("Outer road segment creation failed.");
        var districts=Singleton<DistrictManager>.instance;
        if(!districts.CreateDistrict(out district))throw new Exception("Outer district creation failed.");
        var cell=new DistrictManager.Cell { m_district1=district,m_alpha1=255 };
        for(int z=955;z<=975;z++)for(int x=955;x<=975;x++)districts.ModifyCell(x,z,cell);
        File.WriteAllText(Manifest,tree+"\n"+prop+"\n"+a+"\n"+b+"\n"+segment+"\n"+district);
        Check();Entry.Log("PASS: native road, tree, prop, district and raised terrain created in added corner.");
    }
    public static void Check()
    {
        var ids=File.ReadAllLines(Manifest);uint tree=uint.Parse(ids[0]);ushort prop=ushort.Parse(ids[1]),a=ushort.Parse(ids[2]),b=ushort.Parse(ids[3]),segment=ushort.Parse(ids[4]);byte district=byte.Parse(ids[5]);
        var trees=Singleton<TreeManager>.instance;var props=Singleton<PropManager>.instance;var roads=Singleton<NetManager>.instance;
        Position(trees.m_trees.m_buffer[tree].Position,new Vector3(9800,80,9800));
        Position(props.m_props.m_buffer[prop].Position,new Vector3(9840,80,9800));
        Position(roads.m_nodes.m_buffer[a].m_position,new Vector3(9800,80,9900));Position(roads.m_nodes.m_buffer[b].m_position,new Vector3(10000,80,9900));
        if(roads.m_segments.m_buffer[segment].m_startNode!=a || roads.m_segments.m_buffer[segment].m_endNode!=b)throw new Exception("Outer road identity failed.");
        if(Singleton<DistrictManager>.instance.GetDistrict(new Vector3(9800,80,9800))!=district)throw new Exception("Outer district lookup failed.");
        if(Singleton<TerrainManager>.instance.RawHeights[1150*1201+1150]!=5120 || Singleton<GameAreaManager>.instance.m_areaCount!=100)throw new Exception("Outer terrain/ownership restoration failed.");
        var terrain=Singleton<TerrainManager>.instance;
        terrain.GetZoneCell(4800,4800);terrain.GetSurfaceCell(4800,4800);
        terrain.GetZoneCell(-16,-16);terrain.GetSurfaceCell(-16,-16);
        Entry.Log("PASS: added-corner native object positions, road identities, district query, terrain and 100-area ownership verified.");
    }
    static void Position(Vector3 actual,Vector3 expected){if((actual-expected).sqrMagnitude>4)throw new Exception("Outer object position differs: "+actual+" expected "+expected);}
}
