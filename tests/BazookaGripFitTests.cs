using System;using System.Linq;using System.Collections.Generic;using System.Numerics;using System.Globalization;using XiiiXR;
// 0.1.227: the hands closed round the bazooka's grips and round its rocket,
// checked with the player's own hand as logged (HAND REST) and the bazooka's
// grips as measured in the game (BAZOOKA grips, logs of 0.1.223 to 0.1.226):
// the fingers lie round the grip (on its surface, neither in it nor off it),
// the index of the hand on the handle above the handle (on the trigger), the
// thumb not in the grip, the left hand the right one mirrored.
class BazookaGripFitTests
{
 static readonly List<string> failures=new();
 static void Check(bool ok,string message){if(!ok)failures.Add(message);}
 const string LoggedHand="hand=R F:-33.2,7.0,102.5;-37.0,7.8,137.0;-39.6,8.3,160.3;pad=-43.2,1.9,178.4;r=8.5 F:-10.1,2.1,103.8;-12.0,2.5,144.9;-13.3,2.8,170.0;pad=-15.2,-2.1,187.0;r=8.3 F:13.2,-2.8,101.9;13.9,-2.9,141.7;14.3,-3.0,164.6;pad=14.9,-7.2,179.1;r=7.7 F:32.5,-6.9,96.6;33.6,-7.1,129.3;34.2,-7.2,147.9;pad=34.5,-12.0,160.3;r=8.4 T:-41.8,-6.2,41.6;-56.4,-3.1,64.1;-71.0,0.0,86.6;pad=-82.7,-6.8,101.3;r=8.5";
 static FingerPoseMath Hand(bool right)
 {
  var names=new List<string>();var rest=new List<Matrix4x4>();var pads=new List<Vector3>();int f=0;string side=right?"R":"L";float sx=right?1:-1;
  foreach(var part in LoggedHand.Split(' ',StringSplitOptions.RemoveEmptyEntries).Skip(1))
  {
   bool th=part.StartsWith("T:");var fields=part.Substring(2).Split(';');
   for(int j=0;j<3;j++){var c=fields[j].Split(',').Select(x=>float.Parse(x,CultureInfo.InvariantCulture)/1000).ToArray();names.Add(th?$"{side}_Thumb_01_{j+1:00}SHJnt":$"{side}_Finger_{f+1:00}_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(sx*c[0],c[1],c[2]));}
   var p=fields[3].Substring(4).Split(',').Select(x=>float.Parse(x,CultureInfo.InvariantCulture)/1000).ToArray();pads.Add(new Vector3(sx*p[0],p[1],p[2]));
   if(!th)f++;
  }
  var h=new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
  int k=0;foreach(int bone in h.RimDistalBones){var pad=pads[k++];h.SetRimPadCloud(bone,new[]{pad,pad+Vector3.UnitX*.0005f,pad-Vector3.UnitX*.0005f,rest[bone].Translation});}
  return h;
 }
 // The grips as the game measured them (fitted frame).
 static GripBarMath.Bar Grip(Vector3 center,float length,float thickness)
 {
  var axis=Vector3.Normalize(new Vector3(0,1,.03f));var toward=Vector3.Normalize(Vector3.UnitZ-axis*Vector3.Dot(Vector3.UnitZ,axis));
  return new GripBarMath.Bar(center,axis,toward,length,thickness);
 }
 static readonly GripBarMath.Bar Handle=Grip(new Vector3(-.002f,-.166f,-.095f),.06f,.036f),Front=Grip(new Vector3(-.003f,-.151f,.066f),.10f,.035f);
 const float Size=.7456f;
 // A point of the hand (canonical) where it is drawn on the gun.
 static Vector3 Drawn(Vector3 canonical,Vector3 p,Quaternion q,float size)=>p+Vector3.Transform(new Vector3(0,0,NativeHandMesh.WristZ)+canonical*size,q);
 static (float radial,float along) OnBar(Vector3 w,GripBarMath.Bar bar)
 {
  var a=Vector3.Normalize(bar.Axis);var d=w-bar.Center;float along=Vector3.Dot(d,a);return ((d-a*along).Length(),along);
 }
 static int[][] Chains(FingerPoseMath h)=>Enumerable.Range(0,5).Select(c=>new[]{c*3,c*3+1,c*3+2}).ToArray();
 static void Main()
 {
  var report=new List<string>();
  var placed=new Dictionary<string,(Vector3 p,Quaternion q)>();var raises=new Dictionary<string,float>();
  foreach(bool right in new[]{true,false})foreach(bool onHandle in new[]{true,false})
  {
   var h=Hand(right);var bar=onHandle?Handle:Front;string name=(right?"R":"L")+(onHandle?" handle":" front");
   float radius=BazookaTubeMath.GripRadius(bar)/Size;
   var fitted=h.FitGrip(onHandle?FingerPoseMath.TriggerWrapPrefix:"prop_bazooka_front@",radius,bar.Length*.5f/Size,onHandle,out float curl,out var channel,out var little);
   Check(fitted!=null,name+": no fit");fitted??="";
   Check(curl<FingerPoseMath.HeldCurl&&curl>=FingerPoseMath.MinWrapCurl,name+": the fingers not opened round the thick grip: "+curl);
   h.GripFit(fitted,out var amounts,out float thumbAmount);
   var (p,q)=BazookaTubeMath.OnGrip(bar,BazookaTubeMath.RightTurn,right,BazookaTubeMath.DrawnChannel(channel,Size),little);placed[name]=(p,q);
   Check(MathF.Abs(little.Length()-1)<1e-4f&&little.X*(right?1:-1)>.9f,name+": the fingers' line not across the hand toward the little finger: "+little);
   // The middle of the fingers' curl on the grip's line at its middle.
   var c=Drawn(channel,p,q,Size);var (cr,ca)=OnBar(c,bar);
   Check(cr<.001f&&MathF.Abs(ca)<.001f,name+": the fingers' middle "+cr*100+" cm off the grip's line");
   // The fingers' line along the grip (the little finger down it).
   Check(Vector3.Dot(Vector3.Transform(little,q),-bar.Axis)>.999f,name+": the fingers' line not along the grip");
   // Turned within 15 degrees of the game's right hand on the handle (mirrored for the left).
   var t=BazookaTubeMath.RightTurn;if(!right)t=new Quaternion(t.X,-t.Y,-t.Z,t.W);
   float turnDeg=2*MathF.Acos(Math.Clamp(MathF.Abs(Quaternion.Dot(Quaternion.Normalize(t),q)),0,1))*180/MathF.PI;
   Check(turnDeg<15,name+": turned "+turnDeg+" degrees from the game's hold");
   string profile=(onHandle&&!right?FingerPoseMath.MirrorPrefix:"")+fitted;
   var pose=h.Pose(1,0,profile);var pads=h.RimPads(pose);
   var chains=Chains(h);int index=Enumerable.Range(0,4).OrderBy(i=>Vector3.DistanceSquared(pose[i*3].Translation,pose[12].Translation)).First();
   float barR=BazookaTubeMath.GripRadius(bar);float worstIn=1,worstOff=0;var line=new List<string>();
   for(int f=0;f<4;f++)
   {
    if(onHandle&&f==index)continue;
    // The finger's middle and end joints and its pad round the grip.
    var pts=new[]{pose[chains[f][1]].Translation,pose[chains[f][2]].Translation,pads[f]-Vector3.UnitZ*NativeHandMesh.WristZ};
    foreach(var cp in pts)
    {
     var (r,along)=OnBar(Drawn(cp,p,q,Size),bar);float gap=r-barR;line.Add((gap*1000).ToString("F0"));
     worstIn=Math.Min(worstIn,gap);worstOff=Math.Max(worstOff,gap);
     Check(MathF.Abs(along)<bar.Length*.5f+.01f,name+": a finger beyond the grip's ends");
    }
   }
   // Joint middles 2 to 11 mm off the grip's surface: the fingers' skin (5 to 7 mm at the hand's 0.75) on it.
   Check(worstIn>.002f,name+": a finger in the grip ("+worstIn*1000+" mm)");
   Check(worstOff<.011f,name+": a finger off the grip ("+worstOff*1000+" mm from its surface)");
   if(onHandle)
   {
    // 0.1.231: the hand moved along the handle until the index fingertip is level with the trigger
    // (a trigger 2 cm above the handle's top, 3 cm in front of its line); within the move's limits.
    var padC=h.IndexPad(profile);var (_,padAlong)=OnBar(Drawn(padC,p,q,Size),bar);
    var axis=Vector3.Normalize(bar.Axis);var trig=bar.Center+axis*(bar.Length*.5f+.02f)+bar.Toward*.03f;
    float raise=BazookaTubeMath.RaiseToTrigger(bar,p,q,BazookaTubeMath.DrawnChannel(padC,Size),trig);
    var (_,raisedAlong)=OnBar(Drawn(padC,p+axis*raise,q,Size),bar);var (_,trigAlong)=OnBar(trig,bar);
    Check(MathF.Abs(raisedAlong-trigAlong)<.001f&&raise>0,name+": the index fingertip not level with the trigger after the move ("+raisedAlong*100+" cm, the trigger "+trigAlong*100+")");
    Check(BazookaTubeMath.RaiseToTrigger(bar,p,q,BazookaTubeMath.DrawnChannel(padC,Size),bar.Center+axis*.5f)==BazookaTubeMath.MaxRaise
     &&BazookaTubeMath.RaiseToTrigger(bar,p,q,BazookaTubeMath.DrawnChannel(padC,Size),bar.Center-axis*.5f)==BazookaTubeMath.MinRaise,name+": the move along the handle not kept within its limits");
    // The web of the hand (the index knuckle) not above the handle's own top (its join to the tube): a trigger far up stops there.
    h.IndexPad(profile,out var knuckleC);float handleTop=bar.Length*.5f+.026f;
    float capped=BazookaTubeMath.RaiseToTrigger(bar,p,q,BazookaTubeMath.DrawnChannel(padC,Size),bar.Center+axis*.5f,BazookaTubeMath.DrawnChannel(knuckleC,Size),handleTop);
    var (_,knuckleAlong)=OnBar(Drawn(knuckleC,p+axis*capped,q,Size),bar);
    Check(MathF.Abs(knuckleAlong-(handleTop+BazookaTubeMath.KnuckleOver))<.001f&&capped<BazookaTubeMath.MaxRaise,name+": the hand moved up past the handle's top (its knuckle "+knuckleAlong*100+" cm, the top "+handleTop*100+")");
    Check(BazookaTubeMath.RaiseToTrigger(bar,p,q,BazookaTubeMath.DrawnChannel(padC,Size),trig,BazookaTubeMath.DrawnChannel(knuckleC,Size),handleTop)==raise,name+": a trigger below the cap not reached");
    raises[name]=raise;
    report.Add(name+": the index fingertip "+(padAlong*100).ToString("F1")+" cm along the handle from its middle, moved "+(raise*100).ToString("F1")+" cm to a trigger 2 cm above the handle's top");
    // The index on the trigger: its knuckle above the handle's top (within 3 cm), not round the handle.
    var (_,ia)=OnBar(Drawn(pose[chains[index][0]].Translation,p,q,Size),bar);
    Check(ia>bar.Length*.5f-.006f&&ia<bar.Length*.5f+.03f,name+": the index finger not at the trigger above the handle ("+ia*100+" cm along it)");
    report.Add(name+": the index knuckle "+(ia*100).ToString("F1")+" cm along the handle from its middle");
   }
   // The thumb not in the grip where the grip is.
   float thumbIn=1;
   foreach(var cp in new[]{pose[13].Translation,pose[14].Translation,pads[4]-Vector3.UnitZ*NativeHandMesh.WristZ})
   {var (r,along)=OnBar(Drawn(cp,p,q,Size),bar);if(MathF.Abs(along)<bar.Length*.5f)thumbIn=Math.Min(thumbIn,r-barR);}
   Check(thumbIn>.003f,name+": the thumb in the grip ("+thumbIn*1000+" mm)");
   // 0.1.228: the thumb wraps round the grip (above the handle it was left open along the gun's side),
   // clear of the index finger (on the trigger on the handle).
   Check(thumbAmount>1,name+": the thumb not wrapped round the grip: "+thumbAmount);
   float thumbIndex=1;
   {
    var tp=new[]{pose[12].Translation,pose[13].Translation,pose[14].Translation,pads[4]-Vector3.UnitZ*NativeHandMesh.WristZ};
    var ip=new[]{pose[chains[index][0]].Translation,pose[chains[index][1]].Translation,pose[chains[index][2]].Translation,pads[index]-Vector3.UnitZ*NativeHandMesh.WristZ};
    for(int a=0;a<3;a++)for(int u=0;u<=6;u++)for(int b=0;b<3;b++)for(int w=0;w<=6;w++)
     thumbIndex=Math.Min(thumbIndex,Vector3.Distance(Vector3.Lerp(tp[a],tp[a+1],u/6f),Vector3.Lerp(ip[b],ip[b+1],w/6f)));
   }
   Check(!onHandle||thumbIndex>.012f,name+": the thumb through the index finger on the trigger ("+thumbIndex*1000+" mm)");
   report.Add(name+" "+fitted+" fingers "+string.Join("/",amounts.Select(a=>a.ToString("F2")))+" thumb "+thumbAmount.ToString("F2")+": joints off the surface "+string.Join("/",line)+" mm, turned "+turnDeg.ToString("F0")+" deg"+(thumbIn<1?", thumb joints "+(thumbIn*1000).ToString("F0")+" mm":"")+", thumb to index "+(thumbIndex*1000).ToString("F0")+" mm");
  }
  // The left hand the right one mirrored across the grip (the grips are upright, x=-0.002).
  foreach(var g in new[]{"handle","front"})
  {
   var (pr,qr)=placed["R "+g];var (pl,ql)=placed["L "+g];var bar=g=="handle"?Handle:Front;
   var mirrored=new Vector3(2*bar.Center.X-pr.X,pr.Y,pr.Z);var mq=new Quaternion(qr.X,-qr.Y,-qr.Z,qr.W);
   Check(Vector3.Distance(mirrored,pl)<.002f&&MathF.Abs(Quaternion.Dot(mq,ql))>.999f,"the left hand on the "+g+" not the right one mirrored");
  }
  Check(raises.Count==2&&MathF.Abs(raises["R handle"]-raises["L handle"])<.001f,"the left hand moved along the handle unlike the right one");
  {
   var top=BazookaTubeMath.TopAlong(Handle,new[]{Handle.Center+Vector3.Normalize(Handle.Axis)*.05f,Handle.Center-Vector3.Normalize(Handle.Axis)*.04f,Handle.Center+Handle.Toward*.1f});
   Check(top is float t&&MathF.Abs(t-.05f)<1e-4f&&BazookaTubeMath.TopAlong(Handle,Array.Empty<Vector3>())==null,"the handle's own top along its line");
  }
  // The trigger closure handed on in the profile; the index on the trigger with it.
  Check(FingerPoseMath.TriggerWrapProfile(.6f)=="bazooka_trigger@60"&&MathF.Abs(FingerPoseMath.HeldAmount("bazooka_trigger@60")-.6f)<1e-5f&&FingerPoseMath.HeldAmount("bazooka_trigger")==FingerPoseMath.HeldCurl
   &&MathF.Abs(FingerPoseMath.HeldAmount("mirror:bazooka_trigger@60".Substring(FingerPoseMath.MirrorPrefix.Length))-.6f)<1e-5f,"the trigger closure's profile");
  {
   var h=Hand(true);var a=h.Pose(1,0,"bazooka_trigger@60");var b=h.Pose(1,0,"prop_long_handle@60");
   Check(a[1]!=b[1]&&a[4]==b[4]&&a[7]==b[7],"the index not on the trigger (or the others not closed alike)");
   var l=Hand(false);var la=l.Pose(1,0,"mirror:bazooka_trigger@60");var lb=l.Pose(1,0,"prop_long_handle@60");
   Check(la[1]!=lb[1]&&la[4]==lb[4],"the left hand's index not on the trigger");
  }
  // Without the hand's fingers: the logged hand's channel and line.
  {
   var h=Hand(true);float curl=h.GripCurl(BazookaTubeMath.GripRadius(Handle)/Size,out _);h.GripChannel(curl,true,out var ch,out var lt);
   Check(MathF.Abs(curl-BazookaTubeMath.FallbackCurl)<.03f&&Vector3.Distance(ch,BazookaTubeMath.FallbackChannel(true,true))<.002f&&Vector3.Dot(lt,BazookaTubeMath.FallbackLittle(true,true))>.999f,"the fallback channel not the logged hand's: "+curl+" "+ch);
   h.GripChannel(curl,false,out var all,out var la);
   Check(Vector3.Distance(all,BazookaTubeMath.FallbackChannel(true,false))<.002f&&Vector3.Dot(la,BazookaTubeMath.FallbackLittle(true,false))>.999f,"the fallback channel (all fingers)");
   var fl=BazookaTubeMath.FallbackChannel(false,true);var fr=BazookaTubeMath.FallbackChannel(true,true);Check(fl.X==-fr.X&&fl.Y==fr.Y&&fl.Z==fr.Z,"the left fallback not mirrored");
   var ll=BazookaTubeMath.FallbackLittle(false,false);var lr=BazookaTubeMath.FallbackLittle(true,false);Check(ll.X==-lr.X&&ll.Y==lr.Y&&ll.Z==lr.Z,"the left fallback's line not mirrored");
  }
  // 0.1.226's places (the game's hands of other games): the fingers' middle 10 cm from the handle.
  {
   var h=Hand(true);h.GripChannel(FingerPoseMath.HeldCurl,true,out var ch,out _);
   var old=Drawn(ch,Handle.Center+new Vector3(.059f,.090f,-.044f),BazookaTubeMath.RightTurn,Size);
   Check(OnBar(old,Handle).radial>.04f,"0.1.226's place was on the handle after all");
  }
  // The rocket (5.5 cm, the hand at full size) along the knuckles through the
  // middle of the fingers' curl: each finger on it, the thumb not in it.
  foreach(bool right in new[]{true,false})
  {
   var h=Hand(right);const float tube=.0275f;var fitted=h.FitGrip("prop_rocket@",tube,.11f,false,out float curl,out var ch,out var little)??"";
   Check(fitted.Length>0,"rocket fit");h.GripFit(fitted,out var amounts,out float thumbAmount);
   var at=BazookaTubeMath.DrawnChannel(ch,1);var g=-little;
   var pose=h.Pose(1,0,fitted);var pads=h.RimPads(pose);
   float worstIn=1,worstOff=0,thumbIn=1;var line=new List<string>();
   for(int f=0;f<4;f++)foreach(var cp in new[]{pose[f*3+1].Translation,pose[f*3+2].Translation,pads[f]-Vector3.UnitZ*NativeHandMesh.WristZ})
   {var d=new Vector3(0,0,NativeHandMesh.WristZ)+cp-at;float r=(d-g*Vector3.Dot(d,g)).Length()-tube;worstIn=Math.Min(worstIn,r);worstOff=Math.Max(worstOff,r);line.Add((r*1000).ToString("F0"));}
   foreach(var cp in new[]{pose[13].Translation,pose[14].Translation,pads[4]-Vector3.UnitZ*NativeHandMesh.WristZ})
   {var d=new Vector3(0,0,NativeHandMesh.WristZ)+cp-at;thumbIn=Math.Min(thumbIn,(d-g*Vector3.Dot(d,g)).Length()-tube);}
   report.Add("rocket "+(right?"R ":"L ")+fitted+" fingers "+string.Join("/",amounts.Select(a=>a.ToString("F2")))+" thumb "+thumbAmount.ToString("F2")+": joints off its surface "+string.Join("/",line)+" mm, thumb joints "+(thumbIn*1000).ToString("F0")+" mm");
   // Joint middles 3 to 13 mm off its surface (the skin, 6 to 9 mm, on it).
   Check(worstIn>.003f,"the rocket in the fingers ("+worstIn*1000+" mm)");
   Check(worstOff<.013f,"the fingers off the rocket ("+worstOff*1000+" mm)");
   Check(thumbIn>.004f,"the thumb in the rocket ("+thumbIn*1000+" mm)");
  }
  foreach(var r in report)Console.WriteLine(r);
  if(failures.Count>0)throw new Exception(string.Join("; ",failures));
  Console.WriteLine("PASS: 0.1.231 the hand on the bazooka's handle moved along it until the index fingertip is level with the trigger (up to 4 cm up, never down; the web of the hand not above the handle's own top); the left hand alike.");
  Console.WriteLine("PASS: 0.1.228 the thumb wraps round the bazooka's handle too (clear of it and of the index finger on the trigger).");
  Console.WriteLine("PASS: 0.1.227 the hands close round the bazooka's grips (the middle of the fingers' curl on the grip's line, closed as much as the grip is thick, the index on the trigger above the handle, the left hand the right one mirrored) and round the rocket's tube (its line through that middle, the thumb out of it).");
 }
}
