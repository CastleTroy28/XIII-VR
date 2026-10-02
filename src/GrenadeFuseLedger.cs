using System;
using System.Collections.Generic;
namespace XiiiXR;
// 0.1.140: the thrown grenade's fuse sound ticked on and on after the bang
// (0.1.138 muted it in flight and gave its volume back at the explosion).
// Every grenade the player throws is noted here (its fuse sound instance at
// the launch and at the explosion); a few seconds after the bang (its tail)
// those instances are stopped, and a grenade that never exploded is let go
// after its fuse plus a margin. While any of the player's grenades is live,
// no fuse sound is touched by the sweep.
internal sealed class GrenadeFuseLedger
{
    internal const float Tail=3.5f,Lost=8f;
    private sealed class Entry{internal IntPtr Id,Launch,Blast;internal float LaunchedAt,Delay,ExplodedAt=-1;}
    private readonly List<Entry> entries=new();
    internal int Count=>entries.Count;
    internal void Launch(IntPtr id,IntPtr handle,float now,float delay)
    {
        entries.RemoveAll(e=>e.Id==id);
        if(entries.Count>=16)entries.RemoveAt(0);
        entries.Add(new Entry{Id=id,Launch=handle,LaunchedAt=now,Delay=float.IsFinite(delay)&&delay>0?delay:5});
    }
    // True when it was one of the player's grenades.
    internal bool Exploded(IntPtr id,IntPtr handle,float now)
    {
        foreach(var e in entries)if(e.Id==id&&e.ExplodedAt<0){e.ExplodedAt=now;e.Blast=handle;return true;}
        return false;
    }
    internal bool Known(IntPtr id){foreach(var e in entries)if(e.Id==id)return true;return false;}
    // Until when the player's grenades may still make their own sound.
    internal float LiveUntil
    {
        get{float t=-1;foreach(var e in entries)t=Math.Max(t,e.ExplodedAt>=0?e.ExplodedAt+Tail:e.LaunchedAt+e.Delay+Tail);return t;}
    }
    internal bool Live(IntPtr id,float now)
    {
        foreach(var e in entries)if(e.Id==id)return e.ExplodedAt>=0?now<e.ExplodedAt+Tail:now<e.LaunchedAt+e.Delay+Lost;
        return false;
    }
    // Instances to stop now (distinct, non-zero); their entries are done.
    internal List<IntPtr> Due(float now)
    {
        var stop=new List<IntPtr>();
        for(int i=entries.Count-1;i>=0;i--)
        {
            var e=entries[i];
            bool done=e.ExplodedAt>=0?now>=e.ExplodedAt+Tail:now>=e.LaunchedAt+e.Delay+Lost;
            if(!done)continue;
            if(e.Launch!=IntPtr.Zero&&!stop.Contains(e.Launch))stop.Add(e.Launch);
            if(e.Blast!=IntPtr.Zero&&!stop.Contains(e.Blast))stop.Add(e.Blast);
            entries.RemoveAt(i);
        }
        return stop;
    }
    internal void Clear()=>entries.Clear();
}
