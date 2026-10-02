using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class GripCarry
{
 // 0.1.156: the game sometimes keeps the thing
 // (its drop refused). Asked again twice; then the mod puts it down itself.
 // 0.1.158:
 // the mod puts it down at once, without the game's drop: the thing it was
 // taken from (the game's pickup) where the thing is drawn in the hand, still,
 // falling straight down. It does not touch the player's body until it is
 // clear of it (overlapping the body, the physics shoved it away). The game's
 // drop (and the retries) only when that pickup is not known.
 private Equipable? dropCheck;private float dropCheckAt;private int dropTries;private bool dropSeen;private EnvironmentalPickup? taken;
 partial void WatchDrop(Equipable e){dropSeen=false;dropCheck=e;dropCheckAt=Time.realtimeSinceStartup+.35f;dropTries=1;}
 partial void RememberTaken(Equipable e){taken=TakenFrom(e,4);}
 partial void NoteDropSeen(){dropSeen=true;}
 partial void PutDown(Equipable e,ref bool done,ref string how)
 {
  var pickup=taken;
  if(pickup==null)pickup=taken=TakenFrom(e,30);
  if(pickup==null){how=" (the thing it was taken from was not found)";return;}
  if(!PutDownNow(e,pickup,out how))return;
  done=true;dropCheck=null;taken=null;
 }
 partial void CheckDrop()
 {
  if(dropCheck==null||Time.realtimeSinceStartup<dropCheckAt)return;
  var e=dropCheck;
  // Taken again meanwhile: it stays.
  if(item!=null&&item.Pointer==e.Pointer){dropCheck=null;return;}
  if(inventory==null||inventory.currentEquipable==null||inventory.currentEquipable.Pointer!=e.Pointer){dropCheck=null;return;}
  if(inventory.isInTransit){dropCheckAt=Time.realtimeSinceStartup+.2f;return;}
  if(dropTries<3)
  {
   dropTries++;dropCheckAt=Time.realtimeSinceStartup+.35f;
   dropping=true;releaseUntil=Time.realtimeSinceStartup+1;
   try{inventory.TryDropCurrentEquipableAndAmmo();}catch(Exception ex){Bootstrap.Warn("GRIP prop drop: "+ex.Message);}finally{dropping=false;}
   Bootstrap.Write("GRIP prop "+e.identifier+": the game kept it"+(dropSeen?" (it placed a dropped thing, yet it is still in the hand)":" (it placed nothing)")+"; asked to drop it again ("+dropTries+")");dropSeen=false;return;
  }
  dropCheck=null;
  var pickup=taken??TakenFrom(e,30);taken=null;
  string how="";
  if(pickup==null){how="; the thing it was taken from was not found";TakeOutOfHands(e);}
  else if(!PutDownNow(e,pickup,out how))TakeOutOfHands(e);
  Bootstrap.Write("GRIP prop "+e.identifier+": the game did not let go of it; put down by the mod"+how);
 }
 // The pickup where the thing is drawn in the hand, falling; the thing out of the hands.
 private bool PutDownNow(Equipable e,EnvironmentalPickup pickup,out string how)
 {
  var center=Vector3.zero;bool drawn=false;
  var hands=WeaponHands.Current;
  if(hands!=null)drawn=hands.TryHeldPropCenter(e,out center);
  if(!drawn)center=HandPoint();
  try{how=(drawn?"":" (at the hand: the thing was not drawn)")+Place(pickup,center,e);}
  catch(Exception ex){how=" (the mod could not put it down: "+ex.Message+")";return false;}
  TakeOutOfHands(e);
  return true;
 }
 private void TakeOutOfHands(Equipable e)
 {
  if(WeaponHands.Current?.DismissProp(e)==true)return;
  try{inventory?.RemoveAndSwitch(e);}catch(Exception ex){Bootstrap.Warn("GRIP prop out of the hands: "+ex.Message);}
 }
 private Vector3 HandPoint()
 {
  bool main=HoldSide(WeaponHands.LeftHanded?0:1)==1;
  if(rig.SampleWorldHands(out var left,out var right,out bool leftValid)&&(main||leftValid))return CameraRig.UnityPosition(main?right:left);
  return rig.HeadPosition+rig.HeadRotation*Vector3.forward*.5f;
 }
 private string Place(EnvironmentalPickup pickup,Vector3 center,Equipable e)
 {
  string name=e.identifier;
  var go=pickup.gameObject;
  if(!go.activeSelf)go.SetActive(true);
  pickup._wasPicked_k__BackingField=false;
  if(pickup.visualParent!=null&&!pickup.visualParent.activeSelf)pickup.visualParent.SetActive(true);
  try{pickup.SetAllColliders(true);pickup.SetGravityInfluence(true);pickup.MarkAsDrop();}catch(Exception){}
  var t=go.transform;
  // Its pivot may be off its middle: its middle goes where the drawn thing's middle was.
  var offset=MiddleOf(pickup,out bool measured)-t.position;
  if(!measured)offset=Vector3.zero;
  string wall="";
  var at=Unblocked(center,pickup,ref wall);var p=at-offset;
  t.position=p;
  var rb=pickup.CachedRigidbody;
  if(rb!=null)
  {
   rb.isKinematic=false;rb.useGravity=true;rb.position=p;rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
   // Never shoved out of something fast (a wall it was let go of in).
   if(rb.maxDepenetrationVelocity>1.5f)rb.maxDepenetrationVelocity=1.5f;
   rb.WakeUp();
  }
  // 0.1.161: one with no physics body (the broom) stayed where it was let go of, standing in the air: it falls flat.
  string lay=rb==null?Lay(pickup,e,at):"";
  int pairs=IgnorePlayer(pickup,name,p);
  return "; the thing it was taken from falls from there"+wall+(rb==null?" (it has no physics body"+lay+")":"")+(pairs>0?"; it passes through the player's body until clear of it":"");
 }
 // The middle of what is drawn of it (meshes; not its effects), else of its colliders.
 private static Vector3 MiddleOf(EnvironmentalPickup pickup,out bool measured)
 {
  measured=false;var b=new Bounds();
  var root=pickup.visualParent!=null?pickup.visualParent:pickup.gameObject;
  foreach(var c in root.GetComponentsInChildren(Il2CppType.Of<Renderer>(),false))
  {
   var r=c.TryCast<Renderer>();if(r==null||!r.enabled||r.TryCast<MeshRenderer>()==null&&r.TryCast<SkinnedMeshRenderer>()==null)continue;
   if(!measured){b=r.bounds;measured=true;}else b.Encapsulate(r.bounds);
  }
  if(measured)return b.center;
  foreach(var c in pickup.GetComponentsInChildren(Il2CppType.Of<Collider>(),false))
  {
   var k=c.TryCast<Collider>();if(k==null||!k.enabled||k.isTrigger)continue;
   if(!measured){b=k.bounds;measured=true;}else b.Encapsulate(k.bounds);
  }
  return b.center;
 }
 private readonly Il2CppStructArray<RaycastHit> wallHits=new(32);
 // Not through a wall: from the head to where it was drawn.
 private Vector3 Unblocked(Vector3 center,EnvironmentalPickup pickup,ref string note)
 {
  var head=rig.HeadPosition;var d=center-head;float length=d.magnitude;if(length<.05f)return center;
  var direction=d/length;float nearest=length;
  int n=Physics.RaycastNonAlloc(head,direction,wallHits,length,~0,QueryTriggerInteraction.Ignore);
  for(int i=0;i<Math.Min(n,wallHits.Length);i++)
  {
   var hit=wallHits[i];var c=hit.collider;if(c==null)continue;
   if(player!=null&&c.transform.IsChildOf(player)||c.transform.IsChildOf(pickup.transform))continue;
   if(c.attachedRigidbody!=null&&!c.attachedRigidbody.isKinematic)continue;
   if(hit.distance<nearest)nearest=hit.distance;
  }
  if(nearest>=length)return center;
  note=" (short of a wall it was held in)";
  return head+direction*Math.Max(0,nearest-.2f);
 }
 private sealed class Settling
 {
  internal EnvironmentalPickup? Pickup;internal string Name="";internal Vector3 Placed;internal float At,Next;internal bool Reported;
  internal readonly List<Collider> Own=new(),Body=new();
 }
 private readonly List<Settling> settling=new();
 private int IgnorePlayer(EnvironmentalPickup pickup,string name,Vector3 placed)
 {
  if(player==null)return 0;
  var s=new Settling{Pickup=pickup,Name=name,Placed=placed,At=Time.realtimeSinceStartup,Next=Time.realtimeSinceStartup+.25f};
  foreach(var c in pickup.GetComponentsInChildren(Il2CppType.Of<Collider>(),false))
  {var k=c.TryCast<Collider>();if(k!=null&&k.enabled&&!k.isTrigger&&s.Own.Count<16)s.Own.Add(k);}
  foreach(var c in player.GetComponentsInChildren(Il2CppType.Of<Collider>(),false))
  {var k=c.TryCast<Collider>();if(k!=null&&k.enabled&&!k.isTrigger&&s.Body.Count<24)s.Body.Add(k);}
  int pairs=0;
  foreach(var a in s.Own)foreach(var b in s.Body){try{Physics.IgnoreCollision(a,b,true);pairs++;}catch(Exception){}}
  if(pairs==0)return 0;
  // The same thing put down again: one entry.
  for(int i=settling.Count-1;i>=0;i--)if(settling[i].Pickup==null||settling[i].Pickup!.Pointer==pickup.Pointer)settling.RemoveAt(i);
  settling.Add(s);
  while(settling.Count>8){Restore(settling[0]);settling.RemoveAt(0);}
  return pairs;
 }
 partial void Settle()
 {
  TickFalls();
  if(settling.Count==0)return;
  float now=Time.realtimeSinceStartup;
  for(int i=settling.Count-1;i>=0;i--)
  {
   var s=settling[i];if(now<s.Next)continue;s.Next=now+.25f;
   var pickup=s.Pickup;
   bool gone=pickup==null||!pickup.gameObject.activeInHierarchy||pickup._wasPicked_k__BackingField;
   if(!gone&&!s.Reported&&now-s.At>1.5f)
   {
    s.Reported=true;var p=pickup!.transform.position;var moved=new Vector2(p.x-s.Placed.x,p.z-s.Placed.z).magnitude;
    if(moved>.6f)Bootstrap.Write("GRIP prop "+s.Name+" put down: it went "+moved.ToString("F2")+" m sideways from where it was let go");
   }
   if(!gone&&Overlaps(s))continue;
   Restore(s);settling.RemoveAt(i);
  }
 }
 partial void SettleAll(){foreach(var s in settling)Restore(s);settling.Clear();falls.Clear();landKnock?.Dispose();landKnock=null;}
 private static bool Overlaps(Settling s)
 {
  foreach(var a in s.Own)
  {
   if(a==null)continue;var own=a.bounds;var ab=new Bounds(own.center,own.size+Vector3.one*.2f);
   foreach(var b in s.Body)if(b!=null&&ab.Intersects(b.bounds))return true;
  }
  return false;
 }
 private static void Restore(Settling s)
 {
  foreach(var a in s.Own)foreach(var b in s.Body)
   if(a!=null&&b!=null)try{Physics.IgnoreCollision(a,b,false);}catch(Exception){}
 }
 // The game's pickup this thing came from: of its kind, taken (hidden), nearest (within meters).
 private EnvironmentalPickup? TakenFrom(Equipable e,float meters)
 {
  try
  {
   EnvironmentalPickup? best=null;float nearest=meters*meters;var at=player!=null?player.position:rig.HeadPosition;
   foreach(var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<EnvironmentalPickup>()))
   {
    var p=o.TryCast<EnvironmentalPickup>();if(p==null||!p.gameObject.scene.IsValid()||p.weaponPrefab==null||p.weaponPrefab.identifier!=e.identifier)continue;
    bool gone=p._wasPicked_k__BackingField||!p.gameObject.activeInHierarchy||p.visualParent!=null&&!p.visualParent.activeInHierarchy;
    if(!gone)continue;
    float d=(p.transform.position-at).sqrMagnitude;if(d<nearest){nearest=d;best=p;}
   }
   return best;
  }
  catch(Exception){return null;}
 }
}
