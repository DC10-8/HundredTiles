using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;

namespace HundredTiles.SaveBridge
{
    public sealed class HookSpec
    {
        public MethodBase Target;
        public MethodInfo Prefix,Postfix,Finalizer,Transpiler;
    }

    public static class SerializerHooks
    {
        // Returns the concrete connections for the gated installer. Does not
        // install anything. Full terrain/tool/manager coverage is still required.
        public static IList<HookSpec> Specifications()
        {
            VerifyRuntime();
            Type simulationData=typeof(SimulationManager).GetNestedType("Data");
            Type terrainData=typeof(TerrainManager).GetNestedType("Data");
            Type waterData=typeof(WaterSimulation).GetNestedType("Data");
            Type areaData=typeof(GameAreaManager).GetNestedType("Data");
            Type resourceData=typeof(NaturalResourceManager).GetNestedType("Data");
            var hooks=new List<HookSpec> {
                new HookSpec {Target=typeof(ColossalFramework.IO.DataSerializer).GetMethod("Serialize",new[]{typeof(Stream),typeof(ColossalFramework.IO.DataSerializer.Mode),typeof(uint),typeof(ColossalFramework.IO.IDataContainer)}),Prefix=Bridge("BeginSerialize"),Finalizer=Bridge("EndSerialize")},
                Spec(simulationData,"Deserialize","BeginLoad",null,"LoadFailure",null),
                Spec(simulationData,"AfterDeserialize","BeforeAfterDeserialize",null,"LoadFailure",null),
                Spec(typeof(SimulationManager),"Managers_UpdateData","BeforeManagersUpdate",null,null,null),
                Spec(terrainData,"Serialize",null,null,null,"TerrainSerialize"),
                Spec(terrainData,"Deserialize",null,"TerrainRead","LoadFailure","TerrainDeserialize"),
                Spec(waterData,"Serialize",null,null,null,"WaterSerialize"),
                Spec(waterData,"Deserialize",null,"WaterRead","LoadFailure","WaterDeserialize"),
                Spec(areaData,"Serialize",null,null,null,"AreaSerialize"),
                Spec(areaData,"Deserialize",null,"AreasRead","LoadFailure","AreaDeserialize"),
                Spec(resourceData,"Serialize",null,null,null,"ResourceSerialize"),
                Spec(resourceData,"Deserialize",null,"ResourceTilesRead","LoadFailure","ResourceDeserialize")
            };
            hooks.Add(new HookSpec {Target=typeof(NaturalResourceManager).GetMethod("SimulationStepImpl2",BindingFlags.Instance|BindingFlags.NonPublic),Transpiler=typeof(SerializerHooks).GetMethod("ResourceSchedule")});
            hooks.Add(Spec(typeof(ResourceWrapper),"W2G",null,null,null,"ResourceOrigin"));
            hooks.Add(Spec(typeof(ResourceWrapper),"G2W",null,null,null,"ResourceOrigin"));
            hooks.Add(Spec(typeof(TreeManager).GetNestedType("Data"),"Deserialize",null,"TreesRead","LoadFailure",null));
            hooks.Add(Spec(typeof(PropManager).GetNestedType("Data"),"Deserialize",null,"PropsRead","LoadFailure",null));
            hooks.Add(new HookSpec {Target=typeof(DistrictManager).GetNestedType("Data").GetMethod("Serialize"),Transpiler=typeof(SerializerHooks).GetMethod("DistrictSerialize")});
            hooks.Add(new HookSpec {Target=typeof(DistrictManager).GetNestedType("Data").GetMethod("Deserialize"),Transpiler=typeof(SerializerHooks).GetMethod("DistrictDeserialize"),Postfix=typeof(DistrictSaveBridge).GetMethod("ReadComplete"),Finalizer=Bridge("LoadFailure")});
            foreach(var hook in ObjectSerializerHooks.Specifications())hooks.Add(hook);
            return hooks;
        }

        static HookSpec Spec(Type type,string name,string prefix,string postfix,string finalizer,string transpiler)
        {
            var target=type.GetMethod(name,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            if(target==null) throw new MissingMethodException(type.FullName,name);
            return new HookSpec { Target=target,Prefix=Bridge(prefix),Postfix=Bridge(postfix),Finalizer=Bridge(finalizer),Transpiler=transpiler==null ? null : typeof(SerializerHooks).GetMethod(transpiler) };
        }
        static MethodInfo Bridge(string name) { return name==null ? null : typeof(RuntimeSaveBridge).GetMethod(name); }
        public static IEnumerable<CodeInstruction> DistrictSerialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {Verify(__originalMethod);return DistrictTransform(instructions,"WriteDistricts","WriteParks");}
        public static IEnumerable<CodeInstruction> DistrictDeserialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {Verify(__originalMethod);return DistrictTransform(instructions,"ReadDistricts","ReadParks");}
        static IEnumerable<CodeInstruction> DistrictTransform(IEnumerable<CodeInstruction> instructions,string districts,string parks)
        {var helpers=new Dictionary<string,MethodInfo>();helpers.Add("m_districtGrid",typeof(DistrictSaveBridge).GetMethod(districts));helpers.Add("m_parkGrid",typeof(DistrictSaveBridge).GetMethod(parks));return SerializerTranspiler.Transform(instructions,helpers);}

        public static IEnumerable<CodeInstruction> TerrainSerialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { Verify(__originalMethod); return SerializerTranspiler.Transform(instructions,Helpers("m_rawHeights","WriteHeights","m_blockHeights","WriteBlocks","m_blockHeights2","WriteTargets")); }
        public static IEnumerable<CodeInstruction> TerrainDeserialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { Verify(__originalMethod); return SerializerTranspiler.Transform(instructions,Helpers("m_rawHeights","ReadHeights","m_blockHeights","ReadBlocks")); }
        public static IEnumerable<CodeInstruction> WaterSerialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { Verify(__originalMethod); return SerializerTranspiler.Transform(instructions,Helpers("BeginRead","WriteWater","m_waterFrameIndex:last","WriteWaterFrame")); }
        public static IEnumerable<CodeInstruction> WaterDeserialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { Verify(__originalMethod); return SerializerTranspiler.Transform(instructions,Helpers("m_waterBuffers","ReadWater")); }
        public static IEnumerable<CodeInstruction> AreaSerialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { Verify(__originalMethod); return SerializerTranspiler.Transform(instructions,Helpers("m_areaGrid","WriteAreas","m_areaCount","WriteAreaCount","m_startTile","WriteStartTile")); }
        public static IEnumerable<CodeInstruction> AreaDeserialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { Verify(__originalMethod); return SerializerTranspiler.Transform(instructions,Helpers("m_areaGrid","ReadAreas")); }
        public static IEnumerable<CodeInstruction> ResourceSerialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { Verify(__originalMethod); return SerializerTranspiler.Transform(instructions,Helpers("m_areaResources","WriteResourceTiles","m_naturalResources","WriteResourceCells")); }
        public static IEnumerable<CodeInstruction> ResourceDeserialize(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { Verify(__originalMethod); return SerializerTranspiler.Transform(instructions,Helpers("m_areaResources","ReadResourceTiles","m_naturalResources","ReadResourceCells")); }
        public static IEnumerable<CodeInstruction> ResourceSchedule(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        { Verify(__originalMethod); return ResourceScheduleTranspiler.Transform(instructions); }
        public static IEnumerable<CodeInstruction> ResourceOrigin(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {Verify(__originalMethod);return SerializerTranspiler.Transform(instructions,Helpers("get_gridResolution","LegacyResourceOrigin"));}

        static Dictionary<string,MethodInfo> Helpers(params string[] pairs)
        {
            var result=new Dictionary<string,MethodInfo>();
            for(int i=0;i<pairs.Length;i+=2) result.Add(pairs[i],Bridge(pairs[i+1]));
            return result;
        }
        public static void Verify(MethodBase method)
        {
            // The reviewed whole-assembly hash fixes serializer IL, method
            // identities and lock flow. Other transpilers require a new review.
            using(var stream=File.OpenRead(AssemblyFile.Resolve(method.Module.Assembly)))
            using(var hash=SHA256.Create())
                if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","")!=PatchPlan.Embedded().AssemblyHash)
                    throw new InvalidOperationException("Serializer game version mismatch.");
        }
        static void VerifyRuntime()
        {
            // Root-before-child AfterDeserialize ordering was reviewed in this
            // runtime, which is a separate DLL from Assembly-CSharp.
            using(var stream=File.OpenRead(AssemblyFile.Resolve(typeof(ColossalFramework.IO.DataSerializer).Assembly)))
            using(var hash=SHA256.Create())
                if(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","")!="34701F61DEF31F5F9DF79D573CE19E0BC52575879BCC395D51B39F6D746A58B9")
                    throw new InvalidOperationException("Serializer runtime version mismatch; load ordering requires review.");
            Verify(typeof(SimulationManager).GetNestedType("Data").GetMethod("AfterDeserialize"));
        }
    }
}
