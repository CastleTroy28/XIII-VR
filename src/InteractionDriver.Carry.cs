using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class InteractionDriver
{
 private readonly Il2CppStructArray<RaycastHit> carryRayHits=new(64);
 private void LinkCarryInteraction()
 {
  patches.Patch(AccessTools.DeclaredMethod(typeof(PlayerArmsAnimationControl),"CanDoInteraction"),
   prefix:new HarmonyMethod(typeof(InteractionDriver),nameof(BeginCarryInteraction)),
   finalizer:new HarmonyMethod(typeof(InteractionDriver),nameof(EndCarryInteraction)));
 }
 private static void BeginCarryInteraction(PlayerArmsAnimationControl __instance,out bool __state)
 {
  __state=false;var c=Current;
  // Native CanDoInteraction also checks weapon switching, fire/reload, keys
  // and prop animations. Clear only the carry interaction latch for this
  // synchronous query, never unlock input or bypass a pickup/quest condition.
  if(c==null||!c.carry.HoldingBody||!c.Allowed()||c.root==null||!__instance.transform.IsChildOf(c.root)||!__instance.isInteracting)return;
  c.Sample();bool main=c.input.Action.Down,off=c.offInput.Action.Down;
  if(!(main||off)||c.HandOccupied(main?MainRight:OffRight)||c.BodyTarget)return;
  __state=true;__instance.isInteracting=false;
 }
 private static Exception? EndCarryInteraction(Exception? __exception,PlayerArmsAnimationControl __instance,bool __state)
 {if(__state&&__instance!=null)__instance.isInteracting=true;return __exception;}
 private void SkipCarriedNpc(RaycastSystem ray)
 {
  var body=carry.BodyRoot;
  if(!carry.HoldingBody||body==null||!ray.hasRayHit||ray.rayHit.collider==null||!ray.rayHit.collider.transform.IsChildOf(body))return;
  if(!PrepareRay(out var origin,out var rotation))return;
  int count=Physics.RaycastNonAlloc(origin,rotation*Vector3.forward,carryRayHits,ray.rayDistance,ray.rayLayerMasks,ray.queryTriggerInteraction);
  if(count>=carryRayHits.Length)return; // incomplete query: never take through a wall
  bool found=false;var closest=default(RaycastHit);float distance=float.PositiveInfinity;
  for(int i=0;i<count;i++)
  {
   var h=carryRayHits[i];var col=h.collider;if(col==null)continue;
   var t=col.transform;if(t.IsChildOf(body)||root!=null&&t.IsChildOf(root))continue;
   if(h.distance>=distance)continue;distance=h.distance;closest=h;found=true;
  }
  ray.hasRayHit=found;ray.rayHit=closest;
  if(found)ray.UpdateRaycastHittable(closest);else ray.raycastHittable=null;
 }
}
