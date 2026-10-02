using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppInterop.Runtime;
namespace XiiiXR;
// Promote individually modelled props only; never a level mesh or mission item.
internal sealed class WorldPropBodies:IDisposable
{
    private readonly List<(Rigidbody b,bool kinematic,bool gravity,RigidbodyConstraints constraints,float drag,float angular,float mass)> changed=new();
    internal void Discover(Vector3 hand) { } // No global scans or conversion of decorative meshes.
    internal Rigidbody? Resolve(Collider c)=>Resolve(c,out _);
    internal Rigidbody? Resolve(Collider c,out string why)
    {
        why="";
        var body=c.attachedRigidbody;
        bool named=PhysicalHandsMath.SmallProp(c.name)||(body!=null&&PhysicalHandsMath.SmallProp(body.name));
        var size=c.bounds.size;float longest=Math.Max(size.x,Math.Max(size.y,size.z));
        if(longest>(named?3:1.1f)){why="too big";return null;}
        if(body==null){why="no physics body (fixed in place)";return null;}
        foreach(var component in body.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {var render=component.TryCast<Renderer>();if(render!=null&&render.isPartOfStaticBatch){why="part of the level";return null;}}
        if(body.GetComponent(Il2CppType.Of<Joint>())!=null){why="fastened";return null;}
        if(!named&&body.mass>35){why="too heavy";return null;}
        foreach(var part in body.GetComponentsInChildren(Il2CppType.Of<Collider>(),true))
        {
            var collider=part.TryCast<Collider>();
            if(collider==null||collider.attachedRigidbody!=body)continue;
            if((collider.bounds.center-body.position).magnitude+collider.bounds.extents.magnitude>(named?2.5f:1.2f)){why="too big";return null;}
            var mesh=collider.TryCast<MeshCollider>();
            if(mesh!=null&&!mesh.convex){why="level mesh";return null;}
        }
        if((body.isKinematic||body.constraints!=RigidbodyConstraints.None)&&!named){why="held still by the game (not a known small thing)";return null;}
        if(named&&!changed.Exists(x=>x.b==body))
        {
            if(changed.Count>=64){why="too many";return null;}
            changed.Add((body,body.isKinematic,body.useGravity,body.constraints,body.drag,body.angularDrag,body.mass));
            body.isKinematic=false;body.constraints=RigidbodyConstraints.None;body.useGravity=true;body.drag=.05f;body.angularDrag=.1f;body.mass=Math.Clamp(body.mass,.15f,8);
            Bootstrap.Write("WORLD PROP activated="+body.name+" mass="+body.mass);
        }
        return body;
    }
    public void Dispose()
    {
        foreach(var x in changed)if(x.b!=null){x.b.isKinematic=x.kinematic;x.b.useGravity=x.gravity;x.b.constraints=x.constraints;x.b.drag=x.drag;x.b.angularDrag=x.angular;x.b.mass=x.mass;}
        changed.Clear();
    }
}
