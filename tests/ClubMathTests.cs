using System;using System.Numerics;using XiiiXR;
class ClubMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Near(Vector3 a,Vector3 b,float tol,string s){Check(Vector3.Distance(a,b)<=tol,s+" (got "+a+" expected "+b+")");}
 static void Main()
 {
  // 0.1.196: which guns are held by the barrel.
  foreach(var p in new[]{"ak47","m16","shotgun","sniper","m60","rifle","heavy"})Check(ClubMath.Clubbable(p),p+" cannot be held by its barrel");
  foreach(var p in new[]{"pistol","revolver","uzi","crossbow","bazooka","knife","grenade","prop",""})Check(!ClubMath.Clubbable(p),p+" held by a barrel");
  // Where along the barrel the fist is (fitted frame: +Z to the muzzle).
  var muzzle=new Vector3(0,.04f,.5f);float ak=.85f;
  Check(Math.Abs(ClubMath.MaxAlong(ak)-.34f)<1e-4f&&Math.Abs(ClubMath.MaxAlong(1.1f)-ClubMath.MaxFromMuzzle)<1e-4f,"the barrel's front not 40 % of the gun (at most 35 cm)");
  Check(ClubMath.Along(new Vector3(0,.04f,.42f),muzzle,ak,out float a)&&Math.Abs(a-.08f)<1e-4f,"a hand 8 cm behind the muzzle not on the barrel: "+a);
  Check(ClubMath.Along(new Vector3(.06f,-.02f,.40f),muzzle,ak,out a)&&Math.Abs(a-.10f)<1e-4f,"a hand 8 cm off the barrel's line (a fist round it) not on the barrel");
  Check(ClubMath.Along(new Vector3(0,.04f,.52f),muzzle,ak,out a)&&Math.Abs(a-ClubMath.MinFromMuzzle)<1e-4f,"a hand just past the muzzle not held at the muzzle (the muzzle under the little finger)");
  Check(!ClubMath.Along(new Vector3(.16f,.04f,.42f),muzzle,ak,out _),"a hand 16 cm beside the barrel takes it");
  Check(!ClubMath.Along(new Vector3(0,.04f,.60f),muzzle,ak,out _),"a hand 10 cm beyond the muzzle takes it");
  Check(!ClubMath.Along(new Vector3(0,-.02f,.05f),muzzle,ak,out _),"the handle / the receiver taken for the barrel");
  Check(!ClubMath.Along(new Vector3(float.NaN,0,0),muzzle,ak,out _),"a lost hand takes the barrel");
  // Nearer the muzzle than the support grip's fore-end point (world): the barrel, not the fore-end.
  var front=new Vector3(0,1,.45f);var socket=new Vector3(0,1,.26f);
  Check(ClubMath.NearerFront(new Vector3(0,1,.41f),front,socket)&&!ClubMath.NearerFront(new Vector3(0,1,.30f),front,socket),"the fore-end grip and the barrel's front mixed up");
  Check(ClubMath.NearerFront(new Vector3(0,1,.30f),front,new Vector3(float.NaN,0,0)),"without a fore-end point the barrel refused");
  Near(ClubMath.GrabPoint(muzzle,.08f),new Vector3(0,.04f,.42f),1e-5f,"the fist's point not on the barrel");
  Near(ClubMath.StockEnd(muzzle,ak),new Vector3(0,.04f,-.35f),1e-5f,"the stock's end not at the gun's rear");
  Console.WriteLine("PASS: long guns (not pistols, the Uzi, the crossbow, the bazooka) are held by the barrel's front (5..35 cm from the muzzle, within 10 cm of it), nearer the muzzle than the fore-end grip.");
  // In the fist: muzzle down, stock up leaning forward, sights forward (the hand's aim frame).
  var turn=ClubMath.Turn;
  var toMuzzle=Vector3.Transform(Vector3.UnitZ,turn);var up=Vector3.Transform(Vector3.UnitY,turn);var stock=-toMuzzle;
  float tilt=ClubMath.TiltDegrees*MathF.PI/180;
  Near(stock,new Vector3(0,MathF.Cos(tilt),MathF.Sin(tilt)),1e-4f,"the stock not up above the fist (leaning forward by the tilt)");
  Check(toMuzzle.Y<-.9f&&toMuzzle.Z<0,"the muzzle not down under the little finger");
  Check(up.Z>.9f,"the sights do not face forward");
  // Unity's Euler(90+tilt,0,0) as a quaternion: the same numbers.
  float h=(90+ClubMath.TiltDegrees)*MathF.PI/360;
  Check(Math.Abs(turn.X-MathF.Sin(h))<1e-5f&&Math.Abs(turn.W-MathF.Cos(h))<1e-5f&&Math.Abs(turn.Y)<1e-6f&&Math.Abs(turn.Z)<1e-6f,"the turn not a pitch about the hand's x");
  // Placed in the fist: the barrel's point there, the stock's end high above it on a stretched arm.
  foreach(var yaw in new[]{0f,1.2f,-2.5f})
  {
   var aim=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);var fist=new Vector3(.2f,1.3f,.5f);
   var rotation=aim*turn;var grab=ClubMath.GrabPoint(muzzle,.08f);var origin=fist-Vector3.Transform(grab,rotation);
   Near(origin+Vector3.Transform(grab,rotation),fist,1e-5f,"the barrel not in the fist");
   var end=origin+Vector3.Transform(ClubMath.StockEnd(muzzle,ak),rotation);var tip=origin+Vector3.Transform(muzzle,rotation);
   Check(end.Y-fist.Y>.70f,"the stock's end not high above the fist: "+(end.Y-fist.Y));
   Check(tip.Y<fist.Y-.07f&&Vector3.Distance(tip,fist)<.09f,"the muzzle not just under the fist");
  }
  // Turning into the fist: eased, a fifth of a second.
  Check(ClubMath.Blend(0)==0&&ClubMath.Blend(ClubMath.TurnSeconds)==1&&ClubMath.Blend(5)==1&&Math.Abs(ClubMath.Blend(ClubMath.TurnSeconds/2)-.5f)<1e-5f&&ClubMath.Blend(float.NaN)==1&&ClubMath.Blend(-1)==0,"the turn into the fist not eased over its time");
  float last=-1;for(int i=0;i<=20;i++){float b=ClubMath.Blend(ClubMath.TurnSeconds*i/20);Check(b>=last,"the turn goes back");last=b;}
  // A club hits like a broom or a shovel: 5 (light) .. 3 (hard) blows, never fewer than 3.
  Check(MeleeDamageMath.PropHits(0)==5&&MeleeDamageMath.PropHits(1)==3&&MeleeDamageMath.MinPropBlows==3,"a club's blows not as a broom's");
  // 0.1.200: the barrel's thickness where the fist holds it.
  {
   var at=new Vector3(0,.04f,.30f);var pts=new System.Collections.Generic.List<Vector3>();
   void Ring(Vector3 c,float r,float z0,float z1){for(float z=z0;z<=z1+1e-6f;z+=.004f)for(int k=0;k<12;k++){float a=k*MathF.PI/6;pts.Add(new Vector3(c.X+r*MathF.Cos(a),c.Y+r*MathF.Sin(a),z));}}
   Ring(at,.008f,.20f,.45f);                                   // the M4's barrel, 1.6 cm
   Ring(new Vector3(0,.04f-.035f,0),.020f,.10f,.32f);           // its grenade launcher's tube below (its top 1.5 cm under the barrel's line)
   pts.Add(new Vector3(.3f,.3f,.3f));                          // something far off
   float m4=ClubMath.Thickness(pts,at);
   Check(Math.Abs(m4-.016f)<.002f,"the M4's barrel thickness (with the launcher below it): "+m4);
   var dbl=new System.Collections.Generic.List<Vector3>();pts=dbl;
   Ring(new Vector3(-.0095f,.04f,0),.009f,.20f,.45f);Ring(new Vector3(.0095f,.04f,0),.009f,.20f,.45f);   // side by side, 3.8 cm across
   float side=ClubMath.Thickness(dbl,at);
   Check(side>.026f,"two barrels side by side taken for a thin one: "+side);
   Check(float.IsNaN(ClubMath.Thickness(new[]{at,at+Vector3.UnitX*.001f},at))&&float.IsNaN(ClubMath.Thickness(dbl,new Vector3(float.NaN,0,0))),"a thickness from too few points");
  }
  Console.WriteLine("PASS: 0.1.200 a barrel's thickness where it is held: the M4's 1.6 cm (its grenade launcher below does not count), two barrels side by side not thin, too few points: none.");
  Console.WriteLine("PASS: the gun in the fist: muzzle just under the little finger, the stock's end high above (the arm stretched), sights forward, whatever the hand's turn; turns into the fist eased in 0.2 s; hits like a broom (5..3 blows).");
 }
}
