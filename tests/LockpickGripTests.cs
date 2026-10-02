using System;using System.Collections.Generic;using System.Linq;using System.Numerics;using XiiiXR;
class LockpickGripTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 // Handle: 12-sided cylinder r=8 mm, 80 mm long along +Y; pick: 2x2 mm box 70 mm beyond it.
 static (Vector3[] v,int[] t) Mesh(Quaternion turn,float scale)
 {
  var v=new List<Vector3>();var t=new List<int>();
  const int n=12;
  for(int ring=0;ring<2;ring++)for(int i=0;i<n;i++){float a=i*MathF.PI*2/n;v.Add(new Vector3(MathF.Cos(a)*.008f,ring*.08f,MathF.Sin(a)*.008f));}
  for(int i=0;i<n;i++){int a=i,b=(i+1)%n,c=n+i,d=n+(i+1)%n;t.AddRange(new[]{a,c,b,b,c,d});}
  int cap0=v.Count;v.Add(new Vector3(0,0,0));int cap1=v.Count;v.Add(new Vector3(0,.08f,0));
  for(int i=0;i<n;i++){t.AddRange(new[]{cap0,i,(i+1)%n});t.AddRange(new[]{cap1,n+(i+1)%n,n+i});}
  int s=v.Count;float h=.001f;
  foreach(var y in new[]{.08f,.15f})foreach(var x in new[]{-h,h})foreach(var z in new[]{-h,h})v.Add(new Vector3(x,y,z));
  int[] box={0,1,2,1,3,2,4,6,5,5,6,7,0,4,1,1,4,5,2,3,6,3,7,6,0,2,4,2,6,4,1,5,3,3,5,7};
  foreach(int i in box)t.Add(s+i);
  return (v.Select(p=>Vector3.Transform(p*scale,turn)).ToArray(),t.ToArray());
 }
 static void Main()
 {
  // Imported models are axis aligned (any 90-degree orientation).
  foreach(var turn in new[]{Quaternion.Identity,Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI/2),Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-MathF.PI/2),Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI)})
  foreach(float scale in new[]{1f,.2f,3.5f})
  {
   var (v,t)=Mesh(turn,scale);
   var g=KeyGripGeometry.Fit(v,t,false,true);
   var fitted=v.Select(p=>Vector3.Transform(p,g.fit)).ToArray();
   float zMin=fitted.Min(p=>p.Z),zMax=fitted.Max(p=>p.Z);
   Check(MathF.Abs(zMax-zMin-.15f)<.002f,"lockpick not fitted to 15 cm: "+(zMax-zMin));
   // The handle (wide) is at the wrist end, the thin pick points forward.
   float Width(Func<Vector3,bool> f){var s=fitted.Where(f).ToArray();return s.Max(p=>p.X)-s.Min(p=>p.X);}
   Check(Width(p=>p.Z<zMin+.03f)>Width(p=>p.Z>zMax-.03f)*3,"pick points back into the hand");
   Check(g.edge.Z>.025f&&g.edge.Z<.045f,"pinch not in the middle of the handle: "+g.edge.Z);
   Check(g.thickness>.008f&&g.thickness<=.025f,"handle thickness "+g.thickness);
   Check(MathF.Abs(g.edge.X)<.01f,"pinch off the handle axis");
   var (hs,he)=KeyGripGeometry.PickHandle(fitted,t,.15f);
   Check(hs<.01f&&he>.07f&&he<.09f,$"handle range wrong: {hs}..{he}");
  }
  var key=KeyGripGeometry.Fit(Mesh(Quaternion.Identity,1).v,Mesh(Quaternion.Identity,1).t,false);
  Check(key.edge.Z<.015f,"key default changed");
  Console.WriteLine("PASS: lockpick fitted 15 cm, handle pinched in its middle at the wrist end, pick forward, across rotations/scales; key fit unchanged.");
 }
}
