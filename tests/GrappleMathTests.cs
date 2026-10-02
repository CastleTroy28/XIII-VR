using System;using XiiiXR;
class GrappleMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  Check(GrappleMath.ClimbScale(2)==2&&GrappleMath.DefaultClimbScale==2,"twice as fast by default");
  Check(GrappleMath.ClimbScale(10)==4&&GrappleMath.ClimbScale(.1f)==.5f&&GrappleMath.ClimbScale(float.NaN)==2&&GrappleMath.ClimbScale(0)==2&&GrappleMath.ClimbScale(-3)==2,"clamped / fallback");
  Console.WriteLine("PASS: rope climbing speed x2 by default, 0.5-4 from the config.");
 }
}
