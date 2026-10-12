using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;
using HundredTiles.SaveBridge;

namespace HundredTiles.Utilities
{
    public static class ServiceHooks
    {
        delegate bool Calculate(int x,int z,ushort[] buffer,int[] global,ushort[] target,int index);
        delegate void Total(int[] buffer,long[] multipliers,int[] target);
        static Calculate calculate;
        static Total total;
        public static bool Step(ImmaterialResourceManager __instance,int subStep,
            ushort[] ___m_localTempResources,ushort[] ___m_localFinalResources,
            int[] ___m_globalTempResources,int[] ___m_globalFinalResources,
            int[] ___m_totalTempResources,int[] ___m_totalFinalResources,long[] ___m_totalTempResourcesMul)
        {
            if(subStep==0 || subStep==1000)return false;
            if(___m_localTempResources.Length!=ServiceGrid.Values || ___m_localFinalResources.Length!=ServiceGrid.Values)throw new InvalidOperationException("Service simulation requires expanded grids.");
            if(calculate==null)calculate=(Calculate)Delegate.CreateDelegate(typeof(Calculate),typeof(ImmaterialResourceManager).GetMethod("CalculateLocalResources",BindingFlags.Static|BindingFlags.NonPublic));
            if(total==null)total=(Total)Delegate.CreateDelegate(typeof(Total),typeof(ImmaterialResourceManager).GetMethod("CalculateTotalResources",BindingFlags.Static|BindingFlags.NonPublic));
            uint frame=Singleton<SimulationManager>.instance.m_currentFrameIndex;
            int start=ServiceGrid.RowStart(frame),end=ServiceGrid.RowEnd(frame);
            for(int z=start;z<end;z++)
            {
                int min=-1,max=-1;
                for(int x=0;x<ServiceGrid.Side;x++)
                {
                    int index=(z*ServiceGrid.Side+x)*ServiceGrid.Channels;
                    if(calculate(x,z,___m_localTempResources,___m_globalFinalResources,___m_localFinalResources,index)){if(min<0)min=x;max=x;}
                    int multiplier=___m_localFinalResources[index+16];
                    for(int channel=0;channel<ServiceGrid.Channels;channel++)
                    {
                        int value=___m_localFinalResources[index+channel];
                        ___m_totalTempResources[channel]=(int)Math.Min(int.MaxValue,(long)___m_totalTempResources[channel]+value);
                        ___m_totalTempResourcesMul[channel]+=(long)value*multiplier;
                        ___m_localTempResources[index+channel]=0;
                    }
                }
                if(min>=0)__instance.AreaModified(min,z,max,z);
            }
            if((frame&255)==255)
            {
                total(___m_totalTempResources,___m_totalTempResourcesMul,___m_totalFinalResources);
                var statistics=Singleton<StatisticsManager>.instance.Acquire<StatisticArray>(StatisticType.ImmaterialResource);
                for(int i=0;i<ServiceGrid.Channels;i++)
                {
                    ___m_globalFinalResources[i]=___m_globalTempResources[i];___m_globalTempResources[i]=0;
                    ___m_totalTempResources[i]=0;___m_totalTempResourcesMul[i]=0;
                    statistics.Acquire<StatisticInt32>(i,ServiceGrid.Channels).Set(___m_totalFinalResources[i]);
                }
            }
            return false;
        }
        public static IList<HookSpec> Specifications()
        {
            var manager=typeof(ImmaterialResourceManager);
            return new List<HookSpec>{new HookSpec{Target=manager.GetMethod("SimulationStepImpl",BindingFlags.Instance|BindingFlags.NonPublic),Prefix=typeof(ServiceHooks).GetMethod("Step")},new HookSpec{Target=manager.GetMethod("GetParkAreaResourceIndexes"),Prefix=typeof(ExpandedServices).GetMethod("ParkIndexes")}};
        }
    }
}
