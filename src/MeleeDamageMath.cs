using System;
namespace XiiiXR;
// 0.1.141: how many blows take an NPC down. A punch with the fist takes 5 (hard) to 8 (light)
// blows, a blow with a gun in the hand 5 to 6 (0.1.148), counted on the NPC's full
// health, whatever the game's own fist damage. A blow to the head counts
// more, one to a leg less. A weapon thrown at an NPC does a little
// (10..16 of them).
internal static class MeleeDamageMath
{
    internal const float LightSpeed=1.5f,HardSpeed=5f;
    // Swing strength 0 (light, 1.5 m/s) .. 1 (hard, 5 m/s and more).
    internal static float Strength(float speed)=>float.IsFinite(speed)?Math.Clamp((speed-LightSpeed)/(HardSpeed-LightSpeed),0,1):0;
    internal static float Hits(float strength,bool weapon)
    {
        strength=float.IsFinite(strength)?Math.Clamp(strength,0,1):0;
        return weapon?6-strength:8-3*strength;
    }
    // 0.1.148: no blow takes more than a fifth of the full health, the head
    // included (a gun in the hand: 6..5 blows, not 6..4).
    internal const int MinBlows=5;
    internal static float Share(float maxHealth,float hits,float areaFactor,int minBlows=MinBlows)
    {
        if(!(maxHealth>0)||!float.IsFinite(maxHealth)||!(hits>0)||!float.IsFinite(hits))return 0;
        if(!float.IsFinite(areaFactor)||areaFactor<=0)areaFactor=1;
        return Math.Min(maxHealth/hits*areaFactor,maxHealth/Math.Max(1,minBlows));
    }
    // 0.1.150: long-handled
    // things (broom, mop, shovel, ...) hit harder than a fist - 5 (light) to 3
    // (hard) blows, never fewer than 3 - and break only after their last blow
    // on an enemy.
    internal const int MinPropBlows=3,PropDurability=5;
    internal static float PropHits(float strength){strength=float.IsFinite(strength)?Math.Clamp(strength,0,1):0;return 5-2*strength;}
    internal static bool DurableProp(string? identifier)
    {
        if(string.IsNullOrEmpty(identifier))return false;
        var n=identifier!.ToLowerInvariant();
        if(n.Contains("combat"))return false;
        foreach(var t in new[]{"broom","mop","shovel","spade","rake","pipe","plank","crowbar","bat","club","stick","pole"})if(n.Contains(t))return true;
        return false;
    }
    // 0.1.161: wooden long things get a wooden knock (WoodKnock).
    internal static bool WoodenProp(string? identifier)
    {
        if(string.IsNullOrEmpty(identifier))return false;
        var n=identifier!.ToLowerInvariant();
        foreach(var t in new[]{"broom","mop","rake","plank","bat","club","stick","pole"})if(n.Contains(t))return true;
        return false;
    }
    // This blow leaves it standing (the game must not knock it out or kill it
    // on its own rules - a punch to the head did it at once).
    internal static bool Standing(float healthBefore,float share)=>float.IsFinite(healthBefore)&&float.IsFinite(share)&&share>0&&healthBefore-share>.5f;
    // The game's own damage of this blow held to the share: the damage it may
    // pass on, given what it multiplies it by for the body part.
    internal static float Held(float damage,float share,float bodyModifier)
    {
        if(!float.IsFinite(bodyModifier)||bodyModifier<.05f)bodyModifier=.05f;
        if(!float.IsFinite(damage))return share/bodyModifier;
        return damage*bodyModifier>share*1.02f?share/bodyModifier:damage;
    }
    // area: the game's DamageArea (0 body, 1 head, 2 upper limb, 4 lower limb).
    // 0.1.142: an arm counts like the body;
    // a leg a little less.
    internal static float AreaFactor(int area)=>area switch{1=>1.4f,4=>.8f,_=>1f};
    internal static float ThrownHits(float speed)=>16-6*Strength(speed*ThrownReaction);
    // A thrown weapon's speed counts like this much of a fist's (the weapon
    // is lighter than an arm behind it): 8 m/s acts like a 4.8 m/s punch.
    internal const float ThrownReaction=.6f;
    // The game's damage multiplier that makes one blow take this share of the
    // NPC's full health: damage = baseDamage x multiplier x bodyModifier.
    internal static float Multiplier(float maxHealth,float baseDamage,float bodyModifier,float hits,float areaFactor,float fallback)
    {
        if(!(maxHealth>0)||!float.IsFinite(maxHealth)||!(baseDamage>0)||!float.IsFinite(baseDamage)||!(hits>0))return fallback;
        if(!float.IsFinite(bodyModifier)||bodyModifier<.05f)bodyModifier=.05f;
        if(!float.IsFinite(areaFactor)||areaFactor<=0)areaFactor=1;
        return Math.Clamp(maxHealth/hits*areaFactor/(baseDamage*bodyModifier),.001f,100f);
    }
}
