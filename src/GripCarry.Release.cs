using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlayMagic.AI;
using UnityEngine;
namespace XiiiXR;
// 0.1.193. The game's own release (ReleaseBody) switches the player to his
// fists, waits for them, plays a knock-out on the hostage (he stands up and
// doubles over) and only when that animation ends drops him as a knocked-out
// body (HandleBodyDropAnimationComplete: the ragdoll, its knock-out, the
// player's arms and input, the hostage-released event). A hostage let go of
// now drops at once: the NPC's own "released" event and the arms out of the
// hostage hold (what the release does first), then the game's end of the drop
// right away. No fists, no knock-out animation; the weapon in the hand stays.
// He starts falling clear of the player's body, never into a wall.
internal sealed partial class GripCarry
{
 private readonly Il2CppStructArray<RaycastHit> dropHits=new(32);
 partial void DropAtOnce(PlayerCarryAIController c,NPC npc,ref bool done)
 {
  var hostage=c.TryCast<PlayerHostageController>();if(hostage==null)return;
  string before;try{before=npc.actorStatus.ToString();}catch(Exception){before="?";}
  float push=0;
  try
  {
   bool small=false;try{small=npc.usesSmallBodyAnimations;}catch(Exception){}
   try{push=ClearOfPlayer(npc.transform);}catch(Exception ex){Bootstrap.Warn("HOSTAGE drop place: "+ex.Message);}
   try{npc.OnHostageReleasedDelegate?.Invoke(npc);}catch(Exception ex){Bootstrap.Warn("HOSTAGE released event: "+ex.Message);}
   var arms=c.playerArmsAnimationController;
   if(arms!=null)try{arms.SetBodyAnimation(true,false,small);arms.SetBodyAnimation(false,false,small);}catch(Exception ex){Bootstrap.Warn("HOSTAGE arms: "+ex.Message);}
   hostage.HandleBodyDropAnimationComplete();
  }
  catch(Exception ex){Bootstrap.Warn("HOSTAGE drop at once: "+ex.Message);}
  done=c.bodyNPC==null&&!c.isInTransition;
  string after;try{after=npc.actorStatus.ToString();}catch(Exception){after="?";}
  Bootstrap.Write("HOSTAGE "+npc.name+" let go of: "+(done?"dropped at once":"the game's own release")+" ("+before+" -> "+after+(push>0?", "+(push*100).ToString("F0")+" cm ahead clear of the player":"")+"; no fists, no knock-out animation)");
 }
 // Moves him forward (world, still under the arm) so the fall starts clear of the player.
 private float ClearOfPlayer(Transform body)
 {
  var forward=rig.HeadRotation*Vector3.forward;forward.y=0;if(forward.sqrMagnitude<1e-4f)return 0;forward.Normalize();
  var head=rig.HeadPosition;float ahead=Vector3.Dot(body.position-head,forward);
  var from=new Vector3(body.position.x,head.y-.35f,body.position.z);
  float want=CarryAnchorMath.DropPush(ahead,float.PositiveInfinity);if(want<=0)return 0;
  float free=float.PositiveInfinity;
  int n=Physics.RaycastNonAlloc(from,forward,dropHits,want+CarryAnchorMath.DropClear,~0,QueryTriggerInteraction.Ignore);
  for(int i=0;i<Math.Min(n,dropHits.Length);i++)
  {
   var hit=dropHits[i];var col=hit.collider;if(col==null)continue;
   var t=col.transform;if(t.IsChildOf(body)||player!=null&&t.IsChildOf(player))continue;
   if(col.attachedRigidbody!=null&&!col.attachedRigidbody.isKinematic)continue;
   if(hit.distance<free)free=hit.distance;
  }
  float push=CarryAnchorMath.DropPush(ahead,free);
  if(push>0)body.position+=forward*push;
  return push;
 }
}
