using System;
namespace XiiiXR;
internal static class DeferredRenderScale
{
 internal static float Choose(float wanted,float configured)
 {
  if(configured<=1||wanted>=.999f)return 1;
  // 145% -> 125% -> 100%; small 105–125% settings go straight to 100%.
  float tier=wanted*configured<=1.251f?1:Math.Min(1.25f,configured);
  return tier/configured;
 }
 internal static bool CanResize(float wanted,float current,float gpuMs,float now,float next)
  =>Math.Abs(wanted-current)>.01f&&now>=next&&(wanted<current||gpuMs>0);
}
