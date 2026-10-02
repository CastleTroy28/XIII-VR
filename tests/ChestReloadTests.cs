using System;using System.Numerics;using XiiiXR;
class ChestReloadTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 const float Frame=1f/90;
 // The grip moved from a to b over n frames (torso frame); true if it struck the chest on the way.
 static bool Move(ChestStrike s,ref float t,Vector3 a,Vector3 b,int n)
 {
  bool hit=false;
  for(int i=1;i<=n;i++){t+=Frame;hit|=s.Step(Vector3.Lerp(a,b,i/(float)n),t);}
  return hit;
 }
 static void Main()
 {
  var chest=new Vector3(0,-.30f,.08f);var ready=new Vector3(.18f,-.30f,.45f);var low=new Vector3(.25f,-.60f,.30f);
  Check(ChestReloadMath.Inside(chest)&&!ChestReloadMath.Inside(ready)&&!ChestReloadMath.Inside(low)&&!ChestReloadMath.Inside(new Vector3(0,-.02f,.05f))&&!ChestReloadMath.Inside(new Vector3(.4f,-.3f,.05f)),"the chest is not where the grip strikes it");
  // A strike: from the hand's ready place into the chest, fast.
  var s=new ChestStrike();float t=0;
  Check(!Move(s,ref t,ready,ready,3)&&s.Armed,"a grip out of the chest is not armed");
  Check(Move(s,ref t,ready,chest,12),"a grip struck against the chest (0.13 s) does not count");
  Check(!s.Armed&&!Move(s,ref t,chest,chest+new Vector3(0,0,-.05f),6),"one strike counted twice (pressed on, not out again)");
  // Out and in again: the next strike.
  Check(!Move(s,ref t,chest,ready,20)&&s.Armed&&Move(s,ref t,ready,chest,10),"out of the chest and back does not strike again");
  // A calm strike (0.6 s from the ready place) counts too.
  Check(!Move(s,ref t,chest,ready,20)&&Move(s,ref t,ready,chest,54),"a calm strike (0.6 m/s) does not count");
  // Slowly brought to the chest: no strike.
  s.Reset();Move(s,ref t,ready,ready,2);
  Check(!Move(s,ref t,ready,chest,180),"a grip brought slowly (2 s) to the chest strikes");
  // Moved out of the chest (forward, away from the body): no strike.
  s.Reset();Move(s,ref t,ready,ready,2);Move(s,ref t,ready,chest,180);Move(s,ref t,chest,ready,20);
  var s2=new ChestStrike();float t2=0;Move(s2,ref t2,new Vector3(0,-.30f,-.2f),new Vector3(0,-.30f,-.2f),2);
  Check(!Move(s2,ref t2,new Vector3(0,-.30f,-.2f),new Vector3(0,-.30f,.05f),10),"a grip coming forward from behind strikes the chest");
  // The magazine dropped with the grip already at the chest: out first.
  var s3=new ChestStrike();float t3=0;
  Check(!Move(s3,ref t3,chest+new Vector3(0,0,.1f),chest,10)&&!s3.Armed,"a grip already at the chest strikes without leaving it");
  Check(!Move(s3,ref t3,chest,chest+new Vector3(0,0,.12f),6)&&!s3.Armed,"inside the edge's margin re-arms");
  Check(Move(s3,ref t3,chest+new Vector3(0,0,.12f),ready,6)==false&&s3.Armed&&Move(s3,ref t3,ready,chest,10),"out of the chest the grip does not strike again");
  // A jump (a turn of the whole body, a teleport) is no strike; a frame gap neither.
  var s4=new ChestStrike();float t4=0;Move(s4,ref t4,ready,ready,2);
  t4+=Frame;Check(Vector3.Distance(ready,chest)>ChestReloadMath.MaxStep&&!s4.Step(chest,t4),"a jump of the body (one frame from the ready place into the chest) strikes");
  var s6=new ChestStrike();float t6=0;Move(s6,ref t6,ready,ready,2);t6+=.5f;
  Check(!s6.Step(chest,t6),"a strike measured across a long pause (half a second)");
  Check(!new ChestStrike().Step(new Vector3(float.NaN,0,0),1),"a lost pose strikes");
  // Low frame rates (45 Hz) and a quick jab count.
  var s7=new ChestStrike();float t7=0;Move(s7,ref t7,ready,ready,2);
  bool slowFrames=false;for(int i=1;i<=6;i++){t7+=1f/45;slowFrames|=s7.Step(Vector3.Lerp(ready,chest,i/6f),t7);}
  Check(slowFrames,"a strike at 45 frames a second does not count");
  // The torso frame: the head's heading only, kept while looking down.
  Check(ChestReloadMath.Heading(new Vector3(1,0,0),out float yaw)&&MathF.Abs(yaw-MathF.PI/2)<1e-4f&&!ChestReloadMath.Heading(new Vector3(.1f,-.99f,.1f),out _),"the heading is not the head's (or looking down changes it)");
  var head=new Vector3(2,1.7f,3);var world=ChestReloadMath.World(chest,head,yaw);
  Check(Vector3.Distance(ChestReloadMath.Local(world,head,yaw),chest)<1e-4f,"the torso frame does not map back");
  Check(Vector3.Distance(world,head+new Vector3(.08f,-.30f,0))<1e-4f,"turned right, the chest is not in front of the eyes' new heading");
  Check(ChestReloadMath.Inside(ChestReloadMath.GlowPoint),"the glow is not on the chest");
  // The magazine in at once, the slide forward (from a magazine out, or a slide left back).
  var m=new ManualReloadState();m.ObserveRounds(0);Check(m.SlideLocked&&m.NeedsRack&&m.Installed,"the last round does not lock the slide back");
  m.Detach(0,false);Check(!m.Installed&&m.BlocksFire&&m.SlideLocked,"the dropped magazine leaves the gun ready to fire");
  m.QuickLoad();Check(m.Installed&&!m.NeedsRack&&!m.SlideLocked&&!m.BlocksFire&&m.VisualRackTravel==0&&!m.Holding,"the magazine struck in leaves the gun blocked or the slide back");
  var part=new ManualReloadState();part.Detach(7,false);Check(!part.Installed&&!part.SlideLocked,"a magazine with rounds dropped locks the slide back");
  part.QuickLoad();Check(part.Installed&&!part.BlocksFire,"a magazine struck in after a partial one leaves the gun blocked");
  var held=new ManualReloadState();held.Detach(0,false);held.Supply(12);held.QuickLoad();Check(!held.Holding&&held.HeldRounds==0,"a magazine held in the other hand survives the quick load");
  Console.WriteLine("PASS: 0.1.183 pistols reloaded with their own hand (both hands full): the grip struck into the chest counts (90 and 45 frames a second), once until it is out again; brought slowly, from behind, already there, over a jump of the body or a long pause does not; the torso frame turns with the head's heading only; the magazine struck in at once lets the slide go forward and the gun fire.");
 }
}
