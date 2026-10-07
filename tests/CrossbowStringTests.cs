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
  // 0.1.251: the harpoon gun's bands (its "line" bones, the curved ones too) drawn like the string, and
  // let go to a third of their length (not flat across the muzzle); the crossbows' strings as before.
  bool band;
  Check(CrossbowStringMath.StringBone("wpn_harpoon_gun_line_left_a_SH_BND_JNT",out band)&&band&&CrossbowStringMath.StringBone("wpn_harpoon_line_right_crv_j_SH_BND_JNT",out band)&&band,"the harpoon gun's bands not taken");
  Check(CrossbowStringMath.StringBone("wpn_crossbow_string_aa_BND_JNT",out band)&&!band&&CrossbowStringMath.StringBone("wpn_crossbow_tactical_string_right_h_SH_BND_JNT",out band)&&!band,"a crossbow's string taken for a band or left out");
  Check(!CrossbowStringMath.StringBone("wpn_harpoon_gun_arrow_SH_BND_JNT",out _)&&!CrossbowStringMath.StringBone("wpn_crossbow_tactical_arm_left_a_SH_BND_JNT",out _)&&!CrossbowStringMath.StringBone("",out _),"the harpoon or a limb taken for a band");
  float r=CrossbowStringMath.BandRest;
  Check(MathF.Abs(CrossbowStringMath.Shift(nock,tip,0,r)-.30f*(1-r))<1e-5f&&CrossbowStringMath.Shift(nock,tip,1,r)==0&&CrossbowStringMath.Shift(tip,tip,0,r)==0,"a band let go flat, or moved when drawn");
  Check(MathF.Abs(CrossbowStringMath.Shift(nock,tip,0)-CrossbowStringMath.Shift(nock,tip,0,0))<1e-7f&&CrossbowStringMath.Shift(nock,tip,0,float.NaN)==0,"the string's release changed");
  Check(r>.15f&&r<.5f,"bands let go to "+r+" of their length");
  // Drawn as far as the harpoon is slid in (from its notch), not by its rear end behind the notch.
  Check(CrossbowStringMath.Draw(nock+0,nock,tip)==1&&MathF.Abs(CrossbowStringMath.Draw(nock+.15f,nock,tip)-.5f)<1e-5f,"the bands not drawn with the harpoon slid in");
  Console.WriteLine("PASS: 0.1.251 the harpoon gun's bands drawn like a crossbow's string (to the harpoon's notch, as far as it is slid in) and let go to "+(r*100).ToString("F0")+"% of their length; the crossbows' strings unchanged.");
 }
}
