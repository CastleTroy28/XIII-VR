using System;
namespace XiiiXR;
// Reduce only supersampling above the headset's recommended resolution.
// Long stalls/loading never trigger a quality change. Separate down/up
// dwell times prevent resolution oscillation near the refresh-rate limit.
internal sealed class RenderBudgetMath
{
 internal float Viewport{get;private set;}=1;
 private float bad,good;
 internal void Reset(){Viewport=1;bad=good=0;}
 internal float Step(float dt,float hz,float gpuMs,float renderScale,bool enabled)
 {
  if(!enabled||!float.IsFinite(renderScale)||renderScale<=1||!float.IsFinite(hz)||hz<30){Reset();return Viewport;}
  float floor=1/Math.Clamp(renderScale,1,1.5f);Viewport=Math.Clamp(Viewport,floor,1);
  if(!float.IsFinite(dt)||dt<=0||dt>.05f){bad=good=0;return Viewport;}
  float budget=1000/hz;bool gpu=float.IsFinite(gpuMs)&&gpuMs>0;
  bool slow=gpu?gpuMs>budget*.94f:dt*1000>budget*1.12f;
  bool spare=gpu?gpuMs<budget*.72f:dt*1000<budget*1.04f;
  bad=slow?bad+dt:Math.Max(0,bad-dt*.25f);
  good=spare?good+dt:0;
  if(bad>=1.2f){Viewport=Math.Max(floor,Viewport-.05f);bad=good=0;}
  else if(good>=12){Viewport=Math.Min(1,Viewport+.025f);bad=good=0;}
  return Viewport;
 }
}
