using System;
using System.Numerics;
namespace XiiiXR;
internal sealed class ScoopGesture
{
 private bool armed;private Vector3 start;private float began;
 internal void Reset(){armed=false;}
 internal bool Sample(bool held,bool contact,Vector3 hand,Vector3 chest,float now)
 {
  if(!held){Reset();return false;}
  if(!armed){if(contact){armed=true;start=hand;began=now;}return false;}
  if(now-began>.65f||(hand-start).Length()>.65f){Reset();return false;}
  var inward=chest-start;
  if(inward.Length()<.15f)return false;
  if(Vector3.Dot(hand-start,Vector3.Normalize(inward))<.12f)return false;
  Reset();return true;
 }
}
internal static class ClimbHandMath
{
 // 0.1.151: limit - a hand on a turning ceiling fan is carried faster than one on a ladder.
 internal static Vector3 Velocity(Vector3 error,float dt,float limit=2)=>dt<=0||dt>.1f||error.Length()>.8f?Vector3.Zero:Limit(error/dt,limit);
 private static Vector3 Limit(Vector3 p,float n)=>p.Length()>n?Vector3.Normalize(p)*n:p;
}
// 0.1.171: hands on a ladder pulling up near its top lift the character
// straight up until its feet clear the landing beyond the ladder, then carry
// it onto the landing.
internal static class LadderTopMath
{
 internal const float From=1.3f,Pull=.2f,UpSpeed=2.6f,OverSpeed=2.2f,Arrive=.15f,Clear=.08f,UpMax=1.4f,Total=2.6f;
 // Time to go over: the feet within From of the ladder's top, the hands pulling up.
 internal static bool Ready(float feet,float top,float pullUp)=>float.IsFinite(feet)&&float.IsFinite(top)&&float.IsFinite(pullUp)&&feet>=top-From&&pullUp>Pull;
 // A landing: a floor beyond the ladder (away from the climber), higher than the feet, not far above the top.
 internal static bool Landing(Vector3 feet,Vector3 ladder,Vector3 away,Vector3 hit,Vector3 normal,float top)
  =>normal.Y>.65f&&hit.Y>feet.Y+.3f&&hit.Y<top+1f&&Vector3.Dot(new Vector3(hit.X-ladder.X,0,hit.Z-ladder.Z),away)>.05f;
 // The way over: up until the feet clear the landing (then never up again), over onto it; done there.
 internal static Vector3 Velocity(Vector3 feet,Vector3 landing,ref bool over,out bool done)
 {
  done=false;
  if(!over&&feet.Y<landing.Y+Clear)return new Vector3(0,UpSpeed,0);
  over=true;
  var h=new Vector3(landing.X-feet.X,0,landing.Z-feet.Z);float l=h.Length();
  if(l<Arrive||!float.IsFinite(l)){done=true;return Vector3.Zero;}
  return h/l*OverSpeed+new Vector3(0,.3f,0);
 }
}
