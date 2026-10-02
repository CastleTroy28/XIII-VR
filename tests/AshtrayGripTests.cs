using System;using System.Linq;using System.Collections.Generic;using System.Numerics;using XiiiXR;
class AshtrayGripTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main()
 {
  MeasureSurface();
  const string profile="prop_wpn_ms_ashtray";int cases=0;
  foreach(bool right in new[]{true,false})foreach(float scale in new[]{.85f,1f,1.15f})foreach(float thickness in new[]{.006f,.014f,.028615f,.04f})
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
   var anchor=fingers.RimContact(profile,thickness);var pose=fingers.Pose(0,0,profile);var pads=fingers.RimPads(pose);
   // The tray's broad faces must be between opposing pads, with purchase
   // inside its rim. Coinciding with their midpoint alone is insufficient.
   var toTray=Quaternion.Inverse(fingers.RimRotation);
   var low=Vector3.Transform(pads[0]-anchor,toTray);var high=Vector3.Transform(pads[4]-anchor,toTray);
   Check(Math.Abs(low.Y+thickness*.5f)<.001f,"index is not on one rim face");
   Check(Math.Abs(high.Y-thickness*.5f)<.0015f,"thumb is not on opposite face: "+right+" "+scale+" "+thickness+" err="+fingers.RimPinchError);
   Check(Vector2.Distance(new Vector2(low.X,low.Z),new Vector2(high.X,high.Z))<.002f,"thumb does not oppose index");
   Check(low.Z>.004f&&low.Z<.016f,"pinch has no purchase inside rim");
   Check(fingers.RimBonesClear(pose),"palm or phalanges cross tray: "+right+" "+scale+" "+thickness);
   for(int f=0;f<5;f++)for(int j=1;j<3;j++)Check(Math.Abs(Vector3.Distance(pose[f*3+j-1].Translation,pose[f*3+j].Translation)-Vector3.Distance(rest[f*3+j-1].Translation,rest[f*3+j].Translation))<1e-5f,"finger length changed");
   for(int i=0;i<pose.Length;i++)foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})Check(Math.Abs(Vector3.TransformNormal(axis,pose[i]).Length()-Vector3.TransformNormal(axis,rest[i]).Length())<1e-5f,"bone scale changed");
   var held=fingers.Pose(1,1,profile);Check(ReferenceEquals(pose,held),"trigger/grip causes re-solving or a different pose");
   int revision=fingers.Revision;for(int i=0;i<100;i++)Check(fingers.RimContact(profile,thickness)==anchor,"stationary rim anchor drifts");
   Check(fingers.Revision==revision,"contact dirties the skin every frame");
   // A fresh visual/outfit may have another thickness; pose and mount must
   // change together, and the next render must see a new revision.
   fingers.RimContact(profile,thickness+.002f);Check(fingers.Revision>revision&&!ReferenceEquals(pose,fingers.Pose(1,0,profile)),"geometry change reused an incompatible pinch");
   var wrist=Matrix4x4.CreateFromYawPitchRoll(.7f,-.5f,1.2f)*Matrix4x4.CreateTranslation(4,2,-3);
   var itemRim=new Vector3(0,-.044f,.073f);var mount=itemRim-Vector3.Transform(anchor,toTray);
   var actual=Vector3.Transform(Vector3.Transform(pads[0],toTray)+mount,wrist);
   var expected=Vector3.Transform(itemRim+new Vector3(0,-thickness*.5f,.008f),wrist);
   Check(Vector3.Distance(actual,expected)<.001f,"grip/item contact separates when wrist turns");cases++;
  }
  Console.WriteLine("PASS: "+cases+" mirrored/outfit/thickness fixtures; opposing skin pads, rim contact and rim purchase; rigid bone lengths/scales; trigger stability; cached solving; geometry revision; rotated world-space attachment. Synthetic rigs, not headset validation.");
 }
 static void MeasureSurface()
 {
  foreach(float angle in new[]{0f,.35f,-.45f})
  {
   var v=new List<Vector3>();var t=new List<int>();
   // A shallow tray: broad bottom and a raised rear rim. Its thickness
   // at the pinch differs from the central bowl and its rotated AABB.
   Box(new(-.115f,-.014f,0),new(.115f,-.008f,.16f));
   Box(new(-.115f,-.008f,0),new(.115f,.014f,.022f));
   Box(new(-.115f,-.008f,.138f),new(.115f,.014f,.16f));
   var transform=Matrix4x4.CreateRotationY(angle)*Matrix4x4.CreateTranslation(.13f,.07f,.04f);
   var vertices=v.Select(p=>Vector3.Transform(p,transform)).ToArray();
   Check(AshtrayRimGeometry.TryMeasure(vertices,t.ToArray(),.13f,out var edge,out float thickness),"rim section missed");
   Check(Math.Abs(thickness-.028f)<1e-5f,"rim measurement sampled the bowl or AABB");
   Check(Math.Abs(edge.Y-.07f)<1e-5f,"surface midpoint changed");
   if(angle!=0)Check(edge.Z>vertices.Min(p=>p.Z)+.015f,"pinch follows empty AABB corner");
   Check(!AshtrayRimGeometry.TryMeasure(vertices,t.ToArray(),2,out _,out _),"sample outside mesh accepted");
   void Box(Vector3 min,Vector3 max)
   {
    int first=v.Count;
    for(int i=0;i<8;i++)v.Add(new((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z));
    foreach(int i in new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5})t.Add(first+i);
   }
  }
 }

}
