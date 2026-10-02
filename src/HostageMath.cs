using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.112: the player is behind the NPC: the NPC faces away from the
// player's head (within ~75 degrees of straight away) and the head is near.
internal static class HostageMath
{
    internal const float MaxDistance=1.5f,MinAway=.25f;
    internal static bool Behind(Vector3 npcForward,Vector3 npcPosition,Vector3 head)
    {
        var f=new Vector2(npcForward.X,npcForward.Z);var to=new Vector2(npcPosition.X-head.X,npcPosition.Z-head.Z);
        float distance=to.Length();
        if(!float.IsFinite(distance)||distance>MaxDistance||distance<1e-3f||f.LengthSquared()<1e-6f)return false;
        return Vector2.Dot(Vector2.Normalize(f),to/distance)>=MinAway;
    }
}
