using System;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.194. Two causes: the game's own hip-fire spread (the VR crossbow is
// always aimed down its own sight) and the bolt's fall under gravity. The
// spread is off for the player's crossbow (SpreadPolicy); the bolt is launched
// so it reaches the aimed point (BallisticMath) - the crossbow in the hand
// and the one fired from the other hand.
internal sealed partial class WeaponHands
{
    private string copyShotProfile="";
    private float nextBoltReport;
    private bool boltPlainReported;
    private static void AimedBolt(FireComponent __instance,Projectile instancedProjectile,Vector3 hitPoint)
    {
        var c=Current;if(c==null||instancedProjectile==null)return;
        try{c.StraightBolt(__instance,instancedProjectile,hitPoint);}
        catch(Exception ex){Bootstrap.Warn("CROSSBOW straight bolt unavailable: "+ex.Message);}
    }
    private string? BoltKind(FireComponent f)
    {
        if(CopyShotBy(f))return copyShotProfile;
        if(Owns(f)&&poseValid&&CanControl(playerId))return profile;
        return null;
    }
    private void StraightBolt(FireComponent f,Projectile p,Vector3 target)
    {
        if(BoltKind(f)!="crossbow")return;
        var rb=p.projectileRigidBody;if(rb==null)return;
        if(rb.isKinematic||!rb.useGravity)
        {
            if(!boltPlainReported){boltPlainReported=true;Bootstrap.Write("CROSSBOW bolt flies without gravity (kinematic="+rb.isKinematic+" gravity="+rb.useGravity+"): only the spread is removed");}
            return;
        }
        var from=p.transform.position;var d=target-from;var v=rb.velocity;var g=Physics.gravity;
        var r=BallisticMath.Launch(d.x,d.y,d.z,v.magnitude,g.x,g.y,g.z,rb.drag);
        if(r==null)return;
        var aimed=new Vector3(r.Value.x,r.Value.y,r.Value.z);
        rb.velocity=aimed;
        float now=Time.realtimeSinceStartup;
        if(now>=nextBoltReport)
        {
            nextBoltReport=now+2;
            Bootstrap.Write("CROSSBOW bolt aimed at the sighted point: distance="+d.magnitude.ToString("F1")+"m speed="+v.magnitude.ToString("F1")+" -> "+aimed.magnitude.ToString("F1")+"m/s lift="+Vector3.Angle(v,aimed).ToString("F2")+" deg drag="+rb.drag.ToString("F3")+" spread=0");
        }
    }
}
