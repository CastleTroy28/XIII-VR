using System;using System.Linq;using XiiiXR;
class ArmCropMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // The player's rig names: clavicle -> shoulder (upper arm) -> elbow -> wrist -> fingers.
  var names=new[]{"Spine_TopSHJnt","L_Arm_ClavicleSHJnt","L_Arm_ShoulderSHJnt","L_Arm_ElbowSHJnt","L_Arm_WristSHJnt","L_Finger_Index_01SHJnt","R_Arm_ClavicleSHJnt","R_Arm_ShoulderSHJnt","R_Arm_ElbowSHJnt","R_Arm_WristSHJnt"};
  var parents=new[]{-1,0,1,2,3,4,0,6,7,8};
  var keep=ArmCropMath.KeepBones(names,parents,out var how)!;
  Check(how=="elbow names","elbows not found by name");
  Check(keep.SequenceEqual(new[]{false,false,false,true,true,true,false,false,true,true}),"kept bones: "+string.Join(",",keep));
  // Other names: the wrist's parent is the forearm; a gun handle is not a hand.
  var other=new[]{"root","upArm_L","loArm_L","hand_L","m60_handle","upArm_R","loArm_R","hand_R"};
  var otherKeep=ArmCropMath.KeepBones(other,new[]{-1,0,1,2,0,0,5,6},out how)!;
  Check(how=="wrist parents"&&otherKeep.SequenceEqual(new[]{false,false,true,true,false,false,true,true}),"wrist-parent fallback: "+how+" "+string.Join(",",otherKeep??new bool[0]));
  Check(ArmCropMath.KeepBones(new[]{"root","gun"},new[]{-1,0},out how)==null&&how=="none","arms without an elbow are cut");
  // A bone chain that loops is not followed forever.
  Check(ArmCropMath.KeepBones(new[]{"a","b_elbow"},new[]{1,0},out _)!.All(x=>x),"looping parents");
  // Triangles: all three corners must follow the kept bones.
  var tris=new[]{0,1,2, 1,2,3, 2,3,4, 3,4,9};
  var weights=new[]{0f,.2f,.5f,1f,1f};
  Check(ArmCropMath.Triangles(tris,weights).SequenceEqual(new[]{2,3,4}),"triangle cut");
  Check(ArmCropMath.Triangles(tris,weights,.1f).SequenceEqual(new[]{1,2,3,2,3,4}),"threshold");
  Console.WriteLine("PASS: mounted gun arms: kept from the elbow down (by name, else the wrist's parent), shoulders/upper arms cut, bad indices and loops ignored.");
 }
}
