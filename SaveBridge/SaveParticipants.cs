using System;
using System.IO;

namespace HundredTiles.SaveBridge
{
    // Participants share the terrain/ownership/resource snapshot binding and
    // transaction. All decode and target checks precede the first live commit.
    public interface ISaveParticipant
    {
        string Key { get; }
        byte[] Capture(byte[] binding);
        void BeginLoad();
        object Prepare(byte[] bytes, byte[] binding);
        void ValidateTarget(object prepared);
        void Commit(object prepared);
        void ClearSave();
        void ClearLoad();
    }

    internal sealed class ObjectParticipant : ISaveParticipant
    {
        bool treesRead, propsRead;
        public string Key { get { return "HundredTiles.Objects.v1"; } }
        public byte[] Capture(byte[] binding) { return ObjectSaveBridge.Capture(binding); }
        public void BeginLoad() { treesRead = propsRead = false; }
        public void TreesRead()
        { if (treesRead) throw new InvalidOperationException("Trees read twice."); treesRead = true; }
        public void PropsRead()
        { if (propsRead) throw new InvalidOperationException("Props read twice."); propsRead = true; }
        public object Prepare(byte[] bytes, byte[] binding)
        {
            if (!treesRead || !propsRead) throw new InvalidOperationException("Object deserialization incomplete.");
            return ObjectSaveBridge.Prepare(bytes, binding);
        }
        public void ValidateTarget(object prepared) { ObjectSaveBridge.ValidateTarget((ObjectSaveBridge.Prepared)prepared); }
        public void Commit(object prepared) { ObjectSaveBridge.Commit((ObjectSaveBridge.Prepared)prepared); }
        public void ClearSave() { ObjectSaveBridge.ClearSave(); }
        public void ClearLoad() { treesRead = propsRead = false; }
    }

    public interface IMigratingSaveParticipant : ISaveParticipant
    {
        bool CanReadSize(int size);
    }
    public interface IChunkedSaveParticipant : ISaveParticipant
    {int EncodedSize {get;}}
    internal sealed class ExtensionBackup
    {
        internal HundredTiles.Core.TransportBackup Chunks;
        internal string Key;
        internal bool HadValue;
        internal byte[] Previous;
    }
}
