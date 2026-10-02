using System;using XiiiXR;
class LeftHoldMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.0006f;
 static void Main()
 {
  // The pistol: 0.1.139 (box middle, 1.5 cm) put it in the palm at x=-0.0227.
  float boxed=LeftHoldMath.HandleX(.0077f,0,false,true,.015f);
  Check(Near(boxed,-.0227f),"pistol, no measured middle: as 0.1.139 "+boxed);
  // With the measured handle middle (-5.2 mm) it stays there (0.1.140 had -0.033: 1 cm right in the hand).
  float pistol=LeftHoldMath.HandleX(.0077f,-.0052f,true,true,.015f);
  Check(Near(pistol,-.0227f)&&pistol>-.030f,"pistol, measured middle: where 0.1.139 had it "+pistol);
  // The revolver: mirrored across its handle (13.2 mm right of the box middle), the same shift.
  float revolver=LeftHoldMath.HandleX(.0302f,.0132f,true,true,.015f);
  Check(Near(revolver,.0264f-.0302f-.005f)&&revolver>-.0188f,"revolver: not 0.1.140's -0.0188 (beside the palm) "+revolver);
  // The config shift still moves it (1 cm more: 1 cm further).
  Check(Near(LeftHoldMath.HandleX(.0077f,-.0052f,true,true,.025f),pistol-.01f),"config shift");
  Check(Near(LeftHoldMath.HandleX(.0077f,-.0052f,true,true,float.NaN),pistol)&&Near(LeftHoldMath.HandleX(.0077f,-.0052f,true,true,1),LeftHoldMath.HandleX(.0077f,-.0052f,true,true,.04f)),"shift fallback/clamp");
  // Long guns: mirrored across the handle, no shift.
  Check(Near(LeftHoldMath.HandleX(.0233f,-.0018f,true,false,.015f),-.0036f-.0233f),"long gun mirror");
  Check(LeftHoldMath.Handgun("pistol")&&LeftHoldMath.Handgun("revolver")&&!LeftHoldMath.Handgun("m16"),"handguns");
  Console.WriteLine("PASS: the left hand's handle hold: pistols where 0.1.139 put them in the palm (measured middle, 0.5 cm shift), the revolver mirrored across its own handle, config shift, long guns plain mirror.");
 }
}
