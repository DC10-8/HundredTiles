using HundredTiles.Core;
namespace ColossalFramework {public static class Singleton<T> where T:new(){public static T instance=new T();}}
namespace HundredTiles.SaveBridge
{
    public interface ISaveParticipant
    {string Key{get;}byte[] Capture(byte[] binding);void BeginLoad();object Prepare(byte[] bytes,byte[] binding);void ValidateTarget(object prepared);void Commit(object prepared);void ClearSave();void ClearLoad();}
    public interface IChunkedSaveParticipant : ISaveParticipant {int EncodedSize{get;}}
    public interface IMigratingSaveParticipant : ISaveParticipant {bool CanReadSize(int size);}
    public static class RuntimeSaveBridge
    {public static ISaveParticipant Participant;public static void RegisterParticipant(ISaveParticipant participant){if(Participant!=null)throw new Exception("Duplicate registration");Participant=participant;}public static Exception LoadFailure(Exception exception)=>exception;}
    public class HookSpec {public System.Reflection.MethodBase Target;public System.Reflection.MethodInfo Prefix,Postfix,Finalizer,Transpiler;}
    public static class SerializerHooks {public static void Verify(System.Reflection.MethodBase method) {if(method==null)throw new Exception("Missing method");}}
}
public class ElectricityManager
{
    public class Data {public void Serialize(){}public void Deserialize(){}}
    public struct Cell {public short m_currentCharge;public ushort m_extraCharge,m_pulseGroup;public byte m_conductivity;public bool m_tmpElectrified,m_electrified;}
    public struct PulseGroup {public uint m_origCharge,m_curCharge;public ushort m_mergeIndex,m_mergeCount;public byte m_x,m_z;}
    public struct PulseUnit {public ushort m_group,m_node;public byte m_x,m_z;}
    public Cell[] m_electricityGrid=new Cell[UtilityGrid.Side*UtilityGrid.Side];
    public PulseGroup[] m_pulseGroups=new PulseGroup[1024];public PulseUnit[] m_pulseUnits=new PulseUnit[32768];public ushort[] m_nodeGroups=new ushort[32768];
    public int m_pulseGroupCount,m_pulseUnitStart,m_pulseUnitEnd,m_processedCells,m_conductiveCells;public bool m_canContinue;
    public int DirtyMax;public void AreaModified(int x1,int z1,int x2,int z2){DirtyMax=x2;}
}
public class WaterManager
{
    public class Data {public void Serialize(){}public void Deserialize(){}}
    public struct Cell
    {
        public short m_currentWaterPressure,m_currentSewagePressure,m_currentHeatingPressure;
        public ushort m_waterPulseGroup,m_sewagePulseGroup,m_heatingPulseGroup,m_closestPipeSegment,m_closestPipeSegment2;
        public byte m_conductivity,m_conductivity2,m_pollution;
        public bool m_tmpHasWater,m_tmpHasSewage,m_tmpHasHeating,m_hasWater,m_hasSewage,m_hasHeating;
    }
    public struct PulseGroup {public uint m_origPressure,m_curPressure,m_collectPressure;public ushort m_mergeIndex,m_mergeCount,m_node;}
    public struct PulseUnit {public ushort m_group,m_node;public byte m_x,m_z;}
    public struct Node
    {
        public ushort m_waterPulseGroup,m_sewagePulseGroup,m_heatingPulseGroup,m_curWaterPressure,m_curSewagePressure,m_curHeatingPressure,
            m_extraWaterPressure,m_extraSewagePressure,m_extraHeatingPressure,m_collectWaterPressure,m_collectSewagePressure;
        public byte m_pollution;
    }
    public Cell[] m_waterGrid=new Cell[UtilityGrid.Side*UtilityGrid.Side];public Node[] m_nodeData=new Node[32768];
    public PulseGroup[] m_waterPulseGroups=new PulseGroup[1024],m_sewagePulseGroups=new PulseGroup[1024],m_heatingPulseGroups=new PulseGroup[1024];
    public PulseUnit[] m_waterPulseUnits=new PulseUnit[32768],m_sewagePulseUnits=new PulseUnit[32768],m_heatingPulseUnits=new PulseUnit[32768];
    public int m_waterPulseGroupCount,m_waterPulseUnitStart,m_waterPulseUnitEnd,m_sewagePulseGroupCount,m_sewagePulseUnitStart,m_sewagePulseUnitEnd,m_heatingPulseGroupCount,m_heatingPulseUnitStart,m_heatingPulseUnitEnd,m_processedCells,m_conductiveCells;
    public bool m_canContinue;public int DirtyMax;public void AreaModified(int x1,int z1,int x2,int z2){DirtyMax=x2;}
}
namespace HundredTiles.Utilities
{
    internal static class ExpandedElectricityManager
    {
        internal static void EnsureCapacity(int count){if(PulseUnits.Length!=count)Array.Resize(ref PulseUnits,count);}
        internal struct ExpandedPulseGroup {public uint m_origCharge,m_curCharge;public ushort m_mergeIndex,m_mergeCount,m_x,m_z;}
        internal struct ExpandedPulseUnit {public ushort m_group,m_node,m_x,m_z;}
        internal static ExpandedPulseGroup[] PulseGroups=new ExpandedPulseGroup[1024];
        internal static ExpandedPulseUnit[] PulseUnits=new ExpandedPulseUnit[32768];
    }
    internal static class ExpandedWaterManager
    {
        internal static void EnsureCapacity(int count){if(WaterPulseUnits.Length!=count){Array.Resize(ref WaterPulseUnits,count);Array.Resize(ref SewagePulseUnits,count);Array.Resize(ref HeatingPulseUnits,count);}}
        internal struct ExpandedPulseUnit {public ushort m_group,m_node,m_x,m_z;}
        internal static ExpandedPulseUnit[] WaterPulseUnits=new ExpandedPulseUnit[32768],SewagePulseUnits=new ExpandedPulseUnit[32768],HeatingPulseUnits=new ExpandedPulseUnit[32768];
    }
}
