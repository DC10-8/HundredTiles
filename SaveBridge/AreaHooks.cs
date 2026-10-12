using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HundredTiles.Core;

namespace HundredTiles.SaveBridge
{
    // Gated hooks: preserve the game's actual UnlockArea transaction and its
    // notifications, achievement and rollback behavior through reviewed IL.
    public static class AreaHooks
    {
        public static bool MaxAreaCount(ref int __result) { __result=100; return false; }
        public static bool GetArea(GameAreaManager __instance,int x,int z,ref int __result)
        { __result=x<0 || z<0 || x>=10 || z>=10 ? -2 : __instance.m_areaGrid[z*10+x]; return false; }
        public static bool CanUnlock(GameAreaManager __instance,int x,int z,ref bool __result)
        {
            var state=new OwnershipState {Grid=__instance.m_areaGrid,Count=__instance.m_areaCount,Start=0};
            __result=false;
            if(x<0 || z<0 || x>=10 || z>=10 || state.Count>=100 || state.Grid[z*10+x]!=0) return false;
            // Allow all 100 purchases from the start; the native purchase
            // transaction still checks price and updates ownership.
            __result=Ownership.CanPurchase(state,x,z);
            __instance.m_AreasWrapper.OnCanUnlockArea(x,z,ref __result);
            return false;
        }
        public static IList<HookSpec> Specifications()
        {
            var result=new List<HookSpec>();
            foreach(string name in new[]{"get_MaxAreaCount","GetArea","CanUnlock"})
                result.Add(new HookSpec {Target=typeof(GameAreaManager).GetMethod(name,BindingFlags.Instance|BindingFlags.Public),
                    Prefix=typeof(AreaHooks).GetMethod(name=="get_MaxAreaCount"?"MaxAreaCount":name)});
            return result;
        }
    }
}
