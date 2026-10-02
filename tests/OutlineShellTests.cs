using System;using System.Linq;using System.Numerics;using System.Collections.Generic;using XiiiXR;
class OutlineShellTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // A cube with split (hard-edge) normals: 6 faces x 4 corners.
  var pts=new List<Vector3>();var nrm=new List<Vector3>();var tri=new List<int>();
  var axes=new[]{Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ};
  foreach(var a in axes)
  {
   var u=MathF.Abs(a.Y)>.5f?Vector3.UnitX:Vector3.UnitY;var v=Vector3.Cross(a,u);int b=pts.Count;
   foreach(var (su,sv) in new[]{(-1f,-1f),(1f,-1f),(1f,1f),(-1f,1f)}){pts.Add((a+u*su+v*sv)*.02f);nrm.Add(a);}
   tri.AddRange(new[]{b,b+1,b+2,b,b+2,b+3});
  }
  var (shell,faces)=OutlineShell.Build(pts,nrm,tri,.003f);
  Check(shell.Length==pts.Count&&faces.Length==tri.Count,"shell size");
  for(int i=0;i<pts.Count;i++)Check(shell[i].Length()>pts[i].Length()+.0025f&&float.IsFinite(shell[i].X),"a shell point not outside the part");
  // Welded: the three copies of each corner move to the same place (no crack at hard edges).
  for(int i=0;i<pts.Count;i++)for(int j=0;j<pts.Count;j++)if(Vector3.Distance(pts[i],pts[j])<1e-6f)Check(Vector3.Distance(shell[i],shell[j])<1e-6f,"hard edge splits the outline");
  // Winding reversed.
  Check(faces[0]==tri[0]&&faces[1]==tri[2]&&faces[2]==tri[1],"winding not reversed");
  // Missing normals fall back to the direction from the centre; bad indices dropped.
  var (s2,f2)=OutlineShell.Build(pts,null,new List<int>{0,1,2,0,1,999},.003f);
  Check(f2.Length==3&&s2.All(p=>float.IsFinite(p.X)),"missing normals or bad indices");
  Check(OutlineShell.Build(new List<Vector3>(),null,new List<int>(),.003f).vertices.Length==0,"empty part");
  // Blink: dim..full, repeating.
  float lo=1,hi=0;for(float t=0;t<2;t+=.01f){float b=OutlineShell.Blink(t);lo=MathF.Min(lo,b);hi=MathF.Max(hi,b);}
  Check(MathF.Abs(lo-OutlineShell.Dim)<.01f&&hi>.99f,"blink range");
  Console.WriteLine("PASS: reload outline: inside-out shell just outside the part, welded at hard edges, reversed winding, safe without normals; blinking dim to full white.");
 }
}
