using System;using System.Numerics;using XiiiXR;
class ThrowLandingTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 // A floor at y=0 and a wall at z=wall (none when wall<=0).
 static float wall;static int casts;
 static bool Scene(Vector3 a,Vector3 b,out Vector3 p,out Vector3 n)
 {
  casts++;p=n=Vector3.Zero;float best=float.PositiveInfinity;bool found=false;var d=b-a;
  if(a.Y>0&&b.Y<=0){float t=a.Y/(a.Y-b.Y);if(t<best){best=t;p=a+d*t;n=Vector3.UnitY;found=true;}}
  if(wall>0&&a.Z<wall&&b.Z>=wall){float t=(wall-a.Z)/(b.Z-a.Z);if(t<best){best=t;p=a+d*t;n=-Vector3.UnitZ;found=true;}}
  return found;
 }
 static void Main()
 {
  var g=new Vector3(0,-20,0);
  // Under gravity: an arc from 1.5 m at 10 m/s level lands where the parabola meets the floor.
  wall=0;Check(ThrowLandingMath.Land(new Vector3(0,1.5f,0),new Vector3(0,0,10),g,Scene,out var p,out var n,out float t),"a level throw does not land");
  float tt=MathF.Sqrt(1.5f/10f);
  Check(MathF.Abs(p.Z-10*tt)<.12f&&MathF.Abs(p.Y)<1e-4f&&n==Vector3.UnitY&&MathF.Abs(t-tt)<.03f,"the arc lands at "+p+" after "+t+" s, not at "+(10*tt)+" m after "+tt+" s");
  // Thrown up at 45 degrees it lands further off and later.
  Check(ThrowLandingMath.Land(new Vector3(0,1.5f,0),new Vector3(0,7.07f,7.07f),g,Scene,out var p2,out _,out float t2)&&p2.Z>p.Z&&t2>t,"thrown upward does not land further");
  // A wall in the way stops it there, facing back.
  wall=2;Check(ThrowLandingMath.Land(new Vector3(0,1.5f,0),new Vector3(0,0,10),g,Scene,out var p3,out var n3,out _)&&MathF.Abs(p3.Z-2)<1e-3f&&n3==-Vector3.UnitZ&&p3.Y>1f,"a wall in the way does not stop the arc");
  // Straight (a knife flown without gravity): one cast along the line.
  wall=5;casts=0;Check(ThrowLandingMath.Land(new Vector3(0,1.5f,0),new Vector3(0,-.1f,30),Vector3.Zero,Scene,out var p4,out _,out float t4)&&casts==1&&MathF.Abs(p4.Z-5)<1e-3f&&MathF.Abs(t4-5f/30f)<.01f,"a straight flight not cast once along its line");
  // Thrown up into nothing: no landing within the flight followed; a bounded number of casts.
  wall=0;casts=0;Check(!ThrowLandingMath.Land(new Vector3(0,1.5f,0),new Vector3(0,40,0),g,Scene,out _,out _,out _)&&casts<=(int)(ThrowLandingMath.MaxTime/ThrowLandingMath.Step)+1,"a flight that never lands is marked, or followed for ever");
  Check(!ThrowLandingMath.Land(Vector3.Zero,Vector3.Zero,g,Scene,out _,out _,out _)&&!ThrowLandingMath.Land(new Vector3(float.NaN,0,0),Vector3.UnitZ,g,Scene,out _,out _,out _),"no speed or a broken origin lands");
  // The mark grows with distance, within limits.
  Check(ThrowLandingMath.Radius(1)<ThrowLandingMath.Radius(10)&&ThrowLandingMath.Radius(0)>=.08f&&ThrowLandingMath.Radius(500)<=.3f&&ThrowLandingMath.Radius(float.NaN)==.08f,"mark radius");
  // A swing counts forward of the look only.
  Check(ThrowLandingMath.Forward(new Vector3(.2f,-1,1),Vector3.UnitZ)&&!ThrowLandingMath.Forward(new Vector3(0,1,-1),Vector3.UnitZ)&&!ThrowLandingMath.Forward(new Vector3(0,5,0),Vector3.UnitZ),"forward swing");
  // The game's knife measured as it flies: straight, falling, or stopped.
  Vector3 Fly(float s,Vector3 v,Vector3 a)=>new Vector3(0,1.5f,0)+v*s+a*(.5f*s*s);
  var v0=new Vector3(0,2,29.9f);
  Check(ThrowLandingMath.Fit(.02f,Fly(.02f,v0,Vector3.Zero),.08f,Fly(.08f,v0,Vector3.Zero),.14f,Fly(.14f,v0,Vector3.Zero),out float sp,out float drop)&&MathF.Abs(sp-v0.Length())<.01f&&drop<.01f,"a straight knife flight measured as falling or slower");
  Check(ThrowLandingMath.Fit(.02f,Fly(.02f,v0,g),.08f,Fly(.08f,v0,g),.14f,Fly(.14f,v0,g),out _,out float drop2)&&MathF.Abs(drop2-20)<.5f,"a falling knife flight measured at "+drop2);
  var stuck=Fly(.07f,v0,Vector3.Zero);
  Check(!ThrowLandingMath.Fit(.02f,Fly(.02f,v0,Vector3.Zero),.08f,stuck,.14f,stuck,out _,out _),"a knife stuck in a wall measured as a flight");
  Check(!ThrowLandingMath.Fit(.02f,Vector3.Zero,.02f,Vector3.UnitZ,.14f,Vector3.UnitZ*2,out _,out _)&&!ThrowLandingMath.Fit(.02f,Vector3.Zero,.08f,new Vector3(0,0,.1f),.14f,new Vector3(0,0,.2f),out _,out _),"no time between positions, or a slow thing, measured");
  // The next knife at once: thrown and launched, a grip press at its place, knives left.
  Check(KnifeRetakeMath.Thrown(true,1f,1.1f,false)&&!KnifeRetakeMath.Thrown(true,1f,.5f,false)&&!KnifeRetakeMath.Thrown(false,1f,1.1f,false)&&KnifeRetakeMath.Thrown(false,-10,0,true),"a throw not launched yet (or none) lets the next knife be taken");
  Check(KnifeRetakeMath.Retake(true,true,true,3)&&KnifeRetakeMath.Retake(true,true,true,-1),"the next knife not taken");
  Check(!KnifeRetakeMath.Retake(false,true,true,3)&&!KnifeRetakeMath.Retake(true,false,true,3)&&!KnifeRetakeMath.Retake(true,true,false,3)&&!KnifeRetakeMath.Retake(true,true,true,0),"a knife taken without a throw, a press, its place or knives left");
  Check(KnifeRetakeMath.PressWait>.5f&&KnifeRetakeMath.PressWait<1.5f,"the press waits too short or past the throw's own time-out");
  Console.WriteLine("PASS: 0.1.214 the landing mark: an arc lands where it meets the floor or a wall (the time too), a straight knife flight is one cast, a flight that never lands is not marked; the mark grows with distance; only a forward swing counts; the game's knife flight is measured from its positions (straight, falling, or stuck: not used).");
  Console.WriteLine("PASS: 0.1.214 the next knife is taken at once once the thrown one has left, with a grip press at its place and knives left; a throw let go early waits for the game's knife.");
 }
}
