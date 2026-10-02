using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// One owner for ALL native screen UI, across frontend, gameplay and cinematics.
// Independent ownership used to restore subtitle/comic canvases mid-transition.
internal sealed class FrontendMenu : IDisposable
{
    private readonly CameraRig rig;
    private readonly List<WorldCanvas> canvases=new();
    private readonly FocusMarkers markers=new();
    private readonly CinematicFrame frames=new();
    private readonly CycleIcons cycleIcons=new();
    private readonly FullscreenEffects fullscreen=new();
    private readonly CinematicMask cinema=new();
    private int discoveryRevision,deepRevision=-1;
    private float nextFind;
    private bool discoveredScripted,discoveredFrontend;
    private bool anchored,wasFrontend,wasScripted,wasFixed;
    private Vector3 position;
    private Quaternion rotation;
    private readonly OptionalWork discovery,presentation;
    internal FrontendMenu(CameraRig owner)
    {
        rig=owner;
        discovery=new OptionalWork("VR UI discovery",TickCore);
        presentation=new OptionalWork("VR UI render",RenderCore);
    }
    internal void Own(Canvas c)
    {
        if(c==null)return;c=c.rootCanvas;
        foreach(var p in canvases)if(p.Canvas==c)return;
        if(c.renderMode==RenderMode.WorldSpace||c.name.StartsWith("XIII ",StringComparison.Ordinal))return;
        canvases.Add(new WorldCanvas(c));discoveryRevision++;Bootstrap.Write("UI LAYER owned="+c.name);
    }
    internal void Tick()=>discovery.Run(Time.realtimeSinceStartup);
    private void TickCore()
    {
        // 0.1.121: every 1.5 s in plain gameplay (twice a second in menus,
        // cutscenes and after a scene change), never in a frame with another
        // scene search.
        bool busy=rig.Frontend||rig.Scripted||rig.MovieActive||PauseMenuControl.HackGameIsPaused||GameUiControls.Current?.PointerMenuOpen==true;
        if(!SceneScan.Due(ref nextFind,busy?.5f:1.5f))return;
        if(canvases.RemoveAll(p=>p.Canvas==null)>0)discoveryRevision++;
        long canvasTimer=FramePerformance.Begin();
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Canvas>()))
        {
            var c=obj.TryCast<Canvas>();
            if(c==null||!c.gameObject.scene.IsValid()||c.rootCanvas!=c||c.name=="DevConsoleCanvas")continue;
            Own(c);
        }
        SceneScan.End("canvas",canvasTimer);
        FramePerformance.Scope("canvas-discovery",canvasTimer);
        // Full image/effect enumeration allocates many IL2CPP wrappers. Do it
        // on UI/scene changes, not twice a second throughout normal gameplay.
        if(discoveryRevision!=deepRevision||discoveredScripted!=rig.Scripted||discoveredFrontend!=rig.Frontend)
        {
            long timer=FramePerformance.Begin();
            frames.Discover();cycleIcons.Discover();fullscreen.Discover();
            deepRevision=discoveryRevision;discoveredScripted=rig.Scripted;discoveredFrontend=rig.Frontend;
            FramePerformance.Scope("ui-discovery",timer);
        }
    }
    internal void Render()=>presentation.Run(Time.realtimeSinceStartup);
    private void RenderCore()
    {
        bool fixedPanel=!rig.Scripted&&(rig.Frontend||GameUiControls.Current?.PointerMenuOpen==true);
        if(!anchored||!fixedPanel||fixedPanel!=wasFixed||wasFrontend!=rig.Frontend||wasScripted!=rig.Scripted)
        {
            rotation=fixedPanel?Quaternion.Euler(0,rig.HeadRotation.eulerAngles.y,0):rig.HeadRotation;
            position=rig.HeadPosition+rotation*new Vector3(0,0,1.7f);
        }
        anchored=true;wasFixed=fixedPanel;wasFrontend=rig.Frontend;wasScripted=rig.Scripted;
        foreach(var p in canvases)p.PoseAt(rig,position,rotation);
        markers.Render(rig);frames.Render(rig.Scripted);cycleIcons.Render();fullscreen.Render(rig);cinema.Render(rig);
    }
    internal void Recenter()=>anchored=false;
    internal void SceneChanged()
    {markers.Dispose();discovery.Reset();presentation.Reset();nextFind=0;deepRevision=-1;anchored=false;}
    public void Dispose(){markers.Dispose();cinema.Dispose();frames.Dispose();cycleIcons.Dispose();fullscreen.Dispose();foreach(var p in canvases)p.Restore();canvases.Clear();}
}
