// Test-only stand-ins. No game assembly is loaded or patched by these tests.
#pragma warning disable CS0414, CS0169 // fields are accessed through the production bridge's reflection
using HundredTiles.Core;
namespace ColossalFramework
{
    public static class Singleton<T> where T:new() { public static readonly T instance=new T(); }
}
namespace HundredTiles.SaveBridge
{
    public sealed class HookSpec {public System.Reflection.MethodBase Target;public System.Reflection.MethodInfo Prefix;}
}
public sealed class SimulationManager
{
    public sealed class Data { }
    public uint m_currentFrameIndex=128;
    public Dictionary<string,byte[]> m_serializableDataStorage=new Dictionary<string,byte[]>();
}
public sealed class GameAreaManager
{
    public int[] m_areaGrid=new int[100];
    public int m_areaCount=1,m_maxAreaCount=100;
    int m_startTile=44;
    bool m_areasUpdated;
    public GameAreaManager() {m_areaGrid[44]=1;}
    public int Start {get=>m_startTile;set=>m_startTile=value;}
    public int MaxAreaCount=>m_maxAreaCount;
    public int GetArea(int x,int z)=>0;
    public bool CanUnlock(int x,int z)=>false;
    public AreasWrapper m_AreasWrapper=new AreasWrapper();
}
public sealed class UnlockManager
{
    public bool Enabled=true;
    public bool Unlocked(int count)=>Enabled;
}
public sealed class AreasWrapper
{
    public int Calls;
    public bool Veto;
    public void OnCanUnlockArea(int x,int z,ref bool result) {Calls++; if(Veto)result=false;}
}
public sealed class TerrainManager
{
    public ushort[] RawHeights {get;}=new ushort[1201*1201];
    public ushort[] BlockHeights {get;}=new ushort[1201*1201];
    public ushort[] BlockHeightTargets {get;}=new ushort[1201*1201];
    public WaterSimulation WaterSimulation {get;}=new WaterSimulation();
    public int FlatnessRebuilds;
    public TerrainManager() { WaterSimulation.Initialize(BlockHeights); }
    void RefreshPatchFlatness() { FlatnessRebuilds++; }
}
public sealed class NaturalResourceManager
{
    public struct AreaCell {public uint m_tempOre,m_tempOil,m_tempForest,m_tempFertility,m_tempWater,m_finalOre,m_finalOil,m_finalForest,m_finalFertility,m_finalWater;}
    public struct ResourceCell {public byte m_ore,m_oil,m_sand,m_forest,m_tree,m_fertility,m_pollution,m_water,m_shore,m_burned,m_destroyed,m_modified;}
    public ResourceCell[] m_naturalResources=new ResourceCell[ResourceGrid.Cells];
    public AreaCell[] m_areaResources=new AreaCell[100];
    public void AreaModified(int x,int z,int maxX,int maxZ) {}
    public void AreaModifiedB(int x,int z,int maxX,int maxZ) {}
}
public sealed class WaterSimulation
{
    public enum Status {None,Running,WaitingForLoad,ReadyForLoad,Terminated}
    public struct Cell {public ushort m_height,m_pollution;public short m_velocityX,m_velocityZ;}
    object m_simulationBufferLock=new object();
    Status m_simulationStatus=Status.Running;
    uint m_simulationFrameIndex=128,m_waterFrameIndex=128,m_waterFrameIndex2=128;
    int m_readLockCount,m_bitCells=19;
    ushort[] m_heightBuffer=Array.Empty<ushort>();
    Cell[][] m_waterBuffers=new[]{new Cell[1201*1201],new Cell[1201*1201]};
    ulong[] m_waterExists1=new ulong[64],m_waterExists2=new ulong[64],m_waterExists3=new ulong[64];
    public void Initialize(ushort[] heights) {m_heightBuffer=heights;}
    public Cell[] BeginRead() {lock(m_simulationBufferLock) {m_readLockCount++;return m_waterBuffers[~(m_waterFrameIndex>>6)&1];}}
    public void EndRead() {lock(m_simulationBufferLock) {m_readLockCount--;}}
    public void SetStatus(Status value) {m_simulationStatus=value;}
    public Cell[][] Buffers=>m_waterBuffers;
    public ulong[] Mask=>m_waterExists1;
    public ushort[] HeightPointer=>m_heightBuffer;
    public int Readers=>m_readLockCount;
    public uint Frame=>m_waterFrameIndex;
}
