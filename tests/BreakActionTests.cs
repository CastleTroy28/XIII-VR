using System;using System.Numerics;using XiiiXR;
class BreakActionTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static string[] L(string s)=>s.Split(',');
 static void Main()
 {
  // 0.1.194: the double-barrelled shotgun reloaded by hand.
  var st=new BreakActionState();st.Load(2);
  Check(st.LiveRounds==2&&!st.Open&&!st.BlocksFire,"a loaded gun from the game is not two live chambers, shut");
  Check(st.Observe(1)==1&&st.Chambers[0]==BreakChamber.Spent&&st.Chambers[1]==BreakChamber.Live,"a shot does not fire the right barrel first");
  // Opened: only the fired case flies out, the unfired shell stays.
  var out1=st.OpenGun();
  Check(out1.Length==1&&out1[0]==0&&st.Chambers[0]==BreakChamber.Empty&&st.Chambers[1]==BreakChamber.Live&&st.Open,"opening threw out an unfired shell or kept the fired case");
  Check(st.OpenGun().Length==0,"opening twice ejects again");
  for(int i=0;i<30;i++)st.Tick(1f/90);Check(st.Swing==1,"the barrels do not swing open");
  Check(st.BlocksFire,"an open gun fires");
  // A shell only into an open empty chamber, only while held.
  Check(!st.Insert(0),"a shell went in without one in the hand");
  Check(st.Take()&&!st.Take()&&st.Holding,"a second shell taken into a full hand");
  Check(!st.Insert(1)&&st.Holding,"a shell pushed into a loaded chamber");
  Check(st.Insert(0)&&!st.Holding&&st.LiveRounds==2,"a shell pushed into the empty chamber did not go in");
  // Shut: by a flick up and down (the barrels' weight), not by a slow lift.
  float t=0;
  for(int i=0;i<30;i++,t+=1f/90)Check(!st.Flick(t,.25f,20),"a slow lift shut the gun");
  Check(st.Open,"a slow lift shut the gun");
  bool shut=false;
  for(int i=0;i<30;i++,t+=1f/90)Check(!st.Flick(t,.7f,20)&&st.Open,"an ordinary lift of the gun shut it");
  for(int i=0;i<5;i++,t+=1f/90)shut|=st.Flick(t,1.1f,40);
  for(int i=0;i<5&&!shut;i++,t+=1f/90)shut|=st.Flick(t,-.2f,-30);
  Check(shut&&!st.Open,"a flick up and down did not shut the gun");
  Check(st.BlocksFire,"the barrels still swinging shut: it fires");
  for(int i=0;i<30;i++)st.Tick(1f/90);
  Check(!st.BlocksFire&&st.Swing==0,"shut, it does not fire");
  // A flick up then held up for long: nothing once the moment has passed.
  st.OpenGun();for(int i=0;i<30;i++)st.Tick(1f/90);Check(st.Swing==1,"the barrels do not swing fully open");
  t+=1;for(int i=0;i<5;i++,t+=1f/90)st.Flick(t,1.1f,0);t+=.6f;
  Check(!st.Flick(t,0,0)&&st.Open,"a flick long ago shut the gun");
  // The muzzle raised fast and stopped shuts it too.
  var fresh=new BreakActionState();fresh.Load(2);fresh.Observe(0);fresh.OpenGun();
  Check(!fresh.Flick(t,1.2f,0)&&!fresh.Flick(t+.02f,0,0)&&fresh.Open,"the motion that opened the gun shut it at once");
  t+=1;st.Flick(t,0,220);t+=1f/90;st.Flick(t,0,230);t+=1f/90;
  Check(st.Flick(t,0,10)&&!st.Open,"a wrist flick (the muzzle raised and stopped) did not shut the gun");
  // 0.1.241: with the other hand at the belt, holding a shell or just done with one (quiet), no flick shuts it.
  {
   var q=new BreakActionState();q.Load(2);q.OpenGun();q.Tick(1);float tq=50;
   for(int i=0;i<5;i++,tq+=1f/90)q.Flick(tq,1.2f,250,true);
   for(int i=0;i<5;i++,tq+=1f/90)Check(!q.Flick(tq,-.3f,-40,true)&&q.Open,"a flick while quiet shut the gun");
   for(int i=0;i<3;i++,tq+=1f/90)Check(!q.Flick(tq,0,0)&&q.Open,"a flick made while quiet shut the gun afterwards");
   Console.WriteLine("PASS: 0.1.241 the barrels stay open while the other hand is at the belt or with a shell (B or a deliberate flick shut them).");
  }
  Check(!st.CloseGun(),"shutting a shut gun");
  // Both fired: both cases out; the held shell let go of goes back.
  st.Observe(0);Check(st.Chambers[0]==BreakChamber.Spent&&st.Chambers[1]==BreakChamber.Spent,"both barrels fired not both spent");
  var both=st.OpenGun();Check(both.Length==2&&!st.AnySpent&&st.AnyEmpty,"both fired cases not out");
  st.Take();Check(st.LetGo()==1&&!st.Holding&&st.LetGo()==0,"the shell let go of not given back once");
  // The game's own rounds (a checkpoint) fill the chambers.
  st.Observe(2);Check(st.LiveRounds==2,"rounds the game put in not in the chambers");
  var one=new BreakActionState();one.Load(1);Check(one.Chambers[0]==BreakChamber.Live&&one.Chambers[1]==BreakChamber.Empty,"one round from the game not in the right barrel");
  Check(one.Observe(-3)==1&&one.Observe(9)==0&&one.LiveRounds==2,"out-of-range rounds");
  Check(!one.Flick(float.NaN,1,1)&&!one.Flick(0,float.NaN,0),"bad numbers shut the gun");
  // The game's rig (log of 0.1.192).
  var rig=L("wpn_shotgun_hunting_SH_BND_JNT,wpn_shotgun_hunting_lock_SH_BND_JNT,wpn_shotgun_hunting_tilt_SH_BND_JNT,wpn_shotgun_hunting_shell_right_zero_SH_JNT,wpn_shotgun_hunting_shell_right_SH_BND_JNT,wpn_shotgun_hunting_shell_left_zero_SH_JNT,wpn_shotgun_hunting_shell_left_SH_BND_JNT,wpn_shotgun_hunting_shell_middle_zero_SH_JNT,wpn_shotgun_hunting_shell_middle_SH_BND_JNT,wpn_shotgun_hunting_trigger_right_SH_BND_JNT,wpn_shotgun_hunting_trigger_left_SH_BND_JNT");
  var f=BreakRig.Find(rig);
  Check(rig[f.Root]=="wpn_shotgun_hunting_SH_BND_JNT"&&rig[f.Tilt]=="wpn_shotgun_hunting_tilt_SH_BND_JNT"&&rig[f.Lock]=="wpn_shotgun_hunting_lock_SH_BND_JNT"
   &&rig[f.Right]=="wpn_shotgun_hunting_shell_right_SH_BND_JNT"&&rig[f.Left]=="wpn_shotgun_hunting_shell_left_SH_BND_JNT"&&rig[f.Middle]=="wpn_shotgun_hunting_shell_middle_SH_BND_JNT","hunting shotgun rig: "+f);
  Check(BreakRig.Chamber(rig[3],0)&&BreakRig.Chamber(rig[4],0)&&!BreakRig.Chamber(rig[4],1)&&BreakRig.Chamber(rig[6],1)&&!BreakRig.Chamber(rig[8],0)&&BreakRig.Shell(rig[8])&&!BreakRig.Shell(rig[2]),"chamber shells by name");
  var pump=L("wpn_shotgun_trigger_BND_JNT,wpn_shotgun_shell_BND_JNT,wpn_shotgun_shell_zero_JNT,wpn_shotgun_magazine_BND_JNT,wpn_shotgun_forestock_BND_JNT,wpn_shotgun_BND_JNT");
  Check(!BreakRig.Is(pump)&&BreakRig.Is(rig),"the pump shotgun taken for a double-barrelled one");
  var rb=ReloadBones.Find("shotgun",rig);Check(rig[rb.Root]=="wpn_shotgun_hunting_SH_BND_JNT"&&rig[rb.Ammo]=="wpn_shotgun_hunting_shell_middle_SH_BND_JNT","the hand's shell of the double-barrelled rig: "+rb);
  // The barrels' axis: a gun whose muzzle point faces 35 degrees off it.
  var pts=new Vector3[400];var dir=Vector3.Normalize(new Vector3(0,-MathF.Sin(.61f),MathF.Cos(.61f)));var rnd=new Random(3);
  for(int i=0;i<pts.Length;i++)pts[i]=dir*(float)(rnd.NextDouble()*.4)+new Vector3((float)rnd.NextDouble()*.02f,(float)rnd.NextDouble()*.02f,(float)rnd.NextDouble()*.02f);
  var ax=BreakRig.Axis(pts);Check(Vector3.Dot(ax,dir)>.995f&&ax.Z>0,"the barrels' axis not found: "+ax);
  // The chambers at the barrels' rear: side by side at the top.
  var slice=new System.Collections.Generic.List<Vector3>();
  for(int b=0;b<2;b++)for(int k=0;k<36;k++){float a=k*MathF.PI/18;slice.Add(new Vector3((b==0?.01f:-.01f)+.01f*MathF.Cos(a),.01f*MathF.Sin(a),-.2f));}
  for(int k=0;k<10;k++)slice.Add(new Vector3(0,-.02f,-.2f)); // the lumps under the breech
  var ch=BreakRig.Chambers(slice.ToArray(),-.2f);
  Check(Math.Abs(ch[0].X-.01f)<.002f&&Math.Abs(ch[1].X+.01f)<.002f&&Math.Abs(ch[0].Y)<.002f&&ch[0].Z==-.2f,"the chambers not at the barrels' rear, right and left: "+ch[0]+" "+ch[1]);
  Check(BreakRig.InChamber(new Vector3(.01f,0,-.18f),ch[0],.5f)&&!BreakRig.InChamber(new Vector3(.01f,.15f,-.18f),ch[0],.5f)&&!BreakRig.InChamber(new Vector3(.01f,0,.1f),ch[0],.5f),"a parked shell taken for a loaded chamber");
  Console.WriteLine("PASS: 0.1.194 double-barrelled shotgun by hand: opened, only fired cases out (an unfired shell stays), shells only into open empty chambers, shut by a flick up and down or of the wrist (not a slow lift, not a flick long ago), no fire open or swinging, shots and the game's rounds followed; its rig, barrel axis and chambers. Drawing and feel need the headset.");
 }
}
