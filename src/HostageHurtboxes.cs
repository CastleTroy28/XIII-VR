using System;
using System.Collections.Generic;
using PlayMagic.AI;
using UnityEngine;
namespace XiiiXR;
internal sealed class HostageHurtboxes
{
 private readonly List<(Collider collider,NPC.DamageArea area)> boxes=new();
 private readonly HashSet<int> seen=new();private NPC? owner;
 internal int Count=>boxes.Count;
 internal void Bind(NPC npc)
 {
  if(owner!=null&&owner.Pointer==npc.Pointer&&boxes.Count>0)return;
  owner=npc;boxes.Clear();seen.Clear();npc.SaveColliderReferences(false);
  void Add(Collider? c,NPC.DamageArea area){if(c!=null&&seen.Add(c.GetInstanceID()))boxes.Add((c,area));}
  Add(npc.headCollider,NPC.DamageArea.Head);
  var torso=npc.torsoColliders;if(torso!=null)foreach(var c in torso)Add(c,NPC.DamageArea.Body);
  var arms=npc.upperLimbColliders;if(arms!=null)foreach(var c in arms)Add(c,NPC.DamageArea.UpperLimb);
  var legs=npc.lowerLimbColliders;if(legs!=null)foreach(var c in legs)Add(c,NPC.DamageArea.LowerLimb);
 }
 internal bool Intersect(Vector3 from,Vector3 to,out Collider? collider,out Vector3 point,out NPC.DamageArea area)
 {
  collider=null;point=to;area=NPC.DamageArea.Unknown;float nearest=1;
  foreach(var entry in boxes)
  {
   var c=entry.collider;if(c==null||!c.gameObject.activeInHierarchy)continue;
   // Native carry may disable a hurtbox or move it to IgnoreRaycast. Read
   // its bone/shape directly; do not change any live collision flags/layers.
   var t=c.transform;float h;bool hit=false;
   var capsule=c.TryCast<CapsuleCollider>();var sphere=c.TryCast<SphereCollider>();var box=c.TryCast<BoxCollider>();
   if(capsule!=null)
   {
    var axis=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
    var scale=t.lossyScale;float sx=Math.Abs(scale.x),sy=Math.Abs(scale.y),sz=Math.Abs(scale.z);
    float along=capsule.direction==0?sx:capsule.direction==1?sy:sz;
    float across=capsule.direction==0?Math.Max(sy,sz):capsule.direction==1?Math.Max(sx,sz):Math.Max(sx,sy);
    float radius=capsule.radius*across,half=Math.Max(0,capsule.height*along*.5f-radius);
    var center=t.TransformPoint(capsule.center);var delta=t.TransformDirection(axis).normalized*half;
    hit=HostageShieldMath.Capsule(N(from),N(to),N(center-delta),N(center+delta),radius,out h);
   }
   else if(sphere!=null)
   {
    var s=t.lossyScale;float radius=sphere.radius*Math.Max(Math.Abs(s.x),Math.Max(Math.Abs(s.y),Math.Abs(s.z)));
    hit=HostageShieldMath.Sphere(N(from),N(to),N(t.TransformPoint(sphere.center)),radius,out h);
   }
   else if(box!=null)hit=HostageShieldMath.Box(N(t.InverseTransformPoint(from)),N(t.InverseTransformPoint(to)),N(box.center),N(box.size),out h);
   else continue;
   if(!hit||h>=nearest)continue;
   nearest=h;collider=c;area=entry.area;
  }
  if(collider==null)return false;point=from+(to-from)*nearest;return true;
 }
 // 0.1.205: the hurtbox a bullet that missed his shape still goes into: his
 // body's (else his head's, an arm's, a leg's) nearest to where it came from,
 // at its side towards it.
 internal bool Facing(Vector3 from,out Collider? collider,out Vector3 point,out NPC.DamageArea area)
 {
  collider=null;point=from;area=NPC.DamageArea.Unknown;int bestRank=int.MaxValue;float bestDistance=float.PositiveInfinity;
  foreach(var entry in boxes)
  {
   var c=entry.collider;if(c==null||!c.gameObject.activeInHierarchy)continue;
   int rank=HostageShieldMath.ShieldRank((int)entry.area);if(rank>bestRank)continue;
   var at=ColliderSurface.TryClosest(c,from,out var closest)?closest:c.bounds.center;
   float d=(at-from).sqrMagnitude;if(!float.IsFinite(d))continue;
   if(rank==bestRank&&d>=bestDistance)continue;
   bestRank=rank;bestDistance=d;collider=c;point=at;area=entry.area;
  }
  return collider!=null;
 }
 private static System.Numerics.Vector3 N(Vector3 v)=>new(v.x,v.y,v.z);
}
