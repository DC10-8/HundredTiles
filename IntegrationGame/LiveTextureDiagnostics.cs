using UnityEngine;
using System.Collections;
using System;
using System.IO;
using System.Reflection;

static class LiveTextureDiagnostics
{
    static Type Find(string name){foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){var type=assembly.GetType(name);if(type!=null)return type;}throw new Exception("Texture fixture type missing: "+name);}
    public static void VerifyRepair()
    {
        const BindingFlags flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var safety=Find("HyperLoad.Core.TextureCache.CompressionSafety");
        var requires=safety.GetMethod("RequiresExpansion",flags);
        if(!(bool)requires.Invoke(null,new object[]{24,16,5,TextureFormat.DXT1}) || (bool)requires.Invoke(null,new object[]{32,16,6,TextureFormat.DXT1}) || (bool)requires.Invoke(null,new object[]{24,16,1,TextureFormat.RGBA32}))throw new Exception("Unsafe mip classification failed.");
        var dds=new byte[400];
        using(var stream=new MemoryStream(dds))using(var writer=new BinaryWriter(stream))
        {
            writer.Write(0x20534444);writer.Write(124);writer.Write(0xA1007);writer.Write(16);writer.Write(24);writer.Write(192);writer.Write(0);writer.Write(5);
            stream.Position=76;writer.Write(32);writer.Write(4);writer.Write(0x31545844);
            stream.Position=108;writer.Write(0x401008);
            for(int i=128;i<dds.Length;i+=8){dds[i]=0xE0;dds[i+1]=0x07;}
        }
        byte[] serialized;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(false);writer.Write(typeof(Texture2D).AssemblyQualifiedName);writer.Write("Isolated NPOT DDS");writer.Write(false);writer.Write(1);writer.Write(dds.Length);writer.Write(dds);serialized=stream.ToArray();
        }
        var decoded=Find("HyperLoad.Core.Content.AssetDecoder").GetMethod("DecodeTexture",flags).Invoke(null,new object[]{serialized,0,serialized.Length,12});
        if(decoded==null)throw new Exception("NPOT DDS decoding failed.");
        var type=decoded.GetType();
        int width=(int)type.GetField("Width",flags).GetValue(decoded),height=(int)type.GetField("Height",flags).GetValue(decoded),mips=(int)type.GetField("MipCount",flags).GetValue(decoded);
        var format=(TextureFormat)type.GetField("Format",flags).GetValue(decoded);
        var raw=(byte[])type.GetField("Pixels",flags).GetValue(decoded);
        if(width!=24 || height!=16 || mips!=5 || format!=TextureFormat.RGBA32 || raw.Length!=2044)throw new Exception("DDS conversion changed dimensions or mip count: "+width+"x"+height+" format="+format+" mips="+mips+" bytes="+raw.Length);
        Texture2D texture=null,png=null,built=null;
        try
        {
            texture=new Texture2D(width,height,format,true);texture.LoadRawTextureData(raw);texture.Apply(false,false);
            foreach(var color in texture.GetPixels32())if(color.r!=0 || color.g!=255 || color.b!=0 || color.a!=255)throw new Exception("DDS conversion changed decoded pixel colors.");
            png=new Texture2D(24,16,TextureFormat.RGBA32,false);var pixels=new Color32[384];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(0,255,0,255);png.SetPixels32(pixels);png.Apply(false,false);
            byte[] source=png.EncodeToPNG();var recipeType=Find("HyperLoad.Core.TextureCache.TextureRecipe");var recipe=Activator.CreateInstance(recipeType,true);
            recipeType.GetField("InitialFormat",flags).SetValue(recipe,TextureFormat.DXT5);recipeType.GetField("Mipmap",flags).SetValue(recipe,true);recipeType.GetField("AnisoLevel",flags).SetValue(recipe,1);recipeType.GetField("FilterMode",flags).SetValue(recipe,FilterMode.Bilinear);recipeType.GetField("HighQuality",flags).SetValue(recipe,true);recipeType.GetField("ApplyUpdateMipmaps",flags).SetValue(recipe,true);
            built=(Texture2D)Find("HyperLoad.Core.TextureCache.CompressedTextureBuilder").GetMethod("Build",flags).Invoke(null,new object[]{recipe,-10001,source,"Isolated safe NPOT PNG"});
            if(built==null || built.width!=24 || built.height!=16 || built.mipmapCount!=5 || built.format==TextureFormat.DXT1 || built.format==TextureFormat.DXT5)throw new Exception("Runtime PNG compression safety failed.");
            foreach(var color in built.GetPixels32())if(color.r!=0 || color.g!=255 || color.b!=0 || color.a!=255)throw new Exception("PNG safe format changed colors.");
        }
        finally{if(texture!=null)UnityEngine.Object.Destroy(texture);if(png!=null)UnityEngine.Object.Destroy(png);if(built!=null)UnityEngine.Object.Destroy(built);}
        Entry.Log("PASS: unsafe NPOT DDS and PNG retain 24x16 dimensions, five mip levels and exact decoded colors with uncompressed GPU upload; power-of-two compressed images are unaffected.");
    }
    public static IEnumerator Run()
    {
        foreach(bool regenerate in new[]{false,true})
        {
            Debug.Log("STARTUP TEXTURE DIAGNOSTIC BEGIN regenerate="+regenerate);
            var texture=new Texture2D(24,16,TextureFormat.RGB24,true);
            try
            {
                var pixels=new Color[24*16];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(0.2f,0.6f,0.3f,1f);
                texture.SetPixels(pixels);texture.Apply(true,false);
                texture.Compress(true);
                yield return new WaitForEndOfFrame();
                Debug.Log("STARTUP TEXTURE DIAGNOSTIC compressed="+texture.format);
                texture.Apply(regenerate,false);
                yield return new WaitForEndOfFrame();
                Debug.Log("STARTUP TEXTURE DIAGNOSTIC before raw capture");var bytes=texture.GetRawTextureData();
                yield return new WaitForEndOfFrame();
                Debug.Log("STARTUP TEXTURE DIAGNOSTIC captured bytes="+bytes.Length);texture.Apply(false,true);
                yield return new WaitForEndOfFrame();
            }
            finally{UnityEngine.Object.Destroy(texture);Debug.Log("STARTUP TEXTURE DIAGNOSTIC END regenerate="+regenerate);}
        }
    }
}
