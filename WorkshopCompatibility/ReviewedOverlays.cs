using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using ColossalFramework;
using ColossalFramework.Plugins;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
namespace HundredTiles.WorkshopCompatibility {
 // Staged adapter only. Not installed into the active runtime until the full
 // seven-mod composition has been validated; no owner-wide blanket allowance.
 public static class ReviewedOverlays {
  static readonly HashSet<Assembly> verified=new HashSet<Assembly>();
  static string Disk(Assembly assembly){if(!string.IsNullOrEmpty(assembly.Location))return assembly.Location;foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())foreach(var a in plugin.GetAssemblies())if(a==assembly)return Path.Combine(plugin.modPath,assembly.GetName().Name+".dll");throw new InvalidOperationException("Compatibility module disk file missing.");}
  static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
  static void VerifyUpdater(){Assembly runtime=null;foreach(var candidate in AppDomain.CurrentDomain.GetAssemblies())if(candidate.GetName().Name=="HundredTiles.Runtime")runtime=candidate;if(runtime==null)throw new InvalidOperationException("Runtime verifier missing.");var type=runtime.GetType("HundredTiles.Runtime.LandscapingCompatibility",true);type.GetMethod("VerifyBehaviourUpdater",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);}
  public static bool MatchesBody(string originalName,string expected,MethodInfo method){
   bool renamed=(originalName=="Update" || originalName=="LateUpdate" || originalName=="FixedUpdate") && method.Name=="FpsBooster"+originalName && !method.IsStatic && method.ReturnType==typeof(void) && method.GetParameters().Length==0;
   if(method.Name!=originalName && !renamed)return false;
   if(method.GetMethodBody()==null)return false;
   if(ReviewedMethod.SemanticHash(method)==expected){if(renamed)VerifyUpdater();return true;}
   string hash;if(method.Name==originalName && ReviewedMethod.TryFpsLifecycleHash(method,out hash) && hash==expected){VerifyUpdater();return true;}
   return false;
  }
  public static void VerifyModule(Assembly assembly){if(verified.Contains(assembly))return;string name=assembly.GetName().Name;using(var stream=typeof(ReviewedOverlays).Assembly.GetManifestResourceStream(name+".tsv")){if(stream==null)throw new InvalidOperationException("No reviewed overlay module.");using(var reader=new StreamReader(stream)){string[] header=reader.ReadLine().Split('\t');if(header[1]!=name || header[2]!=Hash(Disk(assembly)))throw new InvalidOperationException("Reviewed overlay build changed: "+name);string line;while((line=reader.ReadLine())!=null){var row=line.Split('\t');var type=assembly.GetType(row[2],true);bool found=false;foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))if(MatchesBody(row[3],row[4],method)){found=true;break;}if(!found)throw new InvalidOperationException("Overlay helper changed: "+row[2]+"."+row[3]);}}}verified.Add(assembly);}
  public static int Evaluate(string owner,MethodBase target,Patches patches){string name=null,type=null,method=null,phase=null;
   if(owner=="com.github.algernon-A.csl.visibilitycontrol"){
    name="VisibilityControl";phase="postfix";
    if(target.DeclaringType==typeof(NetManager) && target.Name=="PopulateGroupData"){type="VisibilityControl.Patches.LodDistanceNets";method="NetPopulateGroupData";}
    else if(target.DeclaringType==typeof(TreeManager) && target.Name=="PopulateGroupData"){type="VisibilityControl.Patches.LodDistanceTrees";method="TreePopulateGroupData";}
    else return 0;
   }else if(owner=="com.github.algernon-A.csl.ploppablericorevisited"){
    name="ploppablerico";type="PloppableRICO.BuildingPatches";method="Prefix";phase="prefix";
    var p=target.GetParameters();if(target.DeclaringType!=typeof(Building) || target.Name!="CheckZoning" || p.Length!=3 || p[0].ParameterType!=typeof(ItemClass.Zone) || p[1].ParameterType!=typeof(ItemClass.Zone) || p[2].ParameterType!=typeof(bool))return 0;
   }else if(owner=="com.github.algernon-A.csl.mcu"){
    if(target.DeclaringType!=typeof(CitizenManager) || target.Name!="Awake")return 0;name="MoreCitizenUnits";type="MoreCitizenUnits.GameLimitTranspiler";method="Transpiler";phase="transpiler";
   }else if(owner=="com.github.keallu.csl.hideit"){
    name="HideIt";phase="prefix";method="Prefix";
    if(target.DeclaringType!=typeof(NaturalResourceManager))return 0;
    if(target.Name=="UpdateTexture")type="HideIt.NaturalResourceManagerUpdateTexturePatch";
    else if(target.Name=="UpdateTextureB")type="HideIt.NaturalResourceManagerUpdateTextureBPatch";else return 0;
   }else if(owner=="com.github.algernon-A.csl.propcontrol"){
    name="PropControl";type="PropControl.Patches.PropInstancePatches";phase="prefix";
    if(target.DeclaringType!=typeof(PropInstance))return 0;
    if(target.Name=="get_Position")method="GetPositionPrefix";else if(target.Name=="set_Position")method="SetPositionPostfix";else return 0;
   }else if(owner=="com.github.algernon-A.csl.treecontrol"){
    name="TreeControl";
    string key=target.DeclaringType.FullName+"."+target.Name;
    if(key=="TreeManager+Data.Serialize" || key=="TreeManager+Data.Deserialize"){type="TreeControl.Patches.TreeManagerDataPatches";method=target.Name+"Transpiler";phase="transpiler";}
    else {type="TreeControl.Patches.GameLimitTranspiler";method="Transpiler";phase="transpiler";
     var capacity=new[]{"CommonBuildingAI.HandleFireSpread","ForestFireAI.FindClosestTree","DisasterHelpers.DestroyTrees","DistrictManager.MoveParkTrees","NaturalResourceManager.TreesModified","WeatherManager.FindStrikeTarget","TreeManager.EndRenderingImpl","TreeManager.SampleSmoothHeight","TreeManager.FinalizeTree","TreeManager.UpdateTrees","TreeManager.OverlapQuad","TreeManager.RayCast","TreeManager.HandleFireSpread","TreeManager.TerrainUpdated","TreeManager.AfterTerrainUpdate","TreeManager.CalculateAreaHeight","TreeManager.CalculateGroupData","TreeManager.PopulateGroupData","TreeTool.ApplyBrush","FireCopterAI.FindBurningTree"};
     if(Array.IndexOf(capacity,key)<0)return 0;
    }
   }else if(owner=="boformer.NetworkSkins"){
    name="NetworkSkins";
    if(target.DeclaringType==typeof(SimulationManager) && target.Name=="Managers_UpdateData"){type="NetworkSkins.Patches.SimulationManager.SimulationManagerManagersUpdateDataPatch";method="Prefix";phase="prefix";}
    else if(target.DeclaringType==typeof(NetTool) && target.Name=="CreateNode"){
     // Exact methods and phases are checked below for the two reviewed overloads.
     type="network-skins-create-node";
    }else return 0;
   }else return -1;
   bool found=false;
   foreach(var pair in new[]{new KeyValuePair<string,IEnumerable<Patch>>("prefix",patches.Prefixes),new KeyValuePair<string,IEnumerable<Patch>>("postfix",patches.Postfixes),new KeyValuePair<string,IEnumerable<Patch>>("transpiler",patches.Transpilers),new KeyValuePair<string,IEnumerable<Patch>>("finalizer",patches.Finalizers)})foreach(var patch in pair.Value)if(patch.owner==owner){found=true;var pm=patch.PatchMethod;if(type=="network-skins-create-node"){
 if(pm.DeclaringType.FullName=="NetworkSkins.Patches.NetTool.NetToolCreateNodePatch"){if(pair.Key!="transpiler" || pm.Name!="Transpiler" || target.GetParameters().Length!=19)return 0;}
 else if(pm.DeclaringType.FullName=="NetworkSkins.Patches.NetTool.NetToolCreateNode0Patch"){if(!((pair.Key=="prefix" && pm.Name=="Prefix") || (pair.Key=="postfix" && pm.Name=="Postfix")) || target.GetParameters().Length!=17)return 0;}
 else return 0;
}else if(owner=="com.github.algernon-A.csl.treecontrol" && target.DeclaringType==typeof(NaturalResourceManager) && target.Name=="TreesModified" && pm.DeclaringType.FullName=="TreeControl.Patches.NaturalResourceManagerPatches" && pm.Name=="TreesModifiedPrefix" && pair.Key=="prefix"){}
else if(pair.Key!=phase || pm.DeclaringType.FullName!=type || pm.Name!=method)return 0;
if(pm.Module.Assembly.GetName().Name!=name)return 0;VerifyModule(pm.Module.Assembly);}
   return found?1:0;
  }
 }
}


