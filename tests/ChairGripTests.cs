using System;using System.Linq;using System.Collections.Generic;using System.Numerics;using XiiiXR;
class ChairGripTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 // Hand-mesh offset from the contact, expressed in the chair's frame.
 static Vector3 C(FingerPoseMath f,Vector3 v)=>Vector3.Transform(v,f.ChairRotation);
 static void Main()
 {

  const string profile="prop_wpn_ms_chair";int cases=0;
  foreach(bool right in new[]{true,false})foreach(float scale in new[]{.85f,1f,1.15f})foreach(float thickness in new[]{.012f,.026f,.042f})foreach(bool shaped in new[]{false,true})
  {
   string side=right?"R":"L";float sign=right?1:-1;
   var names=new List<string>();var rest=new List<Matrix4x4>();
   for(int f=0;f<4;f++)for(int j=0;j<3;j++)
   {
    names.Add($"{side}_Finger_{f+1:00}_{j+1:00}SHJnt");
    float z=(.09f+(f==1?.004f:f==3?-.009f:0)+j*(.030f-f*.0015f))*scale;
    rest.Add(Matrix4x4.CreateRotationZ(.15f*f)*Matrix4x4.CreateTranslation(sign*(-.03f+.02f*f)*scale,0,z));
   }
   for(int j=0;j<3;j++){names.Add($"{side}_Thumb_01_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateRotationY(-sign*.3f)*Matrix4x4.CreateTranslation(sign*(-.05f-j*.003f)*scale,-.012f*scale,(.043f+j*.027f)*scale));}
   var fingers=new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
   foreach(int bone in fingers.RimDistalBones)
   {
    var center=rest[bone].Translation+(rest[bone].Translation-rest[bone-1].Translation)*.65f-Vector3.UnitY*.005f;
    fingers.SetRimPadCloud(bone,new[]{center,center+Vector3.UnitX*.0005f,center-Vector3.UnitX*.0005f,rest[bone].Translation});
   }
   Check(fingers.RimMeasuredPads==5,"native pad sampling incomplete");
   var surface=shaped?MakeSurface(thickness):null;
   var clock=System.Diagnostics.Stopwatch.StartNew();var anchor=fingers.ChairContact(thickness,surface);long solveMs=clock.ElapsedMilliseconds;var pose=fingers.Pose(0,0,profile);var pads=fingers.RimPads(pose);
   float worst=0,thumbWorst=0;
   // 0.1.81: each finger lies on/around the board (its bone line within a
   // finger's thickness of the surface somewhere), on the finger side.
   float Gap(Vector3 q){float mid=shaped?.20f*q.X:0,edge=shaped?.20f*Math.Abs(q.X):-.008f;
    float dy=Math.Max(0,Math.Abs(q.Y-mid)-thickness*.5f),dz=Math.Max(0,Math.Max(edge-q.Z,q.Z-edge-.08f));return MathF.Sqrt(dy*dy+dz*dz);}
   var off0=Vector3.UnitZ*NativeHandMesh.WristZ-anchor;
   for(int f=0;f<4;f++)
   {
    float near=1;var line=new[]{pose[f*3].Translation,pose[f*3+1].Translation,pose[f*3+2].Translation,pads[f]-Vector3.UnitZ*NativeHandMesh.WristZ};
    for(int k=1;k<4;k++)for(int u=0;u<=8;u++)near=Math.Min(near,Gap(C(fingers,Vector3.Lerp(line[k-1],line[k],u/8f)+off0)));
    worst=Math.Max(worst,near-.004f);
    // Under the board only when curled round its far end, never through it.
    var pad=C(fingers,pads[f]-anchor);var dip=C(fingers,pose[f*3+2].Translation+off0);
    Check(pad.Y-(shaped?.20f*pad.X:0)>-thickness*.5f||Math.Max(pad.Z,dip.Z)>(shaped?.20f*Math.Abs(pad.X):-.008f)+.08f,"finger wrapped under the board");
   }
   for(int i=4;i<5;i++)
   {
    var p=C(fingers,pads[i]-anchor);float centerY=shaped?.20f*p.X:0;
    float edgeZ=shaped?.20f*Math.Abs(p.X):-.008f;
    {
     // Thumb: under the lower face (thin rail) or against the near edge (thick rail).
     float under=Math.Abs(p.Y-centerY+thickness*.5f)+Math.Max(0,edgeZ-.002f-p.Z);
     float hook=Math.Abs(p.Z-edgeZ)+Math.Max(0,Math.Abs(p.Y-centerY)-thickness*.5f);
     thumbWorst=Math.Max(thumbWorst,Math.Min(under,hook));
    }
   }
   Console.WriteLine($"chair {right} {scale} {thickness} shaped={shaped} fingerGap={worst*1000:F2} thumb={thumbWorst*1000:F2} target={fingers.ChairContactError*1000:F2}");
   Check(worst<.006f,"fingers do not rest on the board");
   // Capsule-clear thumb on a short synthetic thumb: within 8mm of the far
   // face or the edge (0.1.80 reached 6mm by letting the skin sink in).
   Check(thumbWorst<.012f,"thumb does not oppose the fingers at the board");
   // Under the board, or against its near edge no higher than the middle.
   if(!shaped)Check(C(fingers,pads[4]-anchor).Y<thickness*.1f,"thumb is not on the opposite (lower) side of the board");
   for(int f=0;f<5;f++)for(int j=1;j<3;j++)Check(Math.Abs(Vector3.Distance(pose[f*3+j-1].Translation,pose[f*3+j].Translation)-Vector3.Distance(rest[f*3+j-1].Translation,rest[f*3+j].Translation))<1e-5f,"finger length changed");
   // 0.1.75: contact alone is not enough. Every finger/thumb bone segment
   // and pad must stay outside the rail slab (the screenshot defect).
   float depth=0;var offset=Vector3.UnitZ*NativeHandMesh.WristZ-anchor;
   for(int c=0;c<5;c++)
   {
    var pts=new List<Vector3>{pose[c*3].Translation,pose[c*3+1].Translation,pose[c*3+2].Translation,pads[c]-Vector3.UnitZ*NativeHandMesh.WristZ};
    for(int k=1;k<pts.Count;k++)for(int u=0;u<=8;u++)
    {
     var q=C(fingers,Vector3.Lerp(pts[k-1],pts[k],u/8f)+offset);
     float edge=shaped?.20f*Math.Abs(q.X):-.008f,mid=shaped?.20f*q.X:0;
     if(q.Z<=edge+.001f||q.Z>=edge+.08f)continue;
     // True depth: distance to the nearest face of the board.
     depth=Math.Max(depth,Math.Min(thickness*.5f-Math.Abs(q.Y-mid),Math.Min(q.Z-edge,edge+.08f-q.Z)));
    }
   }
   // Palm must not sink into the board either.
   for(int f=0;f<4;f++)for(int u=3;u<=10;u++)
   {
    var q=C(fingers,Vector3.Lerp(Vector3.Zero,rest[f*3].Translation,u/10f)-Vector3.UnitY*.012f+offset);
    float edge=shaped?.20f*Math.Abs(q.X):-.008f,mid=shaped?.20f*q.X:0;
    if(q.Z>edge+.001f&&q.Z<edge+.08f)depth=Math.Max(depth,Math.Min(thickness*.5f-Math.Abs(q.Y-mid),Math.Min(q.Z-edge,edge+.08f-q.Z))-.001f);
   }
   // Natural look: fingers lie along the board instead of a raised claw.
   float knuckleHeight=pose.Take(12).Where((m,i)=>i%3==0).Average(m=>C(fingers,m.Translation+offset).Y)-(shaped?0:thickness*.5f);
   Console.WriteLine($"   penetration={depth*1000:F1}mm solve={solveMs}ms knuckles={knuckleHeight*1000:F0}mm");
   if(!shaped)Check(knuckleHeight<.030f,"knuckles raised above the board (claw pinch)");Check(solveMs<1500,"chair grip solve too slow");
   Check(depth<.002f,"finger bones pass through the chair rail by "+(depth*1000).ToString("F1")+"mm");
   var held=fingers.Pose(1,1,profile);Check(ReferenceEquals(pose,held),"trigger/grip causes re-solving or a different pose");
   int revision=fingers.Revision;for(int i=0;i<100;i++)Check(fingers.ChairContact(thickness,surface)==anchor,"stationary rim anchor drifts");
   Check(fingers.Revision==revision,"contact dirties the skin every frame");
   // A fresh visual/outfit may have another thickness; pose and mount must
   // change together, and the next render must see a new revision.
   fingers.ChairContact(thickness+.002f,surface);Check(fingers.Revision>revision&&!ReferenceEquals(pose,fingers.Pose(1,0,profile)),"geometry change reused an incompatible pinch");
   var wrist=Matrix4x4.CreateFromYawPitchRoll(.7f,-.5f,1.2f)*Matrix4x4.CreateTranslation(4,2,-3);
   // Hand root in the chair frame (as WeaponHands.NativeGrip): rail - R*anchor.
   var itemRail=new Vector3(0,-.025f,.073f);var mount=itemRail-C(fingers,anchor);
   var actual=Vector3.Transform(C(fingers,pads[0])+mount,wrist);
   var expected=Vector3.Transform(itemRail+C(fingers,pads[0]-anchor),wrist);
   Check(Vector3.Distance(actual,expected)<.0001f,"grip separates from chair under wrist rotation");cases++;
  }
  RealisticChair();
  Unreachable();
  MeasuredChair(.2f,0);
  // Narrow backrest block (+-44mm, as the 11 of 21 measured rows suggest)
  // with the hand off-centre either way: the 0.1.79 solver fell back to a
  // fist here (one edge finger never found a face under it).
  MeasuredChair(.044f,.02f);MeasuredChair(.044f,-.02f);
  foreach(float tilt in new[]{0f,.2f,-.2f})AshtrayTray(tilt);
  LoggedAshtray(LoggedAshtray0);LoggedAshtray(LoggedAshtray32);
  Console.WriteLine("PASS: "+cases+" mirrored/outfit/rail-thickness fixtures with flat and shaped mesh surfaces; pads contact actual sloped faces inside curved rail edge; bone lengths, trigger/cache stability and rigid attachment. Synthetic rigs, not headset validation.");
 }
 // 0.1.77: whole backrest like the game chair (tall top board, gap, middle
 // rail, stiles, seat), measured through ChairGripSurface from its triangles
 // and checked against the exact boxes: no finger, thumb or palm point inside.
 static void RealisticChair()
 {
  var anchor=new Vector3(0,-.025f,.073f);const float t=.0266f;
  var boxes=new List<(Vector3 min,Vector3 max)>{
   (new(-.20f,-t/2,-.008f),new(.20f,t/2,.12f)),       // top board
   (new(-.20f,-.010f,.17f),new(.20f,.010f,.20f)),     // middle rail
   (new(-.24f,-.015f,-.008f),new(-.20f,.015f,.45f)),  // stiles
   (new(.20f,-.015f,-.008f),new(.24f,.015f,.45f)),
   (new(-.24f,-.015f,.40f),new(.24f,.45f,.43f))};     // seat
  var v=new List<Vector3>();var tri=new List<int>();
  foreach(var (min,max) in boxes)
  {
   int start=v.Count;for(int i=0;i<8;i++)v.Add(anchor+new Vector3((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z));
   foreach(int i in new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5})tri.Add(start+i);
  }
  var surface=ChairGripSurface.Measure(v.ToArray(),tri.ToArray(),anchor);
  Check(surface.Inside(new Vector3(0,0,.05f),out _)&&!surface.Inside(new Vector3(0,.02f,.05f),out _)&&!surface.Inside(new Vector3(0,0,.15f),out _),"real section inside/outside test");
  foreach(bool right in new[]{true,false})foreach(float scale in new[]{.85f,1f,1.15f})
  {
   string side=right?"R":"L";float sign=right?1:-1;
   var names=new List<string>();var rest=new List<Matrix4x4>();
   for(int f=0;f<4;f++)for(int j=0;j<3;j++)
   {
    names.Add($"{side}_Finger_{f+1:00}_{j+1:00}SHJnt");
    float z=(.09f+(f==1?.004f:f==3?-.009f:0)+j*(.030f-f*.0015f))*scale;
    rest.Add(Matrix4x4.CreateRotationZ(.15f*f)*Matrix4x4.CreateTranslation(sign*(-.03f+.02f*f)*scale,0,z));
   }
   for(int j=0;j<3;j++){names.Add($"{side}_Thumb_01_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateRotationY(-sign*.3f)*Matrix4x4.CreateTranslation(sign*(-.05f-j*.003f)*scale,-.012f*scale,(.043f+j*.027f)*scale));}
   var fingers=new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
   var anchorHand=fingers.ChairContact(t,surface);var pose=fingers.Pose(0,0,"prop_wpn_ms_chair");var pads=fingers.RimPads(pose);
   var offset=Vector3.UnitZ*NativeHandMesh.WristZ-anchorHand;
   float Inside(Vector3 q){float d=0;foreach(var (min,max) in boxes)if(q.X>min.X&&q.Y>min.Y&&q.Z>min.Z&&q.X<max.X&&q.Y<max.Y&&q.Z<max.Z)
    d=Math.Max(d,new[]{q.X-min.X,max.X-q.X,q.Y-min.Y,max.Y-q.Y,q.Z-min.Z,max.Z-q.Z}.Min());return d;}
   float depth=0;
   for(int c=0;c<5;c++)
   {
    var pts=new List<Vector3>{pose[c*3].Translation,pose[c*3+1].Translation,pose[c*3+2].Translation,pads[c]-Vector3.UnitZ*NativeHandMesh.WristZ};
    for(int k=1;k<pts.Count;k++)for(int u=0;u<=8;u++)depth=Math.Max(depth,Inside(C(fingers,Vector3.Lerp(pts[k-1],pts[k],u/8f)+offset)));
   }
   for(int f=0;f<4;f++)for(int u=3;u<=10;u++)depth=Math.Max(depth,Inside(C(fingers,Vector3.Lerp(Vector3.Zero,rest[f*3].Translation,u/10f)-Vector3.UnitY*.012f+offset))-.001f);
   float fingerGap=Enumerable.Range(0,4).Max(i=>Math.Abs(C(fingers,pads[i]-anchorHand).Y-t/2));
   var thumb=C(fingers,pads[4]-anchorHand);
   bool thumbHolds=thumb.Y<0&&(Math.Abs(thumb.Y+t/2)<.006f||Math.Abs(thumb.Z+.008f)<.006f);
   float knuckles=Enumerable.Range(0,4).Average(i=>C(fingers,pose[i*3].Translation+offset).Y)-t/2;
   Console.WriteLine($"real chair {right} {scale} penetration={depth*1000:F1}mm fingerGap={fingerGap*1000:F1}mm thumb={thumb} knuckles={knuckles*1000:F0}mm");
   Check(depth<.002f,"real chair: hand passes through the chair by "+(depth*1000).ToString("F1")+"mm");
   Check(fingerGap<.006f,"real chair: fingers do not rest on the backrest");
   Check(thumbHolds,"real chair: thumb does not oppose under/at the edge");
   Check(knuckles<.030f,"real chair: raised claw instead of a hand on the board");
  }
 }
 // 0.1.79: the in-game solve (0.1.77) found no clean pose and left the
 // open rest hand. Any failure must degrade to a closed hand plus a report.
 static void Unreachable()
 {
  var names=new List<string>();var rest=new List<Matrix4x4>();
  for(int f=0;f<4;f++)for(int j=0;j<3;j++){names.Add($"R_Finger_{f+1:00}_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(-.03f+.02f*f,0,.09f+j*.03f));}
  for(int j=0;j<3;j++){names.Add($"R_Thumb_01_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(-.05f,-.012f,.043f+j*.027f));}
  var fingers=new FingerPoseMath(names.ToArray(),rest.ToArray(),true);
  // A 12mm strip: far too short for the fingers to lie on cleanly.
  var anchor=new Vector3(0,-.025f,.073f);var v=new List<Vector3>();var t=new List<int>();
  for(int i=0;i<=20;i++){float x=-.1f+i*.01f;foreach(var q in new[]{new Vector3(x,-.006f,-.008f),new Vector3(x,.006f,-.008f),new Vector3(x,.006f,.004f),new Vector3(x,-.006f,.004f)})v.Add(anchor+q);
   if(i==0)continue;int a=(i-1)*4,b=i*4;for(int j=0;j<4;j++){int k=(j+1)%4;t.AddRange(new[]{a+j,b+j,b+k,a+j,b+k,a+k});}}
  var surface=ChairGripSurface.Measure(v.ToArray(),t.ToArray(),anchor);
  fingers.ChairContact(.012f,surface);var pose=fingers.Pose(0,0,"prop_wpn_ms_chair");
  float curl=0;for(int f=0;f<4;f++)curl+=Vector3.Distance(pose[f*3+2].Translation,rest[f*3+2].Translation);
  Console.WriteLine("unreachable: "+fingers.ChairReport);
  Check(curl>.02f,"no-solution case leaves the open rest hand");
  Check(fingers.ChairReport.Contains("chairSolve=")&&fingers.ChairReport.Contains("touch="),"solver report missing");
 }
 // 0.1.80: section of the game's chair measured in the 0.1.79 log (backrest
 // raked ~18 degrees, rounded 2-5cm thick top block, thinner panel beyond)
 // with the logged finger lengths and pad depths. 0.1.79 found no pose here.
 static void MeasuredChair(float halfWidth,float handOffset)
 {
  var anchor=new Vector3(0,-.025f,.073f);
  var poly=new (float y,float z)[]{(2,-9.2f),(8.5f,-4.2f),(19.4f,10.8f),(29.2f,40.8f),(35,60),(42,80.8f),(55,120),(80,200),(56,200),(40,120),(17.8f,80.8f),(15,65),(-21,55),(-21.1f,40.8f),(-12,10.8f),(-13.3f,-4.2f),(-8,-9.2f)};
  var v=new List<Vector3>();var t=new List<int>();int n=poly.Length;
  foreach(float x in new[]{-halfWidth,halfWidth})foreach(var q in poly)v.Add(anchor+new Vector3(x,q.y/1000,q.z/1000));
  for(int i=0;i<n;i++){int j=(i+1)%n;t.AddRange(new[]{i,n+i,n+j,i,n+j,j});}
  // Caps: the narrow block is a closed solid.
  for(int i=1;i+1<n;i++){t.AddRange(new[]{0,i,i+1});t.AddRange(new[]{n,n+i+1,n+i});}
  var surface=ChairGripSurface.Measure(v.ToArray(),t.ToArray(),anchor);
  // Independent even-odd test against the polygon itself (mm).
  bool InPoly(Vector3 q){float y=q.Y*1000,z=q.Z*1000;if(Math.Abs(q.X)>halfWidth)return false;bool inside=false;
   for(int i=0,j=n-1;i<n;j=i++){var a=poly[i];var b=poly[j];if((a.z>z)!=(b.z>z)&&y<(b.y-a.y)*(z-a.z)/(b.z-a.z)+a.y)inside=!inside;}return inside;}
  foreach(bool right in new[]{true,false})foreach(float s in new[]{.9f,1f,1.1f})
  {
   string side=right?"R":"L";float sign=right?1:-1;
   var names=new List<string>();var rest=new List<Matrix4x4>();
   float[] len={.077f,.084f,.077f,.063f};float[] xs={-.028f,-.008f,.012f,.03f};float[] dz={0,.004f,-.002f,-.012f};
   for(int f=0;f<4;f++)for(int j=0;j<3;j++){names.Add($"{side}_Finger_{f+1:00}_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(sign*(xs[f]*s+handOffset),0,(.101f+dz[f]+j*len[f]*.36f)*s));}
   for(int j=0;j<3;j++){names.Add($"{side}_Thumb_01_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateRotationY(-sign*.3f)*Matrix4x4.CreateTranslation(sign*(-.05f-j*.004f)*s,-.014f*s,(.045f+j*.026f)*s));}
   var fingers=new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
   float[] padDepth={.0063f,.0047f,.0033f,.0049f,.0094f};int c=0;
   foreach(int bone in fingers.RimDistalBones){var tip=rest[bone].Translation+(rest[bone].Translation-rest[bone-1].Translation)*.65f-Vector3.UnitY*padDepth[c++];fingers.SetRimPadCloud(bone,new[]{tip,tip+Vector3.UnitX*.0005f,tip-Vector3.UnitX*.0005f,rest[bone].Translation});}
   var clock=System.Diagnostics.Stopwatch.StartNew();
   var a=fingers.ChairContact(.0266f,surface);long ms=clock.ElapsedMilliseconds;
   var pose=fingers.Pose(0,0,"prop_wpn_ms_chair");var pads=fingers.RimPads(pose);var off=Vector3.UnitZ*NativeHandMesh.WristZ-a;
   float worst=0;int inside=0;
   for(int ch=0;ch<5;ch++){var pts=new List<Vector3>{pose[ch*3].Translation,pose[ch*3+1].Translation,pose[ch*3+2].Translation,pads[ch]-Vector3.UnitZ*NativeHandMesh.WristZ};
    for(int k=1;k<pts.Count;k++)for(int u=0;u<=8;u++){var q=C(fingers,Vector3.Lerp(pts[k-1],pts[k],u/8f)+off);if(InPoly(q)){inside++;surface.Inside(q,out float d);worst=Math.Max(worst,d);}}}
   // Pads resting on the backrest (distance to the measured section), also
   // when the hand sits further up the raked panel.
   float padGap=0;int onFace=0;for(int i=0;i<4;i++){var q=C(fingers,pads[i]-a);if(Math.Abs(q.X)>halfWidth-.004f)continue;float gap=surface.Signed(q,.05f);if(gap>.006f)continue;onFace++;padGap=Math.Max(padGap,Math.Abs(gap));}
   Check(onFace>=3,"measured chair: fingers not on the backrest");
   var thumb=C(fingers,pads[4]-a);
   Console.WriteLine($"measured chair w={halfWidth*2000:F0}mm off={handOffset*1000:F0} {right} {s}: {ms}ms {fingers.ChairReport} pen={worst*1000:F1}mm padGap={padGap*1000:F1}mm thumb={thumb*1000}");
   Check(fingers.ChairReport.Contains("chairSolve=clean"),"measured chair: no clean grip");
   Check(worst<.002f,"measured chair: hand inside the backrest");
   Check(padGap<.005f,"measured chair: fingers not resting on the raked face");
   Check(thumb.Y<.012f,"measured chair: thumb not below/at the edge");
   Check(ms<600,"measured chair: solve too slow");
   // Same chair model picked up again: cached, no second solve.
   var again=ChairGripSurface.Measure(v.ToArray(),t.ToArray(),anchor);clock.Restart();var a2=fingers.ChairContact(.0266f,again);
   Check(Vector3.Distance(a2,a)<1e-6f,"cached chair grip differs");
   Check(clock.ElapsedMilliseconds<20&&fingers.ChairReport.Contains("cached=true"),"identical chair re-solved on pickup");
  }
 }
 // 0.1.81: the game's ashtray is a flat rectangular tray (screenshots:
 // about 230x145mm, a raised frame round a shallow recess, cigarette notches).
 // It is a striking weapon: the whole hand holds the plate by its long edge,
 // fingers flat on the plain bottom, thumb on the frame, nothing inside it.
 static void AshtrayTray(float tilt)
 {
  var anchor=new Vector3(0,-.043f,.075f);
  // Closed boxes (mm, plate frame: bottom y=0, near edge z=0), touching only.
  var boxes=new List<(Vector3 min,Vector3 max)>{
   (new(-115,0,0),new(115,8,145)),                       // base under the recess
   (new(-115,8,0),new(-8,14,22)),(new(8,8,0),new(115,14,22)),(new(-8,8,0),new(8,9.5f,22)), // near frame, notch
   (new(-115,8,123),new(115,14,145)),                    // far frame
   (new(-115,8,22),new(-93,14,123)),(new(93,8,22),new(115,14,123))}; // side frames
  var toPlate=Matrix4x4.CreateTranslation(0,-7,0)*Matrix4x4.CreateScale(.001f)*Matrix4x4.CreateRotationX(tilt)*Matrix4x4.CreateTranslation(anchor);
  Matrix4x4.Invert(toPlate,out var fromFitted);
  var v=new List<Vector3>();var tri=new List<int>();
  foreach(var (min,max) in boxes)
  {
   int start=v.Count;for(int i=0;i<8;i++)v.Add(Vector3.Transform(new Vector3((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z),toPlate));
   foreach(int i in new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5})tri.Add(start+i);
  }
  // The rim contact as WeaponVisual measures it (mid-plane at the near edge).
  var surface=ChairGripSurface.Measure(v.ToArray(),tri.ToArray(),anchor);
  float Depth(Vector3 fitted){var q=Vector3.Transform(fitted+anchor,fromFitted);float d=0;
   foreach(var (min,max) in boxes)if(q.X>min.X&&q.Y>min.Y&&q.Z>min.Z&&q.X<max.X&&q.Y<max.Y&&q.Z<max.Z)
    d=Math.Max(d,new[]{q.X-min.X,max.X-q.X,q.Y-min.Y,max.Y-q.Y,q.Z-min.Z,max.Z-q.Z}.Min());return d/1000;}
  Vector3 Plate(Vector3 fitted)=>Vector3.Transform(fitted+anchor,fromFitted);
  foreach(bool right in new[]{true,false})foreach(float scale in new[]{.9f,1.1f})
  {
   string side=right?"R":"L";float sign=right?1:-1;
   var names=new List<string>();var rest=new List<Matrix4x4>();
   float[] len={.077f,.084f,.077f,.063f};float[] xs={-.028f,-.008f,.012f,.03f};float[] dz={0,.004f,-.002f,-.012f};
   for(int f=0;f<4;f++)for(int j=0;j<3;j++){names.Add($"{side}_Finger_{f+1:00}_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(sign*xs[f]*scale,0,(.101f+dz[f]+j*len[f]*.36f)*scale));}
   for(int j=0;j<3;j++){names.Add($"{side}_Thumb_01_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateRotationY(-sign*.3f)*Matrix4x4.CreateTranslation(sign*(-.05f-j*.004f)*scale,-.014f*scale,(.045f+j*.026f)*scale));}
   var fingers=new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
   float[] padDepth={.0063f,.0047f,.0033f,.0049f,.0094f};int c=0;
   foreach(int bone in fingers.RimDistalBones){var tip=rest[bone].Translation+(rest[bone].Translation-rest[bone-1].Translation)*.65f-Vector3.UnitY*padDepth[c++];fingers.SetRimPadCloud(bone,new[]{tip,tip+Vector3.UnitX*.0005f,tip-Vector3.UnitX*.0005f,rest[bone].Translation});}
   const string profile="prop_wpn_ms_ashtray";
   var clock=System.Diagnostics.Stopwatch.StartNew();
   var a=fingers.ChairContact(profile,.004f,surface);long ms=clock.ElapsedMilliseconds;
   var pose=fingers.Pose(0,0,profile);var pads=fingers.RimPads(pose);var off=Vector3.UnitZ*NativeHandMesh.WristZ-a;
   float depth=0,worstFinger=0;int wrapped=0;
   for(int ch=0;ch<5;ch++)
   {
    var pts=new List<Vector3>{pose[ch*3].Translation,pose[ch*3+1].Translation,pose[ch*3+2].Translation,pads[ch]-Vector3.UnitZ*NativeHandMesh.WristZ};
    for(int k=1;k<pts.Count;k++)for(int u=0;u<=8;u++)depth=Math.Max(depth,Depth(C(fingers,Vector3.Lerp(pts[k-1],pts[k],u/8f)+off)));
    if(ch==4)continue;
    // Whole finger on the plate: at least two phalanges close to it.
    int near=0;float closest=1;
    for(int k=1;k<pts.Count;k++){float g=Math.Abs(surface.Signed(C(fingers,(pts[k-1]+pts[k])*.5f+off),.05f));closest=Math.Min(closest,g);if(g<.012f)near++;}
    if(near>=2)wrapped++;worstFinger=Math.Max(worstFinger,closest);
   }
   for(int f=0;f<4;f++)for(int u=3;u<=10;u++)depth=Math.Max(depth,Depth(C(fingers,Vector3.Lerp(Vector3.Zero,rest[f*3].Translation,u/10f)-Vector3.UnitY*.012f+off))-.001f);
   var thumb=Plate(C(fingers,pads[4]-a));var fingerPads=Enumerable.Range(0,4).Select(i=>Plate(C(fingers,pads[i]-a))).ToArray();
   Console.WriteLine($"ashtray tilt={tilt:F1} {right} {scale}: {ms}ms pen={depth*1000:F1}mm wrapped={wrapped} closest={worstFinger*1000:F1} thumb(plate mm)={thumb} pads y={string.Join(",",fingerPads.Select(p=>p.Y.ToString("F0")))} {fingers.ChairReport.Substring(0,Math.Min(160,fingers.ChairReport.Length))}");
   Check(depth<.002f,"ashtray: hand inside the plate by "+(depth*1000).ToString("F1")+"mm");
   Check(fingers.ChairReport.Contains("flip=True"),"ashtray: fingers not on the plain bottom face");
   Check(fingerPads.All(p=>p.Y<4||p.Z>145),"ashtray: finger pads not under the plate");
   Check(wrapped==4,"ashtray: fingertip pinch instead of whole fingers on the plate");
   Check(thumb.Y>6&&thumb.Z<40,"ashtray: thumb does not hold the top frame near the edge");
   Check(ms<1500,"ashtray: solve too slow");
   // Rim pinch stays available when no sections were measured.
   fingers.RimContact(profile,.004f);Check(!ReferenceEquals(fingers.Pose(0,0,profile),pose),"ashtray: rim fallback not used without a tray surface");
  }
 }
 // 0.1.82: the game's ashtray as logged by 0.1.81 (PROP SECTIONS, x=0 and
 // x=32 cuts: a 2mm pressed shell - flange, raised floor, flange) and the
 // logged hand skeleton (HAND REST). In 0.1.81 the thumb tip went through
 // the thin floor between two sparse samples.
 const string LoggedAshtray0="12.5 20.8 12.8 21.3 12.8 21.3 13.0 21.7 6.4 111.0 6.7 110.5 6.7 110.5 7.0 110.2 8.8 54.7 9.9 39.6 9.9 39.6 11.1 22.3 8.1 64.9 8.8 54.7 7.4 75.0 8.1 64.9 5.2 109.3 6.3 91.0 6.3 91.0 7.4 75.0 11.1 22.3 10.8 21.7 10.8 21.7 10.6 21.3 4.6 110.2 4.8 109.8 4.8 109.8 5.2 109.3 1.3 12.8 7.9 17.5 7.9 17.5 12.5 20.8 -5.9 117.5 1.4 113.7 1.4 113.7 6.4 111.0 0.5 0.0 1.1 0.4 1.1 0.4 1.6 0.7 -7.2 129.5 -7.7 129.7 -7.7 129.7 -8.3 130.0 -0.6 0.5 0.0 0.2 0.0 0.2 0.5 0.0 -9.4 129.4 -8.8 129.7 -8.8 129.7 -8.3 130.0 10.6 21.3 5.9 17.8 5.9 17.8 -1.0 12.8 4.6 110.2 -0.6 113.0 -0.6 113.0 -8.1 117.1 1.1 12.4 1.3 12.8 0.8 11.8 1.1 12.4 -8.6 117.8 -8.4 117.5 -8.9 122.3 -8.6 117.8 -8.4 117.5 -8.1 117.1 -9.4 129.4 -8.9 122.3 -5.9 117.5 -6.1 117.9 -6.1 117.9 -6.5 118.3 11.8 39.4 10.8 54.7 13.0 21.7 11.8 39.4 10.8 54.7 10.0 65.6 10.0 65.6 9.3 75.8 8.3 91.5 7.0 110.2 9.3 75.8 8.3 91.5 -0.6 0.5 -1.0 7.6 -1.0 12.8 -1.2 12.4 -1.0 7.6 -1.3 12.1 -1.2 12.4 -1.3 12.1 0.8 11.8 1.1 7.5 1.1 7.5 1.6 0.7 -6.5 118.3 -6.8 122.7 -6.8 122.7 -7.2 129.5";
 const string LoggedAshtray32="11.7 27.1 11.9 27.5 11.9 27.5 12.2 28.1 5.5 117.4 6.0 116.7 6.0 116.7 6.1 116.5 7.9 61.0 9.5 39.0 9.5 39.0 10.3 28.6 7.5 66.8 7.9 61.0 6.5 81.4 7.5 66.8 4.3 115.7 6.0 90.0 6.0 90.0 6.5 81.4 10.3 28.6 10.1 28.3 10.1 28.3 9.8 27.7 3.7 116.6 3.8 116.4 3.8 116.4 4.3 115.7 0.4 19.1 4.8 22.2 4.8 22.2 11.7 27.1 -6.7 123.9 3.0 118.7 3.0 118.7 5.5 117.4 -0.3 6.4 0.1 6.7 0.1 6.7 0.7 7.0 -8.1 135.9 -8.4 136.0 -8.4 136.0 -9.2 136.4 -1.4 6.9 -1.0 6.7 -1.0 6.7 -0.3 6.4 -10.2 135.8 -9.5 136.2 -9.5 136.2 -9.2 136.4 9.8 27.7 2.6 22.5 2.6 22.5 -1.8 19.2 3.7 116.6 1.2 118.0 1.2 118.0 -9.0 123.5 0.1 18.5 0.4 19.1 -0.0 18.2 0.1 18.5 -9.4 124.2 -9.3 124.0 -9.6 126.7 -9.4 124.2 -9.3 124.0 -9.0 123.5 -10.2 135.8 -9.6 126.7 -6.7 123.9 -6.9 124.1 -6.9 124.1 -7.3 124.7 11.4 38.7 9.9 61.0 12.2 28.1 11.4 38.7 9.9 61.0 9.5 67.3 9.5 67.3 8.5 81.9 7.9 90.5 6.1 116.5 8.5 81.9 7.9 90.5 -1.4 6.9 -1.8 12.0 -1.8 19.2 -2.0 18.9 -1.8 12.0 -2.2 18.5 -2.0 18.9 -2.2 18.5 -0.0 18.2 0.4 11.9 0.4 11.9 0.7 7.0 -7.3 124.7 -7.5 127.2 -7.5 127.2 -8.1 135.9";
 const string LoggedHand="hand=R F:-33.2,7.0,102.5;-37.0,7.8,137.0;-39.6,8.3,160.3;pad=-43.4,2.7,178.7;r=10.6 F:-10.1,2.1,103.8;-12.0,2.5,144.9;-13.3,2.8,170.0;pad=-14.0,-1.7,187.5;r=8.3 F:13.2,-2.8,101.9;13.9,-2.9,141.7;14.3,-3.0,164.6;pad=15.2,-6.3,179.1;r=7.4 F:32.5,-6.9,96.6;33.6,-7.1,129.3;34.2,-7.2,147.9;pad=34.5,-12.2,159.3;r=7.4 T:-41.8,-6.2,41.6;-56.4,-3.1,64.1;-71.0,0.0,86.6;pad=-82.9,-7.1,101.5;r=9.7";
 static void LoggedAshtray(string profileText)
 {
  var nums=profileText.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(v=>float.Parse(v,System.Globalization.CultureInfo.InvariantCulture)/1000).ToArray();
  var anchor=new Vector3(0,-.045f,.091f);var v=new List<Vector3>();var t=new List<int>();
  var segs=new List<(Vector2 a,Vector2 b)>();
  for(int i=0;i+3<nums.Length;i+=4)
  {
   segs.Add((new Vector2(nums[i],nums[i+1]),new Vector2(nums[i+2],nums[i+3])));
   int s=v.Count;
   v.Add(anchor+new Vector3(-.115f,nums[i],nums[i+1]));v.Add(anchor+new Vector3(.115f,nums[i],nums[i+1]));
   v.Add(anchor+new Vector3(.115f,nums[i+2],nums[i+3]));v.Add(anchor+new Vector3(-.115f,nums[i+2],nums[i+3]));
   t.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});
  }
  var surface=ChairGripSurface.Measure(v.ToArray(),t.ToArray(),anchor);
  // Independent inside test and distance on the logged outline itself.
  bool Inside(Vector3 q){int up=0,down=0;foreach(var (a,b) in segs){if((a.Y<=q.Z)==(b.Y<=q.Z))continue;float y=a.X+(b.X-a.X)*(q.Z-a.Y)/(b.Y-a.Y);if(y>q.Y)up++;else down++;}return up%2==1&&down%2==1;}
  float Dist(Vector3 q){float best=1;foreach(var (a,b) in segs){var d=b-a;float u=Math.Clamp(Vector2.Dot(new Vector2(q.Y,q.Z)-a,d)/Math.Max(d.LengthSquared(),1e-12f),0,1);best=Math.Min(best,Vector2.Distance(a+d*u,new Vector2(q.Y,q.Z)));}return best;}
  var names=new List<string>();var rest=new List<Matrix4x4>();var pads=new List<Vector3>();
  int f=0;
  foreach(var part in LoggedHand.Split(' ',StringSplitOptions.RemoveEmptyEntries).Skip(1))
  {
   bool thumbChain=part.StartsWith("T:");var fields=part.Substring(2).Split(';');
   for(int j=0;j<3;j++)
   {
    var c=fields[j].Split(',').Select(x=>float.Parse(x,System.Globalization.CultureInfo.InvariantCulture)/1000).ToArray();
    names.Add(thumbChain?$"R_Thumb_01_{j+1:00}SHJnt":$"R_Finger_{f+1:00}_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(c[0],c[1],c[2]));
   }
   var p=fields[3].Substring(4).Split(',').Select(x=>float.Parse(x,System.Globalization.CultureInfo.InvariantCulture)/1000).ToArray();pads.Add(new Vector3(p[0],p[1],p[2]));
   if(!thumbChain)f++;
  }
  var fingers=new FingerPoseMath(names.ToArray(),rest.ToArray(),true);
  int k=0;foreach(int bone in fingers.RimDistalBones){var pad=pads[k++];fingers.SetRimPadCloud(bone,new[]{pad,pad+Vector3.UnitX*.0005f,pad-Vector3.UnitX*.0005f,rest[bone].Translation});}
  const string profile="prop_wpn_ms_ashtray";var clock=System.Diagnostics.Stopwatch.StartNew();
  var anchorHand=fingers.ChairContact(profile,.004f,surface);long ms=clock.ElapsedMilliseconds;
  var pose=fingers.Pose(0,0,profile);var rimPads=fingers.RimPads(pose);var off=Vector3.UnitZ*NativeHandMesh.WristZ-anchorHand;
  // Every bone line and pad at 1mm spacing: nothing inside the 2mm plate,
  // nothing crossing it.
  int crossings=0;float worst=0;
  for(int ch=0;ch<5;ch++)
  {
   var pts=new List<Vector3>{pose[ch*3].Translation,pose[ch*3+1].Translation,pose[ch*3+2].Translation,rimPads[ch]-Vector3.UnitZ*NativeHandMesh.WristZ};
   for(int s2=1;s2<pts.Count;s2++)
   {
    float len=Vector3.Distance(pts[s2-1],pts[s2]);int n=Math.Max(2,(int)(len/.001f));
    for(int u=0;u<=n;u++)
    {
     var q=C(fingers,Vector3.Lerp(pts[s2-1],pts[s2],u/(float)n)+off);
     if(Inside(q)){crossings++;worst=Math.Max(worst,Dist(q));}
    }
   }
  }
  // 0.1.83 look: thumb laid forward (not splayed), palm not far onto the plate.
  var thumbDir=Vector3.Normalize(rimPads[4]-Vector3.UnitZ*NativeHandMesh.WristZ-pose[13].Translation);
  Console.WriteLine($"logged ashtray thumbForward={thumbDir.Z:F2}");
  Check(thumbDir.Z>.5f,"logged ashtray: thumb splayed sideways");
  Check(!fingers.ChairReport.Contains("back=52")&&!fingers.ChairReport.Contains("back=68"),"logged ashtray: palm pushed far onto the plate");
  Console.WriteLine($"logged ashtray: {ms}ms inside={crossings} worst={worst*1000:F1}mm {fingers.ChairReport.Substring(0,Math.Min(200,fingers.ChairReport.Length))}");
  Check(crossings==0,"logged ashtray: finger/thumb bone line inside the 2mm plate ("+crossings+" samples)");
  Check(fingers.ChairReport.Contains("touch=4/4"),"logged ashtray: fingers not on the plate");
 }
 static ChairGripSurface MakeSurface(float thickness)
 {
  var v=new List<Vector3>();var t=new List<int>();var anchor=new Vector3(0,-.025f,.073f);
  for(int i=0;i<=20;i++)
  {
   float x=-.1f+i*.01f,y=.20f*x,z=.20f*Math.Abs(x);
   v.Add(anchor+new Vector3(x,y-thickness*.5f,z));v.Add(anchor+new Vector3(x,y+thickness*.5f,z));
   v.Add(anchor+new Vector3(x,y+thickness*.5f,z+.08f));v.Add(anchor+new Vector3(x,y-thickness*.5f,z+.08f));
   if(i==0)continue;int a=(i-1)*4,b=i*4;
   for(int j=0;j<4;j++){int k=(j+1)%4;t.AddRange(new[]{a+j,b+j,b+k,a+j,b+k,a+k});}
  }
  // Another chair part projects behind the selected rail at the same X.
  // Global min-Z sampling used to bind all fingertips to this remote face.
  int start=v.Count;
  for(int i=0;i<8;i++)v.Add(anchor+new Vector3((i&1)==0?-.10f:.10f,(i&2)==0?.18f:.206f,(i&4)==0?-.03f:.01f));
  foreach(int i in new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5})t.Add(start+i);
  var s=ChairGripSurface.Measure(v.ToArray(),t.ToArray(),anchor);Check(s.Count>=17,"chair sections missing");
  for(int i=0;i<9;i++)Check(Math.Abs(s.Pad(-.04f+i*.01f,false).Y)<.04f,"chair pads target a different part 18cm from the backrest");
  return s;
 }
}
