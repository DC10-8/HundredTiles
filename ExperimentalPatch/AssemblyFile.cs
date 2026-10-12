using System;
using System.IO;
using System.Reflection;

namespace HundredTiles.ExperimentalPatch
{
    public static class AssemblyFile
    {
        public static string ManagedDirectory;
        public static string Resolve(Assembly assembly)
        {
            if(!string.IsNullOrEmpty(assembly.Location))return assembly.Location;
            string name=assembly.GetName().Name;
            if(name!="Assembly-CSharp" && name!="ColossalManaged")throw new InvalidOperationException("Unsupported memory-loaded assembly: "+name);
            if(string.IsNullOrEmpty(ManagedDirectory))throw new InvalidOperationException("Game managed directory was not initialized.");
            string path=Path.Combine(ManagedDirectory,name+".dll");
            if(!File.Exists(path))throw new FileNotFoundException("Game assembly file missing.",path);
            var disk=(Assembly)typeof(Assembly).GetMethod("ReflectionOnlyLoadFrom",new[]{typeof(string)}).Invoke(null,new object[]{path});
            if(disk.ManifestModule.ModuleVersionId!=assembly.ManifestModule.ModuleVersionId)
                throw new InvalidOperationException("Memory-loaded game assembly differs from disk: "+name);
            return path;
        }
    }
}
