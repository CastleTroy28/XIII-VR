using System;
using UnityEngine;
namespace XiiiXR;
internal sealed class AmmoPouch:IDisposable
{
    private readonly GameObject root;
    private readonly RigidMeshVisual mesh;
    private int shells=-1;
    private Quaternion beltHeading=Quaternion.identity;private bool headingSet;
    internal bool Valid=>root!=null;
    internal AmmoPouch()
    {
        root=new GameObject("XIII left belt ammunition pouch");
        mesh=new RigidMeshVisual(root.transform,"Brown leather ammunition pouch");SetShellCount(0);
    }
    internal void SetShellCount(int reserve)
    {int count=Math.Clamp(reserve,0,8);if(count==shells)return;shells=count;mesh.Set(AmmoPouchGeometry.Build(count));}
    // 0.1.133: right: on the right side of the belt (the right hand reloads
    // the gun in the left hand).
    internal void Pose(Vector3 head,Quaternion heading,bool right=false)
    {
        // Looking straight down must not spin the belt away from the hand.
        var forward=heading*Vector3.forward;
        if(!headingSet||forward.x*forward.x+forward.z*forward.z>.20f){beltHeading=heading;headingSet=true;}
        heading=beltHeading;
        var pose=BeltAnchorMath.Pose(new System.Numerics.Vector3(head.x,head.y,head.z),
            new System.Numerics.Quaternion(heading.x,heading.y,heading.z,heading.w),right);
        root.transform.SetPositionAndRotation(new Vector3(pose.position.X,pose.position.Y,pose.position.Z),
            new Quaternion(pose.rotation.X,pose.rotation.Y,pose.rotation.Z,pose.rotation.W));
        // 0.1.147: mirrored - the belt round the waist, the shells and the
        // pouch on the right hip, the buckle on the other side.
        root.transform.localScale=new Vector3(right?-1:1,1,1);
        root.SetActive(true);
    }
    internal bool Near(Vector3 hand)
    {var local=root.transform.InverseTransformPoint(hand);return Math.Abs(local.x)<.14f&&local.y>-.14f&&local.y<.19f&&Math.Abs(local.z)<.15f;}
    internal bool NearShell(Vector3 hand)
    {
        var p=root.transform.InverseTransformPoint(hand);
        for(int i=0;i<shells;i++){var c=AmmoPouchGeometry.ShellPosition(i);if(Vector3.Distance(p,new Vector3(c.X,c.Y+.03f,c.Z))<.085f)return true;}
        return false;
    }
    internal void Hide()=>root.SetActive(false);
    // 0.1.120: centre of the pouch (the reload glow).
    internal Vector3 Center=>root.transform.TransformPoint(new Vector3(0,.03f,0));
    internal bool Shown=>root!=null&&root.activeSelf;
    internal Mesh? Shape=>mesh.Shape;
    internal Matrix4x4 ShapeToWorld=>mesh.Frame!=null?mesh.Frame.localToWorldMatrix:root.transform.localToWorldMatrix;
    public void Dispose(){mesh.Dispose();UnityEngine.Object.Destroy(root);}
}
