using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class IptNodeCacheRepair
{
    public static void Run(string input,string output,string gameManaged)
    {
        string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input)));
        bool ipt3=hash=="92E819A07CFD18C6E76D8A78F32BEDF54A0B091D0E49F2BA2F593F9444F17BBC";
        if(!ipt3 && hash!="0519E418B0DDB6A7AB8C717C12B3521B9A38DF7784D9C0E8B49BBB61D02502C0")throw new InvalidOperationException("Unreviewed IPT version; no files changed.");
        if(Path.GetFullPath(input)==Path.GetFullPath(output) || File.Exists(output))throw new InvalidOperationException("Use a new output file.");
        using var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(gameManaged);resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(input))!);
        using var module=ModuleDefinition.ReadModule(input,new ReaderParameters{AssemblyResolver=resolver});
        var cache=module.Types.Single(t=>t.FullName=="ImprovedPublicTransport.Data.CachedNodeData");
        int changed=0;
        foreach(var method in cache.Methods.Where(m=>m.Name=="TryLoadData" || m.Name=="OnSaveData"))
        foreach(var instruction in method.Body.Instructions)
            if(instruction.OpCode==OpCodes.Ldc_I4 && instruction.Operand is int value && value==32768){instruction.Operand=65536;changed++;}
        if(changed!=(ipt3?3:2))throw new InvalidOperationException("Expected reviewed node cache allocation/save sites.");
        if(ipt3)
        {
            var target=module.Types.Single(t=>t.FullName=="ImprovedPublicTransport.HarmonyPatches.NetManagerPatches.ReleaseNodePatch").Methods.Single(m=>m.Name=="ReleaseNodePost");
            var field=cache.Fields.Single(f=>f.Name=="m_cachedNodeData");
            var ret=target.Body.Instructions.Last();
            if(ret.OpCode!=OpCodes.Ret)throw new InvalidOperationException("Reviewed release method exit changed.");
            var il=target.Body.GetILProcessor();var first=target.Body.Instructions[0];
            foreach(var instruction in new[]{Instruction.Create(OpCodes.Ldsfld,field),Instruction.Create(OpCodes.Brfalse,ret),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldsfld,field),Instruction.Create(OpCodes.Ldlen),Instruction.Create(OpCodes.Conv_I4),Instruction.Create(OpCodes.Bge,ret)})il.InsertBefore(first,instruction);
        }
        module.Write(output);
        Console.WriteLine($"IPT node cache repair: ushort ID range 65536; allocation/save changes={changed}; release guard={ipt3}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output)))}");
    }
}
