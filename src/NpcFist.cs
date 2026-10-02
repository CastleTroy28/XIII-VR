using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using PlayMagic.AI;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// A closed hand is an absolute bind-relative pose, not another curl added to a rifle grip.
// The wrist's anatomical axes are calibrated from the same bind pose as the fingers.
internal sealed class NpcFist
{
    private const int Chains=5,Joints=15;
    private Transform wrist=null!;
    private readonly Transform?[] bones=new Transform?[Joints];
    private readonly Quaternion[] closed=new Quaternion[Joints],basis=new Quaternion[Joints],applied=new Quaternion[Joints];
    private readonly bool[] has=new bool[Joints];
    private Quaternion handFrame,wristBase,wristApplied;private bool wristHas;
    private Vector3 knuckle;
    internal Vector3 Knuckle=>wrist.TransformPoint(knuckle);
    internal static string Name(bool right,int chain,int joint)=>(right?"R":"L")+(chain<4?"_Finger_"+(chain+1).ToString("00"):"_Thumb_01")+"_"+(joint+1).ToString("00")+"SHJnt";
    internal static NpcFist? Create(NPC npc,Transform? wrist,bool right,out string why)
    {
        why="no usable bind-pose hand";if(npc==null||wrist==null)return null;
        try
        {
            foreach(var o in npc.GetComponentsInChildren(Il2CppType.Of<SkinnedMeshRenderer>(),true))
            {
                var skin=o.TryCast<SkinnedMeshRenderer>();if(skin==null||skin.sharedMesh==null)continue;
                var list=skin.bones;var bind=skin.sharedMesh.bindposes;
                if(list==null||bind==null||list.Length!=bind.Length)continue;
                var ids=new Dictionary<IntPtr,int>();var names=new Dictionary<string,int>();int wi=-1;
                for(int i=0;i<list.Length;i++)if(list[i]!=null){ids[list[i].Pointer]=i;names[NpcBoneNames.Key(list[i].name)]=i;if(list[i].Pointer==wrist.Pointer)wi=i;}
                if(wi<0)continue;
                var indices=new int[Joints];Array.Fill(indices,-1);int complete=0;
                for(int c=0;c<Chains;c++)
                {
                    bool all=true;
                    for(int j=0;j<3;j++)
                    {
                        string wanted=NpcBoneNames.Key(Name(right,c,j));int found=-1;
                        if(names.TryGetValue(wanted,out int exact))found=exact;
                        else foreach(var pair in names)if(pair.Key.EndsWith("_"+wanted,StringComparison.Ordinal)){found=pair.Value;break;}
                        indices[c*3+j]=found;if(found<0)all=false;
                    }
                    if(all){if(c<4)complete++;}else for(int j=0;j<3;j++)indices[c*3+j]=-1;
                }
                if(complete<3||indices[14]<0)continue;
                Vector3 At(int i)=>bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
                var knuckles=new List<N>();var center=Vector3.zero;
                for(int c=0;c<4;c++)if(indices[c*3]>=0){var p=At(indices[c*3]);center+=p;knuckles.Add(V(p));}
                center/=complete;
                var back=U(FistCurlMath.Back(V(At(wi)),knuckles.ToArray(),V(At(indices[14])),right));
                var forward=center-At(wi);if(back.sqrMagnitude<1e-8f||forward.sqrMagnitude<1e-8f)continue;
                var localForward=bind[wi].MultiplyVector(forward).normalized;
                var localBack=bind[wi].MultiplyVector(back).normalized;
                int curledJoints=0;
                var f=new NpcFist{wrist=wrist,handFrame=Quaternion.LookRotation(localForward,localBack),knuckle=bind[wi].MultiplyPoint3x4(center)};
                for(int c=0;c<Chains;c++)
                {
                    if(indices[c*3]<0)continue;
                    var segment=At(indices[c*3+1])-At(indices[c*3]);var axis=U(FistCurlMath.Hinge(V(segment),V(back)));
                    for(int j=0;j<3;j++)
                    {
                        int n=c*3+j,i=indices[n];var bone=list[i];var parent=bone.parent;
                        if(parent==null||!ids.TryGetValue(parent.Pointer,out int pi))continue;
                        var rest=bind[pi]*bind[i].inverse;
                        if(!Frame(rest,out var restTurn))continue;
                        var hinge=bind[pi].MultiplyVector(axis).normalized;
                        float degrees=(c<4?FistCurlMath.Finger:FistCurlMath.Thumb)[j];
                        var pose=Quaternion.AngleAxis(degrees,hinge)*restTurn;
                        if(c==4&&j==0)
                        {
                            // Oppose the thumb across the outside of the closed fingers.
                            var toward=center-At(i);toward-=back*Vector3.Dot(toward,back);
                            var flat=segment-back*Vector3.Dot(segment,back);
                            float oppose=Vector3.SignedAngle(flat,toward,back);
                            pose=Quaternion.AngleAxis(Math.Clamp(oppose,-38,38),bind[pi].MultiplyVector(back).normalized)*pose;
                        }
                        f.bones[n]=bone;f.closed[n]=pose;curledJoints++;
                    }
                }
                if(curledJoints<12){why="incomplete parent bind poses ("+curledJoints+" joints)";continue;}
                why=complete+" finger chains, thumb and calibrated wrist";return f;
            }
        }
        catch(Exception ex){why=ex.Message;}
        return null;
    }
    // Called BEFORE solving the arm, once or several times per rendered frame.
    internal void RestoreWrist()
    {
        if(wristHas&&Math.Abs(Quaternion.Dot(wrist.localRotation,wristApplied))>.999999f)wrist.localRotation=wristBase;
        wristBase=wrist.localRotation;wristHas=false;
    }
    internal void Orient(Vector3 forward,Vector3 back,float weight)
    {
        if(forward.sqrMagnitude<1e-8f)return;
        back-=forward.normalized*Vector3.Dot(back,forward.normalized);
        if(back.sqrMagnitude<1e-8f)return;
        wrist.rotation=Quaternion.Slerp(wrist.rotation,Quaternion.LookRotation(forward,back)*Quaternion.Inverse(handFrame),Mathf.Clamp01(weight));
        wristApplied=wrist.localRotation;wristHas=true;
    }
    internal void Clench(float weight)
    {
        weight=Mathf.Clamp01(weight);
        for(int i=0;i<Joints;i++)
        {
            var t=bones[i];if(t==null)continue;
            var current=t.localRotation;
            if(has[i]&&Math.Abs(Quaternion.Dot(current,applied[i]))>.999999f)current=basis[i];
            basis[i]=current;t.localRotation=Quaternion.Slerp(current,closed[i],weight);applied[i]=t.localRotation;has[i]=true;
        }
    }
    internal void Release()
    {
        try{RestoreWrist();}catch(Exception){}
        for(int i=0;i<Joints;i++)
        {
            try{var t=bones[i];if(t!=null&&has[i]&&Math.Abs(Quaternion.Dot(t.localRotation,applied[i]))>.999999f)t.localRotation=basis[i];}catch(Exception){}
            has[i]=false;
        }
    }
    private static bool Frame(Matrix4x4 m,out Quaternion turn)
    {
        turn=Quaternion.identity;var f=(Vector3)m.GetColumn(2);var u=(Vector3)m.GetColumn(1);
        if(m.determinant<=0||f.sqrMagnitude<1e-12f||u.sqrMagnitude<1e-12f)return false;
        turn=Quaternion.LookRotation(f,u);return true;
    }
    private static N V(Vector3 v)=>new(v.x,v.y,v.z);
    private static Vector3 U(N v)=>new(v.X,v.Y,v.Z);
}
