using Mono.Cecil;
using Mono.Cecil.Cil;
string root=Path.GetFullPath(args[0]);string helperPath=Path.Combine(root,"HundredTiles.WorkshopCompatibility.dll");
var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(root);resolver.AddSearchDirectory(Environment.GetEnvironmentVariable("HUNDREDTILES_GAME_MANAGED")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed"));resolver.AddSearchDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Steam\steamapps\workshop\content\255710\2040656402"));
using var helper=ModuleDefinition.ReadModule(helperPath,new ReaderParameters{AssemblyResolver=resolver,InMemory=true});
MethodDefinition H(string type,string method)=>helper.Types.Single(t=>t.FullName=="HundredTiles.WorkshopCompatibility."+type).Methods.Single(m=>m.Name==method);
void Replace(ModuleDefinition module,MethodDefinition method,MethodDefinition replacement){var body=new MethodBody(method);method.Body=body;for(int i=0;i<method.Parameters.Count;i++)body.Instructions.Add(Instruction.Create(OpCodes.Ldarg,method.Parameters[i]));body.Instructions.Add(Instruction.Create(OpCodes.Call,module.ImportReference(replacement)));body.Instructions.Add(Instruction.Create(OpCodes.Ret));}
foreach(string file in (args.Length>1 && args[1]=="serializer-only"?new[]{"HundredTiles.SaveBridge.dll"}:new[]{"HundredTiles.Core.dll","HundredTiles.SaveBridge.dll","HundredTiles.Runtime.dll"})){
 string path=Path.Combine(root,file);using var m=ModuleDefinition.ReadModule(path,new ReaderParameters{AssemblyResolver=resolver,InMemory=true});
 if(file=="HundredTiles.Core.dll"){var type=m.Types.Single(t=>t.FullName=="HundredTiles.Core.ObjectPositionCodec");foreach(string name in new[]{"Encode","Decode"})Replace(m,type.Methods.Single(x=>x.Name==name),H("ExpandedObjects",name));}
 if(file=="HundredTiles.SaveBridge.dll"){Replace(m,m.Types.Single(t=>t.FullName=="HundredTiles.SaveBridge.SerializerHooks").Methods.Single(x=>x.Name=="VerifyRuntime"),H("SerializerRuntime","Verify"));var type=m.Types.Single(t=>t.FullName=="HundredTiles.SaveBridge.ObjectSaveBridge");Replace(m,type.Methods.Single(x=>x.Name=="ValidateTarget"),H("SpatialAdapters","ValidateObjectTarget"));}
 if(file=="HundredTiles.Runtime.dll"){
  var type=m.Types.Single(t=>t.FullName=="HundredTiles.Runtime.Compatibility");var method=type.Methods.Single(x=>x.Name=="Allowed");var il=method.Body.GetILProcessor();var first=method.Body.Instructions[0];var result=new VariableDefinition(m.TypeSystem.Int32);method.Body.Variables.Add(result);method.Body.InitLocals=true;
  var instructions=new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,m.ImportReference(H("ReviewedOverlays","Evaluate"))),Instruction.Create(OpCodes.Stloc,result),Instruction.Create(OpCodes.Ldloc,result),Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Blt,first),Instruction.Create(OpCodes.Ldloc,result),Instruction.Create(OpCodes.Ret)};foreach(var ins in instructions)il.InsertBefore(first,ins);
  var add=type.Methods.Single(x=>x.Name=="AddHooks");var start=add.Body.Instructions[0];add.Body.GetILProcessor().InsertBefore(start,Instruction.Create(OpCodes.Ldarg_0));add.Body.GetILProcessor().InsertBefore(start,Instruction.Create(OpCodes.Call,m.ImportReference(H("SpatialAdapters","Prepare"))));
  var installer=m.Types.Single(t=>t.FullName=="HundredTiles.Runtime.Installer");Replace(m,installer.Methods.Single(x=>x.Name=="Method"),H("SpatialAdapters","PrefixMethod"));
 }
 string temp=path+".integrated";m.Write(temp);File.Move(temp,path,true);Console.WriteLine("Integrated: "+file);
}

