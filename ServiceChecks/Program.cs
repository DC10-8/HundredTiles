using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.Utilities;

var manager=Singleton<ImmaterialResourceManager>.instance;
var simulation=Singleton<SimulationManager>.instance;
var temp=new ushort[ServiceGrid.Values];var final=new ushort[ServiceGrid.Values];
var globalTemp=Enumerable.Range(0,35).Select(i=>100+i).ToArray();var globalFinal=new int[35];var totalTemp=new int[35];var totalFinal=new int[35];var multipliers=new long[35];
for(int i=0;i<temp.Length;i++)temp[i]=(ushort)(i%35+1);
void Step(int subStep){Check(!ServiceHooks.Step(manager,subStep,temp,final,globalTemp,globalFinal,totalTemp,totalFinal,multipliers),"Original simulation suppressed");}
Step(0);Step(1000);Check(ImmaterialResourceManager.Visited.All(v=>v==0),"Zero/editor substeps do not consume buffers");
for(uint frame=0;frame<256;frame++)
{
 simulation.m_currentFrameIndex=frame;Step(1);
 Check(ImmaterialResourceManager.TotalCalls==(frame==255?1:0),"Statistics finalize only after all rows");
}
int side=ServiceGrid.Side,cells=side*side;
Check(ImmaterialResourceManager.Visited.All(v=>v==1),"Every cell processed exactly once in a 256-frame cycle");
for(int channel=0;channel<35;channel++)
{Check(totalFinal[channel]==cells*(channel==16?1:channel+1),"Whole-map resource statistics include outer cells");Check(globalFinal[channel]==100+channel && globalTemp[channel]==0 && totalTemp[channel]==0 && multipliers[channel]==0,"Global handoff and next-cycle totals reset");}
Check(temp.All(v=>v==0) && manager.Dirty.Count==side && manager.Dirty.Last()==(0,side-1,side-1,side-1),"Every processed row consumes temporaries and marks its exact bounds");
var parks=Singleton<DistrictManager>.instance;
parks.m_parks.m_buffer[1].m_nameLocation=new UnityEngine.Vector3((454-225+.5f)*38.4f,0,(454-225+.5f)*38.4f);
Check(!ExpandedServices.ParkIndexes(out var indexes,1,80f),"Vanilla byte-coordinate park queue suppressed");
Check(indexes.Length>=100 && indexes.All(i=>i.m_index>=0 && i.m_index<ServiceGrid.Values && i.m_index%35==0),"Expanded park traversal stays within resource channels");
for(int z=450;z<460;z++)for(int x=450;x<460;x++)Check(indexes.Any(i=>i.m_index==(z*side+x)*35 && i.m_cost==0),"All park cells beyond byte coordinate limit retain zero distance");
parks.m_parks.m_buffer[1].m_nameLocation=new UnityEngine.Vector3((side-1-225+.5f)*38.4f,0,(side-1-225+.5f)*38.4f);
ExpandedServices.ParkIndexes(out indexes,1,100f);Check(indexes.All(i=>i.m_index>=0 && i.m_index<ServiceGrid.Values),"Park traversal at the new map corner cannot wrap axes");
Console.WriteLine("PASS: production service simulation prefix executes full 256-frame cycle; "+cells+" unique cells; 35 totals; final-frame statistics; frozen-global handoff; exact dirty rows; ushort park queue beyond 255 and at the outer corner. Game formulas and managers are fixtures, no Unity.");
static void Check(bool value,string name){if(!value)throw new Exception(name);}
