using System;
using System.Numerics;
using XiiiXR;
internal static class ProjectionMathTests
{
    private static void Near(float actual, float expected, string label)
    { if (MathF.Abs(actual - expected) > 0.00003f) throw new Exception(label + ": " + actual + " expected " + expected); }
    private static Vector3 Ndc(Matrix4x4 p, Vector3 v)
    {
        float w = p.M41 * v.X + p.M42 * v.Y + p.M43 * v.Z + p.M44;
        return new Vector3((p.M11*v.X+p.M12*v.Y+p.M13*v.Z+p.M14)/w,
            (p.M21*v.X+p.M22*v.Y+p.M23*v.Z+p.M24)/w,
            (p.M31*v.X+p.M32*v.Y+p.M33*v.Z+p.M34)/w);
    }
    public static void Main()
    {
        // Deliberately asymmetric synthetic lens frusta; not a Pimax preset.
        var left = new EyeFrustum(-1.4f, 0.8f, -0.7f, 1.1f);
        var right = new EyeFrustum(-0.8f, 1.4f, -0.7f, 1.1f);
        foreach (var f in new[] { left, right })
        {
            var p = ProjectionMath.Build(f, 0.01f, 1000);
            foreach (float depth in new[] { 0.01f, 0.1f, 1f, 15f, 1000f })
            {
                Near(Ndc(p, new Vector3(f.Left*depth, 0, -depth)).X, -1, "left boundary");
                Near(Ndc(p, new Vector3(f.Right*depth, 0, -depth)).X, 1, "right boundary");
                Near(Ndc(p, new Vector3(0, f.Bottom*depth, -depth)).Y, 1, "Unity upper boundary");
                Near(Ndc(p, new Vector3(0, f.Top*depth, -depth)).Y, -1, "Unity lower boundary");
            }
            Near(Ndc(p, new Vector3(0, 0, -0.01f)).Z, -1, "near plane");
            Near(Ndc(p, new Vector3(0, 0, -1000)).Z, 1, "far plane");
        }
        var pl = ProjectionMath.Build(left, 0.01f, 1000);
        var pr = ProjectionMath.Build(right, 0.01f, 1000);
        Near(pl.M13, -pr.M13, "left/right lens asymmetry retained");
        if (MathF.Abs(pl.M13) < 0.2f) throw new Exception("asymmetric optical center was lost");
        var symmetric = new EyeFrustum(-1, 1, -1, 1);
        var ps = ProjectionMath.Build(symmetric, 0.01f, 1000);
        // A centered object one metre away lies to the right in the left eye,
        // and to the left in the right eye. Its disparity decreases with depth.
        float nearDisparity = Ndc(ps, new Vector3(0.032f, 0, -1)).X - Ndc(ps, new Vector3(-0.032f, 0, -1)).X;
        float farDisparity = Ndc(ps, new Vector3(0.032f, 0, -10)).X - Ndc(ps, new Vector3(-0.032f, 0, -10)).X;
        Near(nearDisparity, 0.064f, "64mm IPD disparity sign");
        Near(farDisparity, nearDisparity / 10, "disparity decreases with depth");
        foreach (var f in new[] { new EyeFrustum(float.NaN,1,-1,1), new EyeFrustum(1,-1,-1,1), new EyeFrustum(-1,1,1,1) })
        {
            try { ProjectionMath.Build(f, 0.01f, 1000); throw new Exception("invalid frustum accepted"); }
            catch (ArgumentOutOfRangeException) { }
        }
        Console.WriteLine("PASS: asymmetric eye frustum boundaries, vertical convention, clip planes, lens offsets, disparity sign/depth, invalid frusta.");
        Console.WriteLine("Mathematics only; does not validate Unity callbacks, final XR textures, or headset optics.");
    }
}
