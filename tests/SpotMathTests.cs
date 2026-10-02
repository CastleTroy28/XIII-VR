using System;using XiiiXR;
class SpotMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  Check(SpotMath.Delta(350,10)==20&&SpotMath.Delta(10,350)==-20&&SpotMath.Delta(0,180)==180&&SpotMath.Delta(float.NaN,1)==0,"angle difference");
  // An enemy straight ahead of the character (arc at 0); the player's head turned 90 degrees right: the enemy is on his left.
  // The game turns an arc clockwise (negative) for an enemy to the right: the arc now points left (+90).
  Check(Math.Abs(SpotMath.Delta(SpotMath.Corrected(0,0,-90,-1),90))<1e-3f,"arc does not follow the head (clockwise game)");
  Check(Math.Abs(SpotMath.Delta(SpotMath.Corrected(0,0,-90,1),-90))<1e-3f,"arc does not follow the head (anticlockwise game)");
  Check(SpotMath.Corrected(37,20,20,-1)==37,"head and character agree: the game's arc changed");
  Check(Math.Abs(SpotMath.Delta(SpotMath.Corrected(-30,30,30,0),-30))<1e-3f,"unknown direction: not the UI's usual");
  // Which way the game turns its arcs, from two looks.
  Check(SpotMath.Sign(0,0,-40,40)==-1&&SpotMath.Sign(0,0,40,40)==1,"direction of the game's arcs");
  Check(SpotMath.Sign(0,0,-3,5)==0&&SpotMath.Sign(0,0,0,40)==0&&SpotMath.Sign(0,0,90,40)==0,"direction told from too little");
  Check(SpotMath.Sign(350,170,10,-170)==1&&SpotMath.Sign(10,170,350,-170)==-1,"direction across the 180 seam");
  // 0.1.159: the damage strip. Hit from 30 degrees right of the character's heading: the game's strip at -30 (clockwise).
  // The head then looks 30 degrees right: the shooter is straight ahead of the head, the strip at 0.
  Check(Math.Abs(SpotMath.Delta(SpotMath.Strip(-30,30,0),0))<1e-3f,"damage strip does not follow the head");
  // The head turned 90 left afterwards: the shooter is now 120 right, the strip at -120; with an image offset (strip drawn at 180+) the same turn.
  Check(Math.Abs(SpotMath.Delta(SpotMath.Strip(-30,30,120),-120))<1e-3f&&Math.Abs(SpotMath.Delta(SpotMath.Strip(150,30,120),60))<1e-3f,"damage strip turn or image offset");
  Check(Math.Abs(SpotMath.Delta(SpotMath.Strip(170,-170,170),-170))<1e-3f,"damage strip across the 180 seam");
  Check(SpotMath.Strip(12,float.NaN,5)==12,"damage strip with no source changed");
  // Which heading the game measured from (either sign of its angle).
  Check(SpotMath.Reference(30,30,-60)==0&&SpotMath.Reference(-30,30,-60)==0&&SpotMath.Reference(60,30,-60)==1&&SpotMath.Reference(300,30,-60)==1,"the game's reference heading");
  Check(SpotMath.Reference(100,30,-20)==-1&&SpotMath.Reference(float.NaN,30,30)==-1&&SpotMath.Reference(25,float.NaN,20)==1,"reference told from a mismatch");
  Console.WriteLine("PASS: 0.1.159 damage strip: turned from the heading the game measured from (the character's or the head's, either sign) to the head's, every frame (image offset kept, across the seam); a mismatch leaves it.");
  Console.WriteLine("PASS: stealth arcs: turned by the angle between the character's heading and the head's (either way the game turns them), the game's direction told from two looks (not from small or odd changes), across the 180-degree seam.");
 }
}
