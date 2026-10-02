using System;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// Opaque surround, not a small floating picture frame. This does not depend on
// finding every native animation/material that draws the original border.
internal sealed class CinematicMask : IDisposable
{
    private GameObject? root;
    private RigidMeshVisual? visual;
    private System.Numerics.Vector4 previous;
    private bool failed,geometryReady;
    internal const float Depth=1.55f;
    internal static HandMeshGeometry Geometry(System.Numerics.Vector4 bounds)
    {
        var g=new HandMeshGeometry(true);var black=new System.Numerics.Vector4(0,0,0,1);
        // Same angular window as a 1480 x 680 rect on the 1920-wide, 2.2 m
        // native UI plane at 1.7 m. Bars overlap the old border's inner edge.
        float x=1480f/1920*2.2f*.5f*Depth/1.7f,y=680f/1920*2.2f*.5f*Depth/1.7f;
        void Bar(float l,float r,float b,float t)
        {var a=new N(l,b,0);var c=new N(r,t,0);g.Quad(a,new N(r,b,0),c,new N(l,t,0),black);g.Quad(a,new N(r,b,0),c,new N(l,t,0),black,true);}
        Bar(bounds.X,-x,bounds.Z,bounds.W);Bar(x,bounds.Y,bounds.Z,bounds.W);
        Bar(-x,x,bounds.Z,-y);Bar(-x,x,y,bounds.W);return g;
    }
    // 0.1.182: the bars reach a quarter of the view further on every side
    // (reprojection and the headset's own edges never show the world past them).
    internal const float Extra=.25f;
    internal static System.Numerics.Vector4 Widen(System.Numerics.Vector4 b)
    {float w=(b.Y-b.X)*Extra,h=(b.W-b.Z)*Extra;return new System.Numerics.Vector4(b.X-w,b.Y+w,b.Z-h,b.W+h);}
    internal void Render(CameraRig rig)
    {
        if(failed)return;
        if(!rig.Scripted || rig.MovieActive){if(root!=null)root.SetActive(false);return;}
        try
        {
            if(root==null){visual?.Dispose();geometryReady=false;root=new GameObject("XIII cinematic opaque surround");root.layer=5;visual=new RigidMeshVisual(root.transform,"XIII cinematic bars",true,true);}
            var bounds=Widen(ViewCoverage.Bounds(rig.FrustumLeft,rig.EyeLeft,rig.FrustumRight,rig.EyeRight,Depth));
            if(!geometryReady||System.Numerics.Vector4.DistanceSquared(bounds,previous)>1e-8f){visual!.Set(Geometry(bounds));previous=bounds;geometryReady=true;}
            root.transform.SetPositionAndRotation(rig.HeadPosition+rig.HeadRotation*new Vector3(0,0,Depth),rig.HeadRotation);
            root.SetActive(true);
        }
        catch(Exception ex){failed=true;Dispose();Bootstrap.Warn("CINEMATIC surround unavailable: "+ex.Message);}
    }
    public void Dispose(){visual?.Dispose();visual=null;if(root!=null)UnityEngine.Object.Destroy(root);root=null;geometryReady=false;}
}
