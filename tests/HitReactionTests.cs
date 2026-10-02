using System;using System.Numerics;using XiiiXR;
class HitReactionTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 // A standing NPC in its own frame (X right, Y up, Z forward), feet at 0.
 static HitSkeleton Body()=>new HitSkeleton{
  SpineLow=new(0,1.0f,0),SpineMid=new(0,1.15f,0),SpineTop=new(0,1.3f,0),NeckBase=new(0,1.45f,0),NeckTop=new(0,1.55f,0),Head=new(0,1.6f,0),Chin=new(0,1.55f,.09f),
  ShoulderL=new(-.18f,1.42f,0),ShoulderR=new(.18f,1.42f,0),ElbowL=new(-.2f,1.15f,.1f),ElbowR=new(.2f,1.15f,.1f),WristL=new(-.15f,1.1f,.35f),WristR=new(.15f,1.1f,.35f),
  HipL=new(-.1f,.95f,0),HipR=new(.1f,.95f,0),KneeL=new(-.1f,.5f,.05f),KneeR=new(.1f,.5f,.05f)};
 // Runs the reaction for seconds at 90 Hz; returns the state and the total knockback.
 static (HitReactionState s,Vector3 moved) Run(HitPlan plan,float seconds)
 {
  var s=new HitReactionState();s.Add(plan,0);var moved=Vector3.Zero;
  for(int i=1;i<=(int)(seconds*90);i++)moved+=s.Step(1/90f,i/90f);
  return (s,moved);
 }
 static float Max(Func<float,float> f,float from,float to){float m=float.MinValue;for(float t=from;t<=to;t+=1/90f)m=Math.Max(m,f(t));return m;}
 static void Main()
 {
  var k=Body();
  // The player faces the NPC, so the player's left is the NPC's right (+X).
  // A punch into the belly on the player's left: doubled over, bent towards
  // that side (the top tilts to the NPC's right: -Z), that side pulled back.
  {
   var plan=HitReactionMath.Plan(new Vector3(.1f,1.08f,.12f),new Vector3(0,0,-1),4f,false,k);
   Check(plan.Region==HitRegion.Belly&&plan.Side==1,"belly/side: "+plan.Region+" "+plan.Side);
   var (s,moved)=Run(plan,.45f);var mid=s.Angle((int)HitJoint.SpineMid);var top=s.Angle((int)HitJoint.SpineTop);
   Check(mid.X>.12f&&top.X>.1f,"not doubled over: mid "+mid+" top "+top);
   Check(mid.Z<-.04f&&top.Z<-.03f,"not bent towards the side hit (player's left): mid "+mid+" top "+top);
   Check(moved.Z<-.03f&&Math.Abs(moved.X)<.01f,"not pushed back: "+moved);
   var mirror=HitReactionMath.Plan(new Vector3(-.1f,1.08f,.12f),new Vector3(0,0,-1),4f,false,k);
   var (m,_)=Run(mirror,.45f);Check(m.Angle((int)HitJoint.SpineMid).Z>.04f&&mirror.Side==-1,"the other side does not bend the other way");
   var (end,_)=Run(plan,5f);Check(end.Settled,"not back to the animated pose after 5 s: "+end.Angle((int)HitJoint.SpineMid));
   // 0.1.141: it bends slowly: less at 0.1 s than at 0.8 s; still bent while stunned.
   var (early,_)=Run(plan,.035f);Check(early.Angle((int)HitJoint.SpineMid).X<mid.X*.5f,"reaction lacks a short impulse-to-pain transition");
   var (held,_)=Run(plan,plan.Stun*.9f);Check(held.Angle((int)HitJoint.SpineMid).X>.12f,"straightened before it came to ("+plan.Stun+" s)");
  }
  // An uppercut into the jaw: the head and the upper body go back, the NPC is pushed back.
  {
   var d=Vector3.Normalize(new Vector3(0,.85f,-.5f));
   var plan=HitReactionMath.Plan(new Vector3(0,1.55f,.1f),d,4.5f,false,k);
   Check(plan.Region==HitRegion.Head&&plan.Uppercut,"uppercut not recognised: "+plan.Region+" "+plan.Uppercut);
   var s=new HitReactionState();s.Add(plan,0);float head=0,top=0;var moved=Vector3.Zero;
   for(int i=1;i<=45;i++){moved+=s.Step(1/90f,i/90f);head=Math.Min(head,s.Angle((int)HitJoint.Head).X);top=Math.Min(top,s.Angle((int)HitJoint.SpineTop).X);}
   Check(head<-.35f&&top<-.12f,"not thrown back: head "+head+" spine "+top);
   Check(moved.Z<-.15f,"uppercut does not push back: "+moved);
   var weak=HitReactionMath.Plan(new Vector3(0,1.55f,.1f),d,1.6f,false,k);var ws=new HitReactionState();ws.Add(weak,0);float wh=0;
   for(int i=1;i<=45;i++){ws.Step(1/90f,i/90f);wh=Math.Min(wh,ws.Angle((int)HitJoint.Head).X);}
   Check(wh>head*.6f,"a slow uppercut as strong as a fast one: "+wh+" vs "+head);
  }
  // A straight punch to the face: the head snaps back, no uppercut.
  {
   var plan=HitReactionMath.Plan(new Vector3(0,1.66f,.1f),new Vector3(0,0,-1),4f,false,k);
   var s=new HitReactionState();s.Add(plan,0);float head=0;for(int i=1;i<=30;i++){s.Step(1/90f,i/90f);head=Math.Min(head,s.Angle((int)HitJoint.Head).X);}
   Check(plan.Region==HitRegion.Head&&!plan.Uppercut&&head<-.12f,"jab: "+plan.Region+" uppercut="+plan.Uppercut+" head "+head);
  }
  // A hook from the player's right (the NPC's left cheek), across towards the
  // NPC's right: the face turns to its right and the head tilts that way.
  {
   var plan=HitReactionMath.Plan(new Vector3(-.08f,1.62f,.06f),Vector3.Normalize(new Vector3(1,0,-.2f)),4f,false,k);
   var s=new HitReactionState();s.Add(plan,0);float yaw=0,roll=0;for(int i=1;i<=30;i++){s.Step(1/90f,i/90f);var h=s.Angle((int)HitJoint.Head);yaw=Math.Max(yaw,h.Y);roll=Math.Min(roll,h.Z);}
   Check(plan.Hook&&yaw>.08f&&roll<-.04f,"hook: hook="+plan.Hook+" yaw "+yaw+" roll "+roll);
  }
  // The chest hit on the NPC's right: it leans back and that shoulder goes back (turn right).
  {
   var plan=HitReactionMath.Plan(new Vector3(.12f,1.36f,.12f),new Vector3(0,0,-1),4f,false,k);
   var s=new HitReactionState();s.Add(plan,0);float lean=0,turn=0;for(int i=1;i<=20;i++){s.Step(1/90f,i/90f);var a=s.Angle((int)HitJoint.SpineTop);lean=Math.Min(lean,a.X);turn=Math.Max(turn,a.Y);}
   Check(plan.Region==HitRegion.Chest&&lean<-.03f&&turn>.02f,"chest: "+plan.Region+" lean "+lean+" turn "+turn);
  }
  // The forearm (the weapon hand): the arm swings, the side is reported.
  {
   var plan=HitReactionMath.Plan(new Vector3(.17f,1.12f,.25f),new Vector3(-1,0,0),3f,false,k);
   Check(plan.Region==HitRegion.ForearmR&&plan.ForearmSide==1&&plan.Push[(int)HitJoint.ForearmR].Length()>.5f,"forearm: "+plan.Region+" side "+plan.ForearmSide);
   var l=HitReactionMath.Plan(new Vector3(-.17f,1.12f,.25f),new Vector3(1,0,0),3f,false,k);Check(l.ForearmSide==0,"left forearm side");
   // 0.1.145: the body behind the arm takes the blow too (it bends, it is pushed back).
   var into=HitReactionMath.Plan(new Vector3(.12f,1.15f,.3f),new Vector3(0,0,-1),4.5f,false,k);
   float bodyMove=into.Push[(int)HitJoint.SpineLow].Length()+into.Push[(int)HitJoint.SpineMid].Length()+into.Pain[(int)HitJoint.SpineMid].Length();
   Check(into.Carried!=null&&bodyMove>.5f&&into.Knockback.Length()>.05f,"an arm hit does not reach the body: carried "+into.Carried+" move "+bodyMove+" knockback "+into.Knockback.Length());
  }
  // 0.1.146: a blow to the throat drops the head forward and keeps it down a while.
  {
   var plan=HitReactionMath.Plan(new Vector3(0,1.5f,.07f),new Vector3(0,0,-1),5f,false,k);
   Check(plan.Region==HitRegion.Neck,"throat: "+plan.Region);
   var (s,_)=Run(plan,.6f);var head=s.Angle((int)HitJoint.Head);var neck=s.Angle((int)HitJoint.Neck);
   Check(head.X>.3f&&neck.X>.3f,"the head does not drop: head "+head+" neck "+neck);
   var (late,_)=Run(plan,5f);Check(late.Angle((int)HitJoint.Head).Length()<.03f,"the head does not come back up: "+late.Angle((int)HitJoint.Head).Length());
  }
  // 0.1.146: a hit arm flies off by its own inertia and swings back.
  {
   var plan=HitReactionMath.Plan(new Vector3(.2f,1.2f,.2f),new Vector3(-1,0,0),5f,false,k);
   var s=new HitReactionState();s.Add(plan,0);float peak=0,peakAt=0;
   for(int i=1;i<=270;i++){s.Step(1/90f,i/90f);float a=s.Angle((int)HitJoint.ArmR).Length();if(a>peak){peak=a;peakAt=i/90f;}}
   Check(peak>.6f&&peakAt>=.035f&&peakAt<.3f,"the arm does not fly off: "+peak+" rad at "+peakAt+" s");
   Check(s.Angle((int)HitJoint.ArmR).Length()<.1f,"the arm does not swing back");
   Check(HitReactionMath.ShotImpulse("shotgun")>HitReactionMath.ShotImpulse("pistol")&&HitReactionMath.ShotImpulse("sniper")>=HitReactionMath.ShotImpulse("ak47"),"shot impulses");
  }
  // Many hard hits at once: every joint stays within its limit; strength grows with speed.
  {
   var s=new HitReactionState();
   for(int i=0;i<12;i++){s.Add(HitReactionMath.Plan(new Vector3(0,1.55f,.1f),Vector3.Normalize(new Vector3(0,.9f,-.3f)),6,true,k),i*.02f);s.Step(.02f,i*.02f+.02f);}
   for(int i=0;i<60;i++)s.Step(1/90f,.24f+i/90f);
   for(int j=0;j<HitPlan.Joints;j++)Check(s.Angle(j).Length()<=HitReactionState.MaxAngleOf(j)+1e-4f,"joint "+j+" over the limit");
   Check(HitReactionMath.Strength(2,false)<HitReactionMath.Strength(4,false)&&HitReactionMath.Strength(9,false)==1&&HitReactionMath.Strength(.3f,false)==.1f&&HitReactionMath.Strength(float.NaN,false)==.1f,"strength");
   Check(HitReactionMath.Plan(Vector3.Zero,Vector3.Zero,4,false,k).Push[0]==Vector3.Zero,"no direction, no reaction");
   Check(HitReactionMath.Envelope(0,.3f)==0&&HitReactionMath.Envelope(.35f,.3f)==1&&HitReactionMath.Envelope(5,.3f)==0,"pain envelope");
  }
  // 0.1.141: how long it is out of it.
  {
   var hard=HitReactionMath.Plan(new Vector3(.1f,1.08f,.12f),new Vector3(0,0,-1),5f,false,k);var light=HitReactionMath.Plan(new Vector3(.1f,1.08f,.12f),new Vector3(0,0,-1),1.5f,false,k);
   var arm=HitReactionMath.Plan(new Vector3(.17f,1.12f,.25f),new Vector3(-1,0,0),5f,false,k);
   var upper=HitReactionMath.Plan(new Vector3(0,1.55f,.1f),Vector3.Normalize(new Vector3(0,.85f,-.5f)),5f,false,k);var jab=HitReactionMath.Plan(new Vector3(0,1.66f,.1f),new Vector3(0,0,-1),5f,false,k);
   Check(hard.Stun>=.85f&&hard.Stun<=.95f&&light.Stun<.35f&&light.Stun>.2f,"belly stun hard "+hard.Stun+" light "+light.Stun);
   // 0.1.145: a hard blow on the arm held in front carries into the body: stunned too, a little less than straight into the belly.
   Check(arm.Stun>=.6f&&arm.Stun<hard.Stun&&upper.Stun>jab.Stun&&upper.Stun<=HitReactionMath.MaxStun,"arm "+arm.Stun+" uppercut "+upper.Stun+" jab "+jab.Stun);
   Check(hard.PainHold>=hard.Stun-HitReactionMath.PainRise-1e-4f,"pain not held while stunned");
  }
  Console.WriteLine("PASS: NPC stun: hard blow to the belly 0.85–0.95 s, light 0.2–0.35 s, an arm hit carried into the body (a little less), an uppercut longer than a jab; the pain pose is held while stunned, brief impulse followed by a readable pain pose and quick recovery.");
  Console.WriteLine("PASS: NPC hit reactions: a punch into the belly on the player's left doubles the NPC over towards that side and pushes it back; an uppercut throws the head and upper body back (weaker when slow); a jab snaps the head back; a hook turns the head; a chest hit leans back and turns; a forearm hit swings the arm, names the hand and carries into the body behind it; a throat blow drops the head; a hit arm flies off and swings back; stacked hits stay within the joint limits; back to the animated pose within 5 s.");
 }
}
