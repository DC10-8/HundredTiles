using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.IO;
using ColossalFramework.Plugins;
namespace HundredTiles.Profiles {
 public static class Selector {
  public static string Root {get{return Path.Combine(DataLocation.localApplicationData,"HundredTiles-profiles");}}
  static Assembly Runtime {get{foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="HundredTiles.Runtime")return a;throw new InvalidOperationException("HundredTiles runtime not loaded.");}}
  static int Current {get{foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="HundredTiles.Core")return (int)a.GetType("HundredTiles.Core.ExpandedMap").GetField("TileCount").GetRawConstantValue();throw new InvalidOperationException("HundredTiles core not loaded.");}}
  static int Wanted {get{string path=Path.Combine(Root,"selected.txt");if(!File.Exists(path))return Current;string text=File.ReadAllText(path).Trim();if(text=="editor")return -1;if(text=="100")return 100;if(text=="324")return 324;throw new InvalidOperationException("Invalid HundredTiles profile selection.");}}
  // Tile-count switching uses the helper and the stored bundles of a full profile-switch install. A Workshop copy has
  // neither, so it carries both builds in HundredTilesProfiles\{100,324} as .bin files (the game loads every *.dll below a
  // mod folder) and replaces its DLLs when the game quits. Replacing them earlier would make the game hot-reload the mod.
  // A copy with neither marks its own build and the unavailable one instead of failing on selection.
  static bool HasHelper(int tiles){string root=Root;return File.Exists(Path.Combine(root,"HundredTiles.ProfileSwitch.exe")) && File.Exists(Path.Combine(Path.Combine(root,tiles.ToString()),"files.tsv"));}
  static bool CanSwitchTo(int tiles){return tiles==Current || HasHelper(tiles) || QuitSwitch.Kit(tiles)!=null;}
  static void ArmPending(){try{int wanted=Wanted;if(wanted>0 && wanted!=Current && !HasHelper(wanted) && QuitSwitch.Kit(wanted)!=null)QuitSwitch.Arm(wanted);}catch(Exception e){UnityEngine.Debug.LogWarning("[HundredTiles] Profile switch on quit not armed: "+e.Message);}}
  static string Mark(int tiles,bool ja){if(tiles==Current)return ja?"（このビルド）":" (this build)";return CanSwitchTo(tiles)?"":ja?"（この配布では切替不可）":" (not in this download)";}
  public static string[] Items(){ArmPending();bool ja=(int)Runtime.GetType("HundredTiles.Runtime.UiLanguage").GetProperty("Current").GetValue(null,null)==0;return ja?new[]{"都市（324タイル）"+Mark(324,true),"都市（100タイル）"+Mark(100,true),"エディタ（通常サイズ）"}:new[]{"City (324 tiles)"+Mark(324,false),"City (100 tiles)"+Mark(100,false),"Editor (normal grids)"};}
  static bool Japanese {get{return (int)Runtime.GetType("HundredTiles.Runtime.UiLanguage").GetProperty("Current").GetValue(null,null)==0;}}
  public static string UnlockLabel(string ja,string en){ArmPending();int wanted=Wanted;if(wanted>0 && wanted!=Current)return Japanese?wanted+"タイルへの切替待ち（ゲームを完全終了）":"Switch to "+wanted+" tiles pending (fully exit the game)";return Japanese?Current+"タイルをすべて開放（無料）":"Unlock all "+Current+" tiles (free)";}
  public static void RequestUnlock(Action<string> status){if(!BundleMatches(Wanted,Current)){status(Japanese?"切替待ちです。ゲームを完全終了し、切替完了後に再起動してください":"Profile change pending. Fully exit, wait for completion, then restart.");return;}Runtime.GetType("HundredTiles.Runtime.BulkUnlock").GetMethod("Request").Invoke(null,new object[]{status});}
  static void Refresh(){var type=Runtime.GetType("HundredTiles.Runtime.LanguageView");if(type==null)return;var view=UnityEngine.Object.FindObjectOfType(type);if(view!=null)type.GetMethod("RefreshNow").Invoke(view,null);}
  public static int Selected(){int wanted=Wanted;return wanted<0?2:wanted==100?1:0;}
  public static bool BundleMatches(int wanted,int current){return wanted<0 || wanted==current;}
  public static void AssertBundle(){int wanted=Wanted;if(!BundleMatches(wanted,Current))throw new InvalidOperationException("HundredTiles profile change is pending or failed. Fully exit the game, wait for the profile switch to finish, then restart. See HundredTiles-profiles/switch-status.txt.");}
  public static void Set(int value){if(value<0 || value>2)throw new ArgumentOutOfRangeException("value");int wanted=value==2?-1:value==1?100:324;
   string profileRoot=Root;
   if(wanted>0 && !CanSwitchTo(wanted)){Refresh();string text=Japanese?"このHundredTilesは"+Current+"タイル版です。"+wanted+"タイルで遊ぶには、"+wanted+"タイル版のHundredTilesが必要です（この配布にはタイル数の切替機能が含まれていません）。設定は変更していません。":"This copy of HundredTiles is the "+Current+"-tile build. Playing with "+wanted+" tiles needs the "+wanted+"-tile build of HundredTiles (this download does not include tile-count switching). The setting was not changed.";
    UnityEngine.Debug.Log("[HundredTiles] "+text);ColossalFramework.UI.UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("HundredTiles",text,false);return;}
   Directory.CreateDirectory(profileRoot);string token=Guid.NewGuid().ToString("N");
   bool onQuit=wanted>0 && wanted!=Current && !HasHelper(wanted);
   if(wanted>0 && !onQuit){string folder=Path.Combine(profileRoot,wanted.ToString());if(!File.Exists(Path.Combine(folder,"files.tsv")))throw new InvalidOperationException("Matching profile bundle is missing. Reinstall the complete profile-switch package.");}
   // Keep the legacy city/editor setting valid for SessionGate. Tile selection
   // changes only the bundle for the next process, never the current grids.
   Runtime.GetType("HundredTiles.Runtime.StartupProfile").GetMethod("Set").Invoke(null,new object[]{wanted<0?1:0});
   File.WriteAllText(Path.Combine(profileRoot,"selected.txt"),wanted<0?"editor":wanted.ToString());
   File.WriteAllText(Path.Combine(profileRoot,"request.txt"),token+"\n"+(wanted<0?"editor":wanted.ToString()));
   Refresh();
   if(wanted<0 || wanted==Current){File.WriteAllText(Path.Combine(profileRoot,"switch-status.txt"),"Selection saved; restart the game to apply.");return;}
   if(onQuit){QuitSwitch.Arm(wanted);File.WriteAllText(Path.Combine(profileRoot,"switch-status.txt"),"Waiting for Cities: Skylines to quit. The "+wanted+"-tile build replaces the mod files when the game closes.");
    string note=Japanese?wanted+"タイル版に切り替えます。ゲームを終了すると、そのときにMODのファイルが入れ替わり、次の起動から"+wanted+"タイルになります。それまでは都市を読み込めません。":"Switching to the "+wanted+"-tile build. The mod files are replaced when you quit the game, and the next start uses "+wanted+" tiles. Cities cannot be loaded until then.";
    UnityEngine.Debug.Log("[HundredTiles] "+note);ColossalFramework.UI.UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("HundredTiles",note,false);return;}
   string helper=Path.Combine(profileRoot,"HundredTiles.ProfileSwitch.exe");if(!File.Exists(helper))throw new InvalidOperationException("Profile switch helper is missing.");
   var process=Process.GetCurrentProcess();var start=new ProcessStartInfo(helper,"wait "+process.Id+" "+process.StartTime.ToUniversalTime().Ticks+" "+token+" \""+profileRoot+"\"");start.UseShellExecute=false;start.CreateNoWindow=true;start.WindowStyle=ProcessWindowStyle.Hidden;Process.Start(start);
   File.WriteAllText(Path.Combine(profileRoot,"switch-status.txt"),"Waiting for Cities: Skylines to exit. Keep the game closed until switching is complete.");
  }
 }
}
