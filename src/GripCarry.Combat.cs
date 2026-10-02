using System;
using System.Collections.Generic;
using HarmonyLib;
using PlayMagic.AI;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class GripCarry
{
 private readonly HostageHurtboxes hostageBoxes=new();
 private readonly Dictionary<int,NPC> hostageShooters=new();private int shooterEpoch=-1,shieldReports;
 private float nextCombatError,nextShieldNote;
 // 0.1.191: while held, a hostage cannot die. A
 // bullet hurts him (the game's hit, cry and reaction) but leaves him a
 // sliver of life; the hits that would have killed him are remembered, and
 // when he is let go of he dies on the floor (by that shooter).
 private const float HostageGuard=100000;
 private NPC? mortalHostage;private OwnerInfo mortalBy;private int hostageHits;
 private NPC? LiveHostage=>!releaseBody&&!rig.Scripted&&rig.HeadTrackingValid&&carrier?.hasBody==true&&!carrier.isInTransition
  &&carrier.TryCast<PlayerHostageController>()!=null&&carrier.bodyNPC?.actorStatus==ActorStatus.Conscious?carrier.bodyNPC:null;
 partial void InstallHostageCombat()
 {
  try
  {
   // T5AI calls the targeting agent directly, bypassing Combatant getters.
   patches.Patch(AccessTools.DeclaredMethod(typeof(AITargetingAgent),"DoesCurrentTargetsHostagePreventAttack"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(HostageAttack)));
   patches.Patch(AccessTools.DeclaredMethod(typeof(AITargetingAgent),"DoesCurrentTargetsHostagePreventCover"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(HostageCover)));
   patches.Patch(AccessTools.PropertyGetter(typeof(AITargetingAgent),"DoesCurrentTargetHaveHostage"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(HostageKnown)));
   foreach(var type in new[]{typeof(NPC),typeof(Combatant),typeof(Civilian)})
    patches.Patch(AccessTools.DeclaredMethod(type,"ReceiveDamage"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(BeginHostageDamage)),finalizer:new HarmonyMethod(typeof(GripCarry),nameof(EndHostageDamage)));
   patches.Patch(AccessTools.DeclaredMethod(typeof(NPC),"IsHeldByPlayer"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(DamageHeldCheck)));
   patches.Patch(AccessTools.DeclaredMethod(typeof(HitInfo),"SetValues"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(KeepInstigator)));
   patches.Patch(AccessTools.DeclaredMethod(typeof(PlayerState),"ReceiveDamage"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(ShieldPlayer)));
   Bootstrap.Write("HOSTAGE COMBAT ready: Terrorist shooters ignore deterrence; police, FBI and guards hold fire at a live hostage's holder (told of it if they came after it was taken) until he fires; every bullet at the holder goes into the hostage");
  }
  catch(Exception ex){Bootstrap.Warn("HOSTAGE COMBAT hooks: "+ex.Message);}
 }
 private static bool HostageAttack(AITargetingAgent __instance,ref bool __result)=>HostageDecision(__instance,ref __result,HostageAsk.Attack);
 private static bool HostageCover(AITargetingAgent __instance,ref bool __result)=>HostageDecision(__instance,ref __result,HostageAsk.Cover);
 private static bool HostageKnown(AITargetingAgent __instance,ref bool __result)=>HostageDecision(__instance,ref __result,HostageAsk.Known);
 private enum HostageAsk{Attack,Cover,Known}
 // 0.1.205: the game
 // tells its enemies of a hostage the moment it is taken (OnHostageTaken),
 // and only those already there. The bank's guards come in through the door
 // after the hostage is taken: they never heard of it, and shot. A shooter
 // that does not know of the live hostage of the one it targets is told now,
 // as the game would have told it, and listens to that hostage's later
 // events (released, fired with). Police, FBI and guards then hold fire at
 // him until he fires with the hostage in his hands (the game's own record);
 // Terrorists shoot anyway (0.1.191).
 private static bool HostageDecision(AITargetingAgent agent,ref bool result,HostageAsk ask)
 {
  var c=Current;var hostage=c?.LiveHostage;if(c==null||hostage==null)return true;
  try
  {
   var target=agent.CurrentTarget;var controller=c.carrier!.TryCast<PlayerHostageController>();
   var holder=controller?.owningPlayerEntity;
   if(target==null||controller==null||holder==null||target.Pointer!=holder.Pointer)return true;
   if(c.shooterEpoch!=SceneScan.Epoch){c.shooterEpoch=SceneScan.Epoch;c.hostageShooters.Clear();c.hostageTold.Clear();}
   int id=agent.ownerInstanceID;
   if(!c.hostageShooters.TryGetValue(id,out var shooter)||shooter==null)
   {
    shooter=AIManager.Instance?.GetNPCByID(id);if(shooter==null)return true;
    c.hostageShooters[id]=shooter;
   }
   if(HostageShieldMath.Gangster((int)shooter.faction)){if(ask==HostageAsk.Known)return true;result=false;return false;}
   c.TellOfHostage(agent,target,shooter,hostage,controller);
   if(ask!=HostageAsk.Attack)return true;
   // He fired with the hostage in his hands: the game decides (it may fire back).
   if(agent.CurrentTargetWrapper?.hasFiredWhileHavingHostage==true)return true;
   result=true;return false;
  }
  catch(Exception ex){c.CombatError(ex);return true;}
 }
 private readonly Dictionary<long,int> hostageTold=new();[ThreadStatic] private static bool telling;
 private void TellOfHostage(AITargetingAgent agent,ITargetable target,NPC shooter,NPC hostage,PlayerHostageController controller)
 {
  var wrapper=agent.CurrentTargetWrapper;
  if(telling||wrapper==null||wrapper.hostageFaction!=NPC.Faction.None)return;
  long key=((long)shooter.GetInstanceID()<<32)^(uint)hostage.GetInstanceID();
  hostageTold.TryGetValue(key,out int tries);if(tries>=3)return;hostageTold[key]=tries+1;
  telling=true;string how="";
  try
  {
   var enemy=shooter.TryCast<Enemy>();
   if(enemy!=null)
   {
    bool listening=false;var observed=enemy.observedPlayerHostageControllers;
    if(observed!=null)for(int i=0;i<observed.Count;i++)if(observed[i]!=null&&observed[i].Pointer==controller.Pointer){listening=true;break;}
    if(!listening){enemy.ObserveHostageController(controller,true);how+=", now listens to the hostage's events";}
    if(wrapper.hostageFaction==NPC.Faction.None){enemy.HandlePlayerTakingHostage(target,hostage.faction);how+=", told as the game tells";}
   }
   if(wrapper.hostageFaction==NPC.Faction.None){agent.SetHostageState(target,hostage.faction);how+=", its target's hostage set";}
  }
  finally{telling=false;}
  Bootstrap.Write("HOSTAGE "+shooter.name+" ("+shooter.faction+") came after "+hostage.name+" ("+hostage.faction+") was taken"+how+"; it knows of the hostage: "+(wrapper.hostageFaction!=NPC.Faction.None));
 }
 private sealed class HostageDamage
 {
  internal readonly NPC Npc;internal readonly OwnerInfo Instigator;internal readonly float Before;
  internal readonly HostageDamage? Previous;
  internal bool Guarded,WasLethal;
  internal HostageDamage(NPC npc,OwnerInfo who,HostageDamage? previous){Npc=npc;Instigator=who;Before=npc.CurrentHP;Previous=previous;}
 }
 [ThreadStatic] private static HostageDamage? hostageDamage;
 private static bool Ballistic(DamageInfo info,OwnerInfo who)=>!who.IsPlayer&&!who.IsInvalid&&(info.type&DamageType.Ballistic)!=0&&(info.type&DamageType.Explosive)==0;
 private static void BeginHostageDamage(NPC __instance,float damage,DamageInfo dmgInfo,ref OwnerInfo instigator,out HostageDamage? __state)
 {
  __state=null;var c=Current;var npc=c?.LiveHostage;
  if(npc==null||npc.Pointer!=__instance.Pointer||damage<=0||!Ballistic(dmgInfo,instigator))return;
  // ReceiveDamage rejects IsHeldByPlayer; Combatant also rejects matching
  // factions. Relax those checks only for this one real incoming bullet.
  // Do not change faction, invulnerability, AI state or carry ownership.
  if(hostageDamage?.Npc.Pointer!=npc.Pointer)
  {
   __state=new HostageDamage(npc,instigator,hostageDamage);hostageDamage=__state;
   // His life raised far above any hit for this one hit only (the game then
   // sees no lethal hit: no death, no ragdoll off the arm).
   try{__state.WasLethal=npc.receivedDamagesAreAlwaysLethal;npc.receivedDamagesAreAlwaysLethal=false;npc.currentHP=__state.Before+HostageGuard;__state.Guarded=true;}
   catch(Exception ex){c!.CombatError(ex);}
  }
  if(instigator.Faction==npc.faction)instigator=new OwnerInfo(instigator.Id,instigator.IsPlayer,NPC.Faction.None,instigator.IsAlly,instigator.IsInvalid,instigator.NetworkActorNumber);
 }
 private static bool DamageHeldCheck(NPC __instance,ref bool __result)
 {if(hostageDamage?.Npc.Pointer!=__instance.Pointer)return true;__result=false;return false;}
 private static void KeepInstigator(HitInfo __instance,ref OwnerInfo instigator)
 {
  var scope=hostageDamage;
  if(scope!=null&&scope.Npc.LastHitReceived?.Pointer==__instance.Pointer)instigator=scope.Instigator;
 }
 private static Exception? EndHostageDamage(Exception? __exception,HostageDamage? __state)
 {
  if(__state==null)return __exception;
  hostageDamage=__state.Previous;
  try
  {
   var npc=__state.Npc;if(npc==null)return __exception;
   if(__state.Guarded)
   {
    npc.receivedDamagesAreAlwaysLethal=__state.WasLethal;
    float hpLost=0;try{hpLost=npc.LastHitReceived?.hpLost??0;}catch(Exception){}
    float applied=HostageShieldMath.Applied(__state.Before+HostageGuard,npc.CurrentHP,hpLost,HostageGuard);
    bool alive=npc.actorStatus==ActorStatus.Conscious;
    float left=HostageShieldMath.Left(__state.Before,applied,out bool mortal);
    npc.currentHP=alive?left:0;
    if(applied<=0)return __exception;
    var c0=Current;
    if(c0!=null)
    {
     c0.hostageHits++;
     if(mortal&&alive)
     {
      bool first=c0.mortalHostage==null||c0.mortalHostage.Pointer!=npc.Pointer;
      c0.mortalHostage=npc;c0.mortalBy=__state.Instigator;
      if(first)Bootstrap.Write("HOSTAGE "+npc.name+" wounded to death in the hands ("+applied.ToString("F0")+" of "+__state.Before.ToString("F0")+" hp): alive while held, dies when let go of");
     }
    }
    // Native OnHit normally already played this voice; the cry below has the NPC's own cooldown.
    var hit=npc.LastHitReceived;if(alive&&hit!=null)npc.AIAudioHandler?.PlayHurtSound(npc,hit);
    if(c0!=null&&c0.shieldReports++<20)Bootstrap.Write("HOSTAGE HIT "+npc.name+" hp="+__state.Before.ToString("F1")+"->"+npc.CurrentHP.ToString("F1")+" (hit "+applied.ToString("F1")+(mortal?", would have died":"")+") shooter="+__state.Instigator.Faction);
    return __exception;
   }
   if(npc.CurrentHP>=__state.Before)return __exception;
   // Native OnHit normally already played this voice. PlayHurtSound has its
   // own per-NPC cooldown, so this also covers a missing subscriber without
   // double voices or a new scream on every pellet. Death keeps native audio.
   if(npc.actorStatus==ActorStatus.Conscious)npc.AIAudioHandler?.PlayHurtSound(npc,npc.LastHitReceived);
   var c=Current;if(c!=null&&c.shieldReports++<20)Bootstrap.Write("HOSTAGE HIT "+npc.name+" hp="+__state.Before.ToString("F1")+"->"+npc.CurrentHP.ToString("F1")+" shooter="+__state.Instigator.Faction);
  }
  catch(Exception ex){Current?.CombatError(ex);}
  return __exception;
 }
 private static bool ShieldPlayer(PlayerState __instance,float damage,DamageInfo dmgInfo,float force,Vector3 hitPosition,OwnerInfo instigator,Vector3 origin,Vector3 direction)
 {
  var c=Current;var npc=c?.LiveHostage;
  if(c==null||npc==null||damage<=0||!Ballistic(dmgInfo,instigator)||c.handler==null||__instance.owner.Id!=c.handler.GetOwner().Id)return true;
  float before=npc.CurrentHP;int hits=c.hostageHits;
  try
  {
   // Use the last straight segment approaching the player's actual hit point,
   // not a ray aimed at their centre. This preserves side/back/exposed hits.
   if(!HostageShieldMath.Finite(V(origin))||!HostageShieldMath.Finite(V(hitPosition))||!HostageShieldMath.Finite(V(direction)))return true;
   float length=Math.Min(3,(hitPosition-origin).magnitude);if(length<.001f||direction.sqrMagnitude<1e-8f)return true;
   var from=hitPosition-direction.normalized*length;
   // The FPS animator may have moved the native parent since the last render.
   // Apply the same HMD anchor before reading bone transforms, without a
   // scene-wide Physics.SyncTransforms or modifying collider enable flags.
   c.RenderBody();
   c.hostageBoxes.Bind(npc);
   // 0.1.205: a bullet that misses the hostage's shape
   // (past his side, his legs, from behind) goes into him too - into his
   // body, its side towards the shooter.
   if(!c.hostageBoxes.Intersect(from,hitPosition,out var box,out var point,out var area))
   {
    if(!c.hostageBoxes.Facing(origin,out box,out point,out area))return true;
    if(c.shieldReports<20&&Time.realtimeSinceStartup>=c.nextShieldNote){c.nextShieldNote=Time.realtimeSinceStartup+3;Bootstrap.Write("HOSTAGE SHIELD a bullet past "+npc.name+" ("+instigator.Faction+") goes into his "+area+" instead of the player");}
   }
   var receiver=npc.GetDamageReceiver();if(receiver==null||!receiver.CanReceiveDamage())return true;
   receiver.ReceiveDamage(damage,dmgInfo,force,box!,point,instigator,origin,direction,area);
   // Only replace a player hit after the NPC actually accepted it. No silent
   // invulnerability when an unsupported NPC or a damage hook rejects damage.
   // 0.1.191: his life no longer drops to zero (kept above it while held): the hit counts.
   return c.hostageHits==hits&&npc.CurrentHP>=before&&npc.actorStatus==ActorStatus.Conscious;
  }
  catch(Exception ex){c.CombatError(ex);return npc==null||c.hostageHits==hits&&npc.CurrentHP>=before;}
 }
 partial void MortalRelease(PlayerCarryAIController c,ref bool mortal)
 {
  try{var npc=c.bodyNPC;mortal=npc!=null&&mortalHostage!=null&&npc.Pointer==mortalHostage.Pointer;}catch(Exception){mortal=false;}
  if(mortal)Bootstrap.Write("HOSTAGE "+mortalHostage!.name+" let go of: wounded to death, he falls lifeless");
 }
 partial void FinishRelease(NPC npc,ref bool handled)
 {
  if(mortalHostage==null||npc.Pointer!=mortalHostage.Pointer)return;
  var by=mortalBy;mortalHostage=null;handled=true;
  try
  {
   if(npc.actorStatus==ActorStatus.Dead)return;
   npc.currentHP=0;npc.Die(by,true);
   Bootstrap.Write("HOSTAGE "+npc.name+" dies of his wounds on the floor (shot by "+by.Faction+")");
  }
  catch(Exception ex){handled=false;CombatError(ex);}
 }
 private void CombatError(Exception ex)
 {if(Time.realtimeSinceStartup<nextCombatError)return;nextCombatError=Time.realtimeSinceStartup+5;Bootstrap.Warn("HOSTAGE COMBAT: "+ex.Message);}
}
