using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using HundredTiles.WorkshopCompatibility;
class OverlayChecks {
 static string game=@"C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed",mods=@"C:\Program Files (x86)\Steam\steamapps\workshop\content\255710";
 static Assembly Resolve(object s,ResolveEventArgs a){string n=new AssemblyName(a.Name).Name+".dll";if(n=="0Harmony.dll")n="CitiesHarmony.Harmony.dll";foreach(var d in new[]{game,Path.Combine(mods,"2040656402"),Path.Combine(mods,"2966894510"),Path.Combine(mods,"2016920607"),@"C:\Users\HP\HundredTiles-Recovered\recovery\installed-dlls"}){var p=Path.Combine(d,n);if(File.Exists(p))return Assembly.LoadFrom(p);}return null;}
 static int Main(){AppDomain.CurrentDomain.AssemblyResolve+=Resolve;try{return Run();}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static int Run(){foreach(var m in typeof(NetTool).GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))if(m.Name=="CreateNode")Console.WriteLine("CreateNode parameters="+m.GetParameters().Length);var vc=Assembly.LoadFrom(Path.Combine(mods,@"2966894510\VisibilityControl.dll"));var rico=Assembly.LoadFrom(Path.Combine(mods,@"2016920607\ploppablerico.dll"));
  Check("com.github.algernon-A.csl.visibilitycontrol",typeof(NetManager).GetMethod("PopulateGroupData",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic),vc.GetType("VisibilityControl.Patches.LodDistanceNets").GetMethod("NetPopulateGroupData",BindingFlags.Static|BindingFlags.NonPublic),false);
  Check("com.github.algernon-A.csl.visibilitycontrol",typeof(TreeManager).GetMethod("PopulateGroupData",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic),vc.GetType("VisibilityControl.Patches.LodDistanceTrees").GetMethod("TreePopulateGroupData",BindingFlags.Static|BindingFlags.NonPublic),false);
  Check("com.github.algernon-A.csl.ploppablericorevisited",typeof(Building).GetMethod("CheckZoning",new[]{typeof(ItemClass.Zone),typeof(ItemClass.Zone),typeof(bool)}),rico.GetType("PloppableRICO.BuildingPatches").GetMethod("Prefix"),true);
  Console.WriteLine("PASS staged Visibility Control + Ploppable RICO adapters: exact build/helpers and targets accepted; wrong phase, target, transpiler and owner rejected.");return 0;
 }
 static void Check(string owner,MethodBase target,MethodInfo method,bool prefix){if(target==null || method==null)throw new Exception("Reviewed target missing.");var p=new Patch(method,0,owner,400,new string[0],new string[0],false);var e=new Patch[0];var correct=new Patches(prefix?new[]{p}:e,prefix?e:new[]{p},e,e);if(ReviewedOverlays.Evaluate(owner,target,correct)!=1)throw new Exception("Known patch rejected.");if(ReviewedOverlays.Evaluate(owner,target,new Patches(prefix?e:new[]{p},prefix?new[]{p}:e,e,e))!=0)throw new Exception("Wrong phase accepted.");if(ReviewedOverlays.Evaluate(owner,target,new Patches(e,e,new[]{p},e))!=0)throw new Exception("Transpiler accepted.");if(ReviewedOverlays.Evaluate(owner,typeof(ResidentAI).GetMethod("GetTaxiProbability",BindingFlags.Instance|BindingFlags.NonPublic),correct)!=0)throw new Exception("Wrong target accepted.");if(ReviewedOverlays.Evaluate("unknown.mod",target,correct)!=-1)throw new Exception("Unknown owner accepted.");}
}
