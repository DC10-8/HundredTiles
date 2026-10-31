using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;
string root=Path.GetFullPath(args[0]);
string game=args.Length>1?Path.GetFullPath(args[1]):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed");
string harmony=args.Length>2?Path.GetFullPath(args[2]):Path.Combine(root,"harmony");
var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.Combine(root,"bundles","324"));resolver.AddSearchDirectory(game);resolver.AddSearchDirectory(harmony);
using var helper=ModuleDefinition.ReadModule(Path.Combine(root,"HundredTiles.Profiles.dll"),new ReaderParameters{AssemblyResolver=resolver,InMemory=true});
MethodDefinition Help(string name)=>helper.Types.Single(t=>t.FullName=="HundredTiles.Profiles.Selector").Methods.Single(m=>m.Name==name);
IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var n in Types(t.NestedTypes))yield return n;}}
foreach(string profile in new[]{"100","324"}){
 string dir=Path.Combine(root,"bundles",profile),input=Path.Combine(dir,"HundredTiles.Runtime.dll");using var m=ModuleDefinition.ReadModule(input,new ReaderParameters{AssemblyResolver=resolver,InMemory=true});
 int arrays=0,selected=0,set=0,guards=0;
 foreach(var type in Types(m.Types).Where(t=>t.FullName.StartsWith("HundredTiles.Runtime.Mod")))foreach(var method in type.Methods.Where(x=>x.HasBody)){
  var il=method.Body.Instructions;
  // Only settings methods and their generated callbacks are changed. Startup
  // city/editor code continues to use its original frozen profile setting.
  bool settings=method.Name.Contains("OnSettingsUI");if(!settings)continue;
  for(int i=0;i<il.Count;i++){
   if(il[i].OpCode==OpCodes.Ldstr && il[i].Operand is string s && s=="都市（100タイル）"){
    int first=i;while(first>=0 && il[first].OpCode!=OpCodes.Newarr)first--;
    if(first<1 || il[first-1].OpCode!=OpCodes.Ldc_I4_2)throw new Exception("Unexpected profile items array.");first--;
    int last=i,count=0;for(;last<il.Count;last++)if(il[last].OpCode==OpCodes.Stelem_Ref && ++count==2)break;
    if(count!=2)throw new Exception("Profile items array end missing.");
    for(int j=first;j<=last;j++){il[j].OpCode=OpCodes.Nop;il[j].Operand=null;}
    il[first].OpCode=OpCodes.Call;il[first].Operand=m.ImportReference(Help("Items"));arrays++;i=last;
   }
  }
  for(int i=2;i<il.Count;i++)if(il[i].Operand is MethodReference text && text.DeclaringType.FullName=="HundredTiles.Runtime.UiLanguage" && text.Name=="Text" && il[i-2].OpCode==OpCodes.Ldstr && (string)il[i-2].Operand=="100タイルをすべて開放（無料）")il[i].Operand=m.ImportReference(Help("UnlockLabel"));
  foreach(var ins in il)if(ins.Operand is MethodReference unlock && unlock.DeclaringType.FullName=="HundredTiles.Runtime.BulkUnlock" && unlock.Name=="Request")ins.Operand=m.ImportReference(Help("RequestUnlock"));
  bool setter=il.Any(x=>x.Operand is MethodReference mr && mr.DeclaringType.FullName=="HundredTiles.Runtime.StartupProfile" && mr.Name=="Set");
  foreach(var ins in il){
   if(ins.Operand is MethodReference mr && mr.DeclaringType.FullName=="HundredTiles.Runtime.StartupProfile"){
    if(mr.Name=="get_Selected"){ins.OpCode=OpCodes.Call;ins.Operand=m.ImportReference(Help("Selected"));selected++;}
    if(mr.Name=="Set"){ins.OpCode=OpCodes.Call;ins.Operand=m.ImportReference(Help("Set"));set++;}
   }
  }
  if(setter)for(int i=0;i<il.Count-1;i++)if(il[i].OpCode==OpCodes.Ldc_I4_1 && (il[i+1].OpCode==OpCodes.Bgt || il[i+1].OpCode==OpCodes.Bgt_S || il[i+1].OpCode==OpCodes.Ble || il[i+1].OpCode==OpCodes.Ble_S)){il[i].OpCode=OpCodes.Ldc_I4_2;guards++;}
 }
 var gate=m.Types.Single(t=>t.FullName=="HundredTiles.Runtime.SessionGate").Methods.Single(x=>x.Name=="BeforeLoad");
 var anchor=gate.Body.Instructions.Single(i=>i.Operand is MethodReference mr && mr.DeclaringType.FullName.EndsWith(".BoundaryHooks") && mr.Name=="CityMode");gate.Body.GetILProcessor().InsertBefore(anchor,Instruction.Create(OpCodes.Call,m.ImportReference(Help("AssertBundle"))));
 if(arrays!=2 || selected!=2 || set!=1 || guards!=1)throw new Exception($"Unexpected selector structure {profile}: {arrays}/{selected}/{set}/{guards}");
 if(profile=="100"){
  // Preserve all 100-grid executable code. Update the two reviewed plugin
  // versions to the same inspected local versions as the current 324 bundle.
  var resource=(EmbeddedResource)m.Resources.Single(r=>r.Name=="HundredTiles.Runtime.ReviewedCompatibility.tsv");
  string text=Encoding.UTF8.GetString(resource.GetResourceData());var lines=text.Split('\n').Where(line=>!new[]{"AreaPOBuilder","RealTimeSync"}.Any(name=>line.Split('\t').Length>1 && line.Split('\t')[1]==name)).ToList();
  string area=File.ReadAllText(Path.Combine(root,"ReviewedAreaPOBuilder.tsv"));
  lines.AddRange(area.Split('\n').Where(l=>l.Split('\t').Length>1 && l.Split('\t')[1]=="AreaPOBuilder"));
  lines.AddRange(File.ReadAllLines(Path.Combine(root,"ReviewedRealTimeSync.tsv")));
  m.Resources.Remove(resource);m.Resources.Add(new EmbeddedResource(resource.Name,resource.Attributes,Encoding.UTF8.GetBytes(string.Join("\n",lines.Where(l=>l.Trim().Length>0))+"\n")));
 }
 // Widen branch operands in edited methods so injected calls cannot overflow
 // an existing short branch when Cecil lays out the modified method body.
 var opcodes=typeof(OpCodes).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(op=>op.Name);
 foreach(var type in Types(m.Types))foreach(var method in type.Methods.Where(x=>x.HasBody && (type.FullName.StartsWith("HundredTiles.Runtime.Mod") || x==gate)))foreach(var ins in method.Body.Instructions)if(ins.Operand is Instruction && ins.OpCode.Name.EndsWith(".s"))ins.OpCode=opcodes[ins.OpCode.Name[..^2]];
 string output=Path.Combine(dir,"HundredTiles.Runtime.updated");m.Write(output);File.Move(output,input,true);File.Copy(Path.Combine(root,"HundredTiles.Profiles.dll"),Path.Combine(dir,"HundredTiles.Profiles.dll"),true);
 Console.WriteLine($"PASS profile selector wiring {profile}: arrays={arrays}, selections={selected}, callback={set}, range={guards}; city guard added.");
}
