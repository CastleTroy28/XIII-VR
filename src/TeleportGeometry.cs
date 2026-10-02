using System;
namespace XiiiXR;
internal static class TeleportGeometry
{
    internal static bool Landing(float normalY,float slopeLimit,float distance,float height)
        =>float.IsFinite(normalY)&&float.IsFinite(slopeLimit)&&float.IsFinite(distance)&&float.IsFinite(height)
        &&normalY>=MathF.Cos(Math.Clamp(slopeLimit,0,45)*MathF.PI/180)
        &&distance>=.15f&&distance<=4.5f&&height>=-.6f&&height<=.35f;
}
