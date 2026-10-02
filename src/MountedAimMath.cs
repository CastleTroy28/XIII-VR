using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.119: how far the mounted gun turns to point where the controllers
// point. Yaw about the gun's up axis, pitch as the change of elevation, each
// limited by the gun's own clamp (taken symmetric; 0/0 or 180+ = free).
internal static class MountedAimMath
{
    internal static float Limit(float a,float b,float free)
    {
        if(!float.IsFinite(a)||!float.IsFinite(b))return free;
        float m=MathF.Max(MathF.Abs(a),MathF.Abs(b));
        return m<1e-3f?free:MathF.Min(m,180);
    }
    internal static float Yaw(Vector3 forward,Vector3 desired,Vector3 up,float limit)
    {
        if(!Unit(ref up))return float.NaN;
        var f=forward-up*Vector3.Dot(forward,up);var d=desired-up*Vector3.Dot(desired,up);
        if(!Unit(ref f)||!Unit(ref d))return float.NaN;
        float angle=MathF.Atan2(Vector3.Dot(up,Vector3.Cross(f,d)),Vector3.Dot(f,d))*180/MathF.PI;
        return Math.Clamp(angle,-Math.Abs(limit),Math.Abs(limit));
    }
    internal static float Pitch(Vector3 forward,Vector3 desired,Vector3 up,float limit)
    {
        if(!Unit(ref up)||!Unit(ref forward)||!Unit(ref desired))return float.NaN;
        float angle=(MathF.Asin(Math.Clamp(Vector3.Dot(desired,up),-1,1))-MathF.Asin(Math.Clamp(Vector3.Dot(forward,up),-1,1)))*180/MathF.PI;
        return Math.Clamp(angle,-Math.Abs(limit),Math.Abs(limit));
    }
    // 0.1.120: the gun as a lever. The handles are behind the pivot, so the
    // hands moved right turn the barrel left, the hands moved down raise it
    // (like a real mounted machine gun).
    // 0.1.123: turning took too long a reach (a 30 cm lever: 29 cm of hand
    // travel to the side stop). Now linear, limited like the pointing aim.
    // 0.1.124: still too far: one radian per 7 cm (about 8 degrees per
    // centimetre, the 75 degree side stop at 9 cm).
    // 0.1.140: up/down 5 deg per cm (was 8): one radian per 11.5 cm.
    internal const float HandleLength=.115f;
    internal static float Lever(float offset,float length,float limit)
    {
        if(!float.IsFinite(offset)||!float.IsFinite(length)||length<1e-3f)return float.NaN;
        float angle=-offset/length*180/MathF.PI;
        return Math.Clamp(angle,-Math.Abs(limit),Math.Abs(limit));
    }
    // 0.1.138: sideways: with Lever's sign the
    // barrel went the same way as the hands (hands left, barrel left); it
    // must go the other way. So the yaw here has the opposite sign of Lever
    // (offset + = hands right gives + yaw), and it is less sharp: gentle near
    // the centre (about 2.5 deg per cm), faster further out, the 75 deg side
    // stop at 12 cm. Up/down (Lever) was right and stays as it is.
    // 0.1.140: gentler still: about 1.5
    // deg per cm at the centre, the 75 deg side stop at 18 cm.
    internal const float YawNear=150f,YawFar=1481f;
    // 0.1.143: the view now
    // turns with the gun, so the gun turns the way the hands move again
    // (hands left: barrel and view left). The 0.1.136 finding that it was
    // the wrong way round came while the view circled the pivot the other
    // way. Same sign as Lever; the same gentle curve.
    internal static float SideLever(float offset,float limit)
    {
        if(!float.IsFinite(offset))return float.NaN;
        float x=MathF.Abs(offset);float angle=YawNear*x+YawFar*x*x;
        return -MathF.Sign(offset)*Math.Min(angle,Math.Abs(limit));
    }
    // 0.1.123: the hands settle on the handles in the first moment after the
    // gun is taken; their centre is taken then (the gun pointed down when the
    // hands came to rest higher than where they grabbed).
    internal const float Settle=.5f;
    // The hands' move since the gun was taken: sideways in the horizontal
    // frame the player faced then (+ = right), and up (+ = up).
    internal static bool Offset(Vector3 current,Vector3 start,Vector3 facing,out float right,out float up)
    {
        right=up=0;
        var f=new Vector3(facing.X,0,facing.Z);if(!Unit(ref f))return false;
        var r=new Vector3(f.Z,0,-f.X);var d=current-start;
        right=Vector3.Dot(d,r);up=d.Y;
        return float.IsFinite(right)&&float.IsFinite(up);
    }
    // 0.1.138: jitter (the shots shake the controllers) smoothed away while
    // holding still, a quick move followed at once (a One Euro filter).
    internal sealed class Smooth
    {
        internal const float MinCutoff=1.5f,Beta=.02f,DerivativeCutoff=1f;
        private bool has;private float value,rate;
        internal void Reset(){has=false;value=rate=0;}
        internal float Step(float input,float dt)
        {
            if(!float.IsFinite(input))return has?value:input;
            if(!has||!(dt>0)||dt>.25f){has=true;value=input;rate=0;return input;}
            rate+=((input-value)/dt-rate)*Alpha(DerivativeCutoff,dt);
            value+=(input-value)*Alpha(MinCutoff+Beta*MathF.Abs(rate),dt);
            return value;
        }
        private static float Alpha(float cutoff,float dt){float tau=1/(2*MathF.PI*cutoff);return 1/(1+tau/dt);}
    }
    private static bool Unit(ref Vector3 v)
    {
        float l=v.Length();if(!float.IsFinite(l)||l<1e-5f)return false;v/=l;return true;
    }
}
// 0.1.124: the stationary machine gun fires only while both triggers are
// held (like its two thumb-trigger handles).
internal static class MountedTriggers
{
    internal static bool Pressed(HandControls left,HandControls right,int phase)
    {
        if(!left.Valid||!right.Valid)return false;
        const ulong T=HandControls.Trigger;
        bool l=(left.Held&T)!=0,r=(right.Held&T)!=0;
        if(phase==0)return l&&r;
        if(phase==1)return l&&r&&((left.Down|right.Down)&T)!=0;
        return (left.Up&T)!=0&&((right.Held|right.Up)&T)!=0||(right.Up&T)!=0&&((left.Held|left.Up)&T)!=0;
    }
}
