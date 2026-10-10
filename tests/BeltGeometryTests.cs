using System;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Text.Json;
using XiiiXR;
// 0.1.255: the ammunition belt (a 3D model made for the mods): worn round the waist (0.1.256: its pouch in the
// middle of the front, a row of shells on each hip), the reserve's shells in their loops (an empty loop's mouth
// for each used one), reachable.
class BeltGeometryTests
{
 static int checks;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static void Main(string[] args)
 {
  Check(BeltModel.Position.Length==BeltModel.Points*3&&BeltModel.Normal.Length==BeltModel.Points*3&&BeltModel.Tone.Length==BeltModel.Points&&BeltModel.Triangle.Length==BeltModel.Triangles*3&&BeltModel.ShellOf.Length==BeltModel.Triangles,"the belt's data sizes");
  Check(BeltModel.Triangle.All(i=>i<BeltModel.Points)&&BeltModel.Tone.All(t=>t*3<BeltModel.Palette.Length)&&BeltModel.ShellOf.All(s=>s>=-1&&s<BeltModel.Shells),"the belt's indices");
  Check(BeltModel.Shells==16&&BeltModel.Use.Distinct().Count()==16&&BeltModel.Use.All(i=>i<16),"sixteen shells, each used once");
  for(int s=0;s<BeltModel.Shells;s++)Check(BeltModel.ShellOf.Count(x=>x==s)>=6&&BeltModel.ShellRadius[s]>.007f&&BeltModel.ShellRadius[s]<.016f,"shell "+s+": its head ("+BeltModel.ShellOf.Count(x=>x==s)+" triangles, radius "+BeltModel.ShellRadius[s]+")");
  // worn: round the waist (the model's own size: a waist some 38 x 35 cm), at the old belt's height and middle
  var full=BeltModelMath.Build(99);var empty=BeltModelMath.Build(0);
  float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
  foreach(var p in full.Points){Check(float.IsFinite(p.LengthSquared()),"a point not finite");var q=p-BeltModelMath.Waist;minX=Math.Min(minX,q.X);maxX=Math.Max(maxX,q.X);minZ=Math.Min(minZ,q.Z);maxZ=Math.Max(maxZ,q.Z);}
  Check(maxX-minX>.38f&&maxX-minX<.62f&&maxZ-minZ>.36f&&maxZ-minZ<.62f,"the belt's size round the waist ("+(maxX-minX)+" x "+(maxZ-minZ)+" m)");
  foreach(var n in full.Normals)Check(Math.Abs(n.Length()-1)<1e-3f,"a normal not of unit length");
  // the faces outward (Unity draws the side the cross points to): the triangles' turn as their points' normals
  int agree=0,all=0;
  foreach(var tone in full.Tones)for(int t=0;t<tone.Length;t+=3){all++;var a=full.Points[tone[t]];var nn=full.Normals[tone[t]]+full.Normals[tone[t+1]]+full.Normals[tone[t+2]];if(Vector3.Dot(Vector3.Cross(full.Points[tone[t+1]]-a,full.Points[tone[t+2]]-a),nn)>0)agree++;}
  Check(agree>all*.95f,"belt triangles turned inward ("+agree+" of "+all+")");
  // 0.1.256: the pouch in the middle of the front; a row of eight shells on each hip, none in front of the pouch
  var pc=BeltModelMath.PouchCentre-BeltModelMath.Waist;
  Check(Math.Abs(pc.X)<.02f&&pc.Z>.18f&&pc.Z<.30f,"the pouch not in the middle of the front ("+pc+")");
  Check(BeltModelMath.AtPouch(BeltModelMath.PouchCentre)&&BeltModelMath.AtPouch(BeltModelMath.PouchCentre+new Vector3(0,.12f,0))&&!BeltModelMath.AtPouch(BeltModelMath.Waist+new Vector3(.22f,0,0))&&!BeltModelMath.AtPouch(BeltModelMath.Waist+new Vector3(-.22f,0,0)),"a hand at the pouch (above it too), not at the hips");
  int left=0,right=0;
  for(int s=0;s<16;s++)
  {
   var t=BeltModelMath.ShellTop(s)-BeltModelMath.Waist;if(t.X<0)left++;else right++;
   Check(t.Y>0&&t.Y<.08f,"shell "+s+"'s head at the belt's top ("+t.Y+")");
   Check(Math.Abs(t.X)>Math.Abs(pc.X)+BeltModelMath.PouchHalf.X,"shell "+s+" in front of the pouch ("+t+")");
   float side=MathF.Abs(MathF.Atan2(t.X,t.Z))*180/MathF.PI;Check(side>25&&side<110,"shell "+s+" not on a hip ("+side+" degrees from the front)");
  }
  Check(left==8&&right==8,"a row of eight shells on each hip ("+left+" left, "+right+" right)");
  // the first shown the easiest to reach (the front of the left hip): the left hip's eight, front to back, then the right's
  var reach=BeltModelMath.Waist+new Vector3(-.12f,.05f,.16f);
  for(int i=1;i<16;i++)Check(Vector3.Distance(BeltModelMath.ShellTop(BeltModel.Use[i-1]),reach)<=Vector3.Distance(BeltModelMath.ShellTop(BeltModel.Use[i]),reach)+1e-6f,"the shells not shown easiest first");
  for(int i=0;i<8;i++){var a=BeltModelMath.ShellTop(BeltModel.Use[i])-BeltModelMath.Waist;Check(a.X<0,"a right hip shell shown before the left hip's");if(i>0)Check(a.Z<BeltModelMath.ShellTop(BeltModel.Use[i-1]).Z-BeltModelMath.Waist.Z,"the left hip's shells not front to back");}
  // the reserve: its shells' heads, each used one an empty loop (a dark disc), the hands at the shown ones only
  int brass=full.Tones.Length-2;   // the brass tone (the last of the model's), the holes' after it
  for(int n=0;n<=16;n++)
  {
   var m=BeltModelMath.Build(n);
   int holes=m.Tones[^1].Length/3;
   Check(holes==(16-n)*12,n+" shells: "+(16-n)+" empty loops ("+holes/12+")");
   if(n>0)Check(m.Tones[brass].Length>=BeltModelMath.Build(n-1).Tones[brass].Length,n+" shells: fewer brass heads than "+(n-1));
  }
  Check(full.Tones[brass].Length>empty.Tones[brass].Length,"the used shells' brass heads not taken away");
  var shown=BeltModel.Use[0];var hidden=BeltModel.Use[15];
  Check(BeltModelMath.AtShell(BeltModelMath.ShellTop(shown)+new Vector3(0,.03f,0),1)&&!BeltModelMath.AtShell(BeltModelMath.ShellTop(hidden)+new Vector3(0,.03f,0),1)||Vector3.Distance(BeltModelMath.ShellTop(shown),BeltModelMath.ShellTop(hidden))<.1f,"a hand at a shown shell takes a round, at a used one's loop none");
  Check(!BeltModelMath.AtShell(BeltModelMath.ShellTop(shown),0),"no reserve: no round at the belt");
  Check(BeltModelMath.ShellShown(shown,1)&&!BeltModelMath.ShellShown(hidden,15)&&BeltModelMath.ShellShown(hidden,16),"which shells a reserve shows");
  Check(full.TriangleCount<60000&&full.Points.Length<65000,"the belt's size in triangles ("+full.TriangleCount+")");
  if(args.Contains("--preview"))
   File.WriteAllText("xiii-xr/build/belt-mesh.json",JsonSerializer.Serialize(new{points=full.Points.Select(v=>new[]{v.X,v.Y,v.Z}),tones=full.Tones,palette=full.Palette.Select(c=>new[]{c.X,c.Y,c.Z})}));
  Console.WriteLine("PASS: "+checks+" checks: the ammunition belt (a 3D model made for the mods) round the waist, its faces outward, the pouch in the middle of the front with a hand's reach round it, a row of eight shells on each hip, the left hip's shown first (front to back), the reserve's shells in their loops and the used ones' loops empty ("+full.TriangleCount+" triangles with all shells).");
 }
}
