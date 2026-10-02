using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace XiiiXR;
// App cadence and explicitly measured mod CPU scopes, not compositor/GPU FPS.
internal static class FramePerformance
{
    private static readonly double[] frames=new double[2048];
    private static int count,slot,lastFrame=-1;
    private static double elapsed,hands,weapon,bake;
    private static double maxFrame,selectionMax;
    private static int slowFrames,selections,gc0=GC.CollectionCount(0);
    private static long allocated=GC.GetTotalAllocatedBytes(false);
    private static string? path;
    internal static bool Visible;
    internal static string Display="FPS: collecting...";
    internal static long Begin()=>Stopwatch.GetTimestamp();
    // 0.1.121: the mod's whole CPU time per frame, by part (Update blocks,
    // LateUpdate, the pre-render pass).
    internal const int Rig=0,Move=1,Weapons=2,Interact=3,Late=4,Render=5;
    private static readonly string[] partNames={"rig","move","weapons","interact","late","render"};
    private static readonly double[] parts=new double[6],lastParts=new double[6];
    private static int halfRateWindows;private static DateTime nextHalfRateNote;
    // 0.1.162: the app held at half the headset's refresh: its rate within 4 %
    // of half and every frame alike (p95 within 20 % of the half-rate frame).
    internal static bool HalfRate(double fps,double p95Ms,double hz)
    {
        if(!(hz>30)||!double.IsFinite(fps)||!double.IsFinite(p95Ms))return false;
        double half=hz/2,frame=1000/half;
        return Math.Abs(fps-half)<=half*.04&&p95Ms<=frame*1.2&&p95Ms>=frame*.9;
    }
    // 0.1.165: and the memory each part allocates (the log showed ~350 MB
    // allocated by the mod in the first seconds of a mission).
    private static readonly long[] partAllocAt=new long[6],partBytes=new long[6];
    internal static long Begin(int part)
    {
        if(part>=0&&part<partAllocAt.Length)partAllocAt[part]=GC.GetAllocatedBytesForCurrentThread();
        return Stopwatch.GetTimestamp();
    }
    internal static void Part(int part,long start)
    {
        if(part<0||part>=parts.Length)return;
        double ms=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
        if(double.IsFinite(ms)&&ms>=0){parts[part]+=ms;ModTotalMs+=ms;PartTotals[part]+=ms;}
        if(partAllocAt[part]!=0){long b=GC.GetAllocatedBytesForCurrentThread()-partAllocAt[part];partAllocAt[part]=0;if(b>0)partBytes[part]+=b;}
    }
    internal static string AllocParts()
    {
        long total=0;var text=new System.Text.StringBuilder();
        for(int i=0;i<partBytes.Length;i++){total+=partBytes[i];if(partBytes[i]>=1<<20)text.Append(text.Length==0?"":" ").Append(partNames[i]).Append('=').Append((partBytes[i]/1048576.0).ToString("F1",CultureInfo.InvariantCulture));partBytes[i]=0;}
        return "modAllocMB="+(total/1048576.0).ToString("F1",CultureInfo.InvariantCulture)+(text.Length>0?"("+text+")":"");
    }
    // 0.1.149: the same by part, never reset (FreezeWatch: which part of the mod a long frame was).
    internal static readonly double[] PartTotals=new double[6];
    internal static string PartName(int i)=>i>=0&&i<partNames.Length?partNames[i]:"?";
    // 0.1.149: all of the mod's measured time so far (FreezeWatch: how much of a long frame was the mod's).
    internal static double ModTotalMs{get;private set;}
    internal static string Parts(int frames)
    {
        if(frames<=0)frames=1;double total=0;var text=new System.Text.StringBuilder();
        for(int i=0;i<parts.Length;i++){total+=parts[i];text.Append(' ').Append(partNames[i]).Append('=').Append((parts[i]/frames).ToString("F2",CultureInfo.InvariantCulture));}
        return "modCPUms="+(total/frames).ToString("F2",CultureInfo.InvariantCulture)+" ("+text.ToString().Trim()+")";
    }
    internal static void Scope(string name,long start)
    {
        double ms=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
        if(ms>=8)Bootstrap.Write(FormattableString.Invariant($"PERF SCOPE {name} ms={ms:F2} frame={Time.frameCount}"));
    }
    internal static void Selection(bool reused,long start,string profile)
    {
        double ms=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
        selections++;selectionMax=Math.Max(selectionMax,ms);
        Bootstrap.Write(FormattableString.Invariant($"PERF SELECT profile={profile} cached={reused} modMs={ms:F2}"));
    }
    internal static void End(long start,int category)
    {
        double ms=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
        if(category==0)hands+=ms;else if(category==1)weapon+=ms;else bake+=ms;
    }
    internal static void Tick()
    {
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(Input.GetKeyDown(KeyCode.F3))Visible=!Visible;
        double dt=Time.unscaledDeltaTime;
        if(!double.IsFinite(dt)||dt<=0)return;
        frames[slot++%frames.Length]=dt*1000;count++;elapsed+=dt;
        maxFrame=Math.Max(maxFrame,dt*1000);if(dt>.05)slowFrames++;
        if(elapsed<5)return;
        var samples=new double[Math.Min(count,frames.Length)];Array.Copy(frames,samples,samples.Length);Array.Sort(samples);
        double p95=samples[(int)Math.Clamp(Math.Ceiling(samples.Length*.95)-1,0,samples.Length-1)];
        long current=GC.GetTotalAllocatedBytes(false);double mb=Math.Max(0,current-allocated)/1048576.0/elapsed;
        double fps=count/elapsed,mean=1000*elapsed/count,h=hands/count,w=weapon/count,b=bake/count;
        int collections=GC.CollectionCount(0)-gc0;
        Display=FormattableString.Invariant($"APP {fps:F0} FPS  {mean:F1} ms\np95 {p95:F1}  MAX {maxFrame:F0} ms\nHANDS {h:F2} / WEAPON {w:F2} ms\nSWITCH MAX {selectionMax:F1} ms");
        Array.Copy(parts,lastParts,parts.Length);
        string mod=Parts(count);Array.Clear(parts,0,parts.Length);
        Display+="\n"+mod.Split(' ')[0];
        double modMs=0;for(int i=0;i<lastParts.Length;i++)modMs+=lastParts[i];modMs/=Math.Max(1,count);
        Bootstrap.Write(FormattableString.Invariant($"PERF appFPS={fps:F1} frameMs={mean:F2} p95Ms={p95:F2} handsCPUms={h:F3} weaponCPUms={w:F3} handBakeCPUms={b:F3} managedMBps={mb:F2} frames={count} maxMs={maxFrame:F2} over50ms={slowFrames} selectionMaxMs={selectionMax:F2} selections={selections} gc0={collections}")
            +" "+mod+" "+AllocParts()+" "+SceneScan.Report()+" "+CameraTiming.Report(count)+" "+CameraTiming.Frames()+" "+ContactWorld.Report(count)+" "+RenderBudget.Report()+" "+WorldStats.Report()+" "+FreezeWatch.Report()+" "+ModGcPacer.Report()+" "+CompositorTiming.Report());
        // 0.1.162: held at exactly half the headset's rate (the headset
        // software's frame smoothing): say so once a minute.
        halfRateWindows=HalfRate(fps,p95,CompositorTiming.HeadsetHz)?halfRateWindows+1:0;
        if(halfRateWindows>=2&&DateTime.UtcNow>=nextHalfRateNote)
        {
            nextHalfRateNote=DateTime.UtcNow.AddSeconds(60);
            Bootstrap.Write(FormattableString.Invariant($"PERF HALF RATE the game is held at {fps:F0} frames a second, half the headset's {CompositorTiming.HeadsetHz:F0} Hz, every frame alike (p95 {p95:F1} ms) while the mod takes {modMs:F1} ms a frame: the headset software's frame smoothing (Pimax Smart Smoothing, SteamVR Motion Smoothing) holds it there and makes up every other frame - things that move against the world (the hands, the guns, the body) double while walking"));
        }
        try
        {
            if(path==null)
            {
                path=Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-performance-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+".csv");
                File.WriteAllText(path,"utc,app_fps,frame_ms,p95_ms,hands_cpu_ms,weapon_cpu_ms,hand_bake_cpu_ms,managed_mb_per_s,max_frame_ms,over50ms,selection_max_ms,selections,gc0\n");
                Bootstrap.Write("PERF CSV="+path+"; F3 toggles in-headset performance panel. GPU/compositor timing is not measured.");
            }
            if(path!="")File.AppendAllText(path,FormattableString.Invariant($"{DateTime.UtcNow:O},{fps:F2},{mean:F3},{p95:F3},{h:F3},{w:F3},{b:F3},{mb:F3},{maxFrame:F3},{slowFrames},{selectionMax:F3},{selections},{collections}\n"));
        }
        catch(Exception ex){if(path!="")Bootstrap.Warn("PERF CSV unavailable: "+ex.Message);path="";}
        count=slot=0;elapsed=hands=weapon=bake=0;allocated=current;
        maxFrame=selectionMax=0;slowFrames=selections=0;gc0=GC.CollectionCount(0);
    }
}
