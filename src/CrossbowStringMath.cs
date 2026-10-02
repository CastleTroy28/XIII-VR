using System;
namespace XiiiXR;
// 0.1.123: the crossbow's string follows the bolt drawn back along the rail.
// Positions are along the gun (fitted z, +Z towards the muzzle). The string
// is cocked with its nock at `nock` and released straight across the bow at
// `tip` (the front of the cocked string, at the cams).
internal static class CrossbowStringMath
{
    // 0.1.162: a crossbow taken "loaded" while the
    // game's own string was not cocked gave a cocked shape drawn 3 cm (it
    // should be ~20); it was kept and saved, and the string hardly moved from
    // then on. A shape drawn less than MinDraw is not a cocked one.
    internal const float MinDraw=.10f;
    internal static bool Cocked(float nock,float tip)=>float.IsFinite(nock)&&float.IsFinite(tip)&&tip-nock>=MinDraw;
    // How far the string is drawn (0 released .. 1 cocked) by a bolt whose
    // rear end is at boltRear: the bolt's rear carries the nock back.
    internal static float Draw(float boltRear,float nock,float tip)
    {
        if(!float.IsFinite(boltRear)||!float.IsFinite(nock)||!float.IsFinite(tip)||tip-nock<1e-3f)return 1;
        return Math.Clamp((tip-boltRear)/(tip-nock),0,1);
    }
    // How far forward a point of the cocked string (at z) moves at a draw:
    // the released string lies straight across at the tip.
    internal static float Shift(float z,float tip,float draw)
    {
        if(!float.IsFinite(z)||!float.IsFinite(tip)||!float.IsFinite(draw))return 0;
        return Math.Max(0,tip-z)*(1-Math.Clamp(draw,0,1));
    }
    // The shown draw follows the wanted one: at once when the string is let
    // go (a shot, an emptied gun), quickly when it is drawn.
    internal const float FollowRate=25;
    internal static float Follow(float shown,float wanted,float dt)
    {
        if(!float.IsFinite(wanted))return shown;
        wanted=Math.Clamp(wanted,0,1);
        if(!float.IsFinite(shown)||wanted<=shown)return wanted;
        float k=1-MathF.Exp(-FollowRate*Math.Clamp(float.IsFinite(dt)?dt:0,0,.25f));
        return shown+(wanted-shown)*k;
    }
}
