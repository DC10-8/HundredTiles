using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Security.Cryptography;
using System.Collections.Generic;
class SwitchWorker {
 static int failAfter=-1;
 static readonly string[] Names={"CitiesHarmony.API.dll","HundredTiles.Core.dll","HundredTiles.ExperimentalPatch.dll","HundredTiles.Runtime.dll","HundredTiles.SaveBridge.dll","HundredTiles.Utilities.dll","HundredTiles.Profiles.dll"};
 static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
 static bool Same(string root,string token){string path=Path.Combine(root,"request.txt");return File.Exists(path) && File.ReadAllLines(path)[0]==token;}
 static bool Alive(int pid,long ticks){try{var p=Process.GetProcessById(pid);return !p.HasExited && p.StartTime.ToUniversalTime().Ticks==ticks;}catch(ArgumentException){return false;}}
 static void Validate(string bundle){string[] lines=File.ReadAllLines(Path.Combine(bundle,"files.tsv"));if(lines.Length!=Names.Length)throw new Exception("Incomplete profile manifest.");foreach(var name in Names){string expected=null;foreach(string line in lines){var p=line.Split('\t');if(p.Length==2 && p[0]==name)expected=p[1];}if(expected==null || Hash(Path.Combine(bundle,name))!=expected)throw new Exception("Profile checksum differs: "+name);}}
 static string[] Targets(string root){string local=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Colossal Order\Cities_Skylines\Addons\Mods\HundredTiles");string workshop=@"C:\Program Files (x86)\Steam\steamapps\workshop\content\255710\3818239060";var targets=File.ReadAllLines(Path.Combine(root,"targets.txt"));if(targets.Length<1 || targets.Length>2)throw new Exception("Invalid switch targets.");foreach(var path in targets){string full=Path.GetFullPath(path).TrimEnd('\\');if(!string.Equals(full,local,StringComparison.OrdinalIgnoreCase) && !string.Equals(full,workshop,StringComparison.OrdinalIgnoreCase))throw new Exception("Unapproved mod path.");}return targets;}
 static void Switch(string root,string token,bool test){if(!Same(root,token))return;string[] request=File.ReadAllLines(Path.Combine(root,"request.txt"));if(request.Length!=2 || (request[1]!="100" && request[1]!="324"))return;
  string bundle=Path.Combine(root,request[1]);Validate(bundle);string[] targets=test?new[]{Path.Combine(root,"target")}:Targets(root);
  // A restart before the waiting helper finished must never trigger replacement
  // under a running game. Leave a diagnostic and preserve all current DLLs.
  if(!test && Process.GetProcessesByName("Cities").Length!=0)throw new Exception("A new game process is running. Close it before switching profiles.");
  string backup=Path.Combine(root,"backups\\"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+token);Directory.CreateDirectory(backup);var changed=new List<string[]>();
  try{
   for(int i=0;i<targets.Length;i++){
    Directory.CreateDirectory(Path.Combine(backup,i.ToString()));
    foreach(string name in Names){string dest=Path.Combine(targets[i],name),saved=Path.Combine(backup,i.ToString(),name);if(!File.Exists(dest))throw new Exception("Installed profile file missing: "+dest);File.Copy(dest,saved,false);}
   }
   for(int i=0;i<targets.Length;i++)foreach(string name in Names){if(!Same(root,token))throw new Exception("Selection changed during switch.");string dest=Path.Combine(targets[i],name),temp=dest+".switch-"+token;File.Copy(Path.Combine(bundle,name),temp,false);changed.Add(new[]{dest,Path.Combine(backup,i.ToString(),name),temp});if(test && changed.Count==failAfter)throw new IOException("Injected switch failure.");File.Replace(temp,dest,null);}
   foreach(var target in targets)foreach(string name in Names)if(Hash(Path.Combine(target,name))!=Hash(Path.Combine(bundle,name)))throw new Exception("Installed checksum differs.");
   File.WriteAllText(Path.Combine(root,"switch-status.txt"),"Complete: "+request[1]+" tiles. You may start Cities: Skylines.");
  }catch{for(int i=changed.Count-1;i>=0;i--){try{File.Copy(changed[i][1],changed[i][0],true);}catch{}}throw;}
 }
 static void SelfTest(){
  string root=Path.Combine(Path.GetTempPath(),"HundredTiles-SwitchTests-"+Guid.NewGuid().ToString("N"));string token=Guid.NewGuid().ToString("N");Directory.CreateDirectory(Path.Combine(root,"100"));Directory.CreateDirectory(Path.Combine(root,"target"));
  File.WriteAllText(Path.Combine(root,"request.txt"),token+"\n100");
  var lines=new List<string>();foreach(var name in Names){File.WriteAllText(Path.Combine(root,"100",name),"new:"+name);File.WriteAllText(Path.Combine(root,"target",name),"old:"+name);lines.Add(name+"\t"+Hash(Path.Combine(root,"100",name)));}File.WriteAllLines(Path.Combine(root,"100","files.tsv"),lines.ToArray());
  // Cancelled requests must leave every installed file untouched.
  Switch(root,Guid.NewGuid().ToString("N"),true);foreach(var name in Names)if(File.ReadAllText(Path.Combine(root,"target",name))!="old:"+name)throw new Exception("Stale request wrote a file.");
  // An injected failure after two replacements must roll back both.
  failAfter=3;bool failed=false;try{Switch(root,token,true);}catch(IOException){failed=true;}if(!failed)throw new Exception("Failure injection did not run.");foreach(var name in Names)if(File.ReadAllText(Path.Combine(root,"target",name))!="old:"+name)throw new Exception("Rollback differs.");
  failAfter=-1;token=Guid.NewGuid().ToString("N");File.WriteAllText(Path.Combine(root,"request.txt"),token+"\n100");Switch(root,token,true);foreach(var name in Names)if(File.ReadAllText(Path.Combine(root,"target",name))!="new:"+name)throw new Exception("Switch differs.");
  File.WriteAllText(Path.Combine(root,"100",Names[0]),"corrupt");failed=false;try{Validate(Path.Combine(root,"100"));}catch(Exception){failed=true;}if(!failed)throw new Exception("Corrupt bundle passed.");
  Console.WriteLine("PASS transactional switch: stale request cancelled, injected failure rolled back, full bundle switched, corrupt bundle rejected. Artifacts: "+root);
 }
 static int Main(string[] args){string root=null;try{
  if(args.Length==1 && args[0]=="selftest"){SelfTest();return 0;}
  if(args.Length==2 && args[0]=="validate"){Validate(args[1]);Console.WriteLine("PASS bundle checksums.");return 0;}
  if(args.Length!=5 || args[0]!="wait")throw new Exception("Invalid arguments.");int pid=int.Parse(args[1]);long ticks=long.Parse(args[2]);string token=args[3];Guid parsed;if(!Guid.TryParseExact(token,"N",out parsed))throw new Exception("Invalid request token.");root=Path.GetFullPath(args[4]);string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Colossal Order\Cities_Skylines\HundredTiles-profiles");if(!string.Equals(root,expected,StringComparison.OrdinalIgnoreCase))throw new Exception("Invalid profile root.");
  while(Alive(pid,ticks)){if(!Same(root,token))return 0;Thread.Sleep(1000);}
  if(!Same(root,token))return 0;
  // A user may restart before the previous helper reaches the transaction.
  // Keep the latest request pending until every game process has exited.
  while(Process.GetProcessesByName("Cities").Length!=0){if(!Same(root,token))return 0;File.WriteAllText(Path.Combine(root,"switch-status.txt"),"Waiting: Cities: Skylines is running. Fully exit it; the requested profile will be applied afterwards.");Thread.Sleep(1000);}
  if(!Same(root,token))return 0;
  Switch(root,token,false);return 0;
 }catch(Exception e){if(root!=null)try{File.WriteAllText(Path.Combine(root,"switch-status.txt"),"FAILED: "+e.Message);}catch{}Console.Error.WriteLine(e.Message);return 1;}}
}
