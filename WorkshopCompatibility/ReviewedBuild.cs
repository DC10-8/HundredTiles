using System;using System.Collections.Generic;using System.IO;using System.Reflection;using System.Reflection.Emit;using System.Security.Cryptography;using ColossalFramework;using ColossalFramework.Plugins;
namespace HundredTiles.WorkshopCompatibility {
 // Verifies a third-party mod build against its manifest (assembly<TAB>name<TAB>SHA256, then method rows with semantic hashes).
 // The exact file passes directly. A different build of the same version passes only when every reviewed method still has its
 // reviewed semantic hash and no method outside the reviewed list reads a manager grid, so rebuilt binaries are accepted
 // without accepting new grid code. Failures name what differed.
 public static class ReviewedBuild {
  public sealed class Result { public bool Ok, Exact; public string Detail; }
  static readonly Dictionary<Assembly,Result> cache=new Dictionary<Assembly,Result>();
  const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;

  // gridReaders: "Namespace.Type::Method" of every method allowed to read a manager grid (adapted or reviewed as harmless).
  public static Result Verify(Assembly assembly,ICollection<string> gridReaders){Result cached;if(cache.TryGetValue(assembly,out cached))return cached;var result=new Result();string name=assembly.GetName().Name;
   try{string[] lines;using(var stream=typeof(ReviewedBuild).Assembly.GetManifestResourceStream(name+".tsv")){if(stream==null)throw new InvalidOperationException("no reviewed manifest");using(var reader=new StreamReader(stream))lines=reader.ReadToEnd().Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries);}
    string[] header=lines[0].Split('\t');string file=Disk(assembly);string hash=Hash(file);result.Exact=header[1]==name && header[2]==hash;var differ=new List<string>();
    for(int i=1;i<lines.Length;i++){var row=lines[i].Split('\t');var type=assembly.GetType(row[2],false);bool found=false;if(type!=null)foreach(var method in type.GetMethods(All))if(ReviewedOverlays.MatchesBody(row[3],row[4],method)){found=true;break;}if(!found)differ.Add(row[2]+"."+row[3]);}
    var unreviewed=result.Exact?new List<string>():GridReaders(assembly,gridReaders);
    result.Ok=differ.Count==0 && unreviewed.Count==0;
    result.Detail=(result.Exact?"exact build":"different build SHA256="+hash)+(differ.Count==0?"":"; changed reviewed methods: "+string.Join(", ",differ.ToArray()))+(unreviewed.Count==0?"":"; unreviewed grid readers: "+string.Join(", ",unreviewed.ToArray()));
   }catch(Exception ex){result.Ok=false;result.Detail=ex.Message;}
   cache[assembly]=result;return result;}

  static string Disk(Assembly assembly){if(!string.IsNullOrEmpty(assembly.Location))return assembly.Location;foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())foreach(var a in plugin.GetAssemblies())if(a==assembly)return Path.Combine(plugin.modPath,assembly.GetName().Name+".dll");throw new InvalidOperationException("mod file missing");}
  static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}

  static OpCode[] one,two;
  static void LoadOpCodes(){if(one!=null)return;var a=new OpCode[256];var b=new OpCode[256];foreach(FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)){var op=(OpCode)f.GetValue(null);ushort v=(ushort)op.Value;if(op.Size==1)a[v]=op;else b[v&0xff]=op;}two=b;one=a;}
  static bool IsGrid(FieldInfo field){return field!=null && field.FieldType.IsArray && field.DeclaringType!=null && field.DeclaringType.Assembly==typeof(NetManager).Assembly && field.Name.StartsWith("m_") && (field.Name.EndsWith("Grid") || field.Name.EndsWith("Grid2"));}

  // Every method body is decoded instruction by instruction; a field operand that cannot be resolved counts as a grid read.
  // Mono 2.0 crashes the process, rather than throwing, when asked for the body of a method whose locals use a type from a
  // missing assembly (seen with mods that reference an optional, uninstalled mod). Bodies are read only when every
  // referenced assembly resolves.
  public static string MissingReference(Assembly assembly){foreach(var reference in assembly.GetReferencedAssemblies()){bool loaded=false;foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name==reference.Name){loaded=true;break;}
    if(loaded)continue;try{Assembly.Load(reference);}catch{return reference.Name;}}return null;}
  public static List<string> GridReaders(Assembly assembly,ICollection<string> allowed){var found=new List<string>();string missing=MissingReference(assembly);if(missing!=null){found.Add("(missing reference "+missing+")");return found;}
   foreach(var method in Methods(assembly)){bool unresolved;bool reads=false;foreach(var field in FieldsRead(method,out unresolved))if(IsGrid(field))reads=true;
    string key=method.DeclaringType.FullName+"::"+method.Name;if((reads || unresolved) && !allowed.Contains(key) && !found.Contains(key))found.Add(key);}
   return found;}
  public static IEnumerable<MethodBase> Methods(Assembly assembly){Type[] types;try{types=assembly.GetTypes();}catch(ReflectionTypeLoadException ex){types=ex.Types;}
   foreach(var type in types){if(type==null)continue;MethodInfo[] methods;ConstructorInfo[] ctors;try{methods=type.GetMethods(All);ctors=type.GetConstructors(All);}catch{continue;}foreach(var m in methods)yield return m;foreach(var c in ctors)yield return c;}}
  public static bool IsManagerGrid(FieldInfo field){return IsGrid(field);}
  // Fields a method body loads or addresses; unresolved is set when an operand could not be resolved.
  public static List<FieldInfo> FieldsRead(MethodBase method,out bool unresolved){LoadOpCodes();unresolved=false;var fields=new List<FieldInfo>();MethodBody body;try{body=method.GetMethodBody();}catch{return fields;}if(body==null)return fields;
   byte[] il=body.GetILAsByteArray();int i=0;Type type=method.DeclaringType;Type[] typeArgs=type!=null && type.IsGenericType?type.GetGenericArguments():null;Type[] methodArgs=method.IsGenericMethod?method.GetGenericArguments():null;
   while(i<il.Length){OpCode op=il[i]==0xFE?two[il[i+1]]:one[il[i]];i+=op.Size;int size=Operand(op.OperandType,il,i);
    if(op.OperandType==OperandType.InlineField){try{var f=method.Module.ResolveField(BitConverter.ToInt32(il,i),typeArgs,methodArgs);if(f!=null && !fields.Contains(f))fields.Add(f);}catch{unresolved=true;}}
    i+=size;}
   return fields;}
  static int Operand(OperandType kind,byte[] il,int at){switch(kind){
   case OperandType.InlineNone:return 0;
   case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:return 1;
   case OperandType.InlineVar:return 2;
   case OperandType.InlineI8:case OperandType.InlineR:return 8;
   case OperandType.InlineSwitch:return 4+4*BitConverter.ToInt32(il,at);
   default:return 4;}}
 }
}
