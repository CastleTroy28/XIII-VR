using System;using System.Numerics;using XiiiXR;
class CombatTempoTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var k=new HitSkeleton{
   SpineLow=new(0,1,0),SpineMid=new(0,1.15f,0),SpineTop=new(0,1.3f,0),NeckBase=new(0,1.45f,0),NeckTop=new(0,1.55f,0),Head=new(0,1.6f,0),Chin=new(0,1.55f,.09f),
   ShoulderL=new(-.18f,1.42f,0),ShoulderR=new(.18f,1.42f,0),ElbowL=new(-.2f,1.15f,.1f),ElbowR=new(.2f,1.15f,.1f),WristL=new(-.15f,1.1f,.35f),WristR=new(.15f,1.1f,.35f),
   HipL=new(-.1f,.95f,0),HipR=new(.1f,.95f,0),KneeL=new(-.1f,.5f,.05f),KneeR=new(.1f,.5f,.05f)};
  var bodyHit=HitReactionMath.Plan(k.SpineTop+Vector3.UnitZ*.1f,-Vector3.UnitZ,4,false,k);
  Check(bodyHit.Stun<.8f&&bodyHit.Stun>.1f,"actual chest plan uses faster recovery");
  Check(BrawlPlan.CloseSpeed>=1.5f&&BrawlPlan.CloseSpeed<=1.9f,"brisk approach without a sprint");
  foreach(float strength in new[]{.1f,.3f,.6f,1f})foreach(HitRegion region in Enum.GetValues<HitRegion>())foreach(int fps in new[]{30,72,120})
  {
   var hit=new HitPlan{Strength=strength,Region=region,Uppercut=region==HitRegion.Head};
   float stun=HitReactionMath.StunSeconds(hit);Check(stun>0&&stun<=.95f,"stun bound");
   var p=new BrawlPlan();float attack=-1;bool pending=true;
   for(int i=0;i<fps*3;i++)
   {
    float t=i/(float)fps;bool held=t<stun;bool struck=pending&&!held;if(struck)pending=false;
    p.Step(t,.9f,held,struck,.5f,.5f);
    if(held)Check(!p.SwingStarted&&p.Side==0,"shorter stun still stops strikes and movement");
    if(p.SwingStarted){attack=t;break;}
   }
   Check(attack>=stun&&attack<stun+.5f,"recovery pauses stack after stun: "+attack+" stun="+stun+" fps="+fps);
  }
  // Faster springs preserve the initial visible response, but no seconds of residual arm wobble.
  foreach(int fps in new[]{30,72,120})
  {
   var hit=new HitPlan();hit.Push[(int)HitJoint.ArmR]=new Vector3(0,5,0);
   var state=new HitReactionState();state.Add(hit,0);float peak=0,after=0;
   for(int i=1;i<=fps*2;i++)
   {
    state.Step(1f/fps,i/(float)fps);float angle=state.Angle((int)HitJoint.ArmR).Length();peak=Math.Max(peak,angle);
    if(i>=fps*1.5f)after=Math.Max(after,angle);
    Check(float.IsFinite(angle)&&angle<=HitReactionState.ArmMaxAngle+.0001f,"stable spring bounds");
   }
   Check(peak>.4f&&after<.04f,"readable impact followed by fast recovery: "+peak+" / "+after);
  }
  Console.WriteLine("PASS CombatTempo: brisk approach; all hit regions/strengths recover into a legal attack within 0.5 s of stun ending; stun <=0.95 s; visible impact and quick stable spring return at 30/72/120 fps.");
 }
}
