using HundredTiles.Core;
namespace HundredTiles.SaveBridge
{
    // Native encoded-array migration is tested by RuntimeChecks, not mock managers.
    public static class LegacyAreas
    {
        public static OwnershipState Read(byte[] bytes, OwnershipState baseline)
        { throw new NotSupportedException("Use native serialization checks for fakeGAM data."); }
    }
}
