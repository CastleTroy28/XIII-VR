using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.122: the native muzzle point is used for the flash only when it lies
// near the fitted aim point (a gun whose muzzle transform sits elsewhere keeps
// the aim point).
internal static class MuzzleMath
{
    internal const float MaxShift=.20f,MaxBack=.12f;
    internal static bool Plausible(Vector3 native,Vector3 aim)
    {
        if(!float.IsFinite(native.X)||!float.IsFinite(native.Y)||!float.IsFinite(native.Z))return false;
        var d=native-aim;return d.Length()<=MaxShift&&d.Z>=-MaxBack;
    }
}
