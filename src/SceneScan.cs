using System;
using System.Diagnostics;
namespace XiiiXR;
// 0.1.121: scene-wide object searches (Resources.FindObjectsOfTypeAll walks
// every loaded object of the game, well over 100 000) cost milliseconds each.
// About twenty a second ran in plain gameplay, and their timers lined up so
// several landed in the same frame: that frame missed the headset refresh,
// SteamVR fell back to half rate with reprojection, and everything that moves
// against the world (hands, gun, subtitles, videos, NPCs) doubled while
// walking. Now at most one search runs in a frame; the others wait for the
// next frame. Their cost is reported in the PERF line.
internal static class SceneScan
{
    private static int frame=int.MinValue;
    internal static int Count {get;private set;}
    internal static double TotalMs {get;private set;}
    internal static double MaxMs {get;private set;}
    internal static string MaxName {get;private set;}="";
    // Due when its time has come and no other search ran in this frame.
    internal static bool Due(ref float next,float interval,float now,int currentFrame)
    {
        if(now<next||currentFrame==frame)return false;
        frame=currentFrame;next=now+Math.Max(0,interval);return true;
    }
    internal static bool Due(ref float next,float interval)=>Due(ref next,interval,UnityEngine.Time.realtimeSinceStartup,UnityEngine.Time.frameCount);
    // 0.1.162: counts scene loads and unloads (CameraRig). A search kept
    // between its rare runs runs again at once after one (the next frame free
    // of other searches).
    internal static int Epoch{get;private set;}
    internal static void SceneChanged()=>Epoch++;
    internal static bool Due(ref float next,float interval,ref int epoch,float now,int currentFrame)
    {
        if(epoch!=Epoch){epoch=Epoch;next=0;}
        return Due(ref next,interval,now,currentFrame);
    }
    internal static bool Due(ref float next,float interval,ref int epoch)=>Due(ref next,interval,ref epoch,UnityEngine.Time.realtimeSinceStartup,UnityEngine.Time.frameCount);
    internal static long Begin()=>Stopwatch.GetTimestamp();
    internal static void End(string name,long start)=>Record(name,(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency);
    // 0.1.164: which searches ran and what they cost (scanBy=name:count/ms).
    private const int Kinds=32;
    private static readonly string[] kindNames=new string[Kinds];private static readonly int[] kindCount=new int[Kinds];private static readonly double[] kindMs=new double[Kinds];private static int kinds;
    internal static void Record(string name,double ms)
    {
        if(!double.IsFinite(ms)||ms<0)return;
        Count++;TotalMs+=ms;if(ms>MaxMs){MaxMs=ms;MaxName=name;}
        int k=Array.IndexOf(kindNames,name,0,kinds);
        if(k<0&&kinds<Kinds){k=kinds++;kindNames[k]=name;}
        if(k>=0){kindCount[k]++;kindMs[k]+=ms;}
    }
    internal static string Report()
    {
        string text=FormattableString.Invariant($"scans={Count} scanMs={TotalMs:F1} scanMaxMs={MaxMs:F2}")+(MaxName.Length>0?"("+MaxName+")":"");
        var by=new System.Text.StringBuilder();
        for(int k=0;k<kinds;k++)if(kindCount[k]>0){by.Append(by.Length==0?" scanBy=":",").Append(kindNames[k].Replace(' ','-')).Append(':').Append(kindCount[k]).Append('/').Append(kindMs[k].ToString("F1",System.Globalization.CultureInfo.InvariantCulture));kindCount[k]=0;kindMs[k]=0;}
        Count=0;TotalMs=MaxMs=0;MaxName="";return text+by;
    }
    internal static void ResetForTests(){frame=int.MinValue;Count=0;TotalMs=MaxMs=0;MaxName="";Epoch=0;Array.Clear(kindCount,0,Kinds);Array.Clear(kindMs,0,Kinds);}
}
