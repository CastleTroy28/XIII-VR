using System;
using System.Numerics;
namespace XiiiXR;
internal static class ViewCoverage
{
    // Union of both canted eyes projected onto one head-local effect plane.
    internal static Vector4 Bounds(EyeFrustum left,PoseValue eyeLeft,EyeFrustum right,PoseValue eyeRight,float depth)
    {
        float minX=-depth,maxX=depth,minY=-depth,maxY=depth;
        void Eye(EyeFrustum f,PoseValue pose)
        {
            if(!f.Valid||!PoseMath.Valid(pose))return;
            // 0.1.182: the eye's
            // Top is the tangent of the angle DOWN and Bottom of the angle up
            // (OpenVR's raw values as ProjectionMath uses them). They were taken
            // the other way round, which only shows on headsets that see further
            // down than up (Quest); Pimax is symmetric.
            foreach(float x in new[]{f.Left,f.Right})foreach(float y in new[]{f.Top,f.Bottom})
            {
                var d=Vector3.Transform(new Vector3(x,y,1),pose.Rotation);
                float t=(depth-pose.Position.Z)/Math.Max(.02f,d.Z);
                var p=pose.Position+d*t;
                minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);
            }
        }
        Eye(left,eyeLeft);Eye(right,eyeRight);
        float mx=(maxX-minX)*.08f,my=(maxY-minY)*.08f;
        return new Vector4(minX-mx,maxX+mx,minY-my,maxY+my);
    }
    internal static byte HealingAlpha(float x,float y)
    {
        float radius=Math.Clamp(MathF.Sqrt(x*x+y*y),0,1);
        return (byte)MathF.Round((.10f+.55f*radius*radius)*255);
    }
}
