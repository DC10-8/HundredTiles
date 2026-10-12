using Mono.Cecil;
using Mono.Cecil.Cil;
using var module=ModuleDefinition.ReadModule(args[0]);
void Reorder(TypeDefinition type){foreach(var child in type.NestedTypes)Reorder(child);var methods=type.Methods.Reverse().ToArray();type.Methods.Clear();foreach(var method in methods)type.Methods.Add(method);}
foreach(var type in module.Types)Reorder(type);
if(args.Length==3 && args[2]=="fps"){
void Rename(TypeDefinition type){foreach(var child in type.NestedTypes)Rename(child);foreach(var method in type.Methods)if(method.Name=="Update" || method.Name=="LateUpdate" || method.Name=="FixedUpdate")method.Name="FpsBooster"+method.Name;}
foreach(var type in module.Types)Rename(type);
}
if(args.Length==3 && args[2]=="mutate"){
var target=module.Types.Single(t=>t.Name=="AirportAuxBuildingAI").Methods.Single(m=>m.Name=="FindRoadAccess");
var instruction=target.Body.Instructions.First(i=>i.OpCode==OpCodes.Ldc_I4 && (int)i.Operand==270);instruction.Operand=271;
}
if(args.Length==3 && args[2]=="elt-mutate"){
var target=module.Types.Single(t=>t.Name=="TerrainToolDetour").Methods.Single(m=>m.Name=="ResetUndoBuffer");
var instruction=target.Body.Instructions.First(i=>i.OpCode==OpCodes.Ldc_I4 && (int)i.Operand==1081);instruction.Operand=1082;
}
if(args.Length>=3 && args[2].StartsWith("elt-trigger")){
using var updater=ModuleDefinition.ReadModule(args[3]);
var trigger=updater.Types.Single(t=>t.FullName=="BehaviourUpdater.BehaviourRegistry").Methods.Single(m=>m.Name==(args[2]=="elt-trigger-wrong"?"UnRegisterBehaviour":"RegisterBehaviour"));
var type=module.Types.Single(t=>t.Name=="TerrainToolDetour");
var target=type.Methods.Single(m=>m.Name=="OnEnable");
var il=target.Body.GetILProcessor();var first=target.Body.Instructions[0];
il.InsertBefore(first,il.Create(OpCodes.Ldarg_0));il.InsertBefore(first,il.Create(OpCodes.Call,module.ImportReference(trigger)));
if(args[2]=="elt-trigger-mutate")target.Body.Instructions.First(i=>i.OpCode==OpCodes.Ldc_I4 && (int)i.Operand==1081).Operand=1082;
}
if(args.Length==3 && args[2]=="spatial-mutate"){
var target=module.Types.Single(t=>t.Name=="PropsManager").Methods.Single(m=>m.Name=="RaycastHoverInstance");
target.Body.Instructions.First(i=>i.OpCode==OpCodes.Ldc_I4 && (int)i.Operand==270).Operand=271;
}
module.Write(args[1]);
Console.WriteLine("Generated metadata reorder fixture: "+args[1]);
