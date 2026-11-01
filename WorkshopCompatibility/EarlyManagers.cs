using System;using System.Collections.Generic;using System.Reflection;using ColossalFramework;using UnityEngine;
namespace HundredTiles.WorkshopCompatibility {
 // HundredTiles enlarges manager arrays by editing each manager's Awake. Awake runs once, so a manager that another MOD
 // created before HundredTiles installed keeps vanilla-sized arrays, and expanded-grid code later indexes past their end
 // (seen as "Array index is out of range" in NetManager.InitializeNode while a save loads).
 // Prepare runs at install: empty grid-only arrays are reallocated at expanded size; managers whose Awake also builds
 // textures or initialised cells stop activation with their names. VerifyBeforeLoad re-checks every live manager before a city loads.
 public static class EarlyManagers {
  public sealed class Field { public string Name; public int Vanilla, Expanded; public Field(string name,int vanilla,int expanded){Name=name;Vanilla=vanilla;Expanded=expanded;} }
  public sealed class Spec { public string Type; public bool Resizable; public Field[] Fields; public Spec(string type,bool resizable,params Field[] fields){Type=type;Resizable=resizable;Fields=fields;} }
  static Field F(string name,int vanilla,int expanded){return new Field(name,vanilla,expanded);}
  // Sizes are the Awake edits in ReviewedPlan.tsv (100 tiles).
  public static readonly Spec[] Specs={
   new Spec("NetManager",true,F("m_nodeGrid",72900,90000),F("m_segmentGrid",72900,90000),F("m_tileNodesCount",1050,4200)),
   new Spec("BuildingManager",true,F("m_buildingGrid",72900,90000),F("m_buildingGrid2",2025,2500)),
   new Spec("PropManager",true,F("m_propGrid",72900,90000)),
   new Spec("ZoneManager",true,F("m_zoneGrid",22500,90000)),
   new Spec("TreeManager",true,F("m_treeGrid",291600,360000)),
   new Spec("VehicleManager",true,F("m_vehicleGrid",291600,360000),F("m_vehicleGrid2",2916,3600),F("m_parkedGrid",291600,360000)),
   new Spec("CitizenManager",true,F("m_citizenGrid",4665600,5760000)),
   new Spec("RenderManager",true,F("m_groups",2025,2500),F("m_megaGroups",81,100),F("m_updatedGroups1",32,40)),
   new Spec("NotificationManager",true,F("m_groupData",25*25*77,50*50*77),F("m_groupData2",25*25*77,50*50*77),F("m_groupDataCount",625,2500)),
   new Spec("DistrictManager",false,F("m_districtGrid",262144,1000000)),
   new Spec("ElectricityManager",false,F("m_electricityGrid",65536,258064)),
   new Spec("WaterManager",false,F("m_waterGrid",65536,258064)),
   new Spec("ImmaterialResourceManager",false,F("m_localFinalResources",2293760,8750000)),
   new Spec("NaturalResourceManager",false,F("m_naturalResources",262144,323761)),
   new Spec("GameAreaManager",false,F("m_areaGrid",25,100)),
   new Spec("TerrainManager",false,F("m_rawHeights",1168561,1442401)),
  };
  const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;

  public static void Prepare(){var resized=new List<string>();var blocked=new List<string>();
   foreach(Spec spec in Specs){object manager=Existing(spec.Type);if(manager==null)continue;string problem=PrepareInstance(manager,spec);
    if(problem==null)resized.Add(spec.Type);else if(problem.Length!=0)blocked.Add(problem);}
   if(resized.Count!=0)Debug.Log("[HundredTiles] Managers created before HundredTiles were resized to 100 tiles: "+string.Join(", ",resized.ToArray()));
   if(blocked.Count!=0)throw new InvalidOperationException(Message(blocked));}

  // null: resized now; "": already expanded; otherwise the reason it cannot be fixed.
  public static string PrepareInstance(object manager,Spec spec){Type type=manager.GetType();var arrays=new Array[spec.Fields.Length];bool vanilla=false,expanded=true;
   for(int i=0;i<spec.Fields.Length;i++){Field f=spec.Fields[i];FieldInfo info=type.GetField(f.Name,Instance);arrays[i]=info==null?null:info.GetValue(manager) as Array;int length=arrays[i]==null?-1:arrays[i].Length;
    if(length!=f.Expanded)expanded=false;if(length==f.Vanilla)vanilla=true;else if(length!=f.Expanded)return spec.Type+"."+f.Name+" length "+length+" (expected "+f.Vanilla+" or "+f.Expanded+")";}
   if(expanded)return "";
   if(!spec.Resizable)return spec.Type+" (created before HundredTiles)";
   for(int i=0;i<spec.Fields.Length;i++)if(arrays[i].Length==spec.Fields[i].Vanilla && !IsEmpty(arrays[i]))return spec.Type+"."+spec.Fields[i].Name+" already holds data";
   for(int i=0;i<spec.Fields.Length;i++){Field f=spec.Fields[i];if(arrays[i].Length==f.Expanded)continue;type.GetField(f.Name,Instance).SetValue(manager,Array.CreateInstance(arrays[i].GetType().GetElementType(),f.Expanded));}
   return vanilla?null:"";}

  public static void VerifyBeforeLoad(){Type installer=Type.GetType("HundredTiles.Runtime.Installer, HundredTiles.Runtime");PropertyInfo installed=installer==null?null:installer.GetProperty("IsInstalled",BindingFlags.Public|BindingFlags.Static);
   if(installed==null || !(bool)installed.GetValue(null,null))return;var wrong=new List<string>();
   foreach(Spec spec in Specs){object manager=Existing(spec.Type);if(manager!=null)wrong.AddRange(Mismatches(manager,spec));}
   if(wrong.Count!=0)throw new InvalidOperationException(Message(wrong));}

  public static List<string> Mismatches(object manager,Spec spec){var wrong=new List<string>();Type type=manager.GetType();
   foreach(Field f in spec.Fields){FieldInfo info=type.GetField(f.Name,Instance);Array array=info==null?null:info.GetValue(manager) as Array;int length=array==null?-1:array.Length;if(length!=f.Expanded)wrong.Add(spec.Type+"."+f.Name+" length "+length+" (expected "+f.Expanded+")");}
   return wrong;}

  static string Message(List<string> problems){return "Game manager arrays are not at 100-tile size, usually because another MOD created the managers before HundredTiles: "+string.Join("; ",problems.ToArray())+
   ". ゲームのマネージャーの配列が100タイル用の大きさになっていません。多くの場合、他のMODがHundredTilesより先にマネージャーを作成したことが原因です。";}

  static object Existing(string name){Type type=typeof(NetManager).Assembly.GetType(name);if(type==null)return null;Type singleton=typeof(Singleton<>).MakeGenericType(type);
   if(!(bool)singleton.GetProperty("exists").GetValue(null,null))return null;return singleton.GetProperty("instance").GetValue(null,null);}

  static bool IsEmpty(Array array){
   if(array is ushort[]){foreach(ushort v in (ushort[])array)if(v!=0)return false;return true;}
   if(array is uint[]){foreach(uint v in (uint[])array)if(v!=0)return false;return true;}
   if(array is int[]){foreach(int v in (int[])array)if(v!=0)return false;return true;}
   if(array is ulong[]){foreach(ulong v in (ulong[])array)if(v!=0)return false;return true;}
   if(array is byte[]){foreach(byte v in (byte[])array)if(v!=0)return false;return true;}
   Type element=array.GetType().GetElementType();object empty=element.IsValueType?Activator.CreateInstance(element):null;
   foreach(object v in array)if(element.IsValueType?!empty.Equals(v):v!=null)return false;return true;}
 }
}
