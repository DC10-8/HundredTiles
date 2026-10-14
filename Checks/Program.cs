using System;
using HundredTiles.Core;

DistrictMigrationChecks.Run();

#if HUNDREDTILES_324
Profile324Checks.Run();
#else
PackedPositionChecks.Run();
MapLayoutChecks.Run();
LandscapeChecks.Run();
UtilityGridChecks.Run();

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
}
int count = 0;
for (int z = 0; z < 10; z++)
for (int x = 0; x < 10; x++)
{
    Check(ExpandedMap.TryGetTile(-8640 + x * 1920 + 960, -8640 + z * 1920 + 960, out int id)
        && id == z * 10 + x, "Tile identity");
    count++;
}
Check(ExpandedMap.WorldMax - ExpandedMap.WorldMin == 19200, "Standard tile width");
Check(ExpandedMap.TryGetTile(-8640, -8640, out int corner) && corner == 0, "Inclusive minimum");
Check(!ExpandedMap.TryGetTile(10560, 0, out _), "Exclusive maximum");
Check(!ExpandedMap.TryGetTile(-8640.001, 0, out _), "Negative outside edge");
Check(!ExpandedMap.TryGetTile(double.NaN, 0, out _), "NaN");
Check(!ExpandedMap.TryGetTile(double.PositiveInfinity, 0, out _), "Infinity");
foreach (int side in new[] { 5, 9 })
{
    var original = new int[side * side];
    for (int i = 0; i < original.Length; i++) original[i] = i + 1;
    var migrated = ExpandedMap.MigrateAreaGrid(original);
    int offset = side == 5 ? 2 : 0;
    for (int z = 0; z < side; z++)
    for (int x = 0; x < side; x++)
        Check(migrated[(z + offset) * 10 + x + offset] == original[z * side + x], "Ownership retained");
    Check(migrated[99] == 0, "New land not purchased");
}
var heights = new ushort[1081 * 1081];
for (int i = 0; i < heights.Length; i++) heights[i] = (ushort)(i % 65536);
var expanded = ExpandedMap.ExtendHeights(heights);
Check(expanded.Length == 1201 * 1201, "Expanded height size");
for (int z = 0; z < 1201; z++)
for (int x = 0; x < 1201; x++)
    Check(expanded[z * 1201 + x] == heights[Math.Min(z, 1080) * 1081 + Math.Min(x, 1080)], "Lossless edge migration");
try { ExpandedMap.ExtendHeights(new ushort[10]); throw new Exception("Invalid height accepted"); }
catch (ArgumentException) { }
try { ExpandedMap.MigrateAreaGrid(new int[100]); throw new Exception("Invalid area accepted"); }
catch (ArgumentException) { }
Console.WriteLine($"PASS: {count} standard-sized tiles; boundaries; 25/81 ownership migration; all 1,442,401 height samples; invalid inputs.");
Check(WaterGrid.CellsPerBit == 19, "Expanded bit granularity");
for (int i = 0; i <= 1200; i++)
{
    Check(WaterGrid.GetMaskCoordinate(i) < 64, "No water mask overflow");
    Check(WaterGrid.GetMaskBit(i) == (1UL << (i / 19)), "Correct water bit");
    Check(WaterGrid.WorldToRaw(WaterGrid.RawToWorld(i)) == i, "World/raw round trip");
}
Check(WaterGrid.RawToWorld(1080) == 8640, "Old edge retained");
Check(WaterGrid.RawToWorld(1200) == 10560, "New water edge");
Check(WaterGrid.GetMaskBit(0) != WaterGrid.GetMaskBit(1200), "Distant water does not alias");
var originalWater = new int[1081 * 1081];
long originalMass = 0;
for (int i = 0; i < originalWater.Length; i++) { originalWater[i] = i % 101; originalMass += originalWater[i]; }
var expandedWater = WaterGrid.ExtendBuffer(originalWater);
long migratedMass = 0;
for (int z = 0; z < 1201; z++)
for (int x = 0; x < 1201; x++)
{
    int expected = x <= 1080 && z <= 1080 ? originalWater[z * 1081 + x] : 0;
    Check(expandedWater[z * 1201 + x] == expected, "Water state retained and added region empty");
    migratedMass += expandedWater[z * 1201 + x];
}
Check(migratedMass == originalMass, "Migration does not create water");
try { WaterGrid.GetMaskCoordinate(1201); throw new Exception("Invalid water coordinate accepted"); }
catch (ArgumentOutOfRangeException) { }
try { WaterGrid.ExtendBuffer(new int[5]); throw new Exception("Invalid water buffer accepted"); }
catch (ArgumentException) { }
Console.WriteLine("PASS: all 1201 water mask coordinates; world coordinates; full buffer migration; mass preservation; invalid inputs.");
var districtIds=new byte[DistrictGrid.Side*DistrictGrid.Side];
Array.Fill(districtIds,(byte)1);
var nameLocations=DistrictNames.Calculate(districtIds);
Check(nameLocations[1]>65535,"District names retain wide indices");
Check(Math.Abs(DistrictNames.WorldX(nameLocations[1])-960)<40 && Math.Abs(DistrictNames.WorldZ(nameLocations[1])-960)<40,"District anchor uses asymmetric world origin");
for(int z=0;z<1000;z++)for(int x=0;x<1000;x++)districtIds[z*1000+x]=(byte)(1+(x/2+z/2)%2);
nameLocations=DistrictNames.Calculate(districtIds);
Check(nameLocations[1]!=nameLocations[2],"A queue containing all 250000 samples remains distinguishable from empty");
Array.Clear(districtIds);
foreach(int location in DistrictNames.Calculate(districtIds))Check(location==0,"Empty district grid");
Check(Math.Abs(DistrictNames.WorldX(0)+8640)<.01 && Math.Abs(DistrictNames.WorldZ(249999)-10521.6)<.01,"District name world bounds");
Console.WriteLine("PASS: district name anchors beyond ushort range; all-sample queue; asymmetric origin; empty districts.");

#endif

