using System;using XiiiXR;
class BallisticMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // 0.1.194: a crossbow bolt under gravity (and drag) reaches the sighted point.
  foreach(float drag in new[]{0f,.05f,.3f})foreach(float dist in new[]{5f,25f,60f,110f})foreach(float up in new[]{-.4f,0,.3f})
  {
   float speed=60;var dir=(x:MathF.Sqrt(1-up*up)*.8f,y:up,z:MathF.Sqrt(1-up*up)*.6f);
   float dx=dir.x*dist,dy=dir.y*dist,dz=dir.z*dist;
   var v=BallisticMath.Launch(dx,dy,dz,speed,0,-9.81f,0,drag);
   Check(v!=null,"a normal crossbow shot left uncorrected d="+dist+" drag="+drag);
   var p=BallisticMath.At(v!.Value.x,v.Value.y,v.Value.z,0,-9.81f,0,drag,dist/speed);
   float miss=MathF.Sqrt((p.x-dx)*(p.x-dx)+(p.y-dy)*(p.y-dy)+(p.z-dz)*(p.z-dz));
   Check(miss<.01f,"the bolt misses the sighted point by "+miss+" m at "+dist+" m (drag "+drag+")");
   Check(v.Value.y>dy/(dist/speed)-1e-4f,"gravity not compensated upward");
   var plain=BallisticMath.At(dx/dist*speed,dy/dist*speed,dz/dist*speed,0,-9.81f,0,drag,dist/speed);
   if(dist>=60)Check(dy-plain.y>.5f,"the uncorrected bolt was expected to fall well below a far target");
  }
  Check(BallisticMath.Launch(0,0,.2f,60,0,-9.81f,0,0)==null,"a point at the muzzle corrected");
  Check(BallisticMath.Launch(0,0,1000,60,0,-9.81f,0,0)==null,"a shot beyond 3 s of flight corrected");
  Check(BallisticMath.Launch(0,0,30,.5f,0,-9.81f,0,0)==null,"a dropped bolt corrected");
  Check(BallisticMath.Launch(float.NaN,0,30,60,0,-9.81f,0,0)==null&&BallisticMath.Launch(0,0,30,60,0,-9.81f,0,float.NaN)==null,"bad numbers corrected");
  var near=BallisticMath.Launch(0,0,10,60,0,-9.81f,0,0)!.Value;
  Check(Math.Abs(near.x)<1e-5f&&Math.Abs(near.z-60)<1e-3f&&near.y>0&&near.y<1,"a near shot changed more than a small lift");
  Check(SpreadPolicy.Multiplier(true,false,"crossbow",.3f)==0&&SpreadPolicy.Multiplier(true,true,"crossbow",.3f)==0,"the crossbow still spreads");
  Check(SpreadPolicy.Multiplier(false,false,"crossbow",.3f)==1,"an NPC crossbow changed");
  Console.WriteLine("ballistic checks passed");
 }
}
