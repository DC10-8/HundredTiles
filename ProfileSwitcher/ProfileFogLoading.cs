using System;
using System.Collections.Generic;
using System.Reflection;
using ICities;
using UnityEngine;
namespace HundredTiles.Profiles {
 // The 100-tile backup predates the runtime fog controller. Keep the shared
 // fallback in the profile assembly so both distributions retain the fix.
 public sealed class ProfileFogLoading : LoadingExtensionBase {
  GameObject controller;
  public override void OnLevelLoaded(LoadMode mode){if(mode!=LoadMode.NewGame && mode!=LoadMode.LoadGame)return;
   Assembly runtime=null;foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="HundredTiles.Runtime")runtime=a;
   if(runtime==null || !(bool)runtime.GetType("HundredTiles.Runtime.Installer").GetProperty("IsInstalled").GetValue(null,null))return;
   if(runtime.GetType("HundredTiles.Runtime.ExpandedMapFogLoading")!=null)return;
   controller=new GameObject("HundredTiles.SharedClearFog");controller.AddComponent<ProfileClearFog>();Debug.Log("[HundredTiles] Shared clear-city fog enabled for the legacy 100-tile profile.");
  }
  public override void OnLevelUnloading(){if(controller!=null){var fog=controller.GetComponent<ProfileClearFog>();if(fog!=null)fog.Restore();UnityEngine.Object.Destroy(controller);controller=null;}base.OnLevelUnloading();}
 }
 public sealed class ProfileClearFog : MonoBehaviour {
  readonly Dictionary<Behaviour,bool> effects=new Dictionary<Behaviour,bool>();
  Vector4 oldFog,oldPollution;bool oldUnityFog,restored;float nextScan;
  void Awake(){oldFog=Shader.GetGlobalVector("_FogParams0");oldPollution=Shader.GetGlobalVector("_FogPollutionParams");oldUnityFog=RenderSettings.fog;Scan();Apply();}
  void Capture(Behaviour value){if(!effects.ContainsKey(value))effects.Add(value,value.enabled);}
  void Scan(){foreach(var v in UnityEngine.Object.FindObjectsOfType<FogProperties>())Capture(v);foreach(var v in UnityEngine.Object.FindObjectsOfType<FogEffect>())Capture(v);foreach(var v in UnityEngine.Object.FindObjectsOfType<DayNightFogEffect>())Capture(v);nextScan=Time.realtimeSinceStartup+1f;}
  void LateUpdate(){if(restored)return;if(Time.realtimeSinceStartup>=nextScan)Scan();Apply();}
  public static Vector4 ClearDensity(Vector4 v){v.x=0;v.y=0;return v;}
  public static Vector4 ClearPollution(Vector4 v){v.x=0;return v;}
  void Apply(){foreach(var p in effects)if(p.Key!=null && p.Key.enabled)p.Key.enabled=false;Shader.SetGlobalVector("_FogParams0",ClearDensity(Shader.GetGlobalVector("_FogParams0")));Shader.SetGlobalVector("_FogPollutionParams",ClearPollution(Shader.GetGlobalVector("_FogPollutionParams")));RenderSettings.fog=false;}
  public void Restore(){if(restored)return;restored=true;foreach(var p in effects)if(p.Key!=null)p.Key.enabled=p.Value;effects.Clear();Shader.SetGlobalVector("_FogParams0",oldFog);Shader.SetGlobalVector("_FogPollutionParams",oldPollution);RenderSettings.fog=oldUnityFog;}
  void OnDestroy(){Restore();}
 }
}
