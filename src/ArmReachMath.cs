using System;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.146: a two-bone arm (shoulder - elbow - wrist) reaching a point: the
// elbow where both bones keep their lengths and it bends towards the pole.
internal static class ArmReachMath
{
    // s: shoulder, e/w: the elbow and wrist now; returns where they go.
    internal static (N elbow,N wrist) Solve(N s,N e,N w,N target,N pole)
    {
        float a=N.Distance(s,e),b=N.Distance(e,w);
        if(!(a>1e-4f)||!(b>1e-4f))return(e,w);
        var toT=target-s;float len=toT.Length();
        var dir=len>1e-5f?toT/len:N.Normalize(w-s);
        float d=Math.Clamp(len,Math.Abs(a-b)+.01f,a+b-.002f);
        float x=(a*a-b*b+d*d)/(2*d);float h=MathF.Sqrt(Math.Max(0,a*a-x*x));
        var pd=pole-s;pd-=dir*N.Dot(pd,dir);
        if(pd.LengthSquared()<1e-8f){pd=-N.UnitY-dir*N.Dot(-N.UnitY,dir);if(pd.LengthSquared()<1e-8f)pd=N.UnitX;}
        pd=N.Normalize(pd);
        return(s+dir*x+pd*h,s+dir*d);
    }
}
