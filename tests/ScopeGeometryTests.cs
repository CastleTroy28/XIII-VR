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
  {
   // 0.1.251: the tactical crossbow drawn 92 cm (was 72): its eyecup's lip,
   // slanted (half of its 12 sectors 5.5 mm further in), no longer closed in the
   // 6 mm the search looks at; the next depth took the eyepiece's narrower
   // ring further in. Searched at the 72 cm size and grown back: the lip again.
   var anchor=new Vector3(0,.04f,.5f);float g=.92f/.72f;
   var cup=new List<Vector3>();
   Box(cup,new Vector3(-.02f,-.06f,-.22f),new Vector3(.02f,.02f,.10f));
   Tube(cup,0,.054f,.016f,-.094f,.215f);                                         // the scope's tube
   for(int a=0;a<12;a++)for(int k=0;k<3;k++)
   {
    float an=(a+.5f)*MathF.PI*2/12+(k-1)*.08f;float depth=a<6?k*.002f:.0055f;
    cup.Add(new Vector3(.0115f*MathF.Cos(an),.054f+.0115f*MathF.Sin(an),-.102f+depth));     // the lip
    cup.Add(new Vector3(.0135f*MathF.Cos(an),.054f+.0135f*MathF.Sin(an),-.102f+depth));
   }
   for(int a=0;a<36;a++){float an=a*MathF.PI*2/36;cup.Add(new Vector3(.0085f*MathF.Cos(an),.054f+.0085f*MathF.Sin(an),-.102f+.0075f));} // the eyepiece's ring further in
   var tube72=new ScopeGeometry.Tube(0,.054f,.011f,-.094f,.215f,11,400);
   var eye72=ScopeGeometry.Eyepiece(cup,tube72);
   Check(eye72.Radius>.0105f&&eye72.Z<-.1005f,"the fixture's lip not found at 72 cm: "+eye72);
   var grown=new List<Vector3>();foreach(var p in cup)grown.Add(ScopeGeometry.FromTuned(p,anchor,g));
   var tube92=ScopeGeometry.FromTuned(tube72,anchor,g);
   var direct=ScopeGeometry.Eyepiece(grown,tube92);
   Check(direct.Radius<eye72.Radius*g*.85f,"the fixture does not show the old fault at 92 cm: "+direct);
   var tuned=new List<Vector3>();foreach(var p in grown)tuned.Add(ScopeGeometry.ToTuned(p,anchor,g));
   var back=ScopeGeometry.FromTuned(ScopeGeometry.Eyepiece(tuned,ScopeGeometry.FromTuned(tube72,anchor,1)),anchor,g);
   Check(MathF.Abs(back.Radius-eye72.Radius*g)<2e-4f&&MathF.Abs(back.Z-(anchor.Z+(eye72.Z-anchor.Z)*g))<2e-4f&&MathF.Abs(back.Y-(anchor.Y+(eye72.Y-anchor.Y)*g))<2e-4f,"searched at 72 cm and grown back, not the lip: "+back+" vs "+eye72);
   Check(back.Radius>direct.Radius*1.2f&&back.Depth<direct.Depth,"the grown-back lens not the lip's (wider, from the lip's own depth): "+back+" vs "+direct);
   var t2=ScopeGeometry.FromTuned(tube72,anchor,g);
   Check(MathF.Abs(t2.Radius-.011f*g)<1e-6f&&MathF.Abs(t2.Rear-(anchor.Z+(-.094f-anchor.Z)*g))<1e-5f&&MathF.Abs(t2.Y-(anchor.Y+(.054f-anchor.Y)*g))<1e-5f,"the tube not grown back around the muzzle: "+t2);
   Check(ScopeGeometry.ToTuned(new Vector3(.1f,.2f,.3f),anchor,float.NaN)==new Vector3(.1f,.2f,.3f)&&ScopeGeometry.FromTuned(tube72,anchor,0)==tube72,"a broken growth changed the search");
   var cupTube=ScopeGeometry.Find(cup);
   var foundTuned=ScopeGeometry.Find(tuned);
   Check(cupTube.HasValue==foundTuned.HasValue&&(!cupTube.HasValue||MathF.Abs(cupTube.Value.Radius-foundTuned!.Value.Radius)<1e-4f&&MathF.Abs(cupTube.Value.Rear-foundTuned.Value.Rear)<1e-3f),"the tube search on the gun brought back is not the 72 cm one");
   var gunTuned=new List<Vector3>();foreach(var p in gun)gunTuned.Add(ScopeGeometry.ToTuned(ScopeGeometry.FromTuned(p,anchor,g),anchor,g));
   var a72=ScopeGeometry.Find(gun);var a92=ScopeGeometry.Find(gunTuned);
   Check(a72!=null&&a92!=null&&MathF.Abs(a72.Value.Radius-a92.Value.Radius)<1e-4f&&MathF.Abs(a72.Value.Y-a92.Value.Y)<1e-4f&&MathF.Abs(a72.Value.Rear-a92.Value.Rear)<1e-3f,"the crossbow's tube not found again on the gun brought back: "+a72+" "+a92);
   Console.WriteLine("PASS: 0.1.251 a crossbow drawn longer than 72 cm has its sight searched at 72 cm and grown back around its muzzle: the picture at the eyecup's lip and as wide as it (a slanted lip took the eyepiece's narrower ring 1 cm in).");
  }
  {
   // 0.1.252: the tactical crossbow's eyepiece (searched at 72 cm): an eyecup
   // wider than the scope's tube, its lip at the rear, the cup's opening
   // (r 14.2 mm) and, 9 mm in, the model's flat glass across it (a vertex in
   // its middle, a ring of 15 mm) with narrower parts behind. The picture was
   // measured within the tube's radius (11 mm) at the lip, in front of the
   // glass, which showed around it; it now lies on the glass, as wide as the opening.
   var eyepiece=new List<Vector3>();var c=new Vector2(.0059f,.0524f);float rear=-.1072f;
   void Ring(float r,float z,int n,float turn=0){for(int a=0;a<n;a++){float an=(a+turn)*MathF.PI*2/n;eyepiece.Add(new Vector3(c.X+r*MathF.Cos(an),c.Y+r*MathF.Sin(an),z));}}
   Box(eyepiece,new Vector3(-.02f,-.06f,-.22f),new Vector3(.02f,.02f,.10f));
   Tube(eyepiece,c.X,c.Y,.011f,-.094f,.215f);                                  // the scope's tube
   Ring(.0157f,rear,46);Ring(.0165f,rear+.004f,50);Ring(.0148f,rear+.004f,25,.5f); // the lip and the cup
   Ring(.0142f,rear+.0075f,25);Ring(.0159f,rear+.0075f,200,.3f);Ring(.0162f,rear+.010f,200);
   eyepiece.Add(new Vector3(c.X,c.Y,rear+.0123f));Ring(.0150f,rear+.0123f,37);  // the glass
   Ring(.0068f,rear+.0135f,12);Ring(.0118f,rear+.0135f,72);                     // behind it
   var tube=new ScopeGeometry.Tube(c.X,c.Y+.0015f,.011f,-.094f,.215f,11,465);
   var rim=ScopeGeometry.Eyepiece(eyepiece,tube);
   var glass=ScopeGeometry.FindGlass(eyepiece,rim);
   Check(glass!=null&&MathF.Abs(glass.Value.Z-(rear+.0123f))<1e-4f&&MathF.Abs(glass.Value.Radius-.0150f)<.0005f&&MathF.Abs(glass.Value.Opening-.0142f)<.0003f,"the eyepiece's glass not found: "+glass+" (rim "+rim+")");
   var lens=ScopeGeometry.OnGlass(rim,glass!.Value);
   Check(MathF.Abs(lens.Z-(rear+.0123f-ScopeGeometry.EyeInside))<1e-4f&&lens.Z<glass.Value.Z,"the picture not just in front of the glass: "+lens);
   Check(MathF.Abs(lens.Radius-.0142f*ScopeGeometry.EyeFill)<.0003f&&lens.Radius>rim.Radius*1.1f,"the picture not as wide as the cup's opening: "+lens+" (rim "+rim+")");
   Check(MathF.Abs(lens.X-c.X)<1e-4f&&MathF.Abs(lens.Y-c.Y)<1e-4f,"the picture not centred on the glass: "+lens);
   // A narrow lens deep inside an open cup (not across it): the picture stays at the rim.
   var narrow=new List<Vector3>();
   Box(narrow,new Vector3(-.02f,-.06f,-.22f),new Vector3(.02f,.02f,.10f));Tube(narrow,c.X,c.Y,.011f,-.094f,.215f);
   foreach(var p in eyepiece)if(p.Z<rear+.011f&&MathF.Sqrt((p.X-c.X)*(p.X-c.X)+(p.Y-c.Y)*(p.Y-c.Y))>.014f)narrow.Add(p);
   narrow.Add(new Vector3(c.X,c.Y,rear+.0123f));for(int a=0;a<24;a++){float an=a*MathF.PI*2/24;narrow.Add(new Vector3(c.X+.007f*MathF.Cos(an),c.Y+.007f*MathF.Sin(an),rear+.0123f));}
   Check(ScopeGeometry.FindGlass(narrow,ScopeGeometry.Eyepiece(narrow,tube))==null,"a narrow inner lens taken for the glass across the opening");
   // A ring with nothing in its middle (an open tube) is not a glass.
   var open=new List<Vector3>();foreach(var p in eyepiece)if(!(MathF.Abs(p.Z-(rear+.0123f))<1e-5f&&MathF.Sqrt((p.X-c.X)*(p.X-c.X)+(p.Y-c.Y)*(p.Y-c.Y))<.001f))open.Add(p);
   Check(ScopeGeometry.FindGlass(open,ScopeGeometry.Eyepiece(open,tube))==null,"an open ring taken for a glass");
   Console.WriteLine("PASS: 0.1.252 an eyepiece's flat glass across its opening (wider than the scope's tube) found: the picture just in front of it, centred and as wide as the cup's opening; a narrow inner lens or an open ring leaves it at the rim.");
  }
 }
}
