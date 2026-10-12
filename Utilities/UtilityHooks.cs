using System;
using System.Collections.Generic;
using System.Reflection;
using HundredTiles.Core;
using HundredTiles.SaveBridge;

namespace HundredTiles.Utilities
{
    public static class UtilityHooks
    {
        public static bool ElectricityStep(ElectricityManager __instance, int subStep,
            ElectricityManager.Cell[] ___m_electricityGrid, ref int ___m_pulseGroupCount,
            ref int ___m_pulseUnitStart, ref int ___m_pulseUnitEnd, ref int ___m_processedCells,
            ref int ___m_conductiveCells, ref bool ___m_canContinue)
        {
            RequireGrid(___m_electricityGrid.Length);
            ExpandedElectricityManager.SimulationStepImpl(__instance, subStep, ___m_electricityGrid,
                ref ___m_pulseGroupCount, ref ___m_pulseUnitStart, ref ___m_pulseUnitEnd,
                ref ___m_processedCells, ref ___m_conductiveCells, ref ___m_canContinue);
            return false;
        }
        public static bool ElectricityUpdate(ElectricityManager __instance, float minX, float minZ, float maxX, float maxZ,
            ElectricityManager.Cell[] ___m_electricityGrid)
        {
            RequireGrid(___m_electricityGrid.Length);
            ExpandedElectricityManager.UpdateGrid(__instance, minX, minZ, maxX, maxZ, ___m_electricityGrid);
            return false;
        }
        public static bool WaterStep(WaterManager __instance, int subStep, WaterManager.Cell[] ___m_waterGrid,
            ref int ___m_waterPulseGroupCount, ref int ___m_waterPulseUnitStart, ref int ___m_waterPulseUnitEnd,
            ref int ___m_sewagePulseGroupCount, ref int ___m_sewagePulseUnitStart, ref int ___m_sewagePulseUnitEnd,
            ref int ___m_heatingPulseGroupCount, ref int ___m_heatingPulseUnitStart, ref int ___m_heatingPulseUnitEnd,
            ref int ___m_processedCells, ref int ___m_conductiveCells, ref bool ___m_canContinue,
            WaterManager.PulseGroup[] ___m_waterPulseGroups, WaterManager.PulseGroup[] ___m_sewagePulseGroups,
            WaterManager.PulseGroup[] ___m_heatingPulseGroups)
        {
            RequireGrid(___m_waterGrid.Length);
            ExpandedWaterManager.SimulationStepImpl(__instance, subStep, ___m_waterGrid,
                ref ___m_waterPulseGroupCount, ref ___m_waterPulseUnitStart, ref ___m_waterPulseUnitEnd,
                ref ___m_sewagePulseGroupCount, ref ___m_sewagePulseUnitStart, ref ___m_sewagePulseUnitEnd,
                ref ___m_heatingPulseGroupCount, ref ___m_heatingPulseUnitStart, ref ___m_heatingPulseUnitEnd,
                ref ___m_processedCells, ref ___m_conductiveCells, ref ___m_canContinue,
                ___m_waterPulseGroups, ___m_sewagePulseGroups, ___m_heatingPulseGroups);
            return false;
        }
        static void RequireGrid(int count)
        { if (count != UtilityGrid.Side * UtilityGrid.Side) throw new InvalidOperationException("Utility hook requires the expanded grid."); }

        public static IList<HookSpec> Specifications()
        {
            var result = new List<HookSpec>();
            foreach (var row in new[] {
                new[] { "ElectricityManager", "SimulationStepImpl", "ElectricityStep" },
                new[] { "ElectricityManager", "UpdateGrid", "ElectricityUpdate" },
                new[] { "WaterManager", "SimulationStepImpl", "WaterStep" } })
            {
                Type type = row[0] == "ElectricityManager" ? typeof(ElectricityManager) : typeof(WaterManager);
                var method = type.GetMethod(row[1], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) throw new MissingMethodException(type.FullName, row[1]);
                var prefix = typeof(UtilityHooks).GetMethod(row[2]);
                foreach (var argument in prefix.GetParameters())
                {
                    if (!argument.Name.StartsWith("___", StringComparison.Ordinal)) continue;
                    var field = type.GetField(argument.Name.Substring(3), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Type expected = argument.ParameterType.IsByRef ? argument.ParameterType.GetElementType() : argument.ParameterType;
                    if (field == null || field.FieldType != expected) throw new MissingFieldException(type.FullName, argument.Name);
                }
                result.Add(new HookSpec { Target = method, Prefix = prefix });
            }
            return result;
        }
    }
}
