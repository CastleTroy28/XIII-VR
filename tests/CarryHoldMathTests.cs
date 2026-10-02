using System;using System.Numerics;using XiiiXR;
class CarryHoldMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  int cases=0;
  foreach(bool right in new[]{false,true})foreach(bool hostage in new[]{false,true})
  for(int degrees=-180;degrees<=180;degrees+=5)
  {
   var q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,degrees*MathF.PI/180);
   var neck=new Vector3(4,1.4f,-3);var hand=CarryHoldMath.Hand(neck,q,hostage,right);
   var wrist=hand.position+Vector3.Transform(new Vector3(0,0,-.065f),hand.rotation);
   var local=Vector3.Transform(wrist-neck,Quaternion.Inverse(q));
   var elbow=Vector3.Transform(hand.elbow-neck,Quaternion.Inverse(q));
   if(!hostage)Check(local.X*(right?-1:1)>0&&elbow.X*(right?-1:1)<0,"neck is not inside forearm span");
   Check(elbow.X*(right?-1:1)<0,"the elbow not on the holding side");
   Check(local.Z>.05f&&local.Z<.10f&&elbow.Z>.08f,"forearm not at front of neck");
   var direction=Vector3.Normalize(Vector3.Transform(hand.elbow-wrist,Quaternion.Inverse(hand.rotation)));
   Check(-direction.Z>MathF.Cos(35*MathF.PI/180),"wrist needs impossible bend that forearm limiter will reject");
   // 0.1.193 (0.1.191 left the palm 8 cm past the neck): a hostage's palm is on the middle of his throat (within 1.5 cm),
   // in front of it, the wrist on the near side (the forearm comes from the holding side), the fingers reaching round its far side.
   if(hostage)
   {
    var palm=Vector3.Transform(hand.position-neck,Quaternion.Inverse(q));float s=right?-1:1;
    var tips=Vector3.Transform(hand.position+Vector3.Transform(new Vector3(0,0,.09f),hand.rotation)-neck,Quaternion.Inverse(q));
    Check(MathF.Abs(palm.X)<.015f&&palm.Z>.05f&&palm.Z<.10f,"the palm not on the middle of the throat");
    Check(local.X*s<-.04f&&local.X*s>-.09f,"the wrist not on the near side of the neck");
    Check(tips.X*s>.06f&&tips.X*s<.12f,"the fingers do not reach round the far side of the neck");
    Check(Vector3.Distance(hand.elbow,wrist)>.2f&&Vector3.Distance(hand.elbow,wrist)<.3f,"the forearm is not a forearm long");
   }
   var moved=CarryHoldMath.Hand(neck+new Vector3(2,-.6f,3),q,hostage,right);
   Check(Vector3.Distance(moved.position-hand.position,new Vector3(2,-.6f,3))<1e-5,"crouch/recenter breaks hand lock");cases++;
  }
  Console.WriteLine("PASS: "+cases+" carry hand fixtures; 0.1.193 a hostage's palm on the middle of his throat (8 cm left of 0.1.191's), wrist on the near side, fingers round the far side, a forearm long; neck inside forearm, anatomical wrist bend, both hands, 360-degree turn and crouch/recenter. Controller rotation is not an input.");
 }
}
