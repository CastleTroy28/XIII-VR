using System;using System.Collections.Generic;using XiiiXR;using PlayMagic.AI;using UnityEngine;using N=System.Numerics;
class HostageCombatTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var npc=new NPC();npc.torsoColliders.Add(new CapsuleCollider{center=new(0,1,.5f),radius=.18f,height=.8f});
  npc.headCollider=new SphereCollider{center=new(0,1.6f,.5f),radius=.11f};
  var c=new GripCarry(npc);var player=new PlayerState();
  Check(c.PatchCount==9,"hostage combat hooks incomplete");
  var shooter=new NPC();var agent=new AITargetingAgent{CurrentTarget=c.Holder,ownerInstanceID=71};AIManager.Instance.Npcs[71]=shooter;
  // 0.1.205: police, FBI, guards hold fire at the holder even where the game would shoot; until he fires himself.
  foreach(var faction in new[]{NPC.Faction.FBI,NPC.Faction.Military,NPC.Faction.SPADS,NPC.Faction.Civilian,NPC.Faction.Government,NPC.Faction.NonPlayer,NPC.Faction.None})
  {shooter.faction=faction;agent.CurrentTargetWrapper.hostageFaction=NPC.Faction.None;Check(c.Prevents(agent)&&c.Prevents(agent,false),"non-bandit fires at the hostage's holder");}
  agent.CurrentTargetWrapper.hasFiredWhileHavingHostage=true;Check(!c.Prevents(agent,false)&&c.Prevents(agent,true),"after he fired the game no longer decides");agent.CurrentTargetWrapper.hasFiredWhileHavingHostage=false;
  shooter.faction=NPC.Faction.Terrorist;Check(!c.Prevents(agent)&&!c.Prevents(agent,true),"bandit still refuses to fire");
  // 0.1.205: one that came after the hostage was taken (the bank's guards) is told, as the game tells, and listens from then on.
  {
   var guard=new Enemy{faction=NPC.Faction.SPADS,name="guard"};var late=new AITargetingAgent{CurrentTarget=c.Holder,ownerInstanceID=72};guard.Agent=late;AIManager.Instance.Npcs[72]=guard;
   Check(c.Prevents(late,false)&&guard.Observed==1&&guard.Told==1&&late.CurrentTargetWrapper.hostageFaction==npc.faction,"a guard that came later not told of the hostage (or fires)");
   Check(c.Known(late)&&c.Prevents(late,false)&&guard.Observed==1&&guard.Told==1,"a guard told twice");
   var listening=new Enemy{faction=NPC.Faction.FBI,name="agent"};var heard=new AITargetingAgent{CurrentTarget=c.Holder,ownerInstanceID=73};listening.Agent=heard;AIManager.Instance.Npcs[73]=listening;
   listening.observedPlayerHostageControllers.Add(c.Controller);
   Check(c.Known(heard)&&listening.Observed==0&&listening.Told==1,"a listening shooter subscribed twice (or not told)");
   var deaf=new Enemy{faction=NPC.Faction.Military,name="deaf",Deaf=true};var stubborn=new AITargetingAgent{CurrentTarget=c.Holder,ownerInstanceID=74};deaf.Agent=stubborn;AIManager.Instance.Npcs[74]=deaf;
   Check(c.Prevents(stubborn,false)&&stubborn.CurrentTargetWrapper.hostageFaction==npc.faction&&stubborn.SetStates==1,"a shooter its own handler leaves unaware: its target's hostage not set");
   var bandit=new Enemy{faction=NPC.Faction.Terrorist,name="bandit"};var bad=new AITargetingAgent{CurrentTarget=c.Holder,ownerInstanceID=75};bandit.Agent=bad;AIManager.Instance.Npcs[75]=bandit;
   Check(!c.Prevents(bad,true)&&bandit.Told==0&&bad.CurrentTargetWrapper.hostageFaction==NPC.Faction.None,"a bandit told of the hostage");
   Console.WriteLine("PASS: 0.1.205 police, FBI and guards hold fire at a live hostage's holder (the game decides again once he fired with him); one that came after the hostage was taken is told as the game tells and listens from then on, once; one already listening is not subscribed again; bandits are not told and keep firing; every bullet at the holder (past the hostage's side, over him, from behind) goes into the hostage's body.");
  }
  agent.CurrentTarget=new PlayerEntity();Check(c.Prevents(agent),"different target affected");agent.CurrentTarget=c.Holder;
  c.Transition=true;Check(c.Prevents(agent),"pickup transition changes AI");c.Transition=false;
  var bullet=new DamageInfo(DamageType.Ballistic);var owner=new OwnerInfo(71,false,NPC.Faction.Terrorist,true,false,17);
  Check(!c.Shot(player,new(0,1,3),new(0,1,0),new(0,0,-1),owner,bullet),"front hit not replaced by body hit");
  Check(npc.CurrentHP==90&&npc.DamageCalls==1&&npc.LastArea==NPC.DamageArea.Body,"body did not receive one native hit");
  Check(npc.LastHitReceived.instigator.Faction==owner.Faction&&npc.LastHitReceived.instigator.Id==71&&npc.LastHitReceived.instigator.NetworkActorNumber==17&&npc.LastHitReceived.instigator.IsAlly,"real shooter attribution lost");
  Check(npc.AIAudioHandler.Voices==1,"native and fallback voice doubled");
  Check(npc.Held&&npc.faction==NPC.Faction.Terrorist&&!GripCarry.HasDamageScope,"carry/faction/damage scope leaked");
  // 0.1.205: past his side, over his head, from behind.
  foreach(var hit in new[]{new Vector3(.35f,1,0),new Vector3(0,2,0)})
   Check(!c.Shot(player,hit+new Vector3(0,0,3),hit,new(0,0,-1),owner,bullet),"a bullet past the hostage hit the player");
  Check(!c.Shot(player,new(0,1,-3),new(0,1,0),new(0,0,1),owner,bullet),"a bullet from behind hit the player");
  Check(npc.DamageCalls==4&&npc.LastArea==NPC.DamageArea.Body&&npc.CurrentHP==60&&npc.actorStatus==ActorStatus.Conscious,"bullets past the hostage not taken by his body");
  Check(c.Shot(player,new(0,1,3),new(0,1,0),new(0,0,-1),owner,new DamageInfo(DamageType.Explosive)),"explosion blocked");
  Check(c.Shot(player,new(0,1,3),new(0,1,0),new(0,0,-1),new OwnerInfo(1,true,NPC.Faction.Player,false,false,0),bullet),"player shot redirected");
  var otherPlayer=new PlayerState{owner=new OwnerInfo(99,true,NPC.Faction.Player,false,false,0)};
  Check(c.Shot(otherPlayer,new(0,1,3),new(0,1,0),new(0,0,-1),owner,bullet),"other player protected");
  npc.RefuseDamage=true;Check(c.Shot(player,new(0,1,3),new(0,1,0),new(0,0,-1),owner,bullet),"rejected damage grants invulnerability");npc.RefuseDamage=false;
  npc.ThrowDamage=true;Check(c.Shot(player,new(0,1,3),new(0,1,0),new(0,0,-1),owner,bullet)&&!GripCarry.HasDamageScope,"native exception leaked damage bypass");npc.ThrowDamage=false;
  // Direct bullet contact with the hostage also enters the scoped native path.
  npc.AIAudioHandler.Cooldown=false;float hpBefore=npc.CurrentHP;GripCarry.SimulateDamage(npc,10,bullet,owner);
  Check(npc.CurrentHP==hpBefore-10&&npc.AIAudioHandler.Voices==2,"direct NPC hit or voice missing");
  var unrelated=new NPC();GripCarry.SimulateDamage(unrelated,10,bullet,owner);Check(unrelated.CurrentHP==100,"unrelated held NPC made vulnerable");
  // 0.1.191: a hit that would kill him leaves him alive in the hands (a sliver of life), he still shields and cries out.
  npc.CurrentHP=5;npc.AIAudioHandler.Cooldown=false;int voices=npc.AIAudioHandler.Voices;
  Check(!c.Shot(player,new(0,1,3),new(0,1,0),new(0,0,-1),owner,bullet)&&npc.actorStatus==ActorStatus.Conscious&&npc.CurrentHP==1,"a fatal shot killed the hostage in the hands");
  Check(npc.AIAudioHandler.Voices==voices+1&&c.MortalIs(npc)&&!npc.receivedDamagesAreAlwaysLethal&&!GripCarry.HasDamageScope,"the wounded hostage does not cry out / not remembered / guard leaked");
  Check(!c.Shot(player,new(0,1,3),new(0,1,0),new(0,0,-1),owner,bullet)&&npc.actorStatus==ActorStatus.Conscious&&npc.CurrentHP==1&&npc.DamageCalls>=3,"a hostage wounded to death stops shielding or dies in the hands");
  // A hostage whose every hit is lethal (the game's flag) lives too; the flag is given back.
  npc.receivedDamagesAreAlwaysLethal=true;npc.AlwaysLethalKills=true;GripCarry.SimulateDamage(npc,1,bullet,owner);
  Check(npc.actorStatus==ActorStatus.Conscious&&npc.receivedDamagesAreAlwaysLethal,"an always-lethal hostage died in the hands (or the flag was lost)");npc.receivedDamagesAreAlwaysLethal=false;npc.AlwaysLethalKills=false;
  // Let go of: he dies once down, by the shooter.
  bool mortal=false;c.MortalLetGo(ref mortal);Check(mortal,"a hostage wounded to death let go of as if unhurt");
  bool handled=false;c.Down(npc,ref handled);
  Check(handled&&npc.actorStatus==ActorStatus.Dead&&npc.DiedBy.Faction==NPC.Faction.Terrorist&&npc.CurrentHP<=0&&!c.MortalIs(npc),"a hostage wounded to death does not fall lifeless when let go of");
  Check(c.Shot(player,new(0,1,3),new(0,1,0),new(0,0,-1),owner,bullet),"dead hostage remains infinite shield");
  Check(c.Prevents(agent),"dead hostage keeps AI override");
  // An unhurt hostage let go of is not killed.
  var calm=new NPC();var c2=new GripCarry(calm);mortal=true;c2.MortalLetGo(ref mortal);handled=false;c2.Down(calm,ref handled);
  Check(!mortal&&!handled&&calm.actorStatus==ActorStatus.Conscious,"an unhurt hostage killed when let go of");Console.WriteLine("PASS: 0.1.191 a held hostage is never killed by a hit (a sliver of life, the cry, still a shield; also an always-lethal one), and falls lifeless by the shooter once let go of; an unhurt one is not killed.");
  // Hurtbox mapping works even when native carry disables physics colliders.
  var body=new NPC();var cap=new CapsuleCollider{center=new(0,1,0),radius=.2f,height=.8f,enabled=false};body.torsoColliders.Add(cap);
  cap.transform.position=new(0,0,.7f);cap.transform.localScale=new(2,1,.5f);cap.transform.rotation=new(N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitY,.7f));
  var boxes=new HostageHurtboxes();boxes.Bind(body);Check(boxes.Intersect(new(0,1,3),new(0,1,0),out var collider,out _,out var area)&&collider==cap&&area==NPC.DamageArea.Body,"disabled scaled rotated native capsule not shielded");
  cap.gameObject.activeInHierarchy=false;Check(!boxes.Intersect(new(0,1,3),new(0,1,0),out _,out _,out _),"inactive hurtbox shields");
  Console.WriteLine("PASS: production combat hooks + hurtboxes; exact bandit faction/target; scoped held and friendly-fire bypass; shooter attribution; direct + intercepted bullets; single damage/voice; miss/back/exposed player; player/other-player/explosion exclusion; native exception cleanup; rejected damage; death and disabled/scaled/rotated hurtboxes. Native damage and Unity are simulated.");
 }
}
namespace XiiiXR
{
 internal sealed partial class GripCarry
 {
  internal static GripCarry? Current;private readonly CameraRig rig=new();private bool releaseBody=false;
  private readonly HarmonyLib.Harmony patches=new();private PlayerCarryAIController? carrier;private PlayerEquipableHandler? handler=new();
  partial void InstallHostageCombat();internal int PatchCount=>patches.Count;
  partial void MortalRelease(PlayerCarryAIController c,ref bool mortal);partial void FinishRelease(NPC npc,ref bool handled);
  internal bool MortalIs(NPC n)=>mortalHostage==n;internal void MortalLetGo(ref bool m)=>MortalRelease(carrier!,ref m);internal void Down(NPC n,ref bool h)=>FinishRelease(n,ref h);
  internal GripCarry(NPC npc){Current=this;carrier=new PlayerHostageController{bodyNPC=npc};InstallHostageCombat();}
  internal PlayerEntity Holder=>((PlayerHostageController)carrier!).owningPlayerEntity!;
  internal bool Transition{set=>carrier!.isInTransition=value;}
  internal bool Prevents(AITargetingAgent a){bool result=true;HostageAttack(a,ref result);return result;}
  // The game's own answer `native` where the mod lets it decide.
  internal bool Prevents(AITargetingAgent a,bool native){bool result=native;return HostageAttack(a,ref result)?native:result;}
  internal bool Known(AITargetingAgent a){bool result=false;return HostageKnown(a,ref result)?a.CurrentTargetWrapper.hostageFaction!=NPC.Faction.None:result;}
  internal PlayerHostageController Controller=>(PlayerHostageController)carrier!;
  internal bool Shot(PlayerState p,Vector3 from,Vector3 hit,Vector3 direction,OwnerInfo who,DamageInfo info)=>ShieldPlayer(p,10,info,1,hit,who,from,direction);
  internal static bool HasDamageScope=>hostageDamage!=null;
  internal void RenderBody(){}
  private static N.Vector3 V(Vector3 v)=>v.N;
  internal static void SimulateDamage(NPC npc,float damage,DamageInfo info,OwnerInfo who)
  {
   BeginHostageDamage(npc,damage,info,ref who,out var state);Exception? error=null;
   try
   {
    bool held=npc.Held;DamageHeldCheck(npc,ref held);
    if(held||who.Faction==npc.faction||npc.RefuseDamage)return;
    if(npc.ThrowDamage)throw new Exception("native damage failure");
    npc.CurrentHP-=damage;npc.DamageCalls++;
    KeepInstigator(npc.LastHitReceived,ref who);npc.LastHitReceived.instigator=who;
    if(npc.CurrentHP<=0||npc.receivedDamagesAreAlwaysLethal&&npc.AlwaysLethalKills)npc.actorStatus=ActorStatus.Dead;
    if(npc.actorStatus==ActorStatus.Conscious)npc.AIAudioHandler.PlayHurtSound(npc,npc.LastHitReceived);
   }
   catch(Exception ex){error=ex;throw;}
   finally{if(EndHostageDamage(error,state)!=error)throw new Exception("native exception changed");}
  }
 }
 class CameraRig{internal bool Scripted=false,HeadTrackingValid=true;}
 static class SceneScan{internal static int Epoch=1;}
 // The middle of the hurtbox (enough to tell which side the shooter is on).
 static class ColliderSurface{internal static bool TryClosest(Collider c,Vector3 p,out Vector3 q){q=c.transform.TransformPoint(c is CapsuleCollider k?k.center:c is SphereCollider s?s.center:new Vector3(0,0,0));return true;}}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
}
namespace HarmonyLib
{
 class Harmony{internal int Count;internal void Patch(object method,HarmonyMethod? prefix=null,HarmonyMethod? finalizer=null){Count++;}}
 class HarmonyMethod{internal HarmonyMethod(Type t,string name){}}
 static class AccessTools{internal static object DeclaredMethod(Type t,string name)=>name;internal static object PropertyGetter(Type t,string name)=>name;}
}
class ITargetable{internal readonly IntPtr Pointer=(IntPtr)(++next);static int next;}
class PlayerEntity:ITargetable{}
class Enemy:Combatant
{
 internal List<PlayerHostageController> observedPlayerHostageControllers=new();internal int Observed,Told;internal AITargetingAgent? Agent;internal bool Deaf;
 internal void ObserveHostageController(PlayerHostageController c,bool state){Observed++;if(state)observedPlayerHostageControllers.Add(c);}
 internal void HandlePlayerTakingHostage(ITargetable t,NPC.Faction f){Told++;if(!Deaf&&Agent!=null)Agent.CurrentTargetWrapper.hostageFaction=f;}
}
class PlayerCarryAIController{static int next;internal readonly IntPtr Pointer=(IntPtr)(++next);internal NPC? bodyNPC;internal bool hasBody=>bodyNPC!=null;internal bool isInTransition;internal T? TryCast<T>() where T:class=>this as T;}
class PlayerHostageController:PlayerCarryAIController{internal PlayerEntity? owningPlayerEntity=new();}
class PlayerEquipableHandler{internal OwnerInfo GetOwner()=>new(1,true,NPC.Faction.Player,false,false,0);}
class PlayerState{internal OwnerInfo owner=new(1,true,NPC.Faction.Player,false,false,0);}
class Civilian:NPC{}
[Flags]enum DamageType{Melee=16,Explosive=32,Ballistic=64}
class DamageInfo{internal DamageType type;internal DamageInfo(DamageType t){type=t;}}
readonly struct OwnerInfo
{
 internal readonly int Id,NetworkActorNumber;internal readonly bool IsPlayer,IsAlly,IsInvalid;internal readonly NPC.Faction Faction;
 internal OwnerInfo(int id,bool isPlayer,NPC.Faction faction,bool isAlly,bool isInvalid,int networkActorNumber)
 {Id=id;IsPlayer=isPlayer;Faction=faction;IsAlly=isAlly;IsInvalid=isInvalid;NetworkActorNumber=networkActorNumber;}
}
namespace PlayMagic.AI
{
 enum ActorStatus{Conscious=1,Dead=3}
 class NPC
 {
  internal enum Faction{None=0,Player=2,FBI=4,Terrorist=8,SPADS=16,Military=32,Civilian=64,Government=52,NonPlayer=124}
  internal enum DamageArea{Body,Head,UpperLimb,Unknown,LowerLimb}
  static int next;internal readonly IntPtr Pointer=(IntPtr)(++next);internal string name="hostage";internal T? TryCast<T>() where T:class=>this as T;internal int GetInstanceID()=>(int)Pointer;internal Faction faction=Faction.Terrorist;
  internal ActorStatus actorStatus=ActorStatus.Conscious;internal float currentHP=100;internal float CurrentHP{get=>currentHP;set=>currentHP=value;}internal bool Held=true,RefuseDamage,ThrowDamage,receivedDamagesAreAlwaysLethal,AlwaysLethalKills;internal int DamageCalls;
  internal OwnerInfo DiedBy;internal void Die(OwnerInfo who,bool ragdolls){actorStatus=ActorStatus.Dead;DiedBy=who;}
  internal HitInfo LastHitReceived=new();internal AIAudioHandler AIAudioHandler=new();internal DamageArea LastArea;
  internal Collider? headCollider;internal List<Collider> torsoColliders=new(),upperLimbColliders=new(),lowerLimbColliders=new();
  internal void SaveColliderReferences(bool force){}internal NPC GetDamageReceiver()=>this;internal bool CanReceiveDamage()=>true;
  internal void ReceiveDamage(float damage,DamageInfo info,float force,Collider box,Vector3 point,OwnerInfo who,Vector3 origin,Vector3 direction,DamageArea area)
  {LastArea=area;GripCarry.SimulateDamage(this,damage,info,who);}
 }
 class Combatant:NPC{}
 class AITargetingAgent
 {
  internal ITargetable? CurrentTarget;internal int ownerInstanceID;internal TargetableWrapper CurrentTargetWrapper=new();internal int SetStates;
  internal void SetHostageState(ITargetable t,NPC.Faction f){SetStates++;CurrentTargetWrapper.hostageFaction=f;}
  internal class TargetableWrapper{internal NPC.Faction hostageFaction;internal bool hasFiredWhileHavingHostage;}
 }
 class AIManager{internal static AIManager Instance=new();internal Dictionary<int,NPC> Npcs=new();internal NPC? GetNPCByID(int id)=>Npcs.TryGetValue(id,out var n)?n:null;}
 class HitInfo{internal readonly IntPtr Pointer=(IntPtr)(++next);static int next;internal OwnerInfo instigator;internal float hpLost=0;}
 class AIAudioHandler{internal int Voices;internal bool Cooldown;internal void PlayHurtSound(NPC n,HitInfo hit){if(!Cooldown){Voices++;Cooldown=true;}}}
}
namespace UnityEngine
{
 struct Vector3
 {
  internal N.Vector3 N;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal Vector3(N.Vector3 v){N=v;}
  internal float x=>N.X;internal float y=>N.Y;internal float z=>N.Z;internal float magnitude=>N.Length();internal float sqrMagnitude=>N.LengthSquared();
  internal Vector3 normalized=>new(System.Numerics.Vector3.Normalize(N));internal static Vector3 right=>new(1,0,0);internal static Vector3 up=>new(0,1,0);internal static Vector3 forward=>new(0,0,1);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.N+b.N);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.N-b.N);public static Vector3 operator*(Vector3 a,float f)=>new(a.N*f);
 }
 struct Quaternion{internal N.Quaternion N;internal Quaternion(N.Quaternion n){N=n;}}
 class GameObject{internal bool activeInHierarchy=true;}
 class Transform
 {
  internal Vector3 position=new(0,0,0),localScale=new(1,1,1);internal Quaternion rotation=new(N.Quaternion.Identity);internal Vector3 lossyScale=>localScale;
  N.Matrix4x4 Matrix=>N.Matrix4x4.CreateScale(localScale.N)*N.Matrix4x4.CreateFromQuaternion(rotation.N)*N.Matrix4x4.CreateTranslation(position.N);
  internal Vector3 TransformPoint(Vector3 p)=>new(N.Vector3.Transform(p.N,Matrix));internal Vector3 TransformDirection(Vector3 p)=>new(N.Vector3.Transform(p.N,rotation.N));
  internal Vector3 InverseTransformPoint(Vector3 p){N.Matrix4x4.Invert(Matrix,out var inv);return new(N.Vector3.Transform(p.N,inv));}
 }
 class Collider{static int next;private readonly int id=++next;internal Transform transform=new();internal GameObject gameObject=new();internal bool enabled=true;internal int GetInstanceID()=>id;internal T? TryCast<T>() where T:class=>this as T;internal Bounds bounds=>new(){center=transform.position};}
 struct Bounds{internal Vector3 center;}
 class CapsuleCollider:Collider{internal Vector3 center;internal float radius,height;internal int direction=1;}
 class SphereCollider:Collider{internal Vector3 center;internal float radius;}
 class BoxCollider:Collider{internal Vector3 center=new(0,0,0),size=new(1,1,1);}
 static class Time{internal static float realtimeSinceStartup=1;}
}
