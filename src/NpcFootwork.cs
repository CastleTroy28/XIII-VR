using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.AI;
using N=System.Numerics;
namespace XiiiXR;
internal sealed class NpcFootwork
{
    private readonly Transform[] joints=new Transform[6];
    private readonly Quaternion[] basis=new Quaternion[6],applied=new Quaternion[6];
    private readonly bool[] has=new bool[6];
    private readonly Quaternion[] footFrame=new Quaternion[2];
    private readonly float[] height=new float[2];
    private readonly BrawlFootwork plan=new();
    private float scale;private int frame=-1;
    internal static NpcFootwork? Create(NpcBones rig,Transform root)
    {
        var l=Foot(rig.leftKnee);var r=Foot(rig.rightKnee);
        if(rig.leftHip==null||rig.rightHip==null||rig.leftKnee==null||rig.rightKnee==null||l==null||r==null)return null;
        var f=new NpcFootwork();f.joints[0]=rig.leftHip;f.joints[1]=rig.leftKnee;f.joints[2]=l;
        f.joints[3]=rig.rightHip;f.joints[4]=rig.rightKnee;f.joints[5]=r;
        var yaw=Quaternion.Euler(0,root.eulerAngles.y,0);
        for(int s=0;s<2;s++){f.footFrame[s]=Quaternion.Inverse(yaw)*f.joints[s*3+2].rotation;f.height[s]=f.joints[s*3+2].position.y-root.position.y;}
        float leg=Vector3.Distance(rig.leftHip.position,rig.leftKnee.position)+Vector3.Distance(rig.leftKnee.position,l.position);
        f.scale=Math.Clamp(leg/.85f,.75f,1.3f);return f;
    }
    private static Transform? Foot(Transform? knee)
    {
        if(knee==null)return null;
        foreach(var o in knee.GetComponentsInChildren(Il2CppType.Of<Transform>(),true))
        {
            var t=o.TryCast<Transform>();if(t==null||t.Pointer==knee.Pointer)continue;
            string n=NpcBoneNames.Key(t.name);
            if((n.Contains("ankle")||n.Contains("foot"))&&!n.Contains("ik")&&!n.Contains("toe")&&!n.Contains("target")&&!n.Contains("end"))return t;
        }
        return null;
    }
    internal void Restore()
    {
        for(int i=0;i<6;i++)
        {
            var t=joints[i];if(t==null)continue;var q=t.localRotation;
            if(has[i]&&Math.Abs(Quaternion.Dot(q,applied[i]))>.999999f)q=basis[i];
            basis[i]=q;t.localRotation=q;has[i]=false;
        }
    }
    internal void Apply(Transform root,Quaternion yaw,bool leadLeft,float speed,float weight)
    {
        if(weight<.05f){plan.Reset();started=false;return;}
        if(frame!=Time.frameCount)
        {
            frame=Time.frameCount;
            var left=Ground(root.position+yaw*new Vector3(-.17f*scale,0,(leadLeft?.18f:-.18f)*scale),height[0]);
            var right=Ground(root.position+yaw*new Vector3(.17f*scale,0,(leadLeft?-.18f:.18f)*scale),height[1]);
            // Ground supplies heights; initial foot positions preserve a smooth entry from the old stance.
            if(!started){left=joints[2].position;right=joints[5].position;started=true;}
            plan.Advance(Time.time,V(root.position),new N.Quaternion(yaw.x,yaw.y,yaw.z,yaw.w),V(left),V(right),leadLeft,speed,scale);
        }
        for(int s=0;s<2;s++)
        {
            int i=s*3;var hip=joints[i];var knee=joints[i+1];var foot=joints[i+2];
            var pole=hip.position+yaw*new Vector3(s==0?-.12f:.12f,-.3f,.5f);
            ArmReach.TwoBone(hip,knee,foot,U(plan.Feet[s]),pole,weight);
            foot.rotation=Quaternion.Slerp(foot.rotation,yaw*footFrame[s],weight);
        }
        for(int i=0;i<6;i++){applied[i]=joints[i].localRotation;has[i]=true;}
    }
    private bool started;
    private static Vector3 Ground(Vector3 at,float ankleHeight)
    {if(NavMesh.SamplePosition(at,out var h,.35f,NavMesh.AllAreas))at.y=h.position.y;at.y+=ankleHeight;return at;}
    internal void Release(){Restore();plan.Reset();started=false;}
    private static N.Vector3 V(Vector3 p)=>new(p.x,p.y,p.z);
    private static Vector3 U(N.Vector3 p)=>new(p.X,p.Y,p.Z);
}
