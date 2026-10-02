using System;
namespace XiiiXR;
// Optional presentation must never suspend headset poses or gameplay input.
// Cache the delegate at construction; no per-eye closures or unbounded logging.
internal sealed class OptionalWork
{
    private readonly string name;
    private readonly Action work;
    private float retryAt,nextWarning;
    internal OptionalWork(string name,Action work){this.name=name;this.work=work;}
    internal void Run(float now)
    {
        if(now<retryAt)return;
        try{work();}
        catch(Exception ex)
        {
            retryAt=now+.5f;
            if(now<nextWarning)return;
            nextWarning=now+10;
            Bootstrap.Warn(name+" temporarily unavailable; head tracking continues: "+ex);
        }
    }
    internal void Reset()=>retryAt=0;
}
// A transient native object failure must not permanently freeze the rig.
// Explicit VR shutdown is separate and can never be undone by scene recovery.
internal sealed class TrackingRecovery
{
    internal bool Suspended{get;private set;}
    internal bool Stopped{get;private set;}
    internal bool Active=>!Suspended&&!Stopped;
    private float retryAt,nextWarning;
    internal bool Ready(float now)
    {
        if(Stopped||Suspended&&now<retryAt)return false;
        Suspended=false;return true;
    }
    internal bool Suspend(float now)
    {
        if(Stopped)return false;
        Suspended=true;retryAt=now+.5f;
        if(now<nextWarning)return false;
        nextWarning=now+10;return true;
    }
    internal void SceneChanged(){if(!Stopped){Suspended=false;retryAt=0;}}
    internal void Stop(){Stopped=true;Suspended=true;}
}
