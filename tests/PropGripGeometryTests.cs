using System;using System.Linq;using System.Collections.Generic;using System.Numerics;using XiiiXR;
class PropGripGeometryTests{
 static void Check(bool x,string text){if(!x)throw new Exception(text);}
 static void Main(){SurfaceFixtures();var min=new Vector3(-.04f,-.03f,-.005f);var max=-min;var tray=PropGripGeometry.Fit("prop_wpn_ms_ashtray",min,max);
 Check(Vector3.Dot(Vector3.Transform(Vector3.UnitZ,tray.rotation),Vector3.UnitY)>.999f,"tray face points down");
 Check(Vector3.Transform((min+max)*.5f-tray.handle,tray.rotation).Z>0,"tray extends back into wrist");
 var chair=PropGripGeometry.Fit("prop_wpn_ms_chair",new(-.10f,-.12f,-.165f),new(.10f,.12f,.165f));
 Check(Vector3.Dot(Vector3.Transform(-Vector3.UnitZ,chair.rotation),Vector3.UnitZ)>.999f,"chair feet axis points sideways");
 Check(!PropGripGeometry.Controlled("prop_wpn_ms_broom")&&!PropGripGeometry.Controlled("pistol"),"new fitting leaks to other weapons");
 Console.WriteLine("PASS: tray surface faces up and extends away from wrist; chair feet axis faces forward; other item profiles retain authored fitting. Rendered meshes/headset not available.");}
 static void SurfaceFixtures()
 {
  var v=new List<Vector3>();var t=new List<int>();
  // Seat depth reaches +Y, but the back rail is at -Y. The old max-Y
  // bounding corner floats a hand-width away from every triangle.
  Box(new(-.12f,-.1f,-.16f),new(.12f,.12f,-.13f));
  Box(new(-.12f,-.1f,.13f),new(.12f,-.08f,.165f));
  foreach(float scale in new[]{.2f,1f,3f})foreach(float x in new[]{0f,.31f})
  {
   var points=v.Select(p=>p*scale+new Vector3(x,0,0)).ToArray();
   Check(PropSurfaceGeometry.ChairRail(points,t.ToArray(),out var rail),"real chair rail not found");
   Check(Math.Abs(rail.X-x)<1e-5f&&rail.Y/scale>-.101f&&rail.Y/scale<-.079f&&rail.Z/scale>.13f,"chair grip falls in empty bounding-box corner");
  }
  v.Clear();t.Clear();Box(new(-.11f,0,0),new(.11f,.002f,.16f));
  Check(AshtrayRimGeometry.TryMeasure(v.ToArray(),t.ToArray(),0,out var rim,out var thickness)&&Math.Abs(rim.Y-.001f)<1e-5f&&thickness==.004f,"thin imported tray silently falls back to bounds");
  var sheet=new[]{new Vector3(-.1f,.017f,0),new Vector3(.1f,.017f,0),new Vector3(.1f,.017f,.16f),new Vector3(-.1f,.017f,.16f)};
  Check(AshtrayRimGeometry.TryMeasure(sheet,new[]{0,1,2,0,2,3},0,out rim,out thickness)&&Math.Abs(rim.Y-.017f)<1e-5f,"single-sided rim loses actual surface");
  foreach(float weight in new[]{0f,.1f,.5f,.94f,.96f,1f})
   Check(DistalSkinProbe.Follows(new Vector3(.02f*weight,0,0))==(weight>=.95f),"stripped-weight probe accepts neighbouring/blended finger");
  Check(!DistalSkinProbe.Follows(new Vector3(.02f,.01f,0))&&!DistalSkinProbe.Follows(new Vector3(float.NaN,0,0)),"invalid skin bake accepted");
  Console.WriteLine("PASS: hollow chair rail at the opposite depth, translated/scaled meshes; thin and single-sided ashtray surfaces; private-bake skin-weight inference.");
  void Box(Vector3 min,Vector3 max)
  {
   int start=v.Count;for(int i=0;i<8;i++)v.Add(new((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z));
   foreach(int i in new[]{0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5})t.Add(start+i);
  }
 }

}
