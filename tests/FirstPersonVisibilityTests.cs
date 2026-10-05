using System;using System.Collections.Generic;using XiiiXR;using UnityEngine;using PlayMagic;
class FirstPersonVisibilityTests
{
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Main()
 {
  var player=new CustomCharacterController();var beach=new FpsMeshReference(player.transform);var bank=new FpsMeshReference(player.transform);
  player.CurrentFpsRigReference=beach;var visibility=new FirstPersonVisibility();visibility.Tick(player,true);
  Check(!beach.m_fpsMesh.enabled&&beach.m_fpsMesh.updateWhenOffscreen,"hidden beach arms stop offscreen skin updates");
  Check(beach.fpsMainAnimator.cullingMode==AnimatorCullingMode.AlwaysAnimate,"beach hand animator culled");
  player.CurrentFpsRigReference=bank;Time.realtimeSinceStartup=2;visibility.Tick(player,true);
  Check(!bank.m_fpsMesh.enabled&&bank.m_fpsMesh.updateWhenOffscreen,"bank skin kept culled after outfit switch");
  foreach(var a in new[]{bank.fpsMainAnimator,bank.fpsLeftHandAnimator,bank.animatedTarget})Check(a.cullingMode==AnimatorCullingMode.AlwaysAnimate,"outfit animator not kept alive");
  bank.fpsMainAnimator.cullingMode=AnimatorCullingMode.CullCompletely;visibility.Tick(player,true);
  Check(bank.fpsMainAnimator.cullingMode==AnimatorCullingMode.AlwaysAnimate,"subsequent game culling change not corrected");
  visibility.Tick(player,false);
  foreach(var model in new[]{beach,bank})Check(model.m_fpsMesh.enabled&&!model.m_fpsMesh.updateWhenOffscreen&&model.fpsMainAnimator.cullingMode==AnimatorCullingMode.CullCompletely,"native visibility/culling not restored");
  // 0.1.193: the separate left arm is hidden, but the grappling hook, cable and pulley the game hangs on its wrist are not (GrappleVr shows them in the VR hand).
  var player2=new CustomCharacterController();var rig2=new FpsMeshReference(player2.transform);player2.CurrentFpsRigReference=rig2;
  var leftArm=new Transform{name="rig_fps_left_arm_mesh",parent=player2.transform};rig2.fpsLeftHandAnimator.transform=leftArm;
  var wrist=new Transform{name="L_Arm_WristSHJnt",parent=leftArm};var hook=new Transform{name="eqp_grappling_hook(Clone)",parent=wrist};
  var armSkin=new SkinnedMeshRenderer{name="player_arm_GEO"};armSkin.transform=new Transform{name="player_arm_GEO",parent=leftArm};
  var hookMesh=new SkinnedMeshRenderer{name="eqp_grappling_hook_mesh"};hookMesh.transform=new Transform{name="eqp_grappling_hook_mesh",parent=hook};
  var cable=new Renderer{name="eqp_grappling_hook_cable_mesh"};cable.transform=new Transform{name="eqp_grappling_hook_cable_mesh",parent=hook};
  leftArm.children.Add(armSkin);leftArm.children.Add(hookMesh);leftArm.children.Add(cable);
  var visibility2=new FirstPersonVisibility();visibility2.Tick(player2,true);
  Check(!armSkin.enabled,"the separate left arm not hidden");
  Check(hookMesh.enabled&&cable.enabled,"the grappling hook on the left arm's wrist hidden with the arm (gone from the hand once fired)");
  Check(FirstPersonVisibility.Gadget("eqp_grappling_hook_pulley_mesh")&&!FirstPersonVisibility.Gadget("player_arm_GEO")&&!FirstPersonVisibility.Gadget(null),"grappling hook parts not told from the arm");
  Console.WriteLine("PASS: 0.1.193 the separate left arm hidden, the grappling hook, its cable and pulley on its wrist left to the grapple (visible in the hand once fired).");
  Console.WriteLine("PASS: hidden native hand animation survives outfit change and offscreen culling; original flags restored");
  // 0.1.245: a weapon just selected (a chair): its first-person model is hidden at once, not up to a second later.
  {
   var p3=new CustomCharacterController();var rig3=new FpsMeshReference(p3.transform);p3.CurrentFpsRigReference=rig3;
   var v3=new FirstPersonVisibility();Time.realtimeSinceStartup=10;v3.Tick(p3,true);
   var chair=new SkinnedMeshRenderer{name="wpn_chair_mesh"};rig3.transform.children.Add(chair);
   Time.realtimeSinceStartup=10.05f;v3.Tick(p3,true);Check(chair.enabled,"fixture: the model was found before the next scan");
   FirstPersonVisibility.ScanSoon();Time.realtimeSinceStartup=10.06f;v3.Tick(p3,true);
   Check(!chair.enabled,"the selected weapon's own model still shown after the selection");
   var debris=new Renderer{name="chair_01_debris_01"};rig3.transform.children.Add(debris);
   Time.realtimeSinceStartup=10.2f;v3.Tick(p3,true);Check(!debris.enabled,"a part that came a moment later still shown");
   var late=new Renderer{name="late"};rig3.transform.children.Add(late);
   Time.realtimeSinceStartup=11.7f;v3.Tick(p3,true);Check(!late.enabled,"the scans stopped");
   var after=new Renderer{name="after"};rig3.transform.children.Add(after);
   Time.realtimeSinceStartup=11.9f;v3.Tick(p3,true);Check(after.enabled,"the quick scans did not end after a moment");
   Check(FirstPersonVisibility.NextScanDelay(1,1.5f)==FirstPersonVisibility.SoonEvery&&FirstPersonVisibility.NextScanDelay(2,1.5f)==1,"scan delays");
   Console.WriteLine("PASS: 0.1.245 a weapon just selected: its own first-person model (and parts coming a moment later) hidden at once, ten times a second for a moment; then once a second again.");
  }
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace XiiiXR{static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){throw new Exception(s);}}}
namespace UnityEngine
{
 class Object{static int next;readonly int id=++next;internal int GetInstanceID()=>id;internal T? TryCast<T>() where T:class=>this as T;}
 class Transform:Object{internal string name="";internal Transform? parent=null;internal readonly List<Renderer> children=new();internal bool IsChildOf(Transform root)=>this==root||parent?.IsChildOf(root)==true;internal Renderer[] GetComponentsInChildren(Type type,bool inactive)=>children.ToArray();}
 class Renderer:Object{internal string name="native skin";internal bool enabled=true;internal Transform transform=new();internal Object? GetComponentInParent(Type type)=>null;}
 class ParticleSystemRenderer:Renderer{}
 class SkinnedMeshRenderer:Renderer{internal bool updateWhenOffscreen=false;}
 class Animator:Object{internal AnimatorCullingMode cullingMode=AnimatorCullingMode.CullCompletely;internal Transform? transform=null;}
 enum AnimatorCullingMode{AlwaysAnimate,CullCompletely}
 static class Time{internal static float realtimeSinceStartup=0;}
}
namespace PlayMagic.AI{class NPC{}}
namespace PlayMagic
{
 class CustomCharacterController{internal Transform transform=new();internal Transform? tinyArmsMesh=null;internal FpsMeshReference? CurrentFpsRigReference=null;}
 class FpsMeshReference
 {
  internal Transform transform;internal SkinnedMeshRenderer m_fpsMesh=new();internal Animator fpsMainAnimator=new(),fpsLeftHandAnimator=new(),animatedTarget=new();
  internal FpsMeshReference(Transform player){transform=new Transform{parent=player};transform.children.Add(m_fpsMesh);}
 }
}
