using System;
using System.Numerics;
namespace XiiiXR;
// Physical tracking-space motion only: stick movement/turning cannot create speed.
internal sealed class PunchMotion
{
    private Vector3 previous,start,contactPosition,contactDirection,lastDirection,previousSteady;
    private float previousTime,slowTime,cooldown,fastUntil,peakTime;
    // 0.1.108: a held chair/ashtray/bottle breaks (and does damage) only on
    // a real swing: the hand's top speed just before the contact.
    internal const float PropSwingSpeed=3f;
    internal float Peak {get;private set;}
    internal static bool PropSwing(float peak)=>float.IsFinite(peak)&&peak>=PropSwingSpeed;
    private bool initialized,armed,spent,neutral,wasGrip;
    internal float Speed { get; private set; }
    // 0.1.206: waiting to rearm after a touch (for the log).
    internal bool Spent=>spent;
    internal void Reset(){initialized=armed=spent=neutral=wasGrip=false;slowTime=0;Speed=0;fastUntil=0;cooldown=0;Peak=0;peakTime=0;}
    // 0.1.154: a long stick is timed by its end (0.1.152), and held
    // in one hand its end never stood still - the hand's slightest tremor, a
    // metre further out, moved it faster than "still" (0.45 m/s), so the swing
    // was never armed. steady: the point that tells whether the player holds
    // still (the hand); the speed of a blow is still the end's. A fast end also
    // moves further in a frame (up to a metre, not 0.4 m).
    internal float SteadySpeed {get;private set;}
    // 0.1.200: a held thing's part farthest from the hand (its frame), if at
    // least FarEnd away (a chair's far legs; a bottle is timed by the hand).
    internal const float FarEnd=.25f;
    internal static int FarthestPart(System.Collections.Generic.IReadOnlyList<Vector3> offsets,int count,Vector3 hand)
    {
        int best=-1;float bestDistance=FarEnd;
        if(offsets==null||!NativeHandMesh.Finite(hand))return -1;
        for(int i=0;i<count&&i<offsets.Count;i++)
        {
            var o=offsets[i];if(!NativeHandMesh.Finite(o))continue;
            float d=Vector3.Distance(o,hand);if(d>bestDistance){bestDistance=d;best=i;}
        }
        return best;
    }
    internal const float MaxStep=.4f,MaxEndStep=1f;
    // 0.1.206: how far off a touch a swing rearms, and how little of that may
    // go on along the touch's stroke (cos 60 degrees).
    internal const float RearmAway=.20f,RearmOnward=.5f;
    internal bool Sample(Vector3 physical,float now,bool grip,bool allowed)=>Sample(physical,now,grip,allowed,null);
    internal bool Sample(Vector3 physical,float now,bool grip,bool allowed,Vector3? steady)
    {
        if(!allowed||!NativeHandMesh.Finite(physical)||!float.IsFinite(now)||steady is Vector3 st0&&!NativeHandMesh.Finite(st0)){Reset();return false;}
        if(!grip)neutral=true;
        var hold=steady??physical;
        if(!initialized){previous=physical;previousSteady=hold;previousTime=now;initialized=true;wasGrip=grip;return false;}
        float dt=now-previousTime;var delta=physical-previous;var steadyDelta=hold-previousSteady;
        if(dt<.001f)return false;
        previous=physical;previousSteady=hold;previousTime=now;
        if(dt>.1f||delta.Length()>(steady!=null?MaxEndStep:MaxStep)||steadyDelta.Length()>MaxStep){Reset();return false;}
        Speed=delta.Length()/dt;SteadySpeed=steadyDelta.Length()/dt;
        float still=steady!=null?SteadySpeed:Speed;
        // Highest speed of the last ~150 ms.
        if(Speed>=Peak||now-peakTime>.15f){Peak=Speed;peakTime=now;}
        bool pressed=grip&&!wasGrip;wasGrip=grip;
        if(!grip){armed=spent=false;slowTime=0;fastUntil=0;return false;}
        // Entering fists/menu with grip already held must still arm after a
        // brief stationary wind-up; holding it at selection is normal in VR.
        if(!neutral){if(still<.45f)slowTime+=dt;else slowTime=0;
            if(slowTime<.05f)return false;neutral=true;armed=true;start=physical;slowTime=0;}
        // Closing the grip starts a wind-up without requiring a 70 ms pause.
        if(pressed&&now>=cooldown){armed=true;spent=false;start=physical-delta;fastUntil=0;}
        if(spent)
        {
            // A jab can rearm by pulling back, without having to hold perfectly
            // still between blows. Retraction itself never counts as impact.
            // 0.1.206:
            // a thing held in the hand never lets go of the grip, so after a
            // touch (a table, the floor, a soft touch) it rearmed only by
            // pulling back along that touch's stroke - often an old one, or
            // none: swung along another line it never rearmed again. Carried
            // well off the touch (RearmAway) on another line (not within 60
            // degrees of going on along the stroke), it rearms too.
            var away=physical-contactPosition;float back=Vector3.Dot(away,contactDirection),off=away.Length();
            if(now>=cooldown&&(back<-.065f||off>RearmAway&&back<off*RearmOnward))
            {armed=true;spent=false;start=physical;fastUntil=0;slowTime=0;}
            return false;
        }
        if(still<.45f)
        {
            slowTime+=dt;
            if(slowTime>.05f&&now>=cooldown){armed=true;start=physical;fastUntil=0;}
        }
        else slowTime=0;
        if(armed&&Speed>1.5f){fastUntil=now+.12f;lastDirection=Vector3.Normalize(delta);}
        // Preserve a short earned strike window during natural deceleration at
        // contact. Slow touching without a recent real swing still cannot hit.
        return armed&&now>=cooldown&&now<=fastUntil&&Speed>.25f
            &&Vector3.Dot(delta,lastDirection)>0&&Vector3.Distance(start,physical)>.045f;
    }
    internal void Contact(float now)
    {spent=true;armed=false;cooldown=now+.20f;contactPosition=previous;contactDirection=lastDirection;fastUntil=0;}
}
internal static class HandImpact
{
    private static readonly Vector3[] direction=new Vector3[2];
    private static readonly float[] started={-100,-100};
    internal static void Hit(bool right,Vector3 localTravel,float now)
    {
        int i=right?1:0;
        if(!NativeHandMesh.Finite(localTravel)||localTravel.LengthSquared()<1e-8f)return;
        direction[i]=-Vector3.Normalize(localTravel);started[i]=now;
    }
    internal static Vector3 Offset(bool right,float now)
    {
        int i=right?1:0;float t=(now-started[i])/.10f;
        return t>0&&t<1?direction[i]*(.024f*MathF.Sin(MathF.PI*t)):Vector3.Zero;
    }
    internal static void Clear(bool right){started[right?1:0]=-100;}
}
