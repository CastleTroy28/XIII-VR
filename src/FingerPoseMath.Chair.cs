using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace XiiiXR;
internal sealed partial class FingerPoseMath
{
 private Matrix4x4[]? chairPose;private Vector3 chairContact;
 private float chairThickness=.03f;private int chairPadCount=-1,chairRevision;
 // Measured surface, and the side currently being solved (maybe mirrored).
 private ChairGripSurface? chairSurface,surf;
 private bool chairBothSides,ashtrayTray;
 internal float ChairContactError {get;private set;}
 internal static bool ChairProfile(string profile)=>profile.Contains("chair")||profile.Contains("stool");
 // Whole-hand tray grip: chairs, and (0.1.81) the ashtray when its mesh
 // sections were measured. A struck-with object is held in the whole hand,
 // not pinched with fingertips.
 internal static bool TrayProfile(string profile)=>ChairProfile(profile)||profile.Contains("ashtray");
 internal Vector3 ChairContact(float thickness,ChairGripSurface? surface=null)=>ChairContact("prop_wpn_ms_chair",thickness,surface);
 internal Vector3 ChairContact(string profile,float thickness,ChairGripSurface? surface)
 {
  thickness=float.IsFinite(thickness)?Math.Clamp(thickness,.004f,.065f):.03f;
  bool ashtray=profile.Contains("ashtray");
  if(Math.Abs(thickness-chairThickness)>.0001f||chairPadCount!=rimRevision||!ReferenceEquals(chairSurface,surface)||chairBothSides!=ashtray||ashtray&&!ashtrayTray)
  {chairThickness=thickness;chairPadCount=rimRevision;chairSurface=surface;surf=surface;chairBothSides=ashtray;if(ashtray)ashtrayTray=true;chairPose=null;chairRevision++;}
  _=ChairPose();return chairContact;
 }
 // The ashtray falls back to the rim pinch when no sections were measured.
 private bool AshtrayTray(string profile)=>ashtrayTray&&profile.Contains("ashtray");
 private void LeaveAshtrayTray(){if(ashtrayTray){ashtrayTray=false;chairRevision++;}}
 // Every chair of a level shares one model: reuse the solved grip for an
 // identical measured surface instead of solving again on each pickup.
 private readonly Dictionary<string,(Matrix4x4[] pose,Vector3 contact,Quaternion rotation,float error,float depth,string report)> chairCache=new();
 private string ChairSignature()
 {
  // 0.1.153: the measured finger pads themselves, not the count of their
  // changes (a key pinch between two ashtrays changed it, and the 290 ms solve
  // ran again: a 324 ms freeze on picking the ashtray up).
  var b=new System.Text.StringBuilder();b.Append("v153|").Append(chairBothSides).Append('|').Append(chairThickness.ToString("F4"));
  foreach(var pad in rimPads.OrderBy(p=>p.Key))b.Append("|p").Append(pad.Key).Append(':').Append(pad.Value.X.ToString("F4")).Append(',').Append(pad.Value.Y.ToString("F4")).Append(',').Append(pad.Value.Z.ToString("F4"));
  foreach(var r in skinRadius.OrderBy(p=>p.Key))b.Append('|').Append(r.Value.ToString("F4"));
  // The hand itself (the cache is shared by hands and kept on disk).
  foreach(var chain in fingers.Append(thumb))foreach(int i in chain){var t=rest[i].Translation;b.Append('|').Append(t.X.ToString("F4")).Append(',').Append(t.Y.ToString("F4")).Append(',').Append(t.Z.ToString("F4"));}
  if(chairSurface==null)return b.ToString();
  float edge=Edge(0);b.Append('|').Append(chairSurface.Count).Append('|').Append(edge.ToString("F4"));
  foreach(float x in new[]{-.03f,0f,.03f})foreach(float d in new[]{.005f,.02f,.05f,.09f})
   b.Append('|').Append(Top(x,edge+d,out float y)?y.ToString("F4"):"-").Append(Bottom(x,edge+d,out y)?y.ToString("F4"):"-");
  return b.ToString();
 }
 private sealed class TraySolution
 {
  public float Score=float.PositiveInfinity,Error=float.PositiveInfinity,Depth=float.PositiveInfinity,ThumbGap=float.PositiveInfinity;
  public Matrix4x4[] Pose=Array.Empty<Matrix4x4>();public Vector3 Center;public Matrix4x4 Rotation=Matrix4x4.Identity;
  public int Touching,Placements,PalmMiss,Candidates;public float Rake,Back,Roughness;public bool Flip;
 }
 private Matrix4x4[] ChairPose()
 {
  if(chairPose!=null)return chairPose;
  surf=chairSurface;
  string signature=ChairSignature();
  // 0.1.96: also a cache on disk, so the first pickup of a chair/ashtray
  // in a later session does not stall the frame (97-140 ms solves).
  if(!chairCache.ContainsKey(signature)&&ChairSolveCache.TryGet(signature,out var stored))chairCache[signature]=stored;
  if(chairCache.TryGetValue(signature,out var cached))
  {
   chairContact=cached.contact;ChairRotation=cached.rotation;ChairContactError=cached.error;ChairPenetrationDepth=cached.depth;
   ChairReport=cached.report+" cached=true";chairPose=cached.pose;return chairPose;
  }
  var solveWatch=System.Diagnostics.Stopwatch.StartNew();
  float baseZ=fingers.Average(c=>rest[c[0]].Translation.Z);
  float palmY=fingers.Average(c=>rest[c[0]].Translation.Y);
  var report=new System.Text.StringBuilder();
  foreach(var chain in fingers.Append(thumb))
  {
   var a=rest[chain[1]].Translation;var b=rest[chain[2]].Translation;var pad=Vector3.Transform(RimLocalPad(chain),rest[chain[2]]);
   var axis=b-a;float depth=axis.LengthSquared()>1e-12f?Vector3.Cross(Vector3.Normalize(axis),pad-b).Length():.005f;
   report.Append(chain==thumb?"T":"F").Append((Vector3.Distance(pad,rest[chain[0]].Translation)*1000).ToString("F0")).Append('/').Append((depth*1000).ToString("F1"))
    .Append("/r").Append((ChainRadius(chain)[2]*1000).ToString("F1")).Append(' ');
  }
  report.Append("palmY=").Append((palmY*1000).ToString("F0")).Append(" knuckleZ=").Append((baseZ*1000).ToString("F0"));
  report.Append(" edge0=").Append((Edge(0)*1000).ToString("F1"));
  foreach(float d in new[]{.005f,.02f,.05f,.09f})
   report.Append(Top(0,Edge(0)+d,out float hi)&&Bottom(0,Edge(0)+d,out float lo)?$" face{d*1000:F0}=[{lo*1000:F1},{hi*1000:F1}]":$" face{d*1000:F0}=none");
  report.Append(" knuckleX=[").Append(string.Join(",",fingers.Select(c=>(rest[c[0]].Translation.X*1000).ToString("F0")))).Append("] rail=[")
   .Append(chairSurface==null?"flat":$"{chairSurface.MinX*1000:F0}..{chairSurface.MaxX*1000:F0}").Append(']');
  // Tray grip. The board (chair backrest, ashtray plate) lies under the palm
  // and runs forward under the fingers; its near edge sits in the web
  // between thumb and index finger; the thumb holds the opposite face.
  // 0.1.81: every finger/thumb/palm point is a capsule tested against the
  // measured sections (signed distance), never allowed inside. The palm is
  // lowered onto the board until it touches; each finger then takes the
  // flexion that lays all three phalanges closest to the surface without
  // entering it (0.1.80 aimed pads at a face first and, when that failed,
  // accepted the least-bad pose: 20mm inside the chair in the log).
  var sides=new List<(ChairGripSurface? surface,bool flip)>{(chairSurface,false)};
  if(chairBothSides&&chairSurface!=null)sides.Add((chairSurface.Flipped(),true));
  TraySolution? best=null;var tried=new List<TraySolution>();
  foreach(var (surface,flip) in sides)
  {
   surf=surface;var solved=SolveTray(flip,baseZ,palmY);tried.Add(solved);
   if(solved.Pose.Length>0&&(best==null||solved.Score<best.Score))best=solved;
  }
  surf=chairSurface;
  string mode;
  if(best==null)
  {
   // Last resort: a closed hand around the rail, never an open palm.
   var fist=(Matrix4x4[])rest.Clone();
   foreach(var chain in fingers)Bend(fist,chain,.88f,true,false);
   Bend(fist,thumb,.85f,true,true);
   var middle=fingers.Aggregate(Vector3.Zero,(sum,c)=>sum+(Vector3.Transform(RimLocalPad(c),fist[c[2]])+fist[c[0]].Translation)*.5f)/fingers.Count;
   chairContact=middle+Vector3.UnitZ*NativeHandMesh.WristZ;mode="fistFallback";ChairRotation=Quaternion.Identity;
   ChairContactError=float.PositiveInfinity;ChairPenetrationDepth=float.PositiveInfinity;best=new TraySolution{Pose=fist};
  }
  else
  {
   mode=best.Depth<=.001f&&best.Touching==fingers.Count&&best.ThumbGap<.004f?"clean":"partial";
   chairContact=best.Center+Vector3.UnitZ*NativeHandMesh.WristZ;ChairRotation=Quaternion.CreateFromRotationMatrix(best.Rotation);
   ChairContactError=best.Error;ChairPenetrationDepth=best.Depth;
  }
  var sideText=string.Join(" ",tried.Select(t=>$"{(t.Flip?"flip":"side")}:{(float.IsFinite(t.Score)?(t.Score*1000).ToString("F1"):"none")}/p{t.Placements}/m{t.PalmMiss}/c{t.Candidates}/rough{t.Roughness*1000:F1}"));
  ChairReport=$"chairSolve={mode} solveMs={solveWatch.Elapsed.TotalMilliseconds:F0} flip={best.Flip} touch={best.Touching}/{fingers.Count} thumbGapMm={(float.IsFinite(best.ThumbGap)?(best.ThumbGap*1000).ToString("F1"):"none")} back={best.Back*1000:F0} rake={best.Rake*57.3f:F1} {sideText} {report}";
  if(chairCache.Count>=8)chairCache.Clear();
  chairCache[signature]=(best.Pose,chairContact,ChairRotation,ChairContactError,ChairPenetrationDepth,ChairReport);
  ChairSolveCache.Put(signature,chairCache[signature]);
  chairPose=best.Pose;return chairPose;
 }
 internal string ChairReport {get;private set;}="chairSolve=none";
 // Canonical rest skeleton and skin pads (mm), for offline reproduction.
 internal string RestReport()
 {
  var b=new System.Text.StringBuilder("hand=").Append(rightHand?"R":"L");
  static string V(Vector3 v)=>$"{v.X*1000:F1},{v.Y*1000:F1},{v.Z*1000:F1}";
  foreach(var chain in fingers.Append(thumb))
  {
   b.Append(chain==thumb?" T:":" F:");
   foreach(int bone in chain)b.Append(V(rest[bone].Translation)).Append(';');
   b.Append("pad=").Append(V(Vector3.Transform(RimLocalPad(chain),rest[chain[2]]))).Append(";r=").Append((ChainRadius(chain)[2]*1000).ToString("F1"));
  }
  return b.ToString();
 }
 // Rotation of the hand frame into the prop's fitted frame for the grip.
 internal Quaternion ChairRotation {get;private set;}=Quaternion.Identity;
 private Matrix4x4 chairR=Matrix4x4.Identity,chairRt=Matrix4x4.Identity;
 private Vector3 ToChair(Vector3 p,Vector3 center)=>Vector3.Transform(p-center,chairR);
 private Vector3 FromChair(Vector3 q,Vector3 center)=>center+Vector3.Transform(q,chairRt);
 private TraySolution SolveTray(bool flip,float baseZ,float palmY)
 {
  var result=new TraySolution{Flip=flip};
  float rake=EstimateRake(out float roughness);result.Rake=rake;result.Roughness=roughness;
  // Centre the four fingers over the measured part of the rail (0.1.80).
  float handX=fingers.Average(c=>rest[c[0]].Translation.X);
  float railX=surf==null?0:(surf.MinX+surf.MaxX)*.5f;
  var knuckle=new Vector3(handX,palmY,baseZ);var palm=PalmProbes();
  var flipM=flip?Matrix4x4.CreateRotationZ(MathF.PI):Matrix4x4.Identity;
  var candidates=new List<(float score,Vector3 center,Matrix4x4[] pose,Matrix4x4 rotation,int touching,float gap,float back)>();
  var thumbed=new HashSet<int>();int group=0;
  foreach(var (tilt,shift) in new[]{(rake,0f),(rake,-.012f),(rake,.012f),(rake-.12f,0f),(rake+.12f,0f)})
  {
   // Other tilts/offsets only while the measured rake gave no clean grip.
   if(group++>0&&result.Depth<=.001f&&result.Touching==fingers.Count&&result.ThumbGap<.004f)break;
   chairR=Matrix4x4.CreateRotationX(-tilt);Matrix4x4.Invert(chairR,out chairRt);
   float railCenter=railX+shift,edge=Edge(railCenter);
   // 0.1.88: the 0.1.83 palm placement again (the 0.1.85 edge pinch left
   // the little finger up and the thumb curled).
   foreach(float back in new[]{.012f,.025f,.038f,.052f,.068f})
   {
    result.Placements++;
    if(!LowerPalm(knuckle,new Vector3(railCenter,0,edge+back),palm,out var center)){result.PalmMiss++;continue;}
    var trial=(Matrix4x4[])rest.Clone();float score=0,worst=0;int touching=0;
    foreach(var chain in fingers){var (s,g)=CloseFinger(trial,chain,center);score+=s;worst=Math.Max(worst,g);if(g<.0025f)touching++;}
    float depth=ChairPenetration(trial,center,false);
    // The ashtray is held nearer its edge (palm less far onto it).
    score+=depth*20+(fingers.Count-touching)*.006f+(chairBothSides?Math.Abs(back-.018f)*.25f:Math.Abs(back-.038f)*.03f)+Math.Abs(tilt-rake)*.01f+Math.Abs(shift)*.05f;
    candidates.Add((score,center,trial,chairR,touching,worst,back));
   }
   result.Candidates=candidates.Count;
   // Thumb only for the best few finger placements not tried yet.
   foreach(int index in Enumerable.Range(0,candidates.Count).Where(i=>!thumbed.Contains(i)).OrderBy(i=>candidates[i].score).Take(3).ToArray())
   {
    thumbed.Add(index);var c=candidates[index];chairR=c.rotation;Matrix4x4.Invert(chairR,out chairRt);
    var (pose,thumbScore,thumbGap)=PlaceThumb(c.pose,c.center);
    float depth=ChairPenetration(pose,c.center,true);
    float total=c.score+thumbScore+depth*20+roughness*10;
    if(!float.IsFinite(total)||total>=result.Score)continue;
    result.Score=total;result.Pose=pose;result.Center=c.center;result.Rotation=c.rotation*flipM;result.Depth=depth;
    result.Touching=c.touching;result.ThumbGap=thumbGap;result.Error=Math.Max(c.gap,thumbGap);result.Back=c.back;
   }
  }
  return result;
 }
 // Slope of the finger-side face along the hand's forward axis, from the
 // measured sections under the fingers (15-80mm past the edge), and how far
 // the face departs from that plane (a dished or notched face is rough).
 private float EstimateRake(out float roughness)
 {
  roughness=0;if(surf==null)return 0;
  float slope=0;
  foreach(float x in new[]{0f,-.02f,.02f})
  {
   float n=0,sz=0,sy=0,szz=0,szy=0,edge=Edge(x);var points=new List<(float d,float y)>();
   for(float d=.015f;d<=.0801f;d+=.005f)if(Top(x,edge+d,out float y)){n++;sz+=d;sy+=y;szz+=d*d;szy+=d*y;points.Add((d,y));}
   if(n<3)continue;float den=n*szz-sz*sz;if(Math.Abs(den)<1e-9f)continue;
   float k=(n*szy-sz*sy)/den,a=(sy-k*sz)/n;if(!float.IsFinite(k))continue;
   if(x==0)slope=k;
   roughness=Math.Max(roughness,MathF.Sqrt(points.Average(p=>(p.y-a-k*p.d)*(p.y-a-k*p.d))));
  }
  return Math.Clamp(MathF.Atan(slope),-.6f,.6f);
 }
 // Skin radius of each finger, from the measured distal skin patch when
 // available (outfits differ), proximal phalanges a little thicker.
 private readonly Dictionary<int,float> skinRadius=new();
 private float[] ChainRadius(int[] chain)
 {
  bool isThumb=chain==thumb||chain[0]==thumb[0];
  float length=Vector3.Distance(rest[chain[0]].Translation,rest[chain[1]].Translation)+Vector3.Distance(rest[chain[1]].Translation,rest[chain[2]].Translation)
   +Vector3.Distance(rest[chain[2]].Translation,Vector3.Transform(RimLocalPad(chain),rest[chain[2]]));
  float scale=Math.Clamp(length/(isThumb?.07f:.08f),.8f,1.25f);
  float distal=skinRadius.TryGetValue(chain[2],out float measured)?measured:.0062f*scale;
  // Fingertip: a capsule along the distal bone whose palm side is the
  // measured pad, so the whole tip (nail side and end) has volume.
  var (_,offset)=DistalPad(chain);float tip=Math.Clamp(offset,.004f,distal);
  // 0.1.85: a 1.5mm safety skin on the 2mm ashtray shell (the rendered,
  // skinned thumb bulged through it where the capsules just touched).
  float m=chairBothSides?.0008f:0;
  return isThumb?new[]{distal*1.45f+m,distal*1.2f+m,distal+m,tip+m}:new[]{distal*1.3f+m,distal*1.15f+m,distal+m,tip+m};
 }
 // Pad position along and off the distal axis, at rest.
 private (float along,float offset) DistalPad(int[] chain)
 {
  var dip=rest[chain[2]].Translation;var dir=dip-rest[chain[1]].Translation;
  if(dir.LengthSquared()<1e-10f)return (.012f,.005f);dir=Vector3.Normalize(dir);
  var v=Vector3.Transform(RimLocalPad(chain),rest[chain[2]])-dip;float along=Vector3.Dot(v,dir);
  return (Math.Max(along,.006f),(v-dir*along).Length());
 }
 private void MeasureSkinRadius(int[] chain,Vector3[] cloud)
 {
  int bone=chain[2];var origin=rest[bone].Translation;var axis=origin-rest[chain[1]].Translation;
  if(axis.LengthSquared()<1e-10f)return;axis=Vector3.Normalize(axis);
  var finite=cloud.Where(NativeHandMesh.Finite).ToArray();if(finite.Length<8)return;
  float tip=finite.Max(q=>Vector3.Dot(q-origin,axis));if(!(tip>.004f))return;
  var d=new List<float>();
  foreach(var q in finite){var v=q-origin;float t=Vector3.Dot(v,axis);if(t<tip*.15f||t>tip*.7f)continue;d.Add((v-axis*t).Length());}
  if(d.Count<6)return;d.Sort();
  skinRadius[bone]=Math.Clamp(d[(int)(d.Count*.7f)],.004f,.0085f);
 }
 // Palm underside: points between wrist and knuckles, 13mm below the bones.
 private List<(Vector3 p,float r)> PalmProbes()
 {
  var probes=new List<(Vector3,float)>();
  // The knuckle row carries less flesh (10mm) than the middle of the palm.
  foreach(var chain in fingers)foreach(float u in new[]{.35f,.55f,.75f,.95f})
   probes.Add((Vector3.Lerp(Vector3.Zero,rest[chain[0]].Translation,u)-Vector3.UnitY*(u>.9f?.003f:.006f),.007f));
  return probes;
 }
 private float Signed(Vector3 q,float reach)
 {
  if(surf!=null)return surf.Signed(q,reach);
  // Flat board `chairThickness` thick, 8cm deep.
  float half=chairThickness*.5f,dy=Math.Abs(q.Y)-half,dz=Math.Max(-.008f-q.Z,q.Z-.072f);
  if(dy<0&&dz<0)return Math.Max(dy,dz);
  float oy=Math.Max(dy,0),oz=Math.Max(dz,0);return Math.Min(reach,MathF.Sqrt(oy*oy+oz*oz));
 }
 // Put the knuckle line over `target` (chair frame) and lower the hand onto
 // the board along the chair's Y until the palm touches it.
 private bool LowerPalm(Vector3 knuckle,Vector3 target,List<(Vector3 p,float r)> probes,out Vector3 center)
 {
  Vector3 At(float h)=>knuckle-Vector3.Transform(target+new Vector3(0,h,0),chairRt);
  bool Clear(float h){var c=At(h);foreach(var (p,r) in probes)if(Signed(ToChair(p,c),r+.01f)<r)return false;return true;}
  float lastClear=float.NaN;
  for(float h=.10f;h>=-.06f;h-=.002f)
  {
   if(Clear(h)){lastClear=h;continue;}
   if(float.IsNaN(lastClear))continue;
   float lo=h,hi=lastClear;
   for(int i=0;i<7;i++){float mid=(lo+hi)*.5f;if(Clear(mid))hi=mid;else lo=mid;}
   center=At(hi+.0003f);return true;
  }
  center=default;return false;
 }
 // Capsule samples of one finger/thumb: (point, skin radius). The distal
 // capsule runs to the measured pad (on the skin) and a little past it.
 // 0.1.82: dense samples (3.5mm along phalanges, 2mm along the tip) so a
 // 2mm shell (the ashtray) cannot slip between two samples, and the tip is
 // a capsule along the distal bone, not a thin line to the pad.
 private void ChainSamples(Matrix4x4[] pose,int[] chain,float[] radius,List<(Vector3 p,float r,int segment)> output,bool dense=true)
 {
  float step=dense?.0035f:.007f,tipStep=dense?.002f:.004f;
  output.Clear();
  for(int j=0;j<2;j++)
  {
   var a=pose[chain[j]].Translation;var b=pose[chain[j+1]].Translation;
   int n=Math.Clamp((int)MathF.Ceiling(Vector3.Distance(a,b)/step),3,24);
   for(int k=1;k<=n;k++){float u=k/(float)n;output.Add((Vector3.Lerp(a,b,u),radius[j]+(radius[j+1]-radius[j])*u,j));}
  }
  var dip=pose[chain[2]].Translation;var restDir=rest[chain[2]].Translation-rest[chain[1]].Translation;
  Matrix4x4.Invert(rest[chain[2]],out var inverse);
  var dir=Vector3.TransformNormal(Vector3.TransformNormal(restDir,inverse),pose[chain[2]]);
  if(dir.LengthSquared()<1e-12f)dir=pose[chain[2]].Translation-pose[chain[1]].Translation;dir=Vector3.Normalize(dir);
  var (along,_)=DistalPad(chain);float tip=radius[3];
  var end=dip+dir*(along+tip*.4f);int m=Math.Clamp((int)MathF.Ceiling((along+tip*.4f)/tipStep),3,24);
  for(int k=1;k<=m;k++){float u=k/(float)m;output.Add((Vector3.Lerp(dip,end,u),radius[2]+(tip-radius[2])*u,2));}
  // The measured pad can sit outside that capsule (thumb: 9mm off its
  // bone): the skin line from the joint to the pad must not cross a thin
  // shell either.
  var padPoint=Vector3.Transform(RimLocalPad(chain),pose[chain[2]]);
  int l=Math.Clamp((int)MathF.Ceiling(Vector3.Distance(dip,padPoint)/tipStep),3,16);
  for(int k=l/2;k<=l;k++)output.Add((Vector3.Lerp(dip,padPoint,k/(float)l),.001f,2));
 }
 private readonly List<(Vector3 p,float r,int segment)> samples=new();
 // Flex one finger (MCP, PIP, DIP) so its three phalanges lie as close to
 // the surface as possible without entering it. Direct search over natural
 // flexion (slightly raised knuckle allowed), then a local refinement.
 private (float score,float gap) CloseFinger(Matrix4x4[] pose,int[] chain,Vector3 center)
 {
  var work=(Matrix4x4[])pose.Clone();var radius=ChainRadius(chain);var gaps=new float[3];
  float Eval(float a0,float a1,float a2,bool full,bool dense=false)
  {
   Flex(pose,work,chain,a0,a1,a2);ChainSamples(work,chain,radius,samples,dense);
   float clear=float.PositiveInfinity;gaps[0]=gaps[1]=gaps[2]=.02f;
   foreach(var (p,r,segment) in samples)
   {
    float d=Signed(ToChair(p,center),r+.02f)-r;clear=Math.Min(clear,d);
    if(!full&&d<-.0004f)return d;
    gaps[segment]=Math.Min(gaps[segment],Math.Max(0,d));
   }
   return clear;
  }
  // 0.1.88: a finger that cannot reach the surface (past the end of the
  // ashtray) curls in a relaxed fist instead of sticking out straight.
  float Score(float a0,float a1,float a2)
  {
   float g=Math.Min(gaps[0],Math.Min(gaps[1],gaps[2]));
   float rest=g>.012f?(Math.Abs(a0-.95f)+Math.Abs(a1-1.15f)+Math.Abs(a2-.85f))*.006f:(Math.Abs(a0-.2f)+Math.Abs(a1-.3f)+Math.Abs(a2-.25f))*.002f;
   return gaps[2]+gaps[1]*.8f+gaps[0]*.6f+rest;
  }
  var coarse=new List<(float score,float a0,float a1,float a2)>();
  // PIP/DIP may straighten slightly past the rest line, as fingers laid
  // flat on a board do.
  for(float a0=-.5f;a0<=1.51f;a0+=.15f)for(float a1=-.2f;a1<=1.61f;a1+=.22f)foreach(float hook in new[]{-.15f,0f,.45f})
  {
   float a2=Math.Clamp(a1*.7f+hook,-.15f,1.4f);
   if(Eval(a0,a1,a2,false)<-.0004f)continue;coarse.Add((Score(a0,a1,a2),a0,a1,a2));
  }
  // Search on sparse samples, then accept the best pose that also clears
  // the dense check (a thin shell can hide between sparse samples).
  var found=new List<(float score,float a0,float a1,float a2)>(coarse);
  foreach(var c in coarse.OrderBy(c=>c.score).Take(4))
   foreach(float d0 in new[]{-.06f,-.03f,0f,.03f,.06f})foreach(float d1 in new[]{-.08f,0f,.08f})foreach(float d2 in new[]{-.08f,0f,.08f})
   {
    float a0=c.a0+d0,a1=Math.Max(-.25f,c.a1+d1),a2=Math.Clamp(c.a2+d2,-.15f,1.5f);
    if(Eval(a0,a1,a2,false)<-.0004f)continue;found.Add((Score(a0,a1,a2),a0,a1,a2));
   }
  int tried=0;
  foreach(var c in found.OrderBy(c=>c.score))
  {
   if(++tried>40)break;
   if(Eval(c.a0,c.a1,c.a2,false,true)<-.0004f)continue;
   Eval(c.a0,c.a1,c.a2,true,true);
   foreach(int k in chain)pose[k]=work[k];return (Score(c.a0,c.a1,c.a2),Math.Min(gaps[0],Math.Min(gaps[1],gaps[2])));
  }
  // Nothing clear (the surface rises in front of the knuckles): the pose
  // that stays out the most, reported as penetration.
  float most=float.NegativeInfinity;(float a0,float a1,float a2) least=(0,0,0);
  for(float a0=-.9f;a0<=1.51f;a0+=.15f)for(float a1=0;a1<=1.61f;a1+=.2f){float a2=a1*.7f;float c=Eval(a0,a1,a2,true,true);if(c>most){most=c;least=(a0,a1,a2);}}
  Eval(least.a0,least.a1,least.a2,true,true);foreach(int k in chain)pose[k]=work[k];
  return (.05f-most*20,.02f);
 }
 // Thumb on the opposite face near the edge (thin board), or against the
 // edge/outer wall (thick board). 0.1.81: a sampled search over the thumb's
 // own joints (CMC two ways, MCP, IP) that never enters the board; the CCD
 // reach used before cut straight through the edge.
 private float forwardThumb=.012f;
 private (Matrix4x4[] pose,float score,float gap) PlaceThumb(Matrix4x4[] basePose,Vector3 center)
 {
  var radius=ChainRadius(thumb);var list=new List<(Vector3 p,float r,int segment)>();var local=RimLocalPad(thumb);
  var indexPad=ToChair(Vector3.Transform(RimLocalPad(fingers[indexFinger]),basePose[fingers[indexFinger][2]]),center);
  var thumbRoot=ToChair(rest[thumb[0]].Translation,center);
  var targets=new List<(Vector3 p,float bias)>();
  foreach(float mix in new[]{0f,.35f,.7f})
  {
   float x=indexPad.X+(thumbRoot.X-indexPad.X)*mix;float edge=Edge(x);
   // Thin board: thumb pad on the opposite face, a little inside the edge.
   foreach(float inset in new[]{.012f,.022f,.034f,.048f})
    if(Bottom(x,edge+inset,out float y))targets.Add((FromChair(new Vector3(x,y-.0015f,edge+inset),center),Math.Abs(inset-.022f)*.01f+mix*.001f));
   // Thick board: thumb against the edge / outer wall below the top.
   if(Top(x,edge+.004f,out float high)&&Bottom(x,edge+.004f,out float low))
    // (Prefer the opposite face when the thumb can reach it.)
    foreach(float f in new[]{.2f,.4f})targets.Add((FromChair(new Vector3(x,low+(high-low)*f,edge-.0015f),center),.006f+Math.Abs(f-.2f)*.005f));
  }
  var work=(Matrix4x4[])basePose.Clone();
  // q: CMC toward index (0), across (1), twist (2), MCP (3), IP (4).
  float Eval(float[] q,bool full,out float gap,out float error,bool dense=false)
  {
   ThumbFlex(basePose,work,q);ChainSamples(work,thumb,radius,list,dense);
   float clear=float.PositiveInfinity;gap=.03f;error=.05f;
   foreach(var (p,r,_) in list)
   {
    float dist=Signed(ToChair(p,center),r+.03f)-r;clear=Math.Min(clear,dist);
    if(!full&&dist<-.0004f)return dist;gap=Math.Min(gap,Math.Max(0,dist));
   }
   var pad=Vector3.Transform(local,work[thumb[2]]);
   foreach(var (t,bias) in targets)error=Math.Min(error,Vector3.Distance(pad,t)+bias);
   // A thumb beside the fingers on their own face does not hold anything.
   var qp=ToChair(pad,center);if(qp.Z>Edge(qp.X)+.004f&&Top(qp.X,qp.Z,out float fingerFace)&&qp.Y>fingerFace-.002f)error+=.02f;
   // 0.1.83: a thumb laid forward along the board, not splayed sideways
   // (ashtray screenshots).
   var thumbDir=pad-work[thumb[1]].Translation;
   if(thumbDir.LengthSquared()>1e-8f)error+=(1-Vector3.Dot(Vector3.Normalize(thumbDir),Vector3.UnitZ))*(chairBothSides?forwardThumb:0);
   // Resting against the board (pad first) beats floating free of it.
   error+=Math.Max(0,Signed(ToChair(pad,center),.03f))*.4f+gap*.3f+(Math.Abs(q[0]-.3f)+Math.Abs(q[1])+Math.Abs(q[2])+Math.Abs(q[3])+Math.Abs(q[4]))*.0003f;
   return clear;
  }
  float[] qMin={-.8f,-1.7f,-1.5f,-.3f,-.2f},qMax={1.6f,1.3f,1.5f,1.8f,1.7f};
  var found=new List<(float error,float[] q)>();uint seed=2654435761u;
  float Next(){seed=seed*1664525u+1013904223u;return (seed>>8)/16777216f;}
  const int sampleCount=1600,topCount=5;
  for(int i=0;i<sampleCount;i++)
  {
   var q=new float[5];for(int k=0;k<5;k++)q[k]=qMin[k]+(qMax[k]-qMin[k])*Next();
   if(i==0)Array.Clear(q,0,5);
   if(Eval(q,false,out _,out float e)>=-.0004f)found.Add((e,q));
  }
  // Descend on sparse samples; keep the best result that also passes the
  // dense check (the ashtray is a 2mm shell).
  var results=new List<(float error,float[] q)>();
  foreach(var f in found.OrderBy(f=>f.error).Take(topCount))
  {
   var q=(float[])f.q.Clone();float e=f.error;
   // Coordinate descent, staying clear of the board.
   foreach(float step in new[]{.24f,.12f,.06f,.03f,.015f})
    for(int pass=0;pass<3;pass++)
    {
     bool moved=false;
     for(int k=0;k<5;k++)foreach(float sign in new[]{-1f,1f})
     {
      var t=(float[])q.Clone();t[k]=Math.Clamp(t[k]+sign*step,qMin[k],qMax[k]);
      if(Eval(t,false,out _,out float te)>=-.0004f&&te<e-1e-6f){q=t;e=te;moved=true;}
     }
     if(!moved)break;
    }
   results.Add((e,q));
  }
  results.AddRange(found.OrderBy(f=>f.error).Skip(topCount).Take(30));
  foreach(var r in results.OrderBy(r=>r.error))
  {
   if(Eval(r.q,false,out _,out _,true)<-.0004f)continue;
   Eval(r.q,true,out float gap,out float error,true);
   return ((Matrix4x4[])work.Clone(),error,gap);
  }
  // Nothing clear: the thumb pose that stays out the most.
  float most=float.NegativeInfinity;float[] least=new float[5];
  for(int i=0;i<1200;i++)
  {
   var q=new float[5];for(int k=0;k<5;k++)q[k]=qMin[k]+(qMax[k]-qMin[k])*Next();
   float clear=Eval(q,true,out _,out _,true);if(clear>most){most=clear;least=q;}
  }
  Eval(least,true,out _,out _,true);
  return ((Matrix4x4[])work.Clone(),.05f-most*20,.03f);
 }
 // Thumb joints from rest: CMC toward the index knuckle, across it and
 // twisted about the metacarpal (opposition), then MCP and IP flexion.
 // Child axes follow their parents.
 private void ThumbFlex(Matrix4x4[] source,Matrix4x4[] pose,float[] q)
 {
  foreach(int k in thumb)pose[k]=source[k];
  var p0=rest[thumb[0]].Translation;var dir=rest[thumb[1]].Translation-p0;
  if(dir.LengthSquared()<1e-10f)return;
  var toward=rest[fingers[indexFinger][0]].Translation+new Vector3(rightHand?.018f:-.018f,-.028f,-.018f)-p0;
  var axisA=Vector3.Cross(dir,toward);if(axisA.LengthSquared()<1e-10f)axisA=Vector3.UnitX;axisA=Vector3.Normalize(axisA);
  var axisB=Vector3.Normalize(Vector3.Cross(dir,axisA));
  var spin=Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(dir),q[2])*Matrix4x4.CreateFromAxisAngle(axisA,q[0])*Matrix4x4.CreateFromAxisAngle(axisB,q[1]);
  var turn=Matrix4x4.CreateTranslation(-p0)*spin*Matrix4x4.CreateTranslation(p0);
  foreach(int k in thumb)pose[k]*=turn;
  var carried=spin;
  for(int j=1;j<3;j++)
  {
   float angle=q[j+2];if(angle==0)continue;
   var restDir=rest[thumb[Math.Min(j+1,2)]].Translation-rest[thumb[j-1]].Translation;
   var axis=Vector3.Cross(restDir,-Vector3.UnitY);if(axis.LengthSquared()<1e-10f)continue;
   axis=Vector3.Normalize(Vector3.TransformNormal(Vector3.Normalize(axis),carried));
   var pivot=pose[thumb[j]].Translation;var r=Matrix4x4.CreateFromAxisAngle(axis,angle);
   var t=Matrix4x4.CreateTranslation(-pivot)*r*Matrix4x4.CreateTranslation(pivot);
   for(int k=j;k<3;k++)pose[thumb[k]]*=t;
   carried*=r;
  }
 }
 private void Flex(Matrix4x4[] source,Matrix4x4[] pose,int[] chain,float a0,float a1,float a2)
 {
  // Only this finger's bones change; the rest of the skeleton is shared.
  foreach(int k in chain)pose[k]=source[k];
  float[] angles={a0,a1,a2};
  for(int j=0;j<3;j++)
  {
   if(angles[j]==0)continue;
   var pivot=pose[chain[j]].Translation;
   var direction=rest[chain[Math.Min(j+1,2)]].Translation-rest[chain[Math.Max(0,j-1)]].Translation;
   var axis=Vector3.Cross(direction,-Vector3.UnitY);if(axis.LengthSquared()<1e-12f)continue;axis=Vector3.Normalize(axis);
   var turn=Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateFromAxisAngle(axis,angles[j])*Matrix4x4.CreateTranslation(pivot);
   for(int k=j;k<3;k++)pose[chain[k]]*=turn;
  }
 }
 // CCD on joints [first,last] of a thumb chain, moving `local` (in the frame
 // of `bone`) towards `target`. Free rotation axes, as the thumb solver.
 private static void SolvePoint(Matrix4x4[] pose,int[] chain,int bone,Vector3 local,Vector3 target,int first,int last)
 {
  var trial=(Matrix4x4[])pose.Clone();
  for(int pass=0;pass<48;pass++)
  {
   bool changed=false;
   for(int j=last;j>=first;j--)
   {
    var pivot=pose[chain[j]].Translation;
    var a=Vector3.Transform(local,pose[bone])-pivot;var b=target-pivot;
    var axis=Vector3.Cross(a,b);if(axis.LengthSquared()<1e-12f)continue;axis=Vector3.Normalize(axis);
    if(a.LengthSquared()<1e-12f||b.LengthSquared()<1e-12f)continue;
    float delta=Math.Clamp(MathF.Atan2(Vector3.Dot(axis,Vector3.Cross(Vector3.Normalize(a),Vector3.Normalize(b))),Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b))),-.18f,.18f);
    var turn=Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateFromAxisAngle(axis,delta)*Matrix4x4.CreateTranslation(pivot);
    Array.Copy(pose,trial,pose.Length);for(int k=j;k<chain.Length;k++)trial[chain[k]]*=turn;
    if(Vector3.DistanceSquared(Vector3.Transform(local,trial[bone]),target)>=Vector3.DistanceSquared(Vector3.Transform(local,pose[bone]),target)-1e-12f)continue;
    Array.Copy(trial,pose,pose.Length);changed=true;
   }
   if(!changed||Vector3.DistanceSquared(Vector3.Transform(local,pose[bone]),target)<1e-9f)break;
  }
 }
 // Geometry relative to the rail contact. Measured sections when available,
 // otherwise a flat board `chairThickness` thick and 8cm deep.
 private float Edge(float x)=>surf?.Edge(x)??-.008f;
 private bool Top(float x,float z,out float y)
 {
  if(surf!=null)return surf.Faces(x,z,out _,out y);
  y=chairThickness*.5f;return z>=-.008f&&z<=.072f;
 }
 private bool Bottom(float x,float z,out float y)
 {
  if(surf!=null)return surf.Faces(x,z,out y,out _);
  y=-chairThickness*.5f;return z>=-.008f&&z<=.072f;
 }
 internal float ChairPenetrationDepth {get;private set;}
 // Deepest skin point of any finger/thumb capsule or of the palm inside
 // the chair.
 private float ChairPenetration(Matrix4x4[] pose,Vector3 center,bool withThumb)
 {
  float depth=0;var list=new List<(Vector3 p,float r,int segment)>();
  foreach(var chain in withThumb?fingers.Append(thumb):fingers)
  {
   ChainSamples(pose,chain,ChainRadius(chain),list);
   foreach(var (p,r,_) in list)depth=Math.Max(depth,r-Signed(ToChair(p,center),r+.01f));
  }
  foreach(var (p,r) in PalmProbes())depth=Math.Max(depth,r-Signed(ToChair(p,center),r+.01f));
  return depth;
 }
}

// Solved tray grips persisted between sessions (one line per surface/hand
// signature). Plain invariant text; unreadable lines are ignored.
internal static class ChairSolveCache
{
 internal static string? FilePath {get;set;}
 private static Dictionary<string,(Matrix4x4[] pose,Vector3 contact,Quaternion rotation,float error,float depth,string report)>? loaded;
 private static readonly System.Globalization.CultureInfo C=System.Globalization.CultureInfo.InvariantCulture;
 private static void Load()
 {
  if(loaded!=null)return;loaded=new();
  try
  {
   if(FilePath==null||!System.IO.File.Exists(FilePath))return;
   foreach(var line in System.IO.File.ReadAllLines(FilePath))
   {
    var f=line.Split('\t');if(f.Length!=5)continue;
    var n=f[1].Split(' ').Select(x=>float.Parse(x,C)).ToArray();if(n.Length<9||(n.Length-9)%16!=0)continue;
    var pose=new Matrix4x4[(n.Length-9)/16];
    for(int i=0;i<pose.Length;i++){int o=9+i*16;pose[i]=new Matrix4x4(n[o],n[o+1],n[o+2],n[o+3],n[o+4],n[o+5],n[o+6],n[o+7],n[o+8],n[o+9],n[o+10],n[o+11],n[o+12],n[o+13],n[o+14],n[o+15]);}
    loaded[f[0]]=(pose,new Vector3(n[0],n[1],n[2]),new Quaternion(n[3],n[4],n[5],n[6]),n[7],n[8],f[4]);
   }
  }
  catch{}
 }
 internal static bool TryGet(string signature,out (Matrix4x4[] pose,Vector3 contact,Quaternion rotation,float error,float depth,string report) entry)
 {
  Load();entry=default;return loaded!.TryGetValue(signature,out entry);
 }
 internal static void Put(string signature,(Matrix4x4[] pose,Vector3 contact,Quaternion rotation,float error,float depth,string report) e)
 {
  Load();loaded![signature]=e;if(FilePath==null)return;
  try
  {
   var n=new List<float>{e.contact.X,e.contact.Y,e.contact.Z,e.rotation.X,e.rotation.Y,e.rotation.Z,e.rotation.W,e.error,e.depth};
   foreach(var m in e.pose)n.AddRange(new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44});
   if(n.Any(v=>!float.IsFinite(v)))return;
   string line=signature+"\t"+string.Join(" ",n.Select(v=>v.ToString("R",C)))+"\t\t\t"+e.report.Replace('\t',' ').Replace('\n',' ');
   System.IO.File.AppendAllText(FilePath,line+"\n");
  }
  catch{}
 }
}
