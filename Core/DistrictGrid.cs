using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HundredTiles.Core
{
    public static class DistrictGrid
    {
        public const int Side=1000,Origin=450,LegacySide=512,Offset=194,RecordSize=8;
        public const int Bytes=Side*Side*RecordSize,LegacyBytes=LegacySide*LegacySide*RecordSize;
        public const int EncodedSize=84+Bytes*2+32;
        public const float CellSize=19.2f;
        public static byte[] Project(byte[] source)
        {
            if(source==null || source.Length!=Bytes)throw new ArgumentException("Expanded district shape differs.");
            var result=new byte[LegacyBytes];
            for(int z=0;z<LegacySide;z++)Array.Copy(source,((z+Offset)*Side+Offset)*8,result,z*LegacySide*8,LegacySide*8);return result;
        }
        public static byte[] Expand(byte[] source)
        {
            if(source==null || source.Length!=LegacyBytes)throw new ArgumentException("Vanilla district shape differs.");
            var result=new byte[Bytes];
            for(int i=0;i<result.Length;i+=8){result[i+1]=1;result[i+2]=2;result[i+3]=3;result[i+4]=255;}
            for(int z=0;z<LegacySide;z++)Array.Copy(source,z*LegacySide*8,result,((z+Offset)*Side+Offset)*8,LegacySide*8);return result;
        }
        public static void Validate(byte[] cells,int expected)
        {
            if(cells==null || cells.Length!=expected)throw new InvalidDataException("District cells have the wrong length.");
            for(int i=0;i<cells.Length;i+=8)for(int n=0;n<4;n++)if(cells[i+n]>=128)throw new InvalidDataException("District identifier is out of bounds.");
        }
        static byte[] Fingerprint(byte[] districts,byte[] parks)
        {
            Validate(districts,LegacyBytes);Validate(parks,LegacyBytes);
            using(var hash=SHA256.Create()){hash.TransformBlock(districts,0,districts.Length,null,0);hash.TransformFinalBlock(parks,0,parks.Length);return hash.Hash;}
        }
        static bool Equal(byte[] a,byte[] b)
        {if(a==null || b==null || a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        public static byte[] Encode(byte[] districts,byte[] parks,byte[] binding)
        {
            Validate(districts,Bytes);Validate(parks,Bytes);if(binding==null || binding.Length!=32)throw new ArgumentException("District map binding missing.");
            var result=new byte[EncodedSize];
            using(var stream=new MemoryStream(result,true))using(var writer=new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("HTDIST01"));writer.Write(1);writer.Write(Side);writer.Write(Origin);
                writer.Write(Fingerprint(Project(districts),Project(parks)));writer.Write(binding);writer.Write(districts);writer.Write(parks);
                using(var hash=SHA256.Create())writer.Write(hash.ComputeHash(result,0,result.Length-32));
            }
            return result;
        }
        public static void Decode(byte[] bytes,byte[] legacyDistricts,byte[] legacyParks,byte[] binding,out byte[] districts,out byte[] parks)
        {
            if(bytes==null || bytes.Length!=EncodedSize)throw new InvalidDataException("District snapshot length differs.");
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(8))!="HTDIST01" || reader.ReadInt32()!=1 || reader.ReadInt32()!=Side || reader.ReadInt32()!=Origin ||
                    !Equal(reader.ReadBytes(32),Fingerprint(legacyDistricts,legacyParks)) || !Equal(reader.ReadBytes(32),binding))throw new InvalidDataException("District layout, baseline or map binding differs.");
                districts=reader.ReadBytes(Bytes);parks=reader.ReadBytes(Bytes);byte[] checksum=reader.ReadBytes(32);
                using(var hash=SHA256.Create())if(!Equal(checksum,hash.ComputeHash(bytes,0,bytes.Length-32)))throw new InvalidDataException("District checksum differs.");
                Validate(districts,Bytes);Validate(parks,Bytes);
            }
        }
    }
}
