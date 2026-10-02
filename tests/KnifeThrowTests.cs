using System;using System.Numerics;using XiiiXR;
class KnifeThrowTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 // Simulated tracking: segments of (duration, velocity, trigger).
 static KnifeThrowGesture.Result Run(float hz,Vector3 aim,params (float time,Vector3 velocity,bool held)[] path)
 {
  var g=new KnifeThrowGesture();var p=new Vector3(.2f,1.2f,.3f);float t=0,dt=1/hz;KnifeThrowGesture.Result last=default;int results=0;
  g.Sample(t,false,p,aim);
  foreach(var (time,velocity,held) in path)for(float e=0;e<time-1e-5f;e+=dt)
  {
   t+=dt;p+=velocity*dt;var r=g.Sample(t,held,p,aim);
   if(r.Throw||r.Cancelled){last=r;results++;}
  }
  Check(results<=1,"one gesture produced several results");
  return last;
 }
 static void Main()
 {
  var fwd=new Vector3(0,0,1);
  foreach(float hz in new[]{72f,90f,120f})
  {
   // Hold, draw back, snap forward, release during the snap.
   var r=Run(hz,fwd,(.05f,Vector3.Zero,false),(.25f,-fwd*1.2f,true),(.10f,fwd*4f,true),(.02f,fwd*4f,false),(.2f,Vector3.Zero,false));
   Check(r.Throw&&Vector3.Dot(r.Direction,fwd)>.99f&&Math.Abs(r.Speed-4)<.3f,$"{hz}Hz: normal throw failed speed={r.Speed} dir={r.Direction}");
   // Late release: the hand already slowed down for ~70ms.
   r=Run(hz,fwd,(.2f,-fwd,true),(.08f,fwd*4f,true),(.07f,fwd*.3f,true),(.02f,Vector3.Zero,false));
   Check(r.Throw&&r.Speed>3,$"{hz}Hz: late release lost the throw speed={r.Speed}");
   // Releasing while still pulling back throws nothing.
   r=Run(hz,fwd,(.3f,-fwd*2f,true),(.02f,-fwd*2f,false));
   Check(!r.Throw&&r.Cancelled&&r.Reason=="not forward",$"{hz}Hz: backward release threw ({r.Reason})");
   // Slow push is not a throw.
   r=Run(hz,fwd,(.3f,fwd*.6f,true),(.02f,fwd*.6f,false));
   Check(!r.Throw&&r.Reason=="slow",$"{hz}Hz: slow push threw ({r.Reason})");
   // Tap of the trigger is not a throw, even while moving.
   r=Run(hz,fwd,(.03f,fwd*4f,true),(.02f,fwd*4f,false));
   Check(!r.Throw&&r.Reason=="tap",$"{hz}Hz: tap threw ({r.Reason})");
   // Sideways swing.
   r=Run(hz,fwd,(.2f,new Vector3(4,0,0),true),(.02f,new Vector3(4,0,0),false));
   Check(!r.Throw,$"{hz}Hz: sideways swing threw");
   // Aim assist: hand path 25 degrees up, controller level: direction between.
   var up=Vector3.Normalize(new Vector3(0,MathF.Sin(.436f),MathF.Cos(.436f)));
   r=Run(hz,fwd,(.2f,-up,true),(.1f,up*4.5f,true),(.02f,up*4.5f,false));
   float angle=MathF.Acos(Math.Clamp(Vector3.Dot(r.Direction,fwd),-1,1))*57.3f;
   Check(r.Throw&&angle>12&&angle<22,$"{hz}Hz: aim blend angle={angle}");
  }
  // Trigger already held when the knife appears (switch, menu): no throw.
  var g=new KnifeThrowGesture();var pos=Vector3.Zero;
  for(int i=0;i<30;i++){pos+=new Vector3(0,0,.05f);var r=g.Sample(i/90f,i<20,pos,new Vector3(0,0,1));Check(!r.Throw&&!r.Cancelled,"held trigger from before arms a throw");}
  // Invalid samples reset; duplicate timestamps ignored.
  g.Reset();g.Sample(0,true,Vector3.Zero,new Vector3(0,0,1));Check(!g.Armed,"press held through a reset armed");
  g.Sample(.01f,false,Vector3.Zero,new Vector3(0,0,1));g.Sample(.02f,true,Vector3.Zero,new Vector3(0,0,1));Check(g.Armed,"fresh press did not arm");
  g.Sample(.02f,false,Vector3.Zero,new Vector3(0,0,1));Check(g.Armed,"duplicate timestamp consumed");
  g.Sample(.1f,true,new Vector3(float.NaN,0,0),new Vector3(0,0,1));Check(!g.Armed,"NaN tracking kept gesture");
  Console.WriteLine("PASS: knife gesture at 72/90/120Hz: draw-back+snap release throws along the hand's peak velocity (late release kept); backward, slow, tap, sideways and pre-held trigger do not throw; 30% controller aim blend; invalid tracking resets. Pure math; in-game feel needs headset testing.");
 }
}
