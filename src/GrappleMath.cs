using System;
namespace XiiiXR;
// 0.1.142: the rope climbing speed, times the game's own.
internal static class GrappleMath
{
    internal const float DefaultClimbScale=2f,MinClimbScale=.5f,MaxClimbScale=4f;
    internal static float ClimbScale(float configured)=>float.IsFinite(configured)&&configured>0?Math.Clamp(configured,MinClimbScale,MaxClimbScale):DefaultClimbScale;
}
