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
        try{if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_FPS_PRELOADER")=="1")LiveFpsPreloader.Install();Initialize();}
        catch(Exception error){Log("BOOT FAILURE: "+error);if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_FPS_PRELOADER")=="1")Environment.Exit(1);}
    }
    static Assembly Resolve(object sender,ResolveEventArgs args)
    {
        if(Environment.GetEnvironmentVariable("HUNDREDTILES_TEST_FPS_PRELOADER")=="1")
        {string dependency=Path.Combine(Root,"fps-loader/"+new AssemblyName(args.Name).Name+".dll");if(File.Exists(dependency))return Assembly.LoadFrom(dependency);}
        string path=Path.Combine(Path.GetDirectoryName(typeof(Entry).Assembly.Location),new AssemblyName(args.Name).Name+".dll");
        return File.Exists(path)?Assembly.LoadFrom(path):null;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]static void Initialize(){Bootstrap.InstallEarly();}
    public static void Log(string message)
    {
        lock(logLock)
        {
            string line=DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine;
            for(int attempt=0;;attempt++)
            {
                try{using(var stream=new FileStream(Path.Combine(Root,"integration.log"),FileMode.Append,FileAccess.Write,FileShare.ReadWrite))using(var writer=new StreamWriter(stream))writer.Write(line);return;}
                catch(IOException){if(attempt>=9)throw;System.Threading.Thread.Sleep(20);}
            }
        }
    }
}
