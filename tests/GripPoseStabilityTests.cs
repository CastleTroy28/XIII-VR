using System;
using System.Numerics;
using XiiiXR;
class GripPoseStabilityTests
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static void Main()
 {
  var window=new GripPoseStability();var pose=new[]{Matrix4x4.Identity,Matrix4x4.CreateTranslation(0,0,.08f),Matrix4x4.Identity};var hand=new[]{true,true,false};
  bool Sample(float time,int frame,bool settled=true,string key="ashtray:1")=>window.Observe(key,Matrix4x4.Identity,pose,hand,time,frame,settled);
  Check(!Sample(0,1),"first draw sample cached");
  Check(!Sample(.1f,2),"insufficient stable time cached");
  pose[1]=Matrix4x4.CreateTranslation(0,-.02f,.08f);
  Check(!Sample(.2f,3),"closing fingers treated as idle pose");
  Check(!Sample(.25f,4),"transition settled too early");
  Check(!Sample(.9f,4),"second camera in one frame completed capture");
  pose[2]=Matrix4x4.CreateTranslation(3,2,1);
  Check(Sample(.4f,5),"unrelated offhand stopped stable dominant capture");
  Check(!Sample(.41f,6,false),"native transition bypassed");
  Check(!Sample(.6f,7),"previous stable interval survived a transition");
  Check(!Sample(.8f,8,true,"ashtray:2"),"new prop inherited old pose timer");
  Check(!Sample(.9f,9,true,"ashtray:2")&&Sample(1,10,true,"ashtray:2"),"stable prop could not be captured");
  window.Reset();Check(!Sample(4,11),"scene/reset reuses stable sample");
  Console.WriteLine("PASS: animated fingers and draws are not frozen; second-eye callbacks do not count; new visual/reset restarts capture; other hand may move");
 }
}
