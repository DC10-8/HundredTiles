namespace ColossalFramework {public static class Singleton<T> where T:new(){public static T instance=new T();}}
namespace HundredTiles.SaveBridge {public class HookSpec {public System.Reflection.MethodBase Target;public System.Reflection.MethodInfo Prefix;} }
namespace UnityEngine
{
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
 public static class Mathf {public static float Max(float x,float y)=>Math.Max(x,y);public static int Min(int x,int y)=>Math.Min(x,y);public static int Clamp(int value,int min,int max)=>Math.Clamp(value,min,max);public static int FloorToInt(float value)=>(int)Math.Floor(value);}
}
public class SimulationManager {public uint m_currentFrameIndex;}
public enum StatisticType {ImmaterialResource}
public class StatisticInt32 {public int Value;public void Set(int value){Value=value;}}
public class StatisticArray {public readonly StatisticInt32[] Values=Enumerable.Range(0,35).Select(_=>new StatisticInt32()).ToArray();public T Acquire<T>(int i,int count)where T:class{return Values[i] as T;}}
public class StatisticsManager {public readonly StatisticArray Array=new StatisticArray();public T Acquire<T>(StatisticType type)where T:class{return Array as T;}}
public class DistrictManager
{
 public struct Park {public UnityEngine.Vector3 m_nameLocation;}
 public class Parks {public Park[] m_buffer=new Park[128];}
 public Parks m_parks=new Parks();
 public byte GetPark(UnityEngine.Vector3 pos)
 {int x=(int)Math.Floor(pos.x/38.4+225),z=(int)Math.Floor(pos.z/38.4+225);return (byte)(x>=450 && x<460 && z>=450 && z<460?1:0);}
}
public class ImmaterialResourceManager
{
 public const int RESOURCE_COUNT=35;public const float RESOURCEGRID_CELL_SIZE=38.4f;
 public struct ParkAreaIndex {public int m_index,m_cost;}
 public static byte[] Visited=new byte[500*500];public static int TotalCalls;
 public readonly List<(int,int,int,int)> Dirty=new();
 public void AreaModified(int x1,int z1,int x2,int z2){Dirty.Add((x1,z1,x2,z2));}
 private static bool CalculateLocalResources(int x,int z,ushort[] buffer,int[] global,ushort[] target,int index)
 {Visited[z*500+x]++;for(int i=0;i<35;i++)target[index+i]=(ushort)(buffer[index+i]+global[i]);target[index+16]=1;return true;}
 private static void CalculateTotalResources(int[] buffer,long[] mul,int[] target){TotalCalls++;Array.Copy(buffer,target,35);}
 private void SimulationStepImpl(int subStep){}
 public ParkAreaIndex[] GetParkAreaResourceIndexes(byte park,float radius)=>throw new Exception("Vanilla park queue must not run");
}
