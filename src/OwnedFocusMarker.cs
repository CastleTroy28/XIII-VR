using System;
using Vector4=System.Numerics.Vector4;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using V=UnityEngine.Vector3;
namespace XiiiXR;
// Independent world geometry: no desktop CanvasGroup, RectMask or pixel scale.
internal sealed class OwnedFocusMarker:IDisposable
{
    private GameObject? root;
    private RigidMeshVisual? icon;
    private TextMeshPro? label;
    private bool edge,ready;
    private readonly bool secondary;
    internal bool Seen;
    internal OwnedFocusMarker(FocusMarker marker,FocusHUDElement? native)
    {
        secondary=marker.secondaryMarker;
        try
        {
            root=new GameObject("XIII objective marker");root.layer=0;
            icon=new RigidMeshVisual(root.transform,"XIII objective diamond",true,true);
            var font=native?.distanceIndicator?.font;
            if(font!=null)
            {
                var text=new GameObject("XIII objective distance");text.layer=0;text.transform.SetParent(root.transform,false);
                text.transform.localPosition=new V(0,-.043f,0);text.transform.localScale=V.one*.006f;
                label=text.AddComponent(Il2CppType.Of<TextMeshPro>()).TryCast<TextMeshPro>();
                if(label!=null){label.font=font;label.fontSize=3;label.alignment=TextAlignmentOptions.Center;label.color=Color.white;label.rectTransform.sizeDelta=new UnityEngine.Vector2(16,5);}
            }
            root.SetActive(false);
        }
        catch{Dispose();throw;}
    }
    internal void Show(bool visible){if(root!=null&&root.activeSelf!=visible)root.SetActive(visible);}
    internal void Pose(V position,UnityEngine.Quaternion rotation,bool atEdge,float angle,float distance)
    {
        if(root==null)return;
        if(!ready||edge!=atEdge)
        {
            edge=atEdge;ready=true;var mesh=new HandMeshGeometry(true);
            var color=secondary?new Vector4(1,.78f,.18f,1):new Vector4(.2f,.9f,1,1);
            if(atEdge)
            {
                var a=new System.Numerics.Vector3(-.025f,-.022f,0);var b=new System.Numerics.Vector3(.025f,-.022f,0);var c=new System.Numerics.Vector3(0,.026f,0);
                mesh.Tri(a,b,c,color);mesh.Tri(c,b,a,color);
            }
            else for(int i=0;i<4;i++)
            {
                float a=i*MathF.PI/2,b=(i+1)*MathF.PI/2;
                System.Numerics.Vector3 P(float t,float r)=>new(MathF.Sin(t)*r,MathF.Cos(t)*r,0);
                mesh.Quad(P(a,.03f),P(b,.03f),P(b,.022f),P(a,.022f),color);
                mesh.Quad(P(a,.03f),P(b,.03f),P(b,.022f),P(a,.022f),color,true);
            }
            icon!.Set(mesh);
        }
        root.transform.SetPositionAndRotation(position,rotation*(atEdge?UnityEngine.Quaternion.Euler(0,0,-angle):UnityEngine.Quaternion.identity));
        if(label!=null){label.text=MathF.Round(distance)+" m";label.transform.localRotation=atEdge?UnityEngine.Quaternion.Euler(0,0,angle):UnityEngine.Quaternion.identity;}
        Show(true);
    }
    public void Dispose(){icon?.Dispose();icon=null;if(root!=null)UnityEngine.Object.Destroy(root);root=null;label=null;}
}
