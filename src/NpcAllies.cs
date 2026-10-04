using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// 0.1.218: AllyRule read from the game's characters. A log of 0.1.216: Major
// Jones (npc_jones_civilian, in the canyon) was taken hostage by pointing at
// her (the VR hostage rule, made for enemies, let her be taken); the game's
// drop of her then failed (HandleBodyDropAnimationComplete) and broke the level.
internal static class NpcAllies
{
    internal static bool Ally(PlayMagic.AI.NPC? npc)
    {
        if(npc==null)return false;
        try{return AllyRule.Ally(npc.TryCast<PlayMagic.AI.Ally>()!=null,npc.canBeHurtByPlayer,npc.canBeTakenHostageEvenIfCannotBeHurt,InFlashback);}
        catch(Exception){return false;}
    }
    // 0.1.230: a playable flashback (a memory) is on: the game takes no hostage
    // there, and the people in it (Kim in the last mission) are left alone.
    internal static bool InFlashback{get{try{return GameManager.IsInFlashback;}catch(Exception){return false;}}}
    // The NPC a collider belongs to is an ally.
    internal static bool AllyCollider(Collider? collider,out PlayMagic.AI.NPC? npc)
    {
        npc=null;if(collider==null)return false;
        try{npc=collider.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())?.TryCast<PlayMagic.AI.NPC>();}catch(Exception){return false;}
        return Ally(npc);
    }
    internal static bool GameAllowsHostage(PlayMagic.AI.NPC npc)
    {
        try{return AllyRule.HostageByGame(npc.TryCast<PlayMagic.AI.Ally>()!=null,npc.canBeHurtByPlayer,npc.canBeTakenHostageEvenIfCannotBeHurt,InFlashback);}
        catch(Exception){return true;}
    }
    private static readonly HashSet<string> told=new();
    // Once per character and action: what was not done to it, and why.
    internal static void Refused(PlayMagic.AI.NPC? npc,string what)
    {
        if(npc==null)return;
        string name="?";try{name=npc.name;}catch(Exception){}
        if(told.Count>200)told.Clear();
        if(!told.Add(name+"|"+what))return;
        string kind="?",faction="?",hurt="?",anyway="?";
        try{kind=npc.TryCast<PlayMagic.AI.Ally>()!=null?"the game's Ally":npc.GetIl2CppType().Name;faction=npc.faction.ToString();hurt=npc.canBeHurtByPlayer?"yes":"no";anyway=npc.canBeTakenHostageEvenIfCannotBeHurt?"yes":"no";}catch(Exception){}
        Bootstrap.Write("ALLY "+name+" ("+kind+", faction "+faction+", the player may hurt: "+hurt+", hostage even so: "+anyway+(InFlashback?", in a memory (flashback): the game takes no hostage there":"")+"): not "+what+" - the mod leaves the player's allies alone");
    }
}
