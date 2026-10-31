using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
class ReviewCheck {
 static string root,mod; static string game=@"C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed";
 static Assembly Resolve(object sender,ResolveEventArgs a){string extra=Environment.GetEnvironmentVariable("HUNDREDTILES_REVIEW_DEPS");if(!string.IsNullOrEmpty(extra))foreach(string directory in extra.Split(';')){string candidate=Path.Combine(directory,new AssemblyName(a.Name).Name+".dll");if(File.Exists(candidate))return Assembly.LoadFrom(candidate);}string n=new AssemblyName(a.Name).Name+".dll";if(n=="0Harmony.dll")n="CitiesHarmony.Harmony.dll";foreach(string d in new[]{root,mod,game,@"C:\Program Files (x86)\Steam\steamapps\workshop\content\255710\2040656402"}){string p=Path.Combine(d,n);if(File.Exists(p))return Assembly.LoadFrom(p);}return null;}
 static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","");}
 static int Main(string[] args){try{root=Path.GetFullPath(args[1]);mod=Path.GetDirectoryName(Path.GetFullPath(args[2]));AppDomain.CurrentDomain.AssemblyResolve+=Resolve;var rt=Assembly.LoadFrom(Path.Combine(root,"HundredTiles.Runtime.dll"));var plugin=Assembly.LoadFrom(args[2]);
 if(args[0]=="export"){
 Console.WriteLine("assembly\tRealTimeSync\t"+Hash(args[2]));var ep=Assembly.LoadFrom(Path.Combine(root,"HundredTiles.ExperimentalPatch.dll"));var semantic=ep.GetType("HundredTiles.ExperimentalPatch.ReviewedMethod").GetMethod("SemanticHash");
 foreach(var t in plugin.GetTypes())foreach(var m in t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))if(m.GetMethodBody()!=null)Console.WriteLine("method\tRealTimeSync\t"+t.FullName+"\t"+m.Name+"\t"+semantic.Invoke(null,new object[]{m}));return 0;}
 var verify=rt.GetType("HundredTiles.Runtime.ReviewedCompatibility").GetMethod("Verify");bool rejected=false;try{verify.Invoke(null,new object[]{plugin,args[2]});}catch(TargetInvocationException ex){if(!(ex.InnerException is InvalidOperationException))throw;rejected=true;Console.WriteLine(ex.InnerException.Message);}
 if(rejected!=(args[0]=="reject"))throw new Exception("Unexpected verification result.");Console.WriteLine("PASS "+args[0]+": "+Hash(args[2]));return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
