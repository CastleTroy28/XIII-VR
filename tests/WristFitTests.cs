using System;using System.Numerics;using System.Collections.Generic;using System.Linq;using XiiiXR;
class WristFitTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  foreach(float angle in new[]{-.4f,0,.35f})foreach(float mirror in new[]{-1f,1f})
  {
   var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,angle);var offset=new Vector3(.011f*mirror,.019f,0);var points=new List<Vector3>();var tris=new List<int>();
   const int count=64;for(int j=0;j<2;j++)for(int i=0;i<count;i++)
   {float t=i*MathF.PI*2/count;points.Add(Vector3.Transform(offset+new Vector3(.023f*MathF.Cos(t),.017f*MathF.Sin(t),j==0?-.1f:0),rotation));}
   for(int i=0;i<count;i++){int n=(i+1)%count;tris.AddRange(new[]{i,n,n+count,i,n+count,i+count});}
   var fit=WristFitMath.Fit(points.ToArray(),new[]{tris.ToArray()},points.Select(_=>true).ToArray(),Vector3.Transform(new Vector3(0,0,-.28f),rotation));
   var expected=Vector3.Transform(offset+new Vector3(0,0,-.052f),rotation);
   Check(Vector3.Distance(fit.Center,expected)<1e-5f,"band retains old fixed center instead of native skin center");
   Check(Math.Abs(fit.RadiusX-.0245f)<.0001f&&Math.Abs(fit.RadiusY-.0185f)<.0001f,"band not fitted with clearance");
   for(int i=0;i<count;i++)
   {float t=i*MathF.PI*2/count;var skin=Vector3.Transform(offset+new Vector3(.023f*MathF.Cos(t),.017f*MathF.Sin(t),-.052f),rotation);var p=Vector3.Transform(skin-fit.Center,Quaternion.Inverse(fit.Rotation));Check(p.X*p.X/(fit.RadiusX*fit.RadiusX)+p.Y*p.Y/(fit.RadiusY*fit.RadiusY)<1,"strap intersects skin section");}
   var accessory=new[]{Vector3.Transform(new Vector3(-.02f,.03f,-.012f),rotation),Vector3.Transform(new Vector3(.02f,.04f,-.028f),rotation)};
   var elbow=Vector3.Transform(new Vector3(0,0,-.28f),rotation);
   float mount=WristFitMath.AccessoryDistance(accessory,new[]{true,true},elbow);
   Check(Math.Abs(mount-.020f)<1e-5f,"native watch mount not measured from original accessory");
   fit=WristFitMath.Fit(points.ToArray(),new[]{tris.ToArray()},points.Select(_=>true).ToArray(),elbow,mount);
   Check(Vector3.Distance(fit.Center,Vector3.Transform(offset+new Vector3(0,0,-.02f),rotation))<1e-5f,"display still mounted at old fixed 52 mm location");
  }
  foreach(float direction in new[]{-1f,1f})
  {
   var points=new List<Vector3>();var tri=new List<int>();
   for(int z=0;z<2;z++)for(int i=0;i<64;i++){float t=i*MathF.PI/32;points.Add(new Vector3(.02f*MathF.Cos(t),.016f*MathF.Sin(t),z==0?-.08f:0));}
   for(int i=0;i<64;i++){int j=(i+1)%64;tri.AddRange(new[]{i,j,j+64,i,j+64,i+64});}
   for(int i=0;i<16;i++)points.Add(new Vector3(direction*.029f,(i%4-1.5f)*.003f,-.04f+(i/4-1.5f)*.003f));
   var skin=Enumerable.Range(0,144).Select(i=>i<128).ToArray();var accessory=skin.Select(b=>!b).ToArray();
   var fit=new WristFit(new Vector3(0,0,-.04f),Quaternion.Identity,.025f,.020f);
   var mount=WristFitMath.AttachToNativeCase(fit,points.ToArray(),new[]{tri.ToArray()},skin,accessory);
   Check(mount.DisplayPoint.HasValue&&Vector3.Distance(mount.DisplayPoint.Value,new Vector3(direction*.0202f,0,-.04f))<.0001f,"case floats on ellipse / wrong side of strap");
   Check(Vector3.Distance(mount.DisplayNormal!.Value,new Vector3(direction,0,0))<.0001f,"native case side lost");
  }
  {
   // Off-centre case with a flat top: centroid radial direction is tilted,
   // while the authored top plane is level. Keep its measured lateral mount.
   var points=new List<Vector3>();var tri=new List<int>();
   for(int z=0;z<2;z++)for(int i=0;i<64;i++){float t=i*MathF.PI/32;points.Add(new Vector3(.03f*MathF.Cos(t),.022f*MathF.Sin(t),z==0?-.08f:0));}
   for(int i=0;i<64;i++){int j=(i+1)%64;tri.AddRange(new[]{i,j,j+64,i,j+64,i+64});}
   for(int row=0;row<4;row++)for(int col=0;col<4;col++)points.Add(new Vector3(-.007f+(col-1.5f)*.008f,.03f,-.04f+(row-1.5f)*.008f));
   for(int row=0;row<3;row++)for(int col=0;col<3;col++){int a=128+row*4+col;tri.AddRange(new[]{a,a+4,a+5,a,a+5,a+1});}
   var skin=System.Linq.Enumerable.Range(0,144).Select(i=>i<128).ToArray();var accessory=skin.Select(x=>!x).ToArray();
   var fit=new WristFit(new Vector3(0,0,-.04f),Quaternion.Identity,.0315f,.0235f);
   var result=WristFitMath.AttachToNativeCase(fit,points.ToArray(),new[]{tri.ToArray()},skin,accessory);
   Check(result.DisplayNormal.HasValue&&Vector3.Dot(result.DisplayNormal.Value,Vector3.UnitY)>.999f,"asymmetric wrist rolls a flat authored watch face");
   Check(Math.Abs(result.DisplayPoint!.Value.X+.007f)<.00001f,"native case lateral attachment was lost");
  }
  bool rejected=false;try{WristFitMath.Fit(Array.Empty<Vector3>(),Array.Empty<int[]>(),Array.Empty<bool>(),new Vector3(0,0,-1));}catch(InvalidOperationException){rejected=true;}Check(rejected,"missing section produces invalid bracelet");
  Console.WriteLine("PASS: off-center mirrored/rotated native wrist sections fit with 1.5 mm clearance; no fixed-size hanging ring; missing section rejected. Synthetic mesh, not final game fit.");
 }
}
