using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using UnityEngine;
namespace XiiiXR;
// 0.1.151. Two logs: the frame at 14:15:59 took 176 ms, and 32 of the
// 40 long frames (over 120 ms) of the last logs had the mod's own memory
// clean-up in them (the .NET runtime inside the game, not the game's). That
// runtime lets garbage pile up - its young generation is sized by the CPU's
// cache, tens of MB on a large-cache CPU - and then cleans it all at once:
// 150-450 ms with everything stopped, every 20-60 s.
// Now it is cleaned in small steps: after a few MB of new objects, a young-
// generation clean-up (a few ms; the step adapts to the pauses measured), and
// full blocking clean-ups are avoided while playing (SustainedLowLatency).
// Every clean-up the runtime still does on its own is described in the log
// (which generation, how long it stopped the game) when it was long.
internal static class ModGcPacer
{
    private static long allocatedAtGc=-1;private static int lastCount=-1;private static bool started,failed;
    private static long step=GcPaceMath.StartStep;private static double pauseAverage;
    private static int paced,natural,reports;private static double pacedMax,naturalMax,pacedTotal;
    internal static string Last {get;private set;}="";
    internal static float LastAt {get;private set;}=-100;
    internal static bool Enabled=>QualityOptions.GcSteps?.Value!=false;
    internal static void Tick()
    {
        if(failed)return;
        try
        {
            if(!started){started=true;Start();}
            int count=GC.CollectionCount(0);long allocated=GC.GetTotalAllocatedBytes(false);
            if(lastCount<0||allocatedAtGc<0){lastCount=count;allocatedAtGc=allocated;return;}
            // A clean-up the runtime did on its own since the last frame.
            if(count!=lastCount){lastCount=count;allocatedAtGc=allocated;Natural();}
            if(!Enabled||!GcPaceMath.Due(allocated-allocatedAtGc,step))return;
            long before=allocated-allocatedAtGc;
            var watch=Stopwatch.StartNew();
            GC.Collect(0,GCCollectionMode.Forced,true,false);
            double ms=watch.Elapsed.TotalMilliseconds;
            lastCount=GC.CollectionCount(0);allocatedAtGc=GC.GetTotalAllocatedBytes(false);
            paced++;pacedTotal+=ms;pacedMax=Math.Max(pacedMax,ms);
            pauseAverage=pauseAverage<=0?ms:pauseAverage*.8+ms*.2;
            long next=GcPaceMath.Adapt(step,pauseAverage);
            Last="the mod's paced young clean-up "+ms.ToString("F1",CultureInfo.InvariantCulture)+" ms after "+(before/1048576.0).ToString("F1",CultureInfo.InvariantCulture)+" MB";LastAt=Time.realtimeSinceStartup;
            if(next!=step&&reports<40){reports++;Bootstrap.Write("MOD GC step "+(step>>20)+" -> "+(next>>20)+" MB (paced clean-ups average "+pauseAverage.ToString("F1",CultureInfo.InvariantCulture)+" ms)");}
            step=next;
            if(ms>GcPaceMath.SlowMs&&reports<40){reports++;Bootstrap.Warn("MOD GC a paced young clean-up took "+ms.ToString("F0",CultureInfo.InvariantCulture)+" ms ("+(before/1048576.0).ToString("F1",CultureInfo.InvariantCulture)+" MB since the last): "+Info(GCKind.Ephemeral));}
        }
        catch(Exception ex){failed=true;Bootstrap.Warn("MOD GC pacing off: "+ex.Message);}
    }
    private static void Natural()
    {
        natural++;
        var info=GC.GetGCMemoryInfo(GCKind.Any);double ms=Pause(info);naturalMax=Math.Max(naturalMax,ms);
        Last="the mod's own "+(info.Generation==0?"young":info.Generation==1?"middle":"full")+" clean-up (generation "+info.Generation+(info.Concurrent?", background":"")+(info.Compacted?", compacting":"")+") stopped the game "+ms.ToString("F0",CultureInfo.InvariantCulture)+" ms";
        LastAt=Time.realtimeSinceStartup;
        if(ms>=GcPaceMath.SlowMs&&reports<40){reports++;Bootstrap.Warn("MOD GC "+Last+": "+Info(GCKind.Any));}
    }
    private static double Pause(GCMemoryInfo info)
    {
        double ms=0;var pauses=info.PauseDurations;
        for(int i=0;i<pauses.Length;i++)ms+=pauses[i].TotalMilliseconds;
        return ms;
    }
    private static string Info(GCKind kind)
    {
        try
        {
            var i=GC.GetGCMemoryInfo(kind);var g=i.GenerationInfo;
            string gens="";for(int n=0;n<g.Length&&n<5;n++)gens+=(n==0?"":",")+(g[n].SizeBeforeBytes/1048576.0).ToString("F0",CultureInfo.InvariantCulture)+">"+(g[n].SizeAfterBytes/1048576.0).ToString("F0",CultureInfo.InvariantCulture);
            return "#"+i.Index+" gen"+i.Generation+" pause "+Pause(i).ToString("F1",CultureInfo.InvariantCulture)+" ms, promoted "+(i.PromotedBytes/1048576.0).ToString("F1",CultureInfo.InvariantCulture)+" MB, heap "+(i.HeapSizeBytes>>20)+" MB, generations MB before>after "+gens
                +", waiting finalizers "+i.FinalizationPendingCount+", pinned "+i.PinnedObjectsCount+(i.Concurrent?", background":"")+(i.Compacted?", compacted":"");
        }
        catch(Exception ex){return "no details ("+ex.Message+")";}
    }
    private static void Start()
    {
        string mode="?";
        try{var was=GCSettings.LatencyMode;if(was!=GCLatencyMode.SustainedLowLatency)GCSettings.LatencyMode=GCLatencyMode.SustainedLowLatency;mode=was+" -> "+GCSettings.LatencyMode;}
        catch(Exception ex){mode="unchanged ("+ex.Message+")";}
        string budget="";
        try{var i=GC.GetGCMemoryInfo(GCKind.Ephemeral);if(i.Index>0&&i.GenerationInfo.Length>0)budget=", young generation before its last clean-up "+(i.GenerationInfo[0].SizeBeforeBytes>>20)+" MB";}catch(Exception){}
        Bootstrap.Write("MOD GC pacing "+(Enabled?"on":"off (config ModGcSteps=false)")+": a young clean-up every "+(step>>20)+" MB of the mod's new objects (adapts "+(GcPaceMath.MinStep>>20)+"-"+(GcPaceMath.MaxStep>>20)+" MB), so the runtime never piles up tens of MB and stops the game for 150-450 ms; runtime "+Environment.Version+" server="+GCSettings.IsServerGC+" latency "+mode+budget+", "+(Environment.ProcessorCount)+" CPU threads");
    }
    // For the PERF line.
    internal static string Report()
    {
        string s="modGC paced="+paced+(paced>0?"(avg "+(pacedTotal/paced).ToString("F1",CultureInfo.InvariantCulture)+" max "+pacedMax.ToString("F1",CultureInfo.InvariantCulture)+" ms)":"")+" natural="+natural+(natural>0?"(max "+naturalMax.ToString("F0",CultureInfo.InvariantCulture)+" ms)":"")+" step="+(step>>20)+"MB";
        paced=natural=0;pacedMax=naturalMax=pacedTotal=0;return s;
    }
    // A clean-up in the last `seconds` (for a FREEZE line).
    internal static string Recent(float now,float seconds)=>now-LastAt<=seconds+.1f&&Last.Length>0?"; "+Last:"";
}
