using System;using System.Numerics;using XiiiXR;
class ScopeSteadyTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 const float Dt=1/90f;
 static Quaternion Aim(float yawDeg,float pitchDeg)=>Quaternion.CreateFromYawPitchRoll(yawDeg*MathF.PI/180,pitchDeg*MathF.PI/180,0);
 // Peak-to-peak of the aim's yaw and pitch (degrees) against a fixed one, after it settles.
 static float Wobble(bool atEye,float strength,out float lagMean)
 {
  var s=new ScopeSteadyMath();float lo=1e9f,hi=-1e9f,lo2=1e9f,hi2=-1e9f;lagMean=0;int n=0;
  for(int i=0;i<630;i++)
  {
   float t=i*Dt;
   // The hand's tremor (10 Hz, a quarter of a degree) and a slower sway (1.3 Hz, a tenth).
   float yaw=.25f*MathF.Sin(2*MathF.PI*10*t)+.1f*MathF.Sin(2*MathF.PI*1.3f*t),pitch=.2f*MathF.Sin(2*MathF.PI*8.7f*t+1);
   var target=Aim(yaw,pitch);var o=s.Step(target,Dt,atEye,strength);
   if(i<270)continue;   // settled (3 s)
   var d=Vector3.Transform(Vector3.UnitZ,o);float oy=MathF.Atan2(d.X,d.Z)*180/MathF.PI,op=MathF.Asin(Math.Clamp(-d.Y,-1,1))*180/MathF.PI;
   lo=Math.Min(lo,oy);hi=Math.Max(hi,oy);lo2=Math.Min(lo2,op);hi2=Math.Max(hi2,op);lagMean+=ScopeSteadyMath.Degrees(o,target);n++;
  }
  lagMean/=n;return Math.Max(hi-lo,hi2-lo2);
 }
 static void Main()
 {
  float freeLag;float free=Wobble(false,1,out freeLag);
  float steadyLag;float steady=Wobble(true,1,out steadyLag);
  float strongLag;float strong=Wobble(true,2,out strongLag);
  float offLag;float off=Wobble(true,0,out offLag);
  Check(free>.5f&&freeLag<.05f,"off the eye the aim does not follow the hands: wobble "+free+" lag "+freeLag);
  Check(steady<.12f,"at the eye the tremor still sways the scope: "+steady+" of "+free+" degrees");
  Check(strong<steady&&off>.5f,"strength does not set the steadiness (strong "+strong+", off "+off+")");
  // A slow aim (1 deg/s) follows closely; a deliberate turn (40 deg/s) passes, never more than MaxLag behind, and settles at once.
  {
   var s=new ScopeSteadyMath();float worstSlow=0;
   for(int i=0;i<180;i++){var target=Aim(i*Dt,0);var o=s.Step(target,Dt,true);if(i>60)worstSlow=Math.Max(worstSlow,ScopeSteadyMath.Degrees(o,target));}
   Check(worstSlow<.5f,"a slow aim lags too far behind: "+worstSlow);
   float yaw=180*Dt,worst=0;
   for(int i=0;i<45;i++){yaw+=40*Dt;var o=s.Step(Aim(yaw,0),Dt,true);worst=Math.Max(worst,ScopeSteadyMath.Degrees(o,Aim(yaw,0)));}
   Check(worst<=ScopeSteadyMath.MaxLag+.01f,"a deliberate turn lags more than "+ScopeSteadyMath.MaxLag+": "+worst);
   Quaternion last=Quaternion.Identity;for(int i=0;i<36;i++)last=s.Step(Aim(yaw,0),Dt,true);
   Check(ScopeSteadyMath.Degrees(last,Aim(yaw,0))<.1f,"the aim does not settle on the target after a turn");
  }
  // Coming to the eye and leaving it: no jump.
  {
   var s=new ScopeSteadyMath();var target=Aim(3,1);var before=s.Step(target,Dt,false);float jump=0;
   for(int i=0;i<60;i++){var o=s.Step(target,Dt,i<30);jump=Math.Max(jump,ScopeSteadyMath.Degrees(o,before));before=o;}
   Check(jump<.01f&&s.Weight<1,"the aim jumped coming to or leaving the eye");
  }
  // A broken aim or a long pause starts again from the hands.
  {
   var s=new ScopeSteadyMath();s.Step(Aim(0,0),Dt,true);
   var o=s.Step(Aim(20,0),.5f,true);Check(ScopeSteadyMath.Degrees(o,Aim(20,0))<1e-3f,"a long pause kept the old aim");
   var bad=new Quaternion(float.NaN,0,0,1);Check(s.Step(bad,Dt,true).Equals(bad)||float.IsNaN(s.Step(bad,Dt,true).X),"a broken aim not passed through");
  }
  Console.WriteLine("PASS: at the scope the hand's tremor and sway are smoothed away ("+free.ToString("F2")+" -> "+steady.ToString("F3")+" degrees peak to peak, "+(strong).ToString("F3")+" at strength 2); off the eye and at strength 0 the aim is the hands'; a slow aim (1 deg/s) follows within half a degree, a deliberate turn passes never more than "+ScopeSteadyMath.MaxLag+" behind and settles; no jump coming to or leaving the eye; a long pause or a broken aim starts again.");
 }
}
