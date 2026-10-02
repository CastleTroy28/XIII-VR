using System;
namespace XiiiXR;
// 0.1.194. The game launches a crossbow bolt as a physics body under
// gravity (and air drag) straight at the aimed point, so it drops below it
// the farther the target is. The launch velocity is corrected so the bolt
// reaches the aimed point at the moment it would have without gravity/drag.
internal static class BallisticMath
{
    internal const float MaxFlight=3f,MaxBoost=1.6f;
    // (dx,dy,dz): launch point to aimed point; speed: the game's launch speed;
    // gravity: world gravity (y up); drag: the rigidbody's linear drag.
    // Returns the launch velocity that hits the point at t=distance/speed, or
    // null when it should stay the game's own (bad input, too slow, too far).
    internal static (float x,float y,float z)? Launch(float dx,float dy,float dz,float speed,float gx,float gy,float gz,float drag)
    {
        float d=MathF.Sqrt(dx*dx+dy*dy+dz*dz);
        if(!float.IsFinite(d)||!float.IsFinite(speed)||!float.IsFinite(gx+gy+gz)||!float.IsFinite(drag))return null;
        if(d<.5f||speed<1)return null;
        float t=d/speed;if(t>MaxFlight)return null;
        float k=Math.Max(0,drag);
        (float x,float y,float z) v;
        if(k<1e-4f)v=(dx/t-.5f*gx*t,dy/t-.5f*gy*t,dz/t-.5f*gz*t);
        else
        {
            // p(t)=g t/k+(v0-g/k)(1-e^-kt)/k  ->  v0=g/k+(D-g t/k) k/(1-e^-kt)
            float f=k/(1-MathF.Exp(-k*t));if(!float.IsFinite(f))return null;
            v=(gx/k+(dx-gx*t/k)*f,gy/k+(dy-gy*t/k)*f,gz/k+(dz-gz*t/k)*f);
        }
        float s=MathF.Sqrt(v.x*v.x+v.y*v.y+v.z*v.z);
        if(!float.IsFinite(s)||s>speed*MaxBoost)return null;
        return v;
    }
    // Where a shot launched with velocity v lands after t seconds (for tests/logs).
    internal static (float x,float y,float z) At(float vx,float vy,float vz,float gx,float gy,float gz,float drag,float t)
    {
        float k=Math.Max(0,drag);
        if(k<1e-4f)return(vx*t+.5f*gx*t*t,vy*t+.5f*gy*t*t,vz*t+.5f*gz*t*t);
        float e=(1-MathF.Exp(-k*t))/k;
        return(gx*t/k+(vx-gx/k)*e,gy*t/k+(vy-gy/k)*e,gz*t/k+(vz-gz/k)*e);
    }
}
