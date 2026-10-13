using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using ColossalFramework;
using HarmonyLib;
using ICities;

static class LiveIptCacheChecks
{
    sealed class Storage:ISerializableData
    {
        readonly Dictionary<string,byte[]> values=new Dictionary<string,byte[]>();
        public IManagers managers {get{return null;}}
        public string[] EnumerateData(){var keys=new List<string>(values.Keys);return keys.ToArray();}
        public byte[] LoadData(string id){byte[] value;return values.TryGetValue(id,out value)?value:null;}
        public void SaveData(string id,byte[] data){values[id]=data;}
        public void EraseData(string id){values.Remove(id);}
        public bool SaveGame(string name){throw new NotSupportedException();}
        public bool LoadGame(string name){throw new NotSupportedException();}
    }
    public static void Run()
    {
        foreach(string name in new[]{"ipt3-fixed.dll","ipt4-fixed.dll"})
        {
            var assembly=Assembly.Load(File.ReadAllBytes(Path.Combine(Path.Combine(Entry.Root,"ipt-checks"),name)));
            var cache=assembly.GetType("ImprovedPublicTransport.Data.CachedNodeData",true);
            var serializer=assembly.GetType("ImprovedPublicTransport.Data.SerializableDataExtension",true);
            var instanceField=serializer.GetField("instance");var previousInstance=instanceField.GetValue(null);
            var field=cache.GetField("m_cachedNodeData");var previousCache=field.GetValue(null);
            var element=field.FieldType.GetElementType();var storage=new Storage();
            var bridge=Activator.CreateInstance(serializer);serializer.GetMethod("OnCreated").Invoke(bridge,new object[]{storage});
            var harmony=new Harmony("HundredTiles.IPTCacheFixture");
            try
            {
                var arguments=new object[]{null};cache.GetMethod("TryLoadData").Invoke(null,arguments);
                var nodes=(Array)arguments[0];if(nodes.Length!=65536)throw new Exception("IPT default allocation size incorrect.");
                object data=Activator.CreateInstance(element);element.GetProperty("PassengersIn").SetValue(data,37,null);element.GetProperty("PassengersOut").SetValue(data,11,null);nodes.SetValue(data,65000);field.SetValue(null,nodes);
                cache.GetMethod("OnSaveData",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                if(storage.LoadData("IPT_NodeData")==null)throw new Exception("IPT save callback did not write data.");
                arguments=new object[]{null};if(!(bool)cache.GetMethod("TryLoadData").Invoke(null,arguments))throw new Exception("IPT reload callback failed.");
                var restored=(Array)arguments[0];if(restored.Length!=65536 || (int)element.GetProperty("PassengersIn").GetValue(restored.GetValue(65000),null)!=37 || (int)element.GetProperty("PassengersOut").GetValue(restored.GetValue(65000),null)!=11)throw new Exception("High node statistics lost during IPT save/reload.");
                field.SetValue(null,restored);
                var release=assembly.GetType("ImprovedPublicTransport.HarmonyPatches.NetManagerPatches.ReleaseNodePatch",true).GetMethod("ReleaseNodePost");
                harmony.Patch(typeof(NetManager).GetMethod("ReleaseNode",new[]{typeof(ushort)}),postfix:new HarmonyMethod(release));
                if(Singleton<NetManager>.instance.m_nodes.m_buffer[65000].m_flags!=0)throw new Exception("Test node is occupied.");
                Singleton<NetManager>.instance.ReleaseNode(65000);
                if(!(bool)element.GetProperty("IsEmpty").GetValue(restored.GetValue(65000),null))throw new Exception("Native node release did not clear IPT cache.");
                Entry.Log("PASS: "+assembly.GetName().Name+" native Mono default cache allocation, v003 statistics save/reload at node 65000 and actual NetManager.ReleaseNode postfix; memory-only test storage, city storage untouched.");
            }
            finally{harmony.UnpatchAll(harmony.Id);instanceField.SetValue(null,previousInstance);field.SetValue(null,previousCache);}
        }
    }
}
