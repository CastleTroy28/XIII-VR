using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.98: punching bags swing when hit by a hand (or a held melee tip).
// A bag with a live Rigidbody gets a real impulse; a static/kinematic bag is
// swung procedurally about its top mount and returns exactly to rest.
internal sealed class PunchingBags:IDisposable
{
    private sealed class Bag
    {
        internal Transform Root=null!;internal Rigidbody? Body;internal Vector3 Pivot,RestPosition;internal Quaternion RestRotation;
        internal float Length,NextHit;internal N Angle,Velocity;internal bool Swinging;
    }
    private readonly Dictionary<int,(Collider c,Bag? bag)> cache=new();
    private readonly List<Bag> swinging=new();
    private readonly HashSet<string> reported=new();
    private readonly List<(Rigidbody body,float drag,float angular)> damped=new();
    internal bool TryHit(Collider c,Vector3 point,Vector3 velocity,Transform? player)
    {
        if(c==null||player!=null&&c.transform.IsChildOf(player))return false;
        var bag=Resolve(c,velocity);if(bag==null||bag.Root==null)return false;
        float now=Time.realtimeSinceStartup;if(now<bag.NextHit)return false;bag.NextHit=now+.2f;
        velocity=Vector3.ClampMagnitude(velocity,8);
        if(point==Vector3.zero)point=c.ClosestPoint(bag.Pivot+Vector3.down*bag.Length);
        if(bag.Body!=null&&!bag.Body.isKinematic)
        {
            // Effective fist mass grows with the bag (a heavy bag needs a
            // proportionally bigger push to visibly swing).
            float fist=Mathf.Clamp(bag.Body.mass*.2f,4,12);
            bag.Body.AddForceAtPosition(Vector3.ClampMagnitude(velocity*fist,Math.Min(60,bag.Body.mass*2.5f)),point,ForceMode.Impulse);bag.Body.WakeUp();
            return true;
        }
        var r=point-bag.Pivot;
        bag.Velocity=BagSwingMath.Hit(bag.Velocity,new N(r.x,r.y,r.z),new N(velocity.x,velocity.y,velocity.z),bag.Length);
        if(!bag.Swinging){bag.Swinging=true;swinging.Add(bag);}
        return true;
    }
    internal void Tick()
    {
        if(swinging.Count==0)return;
        float dt=Time.deltaTime;
        for(int i=swinging.Count-1;i>=0;i--)
        {
            var bag=swinging[i];
            if(bag.Root==null){swinging.RemoveAt(i);continue;}
            try
            {
                bool moving=BagSwingMath.Step(ref bag.Angle,ref bag.Velocity,bag.Length,dt);
                Apply(bag);
                if(!moving){bag.Swinging=false;swinging.RemoveAt(i);}
            }
            catch(Exception ex){swinging.RemoveAt(i);bag.Swinging=false;Bootstrap.Warn("BAG swing: "+ex.Message);}
        }
    }
    private static void Apply(Bag bag)
    {
        float angle=bag.Angle.Length();
        var q=angle<1e-6f?Quaternion.identity:Quaternion.AngleAxis(angle*Mathf.Rad2Deg,new Vector3(bag.Angle.X,bag.Angle.Y,bag.Angle.Z)/angle);
        bag.Root.SetPositionAndRotation(bag.Pivot+q*(bag.RestPosition-bag.Pivot),q*bag.RestRotation);
    }
    private Bag? Resolve(Collider c,Vector3 velocity)
    {
        int id=c.GetInstanceID();
        if(cache.TryGetValue(id,out var hit)&&hit.c==c)return hit.bag;
        if(cache.Count>512)cache.Clear();
        Bag? bag=null;
        try{bag=Build(c);}
        catch(Exception ex){Bootstrap.Warn("BAG resolve "+c.name+": "+ex.Message);}
        cache[id]=(c,bag);
        return bag;
    }
    private Bag? Build(Collider c)
    {
        Transform? root=null;string matched="";
        // Highest consecutive ancestor (within 5 levels) that is still one bag.
        var t=c.transform;
        for(int depth=0;t!=null&&depth<5;depth++,t=t.parent)
        {
            if(!BagSwingMath.BagName(t.name)){if(root!=null)break;continue;}
            var b=Bounds(t);if(b.size.x>1.2f||b.size.z>1.2f||b.size.y>4)break;
            root=t;matched=t.name;
        }
        var body=c.attachedRigidbody;
        if(root==null&&body!=null&&BagSwingMath.BagName(body.name)){root=body.transform;matched=body.name;}
        if(root==null)return null;
        if(root.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())!=null||root.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.Door>())!=null)return null;
        var bounds=Bounds(root);
        if(bounds.size.y<.3f)return null;
        if(BagSwingMath.NeedsHangingCheck(matched))
        {
            // Something solid right under the bag: it stands, it does not hang.
            var bottom=new Vector3(bounds.center.x,bounds.min.y+.02f,bounds.center.z);
            foreach(var hit in Physics.RaycastAll(bottom,Vector3.down,.12f,~0,QueryTriggerInteraction.Ignore))
                if(hit.collider!=null&&!hit.collider.transform.IsChildOf(root)){Bootstrap.Write("BAG ignored (standing) "+Path(root));return null;}
        }
        bool batched=false;
        foreach(var component in root.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {var render=component.TryCast<Renderer>();if(render!=null&&render.isPartOfStaticBatch)batched=true;}
        var live=body!=null&&!body.isKinematic?body:null;
        // A heavily damped bag barely moves however hard it is hit.
        if(live!=null&&(live.drag>.3f||live.angularDrag>.5f)&&!damped.Exists(d=>d.body==live))
        {
            damped.Add((live,live.drag,live.angularDrag));
            Bootstrap.Write("BAG damping reduced "+live.name+" drag="+live.drag.ToString("F2")+"/"+live.angularDrag.ToString("F2")+" -> "+Math.Min(live.drag,.1f).ToString("F2")+"/"+Math.Min(live.angularDrag,.3f).ToString("F2"));
            live.drag=Math.Min(live.drag,.1f);live.angularDrag=Math.Min(live.angularDrag,.3f);
        }
        if(batched&&live==null){Bootstrap.Warn("BAG cannot swing (static batched mesh) "+Path(root));return null;}
        var pivot=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
        float length=Math.Max(.3f,pivot.y-c.bounds.center.y);
        var animator=root.GetComponentInParent(Il2CppType.Of<Animator>())!=null||root.GetComponentInChildren(Il2CppType.Of<Animator>(),true)!=null;
        Bootstrap.Write("BAG ready "+Path(root)+" collider="+c.name+" mode="+(live!=null?"rigidbody mass="+live.mass.ToString("F1")+" drag="+live.drag.ToString("F2")+"/"+live.angularDrag.ToString("F2"):"swing")+" pivot="+pivot.ToString("F2")+" length="+length.ToString("F2")+" size="+bounds.size.ToString("F2")+" joint="+(body!=null&&body.GetComponent(Il2CppType.Of<Joint>())!=null)+" animator="+animator);
        return new Bag{Root=root,Body=live,Pivot=pivot,RestPosition=root.position,RestRotation=root.rotation,Length=length};
    }
    private static Bounds Bounds(Transform root)
    {
        bool any=false;var b=new Bounds(root.position,Vector3.zero);
        foreach(var component in root.GetComponentsInChildren(Il2CppType.Of<Renderer>(),false))
        {var r=component.TryCast<Renderer>();if(r==null||!r.enabled)continue;if(!any){b=r.bounds;any=true;}else b.Encapsulate(r.bounds);}
        if(!any)foreach(var component in root.GetComponentsInChildren(Il2CppType.Of<Collider>(),false))
        {var c=component.TryCast<Collider>();if(c==null||!c.enabled)continue;if(!any){b=c.bounds;any=true;}else b.Encapsulate(c.bounds);}
        return b;
    }
    // Unknown hit objects of prop size: log once so a bag with an unexpected
    // name can be found in the log.
    internal void Report(Collider c)
    {
        if(reported.Count>=40)return;
        var size=c.bounds.size;if(Math.Max(size.x,Math.Max(size.y,size.z))>3)return;
        if(c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())!=null)return;
        string path=Path(c.transform);if(!reported.Add(path))return;
        var body=c.attachedRigidbody;
        Bootstrap.Write("PUNCH world hit "+path+" size="+size.ToString("F2")+" rb="+(body==null?"none":body.name+(body.isKinematic?" kinematic":" dynamic")));
    }
    private static string Path(Transform t)
    {
        string s=t.name;int n=0;
        for(var p=t.parent;p!=null&&n<4;p=p.parent,n++)s=p.name+"/"+s;
        return s;
    }
    public void Dispose()
    {
        foreach(var bag in swinging)if(bag.Root!=null){bag.Angle=N.Zero;try{Apply(bag);}catch{}}
        swinging.Clear();cache.Clear();
        foreach(var d in damped)if(d.body!=null){d.body.drag=d.drag;d.body.angularDrag=d.angular;}
        damped.Clear();
    }
}
