using System;using System.Numerics;using XiiiXR;
class MountedAimMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static Vector3 Rot(Vector3 v,Vector3 axis,float deg)=>Vector3.Transform(v,Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis),deg*MathF.PI/180));
 static void Main()
 {
  var up=Vector3.UnitY;var f=Vector3.UnitZ;
  // Yaw: the rotation that turns forward onto the desired direction (about up).
  foreach(float a in new[]{-120f,-30f,0f,15f,90f})
  {
   var d=Rot(f,up,a);float yaw=MountedAimMath.Yaw(f,d,up,180);
   Check(MathF.Abs(yaw-a)<.01f,"yaw "+a+" -> "+yaw);
   Check(Vector3.Distance(Rot(f,up,yaw),d)<1e-3f,"applying the yaw does not reach the direction");
  }
  Check(MountedAimMath.Yaw(f,Rot(f,up,100),up,45)==45&&MountedAimMath.Yaw(f,Rot(f,up,-100),up,45)==-45,"yaw limit");
  Check(MathF.Abs(MountedAimMath.Yaw(f,Rot(f,up,30)+up*3,up,180)-30)<.01f,"a raised direction changes the yaw");
  // Pitch: change of elevation; positive = up; applied about cross(forward,up).
  var raised=Vector3.Normalize(new Vector3(0,MathF.Sin(20*MathF.PI/180),MathF.Cos(20*MathF.PI/180)));
  float p=MountedAimMath.Pitch(f,raised,up,60);Check(MathF.Abs(p-20)<.01f,"pitch up 20 -> "+p);
  Check(Vector3.Distance(Rot(f,Vector3.Cross(f,up),p),raised)<1e-3f,"pitch about cross(forward,up) does not raise the gun");
  Check(MountedAimMath.Pitch(f,-up,up,30)==-30&&MountedAimMath.Pitch(f,up,up,30)==30,"pitch limit");
  // Clamps: symmetric, free when unset.
  Check(MountedAimMath.Limit(-60,45,180)==60&&MountedAimMath.Limit(0,0,180)==180&&MountedAimMath.Limit(float.NaN,1,60)==60&&MountedAimMath.Limit(-400,400,180)==180,"clamp reading");
  Check(float.IsNaN(MountedAimMath.Yaw(up,f,up,180))&&float.IsNaN(MountedAimMath.Pitch(Vector3.Zero,f,up,30)),"degenerate aim");
  // 0.1.120: handles (lever): hands down -> barrel up. 0.1.123: linear.
  // 0.1.140: 5 degrees per centimetre (was 8), the 45 degree stop at 9 cm.
  const float L=MountedAimMath.HandleLength;
  Check(MountedAimMath.Lever(0,L,75)==0,"lever at rest");
  float ly=MountedAimMath.Lever(.01f,L,75);Check(ly<-4.6f&&ly>-5.4f,"1 cm -> "+ly);
  Check(MathF.Abs(MountedAimMath.Lever(.02f,L,75)-2*ly)<1e-3f&&MathF.Abs(MountedAimMath.Lever(-.01f,L,75)+ly)<1e-4f,"lever not linear/symmetric");
  float lp=MountedAimMath.Lever(-.03f,L,45);Check(lp>13&&lp<17,"hands down 3 cm -> "+lp);Check(MountedAimMath.Lever(-.10f,L,45)==45,"pitch limit");
  // 0.1.138: sideways the other sign (hands right: + yaw; hands left: - yaw),
  // gentle near the centre. 0.1.140: about 1.5 deg/cm there, the 75 degree stop at 18 cm.
  Check(MountedAimMath.SideLever(0,75)==0,"side at rest");
  float s1=MountedAimMath.SideLever(.01f,75),s3=MountedAimMath.SideLever(.03f,75),s6=MountedAimMath.SideLever(.06f,75);
  Check(-s1>1.3f&&-s1<2f,"hands right 1 cm -> "+s1);
  Check(s1<0&&MountedAimMath.SideLever(-.01f,75)==-s1,"side sign/symmetry (hands left must give the opposite of hands right)");
  Check(-s3/.03f<-s6/.06f&&-s6>11&&-s6<17,"side not gentler at the centre: 3 cm "+s3+", 6 cm "+s6);
  Check(MountedAimMath.SideLever(.18f,75)<=-74.9f&&MountedAimMath.SideLever(.17f,75)>-75&&MountedAimMath.SideLever(.5f,75)==-75&&MountedAimMath.SideLever(-.5f,40)==40,"side stop at 18 cm / limit");
  Check(float.IsNaN(MountedAimMath.SideLever(float.NaN,75)),"side NaN");
  // 0.1.143: the view turns with the gun, so the gun turns the way the hands move (Lever's sign again).
  Check(MathF.Sign(MountedAimMath.SideLever(.02f,75))==MathF.Sign(MountedAimMath.Lever(.02f,L,75)),"side must turn with the lever sign (the way the hands move)");
  // Smoothing: still at a value, shaking of +-1 degree at 11 Hz mostly removed;
  // a quick 30 degree turn followed within about 0.15 s.
  {
   var sm=new MountedAimMath.Smooth();float dt=1/90f,v=0,maxDev=0;
   for(int i=0;i<180;i++){float t=i*dt;v=sm.Step(10+MathF.Sin(2*MathF.PI*11*t),dt);if(i>60)maxDev=Math.Max(maxDev,MathF.Abs(v-10));}
   Check(maxDev<.35f,"shaking not smoothed: "+maxDev);
   var q=new MountedAimMath.Smooth();float w=0;int frames=0;
   for(int i=0;i<90;i++){float t=i*dt;float target=t<.1f?0:30*Math.Min(1,(t-.1f)/.1f);w=q.Step(target,dt);if(t>.1f&&w<27)frames++;}
   Check(frames*dt<.25f&&MathF.Abs(w-30)<.5f,"quick turn lagged: "+frames*dt+" s, end "+w);
   Check(new MountedAimMath.Smooth().Step(5,0)==5&&float.IsNaN(new MountedAimMath.Smooth().Step(float.NaN,dt)),"smooth first/NaN");
  }
  // 0.1.124: fires only with both triggers.
  const ulong T=HandControls.Trigger;
  HandControls H(ulong held,ulong down=0,ulong up=0)=>new HandControls(true,held,down,up);
  Check(!MountedTriggers.Pressed(H(0),H(T),0)&&!MountedTriggers.Pressed(H(T),H(0),0)&&MountedTriggers.Pressed(H(T),H(T),0),"held: both triggers");
  Check(MountedTriggers.Pressed(H(T,T),H(T),1)&&MountedTriggers.Pressed(H(T),H(T,T),1)&&!MountedTriggers.Pressed(H(T),H(T),1)&&!MountedTriggers.Pressed(H(0),H(T,T),1),"down: the second trigger joins");
  Check(MountedTriggers.Pressed(H(0,0,T),H(T),2)&&MountedTriggers.Pressed(H(T),H(0,0,T),2)&&!MountedTriggers.Pressed(H(0,0,T),H(0),2),"up: one of both let go");
  Check(!MountedTriggers.Pressed(new HandControls(false,T,T,0),H(T,T),0),"invalid controller fires");
  Check(MountedAimMath.Lever(10,L,180)==-180&&float.IsNaN(MountedAimMath.Lever(.1f,0,75))&&float.IsNaN(MountedAimMath.Lever(float.NaN,L,75)),"lever limits");
  Check(MountedAimMath.Settle>0&&MountedAimMath.Settle<1,"settle time");
  Check(MountedAimMath.Offset(new Vector3(.1f,.95f,.2f),new Vector3(0,1,0),f,out var right,out var lift)&&MathF.Abs(right-.1f)<1e-5f&&MathF.Abs(lift+.05f)<1e-5f,"offset facing forward");
  Check(MountedAimMath.Offset(new Vector3(0,0,-.1f),Vector3.Zero,Vector3.UnitX*2+up,out right,out _)&&MathF.Abs(right-.1f)<1e-5f,"offset when the player faced right");
  Check(!MountedAimMath.Offset(Vector3.One,Vector3.Zero,up,out _,out _),"offset with a vertical facing");
  // 0.1.122: muzzle flash point plausibility.
  var aimPoint=new Vector3(0,.04f,.5f);
  Check(MuzzleMath.Plausible(new Vector3(0,-.02f,.49f),aimPoint)&&!MuzzleMath.Plausible(new Vector3(0,-.2f,.1f),aimPoint)&&!MuzzleMath.Plausible(new Vector3(float.NaN,0,0),aimPoint)&&!MuzzleMath.Plausible(new Vector3(0,.04f,.3f),aimPoint),"muzzle point plausibility");
  Console.WriteLine("PASS: mounted gun handles: sideways the opposite way round from 0.1.137, about 1.5 deg/cm at the centre, stop at 18 cm; hands down raise it (5 deg/cm); jitter smoothed, quick turns followed; both triggers fire; limits; the frame the player faced.");
  Console.WriteLine("PASS: mounted gun aim: yaw/pitch that turn the gun onto where the controllers point, within the gun's own (symmetric) limits; degenerate input ignored.");
 }
}
