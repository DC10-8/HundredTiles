using System;using System.Collections.Generic;using System.Reflection;using ColossalFramework;using ColossalFramework.Threading;using HarmonyLib;using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 // The save list reads each save through the offsets noted when its package was loaded. Overwriting a save swaps the file
 // before the file watcher replaces the package, and a refresh in that gap reads past the end of a smaller file, which
 // shows "'name' failed to load." These panels now wait until saving ends, when the watcher has had time to catch up.
 public static class SaveListGuard {
  const string Owner="HundredTiles.AutoAdapt";
  const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
  static readonly List<object> held=new List<object>();

  public static string Apply(){var harmony=new Harmony(Owner);var hold=new HarmonyMethod(typeof(SaveListGuard).GetMethod("Hold"));
   harmony.Patch(typeof(LoadPanel).GetMethod("Refresh",Instance),prefix:hold);harmony.Patch(typeof(SavePanel).GetMethod("Refresh",Instance),prefix:hold);
   harmony.Patch(typeof(SavePanel).GetMethod("OnSavingComplete",Instance),postfix:new HarmonyMethod(typeof(SaveListGuard).GetMethod("Release")));
   return "[HundredTiles] Save list refresh waits for saving to finish.";}

  public static bool Hold(object __instance){if(!SavePanel.isSaving)return true;lock(held)if(!held.Contains(__instance))held.Add(__instance);return false;}

  // Runs on the packaging thread, one second after the file was written. Dispatched after the watcher's own updates.
  public static void Release(bool quitAfterSaving){try{object[] panels;lock(held){panels=held.ToArray();held.Clear();}if(panels.Length==0 || quitAfterSaving)return;
    ThreadHelper.dispatcher.Dispatch(delegate{Refresh(panels);});}
   catch(Exception e){ChangedMods.Warn("Save list refresh not resumed: "+e.Message);}}

  public static void Refresh(object[] panels){foreach(object panel in panels){var behaviour=panel as MonoBehaviour;if(behaviour==null || !behaviour.enabled)continue;
    try{panel.GetType().GetMethod("Refresh",Instance).Invoke(panel,null);}catch(Exception e){ChangedMods.Warn("Save list refresh failed: "+e.Message);}}}
 }
}
