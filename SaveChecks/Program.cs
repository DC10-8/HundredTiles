using System.Security.Cryptography;
using HundredTiles.Core;

int size=ExpandedMap.HeightStride * ExpandedMap.HeightStride;
var state=new MapSaveState { Heights=new ushort[size],BlockOffsets=new ushort[size],Water=new WaterCellState[size],WaterFrame=128 };
for(int i=0;i<size;i++)
{
    state.Heights[i]=(ushort)(i%65536);
    state.BlockOffsets[i]=(ushort)(i%73);
    state.Water[i]=new WaterCellState { Height=(ushort)(i%7==0 ? 0 : 200),Pollution=(ushort)(i%131),VelocityX=(short)(i%32000),VelocityZ=(short)-(i%32000) };
}
byte[] encoded=SaveCodec.Encode(state);
Check(encoded.Length==SaveCodec.EncodedSize,"Fixed encoded size");
MapSaveState decoded=SaveCodec.Decode(encoded,out byte[] legacyDigest);
Check(decoded.WaterFrame==state.WaterFrame,"Frame retained");
Compare(state,decoded,"Full state round trip");
MapSaveState legacy=LegacyProjection.FromExpanded(state);
SaveCodec.RequireSame(SaveCodec.Digest(legacy),legacyDigest);
var session=Session(legacy);
MapSaveState restored=session.Resolve(encoded);
Compare(state,restored,"Extension reattachment");
Reject<InvalidOperationException>(()=>session.Resolve(encoded),"Double load refused");
Reject<InvalidOperationException>(()=>new LoadSession().Resolve(encoded),"Early restore refused");
var legacySession=Session(legacy);
var expanded=legacySession.Resolve(null);
for(int z=0;z<ExpandedMap.HeightStride;z++)
for(int x=0;x<ExpandedMap.HeightStride;x++)
{
    int i=z*ExpandedMap.HeightStride+x;
    int old=Math.Min(z,1080)*1081+Math.Min(x,1080);
    Check(expanded.Heights[i]==legacy.Heights[old],"Legacy height edge extension");
    if(x>1080 || z>1080)
    {
        Check(expanded.BlockOffsets[i]==0,"New land has no duplicated block offsets");
        Check(expanded.Water[i].Height==0 && expanded.Water[i].Pollution==0 && expanded.Water[i].VelocityX==0 && expanded.Water[i].VelocityZ==0,"New water empty");
    }
    else Check(expanded.BlockOffsets[i]==legacy.BlockOffsets[z*1081+x],"Legacy block offsets retained");
}
var wrongCity=Session(legacy);
wrongCity.Heights[1]^=1;
Reject<InvalidDataException>(()=>wrongCity.Resolve(encoded),"Mismatched legacy content rejected");
var wrongFrame=Session(legacy,129);
Reject<InvalidDataException>(()=>wrongFrame.Resolve(encoded),"Mismatched water frame rejected");
byte[] corrupt=(byte[])encoded.Clone(); corrupt[SaveCodec.HeaderSize+15]^=1;
Reject<InvalidDataException>(()=>SaveCodec.Decode(corrupt,out _),"Checksum corruption rejected");
Reject<InvalidDataException>(()=>SaveCodec.Decode(encoded.Take(encoded.Length-1).ToArray(),out _),"Truncation rejected");
Reject<InvalidDataException>(()=>SaveCodec.Decode(encoded.Concat(new byte[1]).ToArray(),out _),"Trailing bytes rejected");
foreach(int offset in new[] {0,8,12,16,20,28})
{
    var bad=(byte[])encoded.Clone(); bad[offset]^=1; Rehash(bad);
    Reject<InvalidDataException>(()=>SaveCodec.Decode(bad,out _),"Malformed header rejected before allocation");
}
// Simulate partial/cross-city extension with a valid outer checksum but invalid
// inner baseline fingerprint: corruption must still be detected.
var badLink=(byte[])encoded.Clone(); badLink[32]^=1; Rehash(badLink);
Reject<InvalidDataException>(()=>SaveCodec.Decode(badLink,out _),"Internal baseline linkage rejected");
// Boundary velocities: a dry edge cell must not retain velocity into a new tile
// in the vanilla projection, even when that tile contains water in the extension.
state.Water[1080]=new WaterCellState { VelocityX=123,VelocityZ=-321,Pollution=88 };
state.Water[1081]=new WaterCellState { Height=200 };
state.Water[1080+1201]=new WaterCellState { Height=200 };
var boundary=LegacyProjection.FromExpanded(state);
Check(boundary.Water[1080].VelocityX==0 && boundary.Water[1080].VelocityZ==-321 && boundary.Water[1080].Pollution==0,"Legacy boundary canonicalization");
var duplicate=new LoadSession(); duplicate.TerrainCompleted();
Reject<InvalidOperationException>(()=>duplicate.TerrainCompleted(),"Duplicate terrain stage rejected");
Console.WriteLine("PASS: all 1,442,401 cells round-trip; legacy migration; load ordering; checksum/layout/version; mismatched save/frame; water boundary semantics; duplicate stages.");
var storage=new Dictionary<string,byte[]>();
const string key="HundredTiles.Test";
Check(SaveTransport.Read(storage,key)==null,"No extension is a legacy save");
var backup=SaveTransport.Publish(storage,key,encoded);
Check(storage.Values.All(b=>b.Length<16711680),"Every chunk fits the actual game serializer limit");
Check(SaveTransport.Read(storage,key).SequenceEqual(encoded),"Chunk transport round trip");
byte[] missing=storage[SaveTransport.ChunkKey(key,1)];storage.Remove(SaveTransport.ChunkKey(key,1));
Reject<InvalidDataException>(()=>SaveTransport.Read(storage,key),"Missing chunk rejected");
storage[SaveTransport.ChunkKey(key,1)]=missing;
var originalChunk=storage[SaveTransport.ChunkKey(key,2)];
var changed=(byte[])originalChunk.Clone();changed[0]^=1;storage[SaveTransport.ChunkKey(key,2)]=changed;
Reject<InvalidDataException>(()=>SaveTransport.Read(storage,key),"Mixed/corrupt chunk rejected");
storage[SaveTransport.ChunkKey(key,2)]=originalChunk;
var manifest=storage[key];storage.Remove(key);
Reject<InvalidDataException>(()=>SaveTransport.Read(storage,key),"Orphan chunks refused");
storage[key]=manifest;
backup.Restore(storage);
Check(storage.Count==0,"Failed first save removes manifest and chunks");
Console.WriteLine("PASS: game-size-safe chunk transport; missing/mixed/orphan chunks; transport rollback.");

static LoadSession Session(MapSaveState legacy,uint? frame=null)
{
    var session=new LoadSession();
    Array.Copy(legacy.Heights,session.Heights,legacy.Heights.Length);
    Array.Copy(legacy.BlockOffsets,session.BlockOffsets,legacy.BlockOffsets.Length);
    Array.Copy(legacy.Water,session.Water,legacy.Water.Length);
    session.TerrainCompleted(); session.WaterCompleted(frame??legacy.WaterFrame);
    return session;
}
static void Rehash(byte[] bytes) { var hash=SHA256.HashData(bytes.AsSpan(0,bytes.Length-32)); Array.Copy(hash,0,bytes,bytes.Length-32,32); }
static void Compare(MapSaveState first,MapSaveState second,string name)
{
    Check(first.Heights.SequenceEqual(second.Heights),name+" heights");
    Check(first.BlockOffsets.SequenceEqual(second.BlockOffsets),name+" blocks");
    Check(first.Water.SequenceEqual(second.Water),name+" water");
}
static void Check(bool condition,string name) { if(!condition) throw new Exception(name); }
static void Reject<T>(Action action,string name) where T:Exception { try { action(); } catch(T) { return; } throw new Exception(name); }
