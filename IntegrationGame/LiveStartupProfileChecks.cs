using System;
using System.IO;
using ColossalFramework.IO;
using ColossalFramework.UI;
using HundredTiles.Runtime;
using UnityEngine;

static class LiveStartupProfileChecks
{
    public static void Run(bool editor)
    {
        var panel=(UIPanel)UIView.GetAView().AddUIComponent(typeof(UIPanel));panel.width=700;panel.height=900;
        int originalLanguage=UiLanguage.Current;
        try
        {
            new Mod().OnSettingsUI(new UIHelper(panel));
            UIDropDown profile=null;
            foreach(var dropdown in panel.GetComponentsInChildren<UIDropDown>(true))if(dropdown.name=="HundredTiles.StartupProfile")profile=dropdown;
            int expected=editor?1:0;
            if(profile==null || profile.selectedIndex!=expected)throw new Exception("Startup profile dropdown state incorrect.");
            UiLanguage.Set(1);
            if(profile.items[1]!="Editor (normal grids)" || profile.selectedIndex!=expected || StartupProfile.Selected!=expected)throw new Exception("Profile language switch changed selection.");
            UiLanguage.Set(0);
            if(profile.items[0]!="都市（"+HundredTiles.Core.ExpandedMap.TileCount+"タイル）" || profile.selectedIndex!=expected)throw new Exception("Japanese profile label incorrect.");
            profile.selectedIndex=1-expected;
            string path=Path.Combine(DataLocation.localApplicationData,"HundredTiles.startup-profile.txt");
            if(StartupProfile.Selected!=1-expected || File.ReadAllText(path).Trim()!=(editor?"city":"editor"))throw new Exception("Profile change was not persisted.");
            if(Installer.IsInstalled==editor)throw new Exception("Changing a setting mutated runtime patches.");
            profile.selectedIndex=expected;
            if(File.ReadAllText(path).Trim()!=(editor?"editor":"city"))throw new Exception("Profile setting restoration failed.");
            Entry.Log("PASS: real options profile dropdown persisted both choices; Japanese/English labels update without altering selection; live city/editor patches remain unchanged.");
        }
        finally{UiLanguage.Set(originalLanguage);UnityEngine.Object.Destroy(panel.gameObject);}
    }
}
