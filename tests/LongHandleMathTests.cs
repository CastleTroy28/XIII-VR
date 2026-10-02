using System;using System.Numerics;using XiiiXR;
class LongHandleMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // The shovel's stick from the log: grip (-0.004,-0.075,0.906), axis (0.014,0.159,-0.987).
  var origin=new Vector3(-.004f,-.075f,.906f);var axis=new Vector3(.014f,.159f,-.987f);
  // The game's right hand: wrist (0.0132,-0.1009,0.9018), its closed grip 7.3 cm along the fingers (+x) and 2.5 cm to the palm (+y).
  var grip=new Vector3(.0132f+.073f,-.1009f+.0245f,.9018f+.0074f);
  float miss=LongHandleMath.Miss(grip,origin,axis);
  Check(miss>LongHandleMath.MaxMiss&&miss>.08f,"the game's shovel hold misses the stick by "+miss);
  // A grip on the stick (anywhere along it) does not miss.
  Check(LongHandleMath.Miss(origin+Vector3.Normalize(axis)*.4f+new Vector3(.01f,0,0),origin,axis)<LongHandleMath.MaxMiss,"a hand on the stick 40 cm up misses it");
  Check(float.IsPositiveInfinity(LongHandleMath.Miss(grip,origin,Vector3.Zero))&&float.IsPositiveInfinity(LongHandleMath.Miss(grip,origin,new Vector3(float.NaN,0,0))),"degenerate axis");
  Console.WriteLine("PASS: a hand's closed grip against a long stick: the game's shovel hold 9 cm off (moved into the fist), one on the stick kept.");
 }
}
