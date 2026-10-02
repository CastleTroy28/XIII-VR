using System;using System.Numerics;using XiiiXR;
class ThrowImpactTests
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static void Main()
 {
  foreach(float dt in new[]{.01f,.02f,.033f})
  {
   var p=new Vector3(0,1.6f,0);var v=new Vector3(0,2,9);var initial=p;var launch=v;var g=new Vector3(0,-9.81f,0);
   for(int i=1;i<=100;i++)
   {ThrowTrajectory.Step(ref p,ref v,g,dt);var expected=initial+launch*(i*dt)+g*(dt*dt*i*(i+1)*.5f);Check(Vector3.Distance(p,expected)<.001f,"preview diverged from discrete rigidbody gravity");}
  }
  Check(ThrowTrajectory.Speed(float.NaN)==9&&ThrowTrajectory.Speed(0)==9&&ThrowTrajectory.Speed(12)==12,"invalid projectile speed leaked");
  var policy=new WeaponImpactDamage();var counts=new int[4];
  for(int target=0;target<300;target++)
  {
   float hp=100;int hits=0;
   while(hp>0&&hits<5){float mult=policy.Multiplier(target,hp,20,1,.7f);hp-=mult*20*.7f;hits++;}
   Check(hits>=1&&hits<=3,"weapon strike did not finish in 1–3 confirmed contacts");counts[hits]++;
  }
  Check(counts[1]>0&&counts[2]>0&&counts[3]>0,"random target strike counts absent");
  Console.WriteLine("PASS: discrete preview/rigidbody gravity at three fixed timesteps, invalid launch speeds, 300 native-damage-scaled target sequences end in 1–3 weapon contacts.");
 }
}
