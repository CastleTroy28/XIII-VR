using System;using System.Numerics;using XiiiXR;
class CutsceneViewTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static Quaternion Yaw(float degrees)=>Quaternion.CreateFromAxisAngle(Vector3.UnitY,degrees*MathF.PI/180);
 static Quaternion Pitch(float degrees)=>Quaternion.CreateFromAxisAngle(Vector3.UnitX,degrees*MathF.PI/180);
 static Quaternion Roll(float degrees)=>Quaternion.CreateFromAxisAngle(Vector3.UnitZ,degrees*MathF.PI/180);
 static Vector3 Forward(Quaternion q)=>Vector3.Transform(Vector3.UnitZ,q);
 static bool Near(Vector3 a,Vector3 b,float e=1e-3f)=>Vector3.Distance(a,b)<e;
 static void Main()
 {
  // Headings: +90 is to the right (+X); straight down takes the top's heading.
  Check(MathF.Abs(CutsceneView.YawDegrees(Yaw(90))-90)<.01f&&MathF.Abs(CutsceneView.YawDegrees(Yaw(-30))+30)<.01f,"heading of a turn");
  Check(MathF.Abs(CutsceneView.YawDegrees(Yaw(40)*Pitch(90))-40)<.5f&&MathF.Abs(CutsceneView.YawDegrees(Yaw(40)*Pitch(-90))-40)<.5f,"heading looking straight down/up not the top's");
  Check(CutsceneView.Cut(.8f,0)&&CutsceneView.Cut(0,30)&&!CutsceneView.Cut(.1f,3)&&CutsceneView.Cut(float.NaN,0),"cut rule");
  var view=new CutsceneView();
  var film=new Vector3(10,2,5);var filmTurn=Yaw(120)*Pitch(25)*Roll(10);
  var head=new Vector3(.3f,1.7f,-.2f);var headTurn=Yaw(-40);
  // The first frame is a cut: the view looks where the film camera looks (heading only), level, from the film camera.
  var (p,q)=view.View(film,filmTurn,head,headTurn);
  Check(view.CutThisCall&&view.Cuts==1,"the first frame is not a cut");
  Check(Near(p,film),"the view does not start at the film camera");
  Check(MathF.Abs(CutsceneView.YawDegrees(q)-120)<.1f&&MathF.Abs(Forward(q).Y)<1e-4f,"the view not on the film camera's heading, or tilted with it");
  var right=Vector3.Transform(Vector3.UnitX,q);Check(MathF.Abs(right.Y)<1e-4f,"the film camera's roll followed");
  // Between cuts: the film camera pans and tilts a little each frame - the view does not.
  var turn=filmTurn;var pos=film;
  for(int i=0;i<60;i++){turn=Yaw(1.5f)*turn*Pitch(.3f);pos+=new Vector3(.01f,0,0);(p,q)=view.View(pos,turn,head,headTurn);Check(!view.CutThisCall,"a slow pan taken for a cut");}
  Check(MathF.Abs(CutsceneView.YawDegrees(q)-120)<.1f&&MathF.Abs(Forward(q).Y)<1e-4f,"the view followed the film camera's pan or tilt");
  Check(Near(p,pos),"the view does not ride with the film camera's position");
  // The head turns and tilts the view; its movement since the cut moves it (turned with the view).
  (p,q)=view.View(pos,turn,head+new Vector3(0,0,.1f),Yaw(-10)*Pitch(-20));
  Check(MathF.Abs(CutsceneView.YawDegrees(q)-150)<.1f,"the head's turn does not turn the view");
  Check(MathF.Abs(MathF.Asin(Forward(q).Y)*180/MathF.PI-20)<.5f,"the head's tilt does not tilt the view");
  var lean=Vector3.Transform(new Vector3(0,0,.1f),Yaw(160));
  Check(Near(p,pos+lean),"the head's movement not added in the view's heading");
  // A long way from the cut point: held at 0.6 m.
  (p,_)=view.View(pos,turn,head+new Vector3(3,0,0),headTurn);
  Check(MathF.Abs(Vector3.Distance(p,pos)-CutsceneView.MaxHeadOffset)<1e-3f,"the head offset is not limited");
  // A cut (the film camera jumps): turned again to its heading, the head's movement counted from now.
  var cutTurn=Yaw(-70)*Pitch(-40);var cutAt=new Vector3(-4,3,9);
  (p,q)=view.View(cutAt,cutTurn,head+new Vector3(3,0,0),Yaw(15));
  Check(view.CutThisCall&&view.Cuts==2&&view.LastJumpMeters>1,"a jump of the film camera is not a cut");
  Check(Near(p,cutAt)&&MathF.Abs(CutsceneView.YawDegrees(q)+70)<.1f,"after a cut the view is not where the film camera is, on its heading");
  // A turn of 40 degrees in one frame at the same place is a cut too.
  (p,q)=view.View(cutAt,Yaw(40)*cutTurn,head+new Vector3(3,0,0),Yaw(15));
  Check(view.CutThisCall&&MathF.Abs(CutsceneView.YawDegrees(q)+30)<.1f,"a sudden turn of the film camera is not a cut");
  // Reset (a new cutscene): the next frame is a cut, counted from one.
  view.Reset();(p,q)=view.View(cutAt,cutTurn,head,headTurn);
  Check(view.CutThisCall&&view.Cuts==1&&MathF.Abs(CutsceneView.YawDegrees(q)+70)<.1f,"reset does not start again");
  // Bad input does not break it.
  (p,q)=view.View(cutAt,new Quaternion(0,0,0,0),head,new Quaternion(float.NaN,0,0,1));
  Check(float.IsFinite(p.X)&&float.IsFinite(q.W),"a broken rotation breaks the view");
  // 0.1.238: the screen that stands still. Facing the screen the view is the film camera's (its turns, tilt and roll kept);
  // turning the head looks at another part; the frame (turned as the film camera) stays where it is in the room.
  {
   var filmQ=Yaw(70)*Pitch(-15)*Roll(5);var screenQ=CutsceneScreen.Facing(Yaw(-20)*Pitch(10));
   Check(MathF.Abs(CutsceneView.YawDegrees(screenQ)+20)<.1f&&MathF.Abs(Forward(screenQ).Y)<1e-5f,"the screen not where the head faced, upright");
   var facing=CutsceneScreen.View(filmQ,screenQ,screenQ);
   Check(CutsceneView.Angle(facing,filmQ)<.01f,"facing the screen, the view is not the film camera's");
   foreach(var headQ in new[]{Yaw(10),Yaw(-60)*Pitch(20),Yaw(-20)*Roll(15)})
   {
    var viewQ=CutsceneScreen.View(filmQ,screenQ,headQ);
    // The frame turned as the film camera, seen from the eye: as the screen seen from the head.
    var frameFromEye=Quaternion.Inverse(viewQ)*filmQ;var screenFromHead=Quaternion.Inverse(headQ)*screenQ;
    Check(CutsceneView.Angle(frameFromEye,screenFromHead)<.01f,"the frame moves with the head instead of standing still");
   }
   var turnedFilm=Yaw(40)*filmQ;
   Check(CutsceneView.Angle(CutsceneScreen.View(turnedFilm,screenQ,screenQ),turnedFilm)<.01f,"the film camera's own turn not shown on the screen");
   Check(float.IsFinite(CutsceneScreen.View(new Quaternion(0,0,0,0),screenQ,new Quaternion(float.NaN,0,0,1)).W),"a broken rotation breaks the screen");
  }
  Console.WriteLine("PASS: 0.1.238 cutscenes on a screen that stands still: the film camera's view and turns, the head looking about the screen, the frame staying in the room.");
  Console.WriteLine("PASS: 0.1.234 steady cutscene view: the film camera's position, the head's turn and tilt, turned to the film camera's heading at each cut only (no pan, tilt or roll followed), the head's movement since the cut added (at most 0.6 m).");
 }
}
