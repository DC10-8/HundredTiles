using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

// A generated load fixture, not a replacement for a player's mature city.
static class LiveLargeCityChecks
{
    public static int Target
    {
        get{string value=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_POPULATION");int target=string.IsNullOrEmpty(value)?1000:int.Parse(value);if(target<1000 || target>650000)throw new Exception("Population fixture target must be 1000..650000.");return target;}
    }
    public static string Source {get{return "HundredTiles_100_Population_"+Target;}}
    public static string ShortSaveName {get{return "HundredTiles_"+HundredTiles.Core.ExpandedMap.TileCount+"_Population_"+Target;}}
    public static string SaveName {get{return ShortSaveName+(LivePopulationRunChecks.Enabled?"_LongRun":"");}}
    public static string Record {get{return "population-"+HundredTiles.Core.ExpandedMap.TileCount+"-"+Target+(LivePopulationRunChecks.Enabled?"-long":"")+"-saved.marker";}}
    static string Manifest {get{return Path.Combine(Entry.Root,"population-"+Target+"-homes.txt");}}
    public static void Prepare(){LiveTerrainToolChecks.Prepare();}
    public static void Create()
    {
        if(HundredTiles.Core.MapLayout.Active.TileCount!=100)throw new Exception("Population fixture must originate in native 100 profile.");
        if(!File.Exists(Path.Combine(Entry.Root,"isolated-test.marker")))throw new Exception("Population generation requires the isolated fixture.");
        LiveConsumerChecks.PrepareUnattendedProgression();
        var buildings=Singleton<BuildingManager>.instance;var citizens=Singleton<CitizenManager>.instance;var graph=Singleton<NetManager>.instance;
        BuildingInfo prefab=null;
        for(uint i=0;i<PrefabCollection<BuildingInfo>.LoadedCount();i++)
        {
            var info=PrefabCollection<BuildingInfo>.GetLoaded(i);
            if(info!=null && info.m_buildingAI is ResidentialBuildingAI && info.m_class.m_subService==ItemClass.SubService.ResidentialHigh && info.m_class.m_level==ItemClass.Level.Level5 && info.m_cellWidth==4 && info.m_cellLength==4 && info.m_zoningMode==BuildingInfo.ZoningMode.Straight){prefab=info;break;}
        }
        if(prefab==null)throw new Exception("Native level-five 4x4 high-density residential prefab missing.");
        int existing=Residents(),remaining=Target-existing;if(remaining<=0)throw new Exception("Fixture already meets target; refusing duplicate generation.");
        int rows=(remaining+4999)/5000;var random=new Randomizer(650000);var road=PrefabCollection<NetInfo>.FindLoaded("Basic Road");
        // Keep the original edge's outside connections clear of the leveling
        // brush. Their native ground-height recalculation must not change them
        // between the reference snapshot and the subsequent load.
        int lastBand=(rows*64)/1000;
        for(int i=1;i<buildings.m_buildings.m_buffer.Length;i++)
        {
            var data=buildings.m_buildings.m_buffer[i];
            if((data.m_flags&Building.Flags.Created)!=0 && data.m_position.x>=8688 && data.m_position.x<=10512 && data.m_position.z>=-312 && data.m_position.z<=1512+lastBand*1000)throw new Exception("Population fixture leveling would affect an existing building: "+i);
        }
        for(int band=0;band<=lastBand;band++)LiveTerrainToolChecks.LevelArea(new Vector3(9600,80,600+band*1000),1760,80);
        var zoneFlags=NetTool.m_zoneGridFlags;NetTool.m_zoneGridFlags=NetSegment.Flags2.ZoneLeft|NetSegment.Flags2.ZoneRight;
        var blocks=new HashSet<ushort>();ushort previous=0;
        try
        {
            for(int row=0;row<rows;row++)
            {
                ushort start=Node(graph,road,ref random,new Vector3(8800,80,row*64));
                if(previous!=0)Segment(graph,road,ref random,previous,start,blocks);previous=start;
                ushort current=start;
                for(int offset=128;offset<1640;offset+=128){ushort next=Node(graph,road,ref random,new Vector3(8800+offset,80,row*64));Segment(graph,road,ref random,current,next,blocks);current=next;}
                ushort end=Node(graph,road,ref random,new Vector3(10440,80,row*64));Segment(graph,road,ref random,current,end,blocks);
            }
            var grid=Singleton<ZoneManager>.instance.m_blocks.m_buffer;
            foreach(ushort id in blocks)grid[id].CalculateBlock1(id);
            foreach(ushort id in blocks)grid[id].CalculateBlock2(id);
            foreach(ushort id in blocks){grid[id].CalculateBlock3(id);for(int z=0;z<grid[id].RowCount;z++)for(int x=0;x<4;x++)grid[id].SetZone(x,z,ItemClass.Zone.ResidentialHigh);}
            var homes=new List<string>();int allocated=0;
            for(int row=0;row<rows && remaining>0;row++)for(int col=0;col<40 && remaining>0;col++)
            {
                ushort home;var position=new Vector3(8840+col*40,80,row*64+24);
                if(!buildings.CreateBuilding(out home,ref random,prefab,position,Mathf.PI,4,Singleton<SimulationManager>.instance.m_currentBuildIndex++))throw new Exception("Large fixture building capacity exhausted.");
                if(!buildings.m_buildings.m_buffer[home].CheckZoning(ItemClass.Zone.ResidentialHigh,ItemClass.Zone.None,false))throw new Exception("Large fixture has invalid zoning at "+position);
                var frame=buildings.m_buildings.m_buffer[home].GetLastFrameData();frame.m_constructState=255;buildings.m_buildings.m_buffer[home].SetFrameData(Singleton<SimulationManager>.instance.m_currentFrameIndex,frame);
                buildings.m_buildings.m_buffer[home].Info.m_buildingAI.SimulationStep(home,ref buildings.m_buildings.m_buffer[home]);
                int housed=0;uint unit=buildings.m_buildings.m_buffer[home].m_citizenUnits;
                for(int visited=0;unit!=0 && remaining>0 && visited<524288;visited++,unit=citizens.m_units.m_buffer[unit].m_nextUnit)
                    if((citizens.m_units.m_buffer[unit].m_flags&CitizenUnit.Flags.Home)!=0)for(int member=0;member<5 && remaining>0;member++)
                    {
                        // Native age values are simulation ticks, not years.
                        // Include two adults, a young adult, a senior and a child.
                        int age=member==0?100:member==1?120:member==2?65:member==3?190:10;
                        uint id;if(!citizens.CreateCitizen(out id,age,(row*40+col)%256,ref random))throw new Exception("Large fixture citizen capacity exhausted.");
                        citizens.m_citizens.m_buffer[id].SetHome(id,home,unit);
                        if(citizens.m_citizens.m_buffer[id].m_homeBuilding!=home)throw new Exception("Large fixture native home assignment failed.");
                        remaining--;allocated++;housed++;
                    }
                homes.Add(home+"\t"+housed);
                if(homes.Count%100==0)Entry.Log("Population fixture generation: homes="+homes.Count+", native residents added="+allocated+", remaining="+remaining);
            }
            if(remaining!=0 || Residents()!=Target)throw new Exception("Large native population census differs.");
            File.WriteAllLines(Manifest,homes.ToArray());TerrainModify.RefreshAllModifications();
            // Bulk manager-level construction queues the ordinary terrain-height
            // recalculation. Complete those pending updates before taking the
            // reference, rather than comparing forced input heights with the
            // native positions calculated during the next load.
            typeof(BuildingManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(buildings,new object[]{0});
            TerrainModify.RefreshAllModifications();
            for(int i=1;i<buildings.m_buildings.m_buffer.Length;i++)if((buildings.m_buildings.m_buffer[i].m_flags&Building.Flags.Created)!=0)buildings.m_buildings.m_buffer[i].CalculateBuilding((ushort)i);
            Entry.Log("FIXTURE: pending native building updates and final native ground-height calculations completed before recording the population reference.");
            File.WriteAllText(Path.Combine(Entry.Root,"population-"+Target+"-structure.sha256"),StructureHash(Path.Combine(Entry.Root,"population-"+Target+"-expected-structure.bin")));
            Entry.Log("PASS: generated native 100-tile population fixture: target="+Target+", existing="+existing+", added="+allocated+", homes="+homes.Count+", prefab="+prefab.name+". No population counter was assigned. City-wide services and traffic are not asserted.");
        }
        finally{NetTool.m_zoneGridFlags=zoneFlags;}
    }
    static ushort Node(NetManager manager,NetInfo info,ref Randomizer random,Vector3 position)
    {ushort id;if(!manager.CreateNode(out id,ref random,info,position,Singleton<SimulationManager>.instance.m_currentBuildIndex++))throw new Exception("Large fixture node capacity exhausted.");return id;}
    static void Segment(NetManager manager,NetInfo info,ref Randomizer random,ushort a,ushort b,HashSet<ushort> blocks)
    {
        ushort id;var direction=(manager.m_nodes.m_buffer[b].m_position-manager.m_nodes.m_buffer[a].m_position).normalized;
        if(!manager.CreateSegment(out id,ref random,info,a,b,direction,-direction,Singleton<SimulationManager>.instance.m_currentBuildIndex++,Singleton<SimulationManager>.instance.m_currentBuildIndex,false))throw new Exception("Large fixture segment capacity exhausted.");
        var segment=manager.m_segments.m_buffer[id];foreach(ushort block in new[]{segment.m_blockStartLeft,segment.m_blockStartRight,segment.m_blockEndLeft,segment.m_blockEndRight})if(block!=0)blocks.Add(block);
    }
    public static void Check(bool original=false)
    {
        var buildings=Singleton<BuildingManager>.instance;var citizens=Singleton<CitizenManager>.instance;int generated=0;
        bool longState=LivePopulationRunChecks.Enabled && !original;
        string prefix="population-"+Target+(longState?"-long":"");
        foreach(string line in File.ReadAllLines(longState?Path.Combine(Entry.Root,prefix+"-homes.txt"):Manifest))
        {
            var fields=line.Split('\t');ushort home=ushort.Parse(fields[0]);int expected=int.Parse(fields[1]);var data=buildings.m_buildings.m_buffer[home];
            if((data.m_flags&Building.Flags.Created)==0 || !(data.Info.m_buildingAI is ResidentialBuildingAI) || !buildings.m_buildings.m_buffer[home].CheckZoning(ItemClass.Zone.ResidentialHigh,ItemClass.Zone.None,false))throw new Exception("Large saved home or zone was lost: "+home);
            int actual=0;uint unit=data.m_citizenUnits;
            for(int visited=0;unit!=0 && visited<524288;visited++,unit=citizens.m_units.m_buffer[unit].m_nextUnit)for(int member=0;member<5;member++){uint id=citizens.m_units.m_buffer[unit].GetCitizen(member);if(id!=0 && citizens.m_citizens.m_buffer[id].m_homeBuilding==home)actual++;}
            if(actual!=expected)throw new Exception("Large saved household count differs: "+home);generated+=actual;
        }
        int expectedResidents=longState?int.Parse(File.ReadAllText(Path.Combine(Entry.Root,prefix+"-census.txt"))):Target;
        int residents=Residents();if(residents!=expectedResidents)throw new Exception("Native population changed across save/migration: "+residents+" / "+expectedResidents);
        string expectedStructure=File.ReadAllText(Path.Combine(Entry.Root,prefix+"-structure.sha256")).Trim();
        string actualStructure=StructureHash(Path.Combine(Entry.Root,prefix+"-actual-structure.bin"));
        if(actualStructure!=expectedStructure)throw new Exception("Native city building/network/home-binding structure changed across save/migration: expected="+expectedStructure+", actual="+actualStructure);
        Entry.Log("PASS: native saved population fixture homes, zoning, exact resident census and city-wide building/network/home-binding fingerprint preserved: generated cohort="+generated+", native resident census="+residents+", active tiles="+HundredTiles.Core.ExpandedMap.TileCount+". Healthy long-duration city operation is not asserted.");
    }
    public static void CaptureLongState()
    {
        var buildings=Singleton<BuildingManager>.instance;var citizens=Singleton<CitizenManager>.instance;
        typeof(BuildingManager).GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(buildings,new object[]{0});TerrainModify.RefreshAllModifications();
        for(int i=1;i<buildings.m_buildings.m_buffer.Length;i++)if((buildings.m_buildings.m_buffer[i].m_flags&Building.Flags.Created)!=0)buildings.m_buildings.m_buffer[i].CalculateBuilding((ushort)i);
        var records=new List<string>();int active=0;
        foreach(string line in File.ReadAllLines(Manifest))
        {
            ushort id=ushort.Parse(line.Split('\t')[0]);var data=buildings.m_buildings.m_buffer[id];
            if((data.m_flags&Building.Flags.Created)==0)throw new Exception("Population benchmark lost an original residential building: "+id);
            if((data.m_flags&(Building.Flags.Active|Building.Flags.Abandoned|Building.Flags.Collapsed))==Building.Flags.Active)active++;
            int count=0;uint unit=data.m_citizenUnits;
            for(int visited=0;unit!=0 && visited<524288;visited++,unit=citizens.m_units.m_buffer[unit].m_nextUnit)for(int member=0;member<5;member++){uint citizen=citizens.m_units.m_buffer[unit].GetCitizen(member);if(citizen!=0 && citizens.m_citizens.m_buffer[citizen].m_homeBuilding==id)count++;}
            records.Add(id+"\t"+count);
        }
        int residents=Residents();
        Entry.Log("Population long-run end: native residents="+residents+", active original homes="+active+" / "+records.Count);
        if(residents<Target*.9 || active<records.Count*.95)throw new Exception("Generated load fixture did not retain a representative population/active housing; healthy long-duration operation cannot be asserted.");
        string prefix=Path.Combine(Entry.Root,"population-"+Target+"-long");
        File.WriteAllLines(prefix+"-homes.txt",records.ToArray());File.WriteAllText(prefix+"-census.txt",residents.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(prefix+"-structure.sha256",StructureHash(prefix+"-expected-structure.bin"));
        Entry.Log("Captured post-operation native reference for a new long-run save; births, deaths and legitimate resident moves are retained rather than overwritten.");
    }
    public static string HousingDiagnostics()
    {
        var buildings=Singleton<BuildingManager>.instance.m_buildings.m_buffer;
        var problems=new Dictionary<string,int>();int active=0,abandoned=0,invalidZone=0;
        foreach(string line in File.ReadAllLines(Manifest))
        {
            ushort id=ushort.Parse(line.Split('\t')[0]);var data=buildings[id];
            if((data.m_flags&Building.Flags.Active)!=0)active++;
            if((data.m_flags&Building.Flags.Abandoned)!=0)abandoned++;
            if(!buildings[id].CheckZoning(ItemClass.Zone.ResidentialHigh,ItemClass.Zone.None,false))invalidZone++;
            string key=data.m_problems.ToString();int count;problems.TryGetValue(key,out count);problems[key]=count+1;
        }
        var entries=new List<string>();foreach(var item in problems)entries.Add(item.Key+"="+item.Value);
        return "active="+active+", abandoned="+abandoned+", invalid zoning="+invalidZone+", problems: "+string.Join("; ",entries.ToArray());
    }
    static string StructureHash(string export=null)
    {
        using(var bytes=new MemoryStream())
        using(var writer=new BinaryWriter(bytes))
        {
            var buildings=Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            for(int i=1;i<buildings.Length;i++)if((buildings[i].m_flags&Building.Flags.Created)!=0)
            {writer.Write((byte)1);writer.Write(i);writer.Write(buildings[i].Info.name);Position(writer,buildings[i].m_position);writer.Write(buildings[i].m_angle);}
            var graph=Singleton<NetManager>.instance;var nodes=graph.m_nodes.m_buffer;var segments=graph.m_segments.m_buffer;
            for(int i=1;i<nodes.Length;i++)if((nodes[i].m_flags&NetNode.Flags.Created)!=0)
            {writer.Write((byte)2);writer.Write(i);writer.Write(nodes[i].Info.name);Position(writer,nodes[i].m_position);var links=new ushort[8];for(int j=0;j<8;j++)links[j]=nodes[i].GetSegment(j);Array.Sort(links);foreach(ushort link in links)writer.Write(link);}
            for(int i=1;i<segments.Length;i++)if((segments[i].m_flags&NetSegment.Flags.Created)!=0)
            {writer.Write((byte)3);writer.Write(i);writer.Write(segments[i].Info.name);writer.Write(segments[i].m_startNode);writer.Write(segments[i].m_endNode);}
            var citizens=Singleton<CitizenManager>.instance.m_citizens.m_buffer;
            for(int i=1;i<citizens.Length;i++)if((citizens[i].m_flags&(Citizen.Flags.Created|Citizen.Flags.DummyTraffic))==Citizen.Flags.Created && citizens[i].m_homeBuilding!=0)
            {writer.Write((byte)4);writer.Write(i);writer.Write(citizens[i].m_homeBuilding);}
            writer.Flush();if(export!=null)File.WriteAllBytes(export,bytes.ToArray());bytes.Position=0;using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","");
        }
    }
    static void Position(BinaryWriter writer,Vector3 value){writer.Write(value.x);writer.Write(value.y);writer.Write(value.z);}
    public static int Residents()
    {int count=0;var citizens=Singleton<CitizenManager>.instance.m_citizens.m_buffer;var buildings=Singleton<BuildingManager>.instance.m_buildings.m_buffer;for(int i=1;i<citizens.Length;i++)if((citizens[i].m_flags&(Citizen.Flags.Created|Citizen.Flags.Dead|Citizen.Flags.DummyTraffic))==Citizen.Flags.Created && citizens[i].m_homeBuilding!=0 && (buildings[citizens[i].m_homeBuilding].m_flags&Building.Flags.Created)!=0)count++;return count;}
}
