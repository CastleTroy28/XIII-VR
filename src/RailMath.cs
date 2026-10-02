using System;
namespace XiiiXR;
// 0.1.122: haptic strength while the crossbow bolt is drawn back along its
// rail: a light constant feel on the rail, stronger the faster it slides.
internal static class RailMath
{
    internal const float Rest=.15f,PerMetrePerSecond=.9f,Max=.7f;
    internal static float Resistance(float speed)=>float.IsFinite(speed)?Math.Clamp(Rest+Math.Max(0,speed)*PerMetrePerSecond,Rest,Max):Rest;
}
