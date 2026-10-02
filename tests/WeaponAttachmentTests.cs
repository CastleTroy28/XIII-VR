using System;using System.Numerics;using XiiiXR;
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
 }
}
