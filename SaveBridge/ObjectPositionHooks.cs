using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using UnityEngine;

namespace HundredTiles.SaveBridge
{
    // Gated connections. Two bits unused by the reviewed vanilla enums travel
    // with struct copies. Serializer projection must strip them before activation.
    public static class ObjectPositionHooks
    {
        public const ushort XExtension = 0x1000, ZExtension = 0x2000, ExtensionMask = 0x3000;
        static bool AssetEditor
        {
            get
            {
                // Map deserialization initializes packed objects before the
                // in-game ToolManager/properties have been created.
                var manager=Singleton<ToolManager>.instance;
                return manager!=null && manager.m_properties!=null && manager.m_properties.m_mode==ItemClass.Availability.AssetEditor;
            }
        }

        public static bool TreeGet(ref TreeInstance __instance, ref Vector3 __result)
        {
            if (AssetEditor) return true;
            __result = Decode(__instance.m_posX, __instance.m_posY, __instance.m_posZ, __instance.m_flags);
            return false;
        }
        public static bool PropGet(ref PropInstance __instance, ref Vector3 __result)
        {
            if (AssetEditor) return true;
            __result = Decode(__instance.m_posX, __instance.m_posY, __instance.m_posZ, __instance.m_flags);
            return false;
        }
        public static bool TreeSet(ref TreeInstance __instance, Vector3 value)
        {
            if (AssetEditor) return true;
            short x, z; ushort y, flags;
            Encode(value, __instance.m_flags, out x, out y, out z, out flags);
            __instance.m_posX = x; __instance.m_posY = y; __instance.m_posZ = z; __instance.m_flags = flags;
            return false;
        }
        public static bool PropSet(ref PropInstance __instance, Vector3 value)
        {
            if (AssetEditor) return true;
            short x, z; ushort y, flags;
            Encode(value, __instance.m_flags, out x, out y, out z, out flags);
            __instance.m_posX = x; __instance.m_posY = y; __instance.m_posZ = z; __instance.m_flags = flags;
            return false;
        }
        public static int TreeX(ref TreeInstance data) { return Raw(data.m_posX, data.m_flags, XExtension); }
        public static int TreeZ(ref TreeInstance data) { return Raw(data.m_posZ, data.m_flags, ZExtension); }
        public static int PropX(ref PropInstance data) { return Raw(data.m_posX, data.m_flags, XExtension); }
        public static int PropZ(ref PropInstance data) { return Raw(data.m_posZ, data.m_flags, ZExtension); }
        static int Raw(short value, ushort flags, ushort marker)
        {
            if (AssetEditor) return value;
            var axis = new PackedObjectAxis { Value = value, PositiveExtension = (flags & marker) != 0 };
            PackedObjectPosition.Decode(axis);
            return value + (axis.PositiveExtension ? 65536 : 0);
        }
        static Vector3 Decode(short x, ushort y, short z, ushort flags)
        {
            return new Vector3(
                PackedObjectPosition.Decode(new PackedObjectAxis { Value = x, PositiveExtension = (flags & XExtension) != 0 }),
                y / 64f,
                PackedObjectPosition.Decode(new PackedObjectAxis { Value = z, PositiveExtension = (flags & ZExtension) != 0 }));
        }
        static void Encode(Vector3 value, ushort originalFlags, out short x, out ushort y, out short z, out ushort flags)
        {
            var ax = PackedObjectPosition.Encode(value.x); var az = PackedObjectPosition.Encode(value.z);
            if (float.IsNaN(value.y) || float.IsInfinity(value.y)) throw new ArgumentOutOfRangeException(nameof(value));
            y = (ushort)Math.Max(0, Math.Min(65535, Math.Round((double)(value.y * 64f), MidpointRounding.ToEven)));
            x = ax.Value; z = az.Value;
            flags = (ushort)((originalFlags & ~ExtensionMask) | (ax.PositiveExtension ? XExtension : 0) | (az.PositiveExtension ? ZExtension : 0));
        }

        public static IList<HookSpec> Specifications()
        {
            var result = new List<HookSpec>();
            foreach (Type type in new[] { typeof(TreeInstance), typeof(PropInstance) })
            {
                string prefix = type == typeof(TreeInstance) ? "Tree" : "Prop";
                var property = type.GetProperty("Position");
                if (property == null) throw new MissingMemberException(type.FullName, "Position");
                result.Add(new HookSpec { Target = property.GetGetMethod(), Prefix = typeof(ObjectPositionHooks).GetMethod(prefix + "Get") });
                result.Add(new HookSpec { Target = property.GetSetMethod(), Prefix = typeof(ObjectPositionHooks).GetMethod(prefix + "Set") });
            }
            return result;
        }
    }
}
