using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HundredTiles.Core;
namespace HundredTiles.WorkshopCompatibility {
 // V1 stays byte-compatible for vanilla capacity; V2 declares expanded tree
 // capacity. Decode requires an exact loaded capacity and validates before commit.
 public static class ExpandedObjects {
  public const int MinimumTrees=262144,MaximumTrees=2097152,Props=65536;
  static readonly byte[] Magic=Encoding.ASCII.GetBytes("HTOBJ001");
  public static byte[] Encode(ObjectPositionRecord[] trees,ObjectPositionRecord[] props,byte[] binding){Validate(trees,props,binding);using(var stream=new MemoryStream(checked(116+7*(trees.Length+props.Length)))){var w=new BinaryWriter(stream);w.Write(Magic);w.Write(trees.Length==MinimumTrees?1:2);w.Write(trees.Length);w.Write(props.Length);w.Write(Fingerprint(ObjectPositionCodec.Project(trees),ObjectPositionCodec.Project(props)));w.Write(binding);Write(w,trees,false);Write(w,props,false);w.Flush();w.Write(Hash(stream.ToArray()));w.Flush();return stream.ToArray();}}
  public static void Decode(byte[] bytes,ObjectPositionRecord[] legacyTrees,ObjectPositionRecord[] legacyProps,byte[] binding,out ObjectPositionRecord[] trees,out ObjectPositionRecord[] props){
   Validate(legacyTrees,legacyProps,binding);
   if(bytes==null || bytes.Length<116 || bytes.Length>=16711680)throw new InvalidDataException("Object extension size differs.");
   var body=new byte[bytes.Length-32];Array.Copy(bytes,body,body.Length);var checksum=new byte[32];Array.Copy(bytes,body.Length,checksum,0,32);if(!Equal(Hash(body),checksum))throw new InvalidDataException("Object checksum differs.");
   using(var stream=new MemoryStream(body,false)){
    var r=new BinaryReader(stream);if(!Equal(r.ReadBytes(8),Magic))throw new InvalidDataException("Object magic differs.");int version=r.ReadInt32(),tc=r.ReadInt32(),pc=r.ReadInt32();
    if((version!=1 && version!=2) || (version==1 && tc!=MinimumTrees) || tc<MinimumTrees || tc>MaximumTrees || tc>legacyTrees.Length || pc!=Props || bytes.Length!=116+7*(tc+pc))throw new InvalidDataException("Object capacity header differs; shrinking is not supported.");
    // An older save may be opened with a larger configured tree buffer. New
    // slots must be empty before migration; never discard live extra objects.
    for(int i=tc;i<legacyTrees.Length;i++)if(legacyTrees[i].Flags!=0 || legacyTrees[i].X.PositiveExtension || legacyTrees[i].Z.PositiveExtension)throw new InvalidDataException("New tree slots are not empty.");
    var legacySaved=new ObjectPositionRecord[tc];Array.Copy(legacyTrees,legacySaved,tc);
    var projection=r.ReadBytes(32);if(!Equal(projection,Fingerprint(ObjectPositionCodec.Project(legacySaved),ObjectPositionCodec.Project(legacyProps))) || !Equal(r.ReadBytes(32),binding))throw new InvalidDataException("Object snapshot binding differs.");
    var saved=Read(r,tc);var p=Read(r,pc);Validate(saved,p,binding);if(!Equal(projection,Fingerprint(ObjectPositionCodec.Project(saved),ObjectPositionCodec.Project(p))))throw new InvalidDataException("Object projection differs.");
    var t=new ObjectPositionRecord[legacyTrees.Length];Array.Copy(saved,t,saved.Length);Validate(t,p,binding);trees=t;props=p;
   }
  }
  static void Validate(ObjectPositionRecord[] trees,ObjectPositionRecord[] props,byte[] binding){if(trees==null || trees.Length<MinimumTrees || trees.Length>MaximumTrees || props==null || props.Length!=Props || binding==null || binding.Length!=32)throw new InvalidDataException("Object buffer capacity differs.");foreach(var list in new[]{trees,props})for(int i=0;i<list.Length;i++){var item=list[i];PackedObjectPosition.Decode(item.X);PackedObjectPosition.Decode(item.Z);if((i==0 || item.Flags==0) && (item.X.PositiveExtension || item.Z.PositiveExtension))throw new InvalidDataException("Unused object extension marker.");}}
  static void Write(BinaryWriter w,ObjectPositionRecord[] list,bool normalizeFire){foreach(var item in list){w.Write(item.X.Value);w.Write(item.Z.Value);w.Write(normalizeFire?(ushort)(item.Flags & ~0xC0):item.Flags);w.Write((byte)((item.X.PositiveExtension?1:0)|(item.Z.PositiveExtension?2:0)));}}
  static ObjectPositionRecord[] Read(BinaryReader r,int count){var list=new ObjectPositionRecord[count];for(int i=0;i<count;i++){short x=r.ReadInt16(),z=r.ReadInt16();ushort flags=r.ReadUInt16();byte markers=r.ReadByte();if(markers>3)throw new InvalidDataException("Unknown object markers.");list[i]=new ObjectPositionRecord{Flags=flags,X=new PackedObjectAxis{Value=x,PositiveExtension=(markers&1)!=0},Z=new PackedObjectAxis{Value=z,PositiveExtension=(markers&2)!=0}};}return list;}
  static byte[] Fingerprint(ObjectPositionRecord[] trees,ObjectPositionRecord[] props){using(var s=new MemoryStream()){var w=new BinaryWriter(s);Write(w,trees,true);Write(w,props,false);w.Flush();return Hash(s.ToArray());}}
  static byte[] Hash(byte[] bytes){using(var h=SHA256.Create())return h.ComputeHash(bytes);}
  static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;int v=0;for(int i=0;i<a.Length;i++)v|=a[i]^b[i];return v==0;}
 }
}
