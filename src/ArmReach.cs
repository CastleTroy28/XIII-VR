using System;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.146: the arm turned so the wrist reaches a point (ArmReachMath).
internal static class ArmReach
{
    // Turns the upper arm and the forearm (weight 0..1 of the way).
    internal static void TwoBone(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole,float weight)
    {
        if(!(weight>0))return;
        var s=upper.position;var e=lower.position;var w=end.position;
        var (elbow,wrist)=ArmReachMath.Solve(V(s),V(e),V(w),V(target),V(pole));
        var toElbow=U(elbow)-s;if(toElbow.sqrMagnitude<1e-8f||(e-s).sqrMagnitude<1e-8f)return;
        upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(e-s,toElbow)*upper.rotation,weight);
        var e2=lower.position;var w2=end.position;var toWrist=U(wrist)-e2;
        if(toWrist.sqrMagnitude<1e-8f||(w2-e2).sqrMagnitude<1e-8f)return;
        lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(w2-e2,toWrist)*lower.rotation,weight);
    }
    private static N V(Vector3 v)=>new(v.x,v.y,v.z);
    private static Vector3 U(N v)=>new(v.X,v.Y,v.Z);
}
