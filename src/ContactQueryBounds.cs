using System;
using System.Numerics;
namespace XiiiXR;
internal static class ContactQueryBounds
{
 internal static bool Contains(Vector3 center,float radius,Vector3 probe,float probeRadius)
 {
  float room=radius-probeRadius;
  return float.IsFinite(room)&&probeRadius>=0&&room>=0&&Vector3.DistanceSquared(center,probe)<=room*room;
 }
}
