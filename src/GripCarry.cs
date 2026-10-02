using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class GripCarry:IDisposable
{
 internal static GripCarry? Current;
 private readonly CameraRig rig;private readonly CarryGripState state=new();
 private readonly Harmony patches=new("xiii.vr.xrbootstrap.carry");
 private readonly Il2CppReferenceArray<Collider> nearby=new(96);
 private readonly System.Collections.Generic.HashSet<int> checkedNpcs=new();private float nextBodyFind,nextReject;
 private Transform? player;private PlayerEquipableInventory? inventory;private PlayerEquipableHandler? handler;
 private Equipable? item;private PlayerCarryAIController? carrier;private PlayerCarryAIController? releasing;
 private PlayMagic.AI.NPC? pendingKnockout;private float knockoutAfter;private bool knockoutRequested;
 private bool dropping,releaseBody,bodyRight;private ulong bodyButton=HandControls.Grip;private float carryStarted,nextHaptic;private int frame=-1;private float nextBind,releaseUntil;
 internal bool Consumed{get;private set;}
 internal bool Owns=>item!=null;
 // 0.1.151: the hand whose grip took the thing (either hand; the one holding it once it is in a hand).
 private int pressSide=-1;
 internal void PressedBy(int side){pressSide=side;}
 internal int HoldSide(int main)
 {
  if(item!=null&&WeaponHands.Current!=null&&inventory?.currentEquipable!=null&&inventory.currentEquipable.Pointer==item.Pointer)return WeaponHands.Current.PrimaryLeft?0:1;
  return pressSide>=0?pressSide:main;
 }
 internal Transform? SpawnBone=>carrier?.bodySpawnBone;
 internal Transform? BodyRoot=>carrier==null||carrier.bodyNPC==null?null:carrier.bodyNPC.transform;
 internal bool CarriesNpc=>(carrier!=null&&(carrier.hasBody||carrier.isInTransition))||(releasing!=null&&releasing.isInTransition);
 internal bool HidesLeft=>CarriesNpc&&!bodyRight;
 internal bool HidesHand(bool right)=>CarriesNpc&&bodyRight==right;
 internal bool HoldingBody=>carrier!=null&&carrier.hasBody&&!carrier.isInTransition&&!releaseBody;
 private int nativeDropDepth;
 internal bool NativeDropInput=>nativeDropDepth>0;
 internal GripCarry(CameraRig rig)
 {
  this.rig=rig;Current=this;InstallBodyAnchor();InstallHostageCombat();
  patches.Patch(AccessTools.DeclaredMethod(typeof(PlayerEquipableInventory),"SetPositionAndDropObject"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(DropPosition)));
  foreach(var type in new[]{typeof(PlayerHostageController),typeof(PickupBodiesController)})
   patches.Patch(AccessTools.DeclaredMethod(type,"Update"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(BeginNativeDrop)),finalizer:new HarmonyMethod(typeof(GripCarry),nameof(EndNativeDrop)));
 }
 // The stock carry Update reads Interact to drop its NPC. Suppress that read
 // only; the same right grip must still reach doors, buttons and other actors.
 private static void BeginNativeDrop(PlayerCarryAIController __instance,out GripCarry? __state)
 {
  __state=null;var c=Current;
  if(c?.carrier==null||c.carrier.Pointer!=__instance.Pointer)return;
  c.nativeDropDepth++;__state=c;
 }
 private static Exception? EndNativeDrop(Exception? __exception,GripCarry? __state)
 {if(__state!=null)__state.nativeDropDepth--;return __exception;}
 private static void DropPosition(PlayerEquipableInventory __instance,ref Vector3 position)
 {
  var c=Current;if(c==null||c.inventory==null||c.inventory.Pointer!=__instance.Pointer||(!c.dropping&&Time.realtimeSinceStartup>c.releaseUntil))return;
  c.NoteDropSeen();
  // 0.1.150: where the hand holding it is (0.1.151: the one that took it).
  bool main=c.HoldSide(WeaponHands.LeftHanded?0:1)==1;
  if(c.rig.SampleWorldHands(out var left,out var right,out bool leftValid)&&(main||leftValid))
  {var hand=main?right:left;var p=CameraRig.UnityPosition(hand);var q=ControllerAim.Rotation(hand);if(ContactRig.Current?.ResolveHand(main,false,ref p,ref q)!=false)position=p;}
 }
 internal bool IsBodyTarget(RaycastSystem ray)
 {
  var target=ray.RaycastHittable;if(target==null||handler==null||!ray.hasRayHit)return false;
  return handler.pickupBodiesController?.CanTakeBody(target,ray.rayHit.point)==true||handler.playerHostageController?.CanTakeHostage(target)==true;
 }
 internal void Tick(Transform? root,HandControls input,bool allowed,RaycastSystem? ray,bool leftAllowed)
 {
  if(frame==Time.frameCount)return;frame=Time.frameCount;Consumed=false;
  if(root!=player){Release();SettleAll();player=root;inventory=null;handler=null;state.Reset();nextBind=0;}
  if(root==null)return;
  if(Time.realtimeSinceStartup>=nextBind&&(inventory==null||handler==null))
  {
   nextBind=Time.realtimeSinceStartup+1;
   foreach(var component in root.GetComponentsInChildren(Il2CppType.Of<PlayerEquipableHandler>(),true))
   {var h=component.TryCast<PlayerEquipableHandler>();if(h!=null&&h.GetOwner().IsPlayer&&!h.GetOwner().IsInvalid){handler=h;inventory=h.inventory;break;}}
  }
  bool own=item!=null;
  state.Sample((input.Down&HandControls.Grip)!=0,(input.Held&HandControls.Grip)!=0,(input.Up&HandControls.Grip)!=0,Time.realtimeSinceStartup,own,allowed&&input.Valid,(int)WeaponHands.GripMode);
  if(own)Consumed=true;
  if(state.CanAdopt&&item==null&&inventory?.currentEquipable!=null&&inventory.currentEquipable.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental)
  {item=inventory.currentEquipable;RememberTaken(item);Bootstrap.Write("GRIP prop acquired "+item.identifier+" ("+(pressSide==0?"left":"right")+" grip; "+(WeaponHands.GripMode==WeaponGripMode.Hold?"held while the grip is held, let go: dropped":WeaponHands.GripMode==WeaponGripMode.Toggle?"the next grip press lets go":"a long grip press lets go")+")");}
  if(item!=null&&(inventory==null||inventory.currentEquipable==null||inventory.currentEquipable.Pointer!=item.Pointer)){item=null;state.Reset();}
  if(state.ShouldRelease(item!=null,inventory?.isInTransit==true))ReleaseItem();
  CheckDrop();
  Settle();
  TickBody(leftAllowed);
 }
 private void TickBody(bool allowed)
 {
  if(releasing!=null)
  {
   if(releasing.isInTransition)return;
   if(pendingKnockout!=null&&Time.realtimeSinceStartup<knockoutAfter)return;
   var npc=pendingKnockout;pendingKnockout=null;releasing=null;
   // 0.1.191: a hostage shot in the hands dies now, once on the floor (GripCarry.Combat).
   bool handled=false;if(npc!=null)FinishRelease(npc,ref handled);
   if(!handled&&npc!=null&&npc.actorStatus==PlayMagic.AI.ActorStatus.Conscious&&handler!=null)
    npc.Knockout(handler.GetOwner(),true);
  }
  var input=rig.LeftControls;bool held=input.Valid&&(input.Held&HandControls.Grip)!=0;
  bool tracked=rig.SampleWorldHands(out var left,out _,out bool lv)&&lv;
  if(carrier!=null)
  {
   // 0.1.98: a hostage taken with a trigger is held by that trigger.
   var hold=bodyRight?rig.RightControls:rig.LeftControls;bool holding=hold.Valid&&(hold.Held&bodyButton)!=0;
   // Native hostage/carry state may lock ordinary interaction during its animation.
   bool safe=WindowFocus.Playable&&rig.HeadTrackingValid&&(tracked||bodyRight)&&!rig.Scripted&&GameUiControls.Current?.BlocksGameplay!=true&&Time.timeScale>0;
   if(!releaseBody&&(!holding||!safe)){releaseBody=true;knockoutRequested=safe&&hold.Valid&&!holding&&carrier.TryCast<PlayerHostageController>()!=null;}
   // 0.1.167: a hostage who dies
   // or is knocked out in the hands is let go of at once (not knocked out
   // again); the grip still held does not take him up again.
   if(!releaseBody&&carrier.hasBody&&!carrier.isInTransition&&carrier.TryCast<PlayerHostageController>()!=null)
   {
    PlayMagic.AI.NPC? npc=null;var status=PlayMagic.AI.ActorStatus.Conscious;
    try{npc=carrier.bodyNPC;if(npc!=null)status=npc.actorStatus;}catch(Exception){npc=null;}
    if(npc==null||status!=PlayMagic.AI.ActorStatus.Conscious)
    {
     releaseBody=true;knockoutRequested=false;
     Bootstrap.Write("HOSTAGE "+(npc==null?"gone":status==PlayMagic.AI.ActorStatus.Dead?"died":"knocked out ("+status+")")+" in the hands: let go at once");
    }
   }
   if(releaseBody&&!carrier.isInTransition){ReleaseBody(knockoutRequested);return;}
   if(!carrier.hasBody&&!carrier.isInTransition&&Time.realtimeSinceStartup-carryStarted>5){ReleaseBody();return;}
   if(safe&&holding&&Time.realtimeSinceStartup>=nextHaptic){nextHaptic=Time.realtimeSinceStartup+.32f;rig.PunchHaptics(bodyRight);}
   return;
  }
  if(!allowed||!tracked||!held||handler==null||WeaponHands.Current?.HandFree(false)==false||InteractionDriver.Current?.HandOccupied(false)==true){return;}
  var p=CameraRig.UnityPosition(left);
  if(Time.realtimeSinceStartup<nextBodyFind&&(input.Down&HandControls.Grip)==0)return;
  nextBodyFind=Time.realtimeSinceStartup+.08f;checkedNpcs.Clear();
  int count=Physics.OverlapSphereNonAlloc(p,.25f,nearby,~0,QueryTriggerInteraction.Collide);
  for(int i=0;i<Math.Min(count,nearby.Length);i++)
  {
   var c=nearby[i];var npc=c?.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())?.TryCast<PlayMagic.AI.NPC>();if(npc==null||!checkedNpcs.Add(npc.GetInstanceID()))continue;
   var target=c!.GetComponentInParent(Il2CppType.Of<NPCHittable>())?.TryCast<IRaycastHittable>();
   target??=npc.GetComponentInChildren(Il2CppType.Of<NPCHittable>(),true)?.TryCast<IRaycastHittable>();if(target==null)continue;
   var bodies=handler.pickupBodiesController;var hostages=handler.playerHostageController;
   if((input.Down&HandControls.Grip)!=0&&bodies!=null&&CarryTargetValidation.Body(bodies,target,p))
   {BeginBody(bodies,npc);bodies.TakeBody(npc,npc.transform.parent);return;}
   if(hostages!=null&&HostageAllowed(hostages,target,npc,p,out string why,(input.Down&HandControls.Grip)!=0))
   {BeginBody(hostages,npc);BeforeHostage(npc);hostages.TakeHostage(npc,npc.transform.parent,false);if(why.Length>0)Bootstrap.Write("LEFT GRIP hostage "+npc.name+" ("+why+")");return;}
   if(Time.realtimeSinceStartup>=nextReject){nextReject=Time.realtimeSinceStartup+3;Bootstrap.Write("LEFT GRIP target="+npc.name+" native carry rejected status="+npc.actorStatus+(hostages==null?"":" "+HostageReport(hostages,npc)));}
  }
 }

 // 0.1.112: the native check also wants the character's body (not the
 // head) behind the NPC and the camera aimed at it. In VR the player walks
 // up behind the NPC and simply grabs: accept when the NPC is in the game's
 // own state for a hostage, faces away from the player's head and the head
 // is close. Empty why: the native check passed.
 // 0.1.168: an enemy the player disarmed (NpcHitReactions) is taken from any
 // side, whatever its AI is doing; its fight and stun end first (TakingHostage).
 internal static Func<PlayMagic.AI.NPC,bool>? Disarmed;
 internal static Action<PlayMagic.AI.NPC>? TakingHostage;
 private static void BeforeHostage(PlayMagic.AI.NPC npc){try{TakingHostage?.Invoke(npc);}catch(Exception ex){Bootstrap.Warn("HOSTAGE fight end: "+ex.Message);}}
 // Only on a fresh press with the hand on him: a fist held closed while
 // punching him (the grip) must not take him.
 private bool HostageAllowed(PlayerHostageController hostages,IRaycastHittable target,PlayMagic.AI.NPC npc,Vector3 hand,out string why,bool pressed)
 {
  why="";
  if(CarryTargetValidation.Hostage(hostages,target,hand))return true;
  if(npc.actorStatus!=PlayMagic.AI.ActorStatus.Conscious)return false;
  bool disarmed=false;if(pressed)try{disarmed=Disarmed?.Invoke(npc)==true;}catch(Exception){disarmed=false;}
  if(disarmed){why="VR: disarmed by the player, any side";return true;}
  bool state=false;try{state=hostages.IsAIInCorrectState(npc);}catch(Exception){state=false;}
  if(!state)return false;
  var head=rig.HeadPosition;var t=npc.transform;
  if(!HostageMath.Behind(V(t.forward),V(t.position),V(head)))return false;
  why="VR: behind, unaware state";return true;
 }
 private static System.Numerics.Vector3 V(Vector3 v)=>new(v.x,v.y,v.z);
 private string HostageReport(PlayerHostageController hostages,PlayMagic.AI.NPC npc)
 {
  string R(Func<bool> f){try{return f()?"yes":"no";}catch(Exception){return "?";}}
  var head=rig.HeadPosition;var t=npc.transform;
  return "state="+R(()=>hostages.IsAIInCorrectState(npc))+" nativeFacing="+R(()=>hostages.IsFacingAwayFromPlayer(npc))+" nativeDistance="+R(()=>hostages.IsInValidHostageTakedownDistance(npc))
   +" headBehind="+R(()=>HostageMath.Behind(V(t.forward),V(t.position),V(head)));
 }
 // 0.1.98: trigger on an empty hand near a conscious NPC (BodyGrab).
 internal bool TryTriggerHostage(bool right,PlayMagic.AI.NPC npc,Collider c,Vector3 p)
 {
  if(carrier!=null||releasing!=null||handler==null)return false;
  var target=c.GetComponentInParent(Il2CppType.Of<NPCHittable>())?.TryCast<IRaycastHittable>();
  target??=npc.GetComponentInChildren(Il2CppType.Of<NPCHittable>(),true)?.TryCast<IRaycastHittable>();
  var hostages=handler.playerHostageController;
  if(target==null||hostages==null||!HostageAllowed(hostages,target,npc,p,out _,true))return false;
  BeginBody(hostages,npc);bodyRight=right;bodyButton=HandControls.Trigger;
  BeforeHostage(npc);hostages.TakeHostage(npc,npc.transform.parent,false);
  Bootstrap.Write((right?"RIGHT":"LEFT")+" TRIGGER hostage "+npc.name);
  return true;
 }
 partial void InstallBodyAnchor();
 partial void InstallHostageCombat();
 // 0.1.191: a hostage wounded to death in the hands: whether he is let go of now, and his death on the floor.
 partial void MortalRelease(PlayerCarryAIController c,ref bool mortal);
 partial void FinishRelease(PlayMagic.AI.NPC npc,ref bool handled);
 partial void RememberBodySize(PlayMagic.AI.NPC npc);
 private void BeginBody(PlayerCarryAIController c,PlayMagic.AI.NPC npc)
 {
  carrier=c;carryStarted=Time.realtimeSinceStartup;releaseBody=false;nextHaptic=0;bodyRight=false;bodyButton=HandControls.Grip;
  RememberBodySize(npc);
  // Keep the authored bodySpawnBone and native arms/hostage animation.
  Bootstrap.Write("LEFT GRIP native body/hostage pickup requested");
 }
 partial void BeforeBodyRelease();
 // 0.1.193: a hostage let go of drops at once (GripCarry.Release.cs); done=false: the game's own release.
 partial void DropAtOnce(PlayerCarryAIController c,PlayMagic.AI.NPC npc,ref bool done);
 private void ReleaseBody(bool knock=false)
 {
  BeforeBodyRelease();
  bool mortal=false;if(carrier!=null)MortalRelease(carrier,ref mortal);
  try{if(carrier!=null&&carrier.hasBody)
  {
   var npc=carrier.bodyNPC;releasing=carrier;pendingKnockout=knock||mortal?npc:null;
   // 0.1.193: a hostage drops at once (GripCarry.Release.cs); a body carried, or on failure, the game's own release.
   bool now=false;if(npc!=null)DropAtOnce(carrier,npc,ref now);
   knockoutAfter=Time.realtimeSinceStartup+(now?0:.15f);
   if(!now&&carrier.bodyNPC!=null)carrier.ReleaseBody();
  }}
  finally{carrier=null;releaseBody=false;knockoutRequested=false;}
 }
 private void ReleaseItem()
 {
  try{if(item!=null&&inventory!=null&&inventory.currentEquipable!=null&&inventory.currentEquipable.Pointer==item.Pointer)
   {
    var e=item;string name=e.identifier;
    // 0.1.158: the mod puts it down at
    // once, where the hand holds it, falling straight down. The game's own
    // drop only when the thing it was taken from is not known.
    bool done=false;string how="";
    PutDown(e,ref done,ref how);
    if(done)Bootstrap.Write("GRIP prop "+name+" let go: put down where the hand held it"+how);
    else{dropping=true;releaseUntil=Time.realtimeSinceStartup+1;WatchDrop(e);inventory.TryDropCurrentEquipableAndAmmo();Bootstrap.Write("GRIP prop "+name+" let go: dropped by the game"+how);}
   }}
  finally{dropping=false;item=null;pressSide=-1;state.Released();}
 }
 // 0.1.156: the game keeping a thing let go of: GripCarry.Drop.cs.
 partial void WatchDrop(Equipable e);
 partial void CheckDrop();
 partial void RememberTaken(Equipable e);
 partial void NoteDropSeen();
 // 0.1.158: put down by the mod (GripCarry.Drop.cs); done=false: the game's drop.
 partial void PutDown(Equipable e,ref bool done,ref string how);
 // 0.1.158: a thing put down does not collide with the player until it is clear of him.
 partial void Settle();
 partial void SettleAll();
 private void Release(){pendingKnockout=null;releasing=null;try{ReleaseBody();}finally{ReleaseItem();}}
 internal void Cancel()=>Release();
 internal void Forget(Equipable consumed){if(item!=null&&item.Pointer==consumed.Pointer){item=null;state.Released();Consumed=false;}}
 public void Dispose(){try{Release();SettleAll();}finally{patches.UnpatchSelf();if(Current==this)Current=null;}}
}
