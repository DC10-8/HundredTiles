using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using ColossalFramework.IO;
using ColossalFramework.Plugins;
using ICities;
namespace HundredTiles.Loader {
 // One mod folder for both tile counts. The 100 and 324 builds use the same assembly names, so only one can be loaded per
 // process. The folder holds this loader and CitiesHarmony.API.dll as its only DLLs, and both builds as .bin files in
 // HundredTilesProfiles\{100,324} (the game loads every *.dll below a mod folder). The loader is created where the game
 // creates a mod's IUserMod, before any OnEnabled. It loads the selected build from bytes, as the game itself loads mods,
 // and adds those assemblies to this plugin so the game finds their loading and threading extensions. Switching is a
 // restart; no file is replaced.
 public sealed class Mod : IUserMod {
  public const string KitFolder="HundredTilesProfiles";
  static readonly string[] Names={"CitiesHarmony.API.dll","HundredTiles.Core.dll","HundredTiles.ExperimentalPatch.dll","HundredTiles.Runtime.dll","HundredTiles.SaveBridge.dll","HundredTiles.Utilities.dll","HundredTiles.Profiles.dll","HundredTiles.WorkshopCompatibility.dll"};
  static readonly string[] Order={"HundredTiles.Core","HundredTiles.ExperimentalPatch","HundredTiles.SaveBridge","HundredTiles.Utilities","HundredTiles.WorkshopCompatibility","HundredTiles.Profiles","HundredTiles.Runtime"};
  static List<Assembly> loaded;
  static bool attached;
  static string failure;
  readonly object real;
  public static int LoadedTiles;

  public Mod(){
   try{
    PluginManager.PluginInfo self=null;foreach(var p in PluginManager.instance.GetPluginsInfo())if(p.GetAssemblies().Contains(typeof(Mod).Assembly))self=p;
    if(self==null)throw new InvalidOperationException("Loader plugin not found.");
    if(loaded==null)loaded=LoadSelected(self.modPath);
    // A second copy (Mods and Workshop both installed) or a hot reload reuses the assemblies already in this process;
    // only the first plugin carries them, so their extensions are not created twice.
    if(!attached){foreach(var a in loaded)self.AddAssembly(a);attached=true;}
    real=Activator.CreateInstance(Find("HundredTiles.Runtime").GetType("HundredTiles.Runtime.Mod",true));
   }catch(Exception e){failure=e.Message;UnityEngine.Debug.LogError("[HundredTiles] Loader failed: "+e);}
  }

  public string Name {get{return real!=null?(string)real.GetType().GetProperty("Name").GetValue(real,null):"HundredTiles (failed to load)";}}
  public string Description {get{return real!=null?(string)real.GetType().GetProperty("Description").GetValue(real,null):"HundredTiles could not be loaded: "+failure;}}
  public void OnEnabled(){Forward("OnEnabled");}
  public void OnDisabled(){Forward("OnDisabled");}
  public void OnSettingsUI(UIHelperBase helper){Forward("OnSettingsUI",helper);}
  void Forward(string method,params object[] args){if(real==null)return;var m=real.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Public);if(m!=null)m.Invoke(real,args);}

  static Assembly Find(string name){foreach(var a in loaded)if(a.GetName().Name==name)return a;throw new InvalidOperationException(name+" not loaded.");}
  static string Root {get{return Path.Combine(DataLocation.localApplicationData,"HundredTiles-profiles");}}
  static string Read(string file){string path=Path.Combine(Root,file);return File.Exists(path)?File.ReadAllText(path).Trim():null;}
  // The editor choice keeps the last city build; the editor runs with normal grids either way.
  public static int Selected(){string s=Read("selected.txt");if(s=="100")return 100;if(s=="324")return 324;return Read("loaded.txt")=="100"?100:324;}

  static List<Assembly> LoadSelected(string modPath){
   int wanted=Selected();string kits=Path.Combine(modPath,KitFolder);List<Assembly> result;
   try{result=LoadKit(Path.Combine(kits,wanted.ToString()));LoadedTiles=wanted;}
   catch(Exception e){int other=wanted==100?324:100;UnityEngine.Debug.LogError("[HundredTiles] The "+wanted+"-tile build could not be loaded ("+e.Message+"); loading the "+other+"-tile build.");result=LoadKit(Path.Combine(kits,other.ToString()));LoadedTiles=other;}
   try{Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,"loaded.txt"),LoadedTiles.ToString());}catch(Exception){}
   UnityEngine.Debug.Log("[HundredTiles] Loaded the "+LoadedTiles+"-tile build from "+KitFolder+".");return result;
  }

  static string Hash(byte[] data){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(data)).Replace("-","");}
  public static string Bin(string name){return Path.ChangeExtension(name,".bin");}
  // Checks every file against files.tsv before loading any of them, then loads the HundredTiles assemblies from bytes.
  public static List<Assembly> LoadKit(string kit){
   string[] lines=File.ReadAllLines(Path.Combine(kit,"files.tsv"));if(lines.Length!=Names.Length)throw new Exception("Incomplete profile manifest.");
   var bytes=new Dictionary<string,byte[]>();
   foreach(var name in Names){string expected=null;foreach(string line in lines){var p=line.Split('\t');if(p.Length==2 && p[0]==name)expected=p[1];}byte[] data=File.ReadAllBytes(Path.Combine(kit,Bin(name)));if(expected==null || Hash(data)!=expected)throw new Exception("Profile checksum differs: "+name);bytes[Path.GetFileNameWithoutExtension(name)]=data;}
   var list=new List<Assembly>();foreach(var name in Order)list.Add(Assembly.Load(bytes[name]));
   // Assemblies loaded from bytes find each other by name; this covers a lookup the runtime does not satisfy itself.
   AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){string n=new AssemblyName(e.Name).Name;foreach(var a in list)if(a.GetName().Name==n)return a;return null;};
   return list;
  }
 }
}
