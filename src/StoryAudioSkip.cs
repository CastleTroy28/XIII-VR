using System;
using System.Collections.Generic;
using System.Diagnostics;
using FMODUnity;
namespace XiiiXR;
// 0.1.98: FMOD does not follow Time.timeScale. While a cutscene is
// fast-forwarded (x20, master bus muted) its voice lines and one-shot effects
// keep playing in real time, so after the skip the characters were still
// talking. Stop every one-shot event while skipping; music and snapshots are
// left alone. Events started on the very last frame (the cutscene's own
// "what happens next" signal) survive the final pass.
internal static class StoryAudioSkip
{
    private static readonly List<FMOD.Studio.EventDescription> oneShots=new();
    private static int bankCount=-1,passes,stopped;
    private static float nextPass,worstMs;
    internal static bool Music(string path)
    {
        path=path.ToLowerInvariant();
        return path.StartsWith("snapshot:")||path.Contains("music")||path.Contains("/mus/")||path.Contains("/mus_")||path.Contains("stinger")
            ||path.Contains("/amb")||path.Contains("ambience")||path.Contains("ambient")||path.Contains("/ui/")||path.Contains("/menu");
    }
    internal static void Begin()
    {
        passes=0;stopped=0;worstMs=0;nextPass=0;
        try{Build();Pass(0);}
        catch(Exception ex){Bootstrap.Warn("STORY audio skip: "+ex.Message);oneShots.Clear();bankCount=-1;}
    }
    internal static void Tick()
    {
        if(oneShots.Count==0||UnityEngine.Time.realtimeSinceStartup<nextPass)return;
        try{Pass(0);}catch(Exception ex){Bootstrap.Warn("STORY audio skip: "+ex.Message);oneShots.Clear();}
    }
    internal static void End()
    {
        try{if(oneShots.Count>0)Pass(150);}catch(Exception ex){Bootstrap.Warn("STORY audio skip: "+ex.Message);}
        Bootstrap.Write("STORY audio skip: stopped="+stopped+" passes="+passes+" worstMs="+worstMs.ToString("F1")+" oneShotEvents="+oneShots.Count);
    }
    private static void Build()
    {
        var system=RuntimeManager.StudioSystem;
        system.getBankCount(out int count);
        if(count==bankCount&&oneShots.Count>0)return;
        var watch=Stopwatch.StartNew();
        oneShots.Clear();bankCount=count;int events=0,music=0;
        system.getBankList(out var banks);
        for(int b=0;banks!=null&&b<banks.Length;b++)
        {
            if(banks[b].getEventList(out var list)!=FMOD.RESULT.OK||list==null)continue;
            for(int i=0;i<list.Length;i++)
            {
                var d=list[i];events++;
                d.isSnapshot(out bool snapshot);if(snapshot)continue;
                d.isOneshot(out bool once);if(!once)continue;
                string path="";try{d.getPath(out path);}catch{path="";}
                if(Music(path??"")){music++;continue;}
                oneShots.Add(d);
            }
        }
        Bootstrap.Write("STORY audio skip ready: banks="+count+" events="+events+" oneShot="+oneShots.Count+" musicKept="+music+" ms="+watch.ElapsedMilliseconds);
    }
    // minPositionMs>0: keep instances that started less than that ago.
    private static void Pass(int minPositionMs)
    {
        var watch=Stopwatch.StartNew();passes++;
        foreach(var d in oneShots)
        {
            if(d.getInstanceCount(out int count)!=FMOD.RESULT.OK||count<=0)continue;
            if(d.getInstanceList(out var list)!=FMOD.RESULT.OK||list==null)continue;
            for(int i=0;i<list.Length;i++)
            {
                var instance=list[i];
                instance.getPlaybackState(out var state);
                if(state==FMOD.Studio.PLAYBACK_STATE.STOPPED||state==FMOD.Studio.PLAYBACK_STATE.STOPPING)continue;
                if(minPositionMs>0&&instance.getTimelinePosition(out int position)==FMOD.RESULT.OK&&position<minPositionMs)continue;
                if(instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE)==FMOD.RESULT.OK)stopped++;
            }
        }
        float ms=(float)watch.Elapsed.TotalMilliseconds;worstMs=Math.Max(worstMs,ms);
        // A large bank set: do not spend every VR frame on it.
        nextPass=UnityEngine.Time.realtimeSinceStartup+(ms>4?.1f:0);
    }
}
