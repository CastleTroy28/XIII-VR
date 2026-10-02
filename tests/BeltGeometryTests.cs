using System;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Text.Json;
using XiiiXR;
class BeltGeometryTests
{
 static void Main()
 {
  var m=AmmoPouchGeometry.Build();
  foreach(var p in m.Vertices)if(!float.IsFinite(p.LengthSquared()))throw new Exception("nonfinite belt mesh");
  for(int i=0;i<m.Triangles.Count;i+=3)
  {var a=m.Vertices[m.Triangles[i]];var b=m.Vertices[m.Triangles[i+1]];var c=m.Vertices[m.Triangles[i+2]];
   if(Vector3.Cross(b-a,c-a).LengthSquared()<1e-18f)throw new Exception("degenerate belt triangle");}
  for(int i=0;i<8;i++)if(AmmoPouchGeometry.ShellPosition(i).Length()>.40f)throw new Exception("shell grab socket too far from bag");
  File.WriteAllText("xiii-xr/build/belt-mesh.json",JsonSerializer.Serialize(new{vertices=m.Vertices.Select(v=>new[]{v.X,v.Y,v.Z}),triangles=m.Triangles,colors=m.Colors.Select(c=>new[]{c.X,c.Y,c.Z,c.W})}));
  Console.WriteLine("PASS finite, nondegenerate belt and eight reachable shell sockets; geometry exported for visual inspection.");
 }
}
