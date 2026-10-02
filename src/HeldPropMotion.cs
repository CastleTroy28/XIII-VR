using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace XiiiXR;
// Grabbed bodies use collision-tested poses, independent of native drag/mass.
// The same native colliders remain on the body for released-flight physics.
internal sealed class HeldPropMotion
{
    private readonly Il2CppReferenceArray<Collider> overlaps=new(96);
    private readonly List<Collider> parts=new();
    internal void Bind(Rigidbody body)
    {
        parts.Clear();
        foreach(var c in body.GetComponentsInChildren(Il2CppType.Of<Collider>(),true))
        {var part=c.TryCast<Collider>();if(part!=null&&part.enabled&&!part.isTrigger&&part.attachedRigidbody==body)parts.Add(part);}
    }
    internal void Follow(Rigidbody body,Vector3 target,Quaternion rotation,float dt)
    {
        Vector3 start=body.position,step=Vector3.ClampMagnitude(target-start,Math.Min(.18f,8*dt));
        float distance=step.magnitude;
        if(distance>.0001f)
        {
            var direction=step/distance;
            if(body.SweepTest(direction,out RaycastHit hit,distance+.002f,QueryTriggerInteraction.Ignore))
                distance=Math.Max(0,Math.Min(distance,hit.distance-.002f));
            if(distance>0)body.position=start+direction*distance;
        }
        var desired=Quaternion.RotateTowards(body.rotation,rotation,600*dt);
        var turn=desired*Quaternion.Inverse(body.rotation);
        foreach(var part in parts)
        {
            if(part==null||!part.enabled)continue;
            var p=body.position+turn*(part.transform.position-body.position);var q=turn*part.transform.rotation;
            float reach=part.bounds.extents.magnitude+(p-part.bounds.center).magnitude+.02f;
            int n=Physics.OverlapSphereNonAlloc(p,reach,overlaps,~0,QueryTriggerInteraction.Ignore);
            if(n>=overlaps.Length)return;
            for(int i=0;i<n;i++)
            {
                var other=overlaps[i];if(other==null||other.attachedRigidbody==body||other.isTrigger)continue;
                if(!Physics.ComputePenetration(part,p,q,other,other.transform.position,other.transform.rotation,out _,out float after))continue;
                Physics.ComputePenetration(part,part.transform.position,part.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float before);
                if(after>before+.002f)return;
            }
        }
        body.rotation=desired;
    }
}
