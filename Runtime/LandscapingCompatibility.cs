using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using ColossalFramework;
using ColossalFramework.Plugins;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;

namespace HundredTiles.Runtime
{
    public static class LandscapingCompatibility
    {
        // name | original raw IL hash (audit provenance) | resolved semantic hash.
        // Generated from the pinned disk DLL in a clean process. Never load a
        // runtime reflection-only reference: loader caches may contain rewritten IL.
        static readonly string[] Reviewed={
            "ResetUndoBuffer|C745536530C5C983BE96524CF7F65F3A072C8DA691B710F218B051E368EC33AD|5C59ADE57C9C35612F77B41B34CAF704F22A5DD03C954691CAD271779C869597",
            "OnEnable|38CBDE69D492233E5FE6F2E4FD77B35C537809ECA36E3D52A2B680E7CC533B60|D5C2F2AE625EFB2CA36F1ECA7B95FB995FD6362F2E1A640DFD3412FC463B952E",
            "SimulationStep|BAEE46B6BAE14E9A12A6A95A028FD94B8C9A495F805B52286E4783CE30A5148C|077FC460EF98AB06F27F2C520228A6565644E4CECD67F94A02C1D868AE936C01",
            "EndStroke|027AB2E99EB7A79D9F1EF07B19C5F1FB7930AA6E7F6B3B788E284B2E267FF75B|4B8AAE81F6EABD8AEB8D9E88A2307D590A1C8CE5F43BD20AF0176A4568DB5E3D",
            "ApplyUndo|78AA52FBBBB80A92101C2AB0CAB7CD07E2EFB2CFDB747CAF08A1576EFDF27DF5|FAEC07051D6E62AA2BEAECC42D17E71922A615C79D8D2E44DF74499C2617BCDA",
            "ApplyBrush|44825750E121F223DC596F3F6CA81D49E120FE9D9FFCF605FE7A9C41E0D8B387|ED70E1D3878F5F37AA67051DA97E6C33266BEC7B9F4F77E454FA33A86D8D18A4"
        };
        public static void AddHooks(Assembly assembly,IList<HookSpec> hooks)
        {
            string path=assembly.Location;
            if(string.IsNullOrEmpty(path))foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                foreach(var loaded in plugin.GetAssemblies())if(loaded==assembly)path=Path.Combine(plugin.modPath,"NaturalResourcesBrush.dll");
            AddReviewedHooks(assembly,path,hooks);
        }
        public static void AddReviewedHooks(Assembly assembly,string path,IList<HookSpec> hooks)
        {
            if(string.IsNullOrEmpty(path) || !File.Exists(path) || PlannedTranspiler.Hash(File.ReadAllBytes(path))!="E8DA4A01A0F0334D30FF8873CAD15E352D1EEB5359AF526E9367FD2BB77DCC88")
                throw new InvalidOperationException("NaturalResourcesBrush version differs from reviewed landscaping compatibility adapter.");
            var type=assembly.GetType("NaturalResourcesBrush.Detours.TerrainToolDetour",true);
            foreach(string row in Reviewed)
            {
                string[] parts=row.Split('|');var target=type.GetMethod(parts[0],BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);
                string actual=target==null || target.GetMethodBody()==null?"missing":ReviewedMethod.SemanticHash(target);
                bool knownTrigger=parts[0]=="OnEnable" && actual=="02B737C59859470AC0F92A26315559FA97522EB78F250284784D2F5ACBBD45F7";
                if(knownTrigger)VerifyBehaviourUpdater();
                if(actual!=parts[2] && !knownTrigger)
                {
                    string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Colossal Order/Cities_Skylines/HundredTiles-diagnostics");
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder,"landscaping-method.txt"),parts[0]+"\nExpected: "+parts[2]+"\nActual: "+actual+"\n"+(actual=="missing"?"signature/body missing":ReviewedMethod.SemanticDescription(target)));
                    throw new InvalidOperationException("NaturalResourcesBrush terrain helper changed: "+parts[0]);
                }
                hooks.Add(new HookSpec{Target=target,Transpiler=typeof(LandscapingCompatibility).GetMethod("TerrainGrid")});
            }
        }
        internal static void VerifyBehaviourUpdater()
        {
            // FPS Booster's reviewed AutoPatcher prepends exactly ldarg.0 +
            // BehaviourRegistry.RegisterBehaviour(MonoBehaviour) to OnEnable.
            // Its complete semantic fingerprint above also verifies the unchanged body.
            Assembly updater=null;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="BehaviourUpdater")updater=assembly;
            string path=updater==null?null:updater.Location;
            if(string.IsNullOrEmpty(path))foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                foreach(var loaded in plugin.GetAssemblies())if(loaded.GetName().Name=="FPS_Booster")path=Path.Combine(plugin.modPath,"Patches/BehaviourUpdater.dll");
            if(string.IsNullOrEmpty(path) || !File.Exists(path) || PlannedTranspiler.Hash(File.ReadAllBytes(path))!="0D3DC3B601E3E6157008BD8BF0CB931FC99005D1B3450EB19027FEBB7F4A4C0E")throw new InvalidOperationException("BehaviourUpdater differs from reviewed landscaping enable trigger.");
        }
        public static IEnumerable<CodeInstruction> TerrainGrid(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {
            var output=new List<CodeInstruction>();foreach(var instruction in instructions)output.Add(new CodeInstruction(instruction));
            int origins=0,constants=0;
            foreach(var instruction in output)
                if(instruction.opcode==OpCodes.Ldc_I4 && instruction.operand is int)
                {
                    int value=(int)instruction.operand;
                    if(value==1081){instruction.operand=HundredTiles.Core.ExpandedMap.HeightStride;constants++;}
                    else if(value==1168561){instruction.operand=HundredTiles.Core.ExpandedMap.HeightStride*HundredTiles.Core.ExpandedMap.HeightStride;constants++;}
                    else if(value==1080){instruction.operand=HundredTiles.Core.ExpandedMap.RawResolution;constants++;}
                }
            if(__originalMethod.Name=="ApplyBrush")for(int i=0;i+3<output.Count;i++)
            {
                // Local 3 stores the grid extent. Eight origin expressions also
                // use it; retain their original half-extent of 540 cells.
                if(output[i].opcode==OpCodes.Ldloc_3 &&
                   ((output[i+1].opcode==OpCodes.Conv_R8 && output[i+2].opcode==OpCodes.Ldc_R8 && (double)output[i+2].operand==0.5) ||
                    (output[i+1].opcode==OpCodes.Conv_R4 && output[i+2].opcode==OpCodes.Ldc_R4 && (float)output[i+2].operand==0.5f)) && output[i+3].opcode==OpCodes.Mul)
                {output[i].opcode=OpCodes.Ldc_I4;output[i].operand=1080;origins++;}
            }
            if(__originalMethod.Name=="ApplyBrush")
            {
                if(origins!=8)throw new InvalidOperationException("NaturalResourcesBrush expected eight original world-origin expressions.");
            }
            if(constants==0)throw new InvalidOperationException("NaturalResourcesBrush reviewed terrain constants missing.");
            return output;
        }
    }
}

