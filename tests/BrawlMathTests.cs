using System;
using System.Numerics;
using XiiiXR;
class BrawlMathTests
{
 static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static void Main()
 {
  Check(BrawlMath.Damage(200,7)==14&&BrawlMath.Damage(100,99)==30,"damage remains configurable and bounded");
  Check(BrawlMath.Punch(.05f,out bool pulling)==0&&pulling,"readable wind-up before extension");
  Check(BrawlMath.Punch(BrawlMath.HitDelay,out _)==1&&BrawlMath.Punch(BrawlMath.HitDelay+BrawlMath.Hold+BrawlMath.Back+.01f,out _)==0,"full extension then recovery");
  Check(BrawlPlan.SwingTime>=BrawlMath.HitDelay+BrawlMath.Hold+BrawlMath.Back,"no next punch before recovery");
  var p=new BrawlPlan(new BrawlStyle{Circle=1},.65f);
  Check(p.Step(.1f,.9f,false,false,.5f,.5f)==0&&!p.SwingStarted&&p.Act==BrawlAct.Recover,"disarm reaction before fighting");
  p.Step(.66f,.9f,false,false,.5f,.5f);p.Step(.9f,.9f,false,false,.5f,.95f);
  Check(p.Act==BrawlAct.Swing&&p.SwingStarted&&p.LeftFist,"first punch uses lead hand");
  p.Step(1f,.9f,true,false,.5f,.5f);
  Check(p.Act==BrawlAct.Recover&&p.Side==0&&!p.SwingStarted,"stun immediately cancels punch and lateral motion");
  p.Step(1.1f,.9f,true,false,.5f,.5f);
  Check(p.Side==0,"sustained stun remains still");
  p.Step(1.2f,float.NaN,false,false,float.NaN,.5f);
  Check(p.Side==0&&!p.SwingStarted,"invalid tracking is idle, not chase");
  p.Step(1.3f,50,false,false,.5f,.5f);Check(p.Side==0,"out of range gives up");
  p=new BrawlPlan();p.Step(0,.9f,false,false,.5f,.95f);int first=p.Swings;
  p.Step(.7f,2.6f,false,false,.5f,.95f);Check(!p.SwingStarted&&p.Swings==first&&p.Act==BrawlAct.StepBack,"retreat out of reach cancels the remaining combo");
  p=new BrawlPlan();p.Step(0,.5f,false,false,.5f,.5f);Check(p.Act==BrawlAct.StepBack,"crowded: creates space before striking");
  p=new BrawlPlan();p.Step(0,.9f,false,false,.1f,.5f);Check(p.Act==BrawlAct.Feint&&!p.SwingStarted,"feint has no damaging swing");
  p=new BrawlPlan();for(float t=0;t<5;t+=.01f){p.Step(t,.9f,false,false,.5f,.95f,false);Check(!p.SwingStarted,"blocked sight, facing or attack slot prohibits new swings");}
  p=new BrawlPlan();p.Step(0,1.4f,false,false,.5f,.5f);Check(p.Act==BrawlAct.StepIn,"step in");
  p.Step(.33f,1.4f,false,false,.5f,.5f);Check(!p.SwingStarted&&p.Act==BrawlAct.Guard,"failed approach does not punch empty air");
  p=new BrawlPlan();p.Step(0,.9f,false,false,.5f,.95f);bool left=p.LeftFist;
  p.Step(.64f,.9f,false,false,.5f,.5f);Check(p.SwingStarted&&p.LeftFist!=left,"combo alternates hands");
  p.Step(1.28f,.9f,false,false,.5f,.5f);Check(p.Swings==3,"fresh aggressive fighter can complete three hits");
  p.Step(1.92f,.9f,false,false,.5f,.5f);Check(p.Act==BrawlAct.StepBack,"combo ends in an exit");
  // Long deterministic fight: bounded speed, real guard/exit/feint phases and recovery budget.
  var rng=new Random(19);p=new BrawlPlan(BrawlStyle.From(()=>(float)rng.NextDouble()));int swings=0,feints=0,exits=0;
  for(int i=0;i<18000;i++)
  {
   float t=i/90f;bool stunned=i%900<40;
   float v=p.Step(t,.92f,stunned,false,(float)rng.NextDouble(),(float)rng.NextDouble());
   Check(float.IsFinite(v)&&Math.Abs(v)<2.2f&&float.IsFinite(p.Side),"finite bounded motion");
   Check(p.Stamina>=0&&p.Stamina<=1,"bounded stamina");
   if(stunned)Check(v==0&&p.Side==0&&!p.SwingStarted,"stunned cannot act");
   if(p.SwingStarted)swings++;if(p.Act==BrawlAct.Feint)feints++;if(p.Act==BrawlAct.StepBack)exits++;
  }
  Check(swings>20&&swings<330&&feints>0&&exits>0,"varied fight with pauses, not constant punches");
  var hand=new[]{new Vector3(-.03f,0,.1f),new Vector3(-.01f,0,.1f),new Vector3(.01f,0,.1f),new Vector3(.03f,0,.1f)};
  Check(Vector3.Distance(FistCurlMath.Back(Vector3.Zero,hand,new(-.05f,-.02f,.06f),true),Vector3.UnitY)<1e-4f,"right hand palm frame");
  Check(Vector3.Distance(FistCurlMath.Back(Vector3.Zero,hand,new(.05f,-.02f,.06f),false),Vector3.UnitY)<1e-4f,"left hand palm frame");
  var (elbow,wrist)=ArmReachMath.Solve(new(0,1.4f,0),new(.3f,1.4f,0),new(.55f,1.4f,0),new(.1f,1.5f,.3f),new(.2f,1.1f,0));
  Check(Math.Abs(Vector3.Distance(elbow,new(0,1.4f,0))-.3f)<1e-4f&&Vector3.Distance(wrist,new(.1f,1.5f,.3f))<1e-4f,"IK preserves reach and bone length");
  Console.WriteLine("PASS BrawlMath: "+checks+" assertions; disarm recovery, facing/visibility gate, feints, alternating combos, exits, stamina, stun cancellation, 200 s deterministic fight.");
 }
}
