using System;
namespace XiiiXR;
// 0.1.142: where the left hand holds a weapon's handle (x in the weapon's
// fitted frame), from where the right hand holds it: mirrored across the
// middle of the handle.
// Handguns: 0.1.139 mirrored the pistol across the middle of its box and
// shifted it 1.5 cm (the config's left pistol offset) - it ended up in
// the palm. 0.1.140 mirrored across the measured middle of the handle (the
// pistol's is 5 mm left of the box middle, the revolver's 13 mm right) and
// kept the 1.5 cm: the pistol moved 1 cm to the right in the hand and the
// revolver still lay beside the palm. Now the measured middle with the shift
// that keeps the pistol where it was (the config value minus 1 cm).
internal static class LeftHoldMath
{
    internal const float DefaultShift=.015f,MeasuredShiftLess=.010f,MaxShift=.04f;
    internal static bool Handgun(string profile)=>profile is "pistol" or "revolver";
    internal static float HandleX(float rightX,float middle,bool measured,bool handgun,float shift)
    {
        if(!float.IsFinite(middle))middle=0;
        if(!handgun)return 2*middle-rightX;
        shift=float.IsFinite(shift)?Math.Clamp(shift,-MaxShift,MaxShift):DefaultShift;
        return 2*middle-rightX-(measured?shift-MeasuredShiftLess:shift);
    }
}
