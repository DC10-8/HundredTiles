using System;
using ICities;
using CitiesHarmony.API;
using UnityEngine;
using ColossalFramework.UI;
namespace HundredTiles.Runtime
{
 public sealed class Mod : IUserMod
 {
  static bool enabled;
  public string Name {get{return "HundredTiles 1.0";}}
  public string Description {get{return UiLanguage.Text("標準サイズ10×10の都市領域。Harmonyが必要です。オプションの起動用途を「エディタ」に変更して再起動すると、MODを有効にしたままエディタを使えます。","Standard-sized 10×10 city area. Requires Harmony. Select the Editor startup profile in options and restart to use editors with the mod enabled.");}}
  public void OnEnabled(){enabled=true;HarmonyHelper.DoOnHarmonyReady(delegate{if(!enabled)return;try{SessionGate.Enable();if(StartupProfile.Selected==0)Installer.Install();}catch(Exception error){Installer.FailureReason=error.Message;Debug.LogError("[HundredTiles] "+error);}});}
  public void OnDisabled(){enabled=false;SessionGate.Disable();}
  static void ButtonLayout(UIButton button,UIComponent root){button.autoSize=false;button.width=Math.Max(300,root.width-32);button.height=48;button.wordWrap=true;}
  static UIHelperBase Group(UIHelperBase helper,LanguageView view,string ja,string en){var group=helper.AddGroup(UiLanguage.Text(ja,en));var content=((UIHelper)group).self as UIComponent;var label=content.parent.Find<UILabel>("Label");if(label!=null)view.Bind(delegate{if(label!=null)label.text=UiLanguage.Text(ja,en);});return group;}
  public void OnSettingsUI(UIHelperBase helper)
  {
   var root=((UIHelper)helper).self as UIComponent;var view=root.gameObject.AddComponent<LanguageView>();
   var language=(UIDropDown)helper.AddDropdown("Language / 言語",new[]{"日本語","English"},UiLanguage.Current,delegate(int value){UiLanguage.Set(value);});language.name="HundredTiles.Language";
   var languageLabel=language.parent.Find<UILabel>("Label");if(languageLabel!=null)view.Bind(delegate{if(languageLabel!=null)languageLabel.text=UiLanguage.Text("言語 / Language","Language / 言語");});
   view.Bind(delegate{if(language!=null && language.selectedIndex!=UiLanguage.Current)language.selectedIndex=UiLanguage.Current;});
   var profileGroup=Group(helper,view,"起動用途（変更後はゲームを再起動）","Startup profile (restart after changing)");
   bool bindingProfile=false;var profile=(UIDropDown)profileGroup.AddDropdown(UiLanguage.Text("用途","Profile"),new[]{UiLanguage.Text("都市（100タイル）","City (100 tiles)"),UiLanguage.Text("エディタ（通常サイズ）","Editor (normal grids)")},StartupProfile.Selected,delegate(int value){if(bindingProfile || value<0 || value>1)return;try{StartupProfile.Set(value);}catch(Exception error){Debug.LogError("[HundredTiles] Startup profile save failed: "+error);UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("HundredTiles",UiLanguage.Text("設定を保存できませんでした。\n","Could not save the setting.\n")+error.Message,false);}});profile.name="HundredTiles.StartupProfile";
   var profileLabel=profile.parent.Find<UILabel>("Label");view.Bind(delegate{if(profileLabel!=null)profileLabel.text=UiLanguage.Text("用途","Profile");if(profile!=null){bindingProfile=true;try{profile.items=new[]{UiLanguage.Text("都市（100タイル）","City (100 tiles)"),UiLanguage.Text("エディタ（通常サイズ）","Editor (normal grids)")};profile.selectedIndex=StartupProfile.Selected;}finally{bindingProfile=false;}}});
   var traffic=Group(helper,view,"外部の通過交通（需要に関係なく発生）","Outside through traffic (independent of demand)");
   var automatic=(UICheckBox)traffic.AddCheckbox(UiLanguage.Text("読み込み・建物の追加時に少量ずつ自動発生","Automatically request a small wave after loading or adding buildings"),OutsideTraffic.Automatic,delegate(bool value){OutsideTraffic.SetAutomatic(value);});
   view.Bind(delegate{var label=automatic.Find<UILabel>("Label");if(label!=null)label.text=UiLanguage.Text("読み込み・建物の追加時に少量ずつ自動発生","Automatically request a small wave after loading or adding buildings");});
   UIButton trafficButton=null;trafficButton=(UIButton)traffic.AddButton(UiLanguage.Text("車・列車の発生を今すぐ依頼","Request cars and trains now"),delegate{OutsideTraffic.Request(delegate(string value){if(trafficButton!=null)trafficButton.text=value;});});ButtonLayout(trafficButton,root);view.Bind(delegate{if(trafficButton!=null)trafficButton.text=UiLanguage.Text("車・列車の発生を今すぐ依頼","Request cars and trains now");});
   var group=Group(helper,view,"タイルの開放（都市を読み込んでから実行）","Tile unlock (run after loading a city)");string unlockStatus=null;UIButton unlock=null;
   unlock=(UIButton)group.AddButton(UiLanguage.Text("100タイルをすべて開放（無料）","Unlock all 100 tiles (free)"),delegate{BulkUnlock.Request(delegate(string message){unlockStatus=message;if(unlock!=null)unlock.text=UiLanguage.Status(message);});});unlock.name="HundredTiles.Unlock";ButtonLayout(unlock,root);
   view.Bind(delegate{if(unlock!=null)unlock.text=unlockStatus==null?UiLanguage.Text("100タイルをすべて開放（無料）","Unlock all 100 tiles (free)"):UiLanguage.Status(unlockStatus);});
   if(!string.IsNullOrEmpty(Installer.FailureReason))Group(group,view,"起動失敗: "+Installer.FailureReason,"Startup error: "+Installer.FailureReason);
   var landscape=Group(helper,view,"拡張部分の自然（生成後は都市を保存してください）","Nature in the added area (save the city afterwards)");string generateStatus=null,repairStatus=null;UIButton generate=null,repair=null;
#if HUNDREDTILES_324
   UIButton undo=null;undo=(UIButton)landscape.AddButton(UiLanguage.Text("地形編集を1回戻す（費用は返金されません）","Undo one terrain stroke (costs are not refunded)"),delegate{CityTerrainUndo.Request(delegate(string message){if(undo!=null)undo.text=message;});});undo.name="HundredTiles.TerrainUndo";ButtonLayout(undo,root);view.Bind(delegate{if(undo!=null)undo.text=UiLanguage.Text("地形編集を1回戻す（費用は返金されません）","Undo one terrain stroke (costs are not refunded)");});
#endif
   generate=(UIButton)landscape.AddButton(UiLanguage.Text("追加19タイルに起伏・川・森を生成","Generate hills, rivers and forests in the 19 added tiles"),delegate{LandscapeGeneration.Request(delegate(string message){generateStatus=message;if(generate!=null)generate.text=UiLanguage.Text("追加19タイルに起伏・川・森を生成","Generate hills, rivers and forests in the 19 added tiles")+"\n"+UiLanguage.Status(message);});});generate.name="HundredTiles.Generate";ButtonLayout(generate,root);generate.height=96;
   repair=(UIButton)landscape.AddButton(UiLanguage.Text("川の接続を修復して森林を増やす","Repair river connections and increase forests"),delegate{LandscapeGeneration.Request(delegate(string message){repairStatus=message;if(repair!=null)repair.text=UiLanguage.Text("川の接続を修復して森林を増やす","Repair river connections and increase forests")+"\n"+UiLanguage.Status(message);});});repair.name="HundredTiles.Repair";ButtonLayout(repair,root);repair.height=96;
   view.Bind(delegate{if(generate!=null)generate.text=generateStatus==null?UiLanguage.Text("追加19タイルに起伏・川・森を生成","Generate hills, rivers and forests in the 19 added tiles"):UiLanguage.Text("追加19タイルに起伏・川・森を生成","Generate hills, rivers and forests in the 19 added tiles")+"\n"+UiLanguage.Status(generateStatus);if(repair!=null)repair.text=repairStatus==null?UiLanguage.Text("川の接続を修復して森林を増やす","Repair river connections and increase forests"):UiLanguage.Text("川の接続を修復して森林を増やす","Repair river connections and increase forests")+"\n"+UiLanguage.Status(repairStatus);});
  }
 }
}

