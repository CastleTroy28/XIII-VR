using System;
namespace XiiiXR;
internal sealed class ShotFeedback
{
    private float previousTime=-1,lastShot=float.NegativeInfinity,decay=.10f;
    internal float Kickback { get; private set; }
    internal float Pitch { get; private set; }
    internal float FlashUntil { get; private set; }
    internal bool Supported { get; private set; }
    internal int Sequence { get; private set; }
    internal void Reset()
    { previousTime=-1; lastShot=float.NegativeInfinity; Kickback=Pitch=FlashUntil=0; Supported=false; Sequence=0; }
    internal void Advance(float now)
    {
        float dt=previousTime<0?0:Math.Max(0,now-previousTime); previousTime=now;
        float damping=MathF.Exp(-dt/decay); Kickback*=damping; Pitch*=damping;
    }
    internal bool Fire(float now,string profile,bool supported)
    {
        // SpreadFire may announce each pellet. One shell creates one impulse.
        if(!float.IsFinite(now) || now-lastShot<.025f) return false;
        Advance(now); lastShot=now; Supported=supported; decay=supported?.065f:.11f;
        float back=profile=="shotgun"?.060f:profile=="ak47"?.018f:profile=="revolver"?.042f:.025f;
        float pitch=profile=="shotgun"?6.5f:profile=="ak47"?1.8f:profile=="revolver"?7f:4f;
        float support=supported?.4f:1f;
        Kickback=Math.Min(.10f,Kickback+back*support); Pitch=Math.Min(12,Pitch+pitch*support);
        Sequence++; FlashUntil=now+.03f; return true;
    }
}
// Short non-blocking legacy OpenVR pulse train. Valve requires >=5 ms between
// pulses to a controller. Haptics are tied to real shots, never trigger polling.
internal sealed class HapticBurst
{
    private float until,nextPulse;
    private ushort strength;
    private uint device=uint.MaxValue;
    internal void Cancel() { until=0; strength=0; device=uint.MaxValue; }
    internal void Queue(float now,uint index,ushort duration,float seconds)
    {
        device=index; strength=(ushort)Math.Clamp((int)duration,0,3999);
        until=now+Math.Clamp(seconds,0,.18f);
    }
    internal bool TryPulse(float now,uint index,bool available,out ushort microseconds)
    {
        microseconds=0;
        if(!available || index!=device || now>=until) { Cancel(); return false; }
        if(strength==0 || now<nextPulse) return false;
        nextPulse=now+.006f; microseconds=strength; return true;
    }
}
// Gun suspension must never erase a physical impact queued by PunchDriver.
// Both sources share one per-controller rate limit and produce at most one pulse.
internal sealed class HapticChannel
{
    private readonly HapticBurst shot=new(),impact=new();
    private float nextPulse;
    internal void Cancel(){shot.Cancel();impact.Cancel();}
    internal void CancelShot()=>shot.Cancel();
    internal void Queue(float now,uint index,ushort duration,float seconds)=>shot.Queue(now,index,duration,seconds);
    internal void Impact(float now,uint index)=>impact.Queue(now,index,3999,.16f);
    internal void Mechanism(float now,uint index,ushort strength,float seconds)=>impact.Queue(now,index,strength,seconds);
    internal bool TryPulse(float now,uint device,bool available,bool supportGrip,out ushort duration)
    {
        duration=0;
        if(!available){Cancel();return false;}
        if(now<nextPulse)return false;
        bool a=shot.TryPulse(now,device,supportGrip,out ushort s);
        bool b=impact.TryPulse(now,device,true,out ushort p);
        if(!a&&!b)return false;
        nextPulse=now+.006f;duration=Math.Max(s,p);return true;
    }
}
