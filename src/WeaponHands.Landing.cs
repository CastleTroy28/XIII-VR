using System;
using PlayMagic.Weapons;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.214: where the thing ready to be thrown lands, marked where it would
// hit (the ground, a wall, an enemy): a knife held by the grip, a grenade
// with its pin out, a bottle or an ashtray, in either hand. A swing under
// way is shown as letting go now would throw it; otherwise a plain throw
// along where the controller points. Once thrown, the mark stays where the
// throw lands until it gets there. "ThrowLandingMarker" in the settings file
// switches it off.
internal sealed partial class WeaponHands
{
    private readonly ThrowMarker[] landingMarks={new("XIII throw landing left"),new("XIII throw landing right")};
    private readonly bool[] landingAsked=new bool[2];
    private readonly Vector3[] landingFrom=new Vector3[2],landingVelocity=new Vector3[2],landingGravity=new Vector3[2];
    private readonly float[] landingFrozenUntil=new float[2],landingNext=new float[2];
    private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> landingHits=new(16);
    private ThrowLandingMath.Cast? landingCast;
    // The game's knife flight, measured after its launches (straight until then).
    private float knifeFlightSpeed=30,knifeFlightDrop;private bool knifeFlightKnown;
    private float KnifeFlightSpeed=>knifeFlightSpeed;
    private Vector3 KnifeGravity=>knifeFlightDrop>=ThrowLandingMath.StraightBelow?new Vector3(0,-knifeFlightDrop,0):Vector3.zero;
    private static bool LandingOn=>QualityOptions.ThrowLanding?.Value!=false;
    // Hand s holds a thing ready to be thrown: from there, with this velocity.
    private void AimLanding(int s,Vector3 origin,Vector3 velocity,Vector3 gravity)
    {
        if(s<0||s>1)return;
        landingAsked[s]=true;landingFrom[s]=origin;landingVelocity[s]=velocity;landingGravity[s]=gravity;
    }
    // A knife: along a swing under way, else where the controller points.
    private void AimKnifeLanding(int s,Vector3 hand,bool aimed)
    {
        if(!LandingOn||!AimThrow(s,out var direction))return;
        if(!aimed&&SwingThrow(s,out var swing,out _,out _))direction=swing;
        direction.Normalize();
        AimLanding(s,hand+direction*.12f,direction*KnifeFlightSpeed,KnifeGravity);
    }
    // A bottle or an ashtray (the game's throwing speed, scaled by the swing as its throw is).
    private void AimPropLanding(int s,Vector3 origin,Vector3 aim,float native,bool aimed)
    {
        if(!LandingOn)return;
        var direction=aim;float scale=1;
        if(!aimed&&SwingThrow(s,out var swing,out float speed,out _)){direction=swing;scale=Math.Clamp(speed/3f,.8f,1.35f);}
        direction.Normalize();
        AimLanding(s,origin+direction*.09f,direction*native*scale,Physics.gravity);
    }
    // A grenade with its pin out: the hand's swing under way (its throw:
    // turned toward the look, as briskly as the game throws), else a throw
    // along the controller at the game's speed.
    private void AimGrenadeLanding(int s,GrenadeThrow gesture,Vector3 center,Quaternion toWorld,Equipable? grenade)
    {
        if(!LandingOn)return;
        float native=-1;try{if(grenade!=null)native=grenade.CurrentEquipableParameters.primaryProjectileSpeed;}catch(Exception){}
        native=ThrowTrajectory.Speed(native);
        var look=rig.HeadRotation*Vector3.forward;
        if(gesture.Peek(Time.realtimeSinceStartup,out var v,out float hand)&&hand>=SwingRelease.MinSpeed)
        {
            var world=ToN(toWorld*new Vector3(v.X,v.Y,v.Z));
            if(ThrowLandingMath.Forward(world,ToN(look)))
            {
                var thrown=GrenadeThrow.Lively(GrenadeThrow.TowardLook(world,ToN(look)),hand,native);
                AimLanding(s,center,ContactWorld.U(thrown),Physics.gravity);return;
            }
        }
        if(!AimThrow(s,out var aim))aim=look;
        AimLanding(s,center,ContactWorld.U(GrenadeThrow.TowardLook(ToN(aim.normalized*native),ToN(look))),Physics.gravity);
    }
    // Every frame, after the hands: the marks shown (or hidden).
    private void TickLanding(bool allowed)
    {
        float now=Time.realtimeSinceStartup;bool on=allowed&&LandingOn;
        for(int s=0;s<2;s++)
        {
            bool asked=landingAsked[s];landingAsked[s]=false;
            if(!on){landingFrozenUntil[s]=0;landingMarks[s].Hide();continue;}
            if(now<landingFrozenUntil[s])continue;
            if(!asked){landingMarks[s].Hide();continue;}
            if(now<landingNext[s]&&landingMarks[s].Shown)continue;
            landingNext[s]=now+.025f;
            ShowLanding(s,landingFrom[s],landingVelocity[s],landingGravity[s],out _);
        }
    }
    private bool ShowLanding(int s,Vector3 origin,Vector3 velocity,Vector3 gravity,out float time)
    {
        time=0;
        try
        {
            landingCast??=LandingCast;
            if(ThrowLandingMath.Land(ToN(origin),ToN(velocity),ToN(gravity),landingCast,out var point,out var normal,out time))
            {
                var p=ContactWorld.U(point);
                landingMarks[s].Show(p,ContactWorld.U(normal),ThrowLandingMath.Radius(Vector3.Distance(rig.HeadPosition,p)));
                return true;
            }
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextLandingError){nextLandingError=Time.realtimeSinceStartup+10;Bootstrap.Warn("THROW LANDING: "+ex.Message);}}
        landingMarks[s].Hide();return false;
    }
    private float nextLandingError;
    // Thrown from hand s: the mark stays where it lands until it gets there.
    private void FreezeLanding(int s,Vector3 origin,Vector3 velocity,Vector3 gravity)
    {
        if(s<0||s>1||!LandingOn)return;
        landingFrozenUntil[s]=0;
        if(ShowLanding(s,origin,velocity,gravity,out float time))landingFrozenUntil[s]=Time.realtimeSinceStartup+Math.Min(time,2f)+.3f;
    }
    private void FreezeKnifeLanding()=>FreezeLanding(knifeThrowSide,knifeOrigin,knifeDirection*KnifeFlightSpeed,KnifeGravity);
    // The first solid thing between two points (not the player, not the
    // mod's own or the invisible walls the hands pass through).
    private bool LandingCast(N from,N to,out N point,out N normal)
    {
        point=normal=N.Zero;
        var a=ContactWorld.U(from);var d=ContactWorld.U(to)-a;float distance=d.magnitude;
        if(!(distance>1e-4f))return false;
        int count=Physics.SphereCastNonAlloc(a,.03f,d/distance,landingHits,distance,~0,QueryTriggerInteraction.Ignore);
        float best=float.PositiveInfinity;bool found=false;var player=rig.PlayerRoot;
        for(int k=0;k<count&&k<landingHits.Length;k++)
        {
            var hit=landingHits[k];var c=hit.collider;
            if(c==null||!(hit.distance>0)||hit.distance>=best)continue;
            if(player!=null&&c.transform.IsChildOf(player))continue;
            if(throwFilter.Excluded(c))continue;
            best=hit.distance;point=ContactWorld.V(hit.point);normal=ContactWorld.V(hit.normal);found=true;
        }
        return found;
    }
    // The game's knife as it flies: three positions after the launch give its
    // speed and its fall (ThrowLandingMath.Fit), for the next marks.
    private Projectile? flightKnife;private float flightAt;private int flightSamples;
    private readonly float[] flightT=new float[3];private readonly Vector3[] flightP=new Vector3[3];
    private void WatchKnifeFlight(Projectile projectile){flightKnife=projectile;flightAt=Time.time;flightSamples=0;}
    private void TickKnifeFlight()
    {
        var k=flightKnife;if(k==null)return;
        try
        {
            float t=Time.time-flightAt;
            if(t>.6f||!k.gameObject.activeInHierarchy){flightKnife=null;return;}
            float due=flightSamples==0?.02f:flightSamples==1?.08f:.14f;
            if(t<due)return;
            flightT[flightSamples]=t;flightP[flightSamples]=k.transform.position;flightSamples++;
            if(flightSamples<3)return;
            flightKnife=null;
            if(!ThrowLandingMath.Fit(flightT[0],ToN(flightP[0]),flightT[1],ToN(flightP[1]),flightT[2],ToN(flightP[2]),out float speed,out float drop))return;
            bool first=!knifeFlightKnown;bool wasStraight=knifeFlightDrop<ThrowLandingMath.StraightBelow;
            knifeFlightSpeed=first?speed:(knifeFlightSpeed+speed)*.5f;knifeFlightDrop=first?drop:(knifeFlightDrop+drop)*.5f;knifeFlightKnown=true;
            if(first||wasStraight!=knifeFlightDrop<ThrowLandingMath.StraightBelow)
                Bootstrap.Write("KNIFE flight measured: "+knifeFlightSpeed.ToString("F1")+" m/s, "+(knifeFlightDrop<ThrowLandingMath.StraightBelow?"straight":"falling "+knifeFlightDrop.ToString("F1")+" m/s2")+" (the landing mark follows it)");
        }
        catch(Exception){flightKnife=null;}
    }
    private void HideLanding(){for(int s=0;s<2;s++){landingAsked[s]=false;landingFrozenUntil[s]=0;try{landingMarks[s].Hide();}catch(Exception){}}}
    private void DisposeLanding(){foreach(var m in landingMarks)m.Dispose();}
}
