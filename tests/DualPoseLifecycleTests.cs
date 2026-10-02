using System;using System.Linq;using System.Collections.Generic;using System.Numerics;using XiiiXR;
class DualPoseLifecycleTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static FingerPoseMath Make(bool right)
 {
  string side=right?"R":"L";var names=new List<string>();var rest=new List<Matrix4x4>();
  for(int f=1;f<=4;f++)for(int j=1;j<=3;j++)
  {names.Add($"{side}_Finger_{f:00}_{j:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation((right?1:-1)*(-.035f+(f-1)*.021f),0,.055f+(j-1)*.03f));}
  for(int j=1;j<=3;j++){names.Add($"{side}_Thumb_01_{j:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(right?-.055f:.055f,-.01f,.025f+(j-1)*.023f));}
  return new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
 }
 static void Main()
 {
  var right=Make(true);var left=Make(false);right.ResetGrips();
  _=left.Pose(1,1,"dual_pistol");Check(!left.Has("dual_pistol"),"uncaptured synthetic grip masquerades as authored");
  int revision=left.Revision;var pose=right.Pose(1,1,"");
  Check(right.Capture("pistol",pose),"fixture capture rejected");
  Check(left.Revision!=revision,"right capture fails to invalidate already rendered left hand");
  var l=left.Pose(1,0,"dual_pistol");Check(left.Has("dual_pistol"),"left does not pick up late right capture");
  for(int i=0;i<l.Length;i++)Check(Vector3.Distance(l[i].Translation,Vector3.Transform(pose[i].Translation,Matrix4x4.CreateScale(-1,1,1)))<.0001f,"left joint differs from mirrored authored grip");
  revision=left.Revision;right.Capture("pistol",pose);Check(left.Revision==revision,"same capture invalidates every eye/frame");
  right.ResetGrips();Check(left.Revision!=revision,"scene/weapon reset retains old grip revision");
  _=left.Pose(1,0,"dual_pistol");Check(!left.Has("dual_pistol"),"stale left authored pose survives source reset");
  // 0.1.128: every weapon hold mirrors to the other hand (left on the handle, right on the fore-end).
  right=Make(true);left=Make(false);
  var rifle=right.Pose(1,1,"");Check(right.Capture("m16",rifle),"rifle grip capture rejected");
  var lr=left.Pose(1,0,FingerPoseMath.MirrorPrefix+"m16");Check(left.Has(FingerPoseMath.MirrorPrefix+"m16"),"left hand has no mirrored rifle grip");
  for(int i=0;i<lr.Length;i++)Check(Vector3.Distance(lr[i].Translation,Vector3.Transform(rifle[i].Translation,Matrix4x4.CreateScale(-1,1,1)))<.0001f,"left rifle joint differs from the mirrored right grip");
  var support=left.Pose(1,0,"");Check(left.Capture("m16",support),"support capture rejected");int rr=right.Revision;
  var rs=right.Pose(1,0,FingerPoseMath.MirrorPrefix+"m16");Check(right.Has(FingerPoseMath.MirrorPrefix+"m16"),"right hand has no mirrored fore-end hold");
  for(int i=0;i<rs.Length;i++)Check(Vector3.Distance(rs[i].Translation,Vector3.Transform(support[i].Translation,Matrix4x4.CreateScale(-1,1,1)))<.0001f,"right fore-end joint differs from the mirrored left hold");
  Check(!right.Has(FingerPoseMath.MirrorPrefix+"ak47")&&right.Pose(1,0,FingerPoseMath.MirrorPrefix+"ak47").Length==rs.Length,"a weapon never held natively has no mirrored hold (procedural grip instead)");
  // 0.1.153: a chair (worked out, not captured) in the left hand: the right hand's hold mirrored.
  right=Make(true);left=Make(false);
  var chair=right.Pose(1,0,"prop_wpn_ms_chair");var lc=left.Pose(1,0,FingerPoseMath.MirrorPrefix+"prop_wpn_ms_chair");
  for(int i=0;i<lc.Length;i++)Check(Vector3.Distance(lc[i].Translation,Vector3.Transform(chair[i].Translation,Matrix4x4.CreateScale(-1,1,1)))<.0001f,"left chair hold is not the right one mirrored (joint "+i+")");
  Check(ReferenceEquals(lc,left.Pose(1,0,FingerPoseMath.MirrorPrefix+"prop_wpn_ms_chair")),"mirrored chair hold rebuilt every frame");
  // A bottle in the left hand: no finger left out on a trigger.
  var bottle=right.Pose(1,1,"prop_wpn_ms_bottle_03");var lb=left.Pose(1,1,FingerPoseMath.MirrorPrefix+"prop_wpn_ms_bottle_03");
  for(int i=0;i<lb.Length;i++)Check(Vector3.Distance(lb[i].Translation,Vector3.Transform(bottle[i].Translation,Matrix4x4.CreateScale(-1,1,1)))<.0001f,"left bottle hold differs from the right one mirrored (index on a trigger?) joint "+i);
  Console.WriteLine("PASS: left-first draw, late native right capture, reflected contact pose, stable cache and scene/weapon invalidation; every weapon hold mirrors to the other hand; a chair and a bottle in the left hand are the right hand's holds mirrored.");
 }
}
