using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlayMagic;
using UnityEngine;
namespace XiiiXR;
// Conservative short teleport: existing walkable floor, free destination and
// a clear swept character capsule. No collider disabling or transform warps.
internal sealed class TeleportDriver : IDisposable
{
    private readonly Il2CppStructArray<RaycastHit> hits=new(64);
    private readonly Il2CppReferenceArray<Collider> overlaps=new(64);
    private readonly TeleportArc arc=new();
    private readonly Vector3[] points=new Vector3[25];
    private bool valid;
    private Vector3 target;
    private int segments;
    private float nextError;
    internal void Cancel(){valid=false;arc.Hide();}
    internal void Tick(CameraRig rig,CustomCharacterController? character,bool aiming,bool commit,bool allowed)
    {
        if(!allowed||character==null||character.controller==null||!character.controller.enabled
            ||character.restrictMovement||character.IsSpawning||character.isMounted||character.isDoingZipline
            ||character.CurrentPlayerState<CustomCharacterController.PlayerStates.Idling
            ||character.CurrentPlayerState>CustomCharacterController.PlayerStates.Crouching)
        {Cancel();return;}
        try
        {
            if(!aiming&&!commit){Cancel();return;}
            bool previousValid=valid;
            // Recheck the destination on release; moving props/NPCs can invalidate it.
            if(!FindTarget(rig,character)){Cancel();return;}
            if(commit)
            {
                if(previousValid&&valid)
                {
                    var before=character.transform.position;
                    character.controller.Move(target-before);
                    var actual=character.transform.position-before;
                    rig.ResetRenderCaches();ContactRig.Current?.ResetGun();ContactRig.Current?.ResetHand(false);ContactRig.Current?.ResetHand(true);
                    WeaponHands.Current?.OnRelocated();
                    Bootstrap.Write("TELEPORT accepted displacement="+actual);
                }
                Cancel();return;
            }
            arc.Show(points,segments,valid);
        }
        catch(Exception ex){Cancel();if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("TELEPORT blocked: "+ex.Message);}}
    }
    private bool Solid(Collider? c,Transform player)=>c!=null&&c.enabled&&!c.isTrigger&&!c.transform.IsChildOf(player);
    private bool FindTarget(CameraRig rig,CustomCharacterController character)
    {
        valid=false;segments=0;
        if(!rig.SampleWorldHands(out var left,out _,out bool leftValid)||!leftValid)return false;
        var player=character.transform;
        var start=CameraRig.UnityPosition(left);
        var direction=ControllerAim.Rotation(left)*Vector3.forward;
        if(!float.IsFinite(start.sqrMagnitude)||Vector3.Distance(start,rig.HeadPosition)>1.4f)return false;
        var velocity=direction*5;points[0]=start;
        for(int i=0;i<points.Length-1;i++)
        {
            float t=(i+1)*.05f;
            var end=start+velocity*t+Vector3.down*(4.9f*t*t);
            var delta=end-points[i];float distance=delta.magnitude;
            int n=Physics.SphereCastNonAlloc(points[i],.015f,delta/distance,hits,distance,~0,QueryTriggerInteraction.Ignore);
            if(n>=hits.Length)return false;
            int nearest=-1;float best=float.PositiveInfinity;
            for(int k=0;k<n;k++)if(Solid(hits[k].collider,player)&&hits[k].distance<best){nearest=k;best=hits[k].distance;}
            points[i+1]=end;segments=i+1;
            if(nearest<0)continue;
            var hit=hits[nearest];points[i+1]=hit.point;
            var cc=character.controller;var feet=character.FeetPosition;
            float radius=cc.radius*Math.Max(Math.Abs(player.lossyScale.x),Math.Abs(player.lossyScale.z));
            float height=cc.height*Math.Abs(player.lossyScale.y);
            if(radius<.05f||radius>.9f||height<radius*2||height>3)return true;
            if(!TeleportGeometry.Landing(hit.normal.y,cc.slopeLimit,Vector3.Distance(hit.point,feet),hit.point.y-feet.y))return true;
            var displacement=hit.point-feet+Vector3.up*.025f;
            target=player.position+displacement;
            float skin=Math.Max(.025f,cc.skinWidth);
            var center=player.TransformPoint(cc.center);
            var lower=center-Vector3.up*(height*.5f-radius)+Vector3.up*skin;
            var upper=center+Vector3.up*(height*.5f-radius);
            float queryRadius=Math.Max(.04f,radius-.015f);
            n=Physics.OverlapCapsuleNonAlloc(lower+displacement,upper+displacement,queryRadius,overlaps,~0,QueryTriggerInteraction.Ignore);
            if(n>=overlaps.Length)return true;
            for(int k=0;k<n;k++)if(Solid(overlaps[k],player))return true;
            float travel=displacement.magnitude;if(travel<.15f)return true;
            n=Physics.CapsuleCastNonAlloc(lower,upper,queryRadius,displacement/travel,hits,travel,~0,QueryTriggerInteraction.Ignore);
            if(n>=hits.Length)return true;
            for(int k=0;k<n;k++)if(Solid(hits[k].collider,player))return true;
            valid=true;return true;
        }
        return true;
    }
    public void Dispose(){Cancel();arc.Dispose();}
}
