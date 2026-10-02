using System;using XiiiXR;
class WaterSplashMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  Check(WaterSplashMath.Slap(2,-.9f),"downward slap does not splash");
  Check(!WaterSplashMath.Slap(.6f,-1),"slow dip splashes");
  Check(!WaterSplashMath.Slap(3,.5f)&&!WaterSplashMath.Slap(3,-.1f),"hand leaving the water / skimming sideways splashes");
  Check(!WaterSplashMath.Slap(float.NaN,-1),"invalid speed splashes");
  Check(WaterSplashMath.Strength(1)<=.36f&&WaterSplashMath.Strength(4)>=.99f&&WaterSplashMath.Strength(10)==1,"strength range");
  int turn=0;Check(!WaterSplashMath.RecordingTurn(ref turn)&&WaterSplashMath.RecordingTurn(ref turn)&&!WaterSplashMath.RecordingTurn(ref turn)&&WaterSplashMath.RecordingTurn(ref turn),"splash sounds do not alternate");
  Console.WriteLine("PASS: water splash only for a hand moving down into the water at >= 1 m/s; strength grows with speed and is bounded; game sound and recording alternate.");
 }
}
