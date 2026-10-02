using System;using XiiiXR;
class ScrewdriverGripTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  Check(MathF.Abs(ScrewdriverGrip.HoldPoint(0,.08f)-(.08f-ScrewdriverGrip.FrontInset))<1e-6f,"thumb not near the front of the handle");
  Check(ScrewdriverGrip.HoldPoint(0,.016f)==.008f,"short handle held behind its middle");
  Check(ScrewdriverGrip.HoldPoint(float.NaN,.05f)==.05f&&ScrewdriverGrip.HoldPoint(.05f,.02f)==.02f,"invalid handle range");
  Console.WriteLine("PASS: lockpick screwdriver grip holds the handle 12 mm behind its front end (never behind its middle); invalid ranges handled.");
 }
}
