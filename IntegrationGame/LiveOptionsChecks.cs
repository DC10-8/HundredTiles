using System;
using System.Collections;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;

static class LiveOptionsChecks
{
    static volatile bool seeded;
    static T Named<T>(UIPanel panel,string name) where T:UIComponent{foreach(var component in panel.GetComponentsInChildren<T>(true))if(component.name==name)return component;return null;}
    public static IEnumerator Run()
    {
        Assembly runtime=null;
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var installer=assembly.GetType("HundredTiles.Runtime.Installer");
            if(installer!=null && (bool)installer.GetProperty("IsInstalled").GetValue(null,null)){runtime=assembly;break;}
        }
        if(runtime==null)throw new Exception("Actual enabled plugin assembly missing.");
        var languageType=runtime.GetType("HundredTiles.Runtime.UiLanguage");languageType.GetMethod("Set").Invoke(null,new object[]{0});
        var panel=(UIPanel)UIView.GetAView().AddUIComponent(typeof(UIPanel));panel.width=700;panel.height=650;
        runtime.GetType("HundredTiles.Runtime.Mod").GetMethod("OnSettingsUI").Invoke(Activator.CreateInstance(runtime.GetType("HundredTiles.Runtime.Mod")),new object[]{new UIHelper(panel)});
        var button=Named<UIButton>(panel,"HundredTiles.Unlock");
        var dropdown=Named<UIDropDown>(panel,"HundredTiles.Language");var generate=Named<UIButton>(panel,"HundredTiles.Generate");var repair=Named<UIButton>(panel,"HundredTiles.Repair");if(dropdown==null || generate==null || repair==null)throw new Exception("Language controls missing.");
        dropdown.selectedIndex=1;if(button.text!="Unlock all 100 tiles (free)" || repair.text!="Repair river connections and increase forests" || generate.text.IndexOf("Generate hills",StringComparison.Ordinal)!=0)throw new Exception("Immediate English switch failed.");
        var labels=panel.GetComponentsInChildren<UILabel>(true);bool tileHeading=false,natureHeading=false;foreach(var label in labels){if(label.text=="Tile unlock (run after loading a city)")tileHeading=true;if(label.text=="Nature in the added area (save the city afterwards)")natureHeading=true;}if(!tileHeading || !natureHeading)throw new Exception("Group headings did not switch to English.");
        if(System.IO.File.ReadAllText(System.IO.Path.Combine(ColossalFramework.IO.DataLocation.localApplicationData,"HundredTiles.language.txt")).Trim()!="en")throw new Exception("Language preference was not saved.");
        var translate=languageType.GetMethod("Status");if((string)translate.Invoke(null,new object[]{"開放に失敗しました。ゲームログを確認してください"})!="Unlock failed. Check the game log." || (string)translate.Invoke(null,new object[]{"自然の生成・修復完了（川の接続 2、追加樹木 35本、水源 4、保護物で見送り 1）"})!="Nature complete: river connections 2, trees added 35, water sources 4, protected crossings skipped 1.")throw new Exception("Error/result localization failed.");
        var reopened=(UIPanel)UIView.GetAView().AddUIComponent(typeof(UIPanel));runtime.GetType("HundredTiles.Runtime.Mod").GetMethod("OnSettingsUI").Invoke(Activator.CreateInstance(runtime.GetType("HundredTiles.Runtime.Mod")),new object[]{new UIHelper(reopened)});if(Named<UIDropDown>(reopened,"HundredTiles.Language").selectedIndex!=1)throw new Exception("Language preference not retained on reopen.");UnityEngine.Object.Destroy(reopened.gameObject);yield return null;
        dropdown.selectedIndex=0;
        if(button==null || button.text!="100タイルをすべて開放（無料）")throw new Exception("Actual options button missing.");
        seeded=false;
        Singleton<SimulationManager>.instance.AddAction(delegate {
            var areas=Singleton<GameAreaManager>.instance;
            Array.Clear(areas.m_areaGrid,0,100);areas.m_areaGrid[44]=1;areas.m_areaCount=1;
            // Recreate a consistent one-tile city: ownership and detailed
            // terrain must both be reset in this disposable verification.
            var terrain=Singleton<TerrainManager>.instance;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
            typeof(TerrainManager).GetField("m_detailPatchCount",flags).SetValue(terrain,0);
            var patches=(TerrainPatch[])typeof(TerrainManager).GetField("m_patches",flags).GetValue(terrain);
            foreach(var patch in patches)patch.m_simDetailIndex=0;
            if(!terrain.SetDetailedPatch(4,4))throw new Exception("One-tile detailed terrain seed failed.");
            seeded=true;
        });
        float deadline=Time.realtimeSinceStartup+30;
        while(!seeded && Time.realtimeSinceStartup<deadline)yield return null;
        if(!seeded)throw new Exception("Options seed simulation action timed out.");
        var hold=new System.Threading.ManualResetEvent(false);Singleton<SimulationManager>.instance.AddAction(delegate{hold.WaitOne(10000,false);});
        Entry.Log("Before unlock click: "+button.text+"; enabled="+button.isEnabled+"; visible="+button.isVisible);button.SimulateClick();button.SimulateClick();
        try{dropdown.selectedIndex=1;if(button.text!="Unlocking all tiles…")throw new Exception("Pending unlock status did not switch instantly: "+button.text+"; language="+dropdown.selectedIndex+"; enabled="+button.isEnabled+"; visible="+button.isVisible);dropdown.selectedIndex=0;}finally{hold.Set();}
        deadline=Time.realtimeSinceStartup+60;
        while(button.text!="100タイルすべて開放済みです" && Time.realtimeSinceStartup<deadline)yield return null;
        var manager=Singleton<GameAreaManager>.instance;
        if(manager.m_areaCount!=100 || button.text!="100タイルすべて開放済みです")throw new Exception("Options unlock failed: "+button.text+" / "+manager.m_areaCount);
        hold.Close();
        foreach(int area in manager.m_areaGrid)if(area==0)throw new Exception("Unowned tile after options unlock.");
        button.SimulateClick();
        deadline=Time.realtimeSinceStartup+10;
        while(button.text!="100タイルすべて開放済みです" && Time.realtimeSinceStartup<deadline)yield return null;
        if(manager.m_areaCount!=100 || button.text!="100タイルすべて開放済みです")throw new Exception("Repeated options click failed.");
        dropdown.selectedIndex=1;if(button.text!="All 100 tiles are unlocked.")throw new Exception("Completed status did not switch instantly.");dropdown.selectedIndex=0;
        Entry.Log("PASS: real language dropdown switches all action labels and pending/completed states instantly; reopening retains preference; reverse switch preserves completed state.");
        Entry.Log("PASS: actual OnSettingsUI button clicked; 99 native unlock transactions from one owned tile; all 100 tiles owned; duplicate/repeated clicks completed safely.");
        UnityEngine.Object.Destroy(panel.gameObject);Application.Quit();
    }
}







