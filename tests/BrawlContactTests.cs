using System;using System.Numerics;using XiiiXR;
class BrawlContactTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var head=new Vector3(0,1.7f,1);
  Check(BrawlContactMath.Hits(new(0,1.7f,.65f),new(0,1.7f,1.2f),head),"swept fast punch reaches head");
  Check(!BrawlContactMath.Hits(new(.45f,1.7f,.65f),new(.45f,1.7f,1.2f),head),"side step dodges");
  Check(!BrawlContactMath.Hits(new(0,1.7f,.65f),new(0,1.7f,1.2f),head-new Vector3(0,.45f,0)),"duck dodges head punch");
  Check(BrawlContactMath.Hits(new(0,1.25f,.8f),new(0,1.25f,1.1f),head),"body punch reaches torso");
  Check(!BrawlContactMath.Hits(new(0,1.7f,-2),new(0,1.7f,2),head),"teleport cannot become a hit");
  Check(!BrawlContactMath.Hits(new(float.NaN,0,0),Vector3.Zero,head),"invalid poses cannot hit");
  Check(!BrawlContactMath.Active(.05f)&&BrawlContactMath.Active(.2f)&&!BrawlContactMath.Active(.5f),"windup and retraction cannot hurt");
  Check(BrawlContactMath.DistanceSquared(Vector3.Zero,Vector3.UnitX,Vector3.UnitY,Vector3.One)>=.999f,"parallel segments");
  Check(BrawlContactMath.DistanceSquared(Vector3.Zero,Vector3.Zero,Vector3.UnitY,Vector3.UnitY)==1,"two points");
  Check(BrawlContactMath.DistanceSquared(-Vector3.UnitX,Vector3.UnitX,-Vector3.UnitY,Vector3.UnitY)<1e-6f,"crossing segments");
  // Fast and slow frame rates observe the same ballistic segment at least once.
  foreach(int fps in new[]{30,72,90,120})
  {
   bool hit=false;var before=new Vector3(0,1.7f,.4f);
   for(int i=0;i<fps;i++){float t=i/(float)fps;float reach=BrawlMath.Punch(t,out _);var at=Vector3.Lerp(new(0,1.7f,.4f),head,reach);if(BrawlContactMath.Active(t)&&BrawlContactMath.Hits(before,at,head))hit=true;before=at;}
   Check(hit,"no missed contact at "+fps+" fps");
  }
  Console.WriteLine("PASS BrawlContact: head/body sweeps, duck/side-step misses, invalid/teleport rejection, active window, degenerate geometry, 30/72/90/120 fps.");
 }
}
