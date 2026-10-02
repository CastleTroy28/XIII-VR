using System;
using UnityEngine;
namespace XiiiXR;
// The native pickup coroutine reparents the NPC to bodySpawnParent and resets
// localScale to one. That inherits the small first-person arm rig's scale.
// Capture BEFORE TakeHostage/TakeBody; never infer a human's size from a mesh
// or multiply by a fixed factor. Imported/model-specific scales stay intact.
internal sealed class CarryBodyScale
{
 internal readonly Transform Body;
 private readonly Transform? originalParent;
 private readonly Vector3 originalLocal,worldAxes;
 internal Vector3 OriginalWorldAxes=>worldAxes;
 internal CarryBodyScale(Transform body)
 {Body=body;originalParent=body.parent;originalLocal=body.localScale;worldAxes=Axes(body);}
 internal static Vector3 Axes(Transform body)=>new(body.TransformVector(Vector3.right).magnitude,body.TransformVector(Vector3.up).magnitude,body.TransformVector(Vector3.forward).magnitude);
 private static bool Axis(float local,float current,float desired,out float corrected)
 {
  corrected=local;
  if(!float.IsFinite(local)||!float.IsFinite(current)||!float.IsFinite(desired)||current<1e-6f||desired<1e-6f)return false;
  corrected=local*(desired/current);return float.IsFinite(corrected);
 }
 internal bool Apply()
 {
  if(Body==null)return false;
  var local=Body.localScale;var now=Axes(Body);
  // TransformVector measures each actual matrix column, including a rotated
  // nonuniform parent. Dividing by parent.lossyScale would distort these axes.
  if(!Axis(local.x,now.x,worldAxes.x,out float x)||!Axis(local.y,now.y,worldAxes.y,out float y)||!Axis(local.z,now.z,worldAxes.z,out float z))return false;
  var corrected=new Vector3(x,y,z);
  if((corrected-local).sqrMagnitude>1e-12f)Body.localScale=corrected;
  return true;
 }
 internal void Restore()
 {
  if(Body==null)return;
  // Native drop sets localScale=one again AFTER restoring the scene parent.
  // Restore the exact authored value only after that operation has finished.
  if(Body.parent==originalParent)Body.localScale=originalLocal;
  else Apply(); // The scene parent may have been destroyed/replaced meanwhile.
 }
}
