using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class NpcHitReactions
{
    private readonly Il2CppStructArray<RaycastHit> brawlHits=new(32);
    private readonly ContactFilter brawlFilter=new();
    private static void CancelPunch(Brawler br,float now)
    {br.ContactSpent=true;br.HasPreviousFist=false;br.SwingAt=-1;br.SwingStart=-10;br.Plan.Interrupt(now);}
    internal void SuspendBrawls()
    {
        brawlControlFrame=-1;
        foreach(var br in brawlers.Values){CancelPunch(br,Time.time);br.ActualSpeed=0;Legs(br,false,0);}
    }
    private bool AttackSlot(Brawler br)
    {
        foreach(var other in brawlers.Values)
        {
            if(other==br||other.Npc==null)continue;
            if((other.Plan.Act==BrawlAct.Swing||other.Plan.Act==BrawlAct.StepIn)&&(other.Npc.transform.position-br.Npc.transform.position).sqrMagnitude<9)return false;
        }
        return true;
    }
    private bool ClearBrawlPath(Brawler br,Vector3 from,Vector3 to,float radius)
    {
        var delta=to-from;float length=delta.magnitude;if(!float.IsFinite(length))return false;if(length<.001f)return true;
        int n=Physics.SphereCastNonAlloc(from,radius,delta/length,brawlHits,length,~0,QueryTriggerInteraction.Ignore);
        if(n>=brawlHits.Length)return false;
        var player=brawlRig?.PlayerRoot;
        for(int i=0;i<n;i++)
        {
            var c=brawlHits[i].collider;if(c==null||!c.enabled||c.isTrigger)continue;
            if(c.transform.IsChildOf(br.Npc.transform)||(player!=null&&c.transform.IsChildOf(player))||brawlFilter.Excluded(c))continue;
            return false;
        }
        return true;
    }
    // Called once per frame AFTER arm IK, wrist orientation, finger curl and hit reactions.
    // Damage follows the visible knuckles, not a timer or a distance around the NPC root.
    private void PunchContact(Body body,Brawler br,float now)
    {
        if(brawlControlFrame!=Time.frameCount||brawlRig==null||br.Plan.Act!=BrawlAct.Swing||Holding(br.Npc.Pointer))
        {br.HasPreviousFist=false;return;}
        var wrist=br.Left?body.Rig.leftWrist:body.Rig.rightWrist;
        var hand=br.Left?body.FistL:body.FistR;
        if(wrist==null||hand==null){br.ContactSpent=true;return;}
        var at=hand.Knuckle;var before=br.HasPreviousFist?br.PreviousFist:at;
        br.PreviousFist=at;br.HasPreviousFist=true;
        float age=(now-br.SwingStart)*br.Style.PunchSpeed;
        if(br.ContactSpent)return;
        if(age>BrawlMath.HitDelay+BrawlMath.Hold){br.ContactSpent=true;br.Missed++;return;}
        if(!BrawlContactMath.Active(age)||!BrawlContactMath.Hits(ContactWorld.V(before),ContactWorld.V(at),ContactWorld.V(lastHead)))return;
        var shoulder=br.Left?body.Rig.leftShoulder:body.Rig.rightShoulder;
        if(shoulder==null||!ClearBrawlPath(br,shoulder.position,at,.055f)||!ClearBrawlPath(br,before,at,.055f))
        {br.ContactSpent=true;br.Missed++;return;}
        var to=lastHead-br.Npc.transform.position;to.y=0;float distance=to.magnitude;
        float dot=distance>.001f?Vector3.Dot(br.Npc.transform.forward,to/distance):1;
        br.ContactPoint=at;Land(br,brawlRig,lastHead,distance,dot);
    }
}
