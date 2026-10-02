using System;
using System.Collections.Generic;
namespace XiiiXR;
// Choose the number of strong weapon contacts once per NPC. Do not roll once
// per collider or frame; a swing still goes through the native damage pipeline.
internal sealed class WeaponImpactDamage
{
    private readonly Dictionary<int,int> remaining=new();
    private readonly Random random=new();
    internal void Clear()=>remaining.Clear();
    // 0.1.91: oneHit — chairs, bottles, ashtrays and other props (not
    // weapons) knock the NPC out with the first strong contact.
    internal float Multiplier(int target,float health,float baseDamage,float nativeMultiplier,float bodyModifier=1,bool oneHit=false)
    {
        if(!float.IsFinite(health)||health<=0||!float.IsFinite(baseDamage)||baseDamage<=0)return nativeMultiplier;
        int hits;
        if(oneHit)hits=1;
        else if(!remaining.TryGetValue(target,out hits))hits=random.Next(1,4);
        remaining[target]=Math.Max(1,hits-1);
        float fraction=health/hits;
        return Math.Clamp(fraction/(baseDamage*Math.Max(.05f,bodyModifier))+.0001f,.01f,100);
    }
}
