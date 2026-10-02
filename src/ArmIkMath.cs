using System;
using System.Numerics;
namespace XiiiXR;
internal static class ArmIkMath
{
    internal static Vector3 Elbow(Vector3 shoulder,Vector3 wrist,Vector3 pole,float forearm)
    {
        forearm=Math.Clamp(forearm,.16f,.38f);const float upper=.30f;
        var delta=shoulder-wrist;float distance=delta.Length();
        var toward=distance>.0001f?delta/distance:Vector3.UnitY;
        var side=pole-toward*Vector3.Dot(pole,toward);
        if(side.LengthSquared()<1e-6f)side=Vector3.Cross(toward,Math.Abs(toward.Y)<.9f?Vector3.UnitY:Vector3.UnitX);
        side=Vector3.Normalize(side);
        float d=Math.Clamp(distance,Math.Abs(upper-forearm)+.001f,upper+forearm-.001f);
        float along=Math.Clamp((forearm*forearm+d*d-upper*upper)/(2*d),-forearm,forearm);
        return wrist+toward*along+side*MathF.Sqrt(Math.Max(0,forearm*forearm-along*along));
    }
    internal static Quaternion ForearmRotation(Vector3 restElbow,Vector3 desiredElbow,float z)
    {
        return Quaternion.Slerp(Quaternion.Identity,ForearmSwing(restElbow,desiredElbow),ForearmWeight(z));
    }
    internal static float ForearmWeight(float z)
    {
        // A forearm is one straight segment. Blend only the wrist seam, then
        // apply ONE rotation to the entire forearm, including the wristband.
        float t=Math.Clamp(-z/.012f,0,1);return t*t*(3-2*t);
    }
    internal static Quaternion ForearmSwing(Vector3 restElbow,Vector3 desiredElbow)
    {
        if(restElbow.LengthSquared()<1e-8f||desiredElbow.LengthSquared()<1e-8f)return Quaternion.Identity;
        var a=Vector3.Normalize(restElbow);var b=Vector3.Normalize(desiredElbow);
        // Limit the anatomical target relative to the hand, not relative to
        // the game's bent bind pose (which used to preserve the original bend).
        float targetAngle=MathF.Acos(Math.Clamp(-b.Z,-1,1));
        const float limit=35*MathF.PI/180;
        if(targetAngle>limit)
        {
            var side=new Vector3(b.X,b.Y,0);
            side=side.LengthSquared()>1e-8f?Vector3.Normalize(side):Vector3.UnitY;
            b=side*MathF.Sin(limit)-Vector3.UnitZ*MathF.Cos(limit);
        }
        float dot=Vector3.Dot(a,b);
        if(!float.IsFinite(dot)||dot>.99999f)return Quaternion.Identity;
        var axis=Vector3.Cross(a,b);
        if(axis.LengthSquared()<1e-8f)axis=Vector3.Cross(a,Math.Abs(a.Y)<.9f?Vector3.UnitY:Vector3.UnitX);
        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis),MathF.Acos(Math.Clamp(dot,-1,1)));
    }
}
