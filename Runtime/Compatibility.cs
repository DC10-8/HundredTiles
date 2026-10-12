using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;

namespace HundredTiles.Runtime
{
    public static class Compatibility
    {
        public static bool Allowed(string owner,MethodBase target,Patches patches)
        {
            return ReviewedCompatibility.Allowed(owner,target,patches);
        }
        public static void AddHooks(IList<HookSpec> hooks)
        {
            NetworkCapacityCompatibility.Configure();
            ReviewedCompatibility.AddHooks(hooks);
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if(assembly.GetName().Name=="ForceRoadConnect")ForceRoadCompatibility.AddHooks(assembly,hooks);
                if(assembly.GetName().Name=="MoveIt")MoveItCompatibility.AddHooks(assembly,hooks);
                if(assembly.GetName().Name=="NoUtilityNetworks")HeatingCompatibility.AddHooks(assembly,hooks);
                if(assembly.GetName().Name=="NaturalResourcesBrush")LandscapingCompatibility.AddHooks(assembly,hooks);
                if(assembly.GetName().Name!="AutoHistoricalPlop")continue;
                string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Colossal Order/Cities_Skylines/Addons/Mods/AutoHistoricalPlop/AutoHistoricalPlop.dll");
                if(!File.Exists(path) || PlannedTranspiler.Hash(File.ReadAllBytes(path))!="43B17D6F29F813548BCEC717D8BFACC4F7A5A9352E2AB34ED5C1AC13C9AD089D")throw new InvalidOperationException("AutoHistoricalPlop version differs from reviewed compatibility adapter.");
                string type="AutoHistoricalPlop.Managers.ZoneRemovalGuard";
                var target=assembly.GetType(type,true).GetMethod("ClearZonesUpdatedIn");
                if(target==null || target.GetParameters().Length!=4 || target.GetMethodBody()==null)throw new InvalidOperationException("AutoHistoricalPlop spatial helper changed.");
                hooks.Add(new HookSpec{Target=target,Transpiler=typeof(Compatibility).GetMethod("HistoricalGrid")});
            }
        }
        public static IEnumerable<CodeInstruction> HistoricalGrid(IEnumerable<CodeInstruction> instructions)
        {
            var output=new List<CodeInstruction>();int count=0,grids=0;
            foreach(var source in instructions){var instruction=new CodeInstruction(source);if(instruction.opcode==OpCodes.Ldfld && instruction.operand is FieldInfo){var field=(FieldInfo)instruction.operand;if(field.DeclaringType==typeof(BuildingManager) && field.Name=="m_buildingGrid")grids++;}if(instruction.opcode==OpCodes.Ldc_I4 && instruction.operand is int){int value=(int)instruction.operand;if(value==270 || value==269){instruction.operand=value==270?300:299;count++;}}output.Add(instruction);}
            if(count!=3 || grids!=1)throw new InvalidOperationException("AutoHistoricalPlop grid adapter expected one grid and three constants.");
            return output;
        }
    }
}
