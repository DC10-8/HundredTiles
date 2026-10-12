using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ColossalFramework;
using ICities;
using UnityEngine;

namespace HundredTiles.Diagnostics
{
    public sealed class Mod : IUserMod
    {
        public string Name { get { return "HundredTiles Development Diagnostics"; } }
        public string Description { get { return "100タイル開発用の読み取り専用診断。マップは拡張しません。"; } }
    }

    public sealed class Loading : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            // Captures immutable dimensions/references only, never cell contents.
            // No scene components, patches, saved data or simulation arrays are changed.
            try
            {
                var report = new StringBuilder();
                Assembly game = typeof(TerrainManager).Assembly;
                report.AppendLine("HundredTiles runtime baseline; no expansion patches installed.");
                report.AppendLine("Mode: " + mode);
                report.AppendLine("Assembly version: " + game.GetName().Version);
                report.AppendLine("MVID: " + game.ManifestModule.ModuleVersionId);
                using (var hash = SHA256.Create())
                using (var file = File.OpenRead(game.Location))
                    report.AppendLine("SHA256: " + BitConverter.ToString(hash.ComputeHash(file)).Replace("-", ""));
                object terrain = Singleton<TerrainManager>.instance;
                object areas = Singleton<GameAreaManager>.instance;
                object water = Field(terrain, "m_waterSimulation");
                AppendFields(report, terrain, new[] { "m_rawHeights", "m_rawHeights2", "m_finalHeights", "m_blockHeights", "m_blockHeights2", "m_rawSurface", "m_patches", "m_detailHeights", "m_detailSurface", "m_detailZones", "m_rawBounds" });
                AppendFields(report, areas, new[] { "m_areaGrid" });
                if (water != null)
                    AppendFields(report, water, new[] { "m_waterBuffers", "m_heightBuffer", "m_heightMaps", "m_surfaceMapsA", "m_surfaceMapsB", "m_heightBounds", "m_waterExists1", "m_waterExists2", "m_waterExists3", "m_bitCells" });
                else report.AppendLine("WaterSimulation: unavailable");
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Colossal Order\\Cities_Skylines\\HundredTilesDiagnostics");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "baseline-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
                File.WriteAllText(path, report.ToString(), Encoding.UTF8);
                Debug.Log("[HundredTiles.Diagnostics] Runtime baseline written: " + path);
            }
            catch (Exception e) { Debug.LogError("[HundredTiles.Diagnostics] Baseline failed: " + e); }
        }

        static object Field(object instance, string name)
        {
            if (instance == null) return null;
            Type type = instance.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(instance);
                type = type.BaseType;
            }
            throw new MissingFieldException(instance.GetType().FullName, name);
        }

        static void AppendFields(StringBuilder report, object instance, string[] names)
        {
            foreach (string name in names)
            {
                try
                {
                    object value = Field(instance, name);
                    report.Append(instance.GetType().Name).Append('.').Append(name).Append(": ");
                    Array array = value as Array;
                    if (array != null)
                    {
                        report.Append(array.Length);
                        Type element = array.GetType().GetElementType();
                        if (element != null && element.IsArray)
                        {
                            report.Append(" [");
                            for (int i = 0; i < array.Length; i++)
                            {
                                if (i > 0) report.Append(',');
                                Array child = array.GetValue(i) as Array;
                                report.Append(child == null ? "null" : child.Length.ToString());
                            }
                            report.Append(']');
                        }
                    }
                    else report.Append(value == null ? "null" : value.ToString());
                    report.AppendLine();
                }
                catch (MissingFieldException) { report.AppendLine(instance.GetType().Name + "." + name + ": MISSING (game version mismatch)"); }
            }
        }
    }
}
