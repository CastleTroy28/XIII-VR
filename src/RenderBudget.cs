using System;
using UnityEngine;
using UnityEngine.XR;
namespace XiiiXR;
internal static class RenderBudget
{
 private static readonly RenderBudgetMath policy=new();
 private static XRDisplaySubsystem? attached;private static Camera? camera;
 private static float originalViewport=1,originalTargets=1,appliedViewport=1,appliedTargets=1,savedScale=1,nextResize;
 private static int qualityRevision;
 private static bool failed;private static double gpuTotal;private static int gpuCount;
 internal static void Tick(XRDisplaySubsystem display,CameraRig? rig)
 {
  if(attached==display&&failed)return;
  try
  {
   float configured=QualityMenu.Clamp(QualityOptions.RenderScale.Value);
   if(attached!=display)
   {
    Release();attached=display;originalViewport=appliedViewport=display.scaleOfAllViewports;
    originalTargets=appliedTargets=display.scaleOfAllRenderTargets;savedScale=configured;qualityRevision=QualityMenu.AppliedRevision;failed=false;
   }
   // The settings menu has already applied a user's explicit resolution change.
   if(Math.Abs(configured-savedScale)>.001f||qualityRevision!=QualityMenu.AppliedRevision)
   {
    display.scaleOfAllViewports=originalViewport;appliedViewport=originalViewport;
    originalTargets=appliedTargets=display.scaleOfAllRenderTargets;savedScale=configured;qualityRevision=QualityMenu.AppliedRevision;policy.Reset();nextResize=0;
   }
   var main=rig?.MainCamera;
   if(main!=null&&main!=camera)
   {
    Restore();policy.Reset();nextResize=0;camera=main;
   }
   float gpu=0;
   if(display.TryGetAppGPUTimeLastFrame(out float seconds)&&float.IsFinite(seconds)&&seconds>0&&seconds<1){gpu=seconds*1000;gpuTotal+=gpu;gpuCount++;}
   if(!QualityOptions.AdaptiveSupersampling.Value){Restore();policy.Reset();return;}
   bool gameplay=rig!=null&&!rig.Frontend&&!rig.Scripted&&rig.PlayerRoot!=null&&rig.HeadTrackingValid&&WindowFocus.Playable&&Time.timeScale>0&&GameUiControls.Current?.BlocksGameplay!=true;
   // Pause/menu does not reallocate the eye buffers just to open and close UI.
   if(!gameplay||main==null)return;
   float wanted=policy.Step(Time.unscaledDeltaTime,CompositorTiming.HeadsetHz,gpu,configured,true);
   bool deferred=main!=null&&(main.actualRenderingPath==RenderingPath.DeferredShading||main.actualRenderingPath==RenderingPath.DeferredLighting);
   if(deferred)
   {
    // Built-in deferred cannot use viewport scaling. Reuse the game's proven
    // render-target scaling route, in coarse steps and at most once per 12 s.
    // Without GPU headroom measurements, restore only on a new camera/level,
    // explicit settings change or disable; do not repeatedly probe higher res.
    float requested=originalTargets*DeferredRenderScale.Choose(wanted,configured);
    if(!DeferredRenderScale.CanResize(requested,appliedTargets,gpu,Time.realtimeSinceStartup,nextResize))return;
    display.scaleOfAllRenderTargets=requested;appliedTargets=requested;nextResize=Time.realtimeSinceStartup+12;
    rig!.ResetRenderCaches();
    Bootstrap.Write(FormattableString.Invariant($"RENDER BUDGET deferred targetScale={requested:F2} savedScale={configured:F2} gpuMs={(gpu>0?gpu.ToString("F2"):"unavailable")} (coarse resize; at most once per 12s)"));
   }
   else
   {
    wanted=Math.Min(originalViewport,wanted);
    if(Math.Abs(wanted-appliedViewport)<.001f)return;
    display.scaleOfAllViewports=wanted;appliedViewport=wanted;
    Bootstrap.Write(FormattableString.Invariant($"RENDER BUDGET viewport={wanted:F3} effectiveScale={configured*wanted:F2} gpuMs={(gpu>0?gpu.ToString("F2"):"unavailable")} (requested; provider may clamp/ignore)"));
   }
  }
  catch(Exception ex){failed=true;try{Restore();}catch{}Bootstrap.Warn("RENDER BUDGET disabled: "+ex.Message);}
 }
 private static void Restore()
 {
  if(attached==null)return;
  if(Math.Abs(appliedViewport-originalViewport)>.001f){attached.scaleOfAllViewports=originalViewport;appliedViewport=originalViewport;}
  if(Math.Abs(appliedTargets-originalTargets)>.001f){attached.scaleOfAllRenderTargets=originalTargets;appliedTargets=originalTargets;}
 }
 internal static string Report()
 {
  string value=gpuCount>0?"appGpuMs="+(gpuTotal/gpuCount).ToString("F2",System.Globalization.CultureInfo.InvariantCulture):"appGpuMs=unavailable";
  gpuTotal=0;gpuCount=0;return FormattableString.Invariant($"{value} viewport={appliedViewport:F3} targetScale={appliedTargets:F2}");
 }
 internal static void Release()
 {try{Restore();}catch{}attached=null;camera=null;appliedViewport=originalViewport=appliedTargets=originalTargets=savedScale=1;nextResize=0;policy.Reset();gpuTotal=0;gpuCount=0;}
}
