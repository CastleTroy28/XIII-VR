using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// 0.1.119: M60 top cover. Found from the rig ("wpn_m60_lid"); its vertices
// give the hinge and the handle point. The reload state decides open/closed,
// the turn is animated here.
// 0.1.120: the cover swings open AWAY from the shooter: hinge at its front
// edge, the rear edge (the handle) rises (0.1.119 had it the other way).
internal sealed partial class WeaponVisual
{
    internal const float CoverOpenDegrees=75,CoverDegreesPerSecond=420;
    internal bool CoverOpen{get;set;}
    private bool coverReady;private Vector3 coverHinge,coverFront;private float coverAngle;private Matrix4x4 coverMeshToFit;
    private WeaponMechanism? coverMechanism;
    // Where the left hand takes the cover (fitted gun space), at its current angle.
    internal Vector3? CoverGrab=>coverReady?CoverPoint(coverFront,coverAngle):null;
    // The cover outline follows the cover's current opening (fitted gun space).
    internal Matrix4x4 CoverShapeToFit=>Matrix4x4.Translate(coverHinge)*Matrix4x4.Rotate(Quaternion.AngleAxis(coverAngle,Vector3.right))*Matrix4x4.Translate(-coverHinge);
    private Vector3 CoverPoint(Vector3 point,float degrees)=>coverHinge+Quaternion.AngleAxis(degrees,Vector3.right)*(point-coverHinge);
    // Middle of the cover (the glow while it has to be opened).
    internal Vector3? CoverMiddle=>coverReady?CoverPoint((coverHinge+coverFront)*.5f,coverAngle):null;
    private void PrepareCover(Part part,Transform[] bones,int cover,Mesh attachment,BoneWeight[] weights)
    {
        try
        {
            var set=new List<int>();
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null&&(i==cover||bones[i].IsChildOf(bones[cover])))set.Add(i);
            var inCover=new HashSet<int>(set);
            var vertices=attachment.vertices;if(vertices.Length!=weights.Length)throw new InvalidOperationException("cover vertices/weights mismatch");
            coverMeshToFit=fitMatrix*part.Matrix;
            var points=new List<Vector3>();
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];float sum=(inCover.Contains(w.boneIndex0)?w.weight0:0)+(inCover.Contains(w.boneIndex1)?w.weight1:0)+(inCover.Contains(w.boneIndex2)?w.weight2:0)+(inCover.Contains(w.boneIndex3)?w.weight3:0);
                if(sum>.5f)points.Add(coverMeshToFit.MultiplyPoint3x4(vertices[i]));
            }
            if(points.Count<8)throw new InvalidOperationException("cover has "+points.Count+" vertices");
            float zMin=float.PositiveInfinity,zMax=float.NegativeInfinity;foreach(var p in points){zMin=Math.Min(zMin,p.z);zMax=Math.Max(zMax,p.z);}
            Vector3 Band(float from,float to){var sum=Vector3.zero;int n=0;foreach(var p in points)if(p.z>=from&&p.z<=to){sum+=p;n++;}return n>0?sum/n:new Vector3(0,0,from);}
            // Hinge at the front edge; the handle (coverFront) is the rear edge.
            coverHinge=Band(zMax-.012f,zMax);coverFront=Band(zMin,zMin+.02f);
            if(!(zMax-zMin>.03f))throw new InvalidOperationException("cover too short: "+(zMax-zMin));
            try
            {
                if(CoverShape!=null)UnityEngine.Object.Destroy(CoverShape);
                CoverShape=ExtractPart(attachment,coverMeshToFit,Weighted(weights,inCover),false,out _,"XIII cover outline source");
            }
            catch(Exception ex){CoverShape=null;Bootstrap.Warn("RELOAD OUTLINE cover shape unavailable: "+ex.Message);}
            coverMechanism=part.Mechanism;
            if(coverMechanism!=null)coverMechanism.CoverBones=set.ToArray();
            coverReady=true;
            Bootstrap.Write("M60 COVER bones="+set.Count+" vertices="+points.Count+" hinge="+coverHinge.ToString("F3")+" front="+coverFront.ToString("F3")+" length="+(zMax-zMin).ToString("F3")+" animated="+(coverMechanism!=null));
        }
        catch(Exception ex){coverReady=false;Bootstrap.Warn("M60 COVER unavailable (reload still works, the cover is not drawn open): "+ex.Message);}
    }
    // Called once per animated frame, before the gun mesh is baked.
    private void TickCover()
    {
        if(!coverReady||coverMechanism==null)return;
        float target=CoverOpen?CoverOpenDegrees:0;
        float step=CoverDegreesPerSecond*Math.Max(0,Time.deltaTime);
        coverAngle=Mathf.MoveTowards(coverAngle,target,step);
        coverMechanism.CoverActive=coverAngle>.2f;
        if(!coverMechanism.CoverActive)return;
        var inFit=Matrix4x4.Translate(coverHinge)*Matrix4x4.Rotate(Quaternion.AngleAxis(coverAngle,Vector3.right))*Matrix4x4.Translate(-coverHinge);
        coverMechanism.CoverPose=coverMeshToFit.inverse*inFit*coverMeshToFit;
    }
}
