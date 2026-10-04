using System;using System.Linq;using System.Numerics;using XiiiXR;
class HolsterTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // Body places by kind, in order of preference.
  Check(HolsterLayout.Candidates("pistol")[0]==HolsterSlot.BeltRight&&HolsterLayout.Candidates("revolver").Count==4&&HolsterLayout.Candidates("uzi")[3]==HolsterSlot.ArmpitRight,"small guns: belt then armpits");
  Check(HolsterLayout.Candidates("ak47")[0]==HolsterSlot.Belly&&HolsterLayout.Candidates("m60")[0]==HolsterSlot.Belly&&HolsterLayout.Candidates("shotgun")[1]==HolsterSlot.RightBack,"rifles/shotgun on the belly, shotgun also behind the right shoulder");
  Check(HolsterLayout.Candidates("sniper")[0]==HolsterSlot.LeftShoulder&&HolsterLayout.Candidates("crossbow")[0]==HolsterSlot.LeftShoulder&&HolsterLayout.Candidates("bazooka")[0]==HolsterSlot.RightBack,"SVD/crossbow over the left shoulder, bazooka behind the right");
  // 0.1.140: every long gun on the belly and over both shoulders.
  foreach(var longGun in new[]{"ak47","m16","m60","rifle","shotgun","heavy","bazooka","sniper","crossbow"})
  {var cand=HolsterLayout.Candidates(longGun);Check(cand.Count==3&&cand.Contains(HolsterSlot.Belly)&&cand.Contains(HolsterSlot.LeftShoulder)&&cand.Contains(HolsterSlot.RightBack),longGun+": not on the belly and both shoulders");}
  Check(!HolsterLayout.Candidates("pistol").Contains(HolsterSlot.Belly)&&!HolsterLayout.Candidates("knife").Contains(HolsterSlot.RightBack),"small weapons on long-gun places");
  var ls=HolsterLayout.Pose(HolsterSlot.LeftShoulder,false);var rs=HolsterLayout.Pose(HolsterSlot.RightBack,false);
  Check(Math.Abs(ls.Reach.X+rs.Reach.X)<1e-4f&&rs.Reach.X>0&&rs.Forward.X>0&&ls.Forward.X<0&&rs.Grip.Z<ls.Grip.Z,"the right shoulder is not the left one mirrored (a little further back)");
  Check(HolsterLayout.Candidates("knife")[0]==HolsterSlot.ChestLeft&&HolsterLayout.Candidates("grenade")[0]==HolsterSlot.ChestRight&&HolsterLayout.Candidates("prop").Count==0,"knife/grenade on the chest; props nowhere");
  Check(HolsterLayout.Firearm("ak47")&&!HolsterLayout.Firearm("knife")&&!HolsterLayout.Firearm("grenade")&&!HolsterLayout.Firearm("mounted")&&!HolsterLayout.Firearm(""),"grip-held kinds");
  // 0.1.140: a weapon's own roll from its left/right bone pairs (+ = counter-clockwise, right one higher).
  {
   float rad=6*MathF.PI/180;Vector3 R(float x,float y)=>new Vector3(x*MathF.Cos(rad)-y*MathF.Sin(rad),x*MathF.Sin(rad)+y*MathF.Cos(rad),.1f);
   var pairs=new System.Collections.Generic.List<(Vector3,Vector3)>{(R(-.3f,0),R(.3f,0)),(R(-.2f,.05f),R(.2f,.05f)),(R(-.25f,-.02f),R(.25f,-.02f))};
   Check(Math.Abs(HandMirror.PairRoll(pairs)-6)<.01f,"roll of a weapon turned 6 deg: "+HandMirror.PairRoll(pairs));
   var swapped=new System.Collections.Generic.List<(Vector3,Vector3)>{(R(.3f,0),R(-.3f,0))};Check(Math.Abs(HandMirror.PairRoll(swapped)-6)<.01f,"pair order changes the roll");
   Check(float.IsNaN(HandMirror.PairRoll(new System.Collections.Generic.List<(Vector3,Vector3)>{(new Vector3(0,0,0),new Vector3(.01f,.01f,0))}))&&float.IsNaN(HandMirror.PairRoll(new System.Collections.Generic.List<(Vector3,Vector3)>())),"pairs too close / none");
  }
  Check(HolsterLayout.Visible(HolsterSlot.RightBack)&&HolsterLayout.Visible(HolsterSlot.Belly)&&!HolsterLayout.Visible(HolsterSlot.None),"behind the right shoulder is drawn (0.1.140)");
  // Poses: belt pistols muzzle down, armpits muzzle back, belly rifle muzzle left (right-handed) / right (left-handed).
  var belt=HolsterLayout.Pose(HolsterSlot.BeltRight,false);Check(belt.Forward.Y<-.9f&&belt.Grip.X>0&&belt.Grip.Y<-.45f&&belt.Grip.Y>-.56f,"belt pistol pose (at the belt, not below it)");
  Check(HolsterLayout.Pose(HolsterSlot.ChestLeft,false).Grip.Y>HolsterLayout.Pose(HolsterSlot.Belly,false).Grip.Y+.1f&&HolsterLayout.CenterPlaced("knife")&&HolsterLayout.CenterPlaced("grenade")&&!HolsterLayout.CenterPlaced("pistol"),"knife/grenade above the belly rifle, placed by their middle");
  Check(HolsterLayout.Pose(HolsterSlot.ArmpitLeft,false).Forward.Z<-.9f,"armpit muzzle back");
  var belly=HolsterLayout.Pose(HolsterSlot.Belly,false);Check(belly.Forward.X<-.9f&&belly.Grip.Z>.1f,"belly rifle muzzle left, in front");
  var mirrored=HolsterLayout.Pose(HolsterSlot.Belly,true);Check(mirrored.Forward.X>.9f&&mirrored.Grip.X<0,"left-handed belly rifle muzzle right");
  Check(HolsterLayout.Pose(HolsterSlot.BeltRight,true).Grip.X<0,"left-handed: the first belt place on the left hip");
  foreach(HolsterSlot s in Enum.GetValues(typeof(HolsterSlot)))
  {
   if(s==HolsterSlot.None)continue;var p=HolsterLayout.Pose(s,false);var q=HolsterLayout.Rotation(p);
   Check(Vector3.Distance(Vector3.Transform(Vector3.UnitZ,q),Vector3.Normalize(p.Forward))<1e-3f,"rotation forward "+s);
   var up=Vector3.Transform(Vector3.UnitY,q);Check(MathF.Abs(Vector3.Dot(up,Vector3.Normalize(p.Forward)))<1e-3f&&Vector3.Dot(up,p.Up)>0,"rotation up "+s);
   Check(Vector3.Distance(p.Reach,p.Grip)<.6f&&p.Reach.Y<0,"reach point "+s);
  }
  Console.WriteLine("PASS: body places: pistols belt/armpits, rifles/shotgun belly, SVD/crossbow left shoulder, bazooka/shotgun right back (hidden), knife/grenade chest; left-handed mirror; rotations.");
  // Assignment: first come first served; the rest out of sight; manual hanging on a free place.
  var a=new HolsterAssignment();
  Check(a.Assign(21,"pistol")==HolsterSlot.BeltRight&&a.Assign(22,"revolver")==HolsterSlot.BeltLeft,"pistol first belt place, revolver the second");
  Check(a.Assign(21,"pistol")==HolsterSlot.BeltRight,"a known weapon changed place");
  Check(a.Assign(26,"ak47")==HolsterSlot.Belly&&a.Assign(24,"shotgun")==HolsterSlot.RightBack&&a.Assign(29,"m60")==HolsterSlot.LeftShoulder&&a.Assign(27,"sniper")==HolsterSlot.None,"belly taken: shotgun behind the right shoulder, M60 over the left, the fourth long gun out of sight");
  Check(a.Place(22,"revolver",HolsterSlot.ArmpitLeft)&&a.SlotOf(22)==HolsterSlot.ArmpitLeft&&!a.Occupied(HolsterSlot.BeltLeft,-1),"revolver hung by hand under the arm");
  Check(!a.Place(22,"revolver",HolsterSlot.BeltRight)&&!a.Place(22,"revolver",HolsterSlot.Belly)&&!a.Place(26,"ak47",HolsterSlot.None),"occupied/wrong place accepted");
  a.Release(26);Check(a.SlotOf(26)==HolsterSlot.None&&a.Assign(27,"sniper")==HolsterSlot.Belly,"dropped AK did not free the belly");
  Check(a.Assign(26,"ak47")==HolsterSlot.None,"AK back: its old place is taken now");
  float D(HolsterSlot s)=>s==HolsterSlot.BeltLeft?.05f:s==HolsterSlot.BeltRight?.02f:1;
  Check(a.Nearest(22,"revolver",D,.16f)==HolsterSlot.BeltLeft,"nearest free place (own/occupied skipped)");
  Check(a.Nearest(22,"revolver",s=>1,.16f)==HolsterSlot.None,"a place out of reach offered");
  // 0.1.134: another weapon of an owned kind comes into play: the two trade places.
  var t=new HolsterAssignment();t.Assign(26,"m16");t.Swap(1000,26);
  Check(t.SlotOf(1000)==HolsterSlot.Belly&&t.SlotOf(26)==HolsterSlot.None&&!t.Known(26)&&t.Occupied(HolsterSlot.Belly,26),"the other weapon did not take the owned one's place");
  t.Swap(26,1000);Check(t.SlotOf(26)==HolsterSlot.Belly&&!t.Known(1000),"trading places back changed them");
  t.Release(26);t.Assign(1001,"m16");t.Swap(1001,26);Check(t.SlotOf(26)==HolsterSlot.Belly&&t.SlotOf(1001)==HolsterSlot.None&&t.Known(1001),"a released place not traded");
  // 0.1.142: who is on a place; the places over the shoulders reach further.
  var h=new HolsterAssignment();h.Assign(26,"m16");h.Assign(24,"shotgun");
  Check(h.Holder(HolsterSlot.Belly,-1)==26&&h.Holder(HolsterSlot.Belly,26)==-1&&h.Holder(HolsterSlot.RightBack,-1)==24&&h.Holder(HolsterSlot.LeftShoulder,-1)==-1&&h.Holder(HolsterSlot.None,-1)==-1,"holder of a place");
  foreach(var sl in new[]{HolsterSlot.LeftShoulder,HolsterSlot.RightBack})
   Check(HolsterLayout.GrabRadiusOf(sl)>=.2f&&HolsterLayout.SnapRadiusOf(sl)>HolsterLayout.GrabRadiusOf(sl)&&HolsterLayout.HintRadiusOf(sl)>HolsterLayout.SnapRadiusOf(sl),"shoulder reach "+sl);
  Check(HolsterLayout.GrabRadiusOf(HolsterSlot.Belly)==HolsterLayout.GrabRadius&&HolsterLayout.SnapRadiusOf(HolsterSlot.BeltRight)==HolsterLayout.SnapRadius,"other places keep their reach");
  // 0.1.169: the left belt place (the pouch side) takes from further away, and hangs further off the pouch.
  Check(HolsterLayout.GrabRadiusOf(HolsterSlot.BeltLeft)>HolsterLayout.GrabRadius&&HolsterLayout.SnapRadiusOf(HolsterSlot.BeltLeft)>HolsterLayout.GrabRadiusOf(HolsterSlot.BeltLeft)&&HolsterLayout.HintRadiusOf(HolsterSlot.BeltLeft)>HolsterLayout.SnapRadiusOf(HolsterSlot.BeltLeft),"left belt reach");
  // 0.1.214: the knife and grenade places on the chest take from further away; the nearer of the two is taken.
  foreach(var sl in new[]{HolsterSlot.ChestLeft,HolsterSlot.ChestRight})
   Check(HolsterLayout.GrabRadiusOf(sl)>HolsterLayout.GrabRadius&&HolsterLayout.SnapRadiusOf(sl)>HolsterLayout.GrabRadiusOf(sl)&&HolsterLayout.HintRadiusOf(sl)>HolsterLayout.SnapRadiusOf(sl),"chest reach "+sl);
  Check(System.Numerics.Vector3.Distance(HolsterLayout.Pose(HolsterSlot.ChestLeft,false).Reach,HolsterLayout.Pose(HolsterSlot.ChestRight,false).Reach)>HolsterLayout.ChestGrabRadius,"one chest place's reach swallows the other's place");
  {var pouch=new System.Numerics.Vector3(-.19f,-.57f,.15f);Check(System.Numerics.Vector3.Distance(HolsterLayout.Pose(HolsterSlot.BeltLeft,false).Reach,pouch)>.21f,"left belt pistol still on the pouch");
   Check(System.Numerics.Vector3.Distance(HolsterLayout.Pose(HolsterSlot.BeltLeft,false).Reach,HolsterLayout.Pose(HolsterSlot.ArmpitLeft,false).Reach)>.15f,"left belt reach swallows the armpit place");
   Check(HolsterLayout.Pose(HolsterSlot.BeltRight,true).Reach==new System.Numerics.Vector3(-.20f,-.45f,0)&&HolsterLayout.Pose(HolsterSlot.BeltLeft,true).Reach==new System.Numerics.Vector3(.23f,-.45f,-.03f),"left-hander's belt places not mirrored");}
  // The wider shoulder reach does not swallow the chest/armpit places next to it.
  foreach(var near in new[]{HolsterSlot.ArmpitLeft,HolsterSlot.ChestLeft})
   Check(System.Numerics.Vector3.Distance(HolsterLayout.Pose(near,false).Reach,HolsterLayout.Pose(HolsterSlot.LeftShoulder,false).Reach)>HolsterLayout.ShoulderGrabRadius,"shoulder reach covers "+near);
  Console.WriteLine("PASS: body places given in the order weapons were owned; out of sight when full; hung by hand on a free place; dropped weapons give theirs up; another one of a kind trades places with the owned one; the holder of a place; wider reach over the shoulders.");
  // Grip modes.
  var g=new WeaponGripState();
  g.Took(false);Check(!g.Step(WeaponGripMode.Hold,false,false),"wheel weapon without grip dropped at once");
  Check(!g.Step(WeaponGripMode.Hold,true,true)&&g.Step(WeaponGripMode.Hold,false,false),"hold: pressed then let go does not put away");
  g.Took(true);Check(!g.Step(WeaponGripMode.Hold,true,false)&&!g.Step(WeaponGripMode.Hold,true,false)&&g.Step(WeaponGripMode.Hold,false,false)&&!g.Step(WeaponGripMode.Hold,false,false),"hold: taken with grip, kept while held, put away once");
  g.Took(true);Check(!g.Step(WeaponGripMode.Toggle,true,false)&&!g.Step(WeaponGripMode.Toggle,false,false)&&g.Step(WeaponGripMode.Toggle,true,true)&&!g.Step(WeaponGripMode.Toggle,true,false),"toggle: taking press ignored, next press puts away once");
  g.Took(false);Check(g.Step(WeaponGripMode.Toggle,true,true),"toggle: wheel weapon put away by a press");
  g.Took(true);Check(!g.Step(WeaponGripMode.Always,false,false)&&!g.Step(WeaponGripMode.Always,true,true),"always: never put away");
  Console.WriteLine("PASS: grip modes: hold (in hand while held), toggle (press takes, next press puts away), always (old).");
  // 0.1.125: pointing a hand at a weapon lying in the world.
  var o=Vector3.Zero;var fwd=Vector3.UnitZ;
  Check(float.IsFinite(PointingMath.Score(o,fwd,new Vector3(0,0,2)))&&float.IsFinite(PointingMath.Score(o,fwd,new Vector3(.2f,0,2))),"a weapon 2 m ahead (or 20 cm off the line) not pointed at");
  Check(float.IsPositiveInfinity(PointingMath.Score(o,fwd,new Vector3(.5f,0,1)))&&float.IsPositiveInfinity(PointingMath.Score(o,fwd,new Vector3(0,0,-1)))&&float.IsPositiveInfinity(PointingMath.Score(o,fwd,new Vector3(0,0,5))),"off to the side, behind or too far pointed at");
  Check(float.IsPositiveInfinity(PointingMath.Score(o,fwd,new Vector3(0,0,.1f)))&&float.IsFinite(PointingMath.Score(o,fwd,new Vector3(0,0,.15f),.10f)),"minimum distance");
  Check(PointingMath.Score(o,fwd,new Vector3(0,0,1.5f))<PointingMath.Score(o,fwd,new Vector3(.1f,0,1.5f))&&PointingMath.Score(o,fwd,new Vector3(0,0,1))<PointingMath.Score(o,fwd,new Vector3(0,0,3)),"on the line / nearer not preferred");
  Check(float.IsPositiveInfinity(PointingMath.Score(o,Vector3.Zero,new Vector3(0,0,1)))&&float.IsPositiveInfinity(PointingMath.Score(o,fwd,new Vector3(float.NaN,0,1))),"degenerate pointing");
  // 0.1.171: a press with nothing pointed looks in a wider cone.
  Check(float.IsPositiveInfinity(PointingMath.Score(o,fwd,new Vector3(.5f,0,1)))&&float.IsFinite(PointingMath.Score(o,fwd,new Vector3(.5f,0,1),PointingMath.MinDistance,PointingMath.MaxDistance,1.8f))&&float.IsPositiveInfinity(PointingMath.Score(o,fwd,new Vector3(1.5f,0,1),PointingMath.MinDistance,PointingMath.MaxDistance,1.8f)),"the wider look on a press");
  // 0.1.179: not through walls and doors.
  Check(PointingMath.Seen(false,true,false,false)&&!PointingMath.Seen(false,false,true,true),"a pointed weapon: the head must see it (the hand alone does not count)");
  Check(PointingMath.Seen(true,false,true,true)&&PointingMath.Seen(true,true,false,true),"a weapon the hand is at, seen by the hand from the player's side, refused");
  Check(!PointingMath.Seen(true,false,false,true)&&!PointingMath.Seen(true,true,true,false),"a weapon reached through a wall or door taken");
  Check(PointingMath.Blocks(.5f,2,false,false,false,false),"a wall between not blocking");
  Check(!PointingMath.Blocks(1.97f,2,false,false,false,false)&&PointingMath.Blocks(1.9f,2,false,false,false,false),"the surface the weapon lies on blocks, or a wall 10 cm before it does not");
  Check(!PointingMath.Blocks(.5f,2,true,false,false,false)&&!PointingMath.Blocks(.5f,2,false,true,false,false)&&!PointingMath.Blocks(.5f,2,false,false,true,false)&&!PointingMath.Blocks(.5f,2,false,false,false,true),"the weapon itself, the player, a loose thing or a person blocks");
  Console.WriteLine("PASS: pointing grab: a cone widening with the distance (0.25-3.5 m), the weapon nearest the line and nearer ones preferred.");
  // 0.1.128: either hand holds the game's weapon; the left hold is the right one mirrored; fore-end hold.
  var grip=new Vector3(.03f,-.06f,-.12f);var m=HandMirror.Point(grip);Check(m.X==-.03f&&m.Y==grip.Y&&m.Z==grip.Z&&HandMirror.Point(m)==grip,"grip point not mirrored across the weapon's middle");
  // 0.1.133: across the weapon's own middle (its left/right bone pairs).
  var across=HandMirror.Point(grip,-.008f);Check(MathF.Abs(across.X-(-.046f))<1e-6f&&across.Y==grip.Y&&across.Z==grip.Z&&Vector3.Distance(HandMirror.Point(across,-.008f),grip)<1e-6f,"grip point not mirrored across the given middle");
  Check(HandMirror.Center(Array.Empty<float>())==0&&HandMirror.Center(new[]{-.008f,-.0078f,-.0081f})==-.008f&&HandMirror.Center(new[]{.2f})==HandMirror.MaxCenter&&HandMirror.Center(new[]{float.NaN,-.01f})==-.01f&&MathF.Abs(HandMirror.Center(new[]{-.01f,-.006f})-(-.008f))<1e-6f,"weapon middle from bone pairs");
  Check(HandMirror.PairName("wpn_crossbow_left_cam_BND_JNT")=="wpn_crossbow_right_cam_BND_JNT"&&HandMirror.PairName("safetyPinLeftLeg_a")=="safetyPinRightLeg_a"&&HandMirror.PairName("wpn_pistol_slide_BND_JNT")==null,"left/right bone pair names");
  // 0.1.135: the handle's middle from the points around the palm (stray points trimmed).
  var pts=new System.Collections.Generic.List<float>();for(int i=0;i<200;i++)pts.Add(.012f+.015f*MathF.Sin(i*.7f));pts.Add(.5f);pts.Add(-.4f);
  Check(MathF.Abs(HandMirror.HandleMiddle(pts)-.012f)<.002f&&float.IsNaN(HandMirror.HandleMiddle(new System.Collections.Generic.List<float>{.01f,.02f}))&&HandMirror.HandleMiddle(Enumerable.Repeat(.2f,50).ToList())==HandMirror.MaxCenter,"handle middle from the points around the palm");
  // 0.1.135: a weapon let go keeps the hand's turn, bounces, then settles.
  {
   float dt=1f/90;var f0=Vector3.UnitZ;var u0=Vector3.UnitY;var yaw=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.2f);
   var w=ThrowMath.Spin(Vector3.Transform(f0,yaw),Vector3.Transform(u0,yaw),f0,u0,dt);
   Check(Vector3.Distance(w,new Vector3(0,.2f/dt,0))<.05f,"hand turn about up not measured: "+w);
   var roll=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-.1f);var wr=ThrowMath.Spin(Vector3.Transform(f0,roll),Vector3.Transform(u0,roll),f0,u0,dt);
   Check(Vector3.Distance(wr,new Vector3(0,0,-.1f/dt))<.05f&&ThrowMath.Spin(f0,u0,f0,u0,dt)==Vector3.Zero&&ThrowMath.Spin(f0,u0,f0,u0,0)==Vector3.Zero,"hand roll not measured / still hand spins");
   var b=ThrowMath.Bounce(new Vector3(2,-5,0),new Vector3(0,0,10),Vector3.UnitY,0);
   Check(b.bounced&&b.velocity.Y>0&&b.velocity.Y<=5*ThrowMath.Restitution+1e-4f&&b.velocity.X>0&&b.velocity.X<2&&b.spin.Length()<=ThrowMath.MaxSpin,"a fast landing does not bounce up, slower");
   Check(!ThrowMath.Bounce(new Vector3(.3f,-.8f,0),Vector3.Zero,Vector3.UnitY,0).bounced&&!ThrowMath.Bounce(new Vector3(2,-5,0),Vector3.Zero,Vector3.UnitY,ThrowMath.MaxBounces).bounced,"a slow landing (or after enough bounces) keeps bouncing");
   var v=new Vector3(0,-6,0);int n=0;while(n<10){var r=ThrowMath.Bounce(v,Vector3.Zero,Vector3.UnitY,n);if(!r.bounced)break;v=new Vector3(r.velocity.X,-r.velocity.Y,r.velocity.Z);n++;}
   Check(n>=1&&n<=ThrowMath.MaxBounces,"bounces never end: "+n);
  }
  // 0.1.137: the landing sound by surface and place.
  Check(WeaponImpactMath.Pick(2,false,false)==DropSound.Room&&WeaponImpactMath.Pick(1,true,false)==DropSound.Room&&WeaponImpactMath.Pick(1,false,false)==DropSound.Ground&&WeaponImpactMath.Pick(37,true,false)==DropSound.Room,"hard floors: the recording indoors and on wood only");
  Check(WeaponImpactMath.Pick(10,true,false)==DropSound.Soft&&WeaponImpactMath.Pick(9,false,true)==DropSound.Soft&&WeaponImpactMath.Pick(-1,false,true)==DropSound.Soft&&WeaponImpactMath.Pick(41,false,false)==DropSound.Soft,"snow/dirt/terrain/leaves not a soft thud");
  Check(WeaponImpactMath.Pick(4,false,false)==DropSound.Metal&&WeaponImpactMath.Pick(27,true,false)==DropSound.Metal&&WeaponImpactMath.Pick(7,true,false)==DropSound.Glass&&WeaponImpactMath.Pick(16,true,false)==DropSound.Carpet&&WeaponImpactMath.Pick(8,false,false)==DropSound.Water,"metal/glass/carpet/water sounds");
  Check(WeaponImpactMath.Pick(-1,true,false)==DropSound.Room&&WeaponImpactMath.Pick(-1,false,false)==DropSound.Ground&&WeaponImpactMath.Pick(13,true,true)==DropSound.Ground,"unknown surface / terrain rock guess");
  Check(WeaponImpactMath.Volume(.3f)==0&&WeaponImpactMath.Volume(float.NaN)==0&&WeaponImpactMath.Volume(WeaponImpactMath.MinSpeed)==.25f&&WeaponImpactMath.Volume(20)==1&&WeaponImpactMath.Volume(2)<WeaponImpactMath.Volume(3),"landing volume by speed");
  Check(WeaponImpactMath.Weight("pistol").pitch==1&&WeaponImpactMath.Weight("m16").pitch<1&&WeaponImpactMath.Weight("magazine").pitch>1&&WeaponImpactMath.Weight("magazine").gain<1,"weapon weight pitch");
  var turn=Quaternion.CreateFromYawPitchRoll(.3f,-.2f,.5f);var mt=HandMirror.Rotation(turn);
  foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})
  {
   var a0=Vector3.Transform(new Vector3(-axis.X,axis.Y,axis.Z),mt);var a1=Vector3.Transform(axis,turn);
   Check(Vector3.Distance(a0,new Vector3(-a1.X,a1.Y,a1.Z))<1e-5f,"hand rotation not mirrored ("+axis+")");
  }
  Check(HandMirror.Trim(new Vector3(10,20,30))==new Vector3(10,-20,-30),"weapon trim: yaw/roll not mirrored for the left hand");
  var hand=new Vector3(.3f,1.2f,.4f);var handTurn=Quaternion.CreateFromYawPitchRoll(1.1f,.2f,-.3f);var gun=new Vector3(.25f,1.15f,.7f);var gunTurn=Quaternion.CreateFromYawPitchRoll(.9f,.1f,0);
  var rel=HandMirror.Relative(hand,handTurn,gun,gunTurn);var back=HandMirror.Apply(hand,handTurn,rel.offset,rel.rotation);
  Check(Vector3.Distance(back.position,gun)<1e-5f&&MathF.Abs(Quaternion.Dot(back.rotation,gunTurn))>.99999f,"fore-end hold moves the weapon at the moment of letting go");
  var moved=HandMirror.Apply(hand+Vector3.UnitX,Quaternion.CreateFromAxisAngle(Vector3.UnitY,.5f)*handTurn,rel.offset,rel.rotation);
  Check(MathF.Abs(Vector3.Distance(moved.position,hand+Vector3.UnitX)-Vector3.Distance(gun,hand))<1e-4f,"fore-end hold does not follow the hand rigidly");
  Check(HandRoles.ForeEnd("ak47")&&HandRoles.ForeEnd("shotgun")&&HandRoles.ForeEnd("uzi")&&!HandRoles.ForeEnd("pistol")&&!HandRoles.ForeEnd("revolver")&&!HandRoles.ForeEnd("grenade"),"which weapons hang by the fore-end");
  Check(HandRoles.Take("ak47",false,"")==HandTake.Game&&HandRoles.Take("ak47",true,"")==HandTake.Game,"an empty hand without a game weapon elsewhere takes it as the game's weapon");
  Check(HandRoles.Take("pistol",false,"m16")==HandTake.Copy&&HandRoles.Take("shotgun",true,"ak47")==HandTake.Copy&&HandRoles.Take("uzi",false,"grenade")==HandTake.Copy,"the other hand's game weapon: this hand holds a copy");
  Check(HandRoles.Take("grenade",true,"ak47")==HandTake.Game&&HandRoles.Take("knife",true,"")==HandTake.Game&&HandRoles.Take("grenade",false,"")==HandTake.Copy&&HandRoles.Take("knife",false,"m16")==HandTake.Copy,"grenade/knife: the right hand throws them as the game's, the left holds a copy");
  Check(HandRoles.Take("knife",false,"")==HandTake.Game&&HandRoles.Take("knife",false,"pistol")==HandTake.Copy,"0.1.156: a knife in the left hand with the right hand empty is the game's (thrown at once)");
  Check(HandRoles.Take("prop",true,"")==HandTake.Refuse&&HandRoles.Take("",false,"")==HandTake.Refuse,"props are not held from the body");
  Check(HandRoles.Switch("ak47","m16")&&HandRoles.Switch("pistol","")&&!HandRoles.Switch("ak47","grenade")&&!HandRoles.Switch("grenade","")&&!HandRoles.Switch("knife","ak47"),"a copy's trigger brings it into play (not over a grenade/knife)");
  // 0.1.200: which other weapon of a kind gives way.
  {
   var none=new (int key,bool floor,bool hand)[]{(1001,false,false)};
   Check(HolsterLayout.LooseToFree(none,2)==-1,"room taken with one other of a kind");
   var body=new (int key,bool floor,bool hand)[]{(1003,false,false),(1001,false,false)};
   Check(HolsterLayout.LooseToFree(body,2)==1001,"the longest kept one on the body does not give way");
   var mixed=new (int key,bool floor,bool hand)[]{(1001,false,false),(1004,true,false),(1002,true,false)};
   Check(HolsterLayout.LooseToFree(mixed,2)==1002,"one on the body gives way before one lying on the floor");
   var held=new (int key,bool floor,bool hand)[]{(1001,false,true),(1002,true,true),(1003,false,false)};
   Check(HolsterLayout.LooseToFree(held,2)==1003,"one in a hand gives way");
   var all=new (int key,bool floor,bool hand)[]{(1001,false,true),(1002,false,true)};
   Check(HolsterLayout.LooseToFree(all,2)==-2,"none in hands to give way, yet one does");
  }
  Console.WriteLine("PASS: 0.1.200 one more weapon of a kind from the ground: one lying on the floor gives way, else the longest kept one on the body (its rounds back), never one in a hand.");
  Console.WriteLine("PASS: either hand: mirrored left hold (point, rotation, trim), fore-end hold follows that hand rigidly, game weapon vs copy, switching by the copy's trigger.");
 }
}
