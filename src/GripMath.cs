using System;
using System.Numerics;
namespace XiiiXR;
internal sealed class GripMath
{
    internal bool Held { get; private set; }
    private Quaternion grabOffset;
    internal void Release() { Held = false; }
    // Solve a two-point grip while preserving the aim at the instant of grabbing.
    // This also supports a pistol, whose supporting hand is beside the grip.
    internal Quaternion Solve(Vector3 primary, Quaternion aim, Vector3 support,
        Vector3 socket, bool supportValid, bool gripDown, bool gripHeld, float grabRadius)
    {
        Vector3 line = support - primary;
        float length = line.Length();
        bool valid = supportValid && float.IsFinite(length) && length >= 0.055f && length <= 0.85f;
        if (!gripHeld || !valid) Held = false;
        if (!valid) return aim;
        Quaternion frame = Look(line, Vector3.Transform(Vector3.UnitY, aim));
        if (!Held && gripDown && Vector3.Distance(support, socket) <= grabRadius)
        { Held = true; grabOffset = Quaternion.Normalize(Quaternion.Inverse(frame) * aim); }
        return Held ? Quaternion.Normalize(frame * grabOffset) : aim;
    }
    internal static Quaternion Look(Vector3 forward, Vector3 up)
    {
        forward = Vector3.Normalize(forward);
        Vector3 right = Vector3.Cross(up, forward);
        if (right.LengthSquared() < 0.0001f)
            right = Vector3.Cross(MathF.Abs(forward.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitZ, forward);
        right = Vector3.Normalize(right); up = Vector3.Normalize(Vector3.Cross(forward, right));
        // System.Numerics uses row-vector matrices.
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(new Matrix4x4(
            right.X,right.Y,right.Z,0, up.X,up.Y,up.Z,0,
            forward.X,forward.Y,forward.Z,0, 0,0,0,1)));
    }
}
