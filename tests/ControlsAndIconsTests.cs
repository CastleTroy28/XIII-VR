using System;using System.Linq;using System.Numerics;using XiiiXR;
class ControlsAndIconsTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static bool Near((byte r,byte g,byte b,byte a) p,(byte r,byte g,byte b) c,int tol=40)=>p.a>200&&Math.Abs(p.r-c.r)<=tol&&Math.Abs(p.g-c.g)<=tol&&Math.Abs(p.b-c.b)<=tol;
 static void Main()
 {
  // The labels -> the controller and the button drawn.
  Check(ControllerIconMath.For("Left/Right Grip")==new IconSpec(IconSide.Either,IconParts.Grip),"either grip");
  Check(ControllerIconMath.For("Right stick click")==new IconSpec(IconSide.Right,IconParts.Stick)&&ControllerIconMath.For("Left stick click")==new IconSpec(IconSide.Left,IconParts.Stick),"stick clicks");
  Check(ControllerIconMath.For("Right Grip + A")==new IconSpec(IconSide.Right,IconParts.Grip|IconParts.Lower)&&ControllerIconMath.For("Left Grip + X")==new IconSpec(IconSide.Left,IconParts.Grip|IconParts.Lower),"chords light both buttons");
  Check(ControllerIconMath.For("Right B")?.Parts==IconParts.Upper&&ControllerIconMath.For("Left Y")?.Side==IconSide.Left&&ControllerIconMath.For("Left trigger")?.Parts==IconParts.Trigger,"single buttons");
  Check(ControllerIconMath.For("Turn key")==null&&ControllerIconMath.For("Hold card to reader")==null&&ControllerIconMath.For("A: wheel, left stick")==null&&ControllerIconMath.For("")==null,"a gesture or a several-step hint gets a button");
  // The drawing: the lit part yellow, the others white, transparent around, the left one mirrored.
  foreach(var part in new[]{IconParts.Grip,IconParts.Trigger,IconParts.Lower,IconParts.Upper,IconParts.Stick})
   foreach(var side in new[]{IconSide.Right,IconSide.Left})
   {
    var px=ControllerIconMath.Paint(new IconSpec(side,part));var (u,v)=ControllerIconMath.Center(part,side);
    Check(Near(ControllerIconMath.At(px,u,v),ControllerIconMath.Lit),part+" not lit on the "+side+" controller: "+ControllerIconMath.At(px,u,v));
    foreach(var other in new[]{IconParts.Grip,IconParts.Lower,IconParts.Upper}.Where(o=>o!=part))
    {var (ou,ov)=ControllerIconMath.Center(other,side);Check(Near(ControllerIconMath.At(px,ou,ov),ControllerIconMath.Body),other+" lit with "+part+" on the "+side+": "+ControllerIconMath.At(px,ou,ov));}
    Check(ControllerIconMath.At(px,.02f,.98f).a==0&&ControllerIconMath.At(px,.98f,.98f).a==0,"the corners are not transparent");
   }
  {var r=ControllerIconMath.Center(IconParts.Grip,IconSide.Right);var l=ControllerIconMath.Center(IconParts.Grip,IconSide.Left);Check(MathF.Abs(r.u+l.u-1)<1e-5f&&r.v==l.v&&r.u<.5f,"the left controller is not the mirror image (grip inward)");}
  {var a=ControllerIconMath.Paint(new IconSpec(IconSide.Either,IconParts.Grip));var b=ControllerIconMath.Paint(new IconSpec(IconSide.Right,IconParts.Grip));Check(a.SequenceEqual(b)&&a.Length==ControllerIconMath.Size*ControllerIconMath.Size*4,"either hand is not drawn as the right controller");}
  // The controls page: every text in every language, mirrored for a left-hander.
  foreach(var lang in new[]{"ru","de","fr","es","it","pl","pt"})foreach(var t in ControlsSheet.Texts())Check(ControlsSheet.Translated(t,lang),"untranslated \""+t+"\" in "+lang);
  Check(ControlsSheet.Rows.Length>=20&&ControlsSheet.Rows.All(r=>r.Action.Length<=40&&r.Right.Length<=60&&r.Left.Length<=60),"the page has too few rows or too long ones");
  string right=ControlsSheet.Body(false,"en"),left=ControlsSheet.Body(true,"en");
  Check(right.Contains("(R3)")&&right.Contains("Left Grip + X")&&!right.Contains("(L3)"),"right-handed page: key or pause button wrong");
  Check(left.Contains("(L3)")&&left.Contains("Right Grip + A")&&left.Contains("(or Left Grip + X)"),"left-handed page: key, pause or door button wrong");
  Check(right.Split('\n').Length==ControlsSheet.Rows.Length&&right.Contains("<pos=40%>"),"one line per action, two columns");
  Check(ControlsSheet.Body(false,"ru").Contains("Правый стик")&&ControlsSheet.Title(true,"ru").Contains("УПРАВЛЕНИЕ VR")&&ControlsSheet.Title(true,"ru").Contains("левша"),"Russian page");
  ControlsSheet.Show();Check(ControlsSheet.Open,"open");ControlsSheet.Close();Check(!ControlsSheet.Open,"close");
  // The bazooka by hand: a rocket from the pouch, its tail into the front of the tube.
  Check(BazookaReloadMath.Take(true,3,true,true,true)&&BazookaReloadMath.Take(true,-1,true,true,true),"a rocket not taken");
  Check(!BazookaReloadMath.Take(false,3,true,true,true)&&!BazookaReloadMath.Take(true,0,true,true,true)&&!BazookaReloadMath.Take(true,3,false,true,true)&&!BazookaReloadMath.Take(true,3,true,false,true)&&!BazookaReloadMath.Take(true,3,true,true,false),"a rocket taken loaded, with none left, a busy hand, away from the pouch or without a press");
  var mouth=new Vector3(0,1.5f,1);var tube=Vector3.UnitZ;
  Check(BazookaReloadMath.Insert(mouth+new Vector3(0,.05f,.05f),tube,mouth,tube),"the rocket's tail at the mouth does not go in");
  Check(!BazookaReloadMath.Insert(mouth+new Vector3(0,0,.3f),tube,mouth,tube),"a rocket goes in from 30 cm");
  Check(!BazookaReloadMath.Insert(mouth,-tube,mouth,tube)&&!BazookaReloadMath.Insert(mouth,Vector3.UnitX,mouth,tube),"a rocket goes in backwards or across");
  Check(!BazookaReloadMath.Insert(new Vector3(float.NaN,0,0),tube,mouth,tube)&&!BazookaReloadMath.Insert(mouth,Vector3.Zero,mouth,tube),"broken poses go in");
  Check(BazookaReloadMath.DotRadius(2)<BazookaReloadMath.DotRadius(40)&&BazookaReloadMath.DotRadius(0)>=.025f&&BazookaReloadMath.DotRadius(1e6f)<=.4f&&BazookaReloadMath.DotRadius(float.NaN)==.025f,"aim dot size");
  // 0.1.217: the bazooka's rocket bones, its tube bone and the tube's real mouth.
  Check(BazookaTubeMath.RocketBone("wpn_bazooka_rocket_SH_BND_JNT")&&!BazookaTubeMath.RocketBone("wpn_bazooka_rocket_zero_SH_JNT")&&!BazookaTubeMath.RocketBone("wpn_bazooka_SH_BND_JNT")&&!BazookaTubeMath.RocketBone(null),"rocket bones");
  var rig=new string?[]{"wpn_bazooka_SH_BND_JNT","wpn_bazooka_rocket_zero_SH_JNT","wpn_bazooka_rocket_SH_BND_JNT","wpn_bazooka_front_grip_SH_BND_JNT"};
  Check(BazookaTubeMath.Root(rig)==0,"the tube's bone");
  var tubePoints=new System.Collections.Generic.List<Vector3>();var rocketPoints=new System.Collections.Generic.List<Vector3>();
  for(int k=0;k<64;k++){float a=k*MathF.PI/32;tubePoints.Add(new Vector3(.045f*MathF.Cos(a),.02f+.045f*MathF.Sin(a),-.5f+.8f*(k%16)/15f));}
  tubePoints.Add(new Vector3(0,.12f,.36f)); // the front sight, above the tube: not its mouth
  for(int k=0;k<40;k++){float a=k*MathF.PI/20;rocketPoints.Add(new Vector3(.03f*MathF.Cos(a),.02f+.03f*MathF.Sin(a),-.1f+.7f*(k%10)/9f));}
  var mouthAt=BazookaTubeMath.Mouth(tubePoints,rocketPoints);
  Check(MathF.Abs(mouthAt.Z-.3f)<.001f&&MathF.Abs(mouthAt.Y-.02f)<.002f&&MathF.Abs(mouthAt.X)<.002f,"the tube's mouth "+mouthAt+" (the front of the tube on the rocket's line, not the warhead or the sight)");
  Check(float.IsNaN(BazookaTubeMath.Mouth(new[]{new Vector3(1,1,0)},rocketPoints).Z)&&float.IsNaN(BazookaTubeMath.Mouth(tubePoints,System.Array.Empty<Vector3>()).Z),"a mouth found from nothing");
  Check(BazookaTubeMath.InTube(rocketPoints,tubePoints),"the loaded rocket is not in the tube");
  var sideways=rocketPoints.Select(v=>new Vector3(v.Z,v.Y,.1f+v.X)).ToList();var beside=rocketPoints.Select(v=>v+new Vector3(.5f,0,0)).ToList();
  Check(!BazookaTubeMath.InTube(sideways,tubePoints)&&!BazookaTubeMath.InTube(beside,tubePoints)&&!BazookaTubeMath.InTube(rocketPoints.Take(3).ToList(),tubePoints),"a rocket across the tube, beside it or of three points counts as in the tube");
  var bar=BazookaTubeMath.HoldBar(new Vector3(0,0,.1f),.74f);
  Check(MathF.Abs(bar.Center.Z-(.1f+.74f*(BazookaTubeMath.GripFromTail-.5f)))<1e-4f&&bar.Axis==Vector3.UnitZ&&BazookaTubeMath.GripFromTail<=.12f,"the rocket's grip point: round its motor tube near the tail, its line along the fist");
  // 0.1.219: the bazooka's own grips as bars: the handle (not its support bone) and the front grip.
  Check(BazookaTubeMath.GripBone("wpn_bazooka_rear_grip_SH_BND_JNT",false)&&!BazookaTubeMath.GripBone("wpn_bazooka_rear_grip_support_SH_BND_JNT",false)&&BazookaTubeMath.GripBone("wpn_bazooka_front_grip_SH_BND_JNT",true)&&!BazookaTubeMath.GripBone("wpn_bazooka_front_grip_SH_BND_JNT",false)&&!BazookaTubeMath.GripBone(null,true),"grip bones");
  var gripPoints=new System.Collections.Generic.List<Vector3>();var lean=Vector3.Normalize(new Vector3(0,-.98f,-.2f));var across1=Vector3.UnitX;var across2=Vector3.Normalize(Vector3.Cross(lean,across1));
  for(int k=0;k<160;k++){float a=(k%16)*MathF.PI/8,t=-.055f+.11f*(k/16)/9f;gripPoints.Add(new Vector3(.01f,-.1f,-.13f)+lean*t+across1*(.017f*MathF.Cos(a))+across2*(.017f*MathF.Sin(a)));}
  var gripBar=BazookaTubeMath.GripBar(gripPoints.ToArray());
  Check(gripBar is GripBarMath.Bar gb&&gb.Axis.Y>.9f&&MathF.Abs(gb.Thickness-.034f)<.008f&&MathF.Abs(gb.Length-.11f)<.01f&&gb.Toward.Z>.9f&&MathF.Abs(Vector3.Dot(gb.Toward,gb.Axis))<1e-3f&&Vector3.Distance(gb.Center,new Vector3(.01f,-.1f,-.13f))<.005f,"the handle's bar (its line up toward the tube, the way forward square to it)");
  var tubeBar=tubePoints.ToArray();Check(BazookaTubeMath.GripBar(tubeBar)==null&&BazookaTubeMath.GripBar(new Vector3[3])==null,"the tube or a few points taken for a handle");
  // 0.1.223: the handle with the trigger is the mid grip (the rear grip is the shoulder rest); the game's hold of it moved onto the front grip.
  Check(BazookaTubeMath.TriggerGripBone("wpn_bazooka_mid_grip_SH_BND_JNT")&&!BazookaTubeMath.TriggerGripBone("wpn_bazooka_rear_grip_SH_BND_JNT")&&!BazookaTubeMath.TriggerGripBone("wpn_bazooka_front_grip_SH_BND_JNT")&&!BazookaTubeMath.TriggerGripBone(null),"the handle's bone");
  {
   var handle=new GripBarMath.Bar(new Vector3(-.003f,-.12f,-.08f),Vector3.Normalize(new Vector3(0,.95f,.3f)),Vector3.UnitZ,.1f,.035f);
   var front=new GripBarMath.Bar(new Vector3(-.003f,-.15f,.066f),Vector3.UnitY,Vector3.UnitZ,.1f,.035f);
   var hand=new Vector3(.04f,-.13f,-.12f);var turn=Quaternion.CreateFromYawPitchRoll(.2f,.1f,-1.3f);
   var same=BazookaTubeMath.MoveHold(hand,turn,handle,handle,false);
   Check(Vector3.Distance(same.position,hand)<1e-5f&&MathF.Abs(Quaternion.Dot(same.rotation,turn))>.99999f,"a hold moved onto its own grip moves");
   var moved=BazookaTubeMath.MoveHold(hand,turn,handle,front,false);
   float before=Vector3.Distance(hand,handle.Center),after=Vector3.Distance(moved.position,front.Center);
   var axisInHand=Vector3.Transform(handle.Axis,Quaternion.Inverse(turn));var frontInHand=Vector3.Transform(front.Axis,Quaternion.Inverse(moved.rotation));
   Check(MathF.Abs(before-after)<1e-5f&&Vector3.Distance(axisInHand,frontInHand)<1e-4f,"the hand not as far from the front grip, or not turned with it, as on the handle");
   var mirrored=BazookaTubeMath.MoveHold(hand,turn,handle,front,true);
   Check(MathF.Abs(mirrored.position.X-(2*front.Center.X-moved.position.X))<1e-5f&&mirrored.position.Y==moved.position.Y&&mirrored.position.Z==moved.position.Z
    &&mirrored.rotation.X==moved.rotation.X&&mirrored.rotation.Y==-moved.rotation.Y&&mirrored.rotation.Z==-moved.rotation.Z,"the other hand not mirrored across the front grip's middle");
  }
  // 0.1.225: the game's left hand counts as on the front grip near it (its samples while the bazooka is drawn are far off).
  {
   var front=new GripBarMath.Bar(new Vector3(-.003f,-.151f,.066f),Vector3.UnitY,Vector3.UnitZ,.1f,.035f);
   Check(BazookaTubeMath.NearGrip(new Vector3(.0356f,-.0757f,.023f),front)&&!BazookaTubeMath.NearGrip(new Vector3(-.0445f,-.0089f,-.1174f),front)&&!BazookaTubeMath.NearGrip(new Vector3(float.NaN,0,0),front),"the game's left hand on the front grip not told from one far off");
   // Its hold moved onto the handle stays as far from the handle (the left hand's own hold, not mirrored).
   var handle=new GripBarMath.Bar(new Vector3(-.002f,-.166f,-.095f),Vector3.UnitY,Vector3.UnitZ,.06f,.036f);
   var leftAt=new Vector3(.0356f,-.0757f,.023f);var leftTurn=Quaternion.CreateFromYawPitchRoll(.9f,-.15f,1.35f);
   var onHandle=BazookaTubeMath.MoveHold(leftAt,leftTurn,front,handle,false);
   Check(Vector3.Distance(onHandle.position-handle.Center,leftAt-front.Center)<1e-5f&&MathF.Abs(Quaternion.Dot(onHandle.rotation,leftTurn))>.99999f,"the left hand's hold moved onto the handle changed");
  }
  // 0.1.226: Unity's Euler order. 0.1.227: a hand round a grip (OnGrip): its
  // fingers' line down the grip, the middle of their curl at the grip's middle;
  // a left hand the right one mirrored.
  {
   var e=BazookaTubeMath.UnityEuler(0,90,0);Check(Vector3.Distance(Vector3.Transform(Vector3.UnitZ,e),Vector3.UnitX)<1e-5f,"Euler yaw");
   var zx=BazookaTubeMath.UnityEuler(90,0,90);Check(Vector3.Distance(Vector3.Transform(Vector3.UnitX,zx),Vector3.Transform(Vector3.Transform(Vector3.UnitX,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,MathF.PI/2)),Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI/2)))<1e-5f,"Euler order (z, then x, then y)");
   var grip=new GripBarMath.Bar(new Vector3(-.002f,-.166f,-.095f),Vector3.Normalize(new Vector3(0,1,.03f)),Vector3.UnitZ,.06f,.036f);
   var channel=new Vector3(.009f,-.026f,.011f);var little=Vector3.Normalize(new Vector3(1,-.1f,-.05f));
   var r=BazookaTubeMath.OnGrip(grip,BazookaTubeMath.RightTurn,true,channel,little);
   Check(Vector3.Distance(r.position+Vector3.Transform(channel,r.rotation),grip.Center)<1e-5f,"the fingers' middle not at the grip's middle");
   Check(Vector3.Dot(Vector3.Transform(little,r.rotation),-grip.Axis)>.9999f,"the fingers' line not down the grip");
   float turned=2*MathF.Acos(Math.Clamp(MathF.Abs(Quaternion.Dot(r.rotation,BazookaTubeMath.RightTurn)),0,1))*180/MathF.PI;Check(turned<20,"the hand turned away from the game's hold: "+turned);
   var mc=new Vector3(-channel.X,channel.Y,channel.Z);var ml=new Vector3(-little.X,little.Y,little.Z);
   var l=BazookaTubeMath.OnGrip(grip,BazookaTubeMath.RightTurn,false,mc,ml);
   Check(MathF.Abs(l.position.X-(2*grip.Center.X-r.position.X))<2e-4f&&MathF.Abs(l.position.Y-r.position.Y)<2e-4f&&MathF.Abs(l.position.Z-r.position.Z)<2e-4f
    &&MathF.Abs(Quaternion.Dot(l.rotation,new Quaternion(r.rotation.X,-r.rotation.Y,-r.rotation.Z,r.rotation.W)))>.9999f,"the left hand not the right one mirrored across the grip");
   Check(Vector3.Dot(Vector3.Transform(Vector3.UnitX,BazookaTubeMath.FromTo(Vector3.UnitX,-Vector3.UnitX)),-Vector3.UnitX)>.9999f&&BazookaTubeMath.FromTo(Vector3.UnitY,Vector3.UnitY)==Quaternion.Identity,"turn between two ways");
   Check(Vector3.Distance(BazookaTubeMath.DrawnChannel(new Vector3(0,0,.1f),.5f),new Vector3(0,0,NativeHandMesh.WristZ+.05f))<1e-6f,"the channel drawn round the wrist at the hand's size");
   Check(MathF.Abs(BazookaTubeMath.GripRadius(grip)-.018f)<1e-6f&&BazookaTubeMath.GripRadius(new GripBarMath.Bar(Vector3.Zero,Vector3.UnitY,Vector3.UnitZ,.1f,float.NaN))==.018f,"grip radius");
   Check(MathF.Abs(BazookaTubeMath.HandSize-.7456f)<1e-4f,"hand size");
  }
  Console.WriteLine("PASS: 0.1.219/0.1.223 bazooka grips: the handle (its mid grip) and the front grip found as bars; the game's hold of the handle moved onto the front grip (mirrored for the other hand) (line, thickness, the way forward) for the hands to close round as round a pistol grip; the rocket held near its tail.");
  Console.WriteLine("PASS: 0.1.227 a hand round a bazooka grip: its fingers' line down the grip and the middle of their curl at the grip's middle, turned as the game holds the handle, the left hand the right one mirrored.");
  Console.WriteLine("PASS: 0.1.217 bazooka: its rocket's own bones (not its reference point), the tube's bone, the tube's mouth at the front of the tube on the rocket's line (not the warhead or a sight), the rocket held round its motor tube.");
  Console.WriteLine("PASS: 0.1.215 controller icons: labels map to the side and button (none for gestures); the lit part is yellow, the others white, transparent around, the left controller mirrored, either hand drawn as the right.");
  Console.WriteLine("PASS: 0.1.215 the VR controls page: every action and button in 8 languages, one line each in two columns, R3/left grip+X right-handed, L3/right grip+A left-handed.");
  Console.WriteLine("PASS: 0.1.215 the bazooka by hand: a rocket only when empty, with rockets left, a free hand at the pouch and a fresh grip press (0.1.221; was the trigger); it goes in tail first at the mouth, along the tube; the aim dot grows with distance.");
 }
}
