using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.214: where a thrown thing lands - the point marked while it is ready
// to be thrown. Its flight is the launched rigidbody's: drag-free under
// gravity, or straight (a knife the game flies without gravity).
internal static class ThrowLandingMath
{
    // Flight pieces cast for a hit (s), how long a flight is followed (s),
    // how far a straight one (m).
    internal const float Step=.05f,MaxTime=2.5f,MaxStraight=60f;
    // The first solid thing between two points of the flight.
    internal delegate bool Cast(Vector3 from,Vector3 to,out Vector3 point,out Vector3 normal);
    internal static Vector3 At(Vector3 origin,Vector3 velocity,Vector3 gravity,float t)=>origin+velocity*t+gravity*(.5f*t*t);
    internal static bool Land(Vector3 origin,Vector3 velocity,Vector3 gravity,Cast cast,out Vector3 point,out Vector3 normal,out float time)
    {
        point=normal=Vector3.Zero;time=0;
        if(cast==null||!Finite(origin)||!Finite(velocity)||!Finite(gravity))return false;
        float speed=velocity.Length();if(!(speed>1e-3f))return false;
        if(gravity.LengthSquared()<1e-6f)
        {
            if(!cast(origin,origin+velocity/speed*MaxStraight,out point,out normal))return false;
            time=Vector3.Distance(origin,point)/speed;return true;
        }
        var previous=origin;
        for(int i=1;i*Step<=MaxTime+1e-4f;i++)
        {
            float t=i*Step;var next=At(origin,velocity,gravity,t);
            if(cast(previous,next,out point,out normal))
            {
                float piece=Vector3.Distance(previous,next);
                time=t-Step+(piece>1e-6f?Math.Clamp(Vector3.Distance(previous,point)/piece,0,1)*Step:0);
                return true;
            }
            previous=next;
        }
        return false;
    }
    // The mark's radius (m): larger further off, so it stays visible.
    internal static float Radius(float distance)=>float.IsFinite(distance)?Math.Clamp(.06f+.01f*distance,.08f,.30f):.08f;
    // A swing counts when it goes forward of the look (not drawn back, not behind).
    internal static bool Forward(Vector3 velocity,Vector3 look)=>velocity.X*look.X+velocity.Z*look.Z>0;
    // The game's knife flight from three of its positions after the launch:
    // its speed and how fast it falls (m/s^2, 0 when it flies straight).
    // False when it stopped, stuck or bounced between them.
    internal static bool Fit(float t0,Vector3 p0,float t1,Vector3 p1,float t2,Vector3 p2,out float speed,out float drop)
    {
        speed=drop=0;
        float a=t1-t0,b=t2-t1;
        if(!(a>.01f&&b>.01f)||!Finite(p0)||!Finite(p1)||!Finite(p2))return false;
        var v1=(p1-p0)/a;var v2=(p2-p1)/b;
        float s1=v1.Length(),s2=v2.Length();
        if(!(s1>3f)||!float.IsFinite(s2)||Math.Abs(s2-s1)>s1*.35f)return false;
        float d=-(v2.Y-v1.Y)/((a+b)*.5f);
        if(!float.IsFinite(d))return false;
        speed=s1;drop=Math.Clamp(d,0,40);
        return true;
    }
    // Below this the measured fall is taken as a straight flight (m/s^2).
    internal const float StraightBelow=2f;
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
