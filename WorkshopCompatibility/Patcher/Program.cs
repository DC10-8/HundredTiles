using Mono.Cecil;
using Mono.Cecil.Cil;
string root=Path.GetFullPath(args[0]);string helperPath=Path.Combine(root,"HundredTiles.WorkshopCompatibility.dll");
var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(root);resolver.AddSearchDirectory(Environment.GetEnvironmentVariable("HUNDREDTILES_GAME_MANAGED")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed"));resolver.AddSearchDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Steam\steamapps\workshop\content\255710\2040656402"));
using var helper=ModuleDefinition.ReadModule(helperPath,new ReaderParameters{AssemblyResolver=resolver,InMemory=true});
MethodDefinition H(string type,string method)=>helper.Types.Single(t=>t.FullName=="HundredTiles.WorkshopCompatibility."+type).Methods.Single(m=>m.Name==method);
void Replace(ModuleDefinition module,MethodDefinition method,MethodDefinition replacement){var body=new MethodBody(method);method.Body=body;for(int i=0;i<method.Parameters.Count;i++)body.Instructions.Add(Instruction.Create(OpCodes.Ldarg,method.Parameters[i]));body.Instructions.Add(Instruction.Create(OpCodes.Call,module.ImportReference(replacement)));body.Instructions.Add(Instruction.Create(OpCodes.Ret));}
string mode=args.Length>1?args[1]:"full";
// Calls EarlyManagers.Prepare before Installer.Install resolves its connections, and EarlyManagers.VerifyBeforeLoad inside SessionGate.BeforeLoad's try block (after BoundaryHooks.CityMode), so a failure is shown through the existing refusal dialog.
void AddEarlyManagerChecks(ModuleDefinition m){
 MethodDefinition Find(string type,string method)=>m.Types.Single(t=>t.FullName=="HundredTiles.Runtime."+type).Methods.Single(x=>x.Name==method);
 bool Calls(MethodDefinition method,string name)=>method.Body.Instructions.Any(i=>i.Operand is MethodReference r && r.DeclaringType.Name=="EarlyManagers" && r.Name==name);
 var install=Find("Installer","Install");if(!Calls(install,"Prepare")){var connections=install.Body.Instructions.Single(i=>i.OpCode==OpCodes.Call && i.Operand is MethodReference r && r.Name=="Connections" && r.DeclaringType.Name=="Installer");install.Body.GetILProcessor().InsertBefore(connections,Instruction.Create(OpCodes.Call,m.ImportReference(H("EarlyManagers","Prepare"))));}
 var gate=Find("SessionGate","BeforeLoad");if(!Calls(gate,"VerifyBeforeLoad")){var cityMode=gate.Body.Instructions.Single(i=>i.OpCode==OpCodes.Call && i.Operand is MethodReference r && r.Name=="CityMode" && r.DeclaringType.Name=="BoundaryHooks");if(!gate.Body.ExceptionHandlers.Any(h=>h.HandlerType==ExceptionHandlerType.Catch && h.TryStart.Offset<=cityMode.Offset && cityMode.Offset<h.TryEnd.Offset))throw new InvalidOperationException("BoundaryHooks.CityMode is not inside BeforeLoad's try block.");gate.Body.GetILProcessor().InsertAfter(cityMode,Instruction.Create(OpCodes.Call,m.ImportReference(H("EarlyManagers","VerifyBeforeLoad"))));}
}
foreach(string file in (mode=="serializer-only"?new[]{"HundredTiles.SaveBridge.dll"}:mode=="early-managers"?new[]{"HundredTiles.Runtime.dll"}:new[]{"HundredTiles.Core.dll","HundredTiles.SaveBridge.dll","HundredTiles.Runtime.dll"})){
 string path=Path.Combine(root,file);using var m=ModuleDefinition.ReadModule(path,new ReaderParameters{AssemblyResolver=resolver,InMemory=true});
 if(file=="HundredTiles.Core.dll"){var type=m.Types.Single(t=>t.FullName=="HundredTiles.Core.ObjectPositionCodec");foreach(string name in new[]{"Encode","Decode"})Replace(m,type.Methods.Single(x=>x.Name==name),H("ExpandedObjects",name));}
 if(file=="HundredTiles.SaveBridge.dll"){Replace(m,m.Types.Single(t=>t.FullName=="HundredTiles.SaveBridge.SerializerHooks").Methods.Single(x=>x.Name=="VerifyRuntime"),H("SerializerRuntime","Verify"));var type=m.Types.Single(t=>t.FullName=="HundredTiles.SaveBridge.ObjectSaveBridge");Replace(m,type.Methods.Single(x=>x.Name=="ValidateTarget"),H("SpatialAdapters","ValidateObjectTarget"));}
 if(file=="HundredTiles.Runtime.dll" && mode=="early-managers")AddEarlyManagerChecks(m);
 else if(file=="HundredTiles.Runtime.dll"){
  AddEarlyManagerChecks(m);
  var type=m.Types.Single(t=>t.FullName=="HundredTiles.Runtime.Compatibility");var method=type.Methods.Single(x=>x.Name=="Allowed");var il=method.Body.GetILProcessor();var first=method.Body.Instructions[0];var result=new VariableDefinition(m.TypeSystem.Int32);method.Body.Variables.Add(result);method.Body.InitLocals=true;
  var instructions=new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,m.ImportReference(H("ReviewedOverlays","Evaluate"))),Instruction.Create(OpCodes.Stloc,result),Instruction.Create(OpCodes.Ldloc,result),Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Blt,first),Instruction.Create(OpCodes.Ldloc,result),Instruction.Create(OpCodes.Ret)};foreach(var ins in instructions)il.InsertBefore(first,ins);
  var add=type.Methods.Single(x=>x.Name=="AddHooks");var start=add.Body.Instructions[0];add.Body.GetILProcessor().InsertBefore(start,Instruction.Create(OpCodes.Ldarg_0));add.Body.GetILProcessor().InsertBefore(start,Instruction.Create(OpCodes.Call,m.ImportReference(H("SpatialAdapters","Prepare"))));
  var installer=m.Types.Single(t=>t.FullName=="HundredTiles.Runtime.Installer");Replace(m,installer.Methods.Single(x=>x.Name=="Method"),H("SpatialAdapters","PrefixMethod"));
 }
 string temp=path+".integrated";m.Write(temp);File.Move(temp,path,true);Console.WriteLine("Integrated: "+file);
}

