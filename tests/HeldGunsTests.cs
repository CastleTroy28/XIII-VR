using System;using System.Linq;using System.Numerics;using XiiiXR;
class HeldGunsTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // 0.1.146: a gun's cells hug its model: a thin barrel (1 cm radius) gets spheres around its own points.
  var pts=new System.Collections.Generic.List<Vector3>();
  for(float z=0;z<=.5f;z+=.004f)for(int n=0;n<12;n++){float ang=n*MathF.PI/6;pts.Add(new Vector3(.01f*MathF.Cos(ang),.01f*MathF.Sin(ang),z));}
  var cells=ContactSolver.Cells(pts,.025f,80,out float cell)!;
  Check(cells.Length<=80,"cell count "+cells.Length);
  Check(pts.All(p=>cells.Any(c=>Vector3.Distance(c.Offset,p)<=c.Radius+1e-5f)),"barrel point outside its cells");
  Check(!cells.Any(c=>Vector3.Distance(c.Offset,new Vector3(0,.035f,.25f))<c.Radius),"3.5 cm above a 1 cm barrel still collides (not tight)");
  Check(cells.Max(c=>c.Radius)<cell*.72f,"partly filled cells kept the whole cell's sphere");
  // Touching: two guns' spheres within the margin.
  var a=new[]{new ContactSphere(Vector3.Zero,.02f)};var b=new[]{new ContactSphere(Vector3.Zero,.02f)};
  var pa=new ContactPose(Vector3.Zero,Quaternion.Identity);
  Check(ContactSolver.Touching(a,pa,b,new ContactPose(new Vector3(.042f,0,0),Quaternion.Identity),.004f,out var point)&&Math.Abs(point.X-.02f)<.002f,"touching guns not found / point "+point);
  Check(!ContactSolver.Touching(a,pa,b,new ContactPose(new Vector3(.06f,0,0),Quaternion.Identity),.004f,out _),"guns 2 cm apart touching");
  // The knock: the speed the touching points met at, once per touch, not too often, silent when slow.
  var k=new GunKnock();
  k.Moved(0,new ContactPose(new Vector3(-.10f,0,0),Quaternion.Identity),0f);k.Moved(2,new ContactPose(new Vector3(.2f,0,0),Quaternion.Identity),0f);
  k.Moved(0,new ContactPose(new Vector3(-.08f,0,0),Quaternion.Identity),.01f);k.Moved(2,new ContactPose(new Vector3(.2f,0,0),Quaternion.Identity),.01f);
  float speed=k.Step(true,0,2,new Vector3(-.06f,0,0),.01f);
  Check(Math.Abs(speed-2f)<.01f,"knock speed "+speed);
  Check(k.Step(true,0,2,new Vector3(-.06f,0,0),.02f)==0,"a second knock while still touching");
  k.Step(false,0,2,Vector3.Zero,.03f);
  Check(k.Step(true,0,2,new Vector3(-.06f,0,0),.05f)==0,"two knocks within 0.25 s");
  // A turning gun: the muzzle end meets faster than its handle.
  var t=new GunKnock();var turn=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.1f);
  t.Moved(1,new ContactPose(Vector3.Zero,Quaternion.Identity),0);t.Moved(1,new ContactPose(Vector3.Zero,turn),.01f);
  Check(t.PointVelocity(1,Vector3.Transform(new Vector3(0,0,.8f),turn)).Length()>t.PointVelocity(1,Vector3.Transform(new Vector3(0,0,.1f),turn)).Length()*5,"the muzzle end of a turning gun not faster");
  Check(GunKnock.Volume(.1f)==0&&GunKnock.Volume(.35f)>=.2f&&GunKnock.Volume(10)<=.8f,"knock volume");
  // Pistol places switched off in VR SETTINGS.
  Check(HolsterLayout.Candidates("pistol").Count==4,"all four pistol places by default");
  HolsterLayout.BeltPistols=()=>false;
  Check(HolsterLayout.Candidates("pistol").SequenceEqual(new[]{HolsterSlot.ArmpitLeft,HolsterSlot.ArmpitRight})&&HolsterLayout.Kind("pistol"),"belt places off: under the arms only");
  HolsterLayout.ArmpitPistols=()=>false;
  Check(HolsterLayout.Candidates("uzi").Count==0&&HolsterLayout.Kind("uzi")&&HandRoles.Take("pistol",true,"")==HandTake.Game,"all pistol places off: still a pistol taken into the hand");
  HolsterLayout.BeltPistols=()=>true;
  Check(HolsterLayout.Candidates("revolver").SequenceEqual(new[]{HolsterSlot.BeltRight,HolsterSlot.BeltLeft}),"under the arms off: the belt only");
  Check(HolsterLayout.Candidates("ak47").Count==3,"long guns not touched by the pistol switches");
  HolsterLayout.ArmpitPistols=()=>true;
  Console.WriteLine("PASS: held guns: cells hug the model (a 1 cm barrel collides 1 cm, not 3.5); two guns touching; a knock at the speed their points met (a turning gun's muzzle faster), once per touch, at most every 0.25 s, silent under 0.3 m/s; pistol places on the belt / under the arms switched off each.");
 }
}
