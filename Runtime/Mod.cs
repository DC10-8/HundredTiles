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
  public string Description {get{return UiLanguage.Text("標準サイズ10×10の都市領域。Harmonyが必要です。有効化・無効化後はゲームを再起動してください。エディターは非対応です。","Standard-sized 10×10 city area. Requires Harmony. Restart after enabling or disabling. Editors are not supported.");}}
  public void OnEnabled(){enabled=true;HarmonyHelper.DoOnHarmonyReady(delegate{if(!enabled)return;try{Installer.Install();}catch(Exception error){Installer.FailureReason=error.Message;Debug.LogError("[HundredTiles] "+error);}});}
  public void OnDisabled(){enabled=false;Installer.Remove();}
  static void ButtonLayout(UIButton button,UIComponent root){button.autoSize=false;button.width=Math.Max(300,root.width-32);button.height=48;button.wordWrap=true;}
  static UIHelperBase Group(UIHelperBase helper,LanguageView view,string ja,string en){var group=helper.AddGroup(UiLanguage.Text(ja,en));var content=((UIHelper)group).self as UIComponent;var label=content.parent.Find<UILabel>("Label");if(label!=null)view.Bind(delegate{if(label!=null)label.text=UiLanguage.Text(ja,en);});return group;}
  public void OnSettingsUI(UIHelperBase helper)
  {
   var root=((UIHelper)helper).self as UIComponent;var view=root.gameObject.AddComponent<LanguageView>();
   var language=(UIDropDown)helper.AddDropdown("Language / 言語",new[]{"日本語","English"},UiLanguage.Current,delegate(int value){UiLanguage.Set(value);});language.name="HundredTiles.Language";
   var languageLabel=language.parent.Find<UILabel>("Label");if(languageLabel!=null)view.Bind(delegate{if(languageLabel!=null)languageLabel.text=UiLanguage.Text("言語 / Language","Language / 言語");});
   view.Bind(delegate{if(language!=null && language.selectedIndex!=UiLanguage.Current)language.selectedIndex=UiLanguage.Current;});
   var group=Group(helper,view,"タイルの開放（都市を読み込んでから実行）","Tile unlock (run after loading a city)");string unlockStatus=null;UIButton unlock=null;
   unlock=(UIButton)group.AddButton(UiLanguage.Text("100タイルをすべて開放（無料）","Unlock all 100 tiles (free)"),delegate{BulkUnlock.Request(delegate(string message){unlockStatus=message;if(unlock!=null)unlock.text=UiLanguage.Status(message);});});unlock.name="HundredTiles.Unlock";ButtonLayout(unlock,root);
   view.Bind(delegate{if(unlock!=null)unlock.text=unlockStatus==null?UiLanguage.Text("100タイルをすべて開放（無料）","Unlock all 100 tiles (free)"):UiLanguage.Status(unlockStatus);});
   if(!string.IsNullOrEmpty(Installer.FailureReason))Group(group,view,"起動失敗: "+Installer.FailureReason,"Startup error: "+Installer.FailureReason);
   var landscape=Group(helper,view,"拡張部分の自然（生成後は都市を保存してください）","Nature in the added area (save the city afterwards)");string generateStatus=null,repairStatus=null;UIButton generate=null,repair=null;
   generate=(UIButton)landscape.AddButton(UiLanguage.Text("追加19タイルに起伏・川・森を生成","Generate hills, rivers and forests in the 19 added tiles"),delegate{LandscapeGeneration.Request(delegate(string message){generateStatus=message;if(generate!=null)generate.text=UiLanguage.Status(message);});});generate.name="HundredTiles.Generate";ButtonLayout(generate,root);
   repair=(UIButton)landscape.AddButton(UiLanguage.Text("川の接続を修復して森林を増やす","Repair river connections and increase forests"),delegate{LandscapeGeneration.Request(delegate(string message){repairStatus=message;if(repair!=null)repair.text=UiLanguage.Status(message);});});repair.name="HundredTiles.Repair";ButtonLayout(repair,root);
   view.Bind(delegate{if(generate!=null)generate.text=generateStatus==null?UiLanguage.Text("追加19タイルに起伏・川・森を生成","Generate hills, rivers and forests in the 19 added tiles"):UiLanguage.Status(generateStatus);if(repair!=null)repair.text=repairStatus==null?UiLanguage.Text("川の接続を修復して森林を増やす","Repair river connections and increase forests"):UiLanguage.Status(repairStatus);});
  }
 }
}

