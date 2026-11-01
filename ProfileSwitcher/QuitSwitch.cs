using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using ColossalFramework.Plugins;
using UnityEngine;
namespace HundredTiles.Profiles {
 // Profile switch for copies without the external helper (Workshop downloads). The game loads mod DLLs from bytes, so
 // the files are not locked, but it hot-reloads a mod whose DLL changes while running. The bundle shipped in
 // <mod>\HundredTilesProfiles\<tiles> is therefore written only in OnApplicationQuit, after the last frame.
 public sealed class QuitSwitch : MonoBehaviour {
  public const string KitFolder="HundredTilesProfiles";
  static readonly string[] Names={"CitiesHarmony.API.dll","HundredTiles.Core.dll","HundredTiles.ExperimentalPatch.dll","HundredTiles.Runtime.dll","HundredTiles.SaveBridge.dll","HundredTiles.Utilities.dll","HundredTiles.Profiles.dll","HundredTiles.WorkshopCompatibility.dll"};
  static QuitSwitch instance;
  int target;
  static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
  public static string Bin(string name){return Path.ChangeExtension(name,".bin");}
  // Every installed HundredTiles copy the game knows about (Mods and Workshop), enabled or not, so no copy is left on
  // the other build.
  public static List<string> Targets(){var list=new List<string>();foreach(var p in PluginManager.instance.GetPluginsInfo()){string path=p.modPath;if(!string.IsNullOrEmpty(path) && File.Exists(Path.Combine(path,"HundredTiles.Core.dll")) && File.Exists(Path.Combine(path,"HundredTiles.Runtime.dll")) && !list.Contains(path))list.Add(path);}return list;}
  // The options panel asks on every refresh; the shipped files do not change while the game runs.
  static readonly Dictionary<int,string> kits=new Dictionary<int,string>();
  public static string Kit(int tiles){string kit;if(!kits.TryGetValue(tiles,out kit)){kit=FindKit(tiles);kits[tiles]=kit;}return kit;}
  static string FindKit(int tiles){foreach(var target in Targets()){string folder=Path.Combine(Path.Combine(target,KitFolder),tiles.ToString());if(Valid(folder))return folder;}return null;}
  static bool Valid(string folder){try{Check(folder);return true;}catch(Exception){return false;}}
  static void Check(string folder){string[] lines=File.ReadAllLines(Path.Combine(folder,"files.tsv"));if(lines.Length!=Names.Length)throw new Exception("Incomplete profile manifest.");foreach(var name in Names){string expected=null;foreach(string line in lines){var p=line.Split('\t');if(p.Length==2 && p[0]==name)expected=p[1];}if(expected==null || Hash(Path.Combine(folder,Bin(name)))!=expected)throw new Exception("Profile checksum differs: "+name);}}
  public static void Arm(int tiles){if(instance==null){var go=new GameObject("HundredTiles.QuitSwitch");DontDestroyOnLoad(go);instance=go.AddComponent<QuitSwitch>();}if(instance.target!=tiles)Debug.Log("[HundredTiles] The "+tiles+"-tile build will be installed when the game quits.");instance.target=tiles;}
  void OnApplicationQuit(){string root=Selector.Root;try{
   string selected=Path.Combine(root,"selected.txt");if(target<=0 || !File.Exists(selected) || File.ReadAllText(selected).Trim()!=target.ToString())return;
   string kit=Kit(target);if(kit==null)throw new Exception("The "+target+"-tile bundle is missing or damaged.");Check(kit);Apply(root,kit,Targets());
   File.WriteAllText(Path.Combine(root,"switch-status.txt"),"Complete: "+target+" tiles. You may start Cities: Skylines.");Debug.Log("[HundredTiles] Installed the "+target+"-tile build.");
  }catch(Exception e){try{File.WriteAllText(Path.Combine(root,"switch-status.txt"),"FAILED: "+e.Message);}catch{}Debug.LogError("[HundredTiles] Profile switch failed: "+e.Message);}}
  // Backs up every destination first and restores them all if any copy or checksum fails.
  public static void Apply(string root,string kit,List<string> targets){
   string backup=Path.Combine(Path.Combine(root,"backups"),DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-quit-"+Guid.NewGuid().ToString("N"));var changed=new List<string[]>();
   for(int i=0;i<targets.Count;i++){string dir=Path.Combine(backup,i.ToString());Directory.CreateDirectory(dir);foreach(string name in Names){string dest=Path.Combine(targets[i],name);if(!File.Exists(dest))throw new Exception("Installed profile file missing: "+dest);File.Copy(dest,Path.Combine(dir,name),false);}}
   try{
    for(int i=0;i<targets.Count;i++)foreach(string name in Names){string dest=Path.Combine(targets[i],name);changed.Add(new[]{dest,Path.Combine(Path.Combine(backup,i.ToString()),name)});File.Copy(Path.Combine(kit,Bin(name)),dest,true);}
    foreach(var t in targets)foreach(string name in Names)if(Hash(Path.Combine(t,name))!=Hash(Path.Combine(kit,Bin(name))))throw new Exception("Installed checksum differs.");
   }catch{for(int i=changed.Count-1;i>=0;i--){try{File.Copy(changed[i][1],changed[i][0],true);}catch{}}throw;}
  }
 }
}
