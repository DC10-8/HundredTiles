using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using HundredTiles.Runtime;
class HookCheck {
 static string root; static string game=@"C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed";
 static Assembly Resolve(object s,ResolveEventArgs a){string n=new AssemblyName(a.Name).Name+".dll";if(n=="0Harmony.dll")n="CitiesHarmony.Harmony.dll";foreach(var d in new[]{root,game,@"C:\Program Files (x86)\Steam\steamapps\workshop\content\255710\2040656402"}){var p=Path.Combine(d,n);if(File.Exists(p))return Assembly.LoadFrom(p);}return null;}
 static int Main(string[] args){root=Path.GetFullPath(args[0]);AppDomain.CurrentDomain.AssemblyResolve+=Resolve;try{return Run(args[1]);}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static int Run(string path){var plugin=Assembly.LoadFrom(path);var type=plugin.GetType("RealTimeSync.Harmony.TrainPassengerEventsPatch",true);var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;var pre=type.GetMethod("Prefix",flags);var post=type.GetMethod("Postfix",flags);const string owner="com.example.realtimesync";var empty=new Patch[0];var prefix=new Patch(pre,0,owner,400,new string[0],new string[0],false);var postfix=new Patch(post,1,owner,400,new string[0],new string[0],false);
 foreach(string name in new[]{"LoadPassengers","UnloadPassengers"}){var target=typeof(PassengerTrainAI).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(!Compatibility.Allowed(owner,target,new Patches(new[]{prefix},new[]{postfix},empty,empty)))throw new Exception("RealTimeSync wrappers rejected: "+name);if(Compatibility.Allowed(owner,target,new Patches(empty,new[]{prefix},empty,empty)))throw new Exception("Wrong phase accepted.");if(Compatibility.Allowed(owner,target,new Patches(empty,empty,new[]{prefix},empty)))throw new Exception("Wrong transpiler accepted.");}
 var unrelated=typeof(ResidentAI).GetMethod("GetTaxiProbability",BindingFlags.Instance|BindingFlags.NonPublic);if(Compatibility.Allowed(owner,unrelated,new Patches(new[]{prefix},empty,empty,empty)))throw new Exception("Wrong target accepted.");Console.WriteLine("PASS save-path compatibility: load/unload wrappers accepted; wrong phase, transpiler and target rejected.");return 0;}
}
