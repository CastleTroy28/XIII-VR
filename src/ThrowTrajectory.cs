using System;
using System.Numerics;
namespace XiiiXR;
internal static class ThrowTrajectory
{
    internal static float Speed(float native)=>float.IsFinite(native)&&native>0?Math.Clamp(native,6,18):9;
    // Same semi-implicit gravity step as a drag-free dynamic Unity rigidbody.
    internal static Vector3 Step(ref Vector3 position,ref Vector3 velocity,Vector3 gravity,float dt)
    {velocity+=gravity*dt;position+=velocity*dt;return position;}
}
