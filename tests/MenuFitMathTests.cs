using System;using XiiiXR;
class MenuFitMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // Five buttons 60 high, 20 apart: 380; a sixth: 460. Gaps (5) shrink by 16 each: 380, no scaling.
  var (s,k)=MenuFitMath.Plan(380,460,20,5);Check(Math.Abs(s-4)<1e-3f&&k==1,"gaps not taken first "+s+" "+k);
  // No gaps to give: the list scaled to its old height.
  (s,k)=MenuFitMath.Plan(300,360,0,5);Check(s==0&&Math.Abs(k-300f/360)<1e-3f,"not scaled to its room "+k);
  // Gaps part of the way, then the scale for the rest.
  (s,k)=MenuFitMath.Plan(300,380,10,5);Check(s==0&&Math.Abs(k-300f/330)<1e-3f,"gaps then scale "+s+" "+k);
  // Never tiny; a list that did not grow is left alone; bad input.
  (s,k)=MenuFitMath.Plan(100,400,0,5);Check(k==MenuFitMath.MinScale,"scaled below the minimum");
  (s,k)=MenuFitMath.Plan(380,380,20,5);Check(s==20&&k==1,"a list with room changed");
  (s,k)=MenuFitMath.Plan(float.NaN,400,20,5);Check(s==20&&k==1,"bad input changed the list");
  Console.WriteLine("PASS: a menu list with the VR SETTINGS button keeps its room: its gaps shrink first, then the list scales down (not below 72 %), a list with room left alone.");
 }
}
