using System;
using System.Numerics;
namespace XiiiXR;
// World-space head position already includes runtime/mod recenter and crouch.
// Only yaw turns the hold; looking up must not lift an NPC onto the head.
internal static class CarryAnchorMath
{
 internal static Quaternion Heading(Quaternion head,Quaternion fallback)
 {
  var f=Vector3.Transform(Vector3.UnitZ,head);
  return f.X*f.X+f.Z*f.Z<.01f?fallback:Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(f.X,f.Z));
 }
 // 0.1.191: the top of his neck 20 cm ahead of the eyes (was 56), 17 cm to
 // the side of the holding hand - his back against the chest, his head beside
 // the player's, the view past it over his shoulder - and 8 cm below the eyes
 // (a hostage of the player's height stands on the floor; was 16).
 internal const float HostageAhead=.20f,HostageSide=.17f,HostageDown=.08f;
 internal static Vector3 Target(Vector3 head,Quaternion yaw,bool hostage,bool right)
 {
  var offset=hostage?new Vector3(right?HostageSide:-HostageSide,-HostageDown,HostageAhead):new Vector3(right?.30f:-.30f,-.34f,.18f);
  return head+Vector3.Transform(offset,yaw);
 }
 // 0.1.193: dropped at once, he
 // starts falling clear of the player's body (the game's own knock-out had
 // him pushed away first) - at least this far ahead of the eyes, never into
 // a wall in front (kept this far from it), moved at most this much.
 internal const float DropAhead=.45f,DropClear=.25f,DropMaxPush=.40f;
 internal static float DropPush(float ahead,float free)
 {
  if(!float.IsFinite(ahead))return 0;
  float push=Math.Min(DropAhead-ahead,DropMaxPush);
  if(float.IsFinite(free))push=Math.Min(push,free-DropClear);
  return Math.Max(0,push);
 }
 internal static Vector3 PlaceRoot(Vector3 root,Vector3 pivot,Quaternion rotation,Quaternion desired,Vector3 target)
  =>target-Vector3.Transform(pivot-root,Quaternion.Normalize(desired*Quaternion.Inverse(rotation)));
}
