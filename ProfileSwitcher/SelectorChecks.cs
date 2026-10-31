using System;
using System.IO;
using System.Reflection;
class SelectorChecks {
 static string root;
 static Assembly Resolve(object s,ResolveEventArgs e){string n=new AssemblyName(e.Name).Name+".dll";foreach(var d in new[]{root,@"C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed"}){var p=Path.Combine(d,n);if(File.Exists(p))return Assembly.LoadFrom(p);}return null;}
 static int Main(string[] args){try{root=Path.GetFullPath(args[0]);AppDomain.CurrentDomain.AssemblyResolve+=Resolve;var rt=Assembly.LoadFrom(Path.Combine(root,"HundredTiles.Runtime.dll"));var core=Assembly.LoadFrom(Path.Combine(root,"HundredTiles.Core.dll"));int tiles=(int)core.GetType("HundredTiles.Core.ExpandedMap").GetField("TileCount").GetRawConstantValue();if(tiles!=int.Parse(args[1]))throw new Exception("Wrong core profile.");var lang=rt.GetType("HundredTiles.Runtime.UiLanguage").GetField("language",BindingFlags.Static|BindingFlags.NonPublic);var selector=Assembly.LoadFrom(Path.Combine(root,"HundredTiles.Profiles.dll")).GetType("HundredTiles.Profiles.Selector");
 foreach(int language in new[]{0,1}){lang.SetValue(null,language);var items=(string[])selector.GetMethod("Items").Invoke(null,null);if(items.Length!=3 || !items[0].Contains("324") || !items[1].Contains("100") || (language==0?!items[2].Contains("エディタ"):!items[2].Contains("Editor")))throw new Exception("Profile choices differ.");}
 var matches=selector.GetMethod("BundleMatches");foreach(int wanted in new[]{-1,100,324})if((bool)matches.Invoke(null,new object[]{wanted,tiles})!=(wanted<0 || wanted==tiles))throw new Exception("Mismatched profile guard failed.");
 Console.WriteLine("PASS "+tiles+" profile constants; three choices in Japanese and English.");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
