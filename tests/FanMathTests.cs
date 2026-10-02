using System;using System.Collections.Generic;using System.Numerics;using XiiiXR;
class FanMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // The swept disc: radial distance and height along the axis.
  FanMath.Split(new Vector3(.5f,-.1f,0),Vector3.UnitY,out float r,out float h);Check(Math.Abs(r-.5f)<1e-4f&&Math.Abs(h+.1f)<1e-4f,"split "+r+" "+h);
  Check(FanMath.InDisc(.5f,0,.7f,-.05f,.05f)&&FanMath.InDisc(.78f,-.15f,.7f,-.05f,.05f),"a hand at a blade, just past its tip and a little under it");
  Check(!FanMath.InDisc(.1f,0,.7f,-.05f,.05f),"the hub is not a blade");
  Check(!FanMath.InDisc(.9f,0,.7f,-.05f,.05f)&&!FanMath.InDisc(.5f,-.3f,.7f,-.05f,.05f)&&!FanMath.InDisc(.5f,.3f,.7f,-.05f,.05f),"too far out, below or above the blades");
  Check(!FanMath.InDisc(float.NaN,0,.7f,0,0)&&!FanMath.InDisc(.5f,0,0,0,0),"bad input");
  // Angles around the axis and the blades read from the mesh (four blades).
  Check(Math.Abs(FanMath.Angle(new Vector3(0,0,1),Vector3.UnitY)-FanMath.Angle(new Vector3(0,.3f,1),Vector3.UnitY))<1e-3f,"the height does not change the angle");
  float a0=FanMath.Angle(new Vector3(1,0,0),Vector3.UnitY),a90=FanMath.Angle(new Vector3(0,0,1),Vector3.UnitY);
  Check(Math.Abs(Math.Abs(a90-a0)-90)<1e-2f||Math.Abs(Math.Abs(a90-a0)-270)<1e-2f,"a quarter turn apart: "+a0+" "+a90);
  var points=new List<Vector3>();
  for(int b=0;b<4;b++){float t=b*MathF.PI/2;var dir=new Vector3(MathF.Cos(t),0,MathF.Sin(t));var side=new Vector3(-dir.Z,0,dir.X);
   for(float l=.05f;l<=.7f;l+=.05f)for(float w=-.06f;w<=.061f;w+=.03f)points.Add(dir*l+side*w);}
  for(int i=0;i<40;i++){float t=i*MathF.PI/20;points.Add(new Vector3(MathF.Cos(t),0,MathF.Sin(t))*.08f);} // the hub
  var blades=FanMath.Blades(points,Vector3.UnitY,.7f);
  Check(blades!=null&&FanMath.Count(blades)==4,"four blades: "+FanMath.Count(blades));
  Check(FanMath.OnBlade(blades,FanMath.Angle(new Vector3(.5f,0,0),Vector3.UnitY))&&FanMath.OnBlade(blades,FanMath.Angle(new Vector3(0,0,-.5f),Vector3.UnitY)),"a hand on a blade");
  Check(!FanMath.OnBlade(blades,FanMath.Angle(new Vector3(.4f,0,.4f),Vector3.UnitY)),"a hand between two blades");
  Check(FanMath.OnBlade(null,123),"blades unknown: the whole disc");
  var disc=new List<Vector3>();for(int i=0;i<720;i++){float t=i*MathF.PI/360;disc.Add(new Vector3(MathF.Cos(t),0,MathF.Sin(t))*.6f);}
  Check(FanMath.Blades(disc,Vector3.UnitY,.7f)==null,"a round lamp is not bladed");
  Check(FanMath.Blades(new[]{Vector3.UnitX},Vector3.UnitY,.7f)==null,"too few points");
  // The ride and what is a ceiling fan.
  Check(FanMath.Ride(360)==FanMath.MaxRide&&FanMath.Ride(-360)==-FanMath.MaxRide&&FanMath.Ride(60)==60&&FanMath.Ride(float.NaN)==0,"ride speed");
  Check(FanMath.CeilingFan(Vector3.UnitY,.7f,200)&&FanMath.CeilingFan(-Vector3.UnitY,.7f,-200),"a ceiling fan either way up");
  Check(!FanMath.CeilingFan(Vector3.UnitX,.7f,200),"a wall vent fan (horizontal axis) is not one");
  Check(!FanMath.CeilingFan(Vector3.UnitY,.1f,200)&&!FanMath.CeilingFan(Vector3.UnitY,3f,200)&&!FanMath.CeilingFan(Vector3.UnitY,.7f,1),"too small, too large or still");
  // 0.1.154: pointed at from any side, within reach: the first blade on the ray.
  {
   var c=new Vector3(0,3.4f,0);var q=System.Numerics.Quaternion.Identity;float R=1.3f;
   Check(FanMath.RayHit(new Vector3(1,1.3f,0),Vector3.UnitY,c,q,Vector3.UnitY,R,-.05f,.05f,null,FanMath.PointReach,out float t)&&Math.Abs(t-(2.1f-.14f))<.06f,"pointed straight up under a blade: missed "+t);
   Check(FanMath.RayHit(new Vector3(-3,1.4f,0),Vector3.Normalize(new Vector3(3.9f,2,0)),c,q,Vector3.UnitY,R,-.05f,.05f,null,FanMath.PointReach,out t),"pointed at from the side (slanted): missed");
   Check(!FanMath.RayHit(new Vector3(0,1.3f,0),Vector3.UnitY,c,q,Vector3.UnitY,R,-.05f,.05f,null,FanMath.PointReach,out _),"the hub taken");
   Check(!FanMath.RayHit(new Vector3(1,1.3f,0),-Vector3.UnitY,c,q,Vector3.UnitY,R,-.05f,.05f,null,FanMath.PointReach,out _),"pointing away takes it");
   Check(!FanMath.RayHit(new Vector3(1,-2,0),Vector3.UnitY,c,q,Vector3.UnitY,R,-.05f,.05f,null,FanMath.PointReach,out _),"out of reach taken");
   Check(FanMath.RayHit(new Vector3(-3,3.4f,0),Vector3.UnitX,c,q,Vector3.UnitY,R,-.05f,.05f,null,FanMath.PointReach,out t)&&Math.Abs(t-(3-R-FanMath.TipReach))<.1f,"level with the fourBlades, pointed along them: missed "+t);
   var fourBlades=new bool[FanMath.Bins];for(int i=0;i<4;i++)fourBlades[i*18]=true;
   var turned=System.Numerics.Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/4);
   Check(!FanMath.RayHit(new Vector3(1,1.3f,0),Vector3.UnitY,c,turned,Vector3.UnitY,R,-.05f,.05f,fourBlades,FanMath.PointReach,out _),"between two fourBlades (turned fan) taken");
   Check(FanMath.RayHit(new Vector3(.7f,1.3f,.7f),Vector3.UnitY,c,turned,Vector3.UnitY,R,-.05f,.05f,fourBlades,FanMath.PointReach,out _),"on a blade (turned fan) missed");
   // Carried to it: fast, however far; the fan still until there, then up to speed in half a second.
   var v=FanMath.Pull(new Vector3(0,2,0),.011f);Check(Math.Abs(v.Length()-FanMath.PullSpeed)<1e-3f,"pulled faster than its speed "+v);
   Check(FanMath.Pull(new Vector3(0,.01f,0),.01f).Length()<1.01f&&FanMath.Pull(new Vector3(0,1,0),0)==Vector3.Zero,"pull near / no time");
   Check(FanMath.RideRamp(100,120,true,.01f)==0,"the fan turns while the player is carried to it");
   float ride=0;int n=0;while(ride<120&&n<200){ride=FanMath.RideRamp(ride,120,false,.011f);n++;}Check(n>30&&n<60&&ride==120,"ride speed not reached in about half a second: "+n+" frames");
   Check(FanMath.RideRamp(0,-120,false,.1f)<0&&FanMath.RideRamp(float.NaN,120,false,.01f)>0,"ramp direction / bad input");
  }
  // GC pacing (ModGcPacer): the step follows the pauses and stays within bounds.
  Check(GcPaceMath.Due(4L<<20,4L<<20)&&!GcPaceMath.Due((4L<<20)-1,4L<<20)&&!GcPaceMath.Due(1L<<30,0),"a clean-up due after the step");
  Check(GcPaceMath.Adapt(4L<<20,10)==3L<<20&&GcPaceMath.Adapt(4L<<20,1)==5L<<20&&GcPaceMath.Adapt(4L<<20,4)==4L<<20,"the step follows the pauses");
  long s=4L<<20;for(int i=0;i<50;i++)s=GcPaceMath.Adapt(s,30);Check(s==GcPaceMath.MinStep,"long pauses: the smallest step "+s);
  for(int i=0;i<50;i++)s=GcPaceMath.Adapt(s,.2);Check(s==GcPaceMath.MaxStep,"short pauses: the largest step "+s);
  Check(GcPaceMath.Adapt(4L<<20,double.NaN)==4L<<20&&GcPaceMath.Adapt(1,0)==GcPaceMath.MinStep,"bad input");
  Console.WriteLine("PASS: ceiling fans: the swept disc (not the hub), a little past the tips and under the blades; blades read from the mesh (a hand between two blades takes nothing), the whole disc when unknown or round; the ride slowed to 120 degrees a second; only fans turning about a near-vertical axis; 0.1.154: pointed at from any side within reach (not the hub, not between blades), the player carried to the blade, the fan still until he is there.");
  Console.WriteLine("PASS: the mod's memory cleaned in steps of 2-12 MB following the pauses, enemies do not.");
 }
}
