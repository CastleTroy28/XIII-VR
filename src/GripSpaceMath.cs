using System;
using System.Numerics;
namespace XiiiXR;
internal static class GripSpaceMath
{
    // Row-vector equivalent of initialRoot * inverse(liveRoot).
    internal static Matrix4x4 FrozenRoot(Matrix4x4 initial,Matrix4x4 live)
    {
        if(!Matrix4x4.Invert(live,out var inverse))throw new InvalidOperationException("Singular weapon root");
        return inverse*initial;
    }
}
