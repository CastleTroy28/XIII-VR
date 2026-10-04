using System;
using UnityEngine;
using PlayMagic.Weapons;
namespace XiiiXR;
internal sealed partial class PunchDriver
{
    // 0.1.216: the game's knockout of an enemy (his fall, his "down" for the
    // mission and the tutorials) when a fist lands hard in his back; else why
    // not, in the log, for a punch from behind that was too light.
    private float nextKnockoutReport;
    partial void BackKnockout(PlayMagic.AI.NPC npc,Collider collider,RaycastHit hit,Vector3 from,Vector3 delta,float speed,bool isRight,CameraRig rig,MeleeComponent melee,Equipable selected,PlayerEquipableInventory inventory,ref bool knocked)
    {
        bool conscious,held,allowed;
        try{conscious=npc.IsAlive&&npc.IsConscious&&!npc.isRagdoll;held=npc.isHeldByPlayer;allowed=npc.CanBeStealthAttacked();}catch(Exception){return;}
        var t=npc.transform;var head=rig.HeadPosition;
        bool behind=KnockoutMath.FromBehind(ContactWorld.V(t.forward),ContactWorld.V(t.position),ContactWorld.V(head));
        bool hard=KnockoutMath.Hard(speed);
        // The game's own rule for a takedown from behind (never a boss).
        if(!KnockoutMath.Knocks(true,behind,hard,conscious,held)||!allowed)
        {
            float now=Time.realtimeSinceStartup;
            if(behind&&conscious&&!held&&now>=nextKnockoutReport&&(!hard||!allowed))
            {nextKnockoutReport=now+3;Bootstrap.Write("VR PUNCH in "+npc.name+"'s back at "+speed.ToString("F1")+" m/s: "+(!allowed?"the game allows no takedown of him":"a knockout needs a swing of "+KnockoutMath.HardSpeed.ToString("F1")+" m/s")+" (an ordinary punch)");}
            return;
        }
        var owner=melee.baseEquipable!=null?melee.baseEquipable.GetOwner():selected.GetOwner();
        if(owner.IsInvalid||!owner.IsPlayer)return;
        try{npc.Knockout(owner,true);}
        catch(Exception ex){Bootstrap.Warn("VR PUNCH knockout from behind: "+ex.Message);return;}
        inventory.playerUsedMeleeForce=true;
        try{melee.OnHit?.Invoke(selected,true);melee.OnSuccessful?.Invoke(hit,from);}catch(Exception){}
        bool down=false;try{down=!npc.IsConscious||!npc.IsAlive;}catch(Exception){}
        Bootstrap.Write("VR PUNCH side="+(isRight?"R":"L")+" target="+npc.name+" speed="+speed.ToString("F2")+" in his back: knocked out"+(down?"":" (the game kept him standing)"));
        NpcHitReactions.Current?.Hit(npc,collider,hit.point,delta,speed,false,from);
        knocked=true;
    }
}
