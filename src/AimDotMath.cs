using System;
namespace XiiiXR;
// 0.1.248: the aim dot's sizes and modes (AimDot).
internal static class AimDotMath
{
    // The dot covers this much of the view at any distance (degrees across).
    internal const float DotDegrees=.35f,MinDot=.004f,MaxDot=.6f;
    // Nothing hit: the laser fades out this far; no dot.
    internal const float LaserMiss=25f,Range=300f;
    internal const float LaserWidth=.0016f,SurfaceGap=.01f;
    internal static float DotSize(float distance)=>float.IsFinite(distance)&&distance>0?Math.Clamp(2*distance*MathF.Tan(DotDegrees*.5f*MathF.PI/180),MinDot,MaxDot):MinDot;
    // 0 off, 1 the dot, 2 the dot and the laser.
    internal static int Mode(int value)=>Math.Clamp(value,0,2);
}
