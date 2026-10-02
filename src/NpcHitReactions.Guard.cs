using System;
using HarmonyLib;
using PlayMagic.AI;
using UnityEngine;
namespace XiiiXR;
// 0.1.148. The log of 0.1.147: every punch to the head took the enemy
// down at once whatever its health (41 of 145 dealt, health 0) - the game's
// own rule for a melee blow to the head. While the mod's punch is dealt
// (PunchDriver: PunchBegins .. PunchEnds) and it is not the blow that
// empties the health (MeleeDamageMath: at most a fifth of the full health a
// blow), the game may take only that share: its damage is held to it, its
// knockout and death held off, an always-lethal
// enemy not lethal for this blow, the health put back if more went.
internal sealed partial class NpcHitReactions
{
    private static IntPtr guardNpc;private static bool guardOn,guardLethal,guardLethalSet,guardStopped,guardClamped;
    private static float guardBefore,guardShare;private static string guardNote="";private static int guardHeld;
    private static void PatchGuard(Harmony h)
    {
        int n=0;string failed="";
        void Try(Type type,string method,string? prefix,string? postfix)
        {
            try
            {
                var m=AccessTools.DeclaredMethod(type,method);if(m==null){failed+=" "+type.Name+"."+method+" (not found)";return;}
                h.Patch(m,prefix:prefix!=null?new HarmonyMethod(typeof(NpcHitReactions),prefix):null,postfix:postfix!=null?new HarmonyMethod(typeof(NpcHitReactions),postfix):null);n++;
            }
            catch(Exception ex){failed+=" "+type.Name+"."+method+" ("+ex.Message+")";}
        }
        foreach(var t in new[]{typeof(NPC),typeof(Combatant)})
        {
            Try(t,"ReceiveDamage",nameof(GuardDamageIn),nameof(GuardDamageOut));
        }
        foreach(var t in new[]{typeof(NPC),typeof(Combatant),typeof(Enemy)})
        {
            Try(t,"Knockout",nameof(GuardKnockout),null);
            Try(t,"Die",nameof(GuardDie),null);
        }
        Bootstrap.Write("NPC HITS five punches at least: "+n+" of the game's damage, knockout and death steps guarded while a punch lands"+(failed.Length>0?"; not:"+failed:""));
    }
    // From PunchDriver around the game's damage of one punch (share: what it should take).
    internal static void PunchBegins(NPC npc,float share)
    {
        guardOn=false;guardNote="";guardLethalSet=false;guardStopped=false;guardClamped=false;
        if(npc==null||!(share>0))return;
        try{guardBefore=npc.CurrentHP;}catch(Exception){return;}
        if(!MeleeDamageMath.Standing(guardBefore,share))return;
        guardNpc=npc.Pointer;guardShare=share;guardOn=true;
    }
    internal static string PunchEnds(NPC npc)
    {
        if(!guardOn)return "";
        guardOn=false;
        try{if(guardLethalSet){guardLethalSet=false;npc.receivedDamagesAreAlwaysLethal=guardLethal;}}catch(Exception){}
        try
        {
            float want=guardBefore-guardShare;
            if((guardStopped||npc.IsAlive&&npc.IsConscious)&&npc.CurrentHP<want-.5f){npc.currentHP=want;if(!guardNote.Contains("health"))guardNote+=", health put back to "+want.ToString("F0");}
            // Its knockout held off before it took any damage, or its damage
            // held too far: the blow still takes its share.
            else if((guardStopped||guardClamped)&&npc.CurrentHP>want+.5f&&(guardStopped||npc.IsAlive&&npc.IsConscious)){npc.currentHP=want;guardNote+=", the blow's share dealt by the mod";}
            if(!npc.IsAlive||!npc.IsConscious)guardNote+=", down all the same";
        }
        catch(Exception){}
        if(guardNote.Length>0)guardHeld++;
        return guardNote.Length>0?" (five punches at least: the game"+guardNote+")":"";
    }
    private static bool Guarding(NPC? npc)=>guardOn&&npc!=null&&npc.Pointer==guardNpc;
    private static void GuardDamageIn(NPC __instance,ref float damage,NPC.DamageArea dmgArea,DamageInfo dmgInfo)
    {
        if(!Guarding(__instance))return;
        try
        {
            if(!guardLethalSet&&__instance.receivedDamagesAreAlwaysLethal){guardLethal=true;guardLethalSet=true;__instance.receivedDamagesAreAlwaysLethal=false;guardNote+=", always lethal - not this time";}
            float modifier=__instance.GetModifiedDamageByBodyPart(1,dmgArea,dmgInfo.type);
            float held=MeleeDamageMath.Held(damage,guardShare,modifier);
            if(held<damage-.01f)guardClamped=true;
            if(held<damage-.01f&&!guardNote.Contains("damage ")){guardNote+=", damage "+damage.ToString("F0")+" x"+modifier.ToString("F2")+" ("+dmgArea+") held to "+guardShare.ToString("F0");}
            damage=held;
        }
        catch(Exception){}
    }
    private static void GuardDamageOut(NPC __instance)
    {
        if(!Guarding(__instance))return;
        try
        {
            float want=guardBefore-guardShare;
            if((guardStopped||__instance.IsAlive&&__instance.IsConscious)&&__instance.CurrentHP<want-.5f){__instance.currentHP=want;if(!guardNote.Contains("health"))guardNote+=", health put back to "+want.ToString("F0");}
        }
        catch(Exception){}
    }
    private static bool GuardKnockout(NPC __instance)
    {
        if(!Guarding(__instance))return true;
        guardStopped=true;if(!guardNote.Contains("knockout"))guardNote+=", knockout held off";
        return false;
    }
    private static bool GuardDie(NPC __instance)
    {
        if(!Guarding(__instance))return true;
        guardStopped=true;if(!guardNote.Contains("death"))guardNote+=", death held off";
        return false;
    }
}
