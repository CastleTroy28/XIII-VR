using System;using System.Numerics;using XiiiXR;
class CarryAnchorMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  foreach(bool hostage in new[]{false,true})foreach(bool right in new[]{false,true})
  foreach(float yaw in new[]{-179f,-90f,0,63f,179f})foreach(float pitch in new[]{-80f,0,80f})
  {
   var head=new Vector3(13,1.65f,-9);var q=Quaternion.CreateFromYawPitchRoll(yaw*MathF.PI/180,pitch*MathF.PI/180,.2f);
   var heading=CarryAnchorMath.Heading(q,Quaternion.Identity);var target=CarryAnchorMath.Target(head,heading,hostage,right);
   Check(Math.Abs(target.Y-head.Y-(hostage?-CarryAnchorMath.HostageDown:-.34f))<.0001f,"looking up/down changes carry height");
   var root=new Vector3(-2,.35f,7);var rotation=Quaternion.CreateFromYawPitchRoll(.8f,.1f,-.3f);var localPivot=new Vector3(.15f,hostage?1.5f:.85f,.1f);
   var pivot=root+Vector3.Transform(localPivot,rotation);
   var placed=CarryAnchorMath.PlaceRoot(root,pivot,rotation,heading,target);
   Check(Vector3.Distance(placed+Vector3.Transform(localPivot,heading),target)<.00001f,"root placement ignores actual NPC skeleton pivot");
   for(int i=0;i<100;i++)placed=CarryAnchorMath.PlaceRoot(placed,placed+Vector3.Transform(localPivot,heading),heading,heading,target);
   Check(Vector3.Distance(placed+Vector3.Transform(localPivot,heading),target)<.00001f,"repeated callbacks accumulate offset");
   // Runtime recenter/crouch/room movement: the rendered head is authoritative.
   var movedHead=head+new Vector3(2,-.65f,3);var moved=CarryAnchorMath.Target(movedHead,heading,hostage,right);
   Check(Vector3.Distance(moved-target,movedHead-head)<.00001f,"carry retains old tracking origin after recenter/crouch");
  }
  var fallback=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.5f);
  Check(Math.Abs(Quaternion.Dot(CarryAnchorMath.Heading(Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI/2),fallback),fallback))>.99999f,"vertical gaze flips heading");
  // 0.1.193: a hostage let go of drops at once, clear of the player (at least 45 cm ahead), never into a wall in front.
  Check(Math.Abs(CarryAnchorMath.DropPush(.20f,float.PositiveInfinity)-.25f)<1e-5f,"a hostage held 20 cm ahead not moved clear of the player before falling");
  Check(CarryAnchorMath.DropPush(.60f,float.PositiveInfinity)==0,"a hostage already clear moved");
  Check(Math.Abs(CarryAnchorMath.DropPush(-.5f,float.PositiveInfinity)-CarryAnchorMath.DropMaxPush)<1e-5f,"moved more than 40 cm");
  Check(Math.Abs(CarryAnchorMath.DropPush(.20f,.35f)-.10f)<1e-5f&&CarryAnchorMath.DropPush(.20f,.1f)==0,"pushed into a wall in front");
  Check(CarryAnchorMath.DropPush(float.NaN,1)==0,"a bad position moves him");
  Console.WriteLine("PASS: 0.1.193 a hostage let go of starts falling clear of the player (45 cm ahead, at most 40 cm moved), never into a wall.");
  Console.WriteLine("PASS: 60 head/model/hand fixtures: yaw, pitch, live recenter/crouch offset, skeleton-pivot placement, repeated callbacks and vertical gaze. No in-game visual test.");
 }
}
