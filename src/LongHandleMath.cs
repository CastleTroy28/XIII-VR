using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.143: a hand on a long stick (broom, mop, shovel): how far the middle of
// the closed hand's grip is from the stick's line.
internal static class LongHandleMath
{
    internal const float MaxMiss=.025f,StickThickness=.032f;
    internal static float Miss(Vector3 grip,Vector3 origin,Vector3 axis)
    {
        if(axis.LengthSquared()<1e-8f||!float.IsFinite(axis.LengthSquared()))return float.PositiveInfinity;
        axis=Vector3.Normalize(axis);var d=grip-origin;
        var off=d-axis*Vector3.Dot(d,axis);float m=off.Length();
        return float.IsFinite(m)?m:float.PositiveInfinity;
    }
}
