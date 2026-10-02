using System;
namespace XiiiXR;
internal static class SpreadPolicy
{
    // Native spread multiplier for the local player's own shots.
    // 0.1.101: the SVD held with both hands shoots where the scope looks
    // (native hip-fire spread of a sniper rifle is huge; the VR scope is
    // always "aimed"). Other rifles/SMGs join the two-hand reduction.
    internal static float Multiplier(bool owned,bool supported,string profile,float factor)
    {
        if(!owned||!float.IsFinite(factor))return 1;
        // 0.1.194: the crossbow shoots where its sight looks, in one hand or two.
        if(profile=="crossbow")return 0;
        if(profile=="sniper")return supported?0:Math.Clamp(factor*2,.05f,1);
        if(supported&&profile is "pistol" or "ak47" or "revolver" or "m16" or "uzi" or "m60")return Math.Clamp(factor,.05f,1);
        return 1;
    }
}
