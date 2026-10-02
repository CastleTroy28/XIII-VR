using System;using System.Linq;using System.Collections.Generic;using System.IO;using System.Globalization;using XiiiXR;
class PerformanceTests
{
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static void Main()
    {
        Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
        foreach(var old in Directory.GetFiles(BepInEx.Paths.ConfigPath,"XIII-XR-performance-*.csv"))File.Delete(old);
        CultureInfo.CurrentCulture=new CultureInfo("ru-RU");
        UnityEngine.Input.Press=true;
        for(int i=0;i<501;i++)
        {
            UnityEngine.Time.frameCount++;UnityEngine.Time.unscaledDeltaTime=.01f;FramePerformance.Tick();
            FramePerformance.Tick(); // two eye/render callbacks must not double count cadence or F3
            UnityEngine.Input.Press=false;
        }
        Check(FramePerformance.Visible,"F3 toggled twice within one frame");
        Check(FramePerformance.Display.Contains("100 FPS")&&FramePerformance.Display.Contains("10.0 ms"),"100Hz app cadence misreported");
        string report=Bootstrap.Messages.Find(s=>s.StartsWith("PERF appFPS"))??throw new Exception("missing metrics");
        Check(report.Contains("appFPS=100.0")&&report.Contains("p95Ms=10.00"),"frame times are locale-dependent or p95 wrong");
        int before=Bootstrap.Messages.FindAll(s=>s.StartsWith("PERF appFPS")).Count;
        UnityEngine.Time.frameCount++;UnityEngine.Time.unscaledDeltaTime=float.NaN;FramePerformance.Tick();
        UnityEngine.Time.frameCount++;UnityEngine.Time.unscaledDeltaTime=0;FramePerformance.Tick();
        Check(before==Bootstrap.Messages.FindAll(s=>s.StartsWith("PERF appFPS")).Count,"invalid frame emitted performance report");
        for(int i=0;i<5001;i++){UnityEngine.Time.frameCount++;UnityEngine.Time.unscaledDeltaTime=.001f;FramePerformance.Tick();}
        Check(FramePerformance.Display.Contains("1000 FPS")&&FramePerformance.Display.Contains("1.0 ms"),"bounded frame buffer wrap corrupts metrics");
        UnityEngine.Time.frameCount++;UnityEngine.Time.unscaledDeltaTime=.8f;FramePerformance.Tick();
        FramePerformance.Selection(true,FramePerformance.Begin(),"pistol");
        for(int i=0;i<430;i++){UnityEngine.Time.frameCount++;UnityEngine.Time.unscaledDeltaTime=.01f;FramePerformance.Tick();}
        Check(Bootstrap.Messages.Any(s=>s.Contains("maxMs=800.")&&s.Contains("over50ms=1")&&s.Contains("selections=1")),"rare long frame hidden by p95/averages");
        foreach(var file in Directory.GetFiles(BepInEx.Paths.ConfigPath,"XIII-XR-performance-*.csv"))
        {var lines=File.ReadAllLines(file);Check(lines.Length>=2&&lines[1].Split(',').Length==13,"performance CSV corrupted by decimal commas");File.Delete(file);}
        // 0.1.121: scene searches — at most one per frame, timers kept; cost reported and reset.
        SceneScan.ResetForTests();float a=0,b=0,c=0;
        Check(SceneScan.Due(ref a,1,10,100)&&a==11,"first search not due");
        Check(!SceneScan.Due(ref b,.5f,10,100)&&b==0,"second search ran in the same frame or lost its turn");
        Check(SceneScan.Due(ref b,.5f,10.01f,101)&&!SceneScan.Due(ref c,.5f,10.01f,101)&&SceneScan.Due(ref c,.5f,10.02f,102),"deferred searches do not run one per frame");
        Check(!SceneScan.Due(ref a,1,10.5f,103)&&SceneScan.Due(ref a,1,11,104),"search interval not kept");
        SceneScan.Record("canvas",1.5);SceneScan.Record("enemies",4.25);SceneScan.Record("bad",double.NaN);
        string scans=SceneScan.Report();Check(scans.Contains("scans=2")&&scans.Contains("scanMs=5.8")&&scans.Contains("scanMaxMs=4.25(enemies)")&&scans.Contains("scanBy=")&&scans.Contains("enemies:1/")&&scans.Contains("canvas:1/"),"scan report: "+scans);
        Check(SceneScan.Report().StartsWith("scans=0 scanMs=0.0"),"scan totals not reset after a report");
        // Compositor frames: GPU time, reprojection reasons, frames shown twice.
        var frames=new List<CompositorFrame>{new(0,4,11.1f,1,0,0),new(CompositorTiming.ReasonCpu|CompositorTiming.Motion,6,22.2f,2,1,0),new(CompositorTiming.ReasonGpu|CompositorTiming.Async|0x100,5,22.2f,2,0,1)};
        string timing=CompositorTiming.Summarize(frames);
        Check(timing.Contains("gpuMs=5.00")&&timing.Contains("gpuMaxMs=6.00")&&timing.Contains("reprojected=2/3")&&timing.Contains("lateCpu=1")&&timing.Contains("lateGpu=1")&&timing.Contains("motionSmoothing=1")&&timing.Contains("throttled=1")&&timing.Contains("shownTwice=2")&&timing.Contains("dropped=1")&&timing.Contains("misPresented=1"),"compositor summary: "+timing);
        Check(CompositorTiming.Summarize(new List<CompositorFrame>())=="compositor=none","empty compositor summary");
        CompositorTiming.Source=()=>throw new InvalidOperationException("gone");
        Check(CompositorTiming.Report().StartsWith("compositor=unavailable")&&CompositorTiming.Source==null&&CompositorTiming.Report()=="compositor=none","a failing compositor source is not dropped");
        // The PERF line carries the mod's CPU by part, the searches and the compositor.
        CompositorTiming.Source=()=>"gpuMs=1.00";SceneScan.Record("canvas",2);
        FramePerformance.Part(FramePerformance.Weapons,FramePerformance.Begin());FramePerformance.Part(99,0);
        for(int i=0;i<520;i++){UnityEngine.Time.frameCount++;UnityEngine.Time.unscaledDeltaTime=.01f;FramePerformance.Tick();}
        string line=Bootstrap.Messages.FindLast(s=>s.StartsWith("PERF appFPS"))!;
        Check(line.Contains(" modCPUms=")&&line.Contains("weapons=")&&line.Contains("render=")&&line.Contains("scans=1 ")&&line.Contains("gpuMs=1.00"),"PERF line without mod parts/searches/compositor: "+line);
        Check(FramePerformance.Display.Contains("modCPUms="),"F3 panel without the mod CPU");
        // 0.1.162: a kept search runs again at once after a scene change, else only when its time comes.
        {
            SceneScan.ResetForTests();float next=0;int epoch=int.MinValue;
            Check(SceneScan.Due(ref next,10,ref epoch,100,1)&&next==110,"a kept search did not run first");
            Check(!SceneScan.Due(ref next,10,ref epoch,105,2),"a kept search ran before its time");
            SceneScan.SceneChanged();
            Check(SceneScan.Due(ref next,10,ref epoch,106,3),"a kept search did not run after a scene change");
            SceneScan.SceneChanged();float other=0;SceneScan.Due(ref other,1,107,4);
            Check(!SceneScan.Due(ref next,10,ref epoch,107,4)&&SceneScan.Due(ref next,10,ref epoch,107.01f,5),"a search after a scene change shared a frame with another or was lost");
        }
        // 0.1.162: the half-rate hold of the headset software's smoothing is named.
        Check(FramePerformance.HalfRate(45.1,23.5,90)&&FramePerformance.HalfRate(44.6,25.6,90)&&!FramePerformance.HalfRate(89.6,12.6,90)&&!FramePerformance.HalfRate(60,22,90)&&!FramePerformance.HalfRate(45,40,90)&&!FramePerformance.HalfRate(45,22.2,0),"half-rate hold misjudged");
        CompositorTiming.HeadsetHz=200;
        for(int i=0;i<1100;i++){UnityEngine.Time.frameCount++;UnityEngine.Time.unscaledDeltaTime=.01f;FramePerformance.Tick();}
        Check(Bootstrap.Messages.Exists(s=>s.StartsWith("PERF HALF RATE")&&s.Contains("100 frames")&&s.Contains("200 Hz")),"half-rate hold not written");
        int notes=Bootstrap.Messages.FindAll(s=>s.StartsWith("PERF HALF RATE")).Count;
        for(int i=0;i<1100;i++){UnityEngine.Time.frameCount++;UnityEngine.Time.unscaledDeltaTime=.01f;FramePerformance.Tick();}
        Check(Bootstrap.Messages.FindAll(s=>s.StartsWith("PERF HALF RATE")).Count==notes,"half-rate note repeated within a minute");
        CompositorTiming.HeadsetHz=0;
        // 0.1.164: each camera's main-thread time per frame.
        {int world=CameraTiming.Slot("Camera - Enviroments");Check(CameraTiming.Slot("Camera - Enviroments")==world,"one camera counted twice");
         CameraTiming.Add(world,System.Diagnostics.Stopwatch.GetTimestamp()-System.Diagnostics.Stopwatch.Frequency/100);string cams=CameraTiming.Report(2);
         Check(cams.StartsWith("camMs=Camera-Enviroments:")&&CameraTiming.Report(1)=="camMs=none","camera timing: "+cams);
         // 0.1.165: when drawing starts and ends in the frame.
         for(int f=1;f<=20;f++){CameraTiming.PreCull(1000+f,.004f);CameraTiming.PreCull(1000+f,.006f);CameraTiming.PostRender(1000+f,.008f);CameraTiming.PostRender(1000+f,.009f);}
         CameraTiming.PreCull(2000,.001f);string drawn=CameraTiming.Frames();
         Check(drawn=="drawStartMs=4.0/4.0 drawEndMs=9.0/9.0","frame drawing times: "+drawn);}
        // 0.1.162: a long call is written with the step that took the time.
        var steps=new StepClock("test",10);steps.Begin();System.Threading.Thread.Sleep(15);steps.Mark("sleep");steps.Mark("quick");steps.End();
        Check(Bootstrap.Messages.Exists(s=>s.StartsWith("PERF SLOW test")&&s.Contains("sleep ")&&!s.Contains("quick")),"slow call not written with its step");
        int slow=Bootstrap.Messages.Count;steps.Begin();steps.Mark("quick");steps.End();
        Check(Bootstrap.Messages.Count==slow,"a short call written");
        foreach(var file in Directory.GetFiles(BepInEx.Paths.ConfigPath,"XIII-XR-performance-*.csv"))File.Delete(file);
        Console.WriteLine("PASS: one scene search per frame with timers kept; search cost, mod CPU by part and compositor timing (GPU, reprojection reasons, repeated frames) in the PERF line.");
        Console.WriteLine("PASS: actual monitor reports app cadence/p95, deduplicates per-frame callbacks/F3, ignores invalid dt, bounds sample buffer, writes locale-independent CSV. These synthetic timings do not measure the game.");
    }
}
namespace BepInEx{static class Paths{internal static string ConfigPath=>"xiii-xr/build/performance-test";}}
namespace UnityEngine
{
    static class Time{internal static int frameCount;internal static float unscaledDeltaTime;internal static float realtimeSinceStartup=0;}
    enum KeyCode{F3}
    static class Input{internal static bool Press;internal static bool GetKeyDown(KeyCode k)=>Press;}
}
namespace XiiiXR{static class Bootstrap{internal static readonly List<string> Messages=new();internal static void Write(string s)=>Messages.Add(s);internal static void Warn(string s)=>throw new Exception(s);}}
namespace XiiiXR{static class FreezeWatch{internal static string Report()=>"gameGC=0";}}
namespace XiiiXR{static class ModGcPacer{internal static string Report()=>"modGC paced=0";}}
// 0.1.188 PERF additions (GPT): collision-query and render-budget reports.
namespace XiiiXR{static class ContactWorld{internal static string Report(int frames)=>"contactMs=0.000";}static class RenderBudget{internal static string Report()=>"viewport=1.00";}}
