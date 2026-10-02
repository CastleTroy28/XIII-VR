using System;using System.Numerics;using System.Linq;using XiiiXR;
class ContactTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static ContactPose P(float x,float y,float z)=>new(new Vector3(x,y,z),Quaternion.Identity);
 sealed class World:IContactWorld
 {
  internal float Limit=1;internal bool Invalid;internal int Pushes;internal int Queries;internal bool Broad;internal Vector3 BroadCenter;internal float BroadRadius;
  public bool Clear(Vector3 c,float r){if(!Broad)return false;BroadCenter=c;BroadRadius=r;return !Invalid&&Pushes==0&&c.Z+r<=Limit;}
  public float Sweep(Vector3 a,Vector3 b,float radius,out Vector3 normal)
  {Queries++;normal=-Vector3.UnitZ;float end=Limit-radius;if(b.Z<=end||b.Z<=a.Z)return 1;return Math.Clamp((end-a.Z)/(b.Z-a.Z),0,1);}
  public bool Overlap(Vector3 c,float r,out Vector3 correction)
  {Queries++;correction=Vector3.Zero;if(Invalid){correction=new(float.NaN);return true;}if(Pushes>0){Pushes--;correction=-Vector3.UnitZ*.01f;return true;}if(c.Z+r>Limit+.000001f){correction=-Vector3.UnitZ*(c.Z+r-Limit+.001f);return true;}return false;}
 }
 static void Main()
 {
  {
   // 0.1.121: broad check — in free space no per-sphere query at all, the tracked pose stands;
   // near the wall the full solve runs as before. The bound holds the whole shape during the move and the turn.
   var bw=new World{Broad=true,Limit=5};var gunShape=ContactSolver.Weapon("shotgun");var s1=new ContactSolver();
   var turned=new ContactPose(new Vector3(0,0,.4f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,1.2f));
   s1.Solve(P(0,0,0),P(0,0,0),gunShape,bw);bw.Queries=0;
   var r1=s1.Solve(turned,P(0,0,0),gunShape,bw);
   Check(s1.Safe&&!s1.Blocked&&r1==turned&&bw.Queries==0,"free gun pose still queried sphere by sphere");
   Check(ContactSolver.Bound(gunShape,P(0,0,0),turned,out var bc,out float br)&&bc==bw.BroadCenter&&br==bw.BroadRadius,"bound not the one queried");
   foreach(var pose in new[]{P(0,0,0),turned,ContactPose.Blend(P(0,0,0),turned,.5f)})
    foreach(var sp in gunShape)Check(Vector3.Distance(pose.Point(sp.Offset),bc)+sp.Radius<=br+1e-4f,"bound misses a sphere on the way");
   bw.Limit=.8f;bw.Queries=0;var r2=s1.Solve(P(0,0,.6f),P(0,0,0),gunShape,bw);
   Check(bw.Queries>0&&gunShape.All(sp=>r2.Point(sp.Offset).Z+sp.Radius<=bw.Limit+.002f),"near a wall the full solve did not run");
   Check(!ContactSolver.Bound(Array.Empty<ContactSphere>(),P(0,0,0),P(0,0,1),out _,out _),"empty shape bounded");
  }
  {
   // 0.1.122: a hand touching the gun is carried by the gun's own motion (no shaking); far away it is not.
   var hw=new World{Limit=5};var hand=new ContactSolver();var hs=new[]{new ContactSphere(Vector3.Zero,.04f)};
   hand.Solve(P(0,0,.3f),P(0,0,.3f),hs,hw);
   var from=P(0,0,0);var to=new ContactPose(new Vector3(.1f,0,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,.2f));
   hand.Carry(from,to);
   var expect=to.Position+Vector3.Transform(new Vector3(0,0,.3f),to.Rotation);
   Check(Vector3.Distance(hand.Previous.Position,expect)<1e-4f,"hand not carried with the gun");
   var gunS=new[]{new ContactSphere(new Vector3(0,0,.2f),.05f)};
   Check(ContactSolver.Near(hs,P(0,0,.3f),gunS,P(0,0,0),.03f)&&!ContactSolver.Near(hs,P(0,0,.5f),gunS,P(0,0,0),.03f),"touch test");
   var fresh=new ContactSolver();fresh.Carry(from,to);Check(!fresh.Ready,"carry before any solve");
   var before=hand.Previous;hand.Carry(P(0,0,0),P(5,0,0));Check(hand.Previous==before,"a teleporting gun dragged the hand along");
  }
  {
   // 0.1.124: a hand resting on the gun is steadied in the gun's frame.
   var rel=default(ContactPose);bool ok=false;var rnd=new Random(3);const float dt=1f/90;
   var place=new Vector3(0,-.03f,.25f);
   // The gun moving and turning: the hand stays exactly on its place on the gun.
   for(int i=0;i<60;i++)
   {
    var g=new ContactPose(new Vector3(i*.01f,0,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,i*.02f));
    var h=new ContactPose(g.Point(place),g.Rotation);
    var o=ContactSolver.Steady(g,h,ref rel,ref ok,dt);
    Check(Vector3.Distance(o.Position,h.Position)<1e-4f,"hand not following the gun's own motion exactly: frame "+i);
   }
   // Millimetre jitter against the gun's cells: filtered away.
   var gun0=P(0,0,0);float worst=0;
   for(int i=0;i<180;i++)
   {
    var j=new Vector3((float)rnd.NextDouble()-.5f,(float)rnd.NextDouble()-.5f,(float)rnd.NextDouble()-.5f)*.008f;
    var o=ContactSolver.Steady(gun0,new ContactPose(place+j,Quaternion.Identity),ref rel,ref ok,dt);
    if(i>30)worst=MathF.Max(worst,Vector3.Distance(o.Position,place));
   }
   Check(worst<.0025f,"hand still shakes on the gun: "+worst);
   // A deliberate slide of 10 cm along the gun follows within a quarter second.
   var slid=place+new Vector3(0,0,-.10f);ContactPose last=default;
   for(int i=0;i<22;i++)last=ContactSolver.Steady(gun0,new ContactPose(slid,Quaternion.Identity),ref rel,ref ok,dt);
   Check(Vector3.Distance(last.Position,slid)<.01f,"slide along the gun lags: "+Vector3.Distance(last.Position,slid));
   // Same frame again (drawing): no extra smoothing step; a far jump is taken at once.
   var again=ContactSolver.Steady(gun0,new ContactPose(slid+new Vector3(.02f,0,0),Quaternion.Identity),ref rel,ref ok,0);
   Check(Vector3.Distance(again.Position,last.Position)<1e-4f,"a second call in the frame moved the hand");
   var far=ContactSolver.Steady(gun0,new ContactPose(new Vector3(0,0,1),Quaternion.Identity),ref rel,ref ok,dt);
   Check(Vector3.Distance(far.Position,new Vector3(0,0,1))<1e-4f,"a far jump is smoothed");
   Console.WriteLine("PASS: hand resting on the held gun: follows the gun's motion exactly, millimetre jitter filtered, a deliberate slide follows in a quarter second.");
  }
  var w=new World();var shape=new[]{new ContactSphere(Vector3.Zero,.04f)};var solver=new ContactSolver();
  var a=solver.Solve(P(0,0,.3f),P(0,0,0),shape,w);Check(solver.Safe&&!solver.Blocked&&a==P(0,0,.3f),"free pose altered");
  a=solver.Solve(P(.4f,.2f,2.5f),P(0,0,0),shape,w);Check(solver.Safe&&solver.Blocked&&a.Position.Z<.96001f&&Math.Abs(a.Position.X-.4f)<.0001f,"fast sweep does not stop/slide at wall");
  a=solver.Solve(P(0,0,.2f),P(0,0,0),shape,w);Check(solver.Safe&&a==P(0,0,.2f),"cannot withdraw from contact");
  solver.Reset();w.Limit=.50f;var gun=ContactSolver.Weapon("shotgun");
  var sideways=new ContactPose(new Vector3(0,0,.1f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/2));
  solver.Solve(sideways,sideways,gun,w);
  a=solver.Solve(P(0,0,.1f),sideways,gun,w);Check(solver.Safe&&solver.Blocked,"barrel rotation did not collide");
  Check(gun.All(s=>a.Point(s.Offset).Z+s.Radius<=w.Limit+.002f),"rotated barrel crossed wall");
  solver.Reset();w.Limit=1;w.Pushes=4;a=solver.Solve(P(0,0,.1f),P(0,0,.1f),shape,w);Check(solver.Safe,"four successful depenetrations falsely hide hand");
  w.Invalid=true;solver.Solve(P(0,0,.1f),P(0,0,.1f),shape,w);Check(!solver.Safe,"saturated overlap buffer ignored");w.Invalid=false;
  solver.Reset();a=solver.Solve(P(0,0,1.1f),P(0,0,1.1f),shape,w);Check(solver.Safe&&a.Position.Z<.96001f,"initial overlap not separated");
  Check(ContactSolver.SphereSweep(new Vector3(-.2f,0,0),new Vector3(.2f,0,0),Vector3.Zero,.08f,out float t,out var n)&&Math.Abs(t-.3f)<.00001f&&n.X<-.99f,"free hand crosses gun volume");
  Check(!ContactSolver.SphereSweep(new Vector3(-.08f,0,0),new Vector3(-.2f,0,0),Vector3.Zero,.08f,out _,out _),"hand cannot withdraw from gun");
  solver.Reset();a=solver.Solve(P(0,0,1.2f),P(0,0,.5f),ContactSolver.Fist,w);
  float edge=a.Point(new Vector3(0,-.015f,.015f)).Z+.05f;
  Check(solver.Safe&&Math.Abs(edge-w.Limit)<.002f,"fist stops before native damage contact sphere reaches NPC");
  solver.Solve(new ContactPose(Vector3.Zero,default),P(0,0,0),shape,w);Check(!solver.Safe,"zero quaternion accepted");
  {
   // 0.1.117: a crossbow-like gun: a thin 72 cm stock plus a 60 cm bow across it.
   var pts=new System.Collections.Generic.List<Vector3>();
   for(float z=0;z<=.72f;z+=.004f)for(float x=-.02f;x<=.02f;x+=.01f)for(float y=-.03f;y<=.03f;y+=.01f)pts.Add(new Vector3(x,y,z));
   for(float x=-.30f;x<=.30f;x+=.004f)pts.Add(new Vector3(x,0,.55f));
   var box=ContactSolver.Box(new Vector3(-.30f,-.03f,0),new Vector3(.30f,.03f,.72f));
   var cells=ContactSolver.Cells(pts,.035f,96,out float cell)!;
   Check(cells.Length<=96&&cells.Length>10,"cell count "+cells.Length);
   float boxRadius=box.Max(c=>c.Radius),cellRadius=cells.Max(c=>c.Radius);
   Check(cellRadius<.05f&&cellRadius<boxRadius*.4f,"cell spheres not tighter than the box: "+cellRadius+" vs "+boxRadius);
   // Every point is covered, and empty space beside the stock is not.
   Check(pts.All(q=>cells.Any(c=>Vector3.Distance(c.Offset,q)<=c.Radius*1.21f)),"mesh point outside the cell spheres");
   Check(!cells.Any(c=>Vector3.Distance(c.Offset,new Vector3(.15f,0,.2f))<c.Radius),"empty space beside the stock collides");
   Check(box.Any(c=>Vector3.Distance(c.Offset,new Vector3(.15f,0,.2f))<c.Radius),"test premise: the box shape covered the empty space");
   Check(ContactSolver.Cells(new Vector3[0],.035f,96,out _)==null&&ContactSolver.Cells(new[]{new Vector3(float.NaN,0,0)},.035f,96,out _)==null,"degenerate cloud");
   Console.WriteLine("PASS: gun contact from occupied mesh cells: <=96 spheres of r<5 cm covering the mesh, empty space beside a thin stock free (box shape covered it).");
  }
  Console.WriteLine("PASS: free motion; fast wall sweep and sliding; long barrel rotation; withdrawal; four-pass separation; saturated query failure; starting overlap; left-hand/gun sphere sweep; invalid rotation.");
  Console.WriteLine("Synthetic solid plane and volumes. Actual level collider topology, fence gaps and game latency require runtime testing.");
 }
}
