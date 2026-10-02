using UnityEngine;
namespace XiiiXR;
// 0.1.98: which hand currently holds an NPC body part (read by the hand
// visual, the hand collision and the fire input).
internal static class BodyGrabState
{
    internal static Transform? Left{get;set;}
    internal static Transform? Right{get;set;}
    internal static Transform? Root(bool right)=>right?Right:Left;
    internal static bool Holding(bool right)=>Root(right)!=null;
}
