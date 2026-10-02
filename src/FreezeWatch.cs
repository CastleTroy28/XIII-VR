using System;
using System.Globalization;
using System.IO;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// 0.1.149: a sample of every frame for
// FreezeDetector, and what it finds written to the log with the mod's last
// messages before it (what the mod was doing), so the next log tells what
// stopped: the frame, the headset's tracking, the character, the game's clock.
internal static class FreezeWatch
{
    private static readonly FreezeDetector detector=new();
    private static readonly string[] notes=new string[12];private static readonly float[] noteAt=new float[12];private static int noteHead;
    private static int reports,frame=-1;private static double modAt;private static bool started;
    private static readonly double[] partsAt=new double[6];
    private static CharacterController? controller;private static Transform? controllerRoot;
    internal const int MaxReports=60,MaxPerKind=20;
    private static readonly System.Collections.Generic.Dictionary<string,int> kinds=new();
    // The headset is worn: its pose changed in the last two seconds.
    private static System.Numerics.Vector3 lastHead;private static System.Numerics.Quaternion lastTurn;private static float movedAt=-10;private static bool has;
    internal static bool HeadMoving=>Time.realtimeSinceStartup-movedAt<2f;
    // Every message the mod writes (Bootstrap.Write): the last few, for the context of a freeze.
    internal static void Note(string s)
    {
        try{notes[noteHead]=s.Length>160?s.Substring(0,160):s;noteAt[noteHead]=Time.realtimeSinceStartup;noteHead=(noteHead+1)%notes.Length;}catch(Exception){}
    }
    internal static void Tick(CameraRig rig)
    {
        if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(!started){started=true;Start();}
        // 0.1.151: the mod's memory cleaned in small steps (before the frame's sample: its pause is this frame's).
        ModGcPacer.Tick();
        double mod=FramePerformance.ModTotalMs;float modMs=(float)(mod-modAt);modAt=mod;
        var parts=new System.Text.StringBuilder();
        for(int i=0;i<partsAt.Length;i++){double d=FramePerformance.PartTotals[i]-partsAt[i];partsAt[i]=FramePerformance.PartTotals[i];if(d>=1)parts.Append(parts.Length>0?", ":"").Append(FramePerformance.PartName(i)).Append(' ').Append(d.ToString("F0",CultureInfo.InvariantCulture)).Append(" ms");}
        var s=Sample(rig,modMs)with{ModParts=parts.ToString()};
        if(!has||s.Head!=lastHead||s.HeadRotation!=lastTurn){lastHead=s.Head;lastTurn=s.HeadRotation;movedAt=s.Now;has=true;}
        var found=detector.Step(s);
        foreach(var e in found)
        {
            if(reports>=MaxReports)break;
            kinds.TryGetValue(e.Kind,out int n);if(n>=MaxPerKind)continue;kinds[e.Kind]=n+1;reports++;
            Bootstrap.Warn(e.Kind+" "+(e.Seconds*1000).ToString("F0",CultureInfo.InvariantCulture)+" ms at "+DateTime.Now.ToString("HH:mm:ss",CultureInfo.InvariantCulture)+" (game "+s.Now.ToString("F1",CultureInfo.InvariantCulture)+" s): "+e.Detail+ModGcPacer.Recent(s.Now,e.Seconds)+Recent(s.Now,e.Seconds));
        }
    }
    private static FrameSample Sample(CameraRig rig,float modMs)
    {
        float now=Time.realtimeSinceStartup,dt=Time.unscaledDeltaTime;
        System.Numerics.Vector3 head=default;System.Numerics.Quaternion turn=System.Numerics.Quaternion.Identity;bool valid=false;
        try{if(rig.PhysicalHeadPose(out var p)){head=p.Position;turn=p.Rotation;valid=true;}}catch(Exception){}
        float stick=0;bool allowed=false;
        try{var l=LocomotionDriver.Current;if(l!=null){stick=l.MoveInput;allowed=l.MoveAllowed;}}catch(Exception){}
        var root=System.Numerics.Vector2.Zero;bool blocked=false;
        try
        {
            var t=rig.PlayerRoot;
            if(t!=null)
            {
                var p=t.position;root=new System.Numerics.Vector2(p.x,p.z);
                if(controllerRoot==null||controllerRoot.Pointer!=t.Pointer){controllerRoot=t;controller=t.GetComponentInChildren(Il2CppType.Of<CharacterController>(),true)?.TryCast<CharacterController>();}
                if(controller!=null)blocked=(controller.collisionFlags&CollisionFlags.Sides)!=0;
            }
        }
        catch(Exception){controller=null;controllerRoot=null;}
        bool menu=false;try{menu=GameUiControls.Current?.BlocksGameplay==true||QualityMenu.Open;}catch(Exception){}
        int gameGc=-1;try{gameGc=Il2CppSystem.GC.CollectionCount(0);}catch(Exception){}
        int scenes=0;try{scenes=UnityEngine.SceneManagement.SceneManager.sceneCount;}catch(Exception){}
        bool focused=Application.isFocused;string? taker=null;
        if(!focused)try{taker=WindowFocus.Foreground();}catch(Exception){}
        bool gameplay=true;try{gameplay=!rig.Frontend&&!rig.MovieActive&&!rig.Scripted;}catch(Exception){}
        return new FrameSample(now,dt,head,turn,valid,stick,allowed,root,blocked,Time.timeScale,menu,gameGc,GC.CollectionCount(0),modMs,scenes,focused,taker,Time.frameCount,gameplay);
    }
    // The mod's own messages in the moments before (what it was doing).
    private static string Recent(float now,float seconds)
    {
        var text=new System.Text.StringBuilder();
        for(int i=0;i<notes.Length;i++)
        {
            int k=(noteHead+i)%notes.Length;var n=notes[k];
            if(n==null||now-noteAt[k]>seconds+1.5f)continue;
            text.Append(" | ").Append(n);
        }
        return text.Length>0?"; the mod's last messages before it:"+text:"";
    }
    // Once: how the game's memory clean-up is set up (a full clean-up of a
    // large heap stops everything for a moment; an incremental one does not).
    private static void Start()
    {
        string incremental="unknown";
        try
        {
            var boot=Path.Combine(BepInEx.Paths.GameRootPath,"XIII_Data","boot.config");
            if(File.Exists(boot))
            {
                incremental="no (full clean-ups)";
                foreach(var line in File.ReadAllLines(boot))if(line.Trim().StartsWith("gc-max-time-slice",StringComparison.OrdinalIgnoreCase)){incremental="yes ("+line.Trim()+")";break;}
            }
        }
        catch(Exception ex){incremental="unknown ("+ex.Message+")";}
        string heap="";
        try{heap=" heap "+(UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong()/1048576)+" MB (used "+(UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()/1048576)+" MB)";}catch(Exception){}
        string clock="";try{clock="; physics step "+(Time.fixedDeltaTime*1000).ToString("F1",CultureInfo.InvariantCulture)+" ms, longest counted frame "+(Time.maximumDeltaTime*1000).ToString("F0",CultureInfo.InvariantCulture)+" ms";}catch(Exception){}
        Bootstrap.Write("FREEZE WATCH on: long frames (over "+(FreezeDetector.LongFrame*1000).ToString("F0",CultureInfo.InvariantCulture)+" ms), a frozen or lost head pose, a character that does not move while the stick is pushed, the game's clock stopping are written here with what happened then; the game's memory clean-up incremental: "+incremental+";"+heap+clock);
    }
    // For the PERF line: the game's memory clean-ups and heap.
    private static int gcAt=-1;
    internal static string Report()
    {
        try
        {
            int now=Il2CppSystem.GC.CollectionCount(0);int n=gcAt<0?0:now-gcAt;gcAt=now;
            return "gameGC="+n+" heapMB="+(UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()/1048576)+"/"+(UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong()/1048576);
        }
        catch(Exception){return "gameGC=?";}
    }
}
