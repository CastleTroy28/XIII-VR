using System;
using System.Numerics;
namespace XiiiXR;
internal static class DualGripMath
{
    internal static Matrix4x4 MirrorDeformation(Matrix4x4 leftRest,Matrix4x4 rightRest,Matrix4x4 rightPose)
    {
        if(!Matrix4x4.Invert(rightRest,out var inverse))throw new InvalidOperationException("Singular right finger rest pose");
        var mirror=Matrix4x4.CreateScale(-1,1,1);
        // Reflect the skin deformation, not the right skeleton's bone axes.
        // Imported left/right bones need not share the same local basis.
        return leftRest*mirror*inverse*rightPose*mirror;
    }
}
