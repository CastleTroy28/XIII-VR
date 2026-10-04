using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace XiiiXR;
// Drives native UI events from a tracked controller. No desktop mouse movement
// and no direct invocation of menu button UnityEvents.
internal sealed class VrMenuPointer : IDisposable
{
    private readonly CameraRig rig;
    private readonly UiPointerState state=new();
    private readonly UiPointerState promptState=new();
    private readonly MenuPrompts prompts=new();
    private readonly MenuKeyboard keyboard;
    private bool promptMode;
    private readonly List<GraphicRaycaster> raycasters=new();
    private readonly Dictionary<int,(Canvas canvas,Camera? previous)> eventCameras=new();
    private readonly Il2CppSystem.Collections.Generic.List<RaycastResult> results=new();
    private Camera? eventCamera;
    private GameObject? cameraRoot;
    private readonly MenuBeam beam=new();
    private EventSystem? system;
    private PointerEventData? data,probe;
    private GameObject? hovered,pressed,drag;
    private float nextFind,nextError;
    private bool active,wheelMode;
    private int lastHoverId;
    internal VrMenuPointer(CameraRig owner){rig=owner;keyboard=new MenuKeyboard(owner);}
    internal void Tick()
    {
        try
        {
            keyboard.Tick();
            // 0.1.210: the pointer works in VR without Windows focus too.
            bool valid=WindowFocus.Playable&&rig.HeadTrackingValid&&rig.MenuPointerControls.Valid
                &&GameUiControls.Current?.PointerMenuOpen==true&&!QualityMenu.Open&&!ControlsSheet.Open&&rig.SamplePointerHand(out _);
            if(!valid){Cancel();return;}
            if(!rig.SamplePointerHand(out var hand)){Cancel();return;}
            rig.DisarmMenuTriggers(); // Only gameplay channels; UI reads independent channels.
            var origin=CameraRig.UnityPosition(hand);var direction=rig.PointerRotation(hand)*Vector3.forward;var pointerInput=rig.MenuPointerControls;
            bool nowWheel=GameUiControls.Current?.WheelOpen==true;
            if(nowWheel!=wheelMode){Cancel();wheelMode=nowWheel;}
            if(GameUiControls.Current?.WheelOpen==true)
            {
                CancelEvents();active=true;
                var items=GameUiControls.Current.Items;items.Hover();
                var point=items.Hovered?items.Point:origin+direction*1.7f;
                int pointed=-1;var ui=GameUiControls.Current;
                bool stickActive=rig.LeftStick.Valid&&rig.LeftStick.Value.LengthSquared()>.09f;
                if(!items.Hovered&&ui.Wheel?.wheelSlices!=null)
                {
                    var slices=ui.Wheel.wheelSlices;
                    for(int i=0;i<slices.Length;i++)
                    {
                        var slice=slices[i];var icon=slice?.weaponIconComponent;
                        if(icon==null||!icon.gameObject.activeInHierarchy||!slice!.CanHover())continue;
                        var rect=icon.rectTransform;
                        if(!MenuRayMath.Plane(ContactWorld.V(origin),ContactWorld.V(direction),ContactWorld.V(rect.position),ContactWorld.V(rect.forward),out var p,out _))continue;
                        var local=rect.InverseTransformPoint(ContactWorld.U(p));
                        if(!rect.rect.Contains(new Vector2(local.x,local.y)))continue;
                        point=ContactWorld.U(p);
                        if(!stickActive&&ui.PointWeapon(i))pointed=i;
                        break;
                    }
                }
                bool held=(pointerInput.Held&HandControls.Trigger)!=0;
                int targetId=items.Hovered?1:pointed>=0?pointed+2:0;
                var step=state.Sample(true,held,targetId);
                ShowRay(origin,point,targetId!=0);
                if(step.Click){if(items.Hovered)ui.ConfirmPointedItem();else if(pointed>=0)ui.ConfirmPointedWeapon();}
                return;
            }
            if(prompts.Hit(origin,direction,out var prompt,out var promptPoint,out ushort promptKey))
            {
                if(!promptMode){CancelEvents();state.Sample(false,false,0);promptMode=true;}
                bool held=(pointerInput.Held&HandControls.Trigger)!=0;
                var step=promptState.Sample(true,held,prompt!.GetInstanceID());
                ShowRay(origin,promptPoint,true);
                if(step.Click)keyboard.Pulse(promptKey);
                return;
            }
            if(promptMode){promptState.Sample(false,false,0);promptMode=false;state.Sample(false,false,0);}
            var current=EventSystem.current;
            if(current==null){Cancel();ShowRay(origin,origin+direction*1.7f,false);return;}
            if(system!=current){Cancel();system=current;data=new PointerEventData(current);probe=new PointerEventData(current);}
            if(!active){state.Sample(false,false,0);active=true;}
            EnsureCamera();eventCamera!.transform.SetPositionAndRotation(rig.HeadPosition,rig.HeadRotation);
            eventCamera.pixelRect=new Rect(0,0,Math.Max(1,Screen.width),Math.Max(1,Screen.height));
            eventCamera.aspect=Math.Max(1,Screen.width)/(float)Math.Max(1,Screen.height);
            if(SceneScan.Due(ref nextFind,.5f))
            {
                long scanStart=SceneScan.Begin();raycasters.Clear();
                foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<GraphicRaycaster>()))
                {var r=obj.TryCast<GraphicRaycaster>();if(r!=null&&r.gameObject.scene.IsValid())raycasters.Add(r);}
                SceneScan.End("raycasters",scanStart);
            }
            RaycastResult best=new();bool found=false;int bestOrder=int.MinValue,bestDepth=int.MinValue;float bestDistance=float.PositiveInfinity;
            Vector2 screen=data!.position;Vector3 hitPoint=origin+direction*1.7f;
            foreach(var r in raycasters)
            {
                if(r==null||!r.isActiveAndEnabled)continue;
                var c=r.canvas;if(c==null||c.renderMode!=RenderMode.WorldSpace||!c.gameObject.activeInHierarchy)continue;
                var t=c.transform;
                if(!MenuRayMath.Plane(ContactWorld.V(origin),ContactWorld.V(direction),ContactWorld.V(t.position),ContactWorld.V(t.forward),out var p,out float distance))continue;
                var wp=ContactWorld.U(p);var sp=eventCamera.WorldToScreenPoint(wp);
                if(sp.z<=0)continue;
                int id=c.GetInstanceID();if(!eventCameras.ContainsKey(id))eventCameras.Add(id,(c,c.worldCamera));
                c.worldCamera=eventCamera;
                probe!.position=new Vector2(sp.x,sp.y);results.Clear();
                bool reverse=r.ignoreReversedGraphics;var blocking=r.blockingObjects;
                try{r.ignoreReversedGraphics=false;r.blockingObjects=GraphicRaycaster.BlockingObjects.None;r.Raycast(probe,results);}
                finally{r.ignoreReversedGraphics=reverse;r.blockingObjects=blocking;}
                for(int i=0;i<results.Count;i++)
                {
                    var hit=results[i];if(hit.gameObject==null)continue;
                    int order=c.sortingOrder;
                    if(found&&(order<bestOrder||order==bestOrder&&(distance>bestDistance+.01f||Math.Abs(distance-bestDistance)<=.01f&&hit.depth<=bestDepth)))continue;
                    best=hit;best.worldPosition=wp;best.worldNormal=-direction;found=true;
                    bestOrder=order;bestDepth=hit.depth;bestDistance=distance;screen=probe.position;hitPoint=wp;
                }
            }
            var target=found?best.gameObject:null;
            data!.pointerId=-101;data.button=PointerEventData.InputButton.Left;
            data.delta=screen-data.position;data.position=screen;data.pointerCurrentRaycast=best;
            if(keyboard.StickMode)
            {
                // Controller jitter must not steal the keyboard-selected row.
                CancelEvents();state.Sample(false,false,0);ShowRay(origin,hitPoint,false);return;
            }
            SetHover(target);
            var clickTarget=target==null?null:ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            var downTarget=target==null?null:ExecuteEvents.GetEventHandler<IPointerDownHandler>(target);
            var effective=downTarget!=null?downTarget:clickTarget;
            bool trigger=(pointerInput.Held&HandControls.Trigger)!=0;
            if(trigger&&drag!=null&&!data.dragging&&(data.position-data.pressPosition).sqrMagnitude>100)
            {data.dragging=true;data.eligibleForClick=false;ExecuteEvents.Execute<IBeginDragHandler>(drag,data,ExecuteEvents.beginDragHandler);}
            var action=state.Sample(true,trigger,effective==null?0:effective.GetInstanceID(),data.dragging);
            if(action.Down)
            {
                data.eligibleForClick=true;data.pressPosition=data.position;data.pointerPressRaycast=best;data.clickCount=1;data.clickTime=Time.unscaledTime;
                data.rawPointerPress=target!;
                pressed=ExecuteEvents.ExecuteHierarchy<IPointerDownHandler>(target!,data,ExecuteEvents.pointerDownHandler);
                if(pressed==null)pressed=clickTarget;data.pointerPress=pressed!;
                drag=ExecuteEvents.GetEventHandler<IDragHandler>(target!);data.pointerDrag=drag!;
                if(drag!=null)ExecuteEvents.Execute<IInitializePotentialDragHandler>(drag,data,ExecuteEvents.initializePotentialDrag);
                Bootstrap.Write("MENU POINTER press="+target!.name);
            }
            if(trigger&&data.dragging&&drag!=null)ExecuteEvents.Execute<IDragHandler>(drag,data,ExecuteEvents.dragHandler);
            if(action.Up)Release(action.Click&&data.eligibleForClick);
            var stick=rig.RightStick;
            if(target!=null&&stick.Valid&&Math.Abs(stick.Value.Y)>.35f)
            {data.scrollDelta=new Vector2(0,stick.Value.Y*Time.unscaledDeltaTime*6);ExecuteEvents.ExecuteHierarchy<IScrollHandler>(target,data,ExecuteEvents.scrollHandler);}
            ShowRay(origin,hitPoint,effective!=null);
            int hoveredId=effective==null?0:effective.GetInstanceID();
            if(hoveredId!=lastHoverId){lastHoverId=hoveredId;if(effective!=null)Bootstrap.Write("MENU POINTER hover="+effective.name);}
        }
        catch(Exception ex)
        {
            Cancel();if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("MENU POINTER recovering: "+ex);}
        }
    }
    private void EnsureCamera()
    {
        if(eventCamera!=null)return;
        cameraRoot=new GameObject("XIII UI event camera");UnityEngine.Object.DontDestroyOnLoad(cameraRoot);
        eventCamera=cameraRoot.AddComponent(Il2CppType.Of<Camera>()).TryCast<Camera>()!;
        eventCamera.enabled=false;eventCamera.stereoTargetEye=StereoTargetEyeMask.None;eventCamera.cullingMask=0;
        eventCamera.fieldOfView=60;eventCamera.nearClipPlane=.01f;eventCamera.farClipPlane=100;
    }
    private void SetHover(GameObject? target)
    {
        var next=target==null?null:ExecuteEvents.GetEventHandler<IPointerEnterHandler>(target);
        if(hovered==next)return;
        if(hovered!=null&&data!=null)ExecuteEvents.Execute<IPointerExitHandler>(hovered,data,ExecuteEvents.pointerExitHandler);
        hovered=next;if(data!=null)data.pointerEnter=next!;
        if(next!=null&&data!=null)ExecuteEvents.Execute<IPointerEnterHandler>(next,data,ExecuteEvents.pointerEnterHandler);
    }
    private void Release(bool click)
    {
        if(data==null)return;
        var p=pressed;pressed=null;
        if(p!=null)ExecuteEvents.Execute<IPointerUpHandler>(p,data,ExecuteEvents.pointerUpHandler);
        if(click&&p!=null)ExecuteEvents.Execute<IPointerClickHandler>(p,data,ExecuteEvents.pointerClickHandler);
        if(data.dragging&&drag!=null)ExecuteEvents.Execute<IEndDragHandler>(drag,data,ExecuteEvents.endDragHandler);
        drag=null;data.pointerPress=null!;data.pointerDrag=null!;data.rawPointerPress=null!;data.eligibleForClick=false;data.dragging=false;
    }
    private void CancelEvents(){Release(false);SetHover(null);}
    private void Cancel()
    {
        state.Sample(false,false,0);promptState.Sample(false,false,0);promptMode=false;active=false;
        try{CancelEvents();}catch{pressed=hovered=drag=null;}
        foreach(var v in eventCameras.Values)if(v.canvas!=null)v.canvas.worldCamera=v.previous!;eventCameras.Clear();
        beam.Hide();lastHoverId=0;
    }
    private void ShowRay(Vector3 from,Vector3 to,bool hit)
    {
        // Drawing is optional; failure must never cancel a pointer press.
        try{beam.Show(from,to,hit);}
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("MENU RAY visual: "+ex.Message);}}
    }
    public void Dispose()
    {
        Cancel();keyboard.Dispose();beam.Dispose();if(cameraRoot!=null)UnityEngine.Object.Destroy(cameraRoot);
    }
}
