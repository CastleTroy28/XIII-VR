using System;using System.Collections.Generic;using System.Numerics;using XiiiXR;
class ScopeGeometryTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Tube(List<Vector3> pts,float x,float y,float r,float z0,float z1)
 {for(float z=z0;z<=z1+1e-4f;z+=.004f)for(int a=0;a<24;a++){float t=a*MathF.PI*2/24;pts.Add(new Vector3(x+r*MathF.Cos(t),y+r*MathF.Sin(t),z));}}
 static void Box(List<Vector3> pts,Vector3 min,Vector3 max)
 {for(float x=min.X;x<=max.X+1e-4f;x+=.006f)for(float y=min.Y;y<=max.Y+1e-4f;y+=.006f)for(float z=min.Z;z<=max.Z+1e-4f;z+=.02f)if(x<=min.X+.006f||x>=max.X-.006f||y<=min.Y+.006f||y>=max.Y-.006f)pts.Add(new Vector3(x,y,z));}
 static void Main()
 {
  {
   // 0.1.121: the scope camera renders only with an eye at the eyepiece.
   var lens=new Vector3(0,1.4f,.3f);var fwd=Vector3.UnitZ;
   Check(ScopeGeometry.Viewing(lens,lens+new Vector3(.03f,.01f,-.08f),fwd),"eye at the eyepiece: no picture");
   Check(!ScopeGeometry.Viewing(lens,lens+new Vector3(0,.35f,-.45f),fwd),"gun at the hip: the scope still renders");
   Check(!ScopeGeometry.Viewing(lens,lens+new Vector3(0,0,.1f),fwd),"looking into the muzzle end: the scope renders");
   Check(!ScopeGeometry.Viewing(lens,lens+new Vector3(.2f,0,-.1f),fwd),"eye beside the scope: the scope renders");
   Check(!ScopeGeometry.Viewing(lens,lens-fwd*.1f,Vector3.Zero)&&!ScopeGeometry.Viewing(lens,new Vector3(float.NaN,0,0),fwd),"degenerate view");
   Console.WriteLine("PASS: scope picture only with an eye close behind the eyepiece, looking along the scope (not at the hip, beside it or from the front).");
  }
  // Crossbow-like: stock/grip below, a barrel tube running to the muzzle, a scope above.
  var gun=new List<Vector3>();
  Box(gun,new Vector3(-.02f,-.06f,-.22f),new Vector3(.02f,.02f,.10f));
  Tube(gun,0,.035f,.012f,-.20f,.50f);
  Tube(gun,0,.075f,.016f,-.12f,.07f);
  for(float x=-.30f;x<=.30f;x+=.004f)gun.Add(new Vector3(x,.035f,.40f));   // bow limbs
  var found=ScopeGeometry.Find(gun);
  Check(found!=null,"scope not found");var t=found!.Value;
  Check(MathF.Abs(t.Y-.075f)<.004f&&MathF.Abs(t.X)<.003f&&MathF.Abs(t.Radius-.016f)<.0025f,"scope axis/radius wrong: "+t);
  Check(MathF.Abs(t.Rear+.12f)<.01f&&MathF.Abs(t.Front-.07f)<.01f&&t.Sectors>=6,"scope extent wrong: "+t);
  // Without a scope: the barrel reaching the muzzle is not taken for one.
  var bare=new List<Vector3>();
  Box(bare,new Vector3(-.02f,-.06f,-.22f),new Vector3(.02f,.02f,.10f));
  Tube(bare,0,.035f,.012f,-.20f,.50f);
  Check(ScopeGeometry.Find(bare)==null,"barrel taken for a scope");
  // A half ring (rail, open channel) is not a tube.
  var rail=new List<Vector3>(bare);
  for(float z=-.1f;z<=.1f;z+=.004f)for(int a=0;a<12;a++){float ang=a*MathF.PI/12;rail.Add(new Vector3(.015f*MathF.Cos(ang),.07f+.015f*MathF.Sin(ang),z));}
  Check(ScopeGeometry.Find(rail)==null,"half-open channel taken for a scope");
  Check(ScopeGeometry.Find(new Vector3[0])==null&&ScopeGeometry.Find(new[]{new Vector3(float.NaN,0,0)})==null,"degenerate input");
  // 0.1.119: the game's crossbow — wide limbs dense with vertices pull the median off the scope;
  // the rig's scope bone (hint) fixes the axis.
  var wide=new List<Vector3>();
  Box(wide,new Vector3(-.02f,-.06f,-.22f),new Vector3(.02f,.02f,.10f));
  Tube(wide,0,.035f,.012f,-.20f,.50f);
  Tube(wide,0,.075f,.016f,-.12f,.07f);
  for(float x=-.35f;x<=-.05f;x+=.002f)for(float y=.0f;y<=.09f;y+=.004f)wide.Add(new Vector3(x,y,.38f));
  var hinted=ScopeGeometry.Find(wide,new Vector3(0,.095f,-.02f));
  Check(hinted!=null&&MathF.Abs(hinted.Value.X)<.001f&&MathF.Abs(hinted.Value.Y-.075f)<.004f&&MathF.Abs(hinted.Value.Radius-.016f)<.0025f,"hinted scope wrong: "+hinted);
  Check(ScopeGeometry.Find(wide,new Vector3(.2f,.3f,0))==null,"a hint far from any tube found one");
  // 0.1.122: the game's crossbow — a 65 cm rail/bolt tube under the scope must not be taken for it.
  var railed=new List<Vector3>();
  Box(railed,new Vector3(-.02f,-.06f,-.22f),new Vector3(.02f,.0f,.10f));
  Tube(railed,-.008f,.017f,.014f,-.219f,.426f);
  Tube(railed,-.008f,.050f,.018f,-.10f,.12f);
  var hint=new Vector3(-.008f,.075f,-.002f);
  var scope=ScopeGeometry.Find(railed,hint);
  Check(scope!=null&&MathF.Abs(scope.Value.Y-.050f)<.004f&&scope.Value.Length<ScopeGeometry.MaxLength,"the rail under the scope taken for it: "+scope);
  var onlyRail=new List<Vector3>();Box(onlyRail,new Vector3(-.02f,-.06f,-.22f),new Vector3(.02f,.0f,.10f));Tube(onlyRail,-.008f,.017f,.014f,-.219f,.426f);
  Check(ScopeGeometry.Find(onlyRail,hint)==null&&ScopeGeometry.Find(onlyRail)==null,"a 65 cm rail taken for a scope");
  var plain=ScopeGeometry.FromHint(hint);Check(plain.Y<hint.Y&&plain.Rear<hint.Z&&plain.Front>hint.Z&&plain.Radius>ScopeGeometry.MinRadius,"no plain tube under the turret");
  Console.WriteLine("PASS: crossbow scope tube found in the mesh (axis/radius/extent within mm); a barrel to the muzzle or an open rail channel is never taken for a scope.");
  {
   // 0.1.123: the lens sits at the eyepiece's own opening: the eyepiece reaches 2.5 cm behind the fitted
   // tube and is narrower than it; the stock lies below; a glass vertex at the very centre.
   var bow=new List<Vector3>();
   Box(bow,new Vector3(-.02f,-.06f,-.30f),new Vector3(.02f,.012f,.10f));
   Tube(bow,-.008f,.050f,.020f,-.10f,.12f);
   Tube(bow,-.008f,.050f,.016f,-.125f,-.10f);                                  // eyepiece housing
   for(float r=.0125f;r<=.0161f;r+=.0012f)for(int a=0;a<36;a++){float an=a*MathF.PI*2/36;bow.Add(new Vector3(-.008f+r*MathF.Cos(an),.050f+r*MathF.Sin(an),-.125f));} // rim face
   bow.Add(new Vector3(-.008f,.050f,-.118f));                                 // eyepiece glass centre
   var tube=new ScopeGeometry.Tube(-.008f,.050f,.020f,-.10f,.12f,12,400);
   var eye=ScopeGeometry.Eyepiece(bow,tube);
   Check(eye.Z>-.1252f&&eye.Z<-.1238f,"lens not at the eyepiece rim: "+eye);
   Check(eye.Radius<.0125f&&eye.Radius>.0105f,"lens not filling the opening (sticks out or too small): "+eye);
   Check(MathF.Abs(eye.X+.008f)<.0011f&&MathF.Abs(eye.Y-.050f)<.0011f&&eye.Sectors>=8,"lens off the eyepiece axis: "+eye);
   // Axis 3 mm off: the opening's centre is found.
   var off=ScopeGeometry.Eyepiece(bow,tube with {X=-.005f,Y=.052f});
   Check(MathF.Abs(off.X+.008f)<.0011f&&MathF.Abs(off.Y-.050f)<.0011f&&off.Radius>.0105f,"opening centre not found from an offset axis: "+off);
   // Nothing measurable: a small lens at the tube's rear, never wider than the tube.
   var none=ScopeGeometry.Eyepiece(new List<Vector3>(),tube);
   Check(MathF.Abs(none.Z-(tube.Rear+.004f))<1e-5f&&none.Radius<tube.Radius,"no fallback lens");
   Console.WriteLine("PASS: the scope picture sits at the eyepiece rim and just fills its opening (centre found around the axis), a small lens when nothing is measured.");
  }
 }
}
