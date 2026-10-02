using System;
using System.Collections.Generic;
using PlayMagic.AI;
using UnityEngine;
namespace XiiiXR;
// 0.1.141: a hit NPC is out of it for a moment: HitPlan.Stun seconds (up to 0.95 s by the
// blow and where it landed) it does not shoot or strike (the game's
// Combatant.UseWeapon / UseMeleeWeapon are refused, a burst in progress is
// stopped), does not walk (its root motion is not applied, its NavMeshAgent
// held); on a hard blow the game's own stun animation plays too. The pain
// pose (HitReactionMath) is held that long, then it comes to.
internal sealed partial class NpcHitReactions
{
    private sealed class Stun{internal NPC Npc=null!;internal float Since,Until;internal bool AgentHeld,AgentWas,GameStun;}
    private readonly Dictionary<IntPtr,Stun> stuns=new();
    internal const float StrongStun=1.2f;
    private bool Stunned(IntPtr npc)=>stuns.TryGetValue(npc,out var s)&&Time.time<s.Until;
    // Stunned, or its gun held by the player.
    private bool Holding(IntPtr npc)=>Stunned(npc)||GrabSide(npc)>=0;
    private string StunFor(NPC npc,float seconds,bool strong)
    {
        if(!(seconds>0)||npc==null)return "";
        float now=Time.time;string how="";
        if(!stuns.TryGetValue(npc.Pointer,out var s))
        {
            s=new Stun{Npc=npc,Since=now};stuns[npc.Pointer]=s;
            how+=StopFiring(npc);StopWalking(npc,s);
        }
        s.Until=Math.Max(s.Until,now+seconds);
        if(strong&&!s.GameStun){try{var a=AnimOf(npc);if(a!=null){a.SetStun(true);s.GameStun=true;how+=" +game stun";}}catch(Exception ex){how+=" (game stun: "+ex.Message+")";}}
        return " stunned "+(s.Until-now).ToString("F1")+" s"+how;
    }
    private static string StopFiring(NPC npc)
    {
        string how="";
        try
        {
            var c=npc.TryCast<Combatant>();
            if(c!=null){var co=c.usingWeaponCoroutine;if(co!=null){c.StopCoroutine(co);c.usingWeaponCoroutine=null;how=" (burst stopped)";}}
        }
        catch(Exception){}
        try{AnimOf(npc)?.ToggleIsShooting(false,true);}catch(Exception){}
        return how;
    }
    private static void StopWalking(NPC npc,Stun s)
    {
        try{AnimOf(npc)?.StopMovement();}catch(Exception){}
        try
        {
            var a=npc.NavMeshAgent;
            if(a!=null&&a.enabled&&a.isOnNavMesh){s.AgentWas=a.isStopped;a.isStopped=true;s.AgentHeld=true;}
        }
        catch(Exception){}
    }
    private readonly List<IntPtr> comeTo=new();
    // Every frame: stuns that are over end (not while its gun is held).
    private void TickStuns()
    {
        if(stuns.Count==0)return;
        float now=Time.time;comeTo.Clear();
        foreach(var pair in stuns)
        {
            var s=pair.Value;
            bool gone=s.Npc==null||!s.Npc.IsAlive||!s.Npc.IsConscious||s.Npc.isRagdoll;
            if(!gone&&(now<s.Until||GrabSide(pair.Key)>=0))continue;
            comeTo.Add(pair.Key);
            if(!gone){End(s);if(reports++<80)Bootstrap.Write("NPC HIT "+s.Npc!.name+" comes to after "+(now-s.Since).ToString("F1")+" s");}
        }
        foreach(var id in comeTo)stuns.Remove(id);
    }
    private static void End(Stun s)
    {
        if(s.AgentHeld)try{var a=s.Npc.NavMeshAgent;if(a!=null&&a.enabled&&a.isOnNavMesh)a.isStopped=s.AgentWas;}catch(Exception){}
        if(s.GameStun)try{AnimOf(s.Npc)?.SetStun(false);}catch(Exception){}
    }
    private void EndAllStuns()
    {
        foreach(var s in stuns.Values)if(s.Npc!=null)try{End(s);}catch(Exception){}
        stuns.Clear();
    }
    // Harmony prefixes: the game's own attack and walking, refused meanwhile.
    private static bool NpcUseWeapon(Combatant __instance)
    {
        var c=Current;if(c==null||__instance==null)return true;
        return !c.Holding(__instance.Pointer)&&!c.brawlers.ContainsKey(__instance.Pointer);
    }
    private static bool NpcMove(AIMovementController __instance)
    {
        var c=Current;if(c==null||__instance==null||c.stuns.Count==0&&c.grabs[0]==null&&c.grabs[1]==null&&c.brawlers.Count==0)return true;
        NPC? npc=null;try{npc=__instance.npcScript;}catch(Exception){}
        // 0.1.147: a brawler is moved by the mod's steps only (its walk
        // animation's own motion would carry it on).
        return npc==null||!c.Holding(npc.Pointer)&&!c.brawlers.ContainsKey(npc.Pointer);
    }
}
