using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;
using HundredTiles.Utilities;

namespace HundredTiles.Runtime
{
    public static class Installer
    {
        public const string Owner="HundredTiles.Runtime";
        static bool installed;
        public static bool IsInstalled { get { return installed; } }
        public static string FailureReason;
        static void Progress(string text)
        {
            string folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Colossal Order/Cities_Skylines/HundredTiles-diagnostics");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"installation-progress.txt"),DateTime.UtcNow.ToString("o")+" "+text);
        }
        public static IList<HookSpec> Connections()
        {
            var plan=PatchPlan.Embedded();var game=typeof(TerrainManager).Assembly;
            SerializerHooks.Verify(typeof(TerrainManager).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic));
            var hooks=new List<HookSpec>();
            foreach(var method in plan.Methods.Values)
            {
                var target=ReviewedMethod.Resolve(game,method);
                var body=target.GetMethodBody().GetILAsByteArray();
                if(!ReviewedMethod.Equivalent(ReviewedMethod.Reference(game,method.Token),target))
                {
                    var info=Harmony.GetPatchInfo(target);
                    string owners=info==null?"none":string.Join(",",new List<string>(info.Owners).ToArray());
                    string folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Colossal Order/Cities_Skylines/HundredTiles-diagnostics");
                    System.IO.Directory.CreateDirectory(folder);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"changed-method.txt"),method.Name+"\nExpected: "+method.BodyHash+"\nActual: "+PlannedTranspiler.Hash(body)+"\nOwners: "+owners+"\nModule: "+game.ManifestModule.ModuleVersionId+"\nIL: "+BitConverter.ToString(body));
                    throw new InvalidOperationException("Reviewed method changed: "+method.Name+"; patch owners: "+owners);
                }
                hooks.Add(new HookSpec{Target=target,Transpiler=typeof(PlannedTranspiler).GetMethod("Transpile")});
            }
            hooks.AddRange(SerializerHooks.Specifications());hooks.AddRange(AreaHooks.Specifications());
            hooks.AddRange(ObjectPositionHooks.Specifications());hooks.AddRange(ObjectGridHooks.Specifications());
            hooks.AddRange(DistrictNameHooks.Specifications());hooks.AddRange(UtilityHooks.Specifications());hooks.AddRange(UtilitySerializerHooks.Specifications());
            hooks.AddRange(BoundaryHooks.Specifications());hooks.AddRange(TaxiHooks.Specifications());
            hooks.AddRange(ServiceSerializerHooks.Specifications());
            hooks.AddRange(ServiceHooks.Specifications());
            var merged=new Dictionary<MethodBase,HookSpec>();
            foreach(var hook in hooks)
            {
                if(hook.Target==null)throw new MissingMethodException("A connection has no target.");
                HookSpec entry;
                if(!merged.TryGetValue(hook.Target,out entry))merged.Add(hook.Target,entry=new HookSpec{Target=hook.Target});
                Merge(ref entry.Prefix,hook.Prefix);Merge(ref entry.Postfix,hook.Postfix);
                Merge(ref entry.Finalizer,hook.Finalizer);Merge(ref entry.Transpiler,hook.Transpiler);
            }
            return new List<HookSpec>(merged.Values);
        }
        static void Merge(ref MethodInfo destination,MethodInfo source)
        {if(source==null)return;if(destination!=null)throw new InvalidOperationException("Overlapping connection: "+source.Name);destination=source;}
        public static void Install()
        {
            if(installed)return;
            AssemblyFile.ManagedDirectory=System.IO.Path.Combine(UnityEngine.Application.dataPath,"Managed");
            // Guard precedes participant registration, reflection and all patches.
            if(!PatchPlan.Embedded().CanActivate)throw new InvalidOperationException("Activation blocked: remaining spatial/render boundaries and Unity save/reload validation are incomplete.");
            if(ColossalFramework.Singleton<TerrainManager>.exists)throw new InvalidOperationException("Enable HundredTiles before game startup. Restart the game after enabling this mod.");
            var hooks=Connections();
            Progress("Connections resolved");
            Compatibility.AddHooks(hooks);
            Progress("Compatibility resolved");
            var conflicts=new List<string>();
            foreach(var hook in hooks)
            {
                var patches=Harmony.GetPatchInfo(hook.Target);
                if(patches!=null)foreach(string owner in patches.Owners)
                    if(owner!=Owner)
                    {
                        // Reviewed AreaPOBuilder wrappers only toggle the
                        // zone-sweep flag; they do not access spatial grids.
                        if(Compatibility.Allowed(owner,hook.Target,patches))continue;
                        conflicts.Add(hook.Target.DeclaringType.FullName+"."+hook.Target.Name+": "+owner);
                    }
            }
            if(conflicts.Count!=0)throw new InvalidOperationException("Patch conflicts: "+string.Join("; ",conflicts.ToArray()));
            var harmony=new Harmony(Owner);
            try
            {
                UtilitySaveBridge.Register();
                ServiceSaveBridge.Register();
                foreach(var hook in hooks){Progress("Applying "+hook.Target.DeclaringType.FullName+"."+hook.Target.Name);harmony.Patch(hook.Target,Method(hook.Prefix),Method(hook.Postfix),Method(hook.Transpiler),Method(hook.Finalizer));}
                installed=true;
                Progress("Installed "+hooks.Count);
                UnityEngine.Debug.Log("[HundredTiles] Installed: "+hooks.Count+" targets; 10x10 tiles; purchase limit 100.");
            }
            catch {harmony.UnpatchAll(Owner);throw;}
        }
        static HarmonyMethod Method(MethodInfo method){return method==null?null:new HarmonyMethod(method);}
        public static void Remove()
        {
            if(!installed)return;
            // Managers retain expanded arrays even at the main menu. Restoring
            // vanilla indexing in this process would corrupt later simulation.
            UnityEngine.Debug.Log("[HundredTiles] Disable takes effect after restarting the game; expanded-array patches remain active for this process.");
        }
    }
}

