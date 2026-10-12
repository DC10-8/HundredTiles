using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

// Development harness only. Never included in a player installation.
public static class Entry
{
    public static string Root;
    static readonly object logLock=new object();
    public static void Main(string[] args)
    {
        Root=Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_ROOT");
        if(string.IsNullOrEmpty(Root))return;
        Root=Path.GetFullPath(Root);
        if(!File.Exists(Path.Combine(Root,"isolated-test.marker")))return;
        AppDomain.CurrentDomain.AssemblyResolve+=Resolve;
        try{Initialize();}catch(Exception error){Log("BOOT FAILURE: "+error);}
    }
    static Assembly Resolve(object sender,ResolveEventArgs args)
    {
        string path=Path.Combine(Path.GetDirectoryName(typeof(Entry).Assembly.Location),new AssemblyName(args.Name).Name+".dll");
        return File.Exists(path)?Assembly.LoadFrom(path):null;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]static void Initialize(){Bootstrap.InstallEarly();}
    public static void Log(string message){lock(logLock)File.AppendAllText(Path.Combine(Root,"integration.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);}
}
