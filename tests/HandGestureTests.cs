using System;
using System.Numerics;
using XiiiXR;
class HandGestureTests
{
 static void Check(bool x,string m){if(!x)throw new Exception(m);}
 static void Main(){
 var s=new ScoopGesture();var chest=Vector3.Zero;var reach=new Vector3(0,0,.6f);
 Check(!s.Sample(true,false,reach,chest,0),"air gesture armed");
 Check(!s.Sample(true,true,reach,chest,.1f),"touch alone captures hostage");
 Check(!s.Sample(true,false,reach+Vector3.UnitX*.2f,chest,.2f),"sideways motion captures hostage");
 Check(s.Sample(true,false,reach-Vector3.UnitZ*.16f,chest,.3f),"inward scoop not recognized");
 s.Reset();s.Sample(true,true,reach,chest,1);Check(!s.Sample(true,false,reach-Vector3.UnitZ*.2f,chest,2),"stale scoop accepted");
 s.Reset();s.Sample(true,true,reach,chest,3);s.Sample(false,false,reach,chest,3.1f);Check(!s.Sample(true,false,reach-Vector3.UnitZ*.2f,chest,3.2f),"release preserves armed scoop");
 Check(ClimbHandMath.Velocity(new Vector3(0,.01f,0),.01f).Y>0,"pull-down error does not climb up");
 Check(ClimbHandMath.Velocity(Vector3.UnitY,.01f)==Vector3.Zero,"tracking jump launches player");
 Check(ClimbHandMath.Velocity(Vector3.UnitY*.2f,.01f).Length()<=2.001f,"unbounded climb velocity");
 Check(ClimbHandMath.Velocity(Vector3.Zero,.01f)==Vector3.Zero,"stationary grip drifts");
 // 0.1.151: a hand on a turning ceiling fan is carried up to 4 m/s.
 {var fast=ClimbHandMath.Velocity(Vector3.UnitX*.3f,.01f,4).Length();Check(fast>2.5f&&fast<=4.001f,"fan ride velocity "+fast);}
 // 0.1.171: over the top of a ladder: near the top and pulling up; a landing beyond the ladder; up, then over, then done.
 Check(LadderTopMath.Ready(8,9,1)&&!LadderTopMath.Ready(7,9,1)&&!LadderTopMath.Ready(8,9,0)&&!LadderTopMath.Ready(float.NaN,9,1),"over the top starts at the wrong time");
 var feet=new Vector3(0,8,0);var ladder=new Vector3(0,8.5f,.3f);var away=Vector3.UnitZ;
 Check(LadderTopMath.Landing(feet,ladder,away,new Vector3(0,9,1),Vector3.UnitY,9),"the floor beyond the ladder not a landing");
 Check(!LadderTopMath.Landing(feet,ladder,away,new Vector3(0,9,-.5f),Vector3.UnitY,9)&&!LadderTopMath.Landing(feet,ladder,away,new Vector3(0,8.1f,1),Vector3.UnitY,9)
  &&!LadderTopMath.Landing(feet,ladder,away,new Vector3(0,11,1),Vector3.UnitY,9)&&!LadderTopMath.Landing(feet,ladder,away,new Vector3(0,9,1),Vector3.UnitZ,9),"behind, too low, too high or a wall taken for a landing");
 {bool over=false;var land=new Vector3(0,9,1);
  var v=LadderTopMath.Velocity(feet,land,ref over,out bool done);Check(v.Y>2&&v.Z==0&&!over&&!done,"not straight up first");
  v=LadderTopMath.Velocity(new Vector3(0,9.1f,0),land,ref over,out done);Check(over&&v.Z>2&&!done,"not over onto the landing");
  v=LadderTopMath.Velocity(new Vector3(0,9.0f,.2f),land,ref over,out done);Check(over&&v.Z>2&&v.Y<1,"went up again after clearing the edge");
  LadderTopMath.Velocity(new Vector3(0,9,.95f),land,ref over,out done);Check(done,"not done on the landing");}
 Console.WriteLine("PASS scoop contact, inward direction, timeout/release; bounded climb correction and tracking discontinuity");
 }
}
