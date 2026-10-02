using System;
using System.Numerics;
namespace XiiiXR;
internal readonly struct EyeFrustum
{
    internal readonly float Left, Right, Top, Bottom;
    internal EyeFrustum(float left, float right, float top, float bottom)
    { Left = left; Right = right; Top = top; Bottom = bottom; }
    internal bool Valid => float.IsFinite(Left) && float.IsFinite(Right) && float.IsFinite(Top) && float.IsFinite(Bottom)
        && Right - Left > 0.0001f && Bottom - Top > 0.0001f;
}
internal static class ProjectionMath
{
    // OpenVR GetProjectionRaw uses negative top and positive bottom tangents.
    // Same clip-space convention as Valve's Unity XR provider, before Unity's
    // graphics-API conversion. Do NOT apply GL.GetGPUProjectionMatrix here.
    // Matrix entries below use column-vector math (copy rows to Unity directly).
    internal static Matrix4x4 Build(EyeFrustum f, float near, float far)
    {
        if (!f.Valid || !float.IsFinite(near) || !float.IsFinite(far) || near <= 0 || far <= near)
            throw new ArgumentOutOfRangeException(nameof(f), "Invalid eye frustum or clip planes");
        float width = f.Right - f.Left, height = f.Bottom - f.Top;
        return new Matrix4x4(
            2 / width, 0, (f.Right + f.Left) / width, 0,
            0, 2 / height, (f.Bottom + f.Top) / height, 0,
            0, 0, -(far + near) / (far - near), -2 * far * near / (far - near),
            0, 0, -1, 0);
    }
}
