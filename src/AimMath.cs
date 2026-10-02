using System;
using System.Numerics;
namespace XiiiXR;
internal static class AimMath
{
    internal static bool Valid(Quaternion q) => float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W) && float.IsFinite(q.LengthSquared()) && q.LengthSquared() > .25f;
    internal static Quaternion Pitch(float degrees) => Quaternion.CreateFromAxisAngle(Vector3.UnitX,Math.Clamp(float.IsFinite(degrees)?degrees:45,-90,90)*MathF.PI/180);
    internal static Quaternion Calibrate(Quaternion rawWorld,Quaternion headWorld)
    {
        if (!Valid(rawWorld) || !Valid(headWorld)) throw new InvalidOperationException("Invalid calibration pose");
        var forward = Vector3.Transform(Vector3.UnitZ,headWorld);
        if (forward.X*forward.X+forward.Z*forward.Z < .1f) throw new InvalidOperationException("Look horizontally forward while calibrating");
        var desired = Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(forward.X,forward.Z));
        return Quaternion.Normalize(Quaternion.Inverse(Quaternion.Normalize(rawWorld))*desired);
    }
    internal static Quaternion Apply(Quaternion raw,Quaternion correction) => Quaternion.Normalize(raw*correction);
    // 0.1.157:
    // an aim calibration was taken with the controller not held forward (it
    // came out 152 degrees off the usual grip) and kept. A calibration is a
    // forward grip only within 60 degrees of the factory one.
    internal const float MaxCalibrationOff=60;
    internal static float AngleDegrees(Quaternion a,Quaternion b)
    {
        if(!Valid(a)||!Valid(b))return 180;
        float d=Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)));
        return 2*MathF.Acos(Math.Clamp(d,0,1))*180/MathF.PI;
    }
    internal static float CalibrationOff(Quaternion q)=>AngleDegrees(q,Pitch(45));
    internal static bool PlausibleCalibration(Quaternion q)=>Valid(q)&&CalibrationOff(q)<=MaxCalibrationOff;
}
internal sealed class PistolSupport
{
    internal bool Held { get; private set; }
    private Quaternion filtered;
    internal void Release() { Held=false; }
    // 0.1.174: the two controllers
    // touch before the support hand's tracked point reaches the grip point.
    // It now takes hold within 24 cm of that point or of the gun hand itself,
    // and a grip pressed up to half a second before arriving counts.
    internal const float Reach=.24f,PressGrace=.5f;
    private float sincePress=float.PositiveInfinity;
    internal Quaternion Solve(Vector3 primary,Quaternion aim,Vector3 support,Vector3 socket,bool valid,bool down,bool held,float radius,float deltaTime)
    {
        float distance=Vector3.Distance(primary,support);
        bool usable=valid && float.IsFinite(distance) && distance<=.5f;
        if (!held || !usable) Held=false;
        float step=float.IsFinite(deltaTime)?Math.Clamp(deltaTime,0,.1f):0;
        if(down)sincePress=0;else if(held)sincePress+=step;else sincePress=float.PositiveInfinity;
        float reach=Math.Max(Math.Clamp(float.IsFinite(radius)?radius:.18f,.05f,.3f),Reach);
        bool near=Vector3.Distance(support,socket)<=reach||distance<=reach;
        bool justGrabbed=false;
        if (!Held && usable && (down||held&&sincePress<=PressGrace) && near)
        { Held=true; justGrabbed=true; filtered=aim; }
        if (!Held) { filtered=aim; return aim; }
        if (justGrabbed) return aim;
        // Support position never determines pistol yaw/pitch/roll. Only the
        // primary orientation is filtered; large deliberate turns stay responsive.
        float dot=Math.Clamp(MathF.Abs(Quaternion.Dot(filtered,aim)),0,1);
        float angle=2*MathF.Acos(dot)*180/MathF.PI;
        float tau=angle>12?.01f:.045f;
        float dt=float.IsFinite(deltaTime)?Math.Clamp(deltaTime,0,.1f):0;
        filtered=Quaternion.Normalize(Quaternion.Slerp(filtered,aim,1-MathF.Exp(-dt/tau)));
        return filtered;
    }
}
