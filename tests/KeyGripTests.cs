using System;using System.Linq;using System.Collections.Generic;using System.Numerics;using XiiiXR;
class KeyGripTests
{
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static void Main()
 {
  var vertices=new List<Vector3>();var triangles=new List<int>();
  // Key with a hollow bow; its centre is empty, so centring the item at
  // the fingertip would not produce contact with either visible face.
  Box(new(-.015f,-.001f,0),new(.015f,.001f,.004f));
  Box(new(-.015f,-.001f,0),new(-.010f,.001f,.03f));
  Box(new(.010f,-.001f,0),new(.015f,.001f,.03f));
  Box(new(-.015f,-.001f,.026f),new(.015f,.001f,.03f));
  Box(new(-.003f,-.001f,.03f),new(.003f,.001f,.10f));
  Box(new(.003f,-.001f,.075f),new(.009f,.001f,.09f));
  foreach(var imported in new[]{Matrix4x4.Identity,Matrix4x4.CreateRotationY(MathF.PI),Matrix4x4.CreateRotationX(MathF.PI/2)*Matrix4x4.CreateScale(.2f)*Matrix4x4.CreateTranslation(1,-2,3)})
  {
   var mesh=vertices.Select(p=>Vector3.Transform(p,imported)).ToArray();
   var fit=KeyGripGeometry.Fit(mesh,triangles.ToArray(),false);
   var contact=fit.edge+Vector3.UnitZ*AshtrayRimGeometry.Purchase;
   var fitted=mesh.Select(p=>Vector3.Transform(p,fit.fit)).ToArray();
   Check(contact.Z<.031f,"key is pinched by blade instead of bow");
   Check(contact.Z<=.0041f||Math.Abs(contact.X)>=.0099f||contact.Z>=.0259f,"key pinch lies in bow hole");
   Check(Math.Abs(fitted.Max(p=>p.Z)-.1f)<.00001f&&fit.thickness>=.004f,"import rotation/scale changes real key size");
  }
  foreach(bool right in new[]{false,true})foreach(float scale in new[]{.85f,1f,1.15f})foreach(string profile in new[]{"key","card"})
  {
   string side=right?"R":"L";float sign=right?1:-1;var names=new List<string>();var rest=new List<Matrix4x4>();
   for(int f=0;f<4;f++)for(int j=0;j<3;j++)
   {names.Add($"{side}_Finger_{f+1:00}_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(sign*(-.03f+.02f*f)*scale,0,(.09f+j*.03f)*scale));}
   for(int j=0;j<3;j++){names.Add($"{side}_Thumb_01_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(sign*(-.05f-j*.003f)*scale,-.012f*scale,(.043f+j*.027f)*scale));}
   var hand=new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
   foreach(int bone in hand.RimDistalBones)
   {
    var p=rest[bone].Translation+(rest[bone].Translation-rest[bone-1].Translation)*.65f-Vector3.UnitY*.005f;
    hand.SetRimPadCloud(bone,new[]{p,p+Vector3.UnitX*.0005f,p-Vector3.UnitX*.0005f});
   }
   var edge=hand.RimContact(profile,.004f);var pose=hand.Pose(0,0,profile);var pads=hand.RimPads(pose);var inverse=Quaternion.Inverse(hand.RimRotation);
   if(profile=="key")Check(Vector3.Distance(Vector3.Transform(Vector3.UnitZ,hand.RimRotation),Vector3.UnitZ)<.00001f,"key blade points sideways relative to controller");
   var index=Vector3.Transform(pads[0]-edge,inverse);var thumb=Vector3.Transform(pads[4]-edge,inverse);
   Check(Math.Abs(index.Y+.002f)<.001f&&Math.Abs(thumb.Y-.002f)<.0015f,$"{side} {scale} {profile}: key not pinched between actual thumb/index pads: {index} / {thumb}; error={hand.RimPinchError}");
   Check(Vector3.Distance(index,thumb)<.006f,"key fingers leave a visible gap");
   Check(ReferenceEquals(pose,hand.Pose(1,1,profile)),"key grip changes with trigger or grip input");
   var itemEdge=new Vector3(.005f,0,0);var offset=edge-Vector3.Transform(itemEdge,hand.RimRotation);
   var root=Matrix4x4.CreateFromYawPitchRoll(.6f,-.5f,.8f)*Matrix4x4.CreateTranslation(3,2,4);
   var skinPoint=Vector3.Transform(pads[0],root);
   var keyPoint=Vector3.Transform(offset+Vector3.Transform(itemEdge+new Vector3(0,-.002f,.008f),hand.RimRotation),root);
   Check(Vector3.Distance(skinPoint,keyPoint)<.001f,"key detaches when controller rotates");
   int rev=hand.Revision;hand.RimContact(profile=="key"?"card":"key",.004f);Check(hand.Revision>rev,"key/card orientation cache reused");
   hand.RimContact("key",.004f);Check(Vector3.Distance(Vector3.Transform(Vector3.UnitZ,hand.RimRotation),Vector3.UnitZ)<.00001f,"card to key transition loses forward blade");
   rev=hand.Revision;hand.RimContact("prop_wpn_ms_ashtray",.028f);Check(hand.Revision>rev,"pinch cache not invalidated when returning to ashtray");
   // 0.1.107: lockpick held like a screwdriver: a closed fist, fixed under the buttons.
   var fist=hand.Pose(0,0,"screwdriver");var fist2=hand.Pose(1,1,"screwdriver");
   for(int f=0;f<4;f++)
   {
    int tip=f*3+2;
    Check(fist[tip].Translation.Y<-.012f*scale&&fist[tip].Translation.Z<rest[tip].Translation.Z-.02f*scale,$"{side} {scale}: finger {f} not closed around the handle: {fist[tip].Translation}");
    Check(Vector3.Distance(fist[tip].Translation,fist2[tip].Translation)<1e-6f,"screwdriver grip changes with grip/trigger input");
   }
   // Handle on the thumb side of the curled index finger, thumb pad on top.
   var axis=hand.ScrewdriverContact(.008f)-Vector3.UnitZ*NativeHandMesh.WristZ;fist=hand.Pose(0,0,"screwdriver");
   var knuckle=Vector3.Lerp(fist[1].Translation,fist[2].Translation,.3f);
   Check((axis.X-knuckle.X)*(right?-1:1)>.004f&&MathF.Abs(axis.Y-knuckle.Y)<1e-4f&&MathF.Abs(axis.Z-knuckle.Z)<1e-4f,$"{side} {scale}: handle not beside the index finger: {axis} / {knuckle}");
   Check(hand.ScrewdriverThumbError<.008f,$"{side} {scale}: thumb does not reach the top of the handle: {hand.ScrewdriverThumbError}");
   var thumbPad=hand.RimPads(fist)[4]-Vector3.UnitZ*NativeHandMesh.WristZ;
   Check((thumbPad.X-axis.X)*(right?-1:1)>-.004f,$"{side} {scale}: thumb under the handle instead of on top: {thumbPad} / {axis}");
   // 0.1.111: a natural thumb: gently, evenly curled (no joint bent over 50 degrees).
   var t0=fist[12].Translation;var t1=fist[13].Translation;var t2=fist[14].Translation;
   float Bent(Vector3 a,Vector3 b)=>MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)),-1,1))*180/MathF.PI;
   Check(Bent(t1-t0,t2-t1)<50&&Bent(t2-t1,thumbPad-t2)<50,$"{side} {scale}: thumb bent unnaturally: {Bent(t1-t0,t2-t1):F0}/{Bent(t2-t1,thumbPad-t2):F0} degrees");
   var thicker=hand.ScrewdriverContact(.014f)-Vector3.UnitZ*NativeHandMesh.WristZ;
   Check(MathF.Abs(MathF.Abs(thicker.X-axis.X)-.003f)<.0005f,"handle thickness ignored");hand.ScrewdriverContact(.008f);
  }
  Console.WriteLine("PASS: key bow contact avoids hole/blade across imported rotations/scales; 12 outfit/hand/key-card pinch fixtures, opposing skin pads, forward blade under controller rotation, fixed pose under buttons, rotated item/hand contact, key/card/ashtray cache transitions; lockpick screwdriver fist closed and fixed, handle beside the curled index, thumb pad on top with a natural, evenly curled thumb, thickness-aware. Synthetic meshes, not game rendering.");
  void Box(Vector3 min,Vector3 max){int start=vertices.Count;for(int i=0;i<8;i++)vertices.Add(new((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z));foreach(int i in new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5})triangles.Add(start+i);}
 }
}
