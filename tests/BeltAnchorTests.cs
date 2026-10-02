using System;
using System.Numerics;
using XiiiXR;
class BeltAnchorTests
{
 static void Main(){
 for(int turn=0;turn<360;turn+=5)foreach(float tilt in new[]{-.8f,0,.8f}){
 var h=new Vector3(3,1.7f,-4);var q=Quaternion.CreateFromYawPitchRoll(turn*MathF.PI/180,tilt,0);
 var p=BeltAnchorMath.Pose(h,q);var local=Vector3.Transform(p.position-h,Quaternion.Inverse(p.rotation));
 if(Vector3.Distance(local,new Vector3(-.19f,-.57f,.15f))>1e-5)throw new Exception("pouch leaves left belt during room turn");
 if(Vector3.Distance(Vector3.Transform(Vector3.UnitY,p.rotation),Vector3.UnitY)>1e-6)throw new Exception("pouch follows head tilt");
 }
 Console.WriteLine("PASS belt stays on left through 360-degree physical heading and head pitch, without camera roll/pitch.");
 // 0.1.133: the right hand reloads the gun in the left hand: the pouch on the right.
 for(int turn=0;turn<360;turn+=15){
 var h=new Vector3(-1,1.6f,2);var q=Quaternion.CreateFromYawPitchRoll(turn*MathF.PI/180,.3f,0);
 var l=BeltAnchorMath.Pose(h,q);var r=BeltAnchorMath.Pose(h,q,true);var local=Vector3.Transform(r.position-h,Quaternion.Inverse(r.rotation));
 if(Vector3.Distance(local,new Vector3(.19f,-.57f,.15f))>1e-5||r.rotation!=l.rotation)throw new Exception("right-side pouch not the left one mirrored");
 }
 Console.WriteLine("PASS the pouch mirrors to the right belt for the right hand's reload.");
 }
}
