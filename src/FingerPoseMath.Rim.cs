using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace XiiiXR;
internal sealed partial class FingerPoseMath
{
    // These contacts belong to this outfit, never to a static cross-level cache.
    private readonly Dictionary<int,Vector3> rimPads=new();
    private Matrix4x4[]? rimPose;
    private Vector3 rimContact;
    private float rimThickness=.0286f;private bool compactPinch,forwardKey;
    private int rimRevision;
    internal IEnumerable<int> RimDistalBones=>fingers.Append(thumb).Select(c=>c[2]);
    internal int RimMeasuredPads=>rimPads.Count;
    internal float RimPinchError {get;private set;}
    internal bool RimClear {get;private set;}
    internal int[] SetRimPadCloud(int bone,Vector3[] cloud)
    {
        var chain=fingers.Append(thumb).FirstOrDefault(c=>c[2]==bone);
        if(chain==null||cloud.Length<3)return Array.Empty<int>();
        var expected=rest[bone].Translation+(rest[bone].Translation-rest[chain[1]].Translation)*.65f-Vector3.UnitY*.005f;
        // A small patch on the actual distal skin, not a made-up continuation
        // of the PIP->DIP segment. The DIP rotates independently of that segment.
        var patch=Enumerable.Range(0,cloud.Length).Where(i=>NativeHandMesh.Finite(cloud[i]))
            .OrderBy(i=>Vector3.DistanceSquared(cloud[i],expected)).Take(3).ToArray();
        if(patch.Length<3||!Matrix4x4.Invert(rest[bone],out var inverse))return Array.Empty<int>();
        rimPads[bone]=Vector3.Transform(patch.Aggregate(Vector3.Zero,(a,i)=>a+cloud[i])/patch.Length,inverse);
        // 0.1.81: the same distal skin patch gives this finger's thickness.
        MeasureSkinRadius(chain,cloud);
        rimPose=null;powerContacts.Clear();rimRevision++;return patch;
    }
    private readonly Dictionary<string,Vector3> powerContacts=new();
    internal Vector3 PowerContact(string profile)
    {
        if(powerContacts.TryGetValue(profile,out var cached))return cached;
        var pose=Pose(1,0,profile);var center=Vector3.Zero;
        foreach(var chain in fingers)
        {
            var pad=Vector3.Transform(RimLocalPad(chain),pose[chain[2]]);
            var palm=pose[chain[0]].Translation-Vector3.UnitY*.005f;
            center+=(pad+palm)*.5f;
        }
        center=center/fingers.Count+Vector3.UnitZ*NativeHandMesh.WristZ;
        powerContacts[profile]=center;return center;
    }
    private Vector3 RimLocalPad(int[] chain)
    {
        int last=chain[2];if(rimPads.TryGetValue(last,out var pad))return pad;
        Matrix4x4.Invert(rest[last],out var inverse);
        var end=rest[last].Translation+(rest[last].Translation-rest[chain[1]].Translation)*.65f-Vector3.UnitY*.005f;
        return Vector3.Transform(end,inverse);
    }
    internal Vector3[] RimPads(Matrix4x4[] pose)=>fingers.Append(thumb)
        .Select(c=>Vector3.Transform(RimLocalPad(c),pose[c[2]])+Vector3.UnitZ*NativeHandMesh.WristZ).ToArray();
    internal Vector3 RimContact(string profile,float thickness=.0286f)
    {
        if(!profile.Contains("ashtray")&&!KeyGripGeometry.PinchProfile(profile))return Vector3.Zero;
        if(profile.Contains("ashtray"))LeaveAshtrayTray();
        bool compact=KeyGripGeometry.PinchProfile(profile);
        bool key=profile=="key";
        if(compactPinch!=compact||forwardKey!=key){compactPinch=compact;forwardKey=key;rimPose=null;rimRevision++;}
        thickness=float.IsFinite(thickness)?Math.Clamp(thickness,.004f,.065f):.0286f;
        if(Math.Abs(thickness-rimThickness)>.00001f){rimThickness=thickness;rimPose=null;rimRevision++;}
        _=RimPose();return rimContact;
    }
    internal Quaternion RimRotation {get;private set;}=Quaternion.Identity;
    private Matrix4x4[] RimPose()
    {
        if(rimPose!=null)return rimPose;
        var index=fingers[indexFinger];Matrix4x4[] best=(Matrix4x4[])rest.Clone();
        float score=float.PositiveInfinity;
        for(int step=0;step<=18;step++)foreach(float thumbCurl in new[]{.4f,.7f,1f})
        {
            float curl=.35f+step*.03f;var trial=(Matrix4x4[])rest.Clone();
            foreach(var chain in fingers)Bend(trial,chain,ReferenceEquals(chain,index)?curl:.9f,true,false);
            Bend(trial,thumb,thumbCurl,true,true);
            var lower=Vector3.Transform(RimLocalPad(index),trial[index[2]]);
            var upper=Vector3.Transform(RimLocalPad(thumb),trial[thumb[2]]);
            var normal=Vector3.Normalize(upper-lower);
            // The fitted key blade is +Z. Keep that axis along the controller;
            // adapt the thumb to the bow instead of yawing the key to the hand.
            if(forwardKey)
            {
                normal.Z=0;
                if(normal.LengthSquared()<1e-10f)normal=Vector3.UnitX;
                normal=Vector3.Normalize(normal);
            }
            upper=lower+normal*rimThickness;
            SolveRimPad(trial,thumb,upper,true);
            float error=Vector3.Distance(Vector3.Transform(RimLocalPad(thumb),trial[thumb[2]]),upper);
            // Orient the rim across the natural thumb/index pinch. Translating
            // their midpoint without rotating the dish leaves both fingers on
            // the same side. The wrist itself remains controller-aligned.
            var outward=compactPinch?Vector3.UnitZ:lower-rest[index[0]].Translation;
            outward-=normal*Vector3.Dot(outward,normal);
            if(outward.LengthSquared()<1e-10f)continue;outward=Vector3.Normalize(outward);
            foreach(float angle in new[]{0f,-.35f,.35f,-.7f,.7f,-1.05f,1.05f,-1.4f,1.4f,-1.75f,1.75f,-2.1f,2.1f})
            {
                if(forwardKey&&angle!=0)continue;
                var forward=Vector3.Transform(outward,Quaternion.CreateFromAxisAngle(normal,angle));
                var across=Vector3.Normalize(Vector3.Cross(normal,forward));
                var frame=new Matrix4x4(across.X,across.Y,across.Z,0,normal.X,normal.Y,normal.Z,0,forward.X,forward.Y,forward.Z,0,0,0,0,1);
                var edge=lower+normal*(rimThickness*.5f)-forward*AshtrayRimGeometry.Purchase;
                var inverse=Matrix4x4.Transpose(frame);
                bool clear=RimPoseClear(trial,edge,inverse);
                // A narrow key is not the broad dish used by the rim clearance
                // proxy. Do not sacrifice finger contact to clear that slab.
                float candidate=error+Math.Abs(curl-.65f)*.0005f+Math.Abs(angle)*.0001f+Math.Abs(thumbCurl-.7f)*.0001f+(clear?0:forwardKey?.0001f:1);
                if(candidate>=score)continue;
                best=trial;score=candidate;RimRotation=Quaternion.CreateFromRotationMatrix(frame);
                rimContact=edge+Vector3.UnitZ*NativeHandMesh.WristZ;RimPinchError=error;
            }
        }
        RimClear=RimBonesClear(best);
        rimPose=best;return best;
    }
    internal bool RimBonesClear(Matrix4x4[] pose)
    {
        var inverse=Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(RimRotation));
        return RimPoseClear(pose,rimContact-Vector3.UnitZ*NativeHandMesh.WristZ,inverse);
    }
    private bool RimPoseClear(Matrix4x4[] pose,Vector3 edge,Matrix4x4 inverse)
    {
        foreach(var chain in fingers.Append(thumb))
        {
            if(!RimChainClear(pose,chain,edge,inverse))return false;
            if(!AshtrayRimGeometry.SegmentClear(Vector3.Transform(-edge,inverse),Vector3.Transform(rest[chain[0]].Translation-edge,inverse),Vector3.Zero,rimThickness,.008f))return false;
        }
        return true;
    }
    private bool RimChainClear(Matrix4x4[] pose,int[] chain,Vector3 edge,Matrix4x4 inverse)
    {
        Vector3 InTray(Vector3 p)=>Vector3.Transform(p-edge,inverse);
        for(int j=1;j<3;j++)if(!AshtrayRimGeometry.SegmentClear(InTray(pose[chain[j-1]].Translation),InTray(pose[chain[j]].Translation),Vector3.Zero,rimThickness,.003f))return false;
        return AshtrayRimGeometry.SegmentClear(InTray(pose[chain[2]].Translation),InTray(Vector3.Transform(RimLocalPad(chain),pose[chain[2]])),Vector3.Zero,rimThickness,0);
    }
    private void SolveRimPad(Matrix4x4[] pose,int[] chain,Vector3 target,bool isThumb)
    {
        var local=RimLocalPad(chain);int last=chain[2];
        var angles=new float[3];var trial=(Matrix4x4[])pose.Clone();
        for(int pass=0;pass<48;pass++)
        {
            bool changed=false;
            for(int j=2;j>=0;j--)
            {
                var pivot=pose[chain[j]].Translation;
                var a=Vector3.Transform(local,pose[last])-pivot;var b=target-pivot;
                var direction=rest[chain[Math.Min(j+1,2)]].Translation-rest[chain[Math.Max(0,j-1)]].Translation;
                var axis=isThumb?Vector3.Cross(a,b):Vector3.Cross(direction,-Vector3.UnitY);
                if(axis.LengthSquared()<1e-12f)continue;axis=Vector3.Normalize(axis);
                a-=axis*Vector3.Dot(a,axis);b-=axis*Vector3.Dot(b,axis);
                if(a.LengthSquared()<1e-12f||b.LengthSquared()<1e-12f)continue;
                a=Vector3.Normalize(a);b=Vector3.Normalize(b);
                float delta=Math.Clamp(MathF.Atan2(Vector3.Dot(axis,Vector3.Cross(a,b)),Vector3.Dot(a,b)),-.18f,.18f);
                if(!isThumb){float next=Math.Clamp(angles[j]+delta,0,j==0?1.45f:1.8f);delta=next-angles[j];}
                var turn=Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateFromAxisAngle(axis,delta)*Matrix4x4.CreateTranslation(pivot);
                Array.Copy(pose,trial,pose.Length);for(int k=j;k<3;k++)trial[chain[k]]*=turn;
                if(Vector3.DistanceSquared(Vector3.Transform(local,trial[last]),target)>=Vector3.DistanceSquared(Vector3.Transform(local,pose[last]),target)-1e-12f)continue;
                Array.Copy(trial,pose,pose.Length);angles[j]+=delta;changed=true;
            }
            if(!changed||Vector3.DistanceSquared(Vector3.Transform(local,pose[last]),target)<1e-9f)break;
        }
    }
}
