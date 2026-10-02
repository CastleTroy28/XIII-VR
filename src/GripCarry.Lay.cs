using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.161: a thing with no physics body (the game's broom stands in the
// level without one) falls by the mod: from how it was held, level along the
// floor, onto the floor below it (LayFlatMath), and knocks when it lands if
// it is wooden.
internal sealed partial class GripCarry
{
 private sealed class Falling
 {
  internal Transform? T;internal Vector3 From,To;internal Quaternion FromTurn,ToTurn;internal float At,Height;internal string Name="";internal bool Wooden;
 }
 private readonly List<Falling> falls=new();
 private readonly Il2CppStructArray<RaycastHit> floorHits=new(32);
 private ChairImpactClip? landKnock;
 private static System.Numerics.Vector3 N(Vector3 v)=>new(v.x,v.y,v.z);
 private static Vector3 U(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);
 private static System.Numerics.Quaternion N(Quaternion q)=>new(q.x,q.y,q.z,q.w);
 private static Quaternion U(System.Numerics.Quaternion q)=>new(q.X,q.Y,q.Z,q.W);
 // Starts the fall; "" when it cannot (it then stays where it was let go of).
 private string Lay(EnvironmentalPickup pickup,Equipable e,Vector3 middle)
 {
  var t=pickup.transform;var turn=t.rotation;
  if(!Box(pickup,t,out var min,out var max))return "; its size unknown: left there";
  var size=max-min;int axis=LayFlatMath.LongAxis(N(size));var along=LayFlatMath.Axis(axis);
  float upOfPositive=(turn*U(along)).y;
  int head=LayFlatMath.HeadEnd(upOfPositive,axis==0?min.x:axis==1?min.y:min.z,axis==0?max.x:axis==1?max.y:max.z);
  var localHead=U(along*head);
  // As it was held: its head the way the drawn thing's head pointed.
  var start=turn;
  if(WeaponHands.Current?.TryHeldPropHead(e,out var heldHead)==true)start=U(System.Numerics.Quaternion.Normalize(LayFlatMath.FromTo(N(turn*localHead),N(heldHead))*N(turn)));
  var heading=start*localHead;if(new Vector2(heading.x,heading.z).sqrMagnitude<.01f)heading=rig.HeadRotation*Vector3.forward;
  // Only a long thing lies down; anything else lands as it stood.
  float other=Math.Max(axis==0?size.y:size.x,axis==2?size.y:size.z);
  bool elongated=size[axis]>2*other;
  if(!elongated)start=turn;
  var end=elongated?U(LayFlatMath.Level(N(start),N(localHead),N(heading))):turn;
  var mid=(min+max)*.5f;
  var from=middle-start*mid;
  if(!Floor(middle,pickup,out float floor))return "; no floor found below it: left there";
  var to=U(LayFlatMath.Rest(N(min),N(max),N(end),floor+.005f,middle.x,middle.z));
  float height=Math.Max(0,(from+start*mid).y-(to+end*mid).y);
  t.SetPositionAndRotation(from,start);
  for(int i=falls.Count-1;i>=0;i--)if(falls[i].T==null||falls[i].T!.Pointer==t.Pointer)falls.RemoveAt(i);
  falls.Add(new Falling{T=t,From=from,To=to,FromTurn=start,ToTurn=end,At=Time.realtimeSinceStartup,Height=height,Name=e.identifier,Wooden=MeleeDamageMath.WoodenProp(e.identifier)});
  return "; it falls flat onto the floor "+height.ToString("F2")+" m below (its long side "+size[axis].ToString("F2")+" m)";
 }
 // Its box about its root, in its own frame, in world units (from its meshes, else its box colliders).
 private static bool Box(EnvironmentalPickup pickup,Transform root,out Vector3 min,out Vector3 max)
 {
  var lo=Vector3.one*float.PositiveInfinity;var hi=Vector3.one*float.NegativeInfinity;bool any=false;
  var inverse=Quaternion.Inverse(root.rotation);var origin=root.position;
  void Add(Transform at,Bounds b)
  {
   for(int i=0;i<8;i++)
   {
    var c=new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z);
    var v=inverse*(at.TransformPoint(c)-origin);lo=Vector3.Min(lo,v);hi=Vector3.Max(hi,v);any=true;
   }
  }
  var visuals=pickup.visualParent!=null?pickup.visualParent:pickup.gameObject;
  foreach(var c in visuals.GetComponentsInChildren(Il2CppType.Of<MeshFilter>(),false))
  {var f=c.TryCast<MeshFilter>();var m=f?.sharedMesh;if(f!=null&&m!=null)Add(f.transform,m.bounds);}
  foreach(var c in visuals.GetComponentsInChildren(Il2CppType.Of<SkinnedMeshRenderer>(),false))
  {var r=c.TryCast<SkinnedMeshRenderer>();var m=r?.sharedMesh;if(r!=null&&m!=null)Add(r.transform,m.bounds);}
  if(!any)
   foreach(var c in pickup.GetComponentsInChildren(Il2CppType.Of<BoxCollider>(),false))
   {var b=c.TryCast<BoxCollider>();if(b!=null&&!b.isTrigger)Add(b.transform,new Bounds(b.center,b.size));}
  min=lo;max=hi;
  return any&&float.IsFinite(min.x)&&(max-min).sqrMagnitude>1e-6f;
 }
 // The floor below a point (not the player, not the thing itself, not a loose thing).
 private bool Floor(Vector3 at,EnvironmentalPickup pickup,out float y)
 {
  y=0;float nearest=float.PositiveInfinity;
  int n=Physics.RaycastNonAlloc(at+Vector3.up*.3f,Vector3.down,floorHits,4.3f,~0,QueryTriggerInteraction.Ignore);
  for(int i=0;i<Math.Min(n,floorHits.Length);i++)
  {
   var hit=floorHits[i];var c=hit.collider;if(c==null)continue;
   if(player!=null&&c.transform.IsChildOf(player)||c.transform.IsChildOf(pickup.transform))continue;
   if(c.attachedRigidbody!=null&&!c.attachedRigidbody.isKinematic)continue;
   if(hit.normal.y<.5f)continue;
   if(hit.distance<nearest){nearest=hit.distance;y=hit.point.y;}
  }
  return float.IsFinite(nearest);
 }
 private void TickFalls()
 {
  if(falls.Count==0)return;
  float now=Time.realtimeSinceStartup;
  for(int i=falls.Count-1;i>=0;i--)
  {
   var f=falls[i];var t=f.T;
   if(t==null||!t.gameObject.activeInHierarchy){falls.RemoveAt(i);continue;}
   float k=LayFlatMath.Fall(now-f.At,f.Height);
   // It tips over a little ahead of its drop (a long thing let go of turns as it falls).
   float turn=Math.Min(1,k*1.25f);
   t.SetPositionAndRotation(Vector3.Lerp(f.From,f.To,k),Quaternion.Slerp(f.FromTurn,f.ToTurn,turn));
   if(k<1)continue;
   falls.RemoveAt(i);
   if(f.Wooden)
   {
    landKnock??=new ChairImpactClip(()=>WoodKnock.Wav,"wood knock",.8f,16);
    landKnock.Play(f.To,Math.Clamp(f.Height,.3f,1f),.85f,true);
   }
   Bootstrap.Write("GRIP prop "+f.Name+" put down: it lies on the floor");
  }
 }
}
