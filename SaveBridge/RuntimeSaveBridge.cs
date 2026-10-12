using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using ColossalFramework;
using HundredTiles.Core;

namespace HundredTiles.SaveBridge
{
    // No automatic installer or IUserMod. These hooks are for the still-gated
    // experimental patch set and must be installed together with all dependencies.
    public static class RuntimeSaveBridge
    {
        public const string DataKey = "HundredTiles.TerrainWater.v1";
        public const string OwnershipKey = "HundredTiles.Ownership.v1";
        public const string ResourceKey = "HundredTiles.Resources.v1";
        public const string ObjectKey = "HundredTiles.Objects.v1";
        static readonly ObjectParticipant objects = new ObjectParticipant();
        static readonly List<ISaveParticipant> participants = new List<ISaveParticipant> { objects, new DistrictSaveBridge.Participant() };
        static bool restored;
        public static bool NeedsLandscape;
        static OwnershipState saveOwnership,saveLegacyOwnership,readOwnership;
        static int[] readArea;
        static LoadSession loading;
        static WaterSimulation.Cell[][] readWater;
        static MapSaveState saving;
        static MapSaveState saveLegacy;
        static WaterSimulation.Cell[] saveWater;
        static ushort[] zeroTargets;

        public sealed class SaveScope
        {
            internal TransportBackup Backup;
            internal byte[] PreviousOwnership;
            internal bool HadOwnership,OwnershipPublished;
            internal byte[] PreviousResource;
            internal bool HadResource,ResourcePublished;
            internal readonly List<ExtensionBackup> Additional = new List<ExtensionBackup>();
        }

        public static void RegisterParticipant(ISaveParticipant participant)
        {
            if (saving != null || loading != null) throw new InvalidOperationException("Cannot register during a save/load.");
            if (participant == null || string.IsNullOrEmpty(participant.Key) || participant.Key.IndexOf(".chunk",StringComparison.Ordinal)>=0 || participant.Key.StartsWith(DataKey, StringComparison.Ordinal) ||
                participant.Key == OwnershipKey || participant.Key == ResourceKey)
                throw new ArgumentException("Invalid additional save key.");
            foreach (var current in participants) if (current.Key == participant.Key) throw new ArgumentException("Duplicate additional save key.");
            var chunked=participant as IChunkedSaveParticipant;
            if(chunked!=null && chunked.EncodedSize<=0)throw new ArgumentException("Invalid chunked save size.");
            participants.Add(participant);
        }
        static byte[] Binding(byte[] resourceBytes)
        { if(resourceBytes==null)return null; using(var sha=SHA256.Create())return sha.ComputeHash(resourceBytes); }

        // Capture from the full DataSerializer.Serialize prefix, before
        // the game starts writing any manager or serializable extension data.
        public static void BeginSave(out SaveScope __state)
        {
            if (saving != null || loading != null) throw new InvalidOperationException("Nested save or save during load.");
            __state = new SaveScope();
            try
            {
                var terrain=Singleton<TerrainManager>.instance;
                var water=terrain.WaterSimulation;
                object sync=Get(water,"m_simulationBufferLock");
                var timer=Stopwatch.StartNew();
                lock(sync)
                {
                    if ((WaterSimulation.Status)Get(water,"m_simulationStatus") != WaterSimulation.Status.Running)
                        throw new InvalidOperationException("Water is not running; save capture refused.");
                    uint frame=Singleton<SimulationManager>.instance.m_currentFrameIndex;
                    Set(water,"m_simulationFrameIndex",frame);
                    Monitor.PulseAll(sync);
                    while (frame > (uint)Get(water,"m_waterFrameIndex"))
                    {
                        if (timer.ElapsedMilliseconds > 10000 || (WaterSimulation.Status)Get(water,"m_simulationStatus") == WaterSimulation.Status.Terminated)
                            throw new InvalidOperationException("Water capture did not reach the save frame.");
                        Monitor.Wait(sync,1);
                    }
                }
                var cells=water.BeginRead();
                try
                {
                    int size=ExpandedMap.HeightStride * ExpandedMap.HeightStride;
                    if (cells.Length != size || terrain.RawHeights.Length != size || terrain.BlockHeights.Length != size || terrain.BlockHeightTargets.Length != size)
                        throw new InvalidDataException("Save bridge requires expanded terrain and water.");
                    saving=new MapSaveState { Heights=(ushort[])terrain.RawHeights.Clone(),BlockOffsets=new ushort[size],Water=new WaterCellState[size],WaterFrame=(uint)Get(water,"m_waterFrameIndex") };
                    for(int i=0;i<size;i++)
                    {
                        saving.BlockOffsets[i]=unchecked((ushort)(terrain.BlockHeights[i]-terrain.BlockHeightTargets[i]));
                        saving.Water[i]=ToState(cells[i]);
                    }
                }
                finally { water.EndRead(); }
                saveLegacy=LegacyProjection.FromExpanded(saving);
                saveWater=ToGame(saveLegacy.Water);
                zeroTargets=new ushort[saveLegacy.Heights.Length];
                byte[] bytes=SaveCodec.Encode(saving);
                var areas=Singleton<GameAreaManager>.instance;
                saveOwnership=new OwnershipState {Grid=(int[])areas.m_areaGrid.Clone(),Count=areas.m_areaCount,Start=(int)Get(areas,"m_startTile")};
                saveLegacyOwnership=Ownership.Project(saveOwnership);
                byte[] ownershipBytes=Ownership.Encode(saveOwnership,bytes);
                byte[] resourceBytes=ResourceBridge.Capture(bytes,ownershipBytes);
                byte[] binding=Binding(resourceBytes);
                var additional=new List<byte[]>();
                foreach(var participant in participants)
                {
                    byte[] captured=participant.Capture(binding);
                    var chunked=participant as IChunkedSaveParticipant;
                    if(captured==null || (chunked==null?captured.Length>=SaveTransport.MaximumEntrySize:captured.Length!=chunked.EncodedSize))throw new InvalidDataException("Additional save entry length differs or exceeds the game limit.");
                    additional.Add(captured);
                }
                var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;
                lock(storage)
                {
                    __state.Backup=SaveTransport.Publish(storage,DataKey,bytes);
                    __state.HadOwnership=storage.TryGetValue(OwnershipKey,out __state.PreviousOwnership);
                    __state.OwnershipPublished=true;
                    storage[OwnershipKey]=ownershipBytes;
                    __state.HadResource=storage.TryGetValue(ResourceKey,out __state.PreviousResource);
                    __state.ResourcePublished=true;storage[ResourceKey]=resourceBytes;
                    for(int i=0;i<participants.Count;i++)
                    {
                        var backup=new ExtensionBackup {Key=participants[i].Key};
                        backup.HadValue=storage.TryGetValue(backup.Key,out backup.Previous);
                        __state.Additional.Add(backup);
                        var chunked=participants[i] as IChunkedSaveParticipant;
                        if(chunked!=null)backup.Chunks=SaveTransport.Publish(storage,backup.Key,additional[i],chunked.EncodedSize);
                        else storage[backup.Key]=additional[i];
                    }
                }
            }
            catch(Exception failure) { EndSave(failure,__state); throw; }
        }

        // DataSerializer queues child containers and writes them after the root
        // container's Serialize returns. The snapshot must cover that full loop.
        public static void BeginSerialize(object __3,out SaveScope __state)
        {__state=null;if(__3 is SimulationManager.Data)BeginSave(out __state);}
        public static Exception EndSerialize(Exception __exception,SaveScope __state)
        {return __state==null?__exception:EndSave(__exception,__state);}

        // Finalizer runs on both success and failure. A failed serialization
        // restores the previous in-memory extension entry rather than leaving
        // a new extension paired with an older standard save.
        public static Exception EndSave(Exception __exception,SaveScope __state)
        {
            if (__exception != null && __state != null && __state.Backup != null)
            {
                var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;
                lock(storage)
                {
                    __state.Backup.Restore(storage);
                    if(__state.OwnershipPublished)
                    {
                        if(__state.HadOwnership) storage[OwnershipKey]=__state.PreviousOwnership;
                        else storage.Remove(OwnershipKey);
                    }
                    if(__state.ResourcePublished)
                    {if(__state.HadResource)storage[ResourceKey]=__state.PreviousResource;else storage.Remove(ResourceKey);}
                    foreach(var backup in __state.Additional)
                    {if(backup.Chunks!=null)backup.Chunks.Restore(storage);if(backup.HadValue)storage[backup.Key]=backup.Previous;else storage.Remove(backup.Key);}
                }
            }
            ClearSave();
            return __exception;
        }
        static void ClearSave() { saving=null; saveLegacy=null; saveWater=null; zeroTargets=null; saveOwnership=null; saveLegacyOwnership=null; ResourceBridge.ClearSave(); foreach(var participant in participants)participant.ClearSave(); }

        public static ushort[] WriteHeights(ushort[] original) { RequireSave(); return saveLegacy.Heights; }
        public static ushort[] WriteBlocks(ushort[] original) { RequireSave(); return saveLegacy.BlockOffsets; }
        public static ushort[] WriteTargets(ushort[] original) { RequireSave(); return zeroTargets; }
        public static WaterSimulation.Cell[] WriteWater(WaterSimulation.Cell[] original) { RequireSave(); return saveWater; }
        public static uint WriteWaterFrame(uint original) { RequireSave(); return saving.WaterFrame; }
        public static int[] WriteAreas(int[] original) { RequireSave(); return saveLegacyOwnership.Grid; }
        public static int WriteAreaCount(int original) { RequireSave(); return saveLegacyOwnership.Count; }
        public static int WriteStartTile(int original) { RequireSave(); return saveLegacyOwnership.Start; }
        public static NaturalResourceManager.AreaCell[] WriteResourceTiles(NaturalResourceManager.AreaCell[] original) { RequireSave(); return ResourceBridge.WriteCache(original); }
        public static NaturalResourceManager.ResourceCell[] WriteResourceCells(NaturalResourceManager.ResourceCell[] original) { RequireSave(); return ResourceBridge.WriteCells(original); }
        public static int LegacyResourceOrigin(int resolution) {return ResourceGrid.LegacyOrigin(resolution);}
        static void RequireSave() { if(saving==null) throw new InvalidOperationException("Save snapshot not prepared."); }

        // SimulationManager.Data.Deserialize prefix. Legacy readers receive
        // scratch arrays; live expanded fields and water height pointers stay put.
        public static void BeginLoad()
        {
            if (saving != null || loading != null) throw new InvalidOperationException("Overlapping save/load.");
            loading=new LoadSession();
            restored=false;
            foreach(var participant in participants)participant.BeginLoad();
            readArea=new int[25]; readOwnership=null;
            ResourceBridge.BeginLoad();
            int count=LegacyProjection.Stride * LegacyProjection.Stride;
            readWater=new[] { new WaterSimulation.Cell[count],new WaterSimulation.Cell[count] };
        }
        public static Exception LoadFailure(Exception __exception)
        {
            if (__exception != null) Reset();
            return __exception;
        }
        static void RequireLoad() { if(loading==null) throw new InvalidOperationException("Load session missing."); }
        public static void TreesRead() { RequireLoad(); objects.TreesRead(); }
        public static void PropsRead() { RequireLoad(); objects.PropsRead(); }
        public static ushort[] ReadHeights(ushort[] original) { RequireLoad(); return loading.Heights; }
        public static ushort[] ReadBlocks(ushort[] original) { RequireLoad(); return loading.BlockOffsets; }
        public static WaterSimulation.Cell[][] ReadWater(WaterSimulation.Cell[][] original) { RequireLoad(); return readWater; }
        public static int[] ReadAreas(int[] original) { RequireLoad(); return readArea; }
        public static NaturalResourceManager.AreaCell[] ReadResourceTiles(NaturalResourceManager.AreaCell[] original) { RequireLoad(); return ResourceBridge.ReadCache(original); }
        public static NaturalResourceManager.ResourceCell[] ReadResourceCells(NaturalResourceManager.ResourceCell[] original) { RequireLoad(); return ResourceBridge.ReadCells(original); }
        public static void ResourceTilesRead()
        {
            RequireLoad();
            ResourceBridge.ReadComplete();
        }
        public static void AreasRead()
        {
            RequireLoad();
            if(readOwnership!=null) throw new InvalidOperationException("Ownership read twice.");
            var areas=Singleton<GameAreaManager>.instance;
            var legacy=new OwnershipState {Grid=(int[])readArea.Clone(),Count=areas.m_areaCount,Start=(int)Get(areas,"m_startTile")};
            var expanded=Ownership.Expand(legacy);
            if(areas.m_areaGrid.Length!=100) throw new InvalidDataException("Ownership target is not expanded.");
            Array.Copy(expanded.Grid,areas.m_areaGrid,100);
            Set(areas,"m_startTile",expanded.Start); areas.m_maxAreaCount=100;
            readOwnership=legacy;
        }
        public static void TerrainRead()
        {
            RequireLoad();
            // Other managers may query terrain during their Deserialize phase.
            // Publish the migrated vanilla baseline immediately; the validated
            // extension is overlaid once all standard data has finished loading.
            var terrain=Singleton<TerrainManager>.instance;
            var heights=ExpandedMap.ExtendHeights(loading.Heights);
            var blocks=WaterGrid.ExtendBuffer(loading.BlockOffsets);
            if(terrain.RawHeights.Length!=heights.Length || terrain.BlockHeights.Length!=blocks.Length)
                throw new InvalidDataException("Legacy terrain target is not expanded.");
            Array.Copy(heights,terrain.RawHeights,heights.Length);
            Array.Copy(blocks,terrain.BlockHeights,blocks.Length);
            loading.TerrainCompleted();
        }
        public static void WaterRead()
        {
            RequireLoad();
            var target=loading.Water;
            for(int i=0;i<target.Length;i++) target[i]=ToState(readWater[0][i]);
            var water=Singleton<TerrainManager>.instance.WaterSimulation;
            lock(Get(water,"m_simulationBufferLock"))
            {
                if((WaterSimulation.Status)Get(water,"m_simulationStatus")!=WaterSimulation.Status.ReadyForLoad)
                    throw new InvalidOperationException("Legacy water publish requires stopped worker.");
                var expanded=WaterGrid.ExtendBuffer(readWater[0]);
                var buffers=(WaterSimulation.Cell[][])Get(water,"m_waterBuffers");
                if(buffers.Length!=2 || buffers[0].Length!=expanded.Length || buffers[1].Length!=expanded.Length)
                    throw new InvalidDataException("Legacy water target is not expanded.");
                Array.Copy(expanded,buffers[0],expanded.Length); Array.Copy(expanded,buffers[1],expanded.Length);
                loading.WaterCompleted((uint)Get(water,"m_waterFrameIndex"));
            }
        }

        // Prefix before *any* Managers_UpdateData call, not OnLoadData, which
        // happens after TerrainManager.LateUpdateData has enabled water flow.
        public static void BeforeManagersUpdate()
        {
            if(restored)return;
            RequireLoad();
            var terrain=Singleton<TerrainManager>.instance;
            var water=terrain.WaterSimulation;
            object sync=Get(water,"m_simulationBufferLock");
            lock(sync)
            {
                if ((WaterSimulation.Status)Get(water,"m_simulationStatus") != WaterSimulation.Status.ReadyForLoad || (int)Get(water,"m_readLockCount") != 0)
                    throw new InvalidOperationException("Restore requires a stopped water worker and drained readers.");
                byte[] bytes,ownershipBytes,resourceBytes; bool hasOwnership,hasResource;
                var storage=Singleton<SimulationManager>.instance.m_serializableDataStorage;
                var additionalBytes=new List<byte[]>();
                lock(storage)
                {
                    bytes=SaveTransport.Read(storage,DataKey); hasOwnership=storage.TryGetValue(OwnershipKey,out ownershipBytes);hasResource=storage.TryGetValue(ResourceKey,out resourceBytes);
                    foreach(var participant in participants)
                    {
                        byte[] value;bool present=storage.TryGetValue(participant.Key,out value);
                        var chunked=participant as IChunkedSaveParticipant;
                        if(chunked!=null)value=SaveTransport.Read(storage,participant.Key,chunked.EncodedSize);
                        if((bytes!=null)!=present || (present && value==null))throw new InvalidDataException("Additional extension set is incomplete: "+participant.Key);
                        additionalBytes.Add(value);
                    }
                }
                if(readOwnership==null) throw new InvalidOperationException("Standard ownership has not finished loading.");
                if((bytes!=null)!=hasOwnership || (hasOwnership && ownershipBytes==null)) throw new InvalidDataException("Terrain and ownership extension pair is incomplete.");
                if((bytes!=null)!=hasResource || (hasResource && resourceBytes==null))throw new InvalidDataException("Resource extension set is incomplete.");
                var ownership=bytes==null ? Ownership.Expand(readOwnership) : Ownership.Decode(ownershipBytes,readOwnership,bytes);
                var resourceState=ResourceBridge.Prepare(resourceBytes,bytes,ownershipBytes);
                var additionalStates=new List<object>();byte[] binding=Binding(resourceBytes);
                for(int i=0;i<participants.Count;i++)
                {
                    object prepared=participants[i].Prepare(additionalBytes[i],binding);
                    participants[i].ValidateTarget(prepared);additionalStates.Add(prepared);
                }
                var areas=Singleton<GameAreaManager>.instance;
                if(areas.m_areaGrid.Length!=100) throw new InvalidDataException("Ownership restore target is not expanded.");
                // Decode/fingerprint check occurs before any live array is changed.
                MapSaveState state=loading.Resolve(bytes);
                NeedsLandscape=bytes==null;
                // For vanilla saves retain the current frame here. The child's
                // AfterDeserialize subsequently supplies the frame for <173 saves.
                if(bytes==null) state.WaterFrame=(uint)Get(water,"m_waterFrameIndex");
                var buffers=(WaterSimulation.Cell[][])Get(water,"m_waterBuffers");
                int count=state.Heights.Length;
                if (terrain.RawHeights.Length != count || terrain.BlockHeights.Length != count || terrain.BlockHeightTargets.Length != count || buffers.Length != 2 || buffers[0].Length != count || buffers[1].Length != count)
                    throw new InvalidDataException("Restore target layout is not expanded.");
                var masks=new ulong[64];
                var cells=ToGame(state.Water);
                for(int z=0;z<ExpandedMap.HeightStride;z++)
                for(int x=0;x<ExpandedMap.HeightStride;x++)
                    if(state.Water[z*ExpandedMap.HeightStride+x].Height != 0) masks[WaterGrid.GetMaskCoordinate(z)] |= WaterGrid.GetMaskBit(x);
                string[] maskNames={"m_waterExists1","m_waterExists2","m_waterExists3"};
                foreach(string name in maskNames) if(((ulong[])Get(water,name)).Length != 64) throw new InvalidDataException("Unexpected water mask shape.");
                Array.Copy(state.Heights,terrain.RawHeights,count);
                Array.Copy(state.BlockOffsets,terrain.BlockHeights,count);
                Array.Clear(terrain.BlockHeightTargets,0,count);
                Array.Copy(cells,buffers[0],count); Array.Copy(cells,buffers[1],count);
                foreach(string name in maskNames) Array.Copy(masks,(ulong[])Get(water,name),64);
                Set(water,"m_bitCells",WaterGrid.CellsPerBit);
                Set(water,"m_waterFrameIndex",state.WaterFrame); Set(water,"m_waterFrameIndex2",state.WaterFrame);
                Array.Copy(ownership.Grid,areas.m_areaGrid,100); areas.m_areaCount=ownership.Count;
                areas.m_maxAreaCount=100; Set(areas,"m_startTile",ownership.Start); Set(areas,"m_areasUpdated",true);
                ResourceBridge.Commit(resourceState);
                for(int i=0;i<participants.Count;i++)participants[i].Commit(additionalStates[i]);
                typeof(TerrainManager).GetMethod("RefreshPatchFlatness",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(terrain,null);
                // Terrain.UpdateData rebuilds derived heights and LOD data next.
                loading=null; readWater=null; readArea=null; readOwnership=null; ResourceBridge.ClearLoad();
                foreach(var participant in participants)participant.ClearLoad();restored=true;
            }
        }

        // DataSerializer completes every Deserialize before invoking the root's
        // AfterDeserialize, then invokes children. Restore before those children
        // bind prefabs, count park objects or rebuild derived manager state.
        public static void BeforeAfterDeserialize() { BeforeManagersUpdate(); }

        static WaterCellState ToState(WaterSimulation.Cell c) { return new WaterCellState { Height=c.m_height,Pollution=c.m_pollution,VelocityX=c.m_velocityX,VelocityZ=c.m_velocityZ }; }
        public static void Reset() { loading=null; readWater=null; readArea=null; readOwnership=null; restored=false; NeedsLandscape=false; ResourceBridge.ClearLoad(); foreach(var participant in participants)participant.ClearLoad(); ClearSave(); }
        static WaterSimulation.Cell[] ToGame(WaterCellState[] cells)
        {
            var result=new WaterSimulation.Cell[cells.Length];
            for(int i=0;i<result.Length;i++) result[i]=new WaterSimulation.Cell { m_height=cells[i].Height,m_pollution=cells[i].Pollution,m_velocityX=cells[i].VelocityX,m_velocityZ=cells[i].VelocityZ };
            return result;
        }
        static object Get(object owner,string name) { return Field(owner,name).GetValue(owner); }
        static void Set(object owner,string name,object value) { Field(owner,name).SetValue(owner,value); }
        static FieldInfo Field(object owner,string name)
        {
            var field=owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            if(field==null) throw new MissingFieldException(owner.GetType().FullName,name);
            return field;
        }
    }
}
