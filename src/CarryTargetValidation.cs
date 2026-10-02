using UnityEngine;
namespace XiiiXR;
// Native CanTakeHostage reads playerRaycast in addition to its target argument.
// Supply the left contact only for this synchronous validation, restoring the
// right-hand ray even if native code throws. Never bypass native NPC rules.
internal static class CarryTargetValidation
{
 internal static bool Hostage(PlayerHostageController controller,IRaycastHittable target,Vector3 contact)
 {
  var ray=controller.playerRaycast;if(ray==null)return false;
  var saved=ray.raycastHittable;var hit=ray.rayHit;bool had=ray.hasRayHit;
  try{ray.raycastHittable=target;ray.hasRayHit=true;ray.rayHit=Contact(controller,contact);return controller.CanTakeHostage(target);}
  finally{ray.raycastHittable=saved;ray.rayHit=hit;ray.hasRayHit=had;}
 }
 internal static bool Body(PickupBodiesController controller,IRaycastHittable target,Vector3 contact)
 {
  var ray=controller.playerRaycast;if(ray==null)return false;
  var saved=ray.raycastHittable;var hit=ray.rayHit;bool had=ray.hasRayHit;
  try{ray.raycastHittable=target;ray.hasRayHit=true;ray.rayHit=Contact(controller,contact);return controller.CanTakeBody(target,contact);}
  finally{ray.raycastHittable=saved;ray.rayHit=hit;ray.hasRayHit=had;}
 }
 private static RaycastHit Contact(PlayerCarryAIController controller,Vector3 point)=>new(){point=point,
  distance=controller.playerCameraPosition==null?0:(point-controller.playerCameraPosition.position).magnitude};
}
