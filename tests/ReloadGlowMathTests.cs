using System;using XiiiXR;
class ReloadGlowMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static GlowTarget T(bool holding=false,int held=0,bool installed=true,bool rack=false,bool open=false,int rounds=30,bool lidded=false,bool single=false)
  =>ReloadGlowMath.Target(holding,held,installed,rack,open,rounds,lidded,single);
 static void Main()
 {
  // Magazine gun: empty -> magazine; out -> pouch; in hand -> nothing (0.1.123); in -> bolt; ready -> nothing.
  Check(T(rounds:0)==GlowTarget.Magazine,"empty rifle: magazine");
  Check(T(rounds:0,rack:true)==GlowTarget.Magazine,"empty pistol (slide locked) still magazine first");
  Check(T(installed:false,rounds:0,rack:true)==GlowTarget.Pouch,"magazine out: pouch");
  Check(T(holding:true,held:30,installed:false,rounds:0,rack:true)==GlowTarget.None,"new magazine in hand: no hint where it goes");
  Check(T(holding:true,held:0,installed:false,rounds:0,rack:true)==GlowTarget.None,"empty magazine in hand: nothing (drop it)");
  Check(T(rounds:30,rack:true)==GlowTarget.Bolt,"inserted: bolt");
  Check(T()==GlowTarget.None,"ready gun glows");
  // Shotgun / crossbow: empty -> pouch; shell/bolt in hand -> nothing; pump when needed.
  Check(T(rounds:0,single:true)==GlowTarget.Pouch&&T(holding:true,held:1,rounds:0,single:true)==GlowTarget.None&&T(rounds:3,rack:true,single:true)==GlowTarget.Bolt,"single rounds");
  // M60: cover, box, pouch, (box in hand: nothing), cover, handle.
  Check(T(rounds:0,lidded:true)==GlowTarget.Cover,"empty M60: cover");
  Check(T(rounds:0,open:true,lidded:true)==GlowTarget.Magazine,"cover open: box");
  Check(T(installed:false,rounds:0,rack:true,open:true,lidded:true)==GlowTarget.Pouch,"box off: pouch");
  Check(T(holding:true,held:100,installed:false,rounds:0,rack:true,open:true,lidded:true)==GlowTarget.None,"new box in hand: no hint");
  Check(T(rounds:100,rack:true,open:true,lidded:true)==GlowTarget.Cover,"new box on: close the cover");
  Check(T(rounds:100,rack:true,lidded:true)==GlowTarget.Bolt,"cover closed: charging handle");
  Check(T(rounds:100,lidded:true)==GlowTarget.None,"ready M60 glows");
  // 0.1.122: crossbow rail resistance.
  Check(RailMath.Resistance(0)==RailMath.Rest&&RailMath.Resistance(.3f)>RailMath.Resistance(.1f)&&RailMath.Resistance(50)==RailMath.Max&&RailMath.Resistance(float.NaN)==RailMath.Rest&&RailMath.Resistance(-1)==RailMath.Rest,"rail resistance");
  Console.WriteLine("PASS: reload hint order: magazine -> pouch -> (nothing while ammunition is in the hand) -> bolt; shells/bolts: pouch -> pump; M60: cover -> box -> pouch -> cover -> handle; nothing when ready.");
 }
}
