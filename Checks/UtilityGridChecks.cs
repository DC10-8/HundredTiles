using System;
using HundredTiles.Core;

static class UtilityGridChecks
{
    static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    public static void Run()
    {
        Check(-UtilityGrid.Origin * UtilityGrid.CellSize <= ExpandedMap.WorldMin &&
            (UtilityGrid.Side - UtilityGrid.Origin) * UtilityGrid.CellSize >= ExpandedMap.WorldMax, "Utility coverage");
        var baseline = new int[65536]; for (int i = 0; i < baseline.Length; i++) baseline[i] = i + 1;
        var expanded = UtilityGrid.Expand(baseline); var projected = UtilityGrid.Project(expanded);
        for (int z = 0; z < 256; z++) for (int x = 0; x < 256; x++)
        {
            Check(projected[z * 256 + x] == baseline[z * 256 + x], "Utility projection");
            Check(UtilityGrid.CellToWorld(x + UtilityGrid.Offset) == (x - 128 + .5f) * 38.25f, "Utility origin changed");
        }
        var visits = new int[UtilityGrid.Side];
        for (int phase = 0; phase < 128; phase++)
        for (int row = UtilityGrid.FirstRow(phase); row <= UtilityGrid.LastRow(phase); row++) visits[row]++;
        for (int i = 0; i < visits.Length; i++)
        { Check(visits[i] == 1, "Utility schedule misses or duplicates rows"); Check(UtilityGrid.WorldToCell(UtilityGrid.CellToWorld(i)) == i, "Utility cell identity"); }
        Check(UtilityGrid.WorldToCell(ExpandedMap.WorldMax - 1) > 255, "Outer utility coordinates still byte-limited");
        Console.WriteLine("PASS: utility grid 508x508 covers the 100-tile map; 65536 cells migrated/projected at unchanged world coordinates; all 508 rows scheduled once.");
    }
}
