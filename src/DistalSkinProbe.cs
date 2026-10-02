using System;using System.Numerics;
namespace XiiiXR;
internal static class DistalSkinProbe
{
    // A two-centimetre local translation maps exactly to skin weight. Exclude
    // adjacent fingers, blended knuckles, wrong outfits and malformed bakes.
    internal static bool Follows(Vector3 delta)=>float.IsFinite(delta.X)&&float.IsFinite(delta.Y)&&float.IsFinite(delta.Z)
        &&delta.X>=.019f&&delta.X<=.0202f&&Math.Abs(delta.Y)<.0002f&&Math.Abs(delta.Z)<.0002f;
}
