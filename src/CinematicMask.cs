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
    private bool failed,geometryReady,previousBox;
    internal const float Depth=1.55f;
    internal static HandMeshGeometry Geometry(System.Numerics.Vector4 bounds)=>Geometry(bounds,false);
    // 0.1.238: box: on the screen that stands still the head turns away from
    // the frame, so the surround is a closed box round the eyes (the frame's
    // wall with its window, the other five walls whole), Depth from them.
    internal static HandMeshGeometry Geometry(System.Numerics.Vector4 bounds,bool box)
    {
        var g=new HandMeshGeometry(true);var black=new System.Numerics.Vector4(0,0,0,1);
        // Same angular window as a 1480 x 680 rect on the 1920-wide, 2.2 m
        // native UI plane at 1.7 m. Bars overlap the old border's inner edge.
        float x=1480f/1920*2.2f*.5f*Depth/1.7f,y=680f/1920*2.2f*.5f*Depth/1.7f;
        void Bar(float l,float r,float b,float t)
        {var a=new N(l,b,0);var c=new N(r,t,0);g.Quad(a,new N(r,b,0),c,new N(l,t,0),black);g.Quad(a,new N(r,b,0),c,new N(l,t,0),black,true);}
        if(box)bounds=new System.Numerics.Vector4(-Depth,Depth,-Depth,Depth);
        Bar(bounds.X,-x,bounds.Z,bounds.W);Bar(x,bounds.Y,bounds.Z,bounds.W);
        Bar(-x,x,bounds.Z,-y);Bar(-x,x,y,bounds.W);
        if(box)
        {
            // The frame's wall is at z = Depth (the root's origin); the box reaches back to z = -Depth.
            float d=Depth,back=-2*Depth;
            void Wall(N a,N b,N c,N e){g.Quad(a,b,c,e,black);g.Quad(a,b,c,e,black,true);}
            Wall(new N(-d,-d,back),new N(d,-d,back),new N(d,d,back),new N(-d,d,back));
            Wall(new N(-d,-d,back),new N(-d,-d,0),new N(-d,d,0),new N(-d,d,back));
            Wall(new N(d,-d,back),new N(d,-d,0),new N(d,d,0),new N(d,d,back));
            Wall(new N(-d,d,back),new N(d,d,back),new N(d,d,0),new N(-d,d,0));
            Wall(new N(-d,-d,back),new N(d,-d,back),new N(d,-d,0),new N(-d,-d,0));
        }
        return g;
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
            bool box=rig.CinemaScreen;
            if(!geometryReady||box!=previousBox||System.Numerics.Vector4.DistanceSquared(bounds,previous)>1e-8f){visual!.Set(Geometry(bounds,box));previous=bounds;previousBox=box;geometryReady=true;}
            // 0.1.238: turned as the film camera on the screen that stands still (CameraRig.CinemaRotation).
            root.transform.SetPositionAndRotation(rig.CinemaPosition+rig.CinemaRotation*new Vector3(0,0,Depth),rig.CinemaRotation);
            root.SetActive(true);
        }
        catch(Exception ex){failed=true;Dispose();Bootstrap.Warn("CINEMATIC surround unavailable: "+ex.Message);}
    }
    public void Dispose(){visual?.Dispose();visual=null;if(root!=null)UnityEngine.Object.Destroy(root);root=null;geometryReady=false;}
}
