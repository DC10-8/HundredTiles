using System;
using System.Reflection;
using UnityEngine;

namespace HundredTiles.StartupRepairs
{
    // Embedded by the repair tool; no extra player-side DLL dependency.
    public static class RecoverableTypeScan
    {
        public static Type[] GetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch(ReflectionTypeLoadException error)
            {
                Type[] partial = error.Types ?? new Type[0];
                int count=0;
                foreach(Type type in partial)if(type!=null)count++;
                Type[] available=new Type[count];
                count=0;
                foreach(Type type in partial)if(type!=null)available[count++]=type;
                Debug.LogWarning("TFW: continuing prerequisite scan with " + count + " loadable types from " + assembly.FullName);
                if(error.LoaderExceptions!=null)
                    foreach(Exception cause in error.LoaderExceptions)
                        if(cause!=null)Debug.LogWarning("TFW: type loader detail: " + cause.Message);
                return available;
            }
        }
    }
}
