using System;using XiiiXR;
class CrossbowStringTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // 0.1.123: the string follows the bolt drawn back along the rail.
  const float nock=-.05f,tip=.25f;
  Check(CrossbowStringMath.Draw(nock,nock,tip)==1&&CrossbowStringMath.Draw(nock-.02f,nock,tip)==1,"bolt rear at the nock: string not cocked");
  Check(CrossbowStringMath.Draw(tip,nock,tip)==0&&CrossbowStringMath.Draw(tip+.1f,nock,tip)==0,"bolt ahead of the released string pulls it");
  Check(MathF.Abs(CrossbowStringMath.Draw(.10f,nock,tip)-.5f)<1e-5f,"half drawn");
  Check(CrossbowStringMath.Draw(float.NaN,nock,tip)==1&&CrossbowStringMath.Draw(0,.1f,.1f)==1,"degenerate draw");
  // Released: every point of the string moves forward to the tip line; cocked: none moves.
  Check(MathF.Abs(CrossbowStringMath.Shift(nock,tip,0)-.30f)<1e-5f&&CrossbowStringMath.Shift(nock,tip,1)==0&&MathF.Abs(CrossbowStringMath.Shift(nock,tip,.5f)-.15f)<1e-5f,"nock shift");
  Check(CrossbowStringMath.Shift(tip,tip,0)==0&&CrossbowStringMath.Shift(tip+.01f,tip,0)==0&&CrossbowStringMath.Shift(float.NaN,tip,0)==0,"points at the cams move");
  // Shown draw: let go at once, drawn quickly but not in one frame.
  Check(CrossbowStringMath.Follow(1,0,.011f)==0,"shot does not release the string at once");
  float f=CrossbowStringMath.Follow(0,1,.011f);Check(f>.1f&&f<.5f,"drawing jumps or lags: "+f);
  float s=0;for(int i=0;i<30;i++)s=CrossbowStringMath.Follow(s,1,.011f);Check(s>.99f,"string never reaches cocked: "+s);
  Check(CrossbowStringMath.Follow(.5f,float.NaN,.01f)==.5f&&CrossbowStringMath.Follow(float.NaN,.3f,.01f)==.3f&&CrossbowStringMath.Follow(0,2,10)<=1,"degenerate follow");
  // 0.1.162: a shape drawn 3 cm (the game's string not cocked when taken) is not a cocked one; the real ones (20 cm) are.
  if(!CrossbowStringMath.Cocked(.028f,.229f)||!CrossbowStringMath.Cocked(.043f,.256f)||CrossbowStringMath.Cocked(.238f,.266f)||CrossbowStringMath.Cocked(float.NaN,.2f))throw new Exception("uncocked string shape accepted or a cocked one refused");
  Console.WriteLine("PASS: 0.1.162 a string shape drawn under 10 cm is not taken as cocked (kept/saved only when cocked).");
  Console.WriteLine("PASS: crossbow string: cocked with the bolt's rear at the nock, released straight across at the cams, carried back by the bolt, let go at once after a shot.");
 }
}
