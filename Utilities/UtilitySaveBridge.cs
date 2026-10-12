using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.SaveBridge;

namespace HundredTiles.Utilities
{
    public static class UtilitySaveBridge
    {
        internal static readonly string[] ElectricityFields={"m_electricityGrid","m_pulseGroups","m_pulseUnits","m_nodeGroups","m_pulseGroupCount","m_pulseUnitStart","m_pulseUnitEnd","m_processedCells","m_conductiveCells","m_canContinue"};
        internal static readonly string[] WaterFields={"m_waterGrid","m_waterPulseGroups","m_sewagePulseGroups","m_heatingPulseGroups","m_waterPulseUnits","m_sewagePulseUnits","m_heatingPulseUnits","m_nodeData","m_waterPulseGroupCount","m_waterPulseUnitStart","m_waterPulseUnitEnd","m_sewagePulseGroupCount","m_sewagePulseUnitStart","m_sewagePulseUnitEnd","m_heatingPulseGroupCount","m_heatingPulseUnitStart","m_heatingPulseUnitEnd","m_processedCells","m_conductiveCells","m_canContinue"};
        static SortedDictionary<string,object> saving;
        static ElectricityManager.Cell[] readElectricity;
        static WaterManager.Cell[] readWater;
        static bool electricityRead,waterRead,registered;
        public static void Register()
        {if(registered)return;RuntimeSaveBridge.RegisterParticipant(new Participant());registered=true;}
        static object Owner(string key) {return key.StartsWith("E.",StringComparison.Ordinal)?(object)Singleton<ElectricityManager>.instance:Singleton<WaterManager>.instance;}
        static FieldInfo Field(object owner,string name)
        {var field=owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(field==null)throw new MissingFieldException(owner.GetType().FullName,name);return field;}
        static object Get(string key) {var owner=Owner(key);return Field(owner,key.Substring(2)).GetValue(owner);}
        static Array ExpandedArray(string key)
        {
            switch(key)
            {
                case "E.m_pulseGroups":return ExpandedElectricityManager.PulseGroups;
                case "E.m_pulseUnits":return ExpandedElectricityManager.PulseUnits;
                case "W.m_waterPulseUnits":return ExpandedWaterManager.WaterPulseUnits;
                case "W.m_sewagePulseUnits":return ExpandedWaterManager.SewagePulseUnits;
                case "W.m_heatingPulseUnits":return ExpandedWaterManager.HeatingPulseUnits;
                default:return null;
            }
        }
        static SortedDictionary<string,object> CaptureState()
        {
            var result=new SortedDictionary<string,object>(StringComparer.Ordinal);
            foreach(string prefix in new[]{"E.","W."})foreach(string name in prefix=="E."?ElectricityFields:WaterFields)
            {
                string key=prefix+name;object value=ExpandedArray(key)??Get(key);
                var array=value as Array;result.Add(key,array==null?value:array.Clone());
            }
            return result;
        }
        // Copy common struct fields while translating only the two spatial axes.
        static Array ConvertAxes(Array original,Type element,bool toLegacy)
        {
            var result=Array.CreateInstance(element,original.Length);
            var sourceFields=original.GetType().GetElementType().GetFields();
            for(int i=0;i<result.Length;i++)
            {
                object source=original.GetValue(i),target=Activator.CreateInstance(element);
                foreach(var field in sourceFields)
                {
                    object value=field.GetValue(source);var destination=element.GetField(field.Name);
                    if(field.Name=="m_x" || field.Name=="m_z")
                    {
                        int coordinate=Convert.ToInt32(value);
                        value=toLegacy?(object)(byte)Math.Max(0,Math.Min(255,coordinate-UtilityGrid.Offset)):(ushort)(coordinate+UtilityGrid.Offset);
                    }
                    destination.SetValue(target,value);
                }
                result.SetValue(target,i);
            }
            return result;
        }
        internal static ElectricityManager.Cell[] Canonical(ElectricityManager.Cell[] cells)
        {
            var result=(ElectricityManager.Cell[])cells.Clone();
            for(int i=0;i<result.Length;i++)if(result[i].m_conductivity==0)result[i]=new ElectricityManager.Cell{m_pulseGroup=ushort.MaxValue};return result;
        }
        internal static WaterManager.Cell[] Canonical(WaterManager.Cell[] cells)
        {
            var result=(WaterManager.Cell[])cells.Clone();
            for(int i=0;i<result.Length;i++)
            {
                if(result[i].m_conductivity==0)
                {result[i].m_currentWaterPressure=result[i].m_currentSewagePressure=0;result[i].m_waterPulseGroup=result[i].m_sewagePulseGroup=ushort.MaxValue;result[i].m_closestPipeSegment=0;result[i].m_pollution=0;result[i].m_hasWater=result[i].m_hasSewage=result[i].m_tmpHasWater=result[i].m_tmpHasSewage=false;}
                if(result[i].m_conductivity2==0)
                {result[i].m_currentHeatingPressure=0;result[i].m_heatingPulseGroup=ushort.MaxValue;result[i].m_closestPipeSegment2=0;result[i].m_hasHeating=result[i].m_tmpHasHeating=false;}
            }
            return result;
        }
        static SortedDictionary<string,object> Project(SortedDictionary<string,object> full)
        {
            var result=new SortedDictionary<string,object>(full,StringComparer.Ordinal);
            result["E.m_electricityGrid"]=Canonical(UtilityGrid.Project((ElectricityManager.Cell[])full["E.m_electricityGrid"]));
            result["W.m_waterGrid"]=Canonical(UtilityGrid.Project((WaterManager.Cell[])full["W.m_waterGrid"]));
            foreach(var pair in full)if(ExpandedArray(pair.Key)!=null)result[pair.Key]=ConvertAxes((Array)pair.Value,Get(pair.Key).GetType().GetElementType(),true);
            return result;
        }
        static byte[] Baseline(ElectricityManager.Cell[] electricity,WaterManager.Cell[] water)
        {var state=new SortedDictionary<string,object>(StringComparer.Ordinal);state.Add("E",Canonical(electricity));state.Add("W",Canonical(water));return UtilitySnapshotCodec.Hash(UtilitySnapshotCodec.Payload(state));}
        public static T Freeze<T>(T original,string key)
        {if(saving==null || !saving.ContainsKey(key))throw new InvalidOperationException("Utility save snapshot missing.");return (T)saving[key];}
        public static ElectricityManager.Cell[] ReadElectricity(ElectricityManager.Cell[] original)
        {if(readElectricity==null)throw new InvalidOperationException("Utility load missing.");return readElectricity;}
        public static WaterManager.Cell[] ReadWater(WaterManager.Cell[] original)
        {if(readWater==null)throw new InvalidOperationException("Utility load missing.");return readWater;}
        public static void ElectricityRead()
        {if(readElectricity==null || electricityRead)throw new InvalidOperationException("Electricity Deserialize stage differs.");PublishBaseline("E.m_electricityGrid",UtilityGrid.Expand(readElectricity));electricityRead=true;}
        public static void WaterRead()
        {if(readWater==null || waterRead)throw new InvalidOperationException("Water Deserialize stage differs.");PublishBaseline("W.m_waterGrid",UtilityGrid.Expand(readWater));waterRead=true;}
        static void PublishBaseline(string key,Array source)
        {var target=(Array)Get(key);if(target.Length!=source.Length)throw new InvalidDataException("Utility target grid is not expanded.");Array.Copy(source,target,source.Length);}
        static SortedDictionary<string,object> LegacyState()
        {
            var state=CaptureState();
            foreach(var key in new[]{"E.m_pulseGroups","E.m_pulseUnits","W.m_waterPulseUnits","W.m_sewagePulseUnits","W.m_heatingPulseUnits"})
                state[key]=ConvertAxes((Array)Get(key),ExpandedArray(key).GetType().GetElementType(),false);
            return state;
        }
        internal static void Validate(SortedDictionary<string,object> state)
        {
            if(((Array)state["E.m_electricityGrid"]).Length!=UtilityGrid.Side*UtilityGrid.Side || ((Array)state["W.m_waterGrid"]).Length!=UtilityGrid.Side*UtilityGrid.Side)
                throw new InvalidDataException("Utility grids must cover the expanded map.");
            foreach(var pair in state)
            {
                var array=pair.Value as Array;
                if(array!=null)
                {var target=ExpandedArray(pair.Key)??(Array)Get(pair.Key);if(array.GetType()!=target.GetType() || array.Length!=target.Length)throw new InvalidDataException("Utility restore target differs: "+pair.Key);}
                else if(pair.Value is int)
                {
                    int value=(int)pair.Value;
                    if(value<0 || (pair.Key.EndsWith("GroupCount",StringComparison.Ordinal) && value>1024) ||
                        ((pair.Key.EndsWith("UnitStart",StringComparison.Ordinal) || pair.Key.EndsWith("UnitEnd",StringComparison.Ordinal)) && value>=32768))throw new InvalidDataException("Utility counter out of bounds: "+pair.Key);
                }
            }
            foreach(string key in new[]{"E.m_pulseGroups","E.m_pulseUnits","W.m_waterPulseUnits","W.m_sewagePulseUnits","W.m_heatingPulseUnits"})
            {
                var array=(Array)state[key];Type element=array.GetType().GetElementType();
                foreach(var value in array)if((ushort)element.GetField("m_x").GetValue(value)>=UtilityGrid.Side || (ushort)element.GetField("m_z").GetValue(value)>=UtilityGrid.Side)throw new InvalidDataException("Utility pulse coordinate out of bounds.");
            }
            foreach(string stem in new[]{"E.m_pulse","W.m_waterPulse","W.m_sewagePulse","W.m_heatingPulse"})
            {
                var units=(Array)state[stem+"Units"];Type element=units.GetType().GetElementType();
                int start=(int)state[stem+"UnitStart"],end=(int)state[stem+"UnitEnd"],groups=(int)state[stem+"GroupCount"];
                for(int i=start;i!=end;i=(i+1)%32768)
                {
                    object unit=units.GetValue(i);
                    if((ushort)element.GetField("m_group").GetValue(unit)>=groups || (ushort)element.GetField("m_node").GetValue(unit)>=32768)
                        throw new InvalidDataException("Active utility pulse references an invalid group or node.");
                }
            }
        }
        sealed class Participant : ISaveParticipant
        {
            public string Key {get{return "HundredTiles.Utilities.v1";}}
            public byte[] Capture(byte[] binding)
            {
                var full=CaptureState();Validate(full);saving=Project(full);
                return UtilitySnapshotCodec.Encode(full,Baseline((ElectricityManager.Cell[])saving["E.m_electricityGrid"],(WaterManager.Cell[])saving["W.m_waterGrid"]),binding);
            }
            public void BeginLoad()
            {readElectricity=new ElectricityManager.Cell[65536];readWater=new WaterManager.Cell[65536];electricityRead=waterRead=false;}
            public object Prepare(byte[] bytes,byte[] binding)
            {
                if(!electricityRead || !waterRead)throw new InvalidOperationException("Utility standard data incomplete.");
                var template=LegacyState();return bytes==null?template:UtilitySnapshotCodec.Decode(bytes,template,Baseline(readElectricity,readWater),binding);
            }
            public void ValidateTarget(object prepared) {Validate((SortedDictionary<string,object>)prepared);}
            public void Commit(object prepared)
            {
                foreach(var pair in (SortedDictionary<string,object>)prepared)
                {var array=pair.Value as Array;if(array!=null)Array.Copy(array,ExpandedArray(pair.Key)??(Array)Get(pair.Key),array.Length);else {object owner=Owner(pair.Key);Field(owner,pair.Key.Substring(2)).SetValue(owner,pair.Value);}}
                Singleton<ElectricityManager>.instance.AreaModified(0,0,UtilityGrid.Side-1,UtilityGrid.Side-1);
                Singleton<WaterManager>.instance.AreaModified(0,0,UtilityGrid.Side-1,UtilityGrid.Side-1);
            }
            public void ClearSave() {saving=null;}
            public void ClearLoad() {readElectricity=null;readWater=null;electricityRead=waterRead=false;}
        }
    }
}
