using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
namespace HundredTiles.Utilities
{
    internal static class UtilityCapacity
    {
        internal static readonly string[] Keys={"E.m_nodeGroups","W.m_nodeData","E.m_pulseUnits","W.m_waterPulseUnits","W.m_sewagePulseUnits","W.m_heatingPulseUnits"};
        internal static bool Known(int count){return count==32768 || count==65024;}
        internal static SortedDictionary<string,object> Shape(SortedDictionary<string,object> original,int[] counts)
        {
            var result=new SortedDictionary<string,object>(original,StringComparer.Ordinal);
            for(int i=0;i<Keys.Length;i++){if(!Known(counts[i]))throw new InvalidDataException("Unsupported utility capacity.");var array=(Array)original[Keys[i]];if(array.Length!=counts[i])result[Keys[i]]=Array.CreateInstance(array.GetType().GetElementType(),counts[i]);}
            return result;
        }
        internal static Array Grow(Array original,int count,string key)
        {
            if(!Known(count) || !Known(original.Length) || original.Length>count)throw new InvalidDataException("Utility capacity cannot shrink; keep the network capacity mod enabled.");
            var result=Array.CreateInstance(original.GetType().GetElementType(),count);
            if(key=="E.m_nodeGroups")for(int i=0;i<count;i++)result.SetValue(ushort.MaxValue,i);
            if(key=="W.m_nodeData")
            {
                var type=original.GetType().GetElementType();object empty=Activator.CreateInstance(type);
                foreach(string name in new[]{"m_waterPulseGroup","m_sewagePulseGroup","m_heatingPulseGroup"})type.GetField(name).SetValue(empty,ushort.MaxValue);
                for(int i=0;i<count;i++)result.SetValue(empty,i);
            }
            Array.Copy(original,result,original.Length);return result;
        }
        internal static SortedDictionary<string,object> Adapt(SortedDictionary<string,object> saved,SortedDictionary<string,object> target)
        {
            var result=new SortedDictionary<string,object>(saved,StringComparer.Ordinal);
            foreach(string key in Keys)
            {
                var original=(Array)saved[key];int count=((Array)target[key]).Length;
                if(original.Length==count)continue;
                if(original.Length>count)throw new InvalidDataException("Utility capacity cannot shrink; keep the network capacity mod enabled.");
                if(key=="E.m_nodeGroups" || key=="W.m_nodeData"){result[key]=Grow(original,count,key);continue;}
                string stem=key.Substring(0,key.Length-5);int start=(int)saved[stem+"UnitStart"],end=(int)saved[stem+"UnitEnd"];
                int used=(end-start+original.Length)%original.Length;
                var next=Array.CreateInstance(original.GetType().GetElementType(),count);
                for(int i=0;i<used;i++)next.SetValue(original.GetValue((start+i)%original.Length),i);
                result[key]=next;result[stem+"UnitStart"]=0;result[stem+"UnitEnd"]=used;
            }
            return result;
        }
    }
}
