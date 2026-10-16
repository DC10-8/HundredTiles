using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using ColossalFramework;
using HarmonyLib;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;
using ICities;

namespace HundredTiles.Runtime
{
    public static class NetworkRecalculationCompatibility
    {
        sealed class Job { public MethodBase Method; public object Target; public ushort Segment; }
        static readonly List<Job> pending=new List<Job>();
        static int scheduled;
        static bool failed;
        public static int Pending { get { lock(pending)return pending.Count; } }
        public static void AddHooks(Assembly assembly,IList<HookSpec> hooks)
        {
            var method=assembly.GetType("MoreNetworkCapacityRenew.TMPECompatibility",true).GetMethod("EnsureSegmentEndManagersCapacity");
            if(ReviewedMethod.SemanticHash(method)!="927B84E5741A3E5496D581BF941833F2D226023F915CFE59474ABB8ED392C400")
                throw new InvalidOperationException("Reviewed network capacity recalculation changed.");
            hooks.Add(new HookSpec {Target=method,Transpiler=typeof(NetworkRecalculationCompatibility).GetMethod("DeferCalls")});
        }
        public static IEnumerable<CodeInstruction> DeferCalls(IEnumerable<CodeInstruction> instructions)
        {
            int count=0;
            foreach(var source in instructions)
            {
                var item=new CodeInstruction(source);var method=item.operand as MethodInfo;
                if(method!=null && method.Name=="Invoke" && method.DeclaringType==typeof(MethodBase) && method.GetParameters().Length==2)
                {item.opcode=OpCodes.Call;item.operand=typeof(NetworkRecalculationCompatibility).GetMethod("InvokeWhenReady");count++;}
                yield return item;
            }
            if(count!=2)throw new InvalidOperationException("Network capacity recalculation call count differs.");
        }
        public static object InvokeWhenReady(MethodBase method,object target,object[] args)
        {
            if(method==null || args==null || args.Length!=1 || !(args[0] is ushort) ||
               (method.DeclaringType.FullName!="TrafficManager.Manager.Impl.ExtSegmentEndManager" || method.Name!="Recalculate") &&
               (method.DeclaringType.FullName!="TrafficManager.Manager.Impl.RoutingManager" || method.Name!="RecalculateSegment"))
                throw new InvalidOperationException("Unexpected network capacity recalculation request.");
            if(Singleton<LoadingManager>.exists && Singleton<LoadingManager>.instance.m_currentlyLoading)
            {
                ushort segment=(ushort)args[0];
                lock(pending)
                {
                    foreach(var job in pending)if(job.Method==method && job.Segment==segment)return null;
                    pending.Add(new Job{Method=method,Target=target,Segment=segment});
                }
                return null;
            }
            return method.Invoke(target,args);
        }
        public static void Update()
        {
            if(failed || Pending==0 || !Singleton<LoadingManager>.exists || Singleton<LoadingManager>.instance.m_currentlyLoading ||
               !Singleton<LoadingManager>.instance.m_loadingComplete || Interlocked.CompareExchange(ref scheduled,1,0)!=0)return;
            Singleton<SimulationManager>.instance.AddAction(Drain);
        }
        static void Drain()
        {
            try
            {
                Job[] jobs;lock(pending)jobs=pending.ToArray();
                foreach(var job in jobs)
                {
                    var segment=Singleton<NetManager>.instance.m_segments.m_buffer[job.Segment];
                    if((segment.m_flags&(NetSegment.Flags.Created|NetSegment.Flags.Deleted))==NetSegment.Flags.Created)
                    {
                        if(segment.Info==null)throw new InvalidOperationException("Loaded segment still has no prefab: "+job.Segment);
                        job.Method.Invoke(job.Target,new object[]{job.Segment});
                    }
                    lock(pending)pending.Remove(job);
                }
                UnityEngine.Debug.Log("[HundredTiles] Deferred TM:PE network recalculation completed after loading: "+jobs.Length+" requests.");
            }
            catch(Exception error){failed=true;RuntimeSaveBridge.LoadFailure(error);UnityEngine.Debug.LogException(error);throw;}
            finally{Interlocked.Exchange(ref scheduled,0);}
        }
        public static void Clear(){lock(pending)pending.Clear();failed=false;Interlocked.Exchange(ref scheduled,0);}
    }
    public sealed class NetworkRecalculationThreading : ThreadingExtensionBase
    {
        public override void OnUpdate(float realTimeDelta,float simulationTimeDelta){NetworkRecalculationCompatibility.Update();}
        public override void OnReleased(){NetworkRecalculationCompatibility.Clear();base.OnReleased();}
    }
    public sealed class NetworkRecalculationLoading : LoadingExtensionBase
    {
        public override void OnLevelUnloading(){NetworkRecalculationCompatibility.Clear();base.OnLevelUnloading();}
    }
}
