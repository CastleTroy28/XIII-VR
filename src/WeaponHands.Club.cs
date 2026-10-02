using System;
using UnityEngine;
namespace XiiiXR;
// 0.1.196.
// The free hand's grip at the barrel's front (the other hand still on the
// handle) holds the barrel. The handle hand letting go leaves the gun in that
// hand as a club: within a fifth of a second it turns so that its barrel lies
// in the fist as a pistol's grip would, the stock above the thumb, the muzzle
// just under the little finger. The other hand's grip at the barrel takes the
// club over (so either hand can hold it); its grip at the handle holds the gun
// to shoot again; the club hand letting go puts it away (a body place) or
// drops it, as always. A club does not fire or reload; its whole shape hits,
// timed by the stock's end, and hits like a broom or a shovel (5..3 blows).
internal sealed partial class WeaponHands
{
    // The game's weapon hanging in the hand 1-GameSide is a club (with foreEndOnly).
    private bool club;private float clubAlong=ClubMath.MinFromMuzzle,clubSince=-10;private bool clubBlend;
    private Vector3 clubFromOffset;private Quaternion clubFromTurn=Quaternion.identity;
    // The hand on the barrel's front while the other one still holds the handle.
    private int barrelHand=-1,barrelWeapon;private float barrelAlong=ClubMath.MinFromMuzzle;
    // The support grip point (world) last drawn: the barrel's front is taken only nearer the muzzle.
    private Vector3 lastSocket=new(float.NaN,float.NaN,float.NaN);
    // The club hand's glove on the gun (fitted frame).
    private Vector3 clubHandPoint;private Quaternion clubHandTurn=Quaternion.identity;private float clubHandSize=1;private bool clubHandValid;
    // A club that became a still copy in its hand (another weapon taken by the other hand).
    private readonly bool[] copyClub=new bool[2];private readonly float[] copyClubAlong=new float[2],copyClubSize={1,1};
    private readonly Vector3[] copyClubPoint=new Vector3[2];private readonly Quaternion[] copyClubTurn={Quaternion.identity,Quaternion.identity};
    internal bool Clubbed=>foreEndOnly&&club&&weapon!=null;
    private bool CopyClub(int s)=>copyKey[s]>=0&&copyForeEnd[s]&&copyClub[s];
    // This hand swings a gun held by its barrel (the game's one or a copy).
    internal bool Clubbing(bool right)
    {
        int s=right?1:0;
        if(copyKey[s]>=0)return CopyClub(s);
        return Clubbed&&GameSide(CurrentKey)==1-s;
    }
    internal bool BarrelHand(bool right)=>barrelHand==(right?1:0)&&weapon!=null&&!foreEndOnly;
    // 0.1.200: each
    // gun's barrel thickness where it was held (from its mesh), and each
    // hand's fingers closed onto it (FingerPoseMath.WrapCurl) when it is
    // thinner than the stick the closed hand is shaped for.
    private static readonly System.Collections.Generic.Dictionary<string,float> barrelThickness=new();
    private readonly float[] clubCurl={FingerPoseMath.HeldCurl,FingerPoseMath.HeldCurl};private readonly string[] clubCurlFor={"",""};
    private readonly Vector3[] clubShift=new Vector3[2];
    // The barrel's middle in the fist moved with the fingers closed round it (hand units).
    private Vector3 ClubShift(bool right){ClubHandProfile(right);return clubShift[right?1:0];}
    private void MeasureBarrel()
    {
        if(visual==null)return;
        try
        {
            float t=visual.BarrelThickness(ClubGrab,out int points);
            if(!float.IsFinite(t)){Bootstrap.Write("HANDS "+profile+" barrel thickness not measured ("+points+" points across it)");return;}
            barrelThickness[profile]=t;
            Bootstrap.Write("HANDS "+profile+" barrel "+(t*1000).ToString("F0")+" mm thick where it is held ("+points+" points)");
        }
        catch(Exception ex){Bootstrap.Warn("HANDS barrel thickness: "+ex.Message);}
    }
    private string ClubHandProfile(bool right)
    {
        int s=right?1:0;string p=copyKey[s]>=0?copyProfile[s]:profile;
        if(!barrelThickness.TryGetValue(p,out float t)){clubShift[s]=Vector3.zero;return LongHandleGrip;}
        float size=copyKey[s]>=0?copyClubSize[s]:gripValid[1]?gripSizes[1]:1;if(!(size>.3f&&size<3))size=1;
        string key=p+":"+t.ToString("F4")+":"+size.ToString("F3");
        if(clubCurlFor[s]!=key)
        {
            var native=right?rightNative:leftNative;if(native==null){clubShift[s]=Vector3.zero;return LongHandleGrip;}
            clubCurl[s]=native.WrapCurl(t*.5f/size,out clubShift[s]);clubCurlFor[s]=key;
            Bootstrap.Write("HANDS the "+Side(s)+" hand's fingers closed "+clubCurl[s].ToString("F2")+" (a stick: "+FingerPoseMath.HeldCurl.ToString("F2")+") round "+p+"'s "+(t*1000).ToString("F0")+" mm barrel (the hand drawn at "+size.ToString("F2")+"), its middle moved "+(clubShift[s]*1000).ToString("F1")+" mm");
        }
        return clubCurl[s]>FingerPoseMath.HeldCurl+.005f?FingerPoseMath.WrapProfile(clubCurl[s]):LongHandleGrip;
    }
    private float ClubLength=>EquipmentProfile.Length(profile);
    private Vector3 ClubGrab=>CV(ClubMath.GrabPoint(CN(visual?.MuzzleOffset??Vector3.zero),clubAlong));
    private Vector3 ClubStockEnd=>CV(ClubMath.StockEnd(CN(visual?.MuzzleOffset??Vector3.zero),ClubLength));
    private static System.Numerics.Vector3 CN(Vector3 v)=>new(v.x,v.y,v.z);
    private static Vector3 CV(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);
    // A hand (world) at the barrel's front of the game's gun drawn now.
    private bool AtBarrelFront(Vector3 hand,out float along)
    {
        along=ClubMath.MinFromMuzzle;
        if(weapon==null||visual==null||!poseValid||!ClubMath.Clubbable(profile))return false;
        var local=visual.FittedToWorld.inverse.MultiplyPoint3x4(hand);
        return ClubMath.Along(CN(local),CN(visual.MuzzleOffset),ClubLength,out along);
    }
    // ...and nearer the muzzle than the fore-end the support grip holds.
    private bool BarrelGrab(Vector3 hand,Vector3 socket,out float along)
    {
        if(!AtBarrelFront(hand,out along)||visual==null)return false;
        var front=visual.FittedToWorld.MultiplyPoint3x4(CV(ClubMath.GrabPoint(CN(visual.MuzzleOffset),ClubMath.MinFromMuzzle)));
        return ClubMath.NearerFront(CN(hand),CN(front),CN(socket));
    }
    private bool HandFreeForBarrel(int s)
    {
        bool right=s==1;
        if(copyKey[s]>=0||DualActive||InteractionDriver.Current?.HandOccupied(right)==true||NpcHitReactions.Current?.Grabbing(right)==true)return false;
        if(GameUiControls.Current?.ItemHeldOn(right)==true||GripCarry.Current?.HidesHand(right)==true)return false;
        if(ManualReady&&reload.Racking)return false;
        return right?!ReloadHandHolding(true):!LeftReloadHolding&&!LeftPistolVisible;
    }
    // Hand o (not on the handle): its grip at the barrel's front holds the barrel.
    private void TrackBarrelHand(int o,Vector3 hand)
    {
        var (held,down)=GripInput(o);
        int id=weapon?.GetInstanceID()??0;
        if(barrelHand>=0&&(barrelHand!=o||!held||foreEndOnly||barrelWeapon!=id||!ClubMath.Clubbable(profile)))
        {
            if(barrelHand==o&&!held)Bootstrap.Write("HANDS the "+Side(o)+" hand let go of the barrel of "+profile);
            barrelHand=-1;
        }
        if(barrelHand>=0||!down||SupportHeld||!HandFreeForBarrel(o)||!BarrelGrab(hand,lastSocket,out float along))return;
        barrelHand=o;barrelAlong=along;barrelWeapon=id;rig.PunchHaptics(o==1);
        Bootstrap.Write("HANDS the "+Side(o)+" hand holds "+profile+" by the barrel, "+(along*100).ToString("F0")+" cm from the muzzle (the "+Side(1-o)+" hand letting go of the handle leaves it there, stock up: a club)");
    }
    // The gun becomes a club in hand c (0 left, 1 right), turning into the fist from where it is.
    private void StartClub(int c,float along,string why)
    {
        if(weapon==null)return;
        int key=(int)weapon.slot,g=1-c;
        clubBlend=false;
        if(visual!=null&&poseValid&&rig.SampleWorldHands(out var l,out var r,out bool leftValid)&&(c==1||leftValid))
        {
            var pose=c==0?l:r;var frame=visual.FittedToWorld;
            var rel=HandMirror.Relative(ToN(CameraRig.UnityPosition(pose)),ToN(CameraRig.UnityRotation(pose)),ToN((Vector3)frame.GetColumn(3)),ToN(frame.rotation));
            clubFromOffset=new Vector3(rel.offset.X,rel.offset.Y,rel.offset.Z);clubFromTurn=new Quaternion(rel.rotation.X,rel.rotation.Y,rel.rotation.Z,rel.rotation.W);
            foreEndOffset=clubFromOffset;foreEndRotation=clubFromTurn;clubBlend=true;
        }
        if(GameSide(key)!=g){if(g==0){leftGameKey=key;leftGameSince=Time.realtimeSinceStartup;}else RightHandHas(key);}
        CancelReloadGesture();StopOwnedFire();ReleaseSupport();
        foreEndOnly=true;club=true;clubAlong=along;clubSince=Time.realtimeSinceStartup;barrelHand=-1;clubHandValid=false;
        MeasureBarrel();
        rig.PunchHaptics(c==1);
        Bootstrap.Write("HANDS "+profile+" held by the barrel in the "+Side(c)+" hand ("+why+"): a club, stock up, "+(along*100).ToString("F0")+" cm from the muzzle; the other hand's grip at the barrel takes it, at the handle holds it to shoot");
    }
    // Drawn: the gun in the club hand (supportPose: that hand's controller).
    // Returns the gun's origin and rotation; keeps its hold for a still copy.
    private bool PoseClub(PoseValue pose,bool right,out Vector3 origin,out Quaternion turn)
    {
        origin=Vector3.zero;turn=Quaternion.identity;
        if(visual==null)return false;
        var at=CameraRig.UnityPosition(pose);var raw=CameraRig.UnityRotation(pose);var aim=HandAim(pose,right);
        // The glove as it would hold the gun's handle (at the controller), fingers closed round the barrel.
        NativeGrip(true,Vector3.zero);
        Quaternion hold=gripValid[1]?gripRotations[1]:Quaternion.identity;float size=gripValid[1]?gripSizes[1]:1;
        if(!right)hold=MirrorQ(hold);
        var hand=aim*hold;
        // 0.1.198: deeper into the curled fingers (the barrel lay at the fingertips).
        // 0.1.200: a thinner barrel: the fingers closed further round it, its middle moved with them.
        var fist=at+hand*(LongHandleContact(right?rightNative:leftNative,size)+(FistShift+ClubShift(right))*size);
        var clubTurn=aim*new Quaternion(ClubMath.Turn.X,ClubMath.Turn.Y,ClubMath.Turn.Z,ClubMath.Turn.W);
        var clubOrigin=fist-clubTurn*ClubGrab;
        var target=HandMirror.Relative(ToN(at),ToN(raw),ToN(clubOrigin),ToN(clubTurn));
        var offset=new Vector3(target.offset.X,target.offset.Y,target.offset.Z);var rotation=new Quaternion(target.rotation.X,target.rotation.Y,target.rotation.Z,target.rotation.W);
        float t=clubBlend?ClubMath.Blend(Time.realtimeSinceStartup-clubSince):1;
        if(t<1){offset=Vector3.Lerp(clubFromOffset,offset,t);rotation=Quaternion.Slerp(clubFromTurn,rotation,t);}
        else clubBlend=false;
        foreEndOffset=offset;foreEndRotation=rotation;
        var held=HandMirror.Apply(ToN(at),ToN(raw),ToN(offset),ToN(rotation));
        origin=new Vector3(held.position.X,held.position.Y,held.position.Z);turn=new Quaternion(held.rotation.X,held.rotation.Y,held.rotation.Z,held.rotation.W);
        // The glove stays at the controller while the gun turns into it.
        var inverse=Quaternion.Inverse(turn);
        clubHandPoint=inverse*(at-origin);clubHandTurn=inverse*hand;clubHandSize=size;clubHandValid=true;
        return true;
    }
    private bool TryPoseClubHand(out Vector3 position,out Quaternion rotation,out float size)
    {
        position=Vector3.zero;rotation=Quaternion.identity;size=clubHandSize;
        if(!clubHandValid||visual==null||!poseValid)return false;
        var frame=visual.FittedToWorld;position=frame.MultiplyPoint3x4(clubHandPoint);rotation=frame.rotation*clubHandTurn;return true;
    }
    // The club became a still copy in hand s (DemoteGame): it keeps its hold.
    private void KeepCopyClub(int s,bool byForeEnd)
    {
        copyClub[s]=byForeEnd&&club&&clubHandValid;
        if(!copyClub[s])return;
        copyClubAlong[s]=clubAlong;copyClubPoint[s]=clubHandPoint;copyClubTurn[s]=clubHandTurn;copyClubSize[s]=clubHandSize;
    }
    private void ClearClub(){club=false;clubBlend=false;clubHandValid=false;barrelHand=-1;}
}
