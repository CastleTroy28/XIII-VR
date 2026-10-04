using System;
using System.Numerics;
using Valve.VR;
namespace XiiiXR;
internal readonly struct PoseValue
{
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public PoseValue(Vector3 p, Quaternion q) { Position = p; Rotation = q; }
}
internal static class PoseMath
{
    // OpenVR uses a right-handed basis with forward -Z. Unity uses forward +Z.
    // Reflect Z on both sides; System.Numerics matrices use row-vector layout.
    public static PoseValue FromOpenVR(HmdMatrix34_t m)
    {
        var rotation = new Matrix4x4(
            m.m0, m.m4, -m.m8, 0,
            m.m1, m.m5, -m.m9, 0,
            -m.m2, -m.m6, m.m10, 0,
            0, 0, 0, 1);
        return new PoseValue(new Vector3(m.m3, m.m7, -m.m11), Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotation)));
    }
    public static Quaternion Yaw(Quaternion rotation)
    {
        Vector3 forward = Vector3.Transform(Vector3.UnitZ, rotation);
        float angle = forward.X * forward.X + forward.Z * forward.Z < 0.0001f ? 0 : MathF.Atan2(forward.X, forward.Z);
        return Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle);
    }
    public static PoseValue Relative(PoseValue current, PoseValue reference)
    {
        Quaternion inverseYaw = Quaternion.Inverse(Yaw(reference.Rotation));
        return new PoseValue(Vector3.Transform(current.Position - reference.Position, inverseYaw), Quaternion.Normalize(inverseYaw * current.Rotation));
    }
    // 0.1.233: an eye's offset from the head for a world scale: the eye
    // distance divided by it (0.9: the eyes 11% further apart, the world seen
    // 10% smaller). Players on Quest 3 found the world too big.
    public static PoseValue ScaledEye(PoseValue eye, float worldScale)
    {
        float s = float.IsFinite(worldScale) && worldScale >= .25f && worldScale <= 4f ? worldScale : 1f;
        return new PoseValue(eye.Position / s, eye.Rotation);
    }
    // 0.1.235: how far apart the eyes are drawn for a world scale (1 / it, 0.25-4; 1 for a bad one).
    public static float ViewScale(float worldScale) => float.IsFinite(worldScale) && worldScale >= .25f && worldScale <= 4f ? 1f / worldScale : 1f;
    public static bool Valid(PoseValue value)
    {
        var p = value.Position; var q = value.Rotation;
        return float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z) && p.LengthSquared() < 1000000
            && float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W) && q.LengthSquared() > 0.5f;
    }
}
