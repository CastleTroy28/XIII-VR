using System;using System.Collections.Generic;using System.Linq;using System.Numerics;using XiiiXR;
class HandToolsTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Near(Vector3 a,Vector3 b,float tol,string s){Check(Vector3.Distance(a,b)<=tol,s+" (got "+a+" expected "+b+")");}
 // A box of 8 vertices and 12 triangles, offset by 'o', sized 's'.
 static void Box(List<Vector3> pts,List<int> tris,Vector3 o,Vector3 s)
 {
  int b=pts.Count;
  for(int i=0;i<8;i++)pts.Add(o+new Vector3((i&1)!=0?s.X:0,(i&2)!=0?s.Y:0,(i&4)!=0?s.Z:0));
  int[] f={0,1,3,0,3,2,4,6,7,4,7,5,0,4,5,0,5,1,2,3,7,2,7,6,0,2,6,0,6,4,1,5,7,1,7,3};
  foreach(int i in f)tris.Add(b+i);
 }
 static void Main()
 {
  // 0.1.195: the zipline hook is its own gadget (slot 16), no longer the grappling hook.
  Check(HandTools.Kind(16,"")==HandToolKind.Zipline&&HandTools.Kind(16,"eqp_whatever")==HandToolKind.Zipline,"the zipline slot not the zipline hook");
  Check(HandTools.Kind(33,"eqp_grappling_hook")==HandToolKind.Grapple&&HandTools.Kind(33," Grappling Hook ")==HandToolKind.Grapple,"the grappling hook not told by its name");
  Check(HandTools.Kind(33,"eqp_zipline")==HandToolKind.Zipline,"a zipline gadget by name not the zipline hook");
  Check(HandTools.Kind(33,"")==HandToolKind.None&&HandTools.Kind(33,null)==HandToolKind.None&&HandTools.Kind(5,"medkit")==HandToolKind.None,"another gadget taken for a hand tool");
  Check(HandTools.Reaches(new Vector3(0,1,0),new Vector3(.1f,1.1f,0)),"a hand at the holding hand cannot take the tool");
  Check(!HandTools.Reaches(new Vector3(0,1,0),new Vector3(.3f,1,0))&&!HandTools.Reaches(new Vector3(float.NaN,1,0),new Vector3(0,1,0)),"a hand far away (or lost) takes the tool");
  Console.WriteLine("PASS: hand tools: the zipline hook and the grappling hook told apart; passed only within reach.");

  // 0.1.197: a hook drawn in the left hand, mirrored into the right hand across the hand root (x):
  // the same place mirrored, the turn mirrored, the thing itself a mirror image.
  var leftAt=new Vector3(.03f,-.02f,.11f);var leftTurn=Quaternion.CreateFromYawPitchRoll(.4f,-.7f,1.1f);var leftSize=new Vector3(.86f,.86f,.86f);
  var mirrored=HandTools.MirrorAcrossHand(leftAt,leftTurn,leftSize);
  Near(mirrored.position,new Vector3(-.03f,-.02f,.11f),1e-6f,"the hook not at the mirrored place");
  Check(mirrored.scale.X<0&&Math.Abs(mirrored.scale.X+.86f)<1e-6f&&mirrored.scale.Y==.86f&&mirrored.scale.Z==.86f,"the hook in the right hand not a mirror image");
  // Every point of the hook lands on the mirror of where it is in the left hand.
  foreach(var point in new[]{new Vector3(0,0,.3f),new Vector3(.05f,.02f,-.04f),new Vector3(-.1f,.07f,.2f)})
  {
   var inLeft=leftAt+Vector3.Transform(point*leftSize,leftTurn);
   var inRight=mirrored.position+Vector3.Transform(point*mirrored.scale,mirrored.rotation);
   Near(inRight,new Vector3(-inLeft.X,inLeft.Y,inLeft.Z),1e-5f,"a point of the hook not mirrored across the hand");
  }
  Console.WriteLine("PASS: the grappling hook in the right hand is the mirror image of the left hand's (every point mirrored across the hand).");
  // A placement read back from a matrix that mirrors (the game's mirrored left wrist): turn and size rebuild it.
  foreach(bool mirror in new[]{false,true})
  {
   var q=Quaternion.CreateFromYawPitchRoll(-.9f,.3f,2.1f);var size=new Vector3(mirror?-.6f:.6f,.6f,.6f);
   var ax=Vector3.Transform(Vector3.UnitX*size.X,q);var ay=Vector3.Transform(Vector3.UnitY*size.Y,q);var az=Vector3.Transform(Vector3.UnitZ*size.Z,q);
   var d=ItemPlacement.Decompose(ax,ay,az);Check(d!=null,"a placement not read back");
   foreach(var point in new[]{new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(.3f,-.2f,.9f)})
   {
    var want=ax*point.X+ay*point.Y+az*point.Z;var got=Vector3.Transform(point*d!.Value.scale,d.Value.rotation);
    Near(got,want,1e-4f,"a "+(mirror?"mirrored ":"")+"placement read back wrong");
   }
   Check(d!.Value.scale.X<0==mirror,"a mirror image not kept as one");
  }
  Check(ItemPlacement.Decompose(Vector3.Zero,Vector3.UnitY,Vector3.UnitZ)==null&&ItemPlacement.Decompose(new Vector3(float.NaN,0,0),Vector3.UnitY,Vector3.UnitZ)==null,"a flat or broken placement read back");
  Console.WriteLine("PASS: a placement that mirrors (the game's hook on its mirrored wrist, a hold mirrored into the other hand) is kept as a mirror image, every point in place.");
  // The rope: L3 or the right B lets go; fired from the right hand, R3 only (the left Y reloads the gun in the left hand).
  var none=new HandControls(true,0,0,0);var rb=new HandControls(true,HandControls.B,HandControls.B,0);var r3=new HandControls(true,HandControls.Stick,HandControls.Stick,0);var ly=new HandControls(true,HandControls.B,HandControls.B,0);
  var l3=new ActionEdge(true,false);var noL3=new ActionEdge(false,false);
  Check(RopeRelease.Pressed(false,l3,none,none,1)&&RopeRelease.Pressed(false,noL3,none,rb,1)&&!RopeRelease.Pressed(false,noL3,none,r3,1),"left-hand rope: L3 / right B do not let go (or R3 does)");
  Check(RopeRelease.Pressed(true,noL3,none,r3,1)&&RopeRelease.Pressed(true,noL3,none,r3,0)&&!RopeRelease.Pressed(true,l3,none,none,1)&&!RopeRelease.Pressed(true,noL3,none,rb,1)&&!RopeRelease.Pressed(true,noL3,ly,none,1),"right-hand rope: R3 does not let go (or L3 / B / left Y do)");
  Check(!RopeRelease.Pressed(true,noL3,none,new HandControls(false,HandControls.Stick,HandControls.Stick,0),1),"a lost controller lets go");
  Console.WriteLine("PASS: the rope of a hook fired from the right hand is let go with R3 (not L3, B or the left Y); from the left hand with L3 or the right B.");
  // The Uzi handle: a small separate piece at the rigged part is drawn back with it;
  // the body (big), another rigged part and a far piece are not.
  var pts=new List<Vector3>();var tris=new List<int>();
  Box(pts,tris,new Vector3(-.02f,0,-.05f),new Vector3(.04f,.05f,.3f));          // 0..7   body (root)
  Box(pts,tris,new Vector3(-.005f,.05f,.08f),new Vector3(.01f,.005f,.02f));     // 8..15  the rigged slider (seed)
  Box(pts,tris,new Vector3(-.012f,.055f,.075f),new Vector3(.024f,.02f,.012f));  // 16..23 the handle (root)
  Box(pts,tris,new Vector3(-.01f,.05f,.1f),new Vector3(.02f,.01f,.01f));        // 24..31 the magazine catch (another bone)
  Box(pts,tris,new Vector3(-.01f,.05f,.22f),new Vector3(.02f,.01f,.01f));       // 32..39 the front sight (far)
  var seed=new bool[pts.Count];var excl=new bool[pts.Count];for(int i=8;i<16;i++)seed[i]=true;for(int i=24;i<32;i++)excl[i]=true;
  var sel=KnobMath.Select(pts.ToArray(),tris.ToArray(),seed,excl,.035f,.08f);var set=new HashSet<int>(sel);
  Check(set.Count==8&&set.SetEquals(new[]{16,17,18,19,20,21,22,23}),"the handle not chosen alone: "+string.Join(",",sel));
  // The handle welded (shared positions) to the slider: its vertices still move, the slider's are not listed twice.
  var weld=new List<Vector3>(pts);weld.Add(pts[8]);var wt=new List<int>(tris){16,17,40};var ws=new bool[weld.Count];var we=new bool[weld.Count];
  for(int i=8;i<16;i++)ws[i]=true;for(int i=24;i<32;i++)we[i]=true;
  var s2=new HashSet<int>(KnobMath.Select(weld.ToArray(),wt.ToArray(),ws,we,.035f,.08f));
  Check(s2.Contains(16)&&s2.Contains(40)&&!s2.Contains(8)&&!s2.Contains(0),"a handle welded to the slider not drawn back");
  // One welded mesh (everything joined to the body): nothing extra moves.
  var joined=new List<int>(tris){0,8,16,0,16,24,0,24,32};
  Check(KnobMath.Select(pts.ToArray(),joined.ToArray(),seed,excl,.035f,.08f).Length==0,"the whole gun drawn back");
  Check(KnobMath.Select(pts.ToArray(),tris.ToArray(),new bool[pts.Count],excl,.035f,.08f).Length==0,"a handle chosen without a rigged part");
  Console.WriteLine("PASS: the Uzi's top handle (a small separate piece at the rigged bolt part) is drawn back with it; the body, other rigged parts, far pieces, a one-piece mesh are not.");
  // 0.1.197: the handle is what stands up from the gun's top around the bolt bone (not plates on its side).
  {
   var g=new List<Vector3>();var gt=new List<int>();
   Box(g,gt,new Vector3(-.02f,0,0),new Vector3(.04f,.08f,.4f));             // 0..7   body (root), top at y .08
   Box(g,gt,new Vector3(-.004f,.06f,.28f),new Vector3(.008f,.01f,.02f));    // 8..15  the rigged bolt part inside
   Box(g,gt,new Vector3(-.01f,.08f,.27f),new Vector3(.02f,.02f,.04f));      // 16..23 the top handle (root)
   Box(g,gt,new Vector3(.022f,.03f,.2f),new Vector3(.003f,.026f,.09f));     // 24..31 a plate on the right side (root)
   Box(g,gt,new Vector3(-.004f,.08f,.35f),new Vector3(.008f,.02f,.01f));    // 32..39 a front sight post in front of it
   Box(g,gt,new Vector3(-.01f,.02f,.1f),new Vector3(.02f,.1f,.03f));        // 40..47 the magazine (another bone), tall
   for(int k=0;k<10;k++)Box(g,gt,new Vector3(-.02f,0,k*.04f),new Vector3(.04f,.08f,.04f)); // 48.. the body's cover in segments (its top points)
   var gs=new bool[g.Count];var ge=new bool[g.Count];for(int i=8;i<16;i++)gs[i]=true;for(int i=40;i<48;i++)ge[i]=true;
   var bone=new Vector3(0,.068f,.289f);
   var top=KnobMath.SelectTop(g.ToArray(),gt.ToArray(),gs,ge,bone,.025f,.04f,.07f,out float surface);var ts=new HashSet<int>(top);
   Check(ts.SetEquals(new[]{16,17,18,19,20,21,22,23}),"the top handle not chosen alone: "+string.Join(",",top)+" (top "+surface+")");
   Check(Math.Abs(surface-.08f)<1e-4f,"the gun's top by the handle not read under the front sight: "+surface);
   // Welded to the cover (one piece with the body): only what stands up moves.
   var gw=new List<int>(gt){0,16,1};
   var welded=new HashSet<int>(KnobMath.SelectTop(g.ToArray(),gw.ToArray(),gs,ge,bone,.025f,.04f,.07f,out _));
   Check(welded.SetEquals(new[]{18,19,22,23}),"a welded handle: not only its standing part: "+string.Join(",",welded));
   // Nothing standing up there: nothing extra moves; a lost bone: nothing.
   var flat=g.GetRange(0,16);var ft=gt.GetRange(0,24);
   Check(KnobMath.SelectTop(flat.ToArray(),ft.ToArray(),new bool[16].Select((_,i)=>i>=8).ToArray(),new bool[16],bone,.025f,.04f,.07f,out _).Length==0,"a flat top gave a handle");
   Check(KnobMath.SelectTop(g.ToArray(),gt.ToArray(),gs,ge,new Vector3(float.NaN,0,0),.025f,.04f,.07f,out _).Length==0,"a lost bone gave a handle");
  }
  Console.WriteLine("PASS: 0.1.197 the Uzi's top handle is what stands up from the gun's top around the bolt bone (a sight in front does not hide it; welded, only its standing part); side plates, the magazine, a flat top are not.");
  // 0.1.198: the zipline hook's handle (its rubber grip) found among its pieces; held as a pistol's grip.
  {
   var h=new List<Vector3>();var ht=new List<int>();
   // A cylinder (n sides) from a to b of radius r, as a separate piece.
   void Rod(Vector3 a,Vector3 b,float r,int sides=10)
   {
    var axis=Vector3.Normalize(b-a);var u=Vector3.Normalize(Vector3.Cross(axis,Math.Abs(axis.X)<.9f?Vector3.UnitX:Vector3.UnitY));var v=Vector3.Cross(axis,u);int start=h.Count;
    for(int k=0;k<sides;k++){float t=k*MathF.PI*2/sides;var o=u*MathF.Cos(t)*r+v*MathF.Sin(t)*r;h.Add(a+o);h.Add(b+o);}
    for(int k=0;k<sides;k++){int i0=start+2*k,i1=start+2*((k+1)%sides);ht.AddRange(new[]{i0,i0+1,i1,i1,i0+1,i1+1});}
   }
   // The hook: claw plate at the top, hub, two thin frame arms, the thick grip bar at the bottom.
   Box(h,ht,new Vector3(-.01f,.20f,-.06f),new Vector3(.02f,.10f,.012f));      // the claw (a flat plate)
   Rod(new Vector3(0,.12f,-.06f),new Vector3(0,.20f,-.06f),.012f);            // the hub's rod (short)
   Rod(new Vector3(0,.12f,-.06f),new Vector3(-.06f,.02f,-.06f),.006f);        // a frame arm (thin)
   Rod(new Vector3(0,.12f,-.06f),new Vector3(.06f,.02f,-.06f),.006f);         // the other arm
   int gripTri=ht.Count;
   Rod(new Vector3(-.055f,.02f,-.06f),new Vector3(.055f,.02f,-.06f),.014f);   // the rubber grip (thick, 11 cm)
   var bar=GripBarMath.Find(h.ToArray(),ht.ToArray());
   Check(bar!=null,"the hook's handle not found");
   Near(bar!.Value.Center,new Vector3(0,.02f,-.06f),.004f,"the handle's middle");
   Check(bar.Value.Axis.X>.99f,"the handle's line not along the bar (its sign the mesh's own, +X): "+bar.Value.Axis);
   Check(bar.Value.Toward.Y>.99f,"the rest of the hook not found above the handle: "+bar.Value.Toward);
   // Held: the bar along the grip line through the fist, the hook forward of it.
   var grip=Vector3.Normalize(new Vector3(.2f,.9f,.3f));var forward=Vector3.Normalize(Vector3.Cross(grip,new Vector3(1,0,0)));if(forward.Z<0)forward=-forward;var fist=new Vector3(.01f,-.03f,.06f);
   var held=GripBarMath.Hold(bar.Value,grip,forward,fist);
   Near(held.position+Vector3.Transform(bar.Value.Center,held.rotation),fist,1e-4f,"the handle's middle not in the fist");
   Check(Math.Abs(Math.Abs(Vector3.Dot(Vector3.Transform(bar.Value.Axis,held.rotation),grip))-1)<1e-3f,"the handle not along the grip line");
   Check(Vector3.Dot(Vector3.Transform(new Vector3(0,.25f,-.06f),held.rotation)+held.position-fist,forward)>.15f,"the claw not forward of the fist");
   // One welded piece (no separate handle): none; a lone stick is not a hook's handle.
   var weldT=new List<int>(ht);weldT.AddRange(new[]{0,h.Count-1,8});
   Check(GripBarMath.Find(h.ToArray(),weldT.ToArray())==null,"a handle found in one welded piece");
   // ...unless the grip is of its own material (submesh): then it is a piece of its own.
   var metal=weldT.GetRange(0,gripTri);metal.AddRange(new[]{0,h.Count-1,8});var rubber=ht.GetRange(gripTri,ht.Count-gripTri);
   var byMaterial=GripBarMath.Find(h.ToArray(),weldT.ToArray(),new List<int[]>{metal.ToArray(),rubber.ToArray()});
   Check(byMaterial!=null,"a grip of its own material not found where it touches the frame");
   Near(byMaterial!.Value.Center,new Vector3(0,.02f,-.06f),.004f,"the grip's middle (by its material)");
   Check(GripBarMath.Report(h.ToArray(),weldT.ToArray(),new List<int[]>{metal.ToArray(),rubber.ToArray()}).Contains("pieces"),"no pieces in the report");
   // The axis's sign: the mesh's own (along its nearest axis, positive).
   Near(GripBarMath.Signed(new Vector3(-1,0,0)),new Vector3(1,0,0),1e-6f,"the bar's sign");
   Near(GripBarMath.Signed(new Vector3(.1f,-.9f,.2f)),new Vector3(-.1f,.9f,-.2f),1e-6f,"the bar's sign along Y");
   Near(GripBarMath.Signed(new Vector3(.1f,.2f,.9f)),new Vector3(.1f,.2f,.9f),1e-6f,"the bar's sign kept");
   // A handle as long as most of the hook (a T bar across its frame) is a handle; the whole length is not.
   Check(GripBarMath.Handle(.16f,.02f,.018f,.20f)&&!GripBarMath.Handle(.18f,.02f,.018f,.20f)&&!GripBarMath.Handle(.10f,.02f,.008f,.20f),"a long handle refused (or a flat or too long one taken)");
   // Drawn at its own size: the hook (32 cm, fitted to 20 cm) back to 32 cm; at most 40 cm, at least 10 cm.
   Check(Math.Abs(GripBarMath.TrueScale(.62f)-1/.62f)<1e-4f&&Math.Abs(GripBarMath.TrueScale(.1f)-2)<1e-6f&&Math.Abs(GripBarMath.TrueScale(4)-.5f)<1e-6f&&GripBarMath.TrueScale(float.NaN)==1,"the hook's own size");
   // The game's own hook (its mesh as the mod wrote it, 0.1.198): the rubber grip,
   // not a frame arm (a flat bar, longer and thicker) nor the shaft to the claw (along it).
   var real=new List<Vector3>();var realT=new List<int>();var inv=System.Globalization.CultureInfo.InvariantCulture;
   string? file=new[]{System.IO.Path.Combine(AppContext.BaseDirectory,"..","tests","fixtures","zipline-hook-mesh.txt"),System.IO.Path.Combine("xiii-xr","tests","fixtures","zipline-hook-mesh.txt")}.FirstOrDefault(System.IO.File.Exists);
   // The game's model is not distributed with the source (tests/fixtures/README.md):
   // without it this one case is skipped.
   if(file==null)Console.WriteLine("SKIP: the game's own hook (tests/fixtures/zipline-hook-mesh.txt, the game's model, not distributed) absent");
   else
   {
   foreach(var line in System.IO.File.ReadLines(file))
   {
    var w=line.Split(' ',StringSplitOptions.RemoveEmptyEntries);if(w.Length<4)continue;
    if(w[0]=="v")real.Add(new Vector3(float.Parse(w[1],inv),float.Parse(w[2],inv),float.Parse(w[3],inv)));
    else if(w[0]=="t")realT.AddRange(new[]{int.Parse(w[1]),int.Parse(w[2]),int.Parse(w[3])});
   }
   var game=GripBarMath.Find(real.ToArray(),realT.ToArray());
   Check(game!=null,"the game's hook: its grip not found");
   Near(game!.Value.Center,new Vector3(.0219f,-.1513f,.075f),.003f,"the game's hook: its grip's middle");
   Check(game.Value.Axis.Z>.99f&&game.Value.Toward.Y>.99f&&Math.Abs(game.Value.Thickness-.0196f)<.002f,"the game's hook: its grip along its own line, the frame above it: "+game.Value.Axis+" "+game.Value.Toward+" "+game.Value.Thickness);
   }
   // The arm that 0.1.198 took: flat (3.1 x 1.9 cm) and along the way to the hook's middle - not a handle.
   Check(!GripBarMath.Handle(.1062f,.0309f,.0191f,.2f)&&!GripBarMath.Beside(new Vector3(-.0014f,.0334f,.0314f),Vector3.Normalize(new Vector3(0,.949f,.315f))),"a frame arm taken for the handle");
   Check(GripBarMath.Beside(new Vector3(0,.077f,0),Vector3.UnitZ)&&!GripBarMath.Beside(new Vector3(.001f,-.075f,0),Vector3.UnitY),"the shaft (the hook along it) taken for the handle");
   var big=GripBarMath.Scaled(bar.Value,1.6f);
   Check(Math.Abs(big.Thickness-bar.Value.Thickness*1.6f)<1e-6f&&Math.Abs(big.Length-bar.Value.Length*1.6f)<1e-6f&&Vector3.Distance(big.Center,bar.Value.Center*1.6f)<1e-6f&&big.Axis==bar.Value.Axis,"the handle at the hook's own size");
  }
  Console.WriteLine("PASS: 0.1.198 the zipline hook's handle is its thick round bar (not the thin frame, the short hub or the flat claw); held with that bar along the pistol grip's line in the fist, the hook forward; a grip of its own material found where it touches the frame; drawn at the hook's own size; the game's hook: its round rubber grip, not a flat frame arm or the shaft.");
 }
}
