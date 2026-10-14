using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ColossalFramework.IO;
using UnityEngine;
namespace HundredTiles.Runtime
{
 public static class UiLanguage
 {
  static int language=-1;
  public static event Action Changed;
  static string SettingsPath {get{return Path.Combine(DataLocation.localApplicationData,"HundredTiles.language.txt");}}
  public static int Current {get{if(language<0){language=0;try{if(File.Exists(SettingsPath) && File.ReadAllText(SettingsPath).Trim()=="en")language=1;}catch(Exception error){Debug.LogWarning("[HundredTiles] Language settings: "+error.Message);}}return language;}}
  public static void Set(int value){if(value!=0 && value!=1)throw new ArgumentOutOfRangeException("value");if(Current==value)return;language=value;try{File.WriteAllText(SettingsPath,value==0?"ja":"en");}catch(Exception error){Debug.LogWarning("[HundredTiles] Language preference was not saved: "+error.Message);}var notify=Changed;if(notify!=null)notify();}
  static string Layout(string value){string count=HundredTiles.Core.ExpandedMap.TileCount.ToString(),side=HundredTiles.Core.ExpandedMap.GridSize.ToString();return value.Replace("100タイル",count+"タイル").Replace("100 tiles",count+" tiles").Replace("10×10",side+"×"+side).Replace(" / 100"," / "+count);}
  public static string Text(string japanese,string english){return Layout(Current==0?japanese:english);}
  static readonly Dictionary<string,string> Messages=new Dictionary<string,string>{
   {"MODの起動に失敗しています。起動失敗の詳細を確認してください","The mod failed to start. Check the startup error below."},
   {"都市を読み込んでから押してください","Load a city before running this action."},
   {"全タイルを開放中です…","Unlocking all tiles…"},
   {"100タイルすべて開放済みです","All 100 tiles are unlocked."},
   {"開放に失敗しました。ゲームログを確認してください","Unlock failed. Check the game log."},
   {"開放処理を開始できませんでした","Could not start unlocking."},
   {"拡張部分の自然を生成中です…","Generating nature in the added area…"},
   {"生成に失敗しました。ゲームログを確認してください","Generation failed. Check the game log."},
   {"生成処理を開始できませんでした","Could not start generation."},
   {"この都市の自然は生成済みです","Nature has already been generated for this city."}
  };
  // Status callbacks retain their canonical messages so a completed or running
  // action can be re-rendered instantly without starting it again.
  public static string Status(string message){if(Current==0)return Layout(message);string value;if(Messages.TryGetValue(message,out value))return Layout(value);
   var partial=Regex.Match(message,@"^開放できた区画: (\d+) / 100（再実行できます）$");if(partial.Success)return Layout("Unlocked tiles: "+partial.Groups[1].Value+" / 100 (you can retry).");
   var nature=Regex.Match(message,@"^自然の生成・修復完了（川の接続 (\d+)、追加樹木 (\d+)本、水源 (\d+)、保護物で見送り (\d+)）$");if(nature.Success)return "Nature complete: river connections "+nature.Groups[1].Value+", trees added "+nature.Groups[2].Value+", water sources "+nature.Groups[3].Value+", protected crossings skipped "+nature.Groups[4].Value+".";
   return message;
  }
 }
 public sealed class LanguageView : MonoBehaviour
 {
  readonly List<Action> refreshers=new List<Action>();
  void Awake(){UiLanguage.Changed+=Refresh;}
  void OnDestroy(){UiLanguage.Changed-=Refresh;}
  public void Bind(Action refresh){refreshers.Add(refresh);refresh();}
  void Refresh(){foreach(var refresh in refreshers)refresh();}
 }
}
