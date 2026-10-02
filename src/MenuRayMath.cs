using System;
using System.Numerics;
namespace XiiiXR;
internal static class MenuRayMath
{
    internal static bool Plane(Vector3 origin,Vector3 direction,Vector3 point,Vector3 normal,out Vector3 hit,out float distance)
    {
        hit=default;distance=0;float denominator=Vector3.Dot(direction,normal);
        if(!float.IsFinite(denominator)||Math.Abs(denominator)<.0001f)return false;
        distance=Vector3.Dot(point-origin,normal)/denominator;
        if(!float.IsFinite(distance)||distance<.015f||distance>8)return false;
        hit=origin+direction*distance;return true;
    }
}
