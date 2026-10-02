using System;
using System.Numerics;
namespace XiiiXR;
internal static class HeadingMath
{
    internal static float Yaw(Quaternion rotation,float fallback)
    {
        if (!float.IsFinite(rotation.X) || !float.IsFinite(rotation.Y) || !float.IsFinite(rotation.Z) || !float.IsFinite(rotation.W) || rotation.LengthSquared() < .5f) return fallback;
        var f = Vector3.Transform(Vector3.UnitZ,Quaternion.Normalize(rotation));
        return f.X*f.X + f.Z*f.Z < .0001f ? fallback : MathF.Atan2(f.X,f.Z);
    }
    internal static Vector2 Rotate(Vector2 stick,float relativeYaw)
    {
        float c = MathF.Cos(relativeYaw), s = MathF.Sin(relativeYaw);
        return new Vector2(c*stick.X+s*stick.Y,-s*stick.X+c*stick.Y);
    }
    internal static Vector2 InBodyFrame(Vector2 stick,Quaternion worldHead,Quaternion worldBody,ref float lastWorldHeading)
    {
        lastWorldHeading = Yaw(worldHead,lastWorldHeading);
        return Rotate(stick,lastWorldHeading-Yaw(worldBody,0));
    }
}
