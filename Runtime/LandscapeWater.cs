using System;
using System.Reflection;
using System.Threading;

namespace HundredTiles.Runtime
{
    // Use the native load handoff to stop the water worker between passes.
    // Both buffers must be seeded; editing only BeginRead's buffer loses water
    // at the next swap and can race an in-progress native simulation pass.
    internal sealed class LandscapeWater : IDisposable
    {
        readonly WaterSimulation water;
        readonly object gate;
        readonly FieldInfo status;
        readonly object previous;
        public readonly WaterSimulation.Cell[][] Buffers;
        public readonly ushort[] Depth;
        public LandscapeWater(WaterSimulation water)
        {
            this.water=water;
            gate=Field("m_simulationBufferLock").GetValue(water);
            status=Field("m_simulationStatus");
            Monitor.Enter(gate);
            previous=status.GetValue(water);
            try
            {
                if(previous.ToString()=="Terminated")throw new InvalidOperationException("Water worker terminated.");
                if(previous.ToString()!="ReadyForLoad")
                {
                    status.SetValue(water,Enum.Parse(status.FieldType,"WaitingForLoad"));
                    Monitor.PulseAll(gate);
                }
                int deadline=Environment.TickCount+15000;
                while(status.GetValue(water).ToString()!="ReadyForLoad" || (int)Field("m_readLockCount").GetValue(water)!=0)
                {
                    if(unchecked(Environment.TickCount-deadline)>=0)throw new InvalidOperationException("Water handoff timed out before landscape changes.");
                    Monitor.Wait(gate,10);
                }
                Buffers=(WaterSimulation.Cell[][])Field("m_waterBuffers").GetValue(water);
                // BeginRead uses this frame's parity to select the authoritative buffer.
                int current=(int)(~((uint)Field("m_waterFrameIndex").GetValue(water)>>6)&1);
                Depth=new ushort[HundredTiles.Core.ExpandedMap.HeightStride*HundredTiles.Core.ExpandedMap.HeightStride];
                for(int i=0;i<Depth.Length;i++)Depth[i]=Buffers[current][i].m_height;
            }
            catch{status.SetValue(water,previous);Monitor.PulseAll(gate);Monitor.Exit(gate);throw;}
        }
        static FieldInfo Field(string name)
        {var field=typeof(WaterSimulation).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(field==null)throw new MissingFieldException("WaterSimulation",name);return field;}
        public void Seed(ushort[] levels,ushort[] ground)
        {
            var masks=new ulong[][]{(ulong[])Field("m_waterExists1").GetValue(water),(ulong[])Field("m_waterExists2").GetValue(water),(ulong[])Field("m_waterExists3").GetValue(water)};
            for(int z=0;z<=HundredTiles.Core.ExpandedMap.RawResolution;z++)for(int x=0;x<=HundredTiles.Core.ExpandedMap.RawResolution;x++)
            {
                int i=z*HundredTiles.Core.ExpandedMap.HeightStride+x;if(levels[i]==0 || (x<=1080 && z<=1080))continue;
                ushort depth=(ushort)Math.Max(0,levels[i]-ground[i]);
                for(int b=0;b<Buffers.Length;b++)
                {Buffers[b][i].m_height=depth;Buffers[b][i].m_velocityX=0;Buffers[b][i].m_velocityZ=0;}
                if(depth!=0)foreach(var mask in masks)mask[z/HundredTiles.Core.WaterGrid.CellsPerBit]|=1UL<<(x/HundredTiles.Core.WaterGrid.CellsPerBit);
            }
        }
        public void Dispose(){status.SetValue(water,previous);Monitor.PulseAll(gate);Monitor.Exit(gate);}
    }
}

