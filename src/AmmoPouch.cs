using System;
using UnityEngine;
namespace XiiiXR;
// The ammunition belt round the waist (0.1.255: an ammo belt made for the mods, BeltModelMath): its pouch in
// the middle of the front (magazines, rockets; 0.1.256), the reserve's shotgun shells in its loops on both hips,
// the reloading hand's side first (the left; mirrored for a left-hander).
internal sealed class AmmoPouch:IDisposable
{
    private readonly GameObject root;
    private readonly ToneMeshVisual mesh;
    private int shells=-1;
    private Quaternion beltHeading=Quaternion.identity;private bool headingSet;
    internal bool Valid=>root!=null;
    internal AmmoPouch()
    {
        root=new GameObject("XIII left belt ammunition pouch");
        mesh=new ToneMeshVisual(root.transform,"XIII ammunition belt");SetShellCount(0);
        Bootstrap.Write("BELT the ammunition belt (a 3D model made for the mods): "+BeltModel.Triangles+" triangles, "+BeltModel.Shells+" shell loops, the pouch in front, the shells on both hips (the left first; the right for a left-hander)");
    }
    internal void SetShellCount(int reserve)
    {int count=BeltModelMath.Shown(reserve);if(count==shells)return;shells=count;mesh.Set(BeltModelMath.Build(count));}
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
        // 0.1.147: mirrored - the belt round the waist, the shells used
        // first on the right hip, the buckle on the other side.
        root.transform.localScale=new Vector3(right?-1:1,1,1);
        root.SetActive(true);
    }
    internal bool Near(Vector3 hand)
    {var local=root.transform.InverseTransformPoint(hand);return BeltModelMath.AtPouch(new System.Numerics.Vector3(local.x,local.y,local.z));}
    internal bool NearShell(Vector3 hand)
    {var p=root.transform.InverseTransformPoint(hand);return BeltModelMath.AtShell(new System.Numerics.Vector3(p.x,p.y,p.z),shells);}
    internal void Hide()=>root.SetActive(false);
    // 0.1.120: centre of the pouch (the reload glow).
    internal Vector3 Center{get{var c=BeltModelMath.PouchCentre;return root.transform.TransformPoint(new Vector3(c.X,c.Y,c.Z));}}
    internal bool Shown=>root!=null&&root.activeSelf;
    internal Mesh? Shape=>mesh.Shape;
    internal Matrix4x4 ShapeToWorld=>mesh.Frame!=null?mesh.Frame.localToWorldMatrix:root.transform.localToWorldMatrix;
    public void Dispose(){mesh.Dispose();UnityEngine.Object.Destroy(root);}
}
