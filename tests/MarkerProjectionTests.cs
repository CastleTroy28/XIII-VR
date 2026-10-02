using System;
using System.Numerics;
using XiiiXR;
class MarkerProjectionTests
{
 static void Require(bool b,string text){if(!b)throw new Exception(text);}
 static void Main(){
 var p=MarkerProjection.Place(new Vector3(0,0,10));Require(!p.edge&&p.x==0&&p.y==0,"front target not centered");
 p=MarkerProjection.Place(new Vector3(1,0,10));Require(!p.edge&&Math.Abs(p.x-.17f)<1e-5,"angular direction lost");
 foreach(var v in new[]{new Vector3(100,2,1),new Vector3(-100,-2,1),new Vector3(0,10,1),new Vector3(0,0,-1),new Vector3(0,0,0)}){
 p=MarkerProjection.Place(v);Require(float.IsFinite(p.x)&&float.IsFinite(p.y)&&Math.Abs(p.x)<=.96001f&&Math.Abs(p.y)<=.49001f,"marker outside panel or NaN");}
 p=MarkerProjection.Place(new Vector3(100,0,1));Require(p.edge&&p.x>.9f&&Math.Abs(p.angle-90)<1e-4,"right arrow on wrong side");
 p=MarkerProjection.Place(new Vector3(-100,0,1));Require(p.edge&&p.x<-.9f,"left arrow on wrong side");
 Console.WriteLine("PASS marker angular projection, edges, behind and coincident targets.");
 }
}
