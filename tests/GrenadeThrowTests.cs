using System;using System.Numerics;using XiiiXR;
class GrenadeThrowTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var grenade=new Vector3(.2f,1.2f,.3f);
  var g=new GrenadeThrow();
  // No pin: holding and releasing throws nothing.
  float t=0;GrenadeThrow.Result r=default;
  for(int i=0;i<30;i++){t+=.011f;r=g.Sample(t,true,new Vector3(0,0,i*.05f));Check(!r.Throw,"thrown while held");}
  r=g.Sample(t+=.011f,false,new Vector3(0,0,1.6f));Check(!r.Throw,"thrown without the pin");
  // Pin: only with the left trigger pressed at the grenade.
  Check(!g.PullPin(true,grenade+new Vector3(.3f,0,0),grenade),"pin pulled from 30 cm away");
  Check(!g.PullPin(false,grenade,grenade),"pin pulled without the trigger");
  Check(g.PullPin(true,grenade+new Vector3(.05f,.03f,0),grenade)&&g.PinPulled,"pin not pulled at the grenade");
  Check(!g.PullPin(true,grenade,grenade),"pin pulled twice");
  // Trigger already held before the pin: arms now; a tap is ignored.
  r=g.Sample(t+=.011f,true,new Vector3(0,0,0));r=g.Sample(t+=.011f,false,new Vector3(0,0,0));Check(!r.Throw&&g.PinPulled,"tap threw or lost the pin");
  // Hold, swing forward-up at ~6 m/s, let go at the end of the swing.
  var v=new Vector3(0,2,6);var p=new Vector3(0,0,-.3f);
  for(int i=0;i<25;i++){t+=.011f;p+=v*.011f;r=g.Sample(t,true,p);Check(!r.Throw,"thrown while holding the lever");}
  t+=.011f;p+=v*.011f;r=g.Sample(t,false,p);
  Check(r.Throw&&!g.PinPulled,"swing and release did not throw");
  Check(Vector3.Distance(r.Velocity,v*GrenadeThrow.Boost)<.3f&&MathF.Abs(r.HandSpeed-v.Length())<.2f,"throw velocity is not the hand's: "+r.Velocity+" hand "+r.HandSpeed);
  // Released while swinging backwards/sideways: it goes that way (inertia), never forced forward.
  var h=new GrenadeThrow();h.PullPin(true,grenade,grenade);t=10;var side=new Vector3(-4,0,-1);p=Vector3.Zero;
  for(int i=0;i<20;i++){t+=.011f;p+=side*.011f;h.Sample(t,true,p);}
  t+=.011f;p+=side*.011f;r=h.Sample(t,false,p);
  Check(r.Throw&&Vector3.Dot(Vector3.Normalize(r.Velocity),Vector3.Normalize(side))>.99f,"sideways release not along the hand: "+r.Velocity);
  // Held still and let go: it drops (small velocity), it is not thrown far.
  var d=new GrenadeThrow();d.PullPin(true,grenade,grenade);t=20;
  for(int i=0;i<20;i++){t+=.011f;d.Sample(t,true,Vector3.Zero);}
  r=d.Sample(t+=.011f,false,Vector3.Zero);Check(r.Throw&&r.Velocity.Length()<.05f,"a still release flew: "+r.Velocity);
  // A wild tracking jump is capped.
  var w=new GrenadeThrow();w.PullPin(true,grenade,grenade);t=30;p=Vector3.Zero;
  for(int i=0;i<20;i++){t+=.011f;p+=new Vector3(0,0,1.2f);w.Sample(t,true,p);}
  r=w.Sample(t+=.011f,false,p+new Vector3(0,0,1.2f));Check(r.Throw&&r.Velocity.Length()<=GrenadeThrow.MaxSpeed+1e-3f,"throw speed not capped: "+r.Velocity.Length());
  // Bad input resets instead of throwing.
  var n=new GrenadeThrow();n.PullPin(true,grenade,grenade);Check(!n.Sample(float.NaN,false,Vector3.Zero).Throw&&!n.Sample(1,false,new Vector3(float.NaN,0,0)).Throw,"NaN throw");
  n.Reset();Check(!n.PinPulled,"reset keeps the pin out");
  // 0.1.138: a real swing flies as briskly as a thrown prop; a gentle toss stays gentle.
  {
   var dir=Vector3.Normalize(new Vector3(.1f,.1f,-1));
   var hard=GrenadeThrow.Lively(dir*5.3f,3.94f,15);Check(hard.Length()>15&&hard.Length()<=GrenadeThrow.MaxSpeed&&Vector3.Dot(Vector3.Normalize(hard),dir)>.999f,"a real swing still sluggish or turned: "+hard);
   var soft=GrenadeThrow.Lively(dir*1.2f,.9f,15);Check(MathF.Abs(soft.Length()-1.2f)<1e-4f,"a gentle toss was sped up: "+soft.Length());
   var mid=GrenadeThrow.Lively(dir*2.4f,1.75f,15);Check(mid.Length()>2.4f&&mid.Length()<hard.Length(),"no blend between toss and throw: "+mid.Length());
   var harder=GrenadeThrow.Lively(dir*6.8f,5.07f,15);Check(harder.Length()>hard.Length(),"a harder swing not further: "+harder.Length()+" vs "+hard.Length());
   Check(GrenadeThrow.Lively(Vector3.Zero,4,15)==Vector3.Zero&&GrenadeThrow.Lively(dir,4,float.NaN)==dir,"drop / bad native speed");
   // Over flat ground (g = 20 m/s^2, released 1.5 m up, 10 degrees up): a real swing lands 8+ m away.
   var lv=GrenadeThrow.Lively(Vector3.Normalize(new Vector3(0,MathF.Sin(.1745f),MathF.Cos(.1745f)))*5.3f,3.94f,15);
   float vy=lv.Y,tLand=(vy+MathF.Sqrt(vy*vy+2*20*1.5f))/20;Check(lv.Z*tLand>8,"a real swing lands too close: "+lv.Z*tLand+" m");
  }
  {
   // 0.1.159: the swing's heading turned 35 % toward the look, within 75 degrees; rise and speed kept.
   var look=new Vector3(0,-.2f,1);var tv=new Vector3(MathF.Sin(.5236f)*10,3,MathF.Cos(.5236f)*10);   // 30 degrees right, rising
   var a=GrenadeThrow.TowardLook(tv,look);float heading=MathF.Atan2(a.X,a.Z)*180/MathF.PI;
   Check(MathF.Abs(heading-19.5f)<.3f&&a.Y==3&&MathF.Abs(new Vector2(a.X,a.Z).Length()-10)<1e-3f,"grenade heading not turned toward the look: "+heading+" "+a);
   var left=GrenadeThrow.TowardLook(new Vector3(-MathF.Sin(.5236f)*10,0,MathF.Cos(.5236f)*10),look);Check(MathF.Abs(MathF.Atan2(left.X,left.Z)*180/MathF.PI+19.5f)<.3f,"left swing turned the wrong way");
   var offSide=new Vector3(10,0,-1);Check(GrenadeThrow.TowardLook(offSide,look)==offSide,"a throw far off the look was turned");
   Check(GrenadeThrow.TowardLook(new Vector3(0,-5,0),look)==new Vector3(0,-5,0)&&GrenadeThrow.TowardLook(tv,new Vector3(0,1,0))==tv,"straight down / looking straight up changed");
   var seam=GrenadeThrow.TowardLook(new Vector3(MathF.Sin(3.0f),0,MathF.Cos(3.0f)),new Vector3(MathF.Sin(-3.0f),0,MathF.Cos(-3.0f)));
   Check(MathF.Abs(MathF.Atan2(seam.X,seam.Z)-(3.0f+.2832f*.35f))<1e-3f,"turned the long way round across the seam");
  }
  Console.WriteLine("PASS: grenade 0.1.159: the swing's heading turned 35 % toward the look within 75 degrees (rise and speed kept; far-off throws, straight drops and a look straight up unchanged; across the seam the short way).");
  Console.WriteLine("PASS: grenade 0.1.138: a real swing leaves at the game's throwing speed scaled by the swing (lands 8+ m away), a gentle toss keeps the hand's speed, blended in between, always in the hand's direction.");
  Console.WriteLine("PASS: grenade: left trigger at the grenade pulls the pin (13 cm reach); nothing is thrown without it; letting go of the held right trigger throws with the hand's own velocity x"+GrenadeThrow.Boost+" in any direction (still hand = drop), capped at "+GrenadeThrow.MaxSpeed+" m/s; taps ignored.");
 }
}
