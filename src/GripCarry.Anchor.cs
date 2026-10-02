using System;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class GripCarry
{
 private Transform? anchoredBody,anchorPivot,anchorNeck,anchorNeckEnd;private Quaternion anchorTilt=Quaternion.identity,anchorYaw=Quaternion.identity;
 private bool anchorHostage;
 private Quaternion standingTilt=Quaternion.identity;
 private bool anchorReady;private float nextAnchorError;
 // Runs as soon as the native pickup attaches the NPC, using the exact head
 // pose about to be rendered. Keep native parent/drop events intact.
 internal void RenderBody()
 {
  try
  {
   UpdateBodySize();
   var npc=carrier?.bodyNPC;var body=npc==null?null:npc.transform;
   if(body==null||(!HoldingBody&&!AttachedForPickup(body))||rig.Scripted||!rig.HeadTrackingValid){anchorReady=false;return;}
   bool hostage=carrier!.TryCast<PlayerHostageController>()!=null;
   if(body!=anchoredBody||!anchorReady)
   {
    anchoredBody=body;anchorPivot=anchorNeck=anchorNeckEnd=null;anchorHostage=hostage;
    if(hostage){var bones=NpcBones.Of(npc!,out _);if(bones?.Usable==true){anchorPivot=bones.Head;anchorNeck=bones.neckBase??bones.Head;anchorNeckEnd=bones.neckTop??bones.Head;}}
    if(anchorPivot==null)foreach(var c in body.GetComponentsInChildren(Il2CppType.Of<SkinnedMeshRenderer>(),true))
    {var skin=c.TryCast<SkinnedMeshRenderer>();if(skin?.rootBone!=null&&skin.rootBone.IsChildOf(body)){anchorPivot=skin.rootBone;break;}}
    if(anchorPivot==null||!anchorPivot.IsChildOf(body)){anchorPivot=null;return;}
    var q=body.rotation;anchorYaw=Quaternion.Euler(0,rig.HeadRotation.eulerAngles.y,0);
    anchorTilt=hostage?standingTilt:Quaternion.Inverse(Quaternion.Euler(0,q.eulerAngles.y,0))*q;
    anchorReady=true;
    Bootstrap.Write("CARRY HEAD ANCHOR "+npc!.name+" pivot="+anchorPivot.name+" mode="+(hostage?"hostage":"shoulder")+" hand="+(bodyRight?"right":"left"));
   }
   if(anchorPivot==null)return;
   var head=rig.HeadRotation;var yaw=CarryAnchorMath.Heading(Q(head),Q(anchorYaw));anchorYaw=U(yaw);
   var target=CarryAnchorMath.Target(V(rig.HeadPosition),yaw,hostage,bodyRight);
   var rotation=anchorYaw*anchorTilt;
   // Set rotation and physical size FIRST, then read the resulting pivot.
   // The old rigid delta assumed scale did not change under the native hand
   // bone. It also assumed an unscaled parent when rotating the root.
   body.rotation=rotation;ApplyBodySize(body);
   body.position+=new Vector3(target.X,target.Y,target.Z)-anchorPivot.position;
  }
  catch(Exception ex){anchorReady=false;if(Time.realtimeSinceStartup>=nextAnchorError){nextAnchorError=Time.realtimeSinceStartup+5;Bootstrap.Warn("CARRY head anchor: "+ex.Message);}}
 }
 // The occupied hand is drawn from the SAME neck and yaw as the NPC. A
 // controller roll cannot orbit the NPC or pull the forearm out of the hold.
 internal bool HasCarryHand(bool right)=>anchorReady&&bodyRight==right&&anchorPivot!=null&&carrier?.hasBody==true&&!releaseBody;
 internal bool TryCarryHand(bool right,out Vector3 position,out Quaternion rotation,out Vector3 elbow)
 {
  position=elbow=Vector3.zero;rotation=Quaternion.identity;
  if(!HasCarryHand(right))return false;
  var pivot=anchorHostage&&anchorNeck!=null?anchorNeck:anchorPivot!;
  var neck=anchorHostage&&anchorNeckEnd!=null?(pivot.position+anchorNeckEnd.position)*.5f:pivot.position;
  var pose=CarryHoldMath.Hand(V(neck),Q(anchorYaw),anchorHostage,right);
  position=new Vector3(pose.position.X,pose.position.Y,pose.position.Z);
  rotation=new Quaternion(pose.rotation.X,pose.rotation.Y,pose.rotation.Z,pose.rotation.W);
  elbow=new Vector3(pose.elbow.X,pose.elbow.Y,pose.elbow.Z);return true;
 }
 private static System.Numerics.Quaternion Q(Quaternion q)=>new(q.x,q.y,q.z,q.w);
 partial void BeforeBodyRelease(){RenderBody();anchorReady=false;anchoredBody=anchorPivot=null;}
}
