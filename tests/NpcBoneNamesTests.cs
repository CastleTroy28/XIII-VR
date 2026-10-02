using System;using System.Collections.Generic;using XiiiXR;
class NpcBoneNamesTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // The game's NPC skeleton (names from the log), with anchors and duplicates around it.
  var bones=new List<(string,int)>{("enm_BehindHouse_Pistol",0),("Model",1),("ROOTSHJnt",2),("Hips_SHJnt",3),("Spine_01SHJnt",4),("Spine_02SHJnt",5),("Spine_TopSHJnt",6),
   ("Neck_01SHJnt",7),("Neck_02SHJnt",8),("HeadSHJnt",9),("Head_endSHJnt",10),("JawSHJnt",10),("headArmorAnchor",10),
   ("L_Arm_ClavicleSHJnt",7),("L_Arm_ShoulderSHJnt",8),("L_Arm_ElbowSHJnt",9),("L_Arm_WristSHJnt",10),("L_Finger_01_01SHJnt",11),("leftHandAnchor",11),
   ("R_Arm_ClavicleSHJnt",7),("R_Arm_ShoulderSHJnt",8),("R_Arm_ElbowSHJnt",9),("R_Arm_WristSHJnt",10),("R_Arm_Wrist_weaponSocket",11),
   ("L_Leg_HipSHJnt",4),("L_Leg_KneeSHJnt",5),("L_Leg_AnkleSHJnt",6),("R_Leg_HipSHJnt",4),("R_Leg_KneeSHJnt",5),("weaponIKAnchor",3)};
  var f=NpcBoneNames.Match(bones);
  string N(NpcBoneNames.Role r)=>f[(int)r]<0?"none":bones[f[(int)r]].Item1;
  Check(N(NpcBoneNames.Role.SpineLower)=="Spine_01SHJnt"&&N(NpcBoneNames.Role.SpineMedium)=="Spine_02SHJnt"&&N(NpcBoneNames.Role.SpineTop)=="Spine_TopSHJnt","spine "+N(NpcBoneNames.Role.SpineLower)+"/"+N(NpcBoneNames.Role.SpineMedium)+"/"+N(NpcBoneNames.Role.SpineTop));
  Check(N(NpcBoneNames.Role.NeckBase)=="Neck_01SHJnt"&&N(NpcBoneNames.Role.NeckTop)=="Neck_02SHJnt"&&N(NpcBoneNames.Role.Head)=="HeadSHJnt"&&N(NpcBoneNames.Role.Jaw)=="JawSHJnt","neck/head "+N(NpcBoneNames.Role.Head));
  Check(N(NpcBoneNames.Role.ShoulderL)=="L_Arm_ShoulderSHJnt"&&N(NpcBoneNames.Role.ElbowL)=="L_Arm_ElbowSHJnt"&&N(NpcBoneNames.Role.WristL)=="L_Arm_WristSHJnt","left arm "+N(NpcBoneNames.Role.ShoulderL)+"/"+N(NpcBoneNames.Role.WristL));
  Check(N(NpcBoneNames.Role.ShoulderR)=="R_Arm_ShoulderSHJnt"&&N(NpcBoneNames.Role.ElbowR)=="R_Arm_ElbowSHJnt"&&N(NpcBoneNames.Role.WristR)=="R_Arm_WristSHJnt","right arm (socket skipped) "+N(NpcBoneNames.Role.WristR));
  Check(N(NpcBoneNames.Role.HipL)=="L_Leg_HipSHJnt"&&N(NpcBoneNames.Role.KneeR)=="R_Leg_KneeSHJnt","legs");
  Check(NpcBoneNames.Usable(f),"usable");
  // Common other names (a humanoid export).
  var other=new List<(string,int)>{("Hips",1),("Spine",2),("Spine1",3),("Spine2",4),("Neck",5),("Head",6),("LeftArm",5),("LeftForeArm",6),("LeftHand",7),("RightArm",5),("RightForeArm",6),("RightHand",7),("LeftUpLeg",2),("LeftLeg",3)};
  var g=NpcBoneNames.Match(other);
  string M(NpcBoneNames.Role r)=>g[(int)r]<0?"none":other[g[(int)r]].Item1;
  Check(M(NpcBoneNames.Role.SpineLower)=="Spine"&&M(NpcBoneNames.Role.SpineTop)=="Spine2"&&M(NpcBoneNames.Role.Head)=="Head"&&M(NpcBoneNames.Role.NeckBase)=="Neck","humanoid spine/head "+M(NpcBoneNames.Role.SpineTop));
  Check(M(NpcBoneNames.Role.ShoulderL)=="LeftArm"&&M(NpcBoneNames.Role.ElbowL)=="LeftForeArm"&&M(NpcBoneNames.Role.WristR)=="RightHand"&&M(NpcBoneNames.Role.HipL)=="LeftUpLeg"&&M(NpcBoneNames.Role.KneeL)=="LeftLeg","humanoid limbs "+M(NpcBoneNames.Role.ShoulderL)+"/"+M(NpcBoneNames.Role.KneeL));
  // Nothing like a skeleton: not usable.
  Check(!NpcBoneNames.Usable(NpcBoneNames.Match(new List<(string,int)>{("Mesh",1),("weaponIKAnchor",2)})),"a mesh without bones taken for a skeleton");
  Check(NpcBoneNames.Key("L_Arm_ElbowSHJnt")=="l_arm_elbow"&&NpcBoneNames.Side("l_arm_elbow")<0&&NpcBoneNames.Side("rightforearm")>0&&NpcBoneNames.Side("spine_02")==0,"keys/sides");
  Console.WriteLine("PASS: NPC bones by name (the game's SHJnt skeleton and humanoid names): spine low/mid/top, neck, head, jaw, arms, legs; anchors/sockets/ends skipped.");
 }
}
