using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HundredTiles.Core;

static class DistrictMigrationChecks
{
    public static void Run()
    {
        var legacy=new byte[DistrictGrid.LegacyBytes];
        var source=new byte[1000*1000*8];
        for(int z=0;z<1000;z++)for(int x=0;x<1000;x++)
        {
            int k=(z*1000+x)*8;
            // Keep the vanilla projection empty; populate every retained outer cell.
            if(x<194 || x>=706 || z<194 || z>=706){source[k]=(byte)(1+(x+z)%127);source[k+4]=(byte)(x%256);}
        }
        var binding=new byte[32];binding[0]=79;
        var encoded=new byte[DistrictGrid.EncodedSizeFor(1000)];
        using(var stream=new MemoryStream(encoded,true))using(var writer=new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("HTDIST01"));writer.Write(1);writer.Write(1000);writer.Write(450);
            using(var hash=SHA256.Create()){hash.TransformBlock(legacy,0,legacy.Length,null,0);hash.TransformFinalBlock(legacy,0,legacy.Length);writer.Write(hash.Hash);}
            writer.Write(binding);writer.Write(source);writer.Write(source);
            using(var hash=SHA256.Create())writer.Write(hash.ComputeHash(encoded,0,encoded.Length-32));
        }
        byte[] districts,parks;
        var storage=new System.Collections.Generic.Dictionary<string,byte[]>();storage["district"]=encoded;
        Require(SaveTransport.ReadKnownLayouts(storage,"district",DistrictGrid.EncodedSize,DistrictGrid.CanReadSize)==encoded,"Legacy unchunked transport");
        SaveTransport.Publish(storage,"district",encoded,encoded.Length);
        Require(SaveTransport.ReadKnownLayouts(storage,"district",DistrictGrid.EncodedSize,DistrictGrid.CanReadSize).Length==encoded.Length,"Legacy chunk transport");
        storage[SaveTransport.ChunkKey("district",0)][0]^=1;
        Reject(()=>SaveTransport.ReadKnownLayouts(storage,"district",DistrictGrid.EncodedSize,DistrictGrid.CanReadSize));
        DistrictGrid.Decode(encoded,legacy,legacy,binding,out districts,out parks);
        for(int z=0;z<1000;z++)for(int x=0;x<1000;x++)for(int n=0;n<8;n++)
            Require(districts[(z*DistrictGrid.Side+x)*8+n]==source[(z*1000+x)*8+n] && parks[(z*DistrictGrid.Side+x)*8+n]==source[(z*1000+x)*8+n],"Retained district and park cells");
#if HUNDREDTILES_324
        {
            int k=(1799*1800+1799)*8;
            Require(districts[k]==0 && districts[k+1]==1 && districts[k+2]==2 && districts[k+3]==3 && districts[k+4]==255,"New cells use native defaults");
        }
#endif
        var roundtrip=DistrictGrid.Encode(districts,parks,binding);
        DistrictGrid.Decode(roundtrip,legacy,legacy,binding,out districts,out parks);
        encoded[100]^=1;Reject(()=>DistrictGrid.Decode(encoded,legacy,legacy,binding,out districts,out parks));encoded[100]^=1;
        binding[0]^=1;Reject(()=>DistrictGrid.Decode(encoded,legacy,legacy,binding,out districts,out parks));
        Require(!DistrictGrid.CanReadSize(encoded.Length-1),"Truncation rejected");
        Console.WriteLine("PASS: every 1000x1000 district/park cell preserved, new cells initialized, round trip, corruption and mismatched binding rejected.");
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Reject(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Invalid district snapshot accepted");}
}
