using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
namespace XiiiXR;
// Keeps the game's UI components/events; changes only root presentation.
internal sealed class WorldCanvas
{
    internal readonly Canvas Canvas;
    private readonly RenderMode mode;
    private readonly Camera? camera;
    private readonly Vector3 localPosition,localScale;
    private readonly Quaternion localRotation;
    private readonly Vector2 size,anchorMin,anchorMax,pivot,layoutSize;
    private readonly RectTransform rect;
    private readonly CanvasScaler? scaler;
    private readonly bool scalerEnabled;
    private bool anchored;
    private Vector3 panelPosition;
    private Quaternion panelRotation;
    internal WorldCanvas(Canvas canvas)
    {
        Canvas=canvas;mode=canvas.renderMode;camera=canvas.worldCamera;
        rect=canvas.transform.TryCast<RectTransform>()!;
        localPosition=rect.localPosition;localRotation=rect.localRotation;localScale=rect.localScale;size=rect.sizeDelta;
        // This compact native canvas sizes the Y-cycle icon backgrounds.
        // Expanding its height to 1080 stretches them into tall dark columns.
        bool compact=canvas.name=="WeaponInventoryIndicator";
        layoutSize=compact&&size.x>0&&size.y>0?size:new Vector2(1920,1080);
        if(compact)Bootstrap.Write("UI cycle icons retain native canvas size="+size+"; original sprites/materials retained");
        anchorMin=rect.anchorMin;anchorMax=rect.anchorMax;pivot=rect.pivot;
        scaler=canvas.GetComponent(Il2CppType.Of<CanvasScaler>())?.TryCast<CanvasScaler>();
        scalerEnabled=scaler!=null&&scaler.enabled;
    }
    internal void Pose(CameraRig rig)
    {
        if(Canvas==null||rect==null)return;
        bool fixedPanel=rig.Frontend||GameUiControls.Current?.PointerMenuOpen==true;
        if(!anchored||!fixedPanel)
        {
            panelRotation=fixedPanel?Quaternion.Euler(0,rig.HeadRotation.eulerAngles.y,0):rig.HeadRotation;
            panelPosition=rig.HeadPosition+panelRotation*new Vector3(0,0,1.7f);
        }
        anchored=fixedPanel;
        PoseAt(rig,panelPosition,panelRotation);
    }
    internal void PoseAt(CameraRig rig,Vector3 sharedPosition,Quaternion sharedRotation)
    {
        if(Canvas==null||rect==null)return;
        if(scaler!=null)scaler.enabled=false;
        Canvas.renderMode=RenderMode.WorldSpace;Canvas.worldCamera=rig.MainCamera!;
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=Canvas.name=="WeaponInventoryIndicator"?CycleIcons.LayoutSize(Canvas,layoutSize):layoutSize;
        rect.SetPositionAndRotation(sharedPosition,sharedRotation);
        var s=rect.parent==null?Vector3.one:rect.parent.lossyScale;
        // Preserve signs: taking Abs(parent scale) mirrored the native wheel.
        float Divide(float value)=>2.2f/1920/(Math.Abs(value)<.00001f?(value<0?-.00001f:.00001f):value);
        rect.localScale=new Vector3(Divide(s.x),Divide(s.y),Divide(s.z));
    }
    internal void Recenter()=>anchored=false;
    internal void Restore()
    {
        if(Canvas==null||rect==null)return;
        Canvas.renderMode=mode;Canvas.worldCamera=camera!;
        rect.anchorMin=anchorMin;rect.anchorMax=anchorMax;rect.pivot=pivot;
        rect.localPosition=localPosition;rect.localRotation=localRotation;rect.localScale=localScale;rect.sizeDelta=size;
        if(scaler!=null)scaler.enabled=scalerEnabled;
    }
}
