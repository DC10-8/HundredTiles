using HundredTiles.Core;

namespace HundredTiles.Utilities
{
    internal static class ElectricityManagerPatches
    {
        internal const int ExpandedElectricityGridResolution = UtilityGrid.Side;
        internal const int ExpandedElectricityGridMax = UtilityGrid.Side - 1;
        internal const float ExpandedElectricityGridHalfResolution = UtilityGrid.Origin;
    }
    internal static class WaterManagerPatches
    {
        internal const int ExpandedWaterGridResolution = UtilityGrid.Side;
        internal const int ExpandedWaterGridMax = UtilityGrid.Side - 1;
        internal const float ExpandedWaterGridHalfResolution = UtilityGrid.Origin;
    }
}
