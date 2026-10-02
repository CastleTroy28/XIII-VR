using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.156: held in
// two hands the thing is stopped at the enemy's body (the weapon's
// collisions), and a sweep that starts touching a body never reports it. An
// enemy already touching the stopped thing on a real stroke is struck where
// the stroke goes.
internal sealed partial class PunchDriver
{
    private readonly Il2CppReferenceArray<Collider> touching=new(32);
    partial void TouchingNpc(ContactSphere[] shape,Vector3 oldPosition,Quaternion oldRotation,Vector3 safePosition,Quaternion safeRotation,Vector3 desiredPosition,Quaternion desiredRotation,Transform root,MeleeComponent melee,ref Touch touch)
    {
        float best=float.PositiveInfinity;
        for(int i=0;i<shape.Length;i++)
        {
            var offset=ContactWorld.U(shape[i].Offset);float radius=shape[i].Radius;
            var at=safePosition+safeRotation*offset;var stroke=desiredPosition+desiredRotation*offset-(oldPosition+oldRotation*offset);
            if(stroke.sqrMagnitude<1e-6f)continue;var dir=stroke.normalized;
            int n=Physics.OverlapSphereNonAlloc(at,radius+.04f,touching,~0,QueryTriggerInteraction.Collide);
            for(int k=0;k<Math.Min(n,touching.Length);k++)
            {
                var c=touching[k];
                if(c==null||!c.enabled||c.transform.IsChildOf(root)||filter.Excluded(c))continue;
                if(c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())==null)continue;
                if(c.isTrigger&&!melee.ValidateIfDamageable(c))continue;
                var origin=at-dir*.4f;
                if(!c.Raycast(new Ray(origin,dir),out var h,.9f))
                {
                    if(!ColliderSurface.TryClosest(c,at,out var closest))continue;
                    var toward=closest-origin;if(toward.sqrMagnitude<1e-6f)continue;
                    if(!c.Raycast(new Ray(origin,toward.normalized),out h,.9f))continue;
                    dir=toward.normalized;
                }
                if(h.distance>=best)continue;
                best=h.distance;touch.Found=true;touch.Hit=h;touch.From=origin;touch.Delta=dir*Math.Max(h.distance,.05f);touch.Probe=i;
            }
        }
    }
    // 0.1.200: the club drawn against an enemy is stopped
    // by its body (the weapon's collisions), so its stroke starts at that body
    // and often finds nothing to sweep into. A real stroke while the weapon is
    // stopped by an enemy's body (in the last frames) strikes that body where
    // the nearest part of the weapon touches it.
    private float nextStoppedReport;
    partial void StoppedByNpc(ContactSphere[] shape,Vector3 oldPosition,Quaternion oldRotation,Vector3 safePosition,Quaternion safeRotation,Vector3 desiredPosition,Quaternion desiredRotation,Transform root,MeleeComponent melee,ref Touch touch)
    {
        var rig=ContactRig.Current;if(rig==null||!rig.TryGunObstacle(3,out var c)||c==null)return;
        if(!c.enabled||c.transform.IsChildOf(root)||filter.Excluded(c))return;
        if(c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())==null)return;
        if(c.isTrigger&&!melee.ValidateIfDamageable(c))return;
        int best=-1;float bestDistance=float.PositiveInfinity;Vector3 bestAt=Vector3.zero,bestClosest=Vector3.zero;
        for(int i=0;i<shape.Length;i++)
        {
            var at=safePosition+safeRotation*ContactWorld.U(shape[i].Offset);
            if(!ColliderSurface.TryClosest(c,at,out var closest))continue;
            float d=Vector3.Distance(at,closest)-shape[i].Radius;
            if(d<bestDistance){bestDistance=d;best=i;bestAt=at;bestClosest=closest;}
        }
        if(best<0||bestDistance>StoppedReach)return;
        var offset=ContactWorld.U(shape[best].Offset);
        var stroke=desiredPosition+desiredRotation*offset-(oldPosition+oldRotation*offset);
        var dir=stroke.sqrMagnitude>1e-6f?stroke.normalized:(bestClosest-bestAt);
        if(dir.sqrMagnitude<1e-8f)return;dir=dir.normalized;
        var origin=bestAt-dir*.4f;
        if(!c.Raycast(new Ray(origin,dir),out var h,.9f))
        {
            var toward=bestClosest-origin;if(toward.sqrMagnitude<1e-6f)return;
            if(!c.Raycast(new Ray(origin,toward.normalized),out h,.9f))return;
            dir=toward.normalized;
        }
        touch.Found=true;touch.Hit=h;touch.From=origin;touch.Delta=dir*Math.Max(h.distance,.05f);touch.Probe=best;
        if(Time.realtimeSinceStartup>=nextStoppedReport){nextStoppedReport=Time.realtimeSinceStartup+3;Bootstrap.Write("VR PUNCH the weapon was stopped by "+c.name+" (an enemy's body, "+(Math.Max(0,bestDistance)*1000).ToString("F0")+" mm off, held off "+(rig.GunGap*100).ToString("F0")+" cm): the stroke strikes it there");}
    }
    // 0.1.205: a held weapon's stroke that found no enemy with one close by: what it met instead.
    private float nextNearReport;
    partial void ReportNearMiss(bool right,float speed,Collider? world,int part,Vector3 safe,float gap,Transform root,float now)
    {
        if(now<nextNearReport)return;
        string near=NearEnemy(safe,1.2f,root);if(near.Length==0)return;
        nextNearReport=now+1.5f;
        Bootstrap.Write("VR PUNCH held weapon side="+(right?"R":"L")+" speed="+speed.ToString("F2")+(world!=null?" met "+world.name+" (part "+part+", not an enemy)":" met nothing")+"; "+near+"; weaponGap="+(gap*100).ToString("F0")+"cm"
            +(ContactRig.Current?.TryGunObstacle(3,out var stop)==true&&stop!=null?"; held off by "+stop.name:""));
    }
    // 0.1.205: the nearest conscious enemy within `within` of the weapon, for the log ("" for none).
    private readonly Il2CppReferenceArray<Collider> around=new(64);
    private string NearEnemy(Vector3 at,float within,Transform root)
    {
        int n=Physics.OverlapSphereNonAlloc(at,within,around,~0,QueryTriggerInteraction.Collide);
        string best="";float bestDistance=float.PositiveInfinity;
        for(int k=0;k<Math.Min(n,around.Length);k++)
        {
            var c=around[k];if(c==null||!c.enabled||c.transform.IsChildOf(root))continue;
            var npc=c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())?.TryCast<PlayMagic.AI.NPC>();
            if(npc==null||!npc.IsAlive||!npc.IsConscious)continue;
            var point=ColliderSurface.TryClosest(c,at,out var closest)?closest:c.bounds.center;
            float d=(point-at).magnitude;
            if(d<bestDistance){bestDistance=d;best="enemy "+npc.name+" "+(d*100).ToString("F0")+" cm off at its "+c.name+(c.isTrigger?" (trigger)":"");}
        }
        return best;
    }
    // The weapon's part within this of the body that stopped it.
    internal const float StoppedReach=.06f;
    // 0.1.200: the enemy's nearest collider the game takes blows through,
    // within HurtboxReach of where the stroke met its body (cached per enemy).
    internal const float HurtboxReach=.30f;
    private readonly System.Collections.Generic.Dictionary<IntPtr,Collider[]> npcColliders=new();private float nextRetargetReport;
    partial void RetargetHurtbox(PlayMagic.AI.NPC npc,MeleeComponent melee,Vector3 stroke,ref Collider collider,ref RaycastHit hit)
    {
        try
        {
            if(!npcColliders.TryGetValue(npc.Pointer,out var all)||all.Length==0||all[0]==null)
            {
                if(npcColliders.Count>64)npcColliders.Clear();
                var found=npc.GetComponentsInChildren(Il2CppType.Of<Collider>(),true);var list=new System.Collections.Generic.List<Collider>();
                foreach(var o in found){var c=o.TryCast<Collider>();if(c!=null)list.Add(c);}
                npcColliders[npc.Pointer]=all=list.ToArray();
            }
            var point=hit.point;if(!float.IsFinite(point.sqrMagnitude)||hit.distance<=0)ColliderSurface.TryClosest(collider,point,out point);
            Collider? best=null;float bestDistance=HurtboxReach;Vector3 bestAt=point;
            foreach(var c in all)
            {
                if(c==null||!c.enabled||!c.gameObject.activeInHierarchy||c.Pointer==collider.Pointer||filter.Excluded(c)||!melee.ValidateIfDamageable(c))continue;
                if(!ColliderSurface.TryClosest(c,point,out var at))continue;
                float d=Vector3.Distance(at,point);if(d<bestDistance){bestDistance=d;best=c;bestAt=at;}
            }
            if(best==null)
            {
                if(Time.realtimeSinceStartup>=nextRetargetReport){nextRetargetReport=Time.realtimeSinceStartup+3;Bootstrap.Write("VR PUNCH struck "+npc.name+"'s "+collider.name+": not one the game takes blows through, and none within "+(HurtboxReach*100).ToString("F0")+" cm");}
                return;
            }
            var dir=stroke.sqrMagnitude>1e-8f?stroke.normalized:(bestAt-point).normalized;
            var origin=bestAt-dir*.3f;
            if(best.Raycast(new Ray(origin,dir),out var h,.8f)){hit=h;}
            else{var toward=bestAt-(point-dir*.3f);if(toward.sqrMagnitude>1e-8f&&best.Raycast(new Ray(point-dir*.3f,toward.normalized),out h,.8f))hit=h;else hit.point=bestAt;}
            if(Time.realtimeSinceStartup>=nextRetargetReport){nextRetargetReport=Time.realtimeSinceStartup+3;Bootstrap.Write("VR PUNCH struck "+npc.name+"'s "+collider.name+" (not one the game takes blows through): its "+best.name+" "+(bestDistance*100).ToString("F0")+" cm away takes the blow");}
            collider=best;
        }
        catch(Exception ex){Bootstrap.Warn("VR PUNCH retarget: "+ex.Message);}
    }
}
