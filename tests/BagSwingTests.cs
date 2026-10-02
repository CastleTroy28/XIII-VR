using System;using System.Numerics;using XiiiXR;
class BagSwingTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // Forward punch below the mount swings the bag forward (rotation about -X).
  var w=BagSwingMath.Hit(Vector3.Zero,new Vector3(0,-1,0),new Vector3(0,0,4),1);
  Check(w.X<0&&MathF.Abs(w.Y)<1e-6f,"punch direction/axis: "+w);
  // Rodrigues: a point under the pivot moves along +Z for that rotation.
  var q=Quaternion.CreateFromAxisAngle(Vector3.Normalize(w),.1f);var moved=Vector3.Transform(new Vector3(0,-1,0),q);
  Check(moved.Z>0,"swing moves the bag away from the fist: "+moved);
  // Amplitude of a strong punch is visible but bounded.
  var a=Vector3.Zero;float peak=0;for(int i=0;i<200;i++){BagSwingMath.Step(ref a,ref w,1,1f/90);peak=MathF.Max(peak,a.Length());}
  Check(peak>.08f&&peak<=BagSwingMath.MaxAngle+1e-4f,"peak angle "+peak);
  // Period of a 1 m pendulum ~2 s; it comes back and settles to exact rest.
  a=Vector3.Zero;w=new Vector3(-.5f,0,0);bool crossed=false;float t=0;
  while(t<1.2f){BagSwingMath.Step(ref a,ref w,1,1f/90);t+=1f/90;if(a.X>0)crossed=true;}
  Check(crossed,"bag returns through rest within a period");
  bool moving=true;for(int i=0;i<90*60&&moving;i++)moving=BagSwingMath.Step(ref a,ref w,1,1f/90);
  Check(!moving&&a==Vector3.Zero&&w==Vector3.Zero,"damped to exact rest");
  // Huge hits are clamped; no spin about the chain.
  w=Vector3.Zero;for(int i=0;i<20;i++)w=BagSwingMath.Hit(w,new Vector3(.3f,-.8f,.1f),new Vector3(8,0,8),.8f);
  Check(w.Length()<=3.5f+1e-4f&&w.Y==0,"clamped "+w);
  a=Vector3.Zero;for(int i=0;i<300;i++)BagSwingMath.Step(ref a,ref w,.8f,1f/90);Check(a.Length()<=BagSwingMath.MaxAngle+1e-4f,"angle limit");
  // Pause (dt=0) keeps the state.
  a=new Vector3(.1f,0,0);w=Vector3.Zero;BagSwingMath.Step(ref a,ref w,1,0);Check(a.X==.1f,"paused");
  Check(BagSwingMath.BagName("PunchingBag_01")&&BagSwingMath.BagName("boxsack_01_mesh")&&!BagSwingMath.NeedsHangingCheck("boxsack_01")&&BagSwingMath.BagName("SM_Boxing_Bag")&&BagSwingMath.BagName("bag_hanging")&&!BagSwingMath.BagName("Sandbag_wall")&&!BagSwingMath.BagName("garbage_bag")&&!BagSwingMath.BagName("wall"),"names");
  Check(!BagSwingMath.NeedsHangingCheck("punching_bag")&&BagSwingMath.NeedsHangingCheck("bag_02"),"hanging check");
  Console.WriteLine("PASS: punch-bag swing direction, bounded amplitude, pendulum return, exact rest, clamps, pause, name filter.");
 }
}
