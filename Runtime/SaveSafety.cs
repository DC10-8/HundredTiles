using System;
using System.Collections.Generic;
using HarmonyLib;
using HundredTiles.SaveBridge;

namespace HundredTiles.Runtime
{
    public static class SaveSafety
    {
        public static void Validate(IList<HookSpec> hooks)
        {
            if(NetworkRecalculationCompatibility.Pending!=0)throw new InvalidOperationException("Saving blocked: deferred network recalculation has not completed.");
            foreach(var hook in hooks)
            {
                var patches=Harmony.GetPatchInfo(hook.Target);
                bool own=false;
                if(patches!=null)foreach(string owner in patches.Owners)
                {
                    if(owner==Installer.Owner)own=true;
                    else if(!Compatibility.Allowed(owner,hook.Target,patches))throw new InvalidOperationException("Saving blocked by a new patch conflict: "+hook.Target.DeclaringType.FullName+"."+hook.Target.Name+": "+owner);
                }
                if(!own)throw new InvalidOperationException("Saving blocked: HundredTiles patch was removed: "+hook.Target.DeclaringType.FullName+"."+hook.Target.Name);
            }
        }
    }
}
