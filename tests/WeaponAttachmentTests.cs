using System;using System.Linq;using System.Numerics;using XiiiXR;
class WeaponAttachmentTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // 0.1.194: the silenced pistol is fitted as the plain one (the silencer left out).
  Check(WeaponGeometry.Attachment("wpn_pistol_silencer_BND_JNT")&&WeaponGeometry.Attachment("Suppressor")&&!WeaponGeometry.Attachment("wpn_pistol_slide_BND_JNT")&&!WeaponGeometry.Attachment(null),"silencer bones by name");
  var rnd=new Random(5);
  // A pistol: slide/frame box (x .049, y .040, z .079), the silencer a tube ahead of it.
  Vector3[] Gun(bool shown,out bool[] attached)
  {
   var p=new System.Collections.Generic.List<Vector3>();var a=new System.Collections.Generic.List<bool>();
   for(int i=0;i<600;i++){p.Add(new Vector3((float)rnd.NextDouble()*.049f-.0245f,(float)rnd.NextDouble()*.040f-.030f,(float)rnd.NextDouble()*.079f-.079f));a.Add(false);}
   for(int i=0;i<300;i++){var q=shown?new Vector3((float)rnd.NextDouble()*.012f-.006f+.004f,(float)rnd.NextDouble()*.012f-.006f,(float)rnd.NextDouble()*.022f):new Vector3(0,0,-.002f);p.Add(q);a.Add(true);}
   attached=a.ToArray();return p.ToArray();
  }
  var plain=Gun(false,out var pa);var silenced=Gun(true,out var sa);
  Check(!WeaponGeometry.WithoutAttachment(plain,pa,.10f,out _,out _,out _,out _),"the plain pistol (silencer hidden, a point) trimmed");
  Check(WeaponGeometry.WithoutAttachment(silenced,sa,.10f,out var min,out var max,out var amin,out var amax),"the silenced pistol not fitted without its silencer");
  var size=max-min;
  Check(Math.Abs(size.X-.049f)<.002f&&Math.Abs(size.Y-.040f)<.002f&&Math.Abs(size.Z-.079f)<.002f,"the silenced pistol's fitted size is not the plain one's: "+size);
  var a=WeaponGeometry.Fit(min,max,"pistol");var b=WeaponGeometry.Fit(new Vector3(-.0245f,-.030f,-.079f),new Vector3(.0245f,.010f,0),"pistol");
  Check(Math.Abs(a.Scale-b.Scale)/b.Scale<.03f&&Vector3.Distance(a.Translation,b.Translation)<.004f,"the silenced pistol lies other than the plain one in the hand");
  Check(amax.Z>max.Z+.015f,"the silencer is not ahead of the muzzle");
  Check(!WeaponGeometry.WithoutAttachment(new Vector3[0],new bool[0],.1f,out _,out _,out _,out _)&&!WeaponGeometry.WithoutAttachment(plain,new bool[3],.1f,out _,out _,out _,out _),"bad input trimmed");
  Console.WriteLine("PASS: 0.1.194 the silenced pistol fitted as the plain one (same size and handle), the silencer ahead; the plain pistol (its silencer hidden) untouched.");
  // 0.1.228: the bazooka fitted without its rocket at its usual scale: the same
  // size and handle wherever the game holds the rocket (the tube 0.263 long in
  // the barrel frame; its rocket's head 13.9 cm ahead of it, or 4.4 cm).
  {
   Vector3[] Bazooka(float ahead,out bool[] rocket)
   {
    var p=new System.Collections.Generic.List<Vector3>();var a=new System.Collections.Generic.List<bool>();
    for(int i=0;i<800;i++){p.Add(new Vector3((float)rnd.NextDouble()*.08f-.04f,(float)rnd.NextDouble()*.135f-.115f,(float)rnd.NextDouble()*.263f-.263f));a.Add(false);}
    for(int i=0;i<300;i++){p.Add(new Vector3((float)rnd.NextDouble()*.03f-.015f,(float)rnd.NextDouble()*.03f-.015f,ahead-(float)rnd.NextDouble()*.27f));a.Add(true);}
    rocket=a.ToArray();return p.ToArray();
   }
   var far=Bazooka(.139f,out var fr);var near=Bazooka(.044f,out var nr);
   bool trimmedFar=WeaponGeometry.WithoutAttachment(far,fr,0,out var fmin,out var fmax,out _,out _);bool trimmedNear=WeaponGeometry.WithoutAttachment(near,nr,0,out var nmin,out var nmax,out _,out _);
   Check(trimmedFar&&trimmedNear,"the bazooka not fitted without its rocket");
   var f1=WeaponGeometry.Fit(fmin,fmax,"bazooka",WeaponGeometry.BazookaScale);var f2=WeaponGeometry.Fit(nmin,nmax,"bazooka",WeaponGeometry.BazookaScale);
   var grip=new Vector3(0,-.06f,-.06f);
   Check(f1.Scale==f2.Scale&&MathF.Abs(f1.Scale-1.1f/.402614f)<1e-4f&&Vector3.Distance(f1.Point(grip),f2.Point(grip))<.006f,"the bazooka's handle not at one place and size whatever its rocket's place");
   // Fitted by its length with the rocket in, the second came out a third bigger.
   static Vector3 Min(Vector3[] p)=>new(p.Min(v=>v.X),p.Min(v=>v.Y),p.Min(v=>v.Z));static Vector3 Max(Vector3[] p)=>new(p.Max(v=>v.X),p.Max(v=>v.Y),p.Max(v=>v.Z));
   var old1=WeaponGeometry.Fit(Min(far),Max(far),"bazooka");var old2=WeaponGeometry.Fit(Min(near),Max(near),"bazooka");
   Check(old2.Scale/old1.Scale>1.25f,"the old fit was not rocket-dependent: "+old1.Scale+" "+old2.Scale);
   Check(MathF.Abs(WeaponGeometry.Fit(fmin,fmax,"bazooka").Scale-1.1f/(fmax.Z-fmin.Z))<1e-4f,"the fit by length changed");
  }
  Console.WriteLine("PASS: 0.1.228 the bazooka fitted without its rocket at its usual scale: one size and handle place wherever the game holds its rocket (fitted by its length, a second one came out a third bigger).");
 }
}
