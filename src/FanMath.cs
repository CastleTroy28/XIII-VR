using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.151: the geometry of a ceiling fan for a hand: the disc its blades
// sweep (around the axis it turns about, between the heights of its parts),
// where the blades are around it (when the mesh can be read), and how fast
// a player hanging on it is carried round.
internal static class FanMath
{
    internal const int Bins=72;
    // A hand this far past the blade tips, above or below the blades, still takes one.
    internal const float TipReach=.10f,Above=.10f,Below=.14f;
    // The hub (and the motor) are not a blade.
    internal const float HubShare=.22f,MinHub=.10f;
    // Carried round at most this fast (degrees a second): the fan slows down with the player on it.
    internal const float MaxRide=120f;
    // Radial distance and height of a point (relative to the fan's centre) along its axis.
    internal static void Split(Vector3 rel,Vector3 axis,out float radial,out float height)
    {
        axis=axis.LengthSquared()>1e-10f?Vector3.Normalize(axis):Vector3.UnitY;
        height=Vector3.Dot(rel,axis);radial=(rel-axis*height).Length();
    }
    // Within the swept disc (not at the hub).
    internal static bool InDisc(float radial,float height,float radius,float low,float high)
    {
        if(!float.IsFinite(radial)||!float.IsFinite(height)||!(radius>0))return false;
        float hub=Math.Max(MinHub,radius*HubShare);
        return radial>=hub&&radial<=radius+TipReach&&height>=low-Below&&height<=high+Above;
    }
    // Angle (degrees, 0..360) of a point around an axis, in a frame turning with the fan.
    internal static float Angle(Vector3 local,Vector3 axis)
    {
        axis=axis.LengthSquared()>1e-10f?Vector3.Normalize(axis):Vector3.UnitY;
        var u=Math.Abs(axis.X)<.9f?Vector3.Normalize(Vector3.Cross(axis,Vector3.UnitX)):Vector3.Normalize(Vector3.Cross(axis,Vector3.UnitY));
        var v=Vector3.Cross(axis,u);
        float a=MathF.Atan2(Vector3.Dot(local,v),Vector3.Dot(local,u))*180/MathF.PI;
        return a<0?a+360:a;
    }
    internal static int Bin(float angle){int b=(int)MathF.Floor(angle/(360f/Bins));return ((b%Bins)+Bins)%Bins;}
    // Where the blades are: bins with mesh points out on the blades (past the hub).
    internal static bool[]? Blades(IEnumerable<Vector3> localPoints,Vector3 axis,float radius)
    {
        var bins=new int[Bins];int total=0;
        foreach(var p in localPoints)
        {
            Split(p,axis,out float r,out _);if(!(r>radius*.45f))continue;
            bins[Bin(Angle(p,axis))]++;total++;
        }
        if(total<12)return null;
        var blades=new bool[Bins];int filled=0;
        for(int i=0;i<Bins;i++)if(bins[i]>0){blades[i]=true;filled++;}
        // A round thing (a lamp, a disc) is not bladed: every direction taken.
        return filled>=Bins-2?null:blades;
    }
    // On a blade: its bin or one within `slack` bins (5 degrees each) of it.
    internal static bool OnBlade(bool[]? blades,float angle,int slack=2)
    {
        if(blades==null)return true;
        int b=Bin(angle);
        for(int d=-slack;d<=slack;d++)if(blades[((b+d)%Bins+Bins)%Bins])return true;
        return false;
    }
    internal static int Count(bool[]? blades)
    {
        if(blades==null)return 0;int n=0;
        for(int i=0;i<Bins;i++)if(blades[i]&&!blades[(i+Bins-1)%Bins])n++;
        return n;
    }
    // The speed a hanging player is carried round (sign: the fan's direction).
    internal static float Ride(float native)=>!float.IsFinite(native)?0:Math.Clamp(native,-MaxRide,MaxRide);
    // 0.1.154: where a hand's ray first
    // meets a blade within `reach` metres. center: the fan's centre; turn: its
    // rotation (the axis and blades are in its own frame); t: along the ray.
    internal const float PointReach=4.5f;const int RaySteps=40;
    internal static bool RayHit(Vector3 origin,Vector3 direction,Vector3 center,Quaternion turn,Vector3 localAxis,float radius,float low,float high,bool[]? blades,float reach,out float t)
    {
        t=float.NaN;
        if(direction.LengthSquared()<1e-10f||!float.IsFinite(reach)||reach<=0)return false;
        direction=Vector3.Normalize(direction);localAxis=localAxis.LengthSquared()>1e-10f?Vector3.Normalize(localAxis):Vector3.UnitY;
        var axis=Vector3.Transform(localAxis,turn);
        // The slab of heights the blades sweep, along the ray.
        float h0=Vector3.Dot(origin-center,axis),dh=Vector3.Dot(direction,axis);
        float lowH=low-Below,highH=high+Above,from=0,to=reach;
        if(Math.Abs(dh)<1e-5f){if(h0<lowH||h0>highH)return false;}
        else
        {
            float a=(lowH-h0)/dh,b=(highH-h0)/dh;if(a>b)(a,b)=(b,a);
            from=Math.Max(from,a);to=Math.Min(to,b);
        }
        if(!(to>=from))return false;
        var inverse=Quaternion.Inverse(turn);
        for(int i=0;i<=RaySteps;i++)
        {
            float s=from+(to-from)*i/RaySteps;var rel=origin+direction*s-center;
            Split(rel,axis,out float r,out float h);
            if(!InDisc(r,h,radius,low,high))continue;
            if(!OnBlade(blades,Angle(Vector3.Transform(rel,inverse),localAxis)))continue;
            t=s;return true;
        }
        return false;
    }
    // Carried to a blade taken from afar (m/s): the error closed at most this fast, however far.
    internal const float PullSpeed=4.5f,Arrive=.25f,PullGiveUp=3f;
    internal static Vector3 Pull(Vector3 error,float dt,float speed=PullSpeed)
    {
        if(!(dt>0)||dt>.1f||!float.IsFinite(error.X+error.Y+error.Z))return Vector3.Zero;
        var v=error/dt;float n=v.Length();return n>speed?v/n*speed:v;
    }
    // The fan's own speed with the player on it: still while he is carried to
    // it, then up to the ride speed in half a second.
    internal static float RideRamp(float current,float target,bool pulling,float dt)
    {
        if(pulling||!float.IsFinite(target))return 0;
        if(!float.IsFinite(current))current=0;dt=Math.Clamp(float.IsFinite(dt)?dt:0,0,.1f);
        float step=Math.Max(Math.Abs(target),60)*2*dt;
        return Math.Abs(target-current)<=step?target:current+Math.Sign(target-current)*step;
    }
    // A fan: turns about an axis near the vertical, blades 0.25-2 m long.
    internal static bool CeilingFan(Vector3 axis,float radius,float degreesPerSecond)
    {
        if(!(radius>=.25f&&radius<=2f))return false;
        axis=axis.LengthSquared()>1e-10f?Vector3.Normalize(axis):Vector3.Zero;
        return Math.Abs(axis.Y)>=MathF.Cos(35*MathF.PI/180)&&float.IsFinite(degreesPerSecond)&&Math.Abs(degreesPerSecond)>=5;
    }
}
