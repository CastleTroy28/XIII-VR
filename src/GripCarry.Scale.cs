using System;
using HarmonyLib;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class GripCarry
{
 private CarryBodyScale? bodySize;private PlayerCarryAIController? sizeCarrier;
 private bool sizeReported;
 partial void InstallBodyAnchor()
 {
  // The base drop clears bodyNPC. Save the snapshot in the prefix so the
  // finalizer can restore even after that reference is gone (or on failure).
  foreach(var type in new[]{typeof(PlayerCarryAIController),typeof(PlayerHostageController),typeof(PickupBodiesController)})
   patches.Patch(AccessTools.DeclaredMethod(type,"HandleBodyDropAnimationComplete"),prefix:new HarmonyMethod(typeof(GripCarry),nameof(BeginSizeRestore)),finalizer:new HarmonyMethod(typeof(GripCarry),nameof(EndSizeRestore)));
 }
 partial void RememberBodySize(PlayMagic.AI.NPC npc)
 {
  try
  {
   bodySize?.Restore();bodySize=new CarryBodyScale(npc.transform);sizeCarrier=carrier;sizeReported=false;
   anchorReady=false;anchoredBody=anchorPivot=null;
   var q=npc.transform.rotation;standingTilt=Quaternion.Inverse(Quaternion.Euler(0,q.eulerAngles.y,0))*q;
   Bootstrap.Write("CARRY SIZE saved "+npc.name+" local="+npc.transform.localScale.ToString("F4")+" worldAxes="+bodySize.OriginalWorldAxes.ToString("F4"));
  }
  catch(Exception ex){bodySize=null;sizeCarrier=null;Bootstrap.Warn("CARRY size capture: "+ex.Message);}
 }
 private void UpdateBodySize()
 {
  if(bodySize==null)return;
  if(sizeCarrier==null||sizeCarrier.bodyNPC==null||sizeCarrier.bodyNPC.transform!=bodySize.Body)
  {bodySize.Restore();bodySize=null;sizeCarrier=null;return;}
  ApplyBodySize(bodySize.Body);
 }
 private static void BeginSizeRestore(PlayerCarryAIController __instance,out CarryBodyScale? __state)
 {
  var c=Current;__state=c?.sizeCarrier!=null&&c.sizeCarrier.Pointer==__instance.Pointer?c.bodySize:null;
 }
 private static Exception? EndSizeRestore(Exception? __exception,CarryBodyScale? __state)
 {
  if(__state!=null)try
  {
   __state.Restore();var c=Current;
   if(c!=null&&ReferenceEquals(c.bodySize,__state))
   {
    // An exception may leave the native body attached: keep maintaining its
    // size until native carry actually releases it, instead of losing state.
    if(c.sizeCarrier==null||c.sizeCarrier.bodyNPC==null){c.bodySize=null;c.sizeCarrier=null;}
    Bootstrap.Write("CARRY SIZE drop restored "+(__state.Body==null?"destroyed NPC":__state.Body.name));
   }
  }
  catch(Exception ex){Bootstrap.Warn("CARRY size restore: "+ex.Message);}
  return __exception;
 }
 private bool AttachedForPickup(Transform body)
 {
  var spawn=carrier?.bodySpawnBone;
  return carrier!=null&&carrier.hasBody&&!releaseBody&&spawn!=null&&body.IsChildOf(spawn);
 }
 private void ApplyBodySize(Transform body)
 {
  if(bodySize==null||bodySize.Body!=body)return;
  var before=CarryBodyScale.Axes(body);bool ok=bodySize.Apply();
  if(ok&&!sizeReported&&((before-bodySize.OriginalWorldAxes).sqrMagnitude>1e-8f||HoldingBody||AttachedForPickup(body)))
  {
   sizeReported=true;
   Bootstrap.Write("CARRY SIZE anchored "+body.name+" before="+before.ToString("F4")+" after="+CarryBodyScale.Axes(body).ToString("F4")+" target="+bodySize.OriginalWorldAxes.ToString("F4")+" parent="+(body.parent==null?"none":body.parent.name));
  }
 }
}
