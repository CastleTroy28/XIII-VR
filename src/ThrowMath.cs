using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.135: a weapon let go from a hand: how fast the hand was turning (it
// keeps turning in the air) and how it bounces off what it lands on.
internal static class ThrowMath
{
    internal const float Restitution=.3f,Friction=.4f,MinBounceSpeed=1.2f,SettleSpeed=.5f,MaxSpin=25;
    internal const int MaxBounces=4;
    // The turn from the last hand orientation to this one (forward and up
    // axes, world) as an angular velocity (world axis times rad/s).
    internal static Vector3 Spin(Vector3 forward,Vector3 up,Vector3 lastForward,Vector3 lastUp,float dt)
    {
        if(!(dt>1e-4f)||!Frame(forward,up,out var x,out var y,out var z)||!Frame(lastForward,lastUp,out var lx,out var ly,out var lz))return Vector3.Zero;
        // Rotation R*R'^T: its trace gives the angle, its skew part the axis.
        float trace=Vector3.Dot(x,lx)+Vector3.Dot(y,ly)+Vector3.Dot(z,lz);
        var skew=Vector3.Cross(lx,x)+Vector3.Cross(ly,y)+Vector3.Cross(lz,z);
        float sin=skew.Length()*.5f,cos=Math.Clamp((trace-1)*.5f,-1,1);
        if(sin<1e-6f)return Vector3.Zero;
        float angle=MathF.Atan2(sin,cos);
        var w=Vector3.Normalize(skew)*(angle/dt);
        return float.IsFinite(w.X)&&float.IsFinite(w.Y)&&float.IsFinite(w.Z)?w:Vector3.Zero;
    }
    private static bool Frame(Vector3 forward,Vector3 up,out Vector3 x,out Vector3 y,out Vector3 z)
    {
        x=y=z=Vector3.Zero;
        if(forward.LengthSquared()<1e-10f||up.LengthSquared()<1e-10f)return false;
        z=Vector3.Normalize(forward);var r=Vector3.Cross(up,z);if(r.LengthSquared()<1e-10f)return false;
        x=Vector3.Normalize(r);y=Vector3.Cross(z,x);return true;
    }
    // Landing on a surface with normal n: bounced (off it, slower, the turn
    // partly kept and partly taken from the slide) or settled (too slow, or
    // bounced enough).
    internal static (bool bounced,Vector3 velocity,Vector3 spin) Bounce(Vector3 velocity,Vector3 spin,Vector3 n,int bounces)
    {
        if(n.LengthSquared()<1e-8f)n=Vector3.UnitY;n=Vector3.Normalize(n);
        float vn=Vector3.Dot(velocity,n);var vt=velocity-n*vn;
        if(vn>=0)return (true,velocity,spin);   // already leaving the surface
        if(bounces>=MaxBounces||-vn<MinBounceSpeed&&vt.Length()<MinBounceSpeed)return (false,Vector3.Zero,Vector3.Zero);
        var v=vt*(1-Friction)-n*(vn*Restitution);
        var w=spin*.5f+Vector3.Cross(n,vt)*2f;
        if(w.Length()>MaxSpin)w=Vector3.Normalize(w)*MaxSpin;
        if(v.Length()<SettleSpeed)return (false,Vector3.Zero,Vector3.Zero);
        return (true,v,w);
    }
}
