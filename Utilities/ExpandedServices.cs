// Adapted from EightyOne2 ImmaterialResourceManagerPatches, MIT license.
// Copyright (c) algernon (K. Algernon A. Sheppard). See UPSTREAM-LICENSE.txt.
using System;
using System.Collections.Generic;
using ColossalFramework;
using UnityEngine;
using static ImmaterialResourceManager;
namespace HundredTiles.Utilities {
 public static class ExpandedServices {
 const int ExpandedImmaterialResourceGridResolution=HundredTiles.Core.ServiceGrid.Side, ExpandedImmaterialResourceGridMax=HundredTiles.Core.ServiceGrid.Side-1;
 const float ExpandedImmaterialResourceGridHalfResolution=225f;
 static readonly Dictionary<ExpandedCellLocation,int> TempAreaIndexes=new Dictionary<ExpandedCellLocation,int>();
 static readonly List<ExpandedAreaQueueItem> TempAreaQueue=new List<ExpandedAreaQueueItem>();
private enum AreaQueueItemDirection
        {
            None = 0,
            Up = 1,
            Down = 2,
            Left = 4,
            Right = 8,
            Vertical = 3,
            Horizontal = 0xC,
            All = 0xF,
        }
public static bool ParkIndexes(out ParkAreaIndex[] __result, byte park, float radius)
        {
            TempAreaQueue.Clear();
            TempAreaIndexes.Clear();
            float num2 = Mathf.Max(38.4f, radius + 19.2f);
            int num3 = Mathf.FloorToInt(num2 * num2 / 1474.56f);
            DistrictManager districtManager = Singleton<DistrictManager>.instance;
            Vector3 nameLocation = districtManager.m_parks.m_buffer[park].m_nameLocation;
            ExpandedCellLocation cellLocation = default;

            // cellLocation.m_x = (byte)Mathf.Clamp((int)((nameLocation.x / 38.4f) + 128f), 0, 255);
            cellLocation.m_x = (ushort)Mathf.Clamp((int)((nameLocation.x / 38.4f) + ExpandedImmaterialResourceGridHalfResolution), 0, ExpandedImmaterialResourceGridMax);

            // cellLocation.m_z = (byte)Mathf.Clamp((int)((nameLocation.z / 38.4f) + 128f), 0, 255);
            cellLocation.m_z = (ushort)Mathf.Clamp((int)((nameLocation.z / 38.4f) + ExpandedImmaterialResourceGridHalfResolution), 0, ExpandedImmaterialResourceGridMax);

            ExpandedAreaQueueItem item = default;
            item.m_cost = 0;
            item.m_location = cellLocation;
            item.m_source = cellLocation;
            item.m_direction = AreaQueueItemDirection.All;
            TempAreaIndexes[cellLocation] = TempAreaQueue.Count;
            TempAreaQueue.Add(item);
            for (int i = 0; i < TempAreaQueue.Count; i++)
            {
                ExpandedAreaQueueItem item2 = TempAreaQueue[i];
                if (item2.m_location.m_x > 0 && (item2.m_direction & AreaQueueItemDirection.Left) != 0)
                {
                    ProcessParkArea(ref item2, park, num3, AreaQueueItemDirection.Left);
                }

                if (item2.m_location.m_z > 0 && (item2.m_direction & AreaQueueItemDirection.Down) != 0)
                {
                    ProcessParkArea(ref item2, park, num3, AreaQueueItemDirection.Down);
                }

                if (item2.m_location.m_x < ExpandedImmaterialResourceGridMax && (item2.m_direction & AreaQueueItemDirection.Right) != 0)
                {
                    ProcessParkArea(ref item2, park, num3, AreaQueueItemDirection.Right);
                }

                if (item2.m_location.m_z < ExpandedImmaterialResourceGridMax && (item2.m_direction & AreaQueueItemDirection.Up) != 0)
                {
                    ProcessParkArea(ref item2, park, num3, AreaQueueItemDirection.Up);
                }
            }

            List<ParkAreaIndex> list = new List<ParkAreaIndex>();
            for (int j = 0; j < TempAreaQueue.Count; j++)
            {
                ExpandedAreaQueueItem areaQueueItem = TempAreaQueue[j];
                if (areaQueueItem.m_cost < num3)
                {
                    ParkAreaIndex parkAreaIndex = default;

                    // parkAreaIndex.m_index = ((areaQueueItem.m_location.m_z * 256) + areaQueueItem.m_location.m_x) * 29;
                    parkAreaIndex.m_index = ((Mathf.Min(areaQueueItem.m_location.m_z, ExpandedImmaterialResourceGridMax) * ExpandedImmaterialResourceGridResolution) + Mathf.Min(areaQueueItem.m_location.m_x, ExpandedImmaterialResourceGridMax)) * RESOURCE_COUNT;

                    parkAreaIndex.m_cost = areaQueueItem.m_cost;
                    ParkAreaIndex item3 = parkAreaIndex;
                    list.Add(item3);
                }
            }

            __result = list.ToArray();

            // Don't execute original method.
            return false;
        }
private static void ProcessParkArea(ref ExpandedAreaQueueItem item, byte park, int maxCost, AreaQueueItemDirection direction)
        {
            ExpandedCellLocation cellLocation = default;
            switch (direction)
            {
                case AreaQueueItemDirection.Left:
                    cellLocation.m_x = (ushort)(item.m_location.m_x - 1);
                    break;
                case AreaQueueItemDirection.Right:
                    cellLocation.m_x = (ushort)(item.m_location.m_x + 1);
                    break;
                default:
                    cellLocation.m_x = item.m_location.m_x;
                    break;
            }

            switch (direction)
            {
                case AreaQueueItemDirection.Up:
                    cellLocation.m_z = (ushort)(item.m_location.m_z - 1);
                    break;
                case AreaQueueItemDirection.Down:
                    cellLocation.m_z = (ushort)(item.m_location.m_z + 1);
                    break;
                default:
                    cellLocation.m_z = item.m_location.m_z;
                    break;
            }

            // Inserted bounds checks due to expanded ranges.
            cellLocation.m_x = (ushort)Mathf.Min(cellLocation.m_x, ExpandedImmaterialResourceGridMax);
            cellLocation.m_z = (ushort)Mathf.Min(cellLocation.m_z, ExpandedImmaterialResourceGridMax);

            ExpandedAreaQueueItem value2 = default;
            if (TempAreaIndexes.TryGetValue(cellLocation, out var value))
            {
                value2 = TempAreaQueue[value];
                value2.m_direction = RemoveDirection(value2.m_direction, direction);
                if (value2.m_cost > 0 && (value2.m_source.m_x != item.m_source.m_x || value2.m_source.m_z != item.m_source.m_z))
                {
                    int num = ((cellLocation.m_x - item.m_source.m_x) * (cellLocation.m_x - item.m_source.m_x)) + ((cellLocation.m_z - item.m_source.m_z) * (cellLocation.m_z - item.m_source.m_z));
                    if (num < value2.m_cost)
                    {
                        value2.m_cost = num;
                        value2.m_source = item.m_source;
                    }
                }

                TempAreaQueue[value] = value2;
                return;
            }

            value2.m_location = cellLocation;

            // Vector3 worldPos = new Vector3(((float)(int)cellLocation.m_x - 128f + 0.5f) * 38.4f, 0f, ((float)(int)cellLocation.m_z - 128f + 0.5f) * 38.4f);
            Vector3 worldPos = new Vector3(((float)(int)cellLocation.m_x - ExpandedImmaterialResourceGridHalfResolution + 0.5f) * RESOURCEGRID_CELL_SIZE, 0f, ((float)(int)cellLocation.m_z - ExpandedImmaterialResourceGridHalfResolution + 0.5f) * RESOURCEGRID_CELL_SIZE);

            if (Singleton<DistrictManager>.instance.GetPark(worldPos) == park)
            {
                value2.m_cost = 0;
                value2.m_source = cellLocation;
            }
            else
            {
                value2.m_cost = ((cellLocation.m_x - item.m_source.m_x) * (cellLocation.m_x - item.m_source.m_x)) + ((cellLocation.m_z - item.m_source.m_z) * (cellLocation.m_z - item.m_source.m_z));
                value2.m_source = item.m_source;
            }

            if (value2.m_cost < maxCost)
            {
                value2.m_direction = RemoveDirection(AreaQueueItemDirection.All, direction);
                TempAreaIndexes[cellLocation] = TempAreaQueue.Count;
                TempAreaQueue.Add(value2);
            }
        }
private static AreaQueueItemDirection RemoveDirection(AreaQueueItemDirection itemDirection, AreaQueueItemDirection direction)
        {
            switch (direction)
            {
                case AreaQueueItemDirection.Up:
                    return itemDirection & ~AreaQueueItemDirection.Down;
                case AreaQueueItemDirection.Down:
                    return itemDirection & ~AreaQueueItemDirection.Up;
                case AreaQueueItemDirection.Left:
                    return itemDirection & ~AreaQueueItemDirection.Right;
                case AreaQueueItemDirection.Right:
                    return itemDirection & ~AreaQueueItemDirection.Left;
                default:
                    return AreaQueueItemDirection.None;
            }
        }
private struct ExpandedCellLocation
        {
            public ushort m_x;

            public ushort m_z;

            public override string ToString()
            {
                return m_x + ";" + m_z;
            }
        }
private struct ExpandedAreaQueueItem
        {
            public int m_cost;

            public ExpandedCellLocation m_location;

            public ExpandedCellLocation m_source;

            public AreaQueueItemDirection m_direction;

            public override string ToString()
            {
                return string.Concat("loc: ", m_location, " cost: ", m_cost, " dir: ", m_direction);
            }
        }
}}

