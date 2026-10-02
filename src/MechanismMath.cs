using System;
namespace XiiiXR;
internal static class MechanismMath
{
    internal static bool Matches(string bone,string profile)
    {
        string name=bone.ToLowerInvariant();
        if(name.Contains("finger") || name.Contains("wrist") || name.Contains("arm_")) return false;
        if(profile=="pistol"&&name.Contains("slidestop"))return false;
        if(profile=="pistol") return name.Contains("slide") || name.Contains("slider") || name.Contains("bolt") || name.Contains("breech");
        if(profile=="shotgun") return name.Contains("pump") || name.Contains("foregrip") || name.Contains("forend") || name.Contains("forestock") || name.Contains("bolt");
        if(profile=="sniper") return name.Contains("champer") || name.Contains("bolt") || name.Contains("charging");
        // 0.1.119: M16 — the charging handle ("m16_handle*") and the bolt carrier
        // travel back together; the bolt release lever does not.
        // 0.1.119: M60 charging handle.
        if(profile=="m60") return name.Contains("m60_handle")&&!name.Contains("handle_top");
        // 0.1.194: the Uzi's top cocking knob, drawn back by hand. 0.1.198: it is
        // the "aim" bone (wpn_uzi_aim_BND_JNT), not "cover" (the ejection port cover).
        if(profile=="uzi") return name.Contains("uzi_aim_bnd");
        if(profile=="m16") return name.Contains("m16_handle") || name.Contains("charging") || name.Contains("m16_bolt")&&!name.Contains("boltrelease");
        return name.Contains("ejection_port") || name.Contains("bolt") || name.Contains("charging") || name.Contains("slide");
    }
    internal static float Cycle(float age,string profile)
    {
        if(!float.IsFinite(age) || age<0) return 0;
        // The Uzi's knob does not move when it fires (only by hand).
        if(profile=="uzi") return 0;
        float delay=profile=="shotgun"?.09f:0;
        float back=profile=="shotgun"?.15f:.035f,forward=profile=="shotgun"?.18f:.075f;
        age-=delay; if(age<=0 || age>=back+forward) return 0;
        return Math.Clamp(age<back?age/back:1-(age-back)/forward,0,1);
    }
    // A still copy's cycled-back mesh for its shots (none for the Uzi's knob).
    internal static float Travel(string profile) => profile=="shotgun"?.085f:profile=="uzi"?0:.028f;
}
