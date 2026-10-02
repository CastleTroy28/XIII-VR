using System;using XiiiXR;using UnityEngine.XR;
class RenderBudgetTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var p=new RenderBudgetMath();
  for(int i=0;i<90*30;i++)p.Step(1f/90,90,6,1.45f,true);
  Check(p.Viewport==1,"good frame loses quality");
  for(int i=0;i<80;i++)p.Step(.2f,90,30,1.45f,true);
  Check(p.Viewport==1,"loading stalls lower quality");
  for(int i=0;i<72*30;i++)p.Step(1f/72,90,15,1.45f,true);
  Check(Math.Abs(p.Viewport*1.45f-1)<.00001f,"quality fell below headset recommendation (or never reduced)");
  float low=p.Viewport;
  for(int i=0;i<90*2;i++)p.Step(1f/90,90,4,1.45f,true);
  Check(p.Viewport==low,"quality oscillates immediately on a good frame");
  for(int i=0;i<90*12;i++)p.Step(1f/90,90,4,1.45f,true);
  Check(p.Viewport>low&&p.Viewport<1,"slow recovery missing");
  Check(p.Step(1f/90,90,30,1.45f,false)==1,"disabling does not restore quality");
  for(int i=0;i<500;i++)p.Step(1f/45,90,30,1,true);
  Check(p.Viewport==1,"native resolution was reduced");
  for(int i=0;i<200;i++)p.Step(1f/72,90,0,1.45f,true);
  Check(p.Viewport<1,"fallback cadence cannot respond to sustained missed frames");
  Check(p.Step(.01f,0,30,1.45f,true)==1,"unknown refresh rate guessed");
  var display=new XRDisplaySubsystem{scaleOfAllViewports=.9f,GpuSeconds=.020f};var rig=new CameraRig();
  for(int i=0;i<400;i++){UnityEngine.Time.realtimeSinceStartup+=1f/90;RenderBudget.Tick(display,rig);}
  Check(display.scaleOfAllViewports<.9f,"production adapter fails to convert GPU seconds to milliseconds");
  Check(RenderBudget.Report().Contains("appGpuMs=20.00"),"GPU report units wrong");
  QualityOptions.AdaptiveSupersampling.Value=false;RenderBudget.Tick(display,rig);
  Check(display.scaleOfAllViewports==.9f,"runtime viewport cap lost when disabled");
  QualityOptions.AdaptiveSupersampling.Value=true;rig.Frontend=true;RenderBudget.Tick(display,rig);
  Check(display.scaleOfAllViewports==.9f,"frontend quality reduced");
  RenderBudget.Release();Check(display.scaleOfAllViewports==.9f,"shutdown does not restore borrowed viewport");
  var deferred=new XRDisplaySubsystem{scaleOfAllViewports=1,scaleOfAllRenderTargets=1.45f,GpuSeconds=0};rig.Frontend=false;rig.MainCamera=new UnityEngine.Camera{actualRenderingPath=UnityEngine.RenderingPath.DeferredShading};
  for(int i=0;i<300;i++){UnityEngine.Time.unscaledDeltaTime=1f/72;UnityEngine.Time.realtimeSinceStartup+=1f/72;RenderBudget.Tick(deferred,rig);}
  Check(deferred.scaleOfAllViewports==1&&Math.Abs(deferred.scaleOfAllRenderTargets-1.25f)<.001f,"deferred used unsupported viewport or failed first coarse reduction");
  int resized=deferred.Resizes;
  for(int i=0;i<300;i++){UnityEngine.Time.realtimeSinceStartup+=1f/72;RenderBudget.Tick(deferred,rig);}
  Check(deferred.Resizes==resized,"deferred reallocates buffers on every small policy step");
  for(int i=0;i<1000;i++){UnityEngine.Time.realtimeSinceStartup+=1f/72;RenderBudget.Tick(deferred,rig);}
  Check(Math.Abs(deferred.scaleOfAllRenderTargets-1)<.001f,"deferred native-resolution floor wrong");
  for(int i=0;i<90*150;i++){UnityEngine.Time.unscaledDeltaTime=1f/90;UnityEngine.Time.realtimeSinceStartup+=1f/90;RenderBudget.Tick(deferred,rig);}
  Check(Math.Abs(deferred.scaleOfAllRenderTargets-1)<.001f,"no-GPU deferred mode repeatedly probes expensive supersampling");
  rig.MainCamera=new UnityEngine.Camera{actualRenderingPath=UnityEngine.RenderingPath.DeferredShading};RenderBudget.Tick(deferred,rig);
  Check(Math.Abs(deferred.scaleOfAllRenderTargets-1.45f)<.001f,"new level failed to restore configured resolution");
  QualityMenu.AppliedRevision++;QualityOptions.RenderScale.Value=1.3f;deferred.scaleOfAllRenderTargets=1.3f;RenderBudget.Tick(deferred,rig);RenderBudget.Release();
  Check(Math.Abs(deferred.scaleOfAllRenderTargets-1.3f)<.001f,"explicit new resolution restored to stale setting");
  Console.WriteLine("PASS: production render budget: GPU units, native-resolution floor, runtime cap, unsupported timing fallback, long stalls, stable/slow recovery, menu/disable/shutdown restore; deferred coarse target resize, cooldown, no-GPU stability, scene/user reset.");
 }
}
namespace UnityEngine {enum RenderingPath{Forward,DeferredShading,DeferredLighting}class Camera{internal RenderingPath actualRenderingPath=RenderingPath.Forward;}static class Time{internal static float unscaledDeltaTime=1f/90,timeScale=1,realtimeSinceStartup=0;}}
namespace UnityEngine.XR {class XRDisplaySubsystem{internal float scaleOfAllViewports=1,GpuSeconds;private float targets=1.45f;internal int Resizes;internal float scaleOfAllRenderTargets{get=>targets;set{targets=value;Resizes++;}}internal bool TryGetAppGPUTimeLastFrame(out float seconds){seconds=GpuSeconds;return seconds>0;}}}
namespace XiiiXR
{
 class Flag<T>{internal T Value;internal Flag(T v){Value=v;}}
 static class QualityMenu{internal static int AppliedRevision=0;internal static float Clamp(float v)=>v;}
 static class QualityOptions{internal static Flag<float> RenderScale=new(1.45f);internal static Flag<bool> AdaptiveSupersampling=new(true);}
 class CameraRig{internal UnityEngine.Camera MainCamera=new();internal void ResetRenderCaches(){}internal bool Frontend=false;internal bool Scripted=>false;internal object PlayerRoot=>this;internal bool HeadTrackingValid=>true;}
 static class CompositorTiming{internal static float HeadsetHz=90;}
 static class WindowFocus{internal static bool Playable=>true;}
 class GameUiControls{internal static GameUiControls? Current=null;internal bool BlocksGameplay=>false;}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s)=>throw new Exception(s);}
}
