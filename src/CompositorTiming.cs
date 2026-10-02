using System;
using System.Collections.Generic;
namespace XiiiXR;
// 0.1.121: what the SteamVR compositor saw of the last frames (GPU time of
// the game, frames shown more than once, reprojection and whether the CPU or
// the GPU was late). Tells a CPU-bound game from a GPU-bound one in the log.
internal readonly record struct CompositorFrame(uint Flags,float GpuMs,float IntervalMs,uint Presents,uint Dropped,uint MisPresented);
internal static class CompositorTiming
{
    // openvr.h VRCompositor_Reprojection* flags.
    internal const uint ReasonCpu=0x01,ReasonGpu=0x02,Async=0x04,Motion=0x08,ThrottleMask=0xF00;
    internal static Func<string>? Source;
    // 0.1.162: the headset's refresh rate (0 unknown).
    internal static float HeadsetHz;
    internal static string Summarize(IReadOnlyList<CompositorFrame> frames)
    {
        int n=0,reprojected=0,cpu=0,gpu=0,motion=0,repeated=0,throttled=0;uint dropped=0,mis=0;
        double gpuSum=0,intervalSum=0;float gpuMax=0;
        foreach(var f in frames)
        {
            n++;
            if(float.IsFinite(f.GpuMs)&&f.GpuMs>=0){gpuSum+=f.GpuMs;gpuMax=Math.Max(gpuMax,f.GpuMs);}
            if(float.IsFinite(f.IntervalMs)&&f.IntervalMs>=0)intervalSum+=f.IntervalMs;
            if((f.Flags&(Async|Motion))!=0)reprojected++;
            if((f.Flags&ReasonCpu)!=0)cpu++;
            if((f.Flags&ReasonGpu)!=0)gpu++;
            if((f.Flags&Motion)!=0)motion++;
            if((f.Flags&ThrottleMask)!=0)throttled++;
            if(f.Presents>1)repeated++;
            dropped+=f.Dropped;mis+=f.MisPresented;
        }
        if(n==0)return "compositor=none";
        return FormattableString.Invariant($"gpuMs={gpuSum/n:F2} gpuMaxMs={gpuMax:F2} frameIntervalMs={intervalSum/n:F2} reprojected={reprojected}/{n} lateCpu={cpu} lateGpu={gpu} motionSmoothing={motion} throttled={throttled} shownTwice={repeated} dropped={dropped} misPresented={mis}");
    }
    internal static string Report()
    {
        try{return Source?.Invoke()??"compositor=none";}
        catch(Exception ex){Source=null;return "compositor=unavailable("+ex.Message+")";}
    }
}
