using System;
using System.Numerics;
namespace XiiiXR;
internal static class BrawlContactMath
{
    // Closest distance between two finite segments; handles points and parallel segments.
    internal static float DistanceSquared(Vector3 p,Vector3 q,Vector3 a,Vector3 b)
    {
        if(!Finite(p)||!Finite(q)||!Finite(a)||!Finite(b))return float.PositiveInfinity;
        var d=q-p;var e=b-a;var r=p-a;
        float x=Vector3.Dot(d,d),y=Vector3.Dot(e,e),z=Vector3.Dot(e,r),s,t;
        if(x<1e-10f&&y<1e-10f)return r.LengthSquared();
        if(x<1e-10f){s=0;t=Math.Clamp(z/y,0,1);}
        else
        {
            float c=Vector3.Dot(d,r);
            if(y<1e-10f){t=0;s=Math.Clamp(-c/x,0,1);}
            else
            {
                float k=Vector3.Dot(d,e),den=x*y-k*k;
                s=den>1e-10f?Math.Clamp((k*z-c*y)/den,0,1):0;t=(k*s+z)/y;
                if(t<0){t=0;s=Math.Clamp(-c/x,0,1);}else if(t>1){t=1;s=Math.Clamp((k-c)/x,0,1);}
            }
        }
        return Vector3.DistanceSquared(p+d*s,a+e*t);
    }
    internal static bool Hits(Vector3 from,Vector3 to,Vector3 head)
    {
        if(!Finite(from)||!Finite(to)||!Finite(head)||Vector3.DistanceSquared(from,to)>.8f*.8f)return false;
        // Approximate the tracked head and torso; the current HMD position follows ducking/leaning.
        return DistanceSquared(from,to,head,head)<=.16f*.16f
            ||DistanceSquared(from,to,head-Vector3.UnitY*.25f,head-Vector3.UnitY*.65f)<=.23f*.23f;
    }
    internal static bool Active(float age)=>float.IsFinite(age)&&age>=BrawlMath.WindUp&&age<=BrawlMath.HitDelay+BrawlMath.Hold;
    private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
}
