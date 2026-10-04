using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// The bazooka's hands. 0.1.227: each hand closed round a measured grip
// (BazookaTubeMath.OnGrip): the middle of the circle its closed fingers lie
// on is on the grip's line, the fingers closed as much as the grip is thick
// (its 3.6 cm at the hand's 0.75: 0.6 of a fist), the hand turned as the
// game's right hand holds the handle (a left hand that hold mirrored):
//  - bazooka in the right hand: the right hand on the handle (with the
//    trigger), the left hand on the front grip;
//  - bazooka in the left hand: the left hand on the handle, the right hand
//    on the front grip.
// On the handle the three lower fingers round it and the index on the
// trigger above it ("bazooka_trigger@60"); on the front grip all four round
// it; each finger closed onto the grip, the thumb clear of it (FitGrip).
// 0.1.218 to 0.1.226 put the hands where a game's own hands had been, or
// mirrored them: their fingers closed 2 to 10 cm off the grips.
internal sealed partial class WeaponHands
{
    // The fingers on the handle with a stick's closure (FingerPoseMath.HeldPose);
    // 0.1.227: with the closure for the handle's thickness (TriggerWrapProfile).
    internal const string BazookaTriggerGrip="bazooka_trigger";
    private static readonly HashSet<string> bazookaGripReported=new();
    private static Quaternion ToUq(System.Numerics.Quaternion q)=>new(q.X,q.Y,q.Z,q.W);
    // The size the game draws its hands at on the bazooka.
    private float BazookaHandSize(NativeHandVisual? native)
    {
        try{if(native!=null&&visual!=null&&native.TryWeaponGrip(visual,out _,out _,out float s,nativeRenderFrame==Time.frameCount)&&s>.3f&&s<3)return s;}catch(Exception){}
        return BazookaTubeMath.HandSize;
    }
    // The front grip's fitted hand profile (FingerPoseMath.FitGrip; the handle's: TriggerWrapPrefix).
    internal const string BazookaFrontPrefix="prop_bazooka_front@";
    // Each hand's profile on each bazooka grip (left front, left handle, right front, right handle).
    private readonly string[] bazookaProfile={"","","",""};
    // Hand `rightHand` round the handle (onHandle) or the front grip, in the fitted frame as drawn.
    private bool BazookaHold(bool rightHand,bool onHandle,out Vector3 p,out Quaternion q,out float size)
    {
        p=Vector3.zero;q=Quaternion.identity;size=1;
        if(profile!="bazooka"||visual==null)return false;
        if((onHandle?visual.TriggerGripBar:visual.FrontGripBar) is not GripBarMath.Bar bar)return false;
        var native=rightHand?rightNative:leftNative;
        size=BazookaHandSize(native);
        float radius=BazookaTubeMath.GripRadius(bar)/size,curl;System.Numerics.Vector3 channel,little;bool measured=true;
        string prefix=onHandle?FingerPoseMath.TriggerWrapPrefix:BazookaFrontPrefix;
        if(native==null||!native.GripFit(prefix,radius,bar.Length*.5f/size,onHandle,out curl,out channel,out little,out string fitted))
        {channel=BazookaTubeMath.FallbackChannel(rightHand,onHandle);little=BazookaTubeMath.FallbackLittle(rightHand,onHandle);curl=BazookaTubeMath.FallbackCurl;fitted=FingerPoseMath.ClosureProfile(prefix,curl);measured=false;}
        bazookaProfile[(rightHand?2:0)+(onHandle?1:0)]=fitted;
        var placed=BazookaTubeMath.OnGrip(bar,BazookaTubeMath.RightTurn,rightHand,BazookaTubeMath.DrawnChannel(channel,size),little);
        // 0.1.231: on the handle, moved along it until the index fingertip is level with the trigger.
        float raise=float.NaN;
        // (No trigger measured: the web of the hand up to the handle's own top.)
        var aim=visual.TriggerPoint is Vector3 measuredTrigger?ToN(measuredTrigger):visual.TriggerGripTop!=null?bar.Center+System.Numerics.Vector3.Normalize(bar.Axis):(System.Numerics.Vector3?)null;
        if(onHandle&&measured&&aim is System.Numerics.Vector3 trigger&&native!.IndexPad((rightHand?"":FingerPoseMath.MirrorPrefix)+fitted,out var pad,out var knuckle))
        {
            raise=BazookaTubeMath.RaiseToTrigger(bar,placed.position,placed.rotation,BazookaTubeMath.DrawnChannel(pad,size),trigger,BazookaTubeMath.DrawnChannel(knuckle,size),visual.TriggerGripTop);
            placed.position+=System.Numerics.Vector3.Normalize(bar.Axis)*raise;
        }
        p=ToU(placed.position);q=ToUq(placed.rotation);
        // Written once for each hand and grip of each bazooka drawn.
        string side=(onHandle?"handle ":"front ")+(rightHand?"R":"L")+"#"+visual.GetHashCode();
        if(bazookaGripReported.Count>64)bazookaGripReported.Clear();
        if(bazookaGripReported.Add(side))
            Bootstrap.Write("BAZOOKA "+(onHandle?"handle (with the trigger)":"front grip")+" held by the "+(rightHand?"right":"left")+" hand round it: the fingers closed "+curl.ToString("F2")+" round its "+(bar.Thickness*100).ToString("F1")+" cm"
                +(onHandle?" (the index on the trigger above them)":"")+", the middle of their curl on its line at its middle "+bar.Center.ToString("F3",null)
                +(float.IsFinite(raise)?", moved "+(raise*100).ToString("0.0")+" cm up it ("+(visual.TriggerPoint!=null?"the index fingertip level with the trigger":"no trigger measured: the web of the hand at its top")+(visual.TriggerGripTop!=null?", not above its top":"")+")":onHandle?", not moved to the trigger ("+(visual.TriggerPoint==null&&visual.TriggerGripTop==null?"no trigger measured":"no fingertip measured")+")":"")
                +"; hand at "+p.ToString("F3")+" (drawn at "+size.ToString("F2")+"; "
                +(measured?"each finger closed onto it, the thumb clear of it: "+native!.GripFitReport(fitted):"the logged hand's fingers")+")");
        return true;
    }
    // NativeGrip's bazooka holds, in the frame TryPoseHand reads them: index 1
    // (right) the right hand on the handle (TryPoseHand takes the left hand's
    // own for a bazooka in the left hand: BazookaLeftHandle); index 0 the
    // support hand on the front grip - the left hand, or the right hand for a
    // bazooka in the left hand (then mirrored here, as TryPoseHand mirrors it back).
    private bool BazookaGrip(bool right,NativeHandVisual native,out Vector3 p,out Quaternion q,out float size)
    {
        if(right)return BazookaHold(true,true,out p,out q,out size);
        if(!BazookaHold(PrimaryLeft,false,out p,out q,out size))return false;
        if(PrimaryLeft){p=MirrorAcross(profile,p);q=MirrorQ(q);}
        return true;
    }
    // The bazooka in the left hand: the left hand round the handle (fitted frame, as drawn).
    private bool BazookaLeftHandle(out Vector3 p,out Quaternion q,out float size)
    {
        p=Vector3.zero;q=Quaternion.identity;size=1;
        return PrimaryLeft&&BazookaHold(false,true,out p,out q,out size);
    }
    // The handle hand's place on the gun (fitted frame): the left hand's own
    // for a bazooka in the left hand, else the right hand's (mirrored for a left hand).
    private Vector3 PrimaryHandPoint()
    {
        var p=NativeGrip(true,Vector3.zero);
        return PrimaryLeft&&BazookaLeftHandle(out var left,out _,out _)?left:HandleSided(p);
    }
    // 0.1.229: the hold report (every few seconds while the bazooka is in a
    // hand): where each hand is drawn, how far from its hold, and how far the
    // game's animation has the bazooka from where it is drawn (held still).
    // 0.1.227/0.1.228 wrote each reason once, so a hand off its grip later in a
    // session said nothing.
    private readonly string[] holdNote={"",""};private readonly int[] holdFrame={-1,-1},drawnFrame={-1,-1};
    private readonly Vector3?[] holdAt=new Vector3?[2];private readonly Vector3[] drawnAt=new Vector3[2];private readonly bool[] drawnHeld=new bool[2];
    private float nextHoldReport;
    private void NoteHold(bool right,string note,Vector3? at){int s=right?1:0;holdNote[s]=note;holdAt[s]=at;holdFrame[s]=Time.frameCount;}
    // The glove's final place this frame (GloveVisual), held or free.
    internal void NoteHandDrawn(bool right,bool held,Vector3 at){if(profile!="bazooka")return;int s=right?1:0;drawnAt[s]=at;drawnHeld[s]=held;drawnFrame[s]=Time.frameCount;}
    private void BazookaHoldReport()
    {
        if(profile!="bazooka"||weapon==null||visual==null||Time.realtimeSinceStartup<nextHoldReport)return;
        nextHoldReport=Time.realtimeSinceStartup+6;
        string Hand(int s)
        {
            string who=s==1?"right":"left";
            if(drawnFrame[s]<Time.frameCount-2)return who+" hand not drawn";
            string note=holdFrame[s]>=Time.frameCount-2?holdNote[s]:"no hold worked out";
            string where=drawnHeld[s]&&holdAt[s] is Vector3 at&&holdFrame[s]>=Time.frameCount-2?", drawn "+(Vector3.Distance(at,drawnAt[s])*100).ToString("F1")+" cm from it":drawnHeld[s]?", drawn held":", drawn free";
            return who+" hand "+note+where;
        }
        string drift=visual.StillDrift(out float m,out float deg)?"; the game's animation would have the bazooka "+(m*100).ToString("F1")+" cm and "+deg.ToString("F0")+" degrees from where it is drawn (drawn still, as taken, where the hands hold it)":"";
        Bootstrap.Write("BAZOOKA HOLD "+(PrimaryLeft?"in the left hand":"in the right hand")+(SupportHeld?", two hands":"")+": "+Hand(PrimaryLeft?0:1)+"; "+Hand(PrimaryLeft?1:0)+drift);
    }
    // The fingers of a hand on a bazooka grip (HandProfileFor): on the
    // handle the index on the trigger and the others closed round it; on the
    // front grip all of them closed round it (as much as the grip is thick).
    private bool BazookaGripHand(bool right,out string grip)
    {
        grip="";
        if(profile!="bazooka"||weapon==null||visual==null)return false;
        bool onHandle=right!=PrimaryLeft;var fitted=bazookaProfile[(right?2:0)+(onHandle?1:0)];
        if(fitted.Length==0)fitted=onHandle?BazookaTriggerGrip:LongHandleGrip;
        grip=(onHandle&&PrimaryLeft?FingerPoseMath.MirrorPrefix:"")+fitted;
        return true;
    }
}
