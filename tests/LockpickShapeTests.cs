using System;using System.Collections.Generic;using System.Linq;using System.Numerics;using XiiiXR;
class LockpickShapeTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 // Closed box as a mesh piece (8 vertices, 12 triangles), optional duplicate seam vertices.
 static void Box(List<Vector3> v,List<int> t,Vector3 min,Vector3 max,bool seams=false)
 {
  int s=v.Count;
  for(int i=0;i<8;i++)v.Add(new Vector3((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z));
  int[] box={0,1,2,1,3,2,4,6,5,5,6,7,0,4,1,1,4,5,2,3,6,3,7,6,0,2,4,2,6,4,1,5,3,3,5,7};
  foreach(int i in box)t.Add(s+i);
  if(seams){int d=v.Count;v.Add(v[s+7]);t.AddRange(new[]{s+3,d,s+5});} // UV-seam copy of a corner
 }
 static void Main()
 {
  // Pick along Y: handle (16 mm thick) + shaft, one piece with a seam copy; two handle rings; the tension wrench beside it.
  var v=new List<Vector3>();var t=new List<int>();
  Box(v,t,new Vector3(-.008f,0,-.008f),new Vector3(.008f,.08f,.008f),true);
  Box(v,t,new Vector3(-.001f,.079f,-.001f),new Vector3(.001f,.15f,.001f));   // shaft touches the handle? separate piece
  var ringsV=new List<Vector3>();var ringsT=new List<int>();
  Box(ringsV,ringsT,new Vector3(-.009f,.02f,-.009f),new Vector3(.009f,.03f,.009f));
  Box(ringsV,ringsT,new Vector3(-.009f,.05f,-.009f),new Vector3(.009f,.06f,.009f));
  // Wrench as seen in the game: a bar ACROSS the handle (crossing its axis), with two bands on it.
  var wrenchV=new List<Vector3>();var wrenchT=new List<int>();
  Box(wrenchV,wrenchT,new Vector3(-.03f,.04f,-.001f),new Vector3(.03f,.043f,.001f));
  Box(wrenchV,wrenchT,new Vector3(-.025f,.039f,-.002f),new Vector3(-.02f,.044f,.002f));
  Box(wrenchV,wrenchT,new Vector3(.02f,.039f,-.002f),new Vector3(.025f,.044f,.002f));
  var parts=new List<(Vector3[] vertices,int[] triangles)>{(v.ToArray(),t.ToArray()),(ringsV.ToArray(),ringsT.ToArray()),(wrenchV.ToArray(),wrenchT.ToArray())};
  var keep=LockpickShape.Filter(parts,out string report);
  Check(keep[0]==null&&keep[1]==null,"pick/handle/rings changed: "+report);
  Check(keep[2]!=null&&keep[2]!.Length==0,"crossing tension wrench kept: "+report);
  // Wrench inside the same mesh as the pick: only its triangles go.
  var all=new List<Vector3>(v);var allT=new List<int>(t);int off=all.Count;all.AddRange(wrenchV);allT.AddRange(wrenchT.Select(i=>i+off));
  keep=LockpickShape.Filter(new List<(Vector3[],int[])>{(all.ToArray(),allT.ToArray())},out report);
  Check(keep[0]!=null&&keep[0]!.Length==t.Count&&keep[0]!.All(i=>i<off),"single-mesh wrench not removed: "+report);
  // Wrench running beside the pick (parallel, 1.6 cm off its axis) with a short foot.
  var besideV=new List<Vector3>();var besideT=new List<int>();
  Box(besideV,besideT,new Vector3(.015f,-.01f,-.001f),new Vector3(.018f,.05f,.001f));
  Box(besideV,besideT,new Vector3(.015f,-.012f,-.001f),new Vector3(.03f,-.009f,.001f));
  keep=LockpickShape.Filter(new List<(Vector3[],int[])>{(v.ToArray(),t.ToArray()),(ringsV.ToArray(),ringsT.ToArray()),(besideV.ToArray(),besideT.ToArray())},out report);
  Check(keep[0]==null&&keep[1]==null&&keep[2]!=null&&keep[2]!.Length==0,"parallel wrench kept or pick damaged: "+report);
  // Rotated model: same result.
  var turn=Quaternion.CreateFromYawPitchRoll(.6f,-1.2f,.3f);
  Vector3[] R(List<Vector3> l)=>l.Select(p=>Vector3.Transform(p,turn)).ToArray();
  keep=LockpickShape.Filter(new List<(Vector3[],int[])>{(R(v),t.ToArray()),(R(ringsV),ringsT.ToArray()),(R(wrenchV),wrenchT.ToArray())},out report);
  Check(keep[0]==null&&keep[1]==null&&keep[2]!=null&&keep[2]!.Length==0,"rotated model: "+report);
  // A lockpick without a wrench is unchanged.
  keep=LockpickShape.Filter(new List<(Vector3[],int[])>{(v.ToArray(),t.ToArray()),(ringsV.ToArray(),ringsT.ToArray())},out report);
  Check(keep.All(k=>k==null),"clean lockpick modified: "+report);
  Check(LockpickShape.Split(0,v.ToArray(),t.ToArray()).Count==2,"seam copies split a piece");
  Console.WriteLine("PASS: lockpick tension wrench dropped (crossing or parallel, separate renderer or same mesh, rotated), bands on it too; handle rings/shaft kept; seam welding; clean model untouched.");
 }
}
