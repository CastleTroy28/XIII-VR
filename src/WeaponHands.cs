using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
using U = UnityEngine.Vector3;
using Q = UnityEngine.Quaternion;
using N = System.Numerics.Vector3;
namespace XiiiXR;
internal sealed partial class WeaponHands : IDisposable
{
    internal static WeaponHands? Current;
    private readonly CameraRig rig;
    private readonly Harmony patches = new("xiii.vr.xrbootstrap.weapons");
    private readonly GripMath grip = new();
    private readonly PistolSupport pistolSupport = new();
    private readonly ShotFeedback feedback = new();
    private readonly WeaponInertia inertia=new();
    // 0.1.207: the aim steadied while an eye is at the scope (as if holding the breath).
    private readonly ScopeSteadyMath scopeSteady=new();private bool steadyAtEye;private float nextSteadyReport;
    private readonly PunchDriver punches=new();
    // 0.1.139: NPCs react to punches with their skeleton.
    private NpcHitReactions? npcHits;
    private int inertiaFrame=-1;
    private Q weightedRotation;
    private int feedbackFrame=-1,visualShotSequence;
    private float visualKick,visualPitch;
    private bool visualFlash;
    private void ResetFeedback()
    { feedback.Reset(); visual?.ResetCycle(); feedbackFrame=-1; visualShotSequence=0; visualKick=visualPitch=0; visualFlash=false; rig.CancelShotHaptics(); }
    internal void OnRelocated(){inertia.Reset();scopeSteady.Reset();inertiaFrame=-1;punches.Reset();InvalidateRenderPose();}
    internal void ResetHandAlignment()
    {
        StopOwnedFire();ReleaseSupport();rightNative?.ResetGrips();leftNative?.ResetGrips();
        Array.Clear(gripValid,0,2);Array.Clear(gripSampled,0,2);OnRelocated();
    }
    private int poseFrame=-1, aimRevision=-1;
    private Q pistolAim;
    // 0.1.156: the other hand holds the game's weapon too (a stick in both hands).
    internal bool BothHandsOn=>weapon!=null&&SupportHeld;
    private bool SupportHeld => (ManualReady&&profile=="shotgun"&&reload.Racking)|| ((profile=="pistol"||profile=="revolver") ? pistolSupport.Held : grip.Held);
    // 0.1.128: either hand may hold the game's weapon, its fore-end or a copy.
    // 0.1.245: this hand holds a weapon taken from the ground or the body (a
    // still copy), or its grip took one a moment ago: that grip press does not
    // pick a thing up as well (InteractionDriver; a revolver and the broom
    // behind it ended up in one hand).
    private readonly float[] tookAt={-10,-10};
    internal const float TakeQuiet=.6f;
    internal bool HandTaken(bool right){int s=right?1:0;return copyKey[s]>=0||Time.realtimeSinceStartup-tookAt[s]<TakeQuiet;}
    internal bool HandFree(bool right)
    {
        int s=right?1:0,key=CurrentKey;
        if(GripCarry.Current?.HidesHand(right)==true||copyKey[s]>=0||HandleHeld(s,key)||ForeEndHeld(s,key))return false;
        // 0.1.221: its grip takes (or holds) rounds at the belt or a rocket.
        if(rocketSide==s||PouchTakesNow(s))return false;
        if(right)return !(nearHolster&&MainSide==1)&&!TakePending&&!ReloadHandHolding(true)&&GameUiControls.Current?.RightItemHeld!=true;
        return !(nearHolster&&MainSide==0)&&!LeftReloadHolding&&!LeftPistolVisible&&GripCarry.Current?.HidesLeft!=true&&GameUiControls.Current?.LeftItemHeld!=true;
    }
    internal bool ScopeViewing=>visual?.ScopeViewing==true;
    internal bool OffhandOccupied=>copyKey[0]>=0||HandleHeld(0,CurrentKey)||ForeEndHeld(0,CurrentKey)||LeftReloadHolding||LeftPistolVisible;
    internal string Profile=>profile;
    internal bool HasTrackedWeapon=>poseValid && enabled && !failed && weapon!=null;
    private void ReleaseSupport() { grip.Release(); pistolSupport.Release(); poseFrame=-1; }
    private readonly Dictionary<int, (bool left, ulong bit)> buttons = new();
    private readonly Dictionary<int,int> lastCommandFrame = new();
    private PlayerEquipableInventory? inventory;
    private PlayerEquipableHandler? handler;
    private Transform? playerRoot;
    private int boundRootId;
    private string lastGate="";
    private Equipable? weapon;
    private FireComponent? fire;
    private Transform? muzzle;
    private WeaponVisual? visual;
    private readonly VisualCache<WeaponVisual> visuals=new(3);
    private NativeHandVisual? leftNative,rightNative;
    internal NativeHandVisual? LeftNative=>leftNative;
    internal NativeHandVisual? RightNative=>rightNative;
    private bool reportedRightGrip,reportedLeftGrip;
    private int nativeRenderFrame=-1,gripFrame=-1;
    // 0.1.121: the pose (with the gun's collision solve) is computed once in
    // the update part of a frame and once before rendering. The fire, muzzle,
    // button, touch and prop hooks of the same part read that pose; the solve
    // ran 6-10 times a frame before.
    private int poseCallFrame=-1;private bool poseCallRendering;
    private readonly U[] gripPositions=new U[2];
    private readonly Q[] gripRotations=new Q[2];
    private readonly float[] gripSizes=new float[2];
    private readonly bool[] gripValid=new bool[2],gripSampled=new bool[2];
    internal void InvalidateRenderPose(){nativeRenderFrame=gripFrame=poseFrame=poseCallFrame=-1;rightNative?.InvalidateSample();leftNative?.InvalidateSample();}
    internal void RegisterHand(NativeHandVisual native,bool right)
    {
        if(ReferenceEquals(right?rightNative:leftNative,native))return;
        gripFrame=poseFrame=-1;Array.Clear(gripValid,0,2);Array.Clear(gripSampled,0,2);
        if(right)rightNative=native;else leftNative=native;
    }
    internal bool TryPoseHand(NativeHandVisual native,bool right,out U position,out Q rotation,out float size)
    {
        position=U.zero;rotation=Q.identity;size=1;
        if(!right&&LeftPistolVisible){position=leftHandPosition;rotation=leftHandRotation;size=gripValid[1]?gripSizes[1]:1;return true;}
        // 0.1.128: a still copy in this hand; the game's weapon in the left hand
        // (the left hand on the handle, the right on the fore-end: mirrored).
        if(TryPoseCopyHand(right,out position,out rotation,out size))return true;
        bool mirrored=PrimaryLeft,handle=right!=mirrored;
        bool pump=ManualReady&&profile=="shotgun"&&reload.Racking;
        if(!HasTrackedWeapon || visual==null || !CanControl(playerId))return SupportMiss(handle,"no weapon under control",WeaponState());
        // 0.1.196: the hand holding the gun by its barrel (a club).
        if(Clubbed&&!handle)return TryPoseClubHand(out position,out rotation,out size);
        if(handle?foreEndOnly:!(foreEndOnly||SupportHeld&&!(reload.Active&&profile!="shotgun")||pump))return SupportMiss(handle,"a reload ("+(reload.Holding?"holding":reload.NeedsRack?"rack":reload.Installed?"active":"no magazine")+")");
        int index=handle?1:0;
        // 0.1.228: the bazooka's hold worked out here when the hands are drawn before the gun this frame.
        if(profile=="bazooka"&&(gripFrame!=Time.frameCount||!gripSampled[index]))NativeGrip(index==1,U.zero);
        if(gripFrame!=Time.frameCount || !gripValid[index])return SupportMiss(handle,gripFrame!=Time.frameCount?"the hands drawn before the gun this frame":handle?"no hold on the handle":"no hold on the front grip");
        size=gripSizes[index];var matrix=visual.FittedToWorld;
        var point=gripPositions[index];var turn=gripRotations[index];
        if(!handle&&pump)point.z-=reload.RackTravel;
        // 0.1.225: the bazooka's handle in the left hand: the left hand's own hold, not the right one's mirrored.
        if(mirrored&&handle&&BazookaLeftHandle(out var leftAt,out var leftTurn,out float leftSize)){point=leftAt;turn=leftTurn;size=leftSize;}
        else if(mirrored){point=handle?LeftHandle(profile,point):MirrorAcross(profile,point);turn=MirrorQ(turn);}
        position=matrix.MultiplyPoint3x4(point);rotation=matrix.rotation*turn;
        if(profile=="bazooka")NoteHold(right,handle?"on its handle":"on its front grip",position);
        return true;
    }
    // 0.1.227: why a support hand holding the bazooka two-handed is not drawn on its front grip (once each).
    private static readonly HashSet<string> supportMissReported=new();
    // 0.1.228: and why the hand holding it by its handle is not drawn there.
    // 0.1.229: with the state behind it (detail), and noted for the hold report (BazookaHoldReport).
    private bool SupportMiss(bool handle,string why,string detail="")
    {
        if(profile!="bazooka")return false;
        bool right=handle!=PrimaryLeft;
        NoteHold(right,!handle&&!SupportHeld?"free (one hand on the bazooka)":(handle?"not on its handle: ":"not on its front grip: ")+why+(detail.Length>0?" ("+detail+")":""),null);
        if(supportMissReported.Count>64)supportMissReported.Clear();
        if(handle){if(supportMissReported.Add("handle "+why))Bootstrap.Write("BAZOOKA the hand holding it is not drawn on its handle: "+why+(detail.Length>0?" ("+detail+")":""));}
        else if(SupportHeld&&supportMissReported.Add(why))Bootstrap.Write("BAZOOKA two hands, but the support hand is not drawn on the front grip: "+why+(detail.Length>0?" ("+detail+")":""));
        return false;
    }
    // 0.1.229: what keeps the game's weapon from being under the hands' control.
    private string WeaponState()
    {
        string gate=ControlGate(playerId);
        return "pose "+(poseValid?"valid":"invalid")+(weapon==null?", no weapon":"")+(visual==null?", no visual":"")+(gate.Length>0?", "+gate:"");
    }
    private string ControlGate(int id)
    {
        if(disposed)return "disposed";if(failed)return "suspended after an error";if(!enabled)return "weapon controls off (F8)";
        if(PropConsumed)return "the thing used up";if(InteractionDriver.Current?.KeyActive==true)return "a key in the hand";
        if(GameUiControls.Current?.PendingConsumable==true)return "a medkit in the hand";if(id!=playerId||playerId<0)return "no player";
        if(handler==null||inventory==null)return "no inventory";if(!rig.HeadTrackingValid)return "no head tracking";if(rig.Scripted)return "a story scene";
        if(GameUiControls.Current?.BlocksGameplay==true)return "a menu";if(!WindowFocus.Playable)return "the game window not in focus";
        if(Time.timeScale<=0||PauseMenuControl.HackGameIsPaused)return "paused";if(GameInputManager.IsInputLocked(playerId))return "the game's input locked";
        return "";
    }
    // The glove's side of it (GloveVisual): the support hand carrying something, drawn free or hidden.
    internal void NoteSupportGlove(bool right,string why)
    {
        if(profile!="bazooka"||!HasTrackedWeapon||foreEndOnly)return;
        if(supportMissReported.Count>64)supportMissReported.Clear();
        if(right!=PrimaryLeft){if(copyKey[right?1:0]<0&&supportMissReported.Add("glove handle "+why))Bootstrap.Write("BAZOOKA the hand holding it is not drawn on its handle: "+why);}
        else if(SupportHeld&&supportMissReported.Add("glove "+why))Bootstrap.Write("BAZOOKA two hands, but the support hand is not drawn on the front grip: "+why);
    }
    private U NativeGrip(bool right,U fallback)
    {
        if(gripFrame!=Time.frameCount){gripFrame=Time.frameCount;Array.Clear(gripSampled,0,2);Array.Clear(gripValid,0,2);}
        int index=right?1:0;
        if(gripSampled[index])return gripValid[index]?gripPositions[index]:fallback;
        gripSampled[index]=true;
        var native=right?rightNative:leftNative;
        if(visual==null || native==null)return fallback;
        if(visual.ControlledPropGrip)
        {
            gripValid[index]=true;gripPositions[index]=right?U.zero:new U(-.11f,-.025f,.073f);gripRotations[index]=Q.identity;gripSizes[index]=1;
            // 0.1.81: the ashtray is a striking weapon; with its measured
            // sections it is held like the chair board (whole hand, edge in the
            // web), not pinched with fingertips. Rim pinch only as a fallback.
            bool ashtray=visual.GripProfile.Contains("ashtray");
            if(right&&ashtray&&visual.ChairSurface==null)
            {
                var contact=native.PropRimContact(visual.GripProfile,visual.PropGripThickness);
                gripRotations[index]=native.PropRimHandRotation;
                gripPositions[index]=visual.PropGripContact-gripRotations[index]*contact;
            }
            else if(right)
            {
                // Contact first: it runs the tray solve that also yields the
                // hand's rotation along the raked backrest / tilted plate.
                var contact=native.PropPowerContact(visual.GripProfile,visual.PropGripThickness,visual.ChairSurface);
                gripRotations[index]=FingerPoseMath.TrayProfile(visual.GripProfile)?native.PropChairHandRotation:Q.identity;
                gripPositions[index]=visual.PropGripContact-gripRotations[index]*contact;
            }
            if(right&&!reportedRightGrip){reportedRightGrip=true;Bootstrap.Write("PROP RIGID GRIP "+visual.GripProfile+" wrist="+gripPositions[index].ToString("F6")+" rim="+visual.PropGripContact.ToString("F6")+" thickness="+visual.PropGripThickness.ToString("F6")+" "+native.PropRimReport);Bootstrap.Write("HAND REST "+native.HandRestReport);}
            return gripPositions[index];}
        // 0.1.219: the bazooka's front grip (0.1.225: where the game holds it, or a hand's own hold moved there).
        if(BazookaGrip(right,native,out var bazookaAt,out var bazookaTurn,out float bazookaSize))
        {
            gripValid[index]=true;gripPositions[index]=bazookaAt;gripRotations[index]=bazookaTurn;gripSizes[index]=bazookaSize;
            if(right)RememberGrip(visual.GripProfile,bazookaAt,bazookaTurn,bazookaSize);else foreEndGrips[visual.GripProfile]=(bazookaAt,bazookaTurn,bazookaSize);
            return bazookaAt;
        }
        U p;Q q;float size;
        // NativeHandVisual caches the wrist and finger skin together per visual.
        if(!native.TryWeaponGrip(visual,out p,out q,out size,nativeRenderFrame==Time.frameCount))
        {
            // 0.1.140: no sample of the game's right hand this frame (the gun in
            // the left hand: the revolver hung beside the open left hand): the
            // last good handle hold of this kind.
            if(!right||!HolsterLayout.Firearm(profile)||!handleGrips.TryGetValue(visual.GripProfile,out var last))return fallback;
            if(cachedGripReported.Add(profile))Bootstrap.Write("NATIVE GRIP "+profile+": no sample of the game's right hand now; its last handle hold is used "+last.point.ToString("F4"));
            gripValid[index]=true;gripPositions[index]=last.point;gripRotations[index]=last.rotation;gripSizes[index]=last.size;
            return last.point;
        }
        // 0.1.257: a gun drawn larger than the game draws it against its hand
        // (the AK, the M16, the crossbows): the hand holding it at the size it
        // had, its fist where it was (the cached holds below are kept so).
        float growth=EquipmentProfile.Growth(profile);
        if(growth!=1){var grown=GrownGrip.Hand(ToN(p),ToN(q),size,ToN(LongHandleContact(native,1)),growth);p=ToU(grown.wrist);size=grown.size;}
        // 0.1.136: the game's left arm is not always on a long gun's fore-end
        // (the shotgun's sample was 47 cm off, and the hand hung there).
        if(!right&&HolsterLayout.Firearm(profile)&&!PlausibleForeEnd(p))
        {
            if(!foreEndGrips.TryGetValue(visual.GripProfile,out var cached)||!PlausibleForeEnd(cached.point))return fallback;
            p=cached.point;q=cached.rotation;size=cached.size;
        }
        // 0.1.100: broom/mop — slide the native hold along the stick to near
        // the handle end (right), the support hand 40 cm further up.
        if(visual.LongGripOrigin is U origin)
        {
            var axis=visual.LongGripAxis;float along=U.Dot(p-origin,axis);var offset=p-(origin+axis*along);
            // 0.1.142: the game's
            // own left arm is not on the shovel (65 cm off its stick): the
            // other hand takes the right hand's hold on the stick, mirrored
            // across it (as a left hand holds a gun's handle).
            if(!right)longSupportMirrored=offset.magnitude>LongHandleReach;
            if(!right&&offset.magnitude>LongHandleReach)
            {
                var r=NativeGrip(true,U.zero);
                if(!gripValid[1])return fallback;
                var ro=r-(origin+axis*U.Dot(r-origin,axis));
                offset=new U(-ro.x,ro.y,ro.z);q=MirrorQ(gripRotations[1]);size=gripSizes[1];
                if(longSupportReported.Add(visual.GripProfile))Bootstrap.Write("LONG HANDLE "+visual.GripProfile+": the game's other hand is "+(p-(origin+axis*along)).magnitude.ToString("F2")+" m off the stick; the "+(right?"right":"left")+" hand holds it like the right hand, mirrored, 40 cm up (right hand "+ro.magnitude.ToString("F3")+" m from the stick's middle)");
            }
            var stick=origin+axis*(right?0f:.40f);
            p=stick+offset;
            // 0.1.143: the
            // game's own hold was made on its small shovel, fitted to a metre
            // it put the stick at the wrist, not in the fist (9 cm off). When
            // the stick misses the grip of the closed hand, the hand is put so
            // that the stick lies in it (its turn kept), fingers closed round it.
            var contact=LongHandleContact(native,size);
            float miss=LongHandleMath.Miss(ToN(p+q*contact),ToN(origin),ToN(axis));
            longGripClosed[index]=miss>LongHandleMath.MaxMiss;
            if(longGripClosed[index])
            {
                p=stick-q*contact;
                if(longClosedReported.Add(visual.GripProfile+(right?"R":"L")))Bootstrap.Write("LONG HANDLE "+visual.GripProfile+": the "+(right?"right":"left")+" hand's hold missed the stick by "+miss.ToString("F3")+" m; the stick now lies in the closed hand (grip "+contact.ToString("F3")+")");
            }
        }
        else longGripClosed[index]=false;
        gripValid[index]=true;gripPositions[index]=p;gripRotations[index]=q;gripSizes[index]=size;
        // 0.1.128: kept for still copies held in a hand (0.1.130: the fore-end hold too).
        if(visual.LongGripOrigin==null){if(right)RememberGrip(visual.GripProfile,p,q,size);else foreEndGrips[visual.GripProfile]=(p,q,size);}
        // 0.1.156: the knife's own pose while the right hand holds it (its copies are drawn in it).
        if(right&&profile=="knife"&&!KnifeLaunching&&Time.realtimeSinceStartup-knifeReleasedAt>1.5f&&Time.realtimeSinceStartup>=nextHeldPose)
        {nextHeldPose=Time.realtimeSinceStartup+.2f;var poses=visual.LivePoses();if(Array.Exists(poses,x=>x!=null)){if(!heldPoses.ContainsKey(profile))Bootstrap.Write("HOLSTER "+profile+": its pose in the right hand kept for its copies");heldPoses[profile]=poses;}}
        // 0.1.135: the middle of the handle the right hand holds (the left
        // hand's hold is mirrored across it); pistols keep the box's middle.
        // 0.1.140: pistols and revolvers too (the revolver's handle is not in
        // the middle of its box: it lay beside the left hand).
        if(right&&!mirrorCenters.ContainsKey(profile)&&HolsterLayout.Firearm(profile))
        {
            float middle=float.NaN;int points=0;
            try{middle=visual.HandleMiddleX(p,q,size,out points);}catch(Exception ex){Bootstrap.Warn("WEAPON MIRROR "+profile+" handle: "+ex.Message);}
            mirrorCenters[profile]=float.IsFinite(middle)?middle:0;
            // 0.1.221: the bazooka mirrored across its own plane of symmetry (its
            // tube's line): around the palm the handle's middle came out 1.4 cm
            // to the right of it, and the left hand hung off its grips.
            // 0.1.223: across its handle's own middle (the handle with the trigger, measured), else its tube's line.
            if(profile=="bazooka"&&visual.SymmetryX is float symmetry){Bootstrap.Write("WEAPON MIRROR bazooka across x="+symmetry.ToString("F4")+" ("+(visual.TriggerGripBar!=null?"its handle's middle":"its tube's line")+"; around the palm "+(float.IsFinite(middle)?middle.ToString("F4"):"none")+")");mirrorCenters[profile]=symmetry;}
            Bootstrap.Write("WEAPON MIRROR "+profile+" handle middle x="+(float.IsFinite(middle)?middle.ToString("F4"):"none (box middle)")+" from "+points+" points around the palm; right hand x="+p.x.ToString("F4")+", left hand x="+LeftHandle(profile,p).x.ToString("F4"));
        }
        if(right?!reportedRightGrip:!reportedLeftGrip)
        {
            if(right)reportedRightGrip=true;else reportedLeftGrip=true;
            Bootstrap.Write("NATIVE GRIP bound weapon="+profile+" side="+(right?"R":"L")+" local="+p.ToString("F6")+" rotation="+q.eulerAngles.ToString("F3")+" handScale="+size+(EquipmentProfile.Growth(profile)!=1?" (the gun drawn "+EquipmentProfile.Growth(profile).ToString("F2")+"x the game's size against its hand, the hand kept its size)":""));
        }
        return p;
    }
    private MuzzleEffects? muzzleEffects;
    private static readonly HashSet<string> cachedGripReported=new(),longSupportReported=new();
    // Farther than this from the stick's middle, a hand is not on it.
    internal const float LongHandleReach=.08f;
    private bool longSupportMirrored;
    private readonly bool[] longGripClosed=new bool[2];
    private static readonly HashSet<string> longClosedReported=new();
    // Fingers closed round a stick (a held prop pose: every finger curled).
    internal const string LongHandleGrip="prop_long_handle";
    private static U LongHandleContact(NativeHandVisual? native,float size)
    {
        U c;try{c=native!=null?native.PropPowerContact(LongHandleGrip,LongHandleMath.StickThickness):new U(0,-.025f,.073f);}catch(Exception){c=new U(0,-.025f,.073f);}
        if(!float.IsFinite(c.sqrMagnitude)||c.sqrMagnitude<1e-6f||c.magnitude>.15f)c=new U(0,-.025f,.073f);
        return c*(float.IsFinite(size)&&size>.3f&&size<3?size:1);
    }
    private float nextVisualAttempt;
    private U aimPosition, aimForward, aimUp;
    private U desiredMeleePosition;private Q desiredMeleeRotation;
    private float nextDiscover, nextReport, nextShotReport, nextBindReport;
    private bool enabled, failed, disposed, poseValid, fireOwned, secondaryOwned, syncing;
    private int secondaryAction, primaryAction, playerId = -1;
    private string profile = "";
    private int seenWeaponId;
    internal WeaponHands(CameraRig cameraRig)
    {
        rig = cameraRig; enabled = WeaponOptions.Enabled.Value;
        try
        {
            primaryAction = Map(InputActions.Weapon_PrimaryFire, false, HandControls.Trigger);
            secondaryAction=Map(InputActions.Weapon_SecondaryFire,false,HandControls.Stick);
            reloadAction=Map(InputActions.Weapon_Reload, false, HandControls.B);
            Patch(typeof(PlayerArmsAnimationControl),"StartIdleTimer",nameof(AllowIdleTimer),false);
            Patch(typeof(PlayerArmsAnimationControl),"CanStartIdleBreakTimer",nameof(CanIdle),false);
            Patch(typeof(PlayMagic.Weapons.AnimationComponent),"IdleBreakerAnimationStart",nameof(AllowWeaponIdle),false);
            Patch(typeof(AmmoManagementComponent),"StartReload",nameof(AllowStartReload),false);
            Patch(typeof(ReloadSound),"FireSound",nameof(AllowReloadSound),false);
            Patch(typeof(AmmoManagementComponent),"ReloadPrimary",nameof(AllowStockReload),false);
            Patch(typeof(GameInputManager), "GetButton_Internal", nameof(ButtonHeld), true);
            Patch(typeof(GameInputManager), "GetButtonDown_Internal", nameof(ButtonDown), true);
            Patch(typeof(GameInputManager), "GetButtonUp_Internal", nameof(ButtonUp), true);
            Patch(typeof(PlayerEquipableHandler), "GetHitPoint", nameof(HitPoint), false);
            Patch(typeof(PlayerEquipableHandler), "GetCameraForwardRay", nameof(ForwardRay), false);
            Patch(typeof(FireComponent), "FixedUpdate", nameof(RefreshMuzzle), true);
            Patch(typeof(MeleeComponent),"Begin",nameof(AllowButtonMelee),false);
            Patch(typeof(MeleeComponent),"SpawnSurfaceHitSFX",nameof(SurfaceSound),false);
            Patch(typeof(MeleeComponent),"DoMeleeAttack",nameof(AllowButtonMelee),false);
            patches.Patch(AccessTools.DeclaredMethod(typeof(SingleFireComponent),"ExecuteFireTask"),prefix:new HarmonyMethod(typeof(WeaponHands),nameof(BeginFireTask)),finalizer:new HarmonyMethod(typeof(WeaponHands),nameof(EndFireTask)));
            Patch(typeof(FireComponent),"GetLaunchDirection",nameof(AimLaunch),false);
            Patch(typeof(AmmoManagementComponent),"StartReload",nameof(DualReload),true);
            Patch(typeof(SingleFireSound),"FireSound",nameof(SelectedShotSound),false);
            try{Patch(typeof(AutoFireSound),"FireSound",nameof(CopyNativeSound),false);Patch(typeof(BurstFireSound),"FireSound",nameof(CopyNativeSound),false);}
            catch(Exception ex){Bootstrap.Warn("COPY FIRE native sound hook: "+ex.Message);}
            Patch(typeof(FireComponent), "SetUpProjectile", nameof(BeforeProjectile), false);
            // 0.1.194: the crossbow bolt reaches the sighted point.
            try{Patch(typeof(FireComponent),"SetUpProjectile",nameof(AimedBolt),true);}
            catch(Exception ex){Bootstrap.Warn("CROSSBOW straight bolt hook unavailable: "+ex.Message);}
            Patch(typeof(FireComponent), "ProjectileLaunched", nameof(Shot), true);
            Patch(typeof(BulletSpreadComponent),"CalculateOffset",nameof(SupportedSpread),true);
            Patch(typeof(ButtonPrompt),"SetInputHint",nameof(PromptLabel),true);
            Patch(typeof(TutorialController),"GetKeyOrIconReference",nameof(TutorialButton),false);
            // 0.1.175: each tutorial hint once per game (TutorialOnce).
            try{Patch(typeof(TutorialController),"ShowMessage",nameof(TutorialShow),false);}
            catch(Exception ex){Bootstrap.Warn("TUTORIAL once-only hook unavailable (hints may repeat): "+ex.Message);}
            // 0.1.179: and the whole tutorial step (the weapon wheel it opens) once.
            try{Patch(typeof(EventReceiver.TutorialShowEvent),"ExecuteInteraction",nameof(TutorialStep),false);}
            catch(Exception ex){Bootstrap.Warn("TUTORIAL once-only step hook unavailable (the weapon wheel tutorial may open again): "+ex.Message);}
            Patch(typeof(ThrowingComponent),"LaunchProjectile",nameof(PropLaunched),false);
            Patch(typeof(ThrowingComponent),"ExecuteFireTask",nameof(PropFireTask),false);
            // 0.1.138: one throw, one grenade (a second one came from the same
            // throw); the thrown grenade does not tick.
            try{Patch(typeof(ThrowingComponent),"HandleProjectile",nameof(ThrowHandleProjectile),false);Patch(typeof(ThrowingComponent),"StartThrow",nameof(ThrowStarted),true);Patch(typeof(ThrowingComponent),"End",nameof(ThrowEnding),false);Patch(typeof(ThrowingComponent),"EndThrow",nameof(ThrowEndThrow),false);}
            catch(Exception ex){Bootstrap.Warn("GRENADE second-projectile guard (HandleProjectile) unavailable: "+ex.Message);}
            try{Patch(typeof(DetonatingProjectile),"StartExplosionSound",nameof(GrenadeSoundStarted),true);Patch(typeof(DetonatingProjectile),"Explode",nameof(GrenadeExploding),false);Patch(typeof(DetonatingProjectile),"ExplodeSound",nameof(GrenadeExploding),false);}
            catch(Exception ex){Bootstrap.Warn("GRENADE quiet fuse unavailable (the thrown grenade ticks): "+ex.Message);}
            // 0.1.126: walking over a weapon takes its ammunition only.
            try{Patch(typeof(PickableItem),"BeginInteractionCheckAndPickUp",nameof(WalkOverPickup),false);}
            catch(Exception ex){Bootstrap.Warn("WALK OVER ammunition-only pickup unavailable (the game picks weapons up): "+ex.Message);}
            // 0.1.128: the weapon pickup itself.
            try{Patch(typeof(WeaponPickup),"TryPickupItem",nameof(WalkOverTry),false);Patch(typeof(WeaponPickup),"PickupItem",nameof(WalkOverPick),false);Patch(typeof(WeaponPickup),"AddItem",nameof(WeaponGiven),true);}
            catch(Exception ex){Bootstrap.Warn("WALK OVER weapon pickup hook unavailable: "+ex.Message);}
            patches.Patch(AccessTools.DeclaredMethod(typeof(PlayerEquipableInventory),"TrySelectSlot"),prefix:new HarmonyMethod(typeof(WeaponHands),nameof(SelectAwayFromProp)),postfix:new HarmonyMethod(typeof(WeaponHands),nameof(SelectedAwayFromProp)));
            Current = this;
            try{npcHits=new NpcHitReactions();}catch(Exception ex){npcHits=null;Bootstrap.Warn("NPC HITS unavailable (punches still hit): "+ex.Message);}
            // 0.1.141: a weapon thrown into an NPC hits it.
            HolsterCopy.StruckNpc=(collider,hit,velocity,profile)=>punches.Thrown(collider,hit,velocity,profile,rig.PlayerRoot);
            try { muzzleEffects=new MuzzleEffects(); }
            catch(Exception ex) { Bootstrap.Warn("MUZZLE FX routing unavailable; tracked weapons continue. "+ex.Message); }
            Bootstrap.Write("WEAPON HANDS 0.1.44 ready; enabled=" + enabled + ". F8 toggles. Render-only weapon copy; Trigger=fire; right B=tap eject/hold grab; left grip=magazine/rounds (at the belt), bolt, shotgun pump after every shot; hold right A=wheel; left stick=select; left grip+X=Escape; left grip near fore-end=two hands.");
        }
        catch { patches.UnpatchSelf(); throw; }
    }
    private static bool TutorialButton(TutorialController __instance,InputActions buttonActions,ref string __result)
    {
        try
        {
            if(CameraRig.Current==null)return true;
            bool ru=(I2.Loc.LocalizationManager.CurrentLanguageCode??"").StartsWith("ru",StringComparison.OrdinalIgnoreCase);
            var label=VrPromptLabels.TutorialLabel(__instance,buttonActions,ru);if(label.Length==0)return true;__result=label;return false;
        }catch{return true;}
    }
    private static TutorialController? tutorials;
    private static bool TutorialShowing(TutorialController? c,string? term)=>c!=null&&!string.IsNullOrEmpty(term)&&c.lastCachedTerm==term&&c.group!=null&&c.group.activeInHierarchy;
    private static bool TutorialShow(TutorialController __instance,string term)
    {
        try
        {
            if(CameraRig.Current==null)return true;
            if(__instance!=null)tutorials=__instance;
            return TutorialOnce.Allow(term,TutorialShowing(__instance,term));
        }
        catch{return true;}
    }
    private static bool TutorialStep(EventReceiver.TutorialShowEvent __instance)
    {
        try
        {
            if(CameraRig.Current==null||__instance==null)return true;
            string? a=__instance.tutorialTerm?.term,b=null,c=null;
            if(__instance.hasControllerSpecificTerm){b=__instance.tutorialTermControllerDefault?.term;c=__instance.tutorialTermControllerAlternative?.term;}
            bool showing=false;try{var t=tutorials;showing=t!=null&&(TutorialShowing(t,a)||TutorialShowing(t,b)||TutorialShowing(t,c));}catch(Exception){tutorials=null;}
            bool allow=TutorialOnce.AllowStep(new[]{a,b,c},__instance.isWeaponWheelTutorial,showing);
            // 0.1.214: its hint, written next, names right A (WheelTutorial).
            if(allow&&__instance.isWeaponWheelTutorial)WheelTutorial.Started();
            return allow;
        }
        catch{return true;}
    }
    private static void PromptLabel(ButtonPrompt __instance){try{VrPromptLabels.Apply(__instance);}catch{}}
    private int Map(InputActions action, bool left, ulong bit)
    {
        int id = GameInputManager.InputActionToRewiredID(action);
        if (id < 0 || buttons.ContainsKey(id)) throw new InvalidOperationException("Unusable game input mapping for " + action + ": " + id);
        buttons.Add(id, (left, bit));
        Bootstrap.Write("WEAPON INPUT action=" + action + " rewired=" + id);
        return id;
    }
    private void Patch(Type type, string method, string hook, bool postfix)
    {
        var original = AccessTools.DeclaredMethod(type, method) ?? throw new MissingMethodException(type.FullName, method);
        var patch = new HarmonyMethod(typeof(WeaponHands), hook);
        patches.Patch(original, prefix: postfix ? null : patch, postfix: postfix ? patch : null);
    }
    internal void Toggle()
    {
        enabled = !enabled; rig.DisarmTrigger();
        if (!enabled) Unbind();
        nextDiscover = 0;
        Bootstrap.Write("WEAPON HANDS enabled=" + enabled);
    }
    // 0.1.162: which step of a long Tick took the time (PERF SLOW weapons).
    private readonly StepClock tickClock=new("weapons");
    // 0.1.248: the aim dots (VR SETTINGS "Aim dot"): the game's weapon's, the second pistol's.
    private readonly AimDot gunDot=new(),leftGunDot=new();
    internal void Tick()
    {
        if(disposed)return;
        gunDot.HideUnlessShown();leftGunDot.HideUnlessShown();
        var clock=tickClock;clock.Begin();
        try
        {
            LoadGrips();SaveGrips();clock.Mark("grips");
            var root=rig.PlayerRoot;int identity=root==null?0:root.GetInstanceID();
            if(identity!=boundRootId){OnSceneChanged();FindPlayer();clock.Mark("player");}
            if(failed){if(Time.realtimeSinceStartup<nextDiscover)return;OnSceneChanged();FindPlayer();}
            if(Time.realtimeSinceStartup>=nextDiscover)
            {
                nextDiscover=Time.realtimeSinceStartup+.5f;FindPlayer();ReportGate();clock.Mark("find player");
            }
            // Bind or discard old-scene objects before touching reload effects.
            muzzleEffects?.Tick();clock.Mark("muzzle effects");TickReloadProps();clock.Mark("reload props");
            UpdateSelection();clock.Mark("selection");
            // 0.1.141: an enemy's gun grabbed by the barrel (before the holsters take anything).
            if(CanControl(playerId)){npcHits?.TickGrab(rig);npcHits?.TickBrawl(rig);}else npcHits?.SuspendBrawls();
            npcHits?.TickHandPickup();clock.Mark("npc hits");
            SampleSwings();clock.Mark("swings");
            KeepWeaponOffTool();
            TickHolsters();clock.Mark("holsters");
            punches.Tick(rig,inventory,CanControl(playerId));clock.Mark("punches");
            if (!CanControl(playerId)) { HideChest(); SuspendPose(); TickLanding(false); PutRocketBack("");HideBazooka(); return; }
            if (fireOwned && (!TriggerControls.Valid || (TriggerControls.Held & HandControls.Trigger) == 0)) StopOwnedFire();
            if(secondaryOwned&&(Underbarrel?!SupportControls.Valid||(SupportControls.Held&HandControls.Trigger)==0||!SupportHeld
                :!rig.RightControls.Valid||(rig.RightControls.Held&HandControls.Stick)==0||(rig.LeftControls.Held&HandControls.Grip)!=0))StopSecondary();
            RenderPose();clock.Mark("pose");
            // 0.1.103: R3 on the SVD changes the VR scope's zoom (the native
            // secondary action — the 2D scope overlay — is not used in VR).
            // 0.1.117: the crossbow too, once its VR scope exists.
            if((profile=="sniper"||profile=="crossbow"&&visual?.HasScope==true)&&visual!=null&&rig.RightControls.Valid&&(rig.RightControls.Down&HandControls.Stick)!=0&&!LockStick.Taken(true))
            {visual.CycleScopeZoom();rig.PunchHaptics(true);reloadAudio??=new ReloadAudio();reloadAudio.PlayCue("sniper",9);}
            TickReload();clock.Mark("reload");TickStall();TickRedraw();TickRevolver();TickLeftCopyReload();TickDual();clock.Mark("dual");TickPropThrow();TickKnifeThrow();TickGrenade();TickGrenadeAfter();TickLanding(true);TickBazooka();clock.Mark("throws");TickHandReload();TickChestReload();TickCopySound();clock.Mark("copies");
        }
        catch (Exception ex) { Fail(ex); }
        finally { clock.End(); }
    }
    private void FindPlayer()
    {
        var root = rig.PlayerRoot;
        boundRootId=root==null?0:root.GetInstanceID();
        if(root!=playerRoot||(root==null&&pouch!=null))
        {
            pouch?.Dispose();pouch=null;drops?.Dispose();drops=null;
            rightNative=null;leftNative=null;gripFrame=poseFrame=-1;
            Array.Clear(gripValid,0,2);Array.Clear(gripSampled,0,2);
        }
        if (root == null) { Unbind(); visuals.Dispose(); reloadStates.Clear(); ClearHolsters(); inventory = null; handler = null; playerRoot = null; playerId = -1; return; }
        if (root == playerRoot && inventory != null && handler != null) return;
        Unbind();visuals.Dispose();reloadStates.Clear();ClearHolsters(); playerRoot = root; playerId = -1;
        inventory = root.GetComponent(Il2CppType.Of<PlayerEquipableInventory>())?.TryCast<PlayerEquipableInventory>();
        if(inventory==null)foreach(var component in root.GetComponentsInChildren(Il2CppType.Of<PlayerEquipableInventory>(),true)){inventory=component.TryCast<PlayerEquipableInventory>();if(inventory!=null)break;}
        handler = root.GetComponent(Il2CppType.Of<PlayerEquipableHandler>())?.TryCast<PlayerEquipableHandler>();
        if(handler==null)foreach(var component in root.GetComponentsInChildren(Il2CppType.Of<PlayerEquipableHandler>(),true)){handler=component.TryCast<PlayerEquipableHandler>();if(handler!=null)break;}
        if (inventory != null && handler != null)
        {
            var owner = handler.GetOwner();
            if (!owner.IsPlayer || owner.IsInvalid) { handler = null; inventory = null; return; }
            playerId = owner.Id;
            StopIdleFlourish();
            Bootstrap.Write("WEAPON PLAYER bound id=" + playerId + " root=" + root.name);
        }
    }
    private void UpdateSelection()
    {
        var selected = inventory == null || !enabled ? null : inventory.currentEquipable;
        if (weapon != null && selected != null && weapon.Pointer == selected.Pointer && selected.gameObject.activeInHierarchy) return;
        // 0.1.127: drawn again after a hand reload: the gun stays in the hand meanwhile.
        if (Redrawing) return;
        if (weapon != null || fire != null) Unbind();
        if (selected == null || !selected.gameObject.activeInHierarchy || inventory!.isInTransit) return;
        if (selected.GetInstanceID() != seenWeaponId)
        { seenWeaponId = selected.GetInstanceID(); FirstPersonVisibility.ScanSoon(); Bootstrap.Write("WEAPON SELECTED id=" + selected.identifier + " slot=" + selected.slot + " name=" + selected.name); }
        profile = EquipmentProfile.ForSlot((int)selected.slot);
        if (profile.Length == 0) return;
        if (!selected.isEquipableSetUp || playerRoot == null || !selected.transform.IsChildOf(playerRoot))
        { BindWaiting("setup=" + selected.isEquipableSetUp + ", waiting for local player weapon hierarchy"); return; }
        var script = selected.primaryFireUsageComponentScript;
        var candidate = script == null ? null : script.TryCast<FireComponent>();
        // Pistol primary usage can be a DualWield wrapper. Bind the actual
        // single/spread/automatic muzzle while leaving input dispatch to the game.
        if (candidate == null || candidate.TryCast<DualWieldComponent>() != null || candidate.projectileOrigin == null)
        {
            Type actual = profile == "pistol" ? typeof(SingleFireComponent) : profile == "shotgun" ? typeof(SpreadFireComponent) : typeof(AutoFireComponent);
            candidate = selected.GetComponent(Il2CppType.From(actual))?.TryCast<FireComponent>();
        }
        if(candidate==null||candidate.projectileOrigin==null)
        {
            foreach(var c in selected.GetComponentsInChildren(Il2CppType.Of<FireComponent>(),true))
            {var f=c.TryCast<FireComponent>();if(f!=null&&f.TryCast<DualWieldComponent>()==null&&f.projectileOrigin!=null){candidate=f;break;}}
        }
        var origin=profile=="prop"?selected.transform:candidate?.projectileOrigin;
        if(origin==null&&EquipmentProfile.RequiresMuzzle(profile))
        {BindWaiting("no valid fire muzzle for slot="+selected.slot);return;}
        // Non-fire equipment still receives a tracked render copy and native use input.
        if(origin==null)origin=selected.transform;
        if(!origin.IsChildOf(selected.transform)&&origin!=selected.transform)
        {BindWaiting("muzzle outside selected equipment");return;}
        if (Time.realtimeSinceStartup < nextVisualAttempt) return;
        long buildTimer=FramePerformance.Begin();
        try
        {
            visual=visuals.Get(selected.GetInstanceID(),v=>v.Matches(selected,origin),
                ()=>WeaponVisual.Create(selected,origin,profile),out bool reused);
            // 0.1.122: whether the crossbow was loaded when its visual was built
            // (its bolt's place on the rail is read from that pose).
            if(!reused){try{visual.LoadedAtBuild=(candidate?.ammoManagementComponent?.PrimaryMagazineAmmoCount??1)>0;}catch(Exception){visual.LoadedAtBuild=false;}}
            visual.Resume(selected);visual.PinPulled=false;poseCallFrame=-1;
            FramePerformance.Selection(reused,buildTimer,profile);
        }
        catch (Exception ex)
        {
            nextVisualAttempt = Time.realtimeSinceStartup + .35f;
            BindWaiting("render-only mesh unavailable: " + ex.Message);
            return; // Weapon selection and locomotion must remain usable.
        }
        weapon = selected; fire = candidate; ammo=candidate?.ammoManagementComponent; muzzle = origin;reportedRightGrip=reportedLeftGrip=false;gripFrame=-1;nativeRenderFrame=-1;
        if(visual.Exists)muzzleOffsets[profile]=visual.MuzzleOffset;
        if(visual.Exists&&HolsterLayout.Firearm(profile))_=visual.MirrorCenterX;
        BindDual();BindReload();StopIdleFlourish();ReloadAudioCatalog.Record(selected,profile);
        rig.DisarmTrigger(); ReleaseSupport();
        Bootstrap.Write("WEAPON BOUND profile=" + profile + " fire=" + (candidate==null?"native-use":candidate.GetIl2CppType().FullName) + " muzzle=" + muzzle.name + " renderOnlyCopy=" + visual.Exists);
    }
    private void BindWaiting(string reason)
    {
        if (Time.realtimeSinceStartup < nextBindReport) return;
        nextBindReport = Time.realtimeSinceStartup + 5;
        Bootstrap.Write("WEAPON BIND waiting: " + profile + "; " + reason);
    }
    internal void OnSceneChanged()
    {
        // Scene destruction can invalidate a snapshot before ordinary Unbind.
        // Isolate each owned resource, then clear bindings even if one restore fails.
        void Cleanup(Action action){try{action();}catch(Exception ex){Bootstrap.Warn("WEAPON scene cleanup: "+ex.Message);}}
        Cleanup(Unbind);Cleanup(visuals.Dispose);Cleanup(()=>drops?.Dispose());Cleanup(()=>pouch?.Dispose());Cleanup(ClearHolsters);
        drops=null;pouch=null;visual=null;weapon=null;fire=null;muzzle=null;ammo=null;consumedProp=null;breakingProp=false;
        handler=null;inventory=null;playerRoot=null;playerId=-1;boundRootId=0;
        reloadStates.Clear();reload=new ManualReloadState();lastCommandFrame.Clear();
        rightNative=null;leftNative=null;Array.Clear(gripValid,0,2);Array.Clear(gripSampled,0,2);
        punches.Reset();failed=false;fireOwned=false;nextDiscover=nextVisualAttempt=nextBindReport=0;
        seenWeaponId=0;lastGate="";profile="";InvalidateRenderPose();rig.DisarmTrigger();
        Bootstrap.Write("WEAPON scene bindings reset; waiting for local inventory");
    }
    private void ReportGate()
    {
        string gate=failed?"failed":!enabled?"disabled":playerId<0||handler==null||inventory==null?"waiting-player"
            :rig.Scripted?"story":GameUiControls.Current?.PendingConsumable==true?"held-medkit"
            :GameUiControls.Current?.BlocksGameplay==true?"UI":!rig.HeadTrackingValid?"tracking"
            :!WindowFocus.Playable?"focus":Time.timeScale<=0||PauseMenuControl.HackGameIsPaused?"pause"
            :GameInputManager.IsInputLocked(playerId)?"native-input-lock":"ready";
        if(gate==lastGate)return;lastGate=gate;
        Bootstrap.Write("COMBAT GATE "+gate+" player="+playerId+" root="+boundRootId+" selected="+(inventory==null||inventory.currentEquipable==null?"none":inventory.currentEquipable.identifier)+" transit="+(inventory!=null&&inventory.isInTransit));
    }
    private bool CanControl(int id)
    {
        return !disposed && !failed && enabled && !PropConsumed && InteractionDriver.Current?.KeyActive!=true && GameUiControls.Current?.PendingConsumable!=true && id == playerId && playerId >= 0 && handler != null && inventory != null
            && rig.HeadTrackingValid && !rig.Scripted && GameUiControls.Current?.BlocksGameplay!=true && WindowFocus.Playable && Time.timeScale > 0 && !PauseMenuControl.HackGameIsPaused
            && !GameInputManager.IsInputLocked(playerId);
    }
    internal bool TryGetEffectMuzzle(Equipable effectWeapon,out Transform anchor)
    {
        anchor=null!;
        // 0.1.130: the flash of a weapon firing by itself in the other hand.
        if(copyShooting&&copyShotWeapon!=null&&effectWeapon!=null&&effectWeapon.Pointer==copyShotWeapon.Pointer&&copyShotAnchor!=null){anchor=copyShotAnchor;return true;}
        if(leftShooting&&DualActive&&leftPoseValid&&leftVisual?.MuzzleAnchor!=null){anchor=leftVisual.MuzzleAnchor;return true;}
        if(leftWeapon!=null&&effectWeapon!=null&&effectWeapon.Pointer==leftWeapon.Pointer&&leftPoseValid&&leftVisual?.MuzzleAnchor!=null){anchor=leftVisual.MuzzleAnchor;return true;}
        if(effectWeapon==null || weapon==null || effectWeapon.Pointer!=weapon.Pointer || !CanControl(playerId)) return false;
        RenderPose();
        if(!poseValid || visual==null || visual.MuzzleAnchor==null) return false;
        anchor=visual.MuzzleAnchor; return true;
    }
    internal bool TryMeleeTip(out U tip)
    {RenderPose();tip=desiredMeleePosition+desiredMeleeRotation*(visual?.MuzzleOffset??U.zero);return poseValid;}
    // 0.1.130: the hand swinging a weapon hits with its far end: the game's
    // weapon (by its stock when it hangs by the fore-end) or a copy in a hand.
    internal bool TryMeleeTip(bool right,out U tip)
    {
        int s=right?1:0;
        if(copyKey[s]>=0){var c=holsters?.CopyOf(copyKey[s]);tip=c==null?U.zero:copyForeEnd[s]?c.RearWorld:c.FrontWorld;return c!=null&&c.Shown;}
        if(!right&&LeftPistolVisible){RenderPose();tip=leftDesiredTip;return leftPoseValid;}
        RenderPose();tip=GameMeleeTip();return poseValid;
    }
    internal U SafeMeleeTip(bool right)
    {
        int s=right?1:0;
        if(copyKey[s]>=0){var c=holsters?.CopyOf(copyKey[s]);if(c!=null)return copyForeEnd[s]?c.RearWorld:c.FrontWorld;}
        if(!right&&LeftPistolVisible)return leftAimPosition;
        return foreEndOnly?GameMeleeTip():aimPosition;
    }
    // 0.1.196: a club hits with its stock's end.
    private U GameMeleeTip()=>desiredMeleePosition+desiredMeleeRotation*(Clubbed?ClubStockEnd:foreEndOnly?new U(0,0,-.22f):visual?.MuzzleOffset??U.zero);
    internal bool TryButtonContacts(bool right,out U palm,out U tip)
    {
        RenderPose();palm=tip=U.zero;
        if(!right)
        {
            if(!LeftPistolVisible||!leftPoseValid)return false;
            palm=leftHandPosition+leftHandRotation*new U(0,-.008f,.03f);tip=leftAimPosition;return true;
        }
        if(!poseValid||visual==null||!gripValid[1])return false;
        var frame=visual.FittedToWorld;
        palm=frame.MultiplyPoint3x4(gripPositions[1])+frame.rotation*gripRotations[1]*new U(0,-.008f,.03f);
        tip=aimPosition;return true;
    }
    internal U MeleeSafeTip=>aimPosition;
    // 0.1.172: a gun hit only with its muzzle (a 5 cm point); now its whole shape
    // hits, as a thing's does: the stock, the grip, the receiver, the barrel.
    // 0.1.202: the guns
    // whose collisions are hand-made (pistol, AK, shotgun, SVD) have no mesh
    // shape, so they hit only with one point (the muzzle; held by the barrel:
    // the stock's end). Their hand-made shape hits whole (the barrel, the
    // stock), as 0.1.172 meant.
    internal ContactSphere[]? MeleeShape=>profile=="prop"||HolsterLayout.Firearm(profile)?visual?.ActiveMeleeShape??(HolsterLayout.Firearm(profile)?HandMadeShape(profile):null):null;
    private static readonly System.Collections.Generic.Dictionary<string,ContactSphere[]> handMadeShapes=new();
    private static ContactSphere[] HandMadeShape(string p){if(!handMadeShapes.TryGetValue(p,out var shape))handMadeShapes[p]=shape=ContactSolver.Weapon(p);return shape;}
    // 0.1.172: a weapon copy swung in a hand hits with its other end too (the stock when held by the handle).
    internal bool TryMeleeOtherEnd(bool right,out U end)
    {
        int s=right?1:0;end=U.zero;
        if(copyKey[s]<0)return false;var c=holsters?.CopyOf(copyKey[s]);if(c==null||!c.Shown)return false;
        end=copyForeEnd[s]?c.FrontWorld:c.RearWorld;return true;
    }
    // 0.1.152: a long stick (broom, shovel): its swing is timed by its end (PunchDriver).
    internal bool LongStick=>profile=="prop"&&visual?.LongGripOrigin!=null||Clubbed;
    internal bool TryLongStickEnd(out U end)
    {
        end=U.zero;
        // 0.1.196: a gun held by its barrel is swung by its stock's end.
        if(Clubbed){RenderPose();end=desiredMeleePosition+desiredMeleeRotation*ClubStockEnd;return poseValid;}
        if(profile!="prop"||visual?.LongHeadEnd is not U head)return false;
        if(visual.GeometryMirrored)head.x=-head.x;
        RenderPose();end=desiredMeleePosition+desiredMeleeRotation*head;return poseValid;
    }
    // 0.1.150: this hand swings the game's own thing (not a copy): its whole shape hits.
    // 0.1.196: or the hand holding it by its barrel (a club).
    internal bool GameMeleeHand(bool right){int s=right?1:0;if(weapon==null||copyKey[s]>=0)return false;int g=GameSide(CurrentKey);return Clubbed?g==1-s:!foreEndOnly&&g==s;}
    internal bool TryMeleePose(out U desired,out Q rotation,out U safe,out Q safeRotation)
    {RenderPose();desired=desiredMeleePosition;rotation=desiredMeleeRotation;safe=visual==null?desired:visual.FittedToWorld.MultiplyPoint3x4(U.zero);safeRotation=visual==null?rotation:visual.FittedToWorld.rotation;return poseValid;}
    internal void NativeFlashShown(Transform anchor) { if(copyShotAnchor!=null&&anchor==copyShotAnchor)return;if(leftVisual?.MuzzleAnchor==anchor)leftVisual.NativeFlashShown();else visual?.NativeFlashShown(); }
    private int holsterFrame=-1;
    internal void RenderPose(bool rendering=false)
    {
        // 0.1.124: the weapons on the body, just before drawing.
        if(rendering&&holsterFrame!=Time.frameCount&&!disposed&&!failed&&enabled){holsterFrame=Time.frameCount;RenderHolsters();}
        if (disposed || failed || syncing || !enabled || weapon == null || muzzle == null || visual == null) return;
        int callFrame=Time.frameCount;
        if(callFrame==poseCallFrame&&(!rendering||poseCallRendering))return;
        poseCallFrame=callFrame;poseCallRendering=rendering;
        syncing = true;
        long performanceStart=FramePerformance.Begin();
        try
        {
            if (inventory == null || !Redrawing && (inventory.isInTransit || inventory.currentEquipable == null || inventory.currentEquipable.Pointer != weapon.Pointer
                || !weapon.gameObject.activeInHierarchy) || !CanControl(playerId) || HolsterHidesCurrent
                || !rig.SampleWorldHands(out var left, out var right, out bool leftValid))
            { SuspendPose(); return; }
            if(rendering && nativeRenderFrame!=Time.frameCount)
            {
                nativeRenderFrame=Time.frameCount;gripFrame=-1;
                leftNative?.InvalidateSample();rightNative?.InvalidateSample();
            }
            // 0.1.128: the game's weapon may be in the left hand (held like the
            // right hand, mirrored; the right hand then holds the fore-end).
            bool mirrored=PrimaryLeft;
            if(mirrored&&!leftValid){SuspendPose();return;}
            var primaryPose=mirrored?left:right;var supportPose=mirrored?right:left;
            bool supportValid=mirrored||leftValid;var supportControls=SupportControls;
            // 0.1.160: a two-handed gun turned toward the other hand (GunAim).
            Q hand = GunAim(primaryPose,!mirrored,profile);
            U position = CameraRig.UnityPosition(primaryPose);
            // Grip origins and the rendered hand use the same authored skeleton
            // and weapon fit. Keep the dominant hand at the tracked controller;
            // move the gun around its handle instead of moving the hand to a guess.
            U primaryGrip=PrimaryHandPoint();
            U supportGrip=Sided(NativeGrip(false,(profile=="pistol"||profile=="revolver")?new U(-.04f,-.015f,.025f):new U(0,-.035f,.26f)));
            // 0.1.186: nor the hand holding a medkit taken from a forearm.
            if(!mirrored&&GripCarry.Current?.HidesLeft==true||copyKey[mirrored?1:0]>=0||GameUiControls.Current?.Items.ArmHeld(mirrored)==true||GameUiControls.Current?.ItemHeldOn(mirrored)==true){supportValid=false;ReleaseSupport();}
            // Authored props can be held sideways. Orient the item around its
            // native wrist so the visible hand follows the tracked controller.
            // The throwing knife's native pinch is authored for the flat-screen
            // arm (fingers sideways); keep the visible hand on the controller too.
            // 0.1.117: the same for hand grenades — the native arm holds the
            // grenade sideways, so aligning the grenade to the controller
            // turned the whole forearm 90 degrees.
            if(!mirrored&&HandLedGrip(profile)&&gripValid[1])hand=GloveVisual.Rotation(right,true)*Q.Inverse(gripRotations[1]);
            // 0.1.150: a thing in the left hand (a left-hander): the right hand's hold mirrored.
            if(mirrored&&HandLedGrip(profile)&&gripValid[1])hand=GloveVisual.Rotation(left,false)*Q.Inverse(MirrorQ(gripRotations[1]));
            U socket = position + hand * (supportGrip-primaryGrip);
            if(poseValid&&QualityOptions.Collisions.Value)socket=visual.FittedToWorld.MultiplyPoint3x4(supportGrip);
            lastSocket=socket;
            // 0.1.196: a grip at the barrel's front holds the barrel (a club when the handle is let go), not the fore-end.
            bool barrelDown=(supportControls.Down&HandControls.Grip)!=0&&supportValid&&BarrelGrab(CameraRig.UnityPosition(supportPose),socket,out _);
            if (aimRevision!=ControllerAim.Revision) { ReleaseSupport(); aimRevision=ControllerAim.Revision; }
            bool wasHeld = SupportHeld;
            bool manualPump=ManualReady&&profile=="shotgun"&&reload.Racking;
            Q aim;
            if(Clubbed&&PoseClub(supportPose,mirrored,out var clubOrigin,out var clubTurn))
            {
                // 0.1.196: held by its barrel in the other hand (a club).
                aim=clubTurn;position=clubOrigin+aim*primaryGrip;
                weightedRotation=aim;inertia.Reset();scopeSteady.Reset();inertiaFrame=Time.frameCount;
            }
            else if(foreEndOnly)
            {
                // 0.1.128: hanging by its fore-end in the other hand.
                var held=HandMirror.Apply(ToN(CameraRig.UnityPosition(supportPose)),ToN(CameraRig.UnityRotation(supportPose)),ToN(foreEndOffset),ToN(foreEndRotation));
                aim=new Q(held.rotation.X,held.rotation.Y,held.rotation.Z,held.rotation.W);
                position=new U(held.position.X,held.position.Y,held.position.Z)+aim*primaryGrip;
                // 0.1.136: pumped with one hand, the pump stays in the hand and the gun slides.
                if(profile=="shotgun"&&ManualReady)position+=aim*U.forward*reload.VisualRackTravel;
                weightedRotation=aim;inertia.Reset();scopeSteady.Reset();inertiaFrame=Time.frameCount;
            }
            else if(DualActive||(!mirrored||LeftRevolverManual)&&RevolverReady&&revolver.Open||ManualReady&&reload.Active&&(profile!="shotgun"||reload.Holding)){ReleaseSupport();aim=hand;}
            else if(profile=="pistol"||profile=="revolver")
            {
                // Render/fire hooks can execute several times per frame. Filter
                // once per frame so smoothing does not depend on eye/pass count.
                if(poseFrame!=Time.frameCount)
                {
                    poseFrame=Time.frameCount;
                    var stable=pistolSupport.Solve(primaryPose.Position,ToN(hand),supportPose.Position,ToN(socket),supportValid && supportControls.Valid,
                        (supportControls.Down & HandControls.Grip)!=0,(supportControls.Held & HandControls.Grip)!=0,
                        WeaponOptions.GripRadius.Value,Time.unscaledDeltaTime);
                    pistolAim=new Q(stable.X,stable.Y,stable.Z,stable.W);
                }
                aim=pistolAim;
            }
            else
            {
                var solved = grip.Solve(primaryPose.Position, ToN(hand), supportPose.Position, ToN(socket), supportValid && supportControls.Valid,
                    ((supportControls.Down & HandControls.Grip) != 0 && !barrelDown || manualPump), (supportControls.Held & HandControls.Grip) != 0,
                    Math.Clamp(WeaponOptions.GripRadius.Value, 0.05f, 0.3f));
                aim = new Q(solved.X,solved.Y,solved.Z,solved.W);
            }
            if(feedbackFrame!=Time.frameCount)
            {
                // All shotgun pellets in the same frame use the pre-shot pose.
                // The recoil from their shared discharge is applied next frame.
                feedbackFrame=Time.frameCount; feedback.Advance(Time.realtimeSinceStartup);
                visualKick=feedback.Kickback; visualPitch=feedback.Pitch;
                visualFlash=feedback.Sequence!=visualShotSequence || Time.realtimeSinceStartup<feedback.FlashUntil;
                visualShotSequence=feedback.Sequence;
            }
            if(inertiaFrame!=Time.frameCount)
            {
                inertiaFrame=Time.frameCount;
                var weighted=inertia.Step(ToN(position),ToN(aim),Time.unscaledDeltaTime,profile,SupportHeld,WeaponOptions.InertiaStrength.Value);
                // 0.1.207:
                // while an eye is at the scope the aim (the picture, the reticle
                // and where the shot goes) is steadied (ScopeSteadyMath).
                bool atEye=EquipmentProfile.Scoped(profile)&&visual.ScopeViewing;float steadiness=1;
                try{steadiness=WeaponOptions.ScopeSteadiness.Value;}catch(Exception){}
                var steadied=scopeSteady.Step(weighted.rotation,Time.unscaledDeltaTime,atEye,steadiness);
                weightedRotation=new Q(steadied.X,steadied.Y,steadied.Z,steadied.W);
                if(atEye!=steadyAtEye&&(atEye||Time.realtimeSinceStartup>=nextSteadyReport))
                {
                    steadyAtEye=atEye;
                    if(atEye){nextSteadyReport=Time.realtimeSinceStartup+10;Bootstrap.Write("SCOPE "+profile+" at the eye: the aim steadied as if holding the breath (strength "+steadiness.ToString("F1")+"; [Weapons] ScopeSteadiness)");}
                }
            }
            // Position is the latest tracking/body sample, including pre-render.
            // Never reuse an Update-time world position after locomotion.
            aim=weightedRotation;
            U gripPosition=position-aim*primaryGrip;
            position=gripPosition;
            position-=aim*U.forward*visualKick;
            aim*=Q.Euler(-visualPitch,0,0);
            desiredMeleePosition=position;desiredMeleeRotation=aim;
            bool pump=manualPump;
            bool clubbed=Clubbed;
            if(ContactRig.Current?.ResolveGun(profile,clubbed?ClubGrab:primaryGrip,pump?supportGrip-U.forward*reload.RackTravel:supportGrip,!clubbed&&(SupportHeld||pump||foreEndOnly),ref position,ref aim,visual.ActiveContactShape)==false)
            {visual.Hide();poseValid=false;return;}
            // 0.1.153: held in the left hand, the thing itself mirrored with the hand.
            visual.MirrorGeometry(mirrored&&HandLedGrip(profile));
            visual.Pose(position, aim,visualFlash,rendering);
            // 0.1.118: the thrown grenade is gone from the hand for a moment.
            if(profile=="grenade"&&(Time.realtimeSinceStartup<grenadeHiddenUntil||emptyAfterThrowAt>0))visual.Hide();
            // 0.1.149: the knife let go of is flying; the next one is not in the hand.
            if(profile=="knife"&&(Time.realtimeSinceStartup<knifeHiddenUntil||emptyAfterThrowAt>0&&emptyAfterThrowSlot==PlayerEquipableInventory.ActiveEquipmentSlot.Knife))visual.Hide();
            RenderReload(left);RenderRevolver(left,right);RenderLeftPistol(rendering);
            aimPosition = position + aim * visual.MuzzleOffset; aimForward = aim * U.forward; aimUp = aim * U.up;
            // A grenade leaves the hand along the controller, not along its
            // own model axis (which now follows the native, sideways hold).
            if(profile=="grenade"&&gripValid[1])
            {var c=ControllerAim.Rotation(right);aimForward=c*U.forward;aimUp=c*U.up;aimPosition=position+aim*primaryGrip+aimForward*.09f;}
            if(profile=="knife")KnifeAim(right);
            poseValid = true; if(fire!=null)WriteMuzzle(fire);
            if(rendering)ShowGunDot(gunDot,aimPosition,aimForward);
            if (wasHeld != SupportHeld) Bootstrap.Write("TWO-HAND " + (SupportHeld ? "engaged" : "released") + " weapon=" + profile);
            UpdateSocket(position + aim * supportGrip);
            if (Time.realtimeSinceStartup >= nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 5;
                Bootstrap.Write("WEAPON POSE " + profile + " twoHands=" + SupportHeld + " muzzle=" + aimPosition + " forward=" + aimForward);
            }
        }
        catch (Exception ex) { Fail(ex); }
        finally { syncing = false; FramePerformance.End(performanceStart,1); }
    }
    // Items whose native grip is authored sideways: the visible hand follows
    // the controller and the item is placed around the native wrist.
    internal static bool HandLedGrip(string profile)=>profile is "prop" or "knife" or "grenade";
    // 0.1.119: the M16's grenade launcher (the game's secondary fire).
    private bool Underbarrel=>profile=="m16";
    private int UnderbarrelCount(){try{return ammo==null?0:Math.Max(0,ammo.SecondaryMagazineAmmoCount)+Math.Max(0,ammo.SecondaryReserveAmmoCount);}catch(Exception){return 0;}}
    internal bool UnderbarrelGrenades(out int count){count=0;if(!Underbarrel||ammo==null||!HasTrackedWeapon)return false;count=UnderbarrelCount();return true;}
    private static N ToN(U v) => new(v.x,v.y,v.z);
    private static System.Numerics.Quaternion ToN(Q q) => new(q.x,q.y,q.z,q.w);
    private void WriteMuzzle(FireComponent component)
    {
        bool left=IsLeft(component)&&leftPoseValid;
        component._ProjectileOriginPositionAfterIK_k__BackingField = left?leftAimPosition:aimPosition;
        component._ProjectileOriginForwardAfterIK_k__BackingField = left?leftAimForward:aimForward;
        if(propThrow!=null&&component.Pointer==propThrow.Pointer&&(throwAiming||Time.realtimeSinceStartup<launchUntil)){component._ProjectileOriginPositionAfterIK_k__BackingField=throwOrigin;component._ProjectileOriginForwardAfterIK_k__BackingField=throwLaunch.normalized;}
        if(!left&&KnifeLaunching){component._ProjectileOriginPositionAfterIK_k__BackingField=knifeOrigin;component._ProjectileOriginForwardAfterIK_k__BackingField=knifeDirection;}
        component._ProjectileOriginUpAfterIK_k__BackingField = left?leftAimUp:aimUp;
    }
    private bool Owns(FireComponent component) => weapon != null && component != null && component.baseEquipable != null && (component.baseEquipable.Pointer == weapon.Pointer||IsLeft(component));
    private bool VirtualButton(int action, int id, int phase)
    {
        rig.PollControls();
        if (!CanControl(id) || !buttons.TryGetValue(action, out var binding)) return false;
        // 0.1.119: M16 underbarrel grenade launcher — the left trigger while the
        // left hand holds the fore-end (support grip). R3 no longer fires it.
        if(action==secondaryAction&&Underbarrel)
        {
            RenderPose();var l=SupportControls;
            if(!l.Valid||!SupportHeld||foreEndOnly||!poseValid||inventory!.isInTransit)return false;
            ulong gl=phase==0?l.Held:phase==1?l.Down:l.Up;bool fired=(gl&HandControls.Trigger)!=0;
            if(fired&&phase==1)Bootstrap.Write("UNDERBARREL fire ("+(PrimaryLeft?"right":"left")+" trigger on the fore-end) grenades="+UnderbarrelCount());
            if(fired)secondaryOwned=phase!=2;
            return fired;
        }
        if(action==secondaryAction && (rig.LeftControls.Held&HandControls.Grip)!=0)return false;
        // 0.1.215: R3 at a key, card or lockpick lock takes the item out (not the secondary fire).
        if(action==secondaryAction&&LockStick.Taken(true))return false;
        // 0.1.197: on the rope of a hook fired from the right hand, R3 lets go (not the secondary fire).
        if(action==secondaryAction&&GrappleVr.RopeRightHanded)return false;
        if((profile=="prop"&&propThrow!=null||GrenadeOwned)&&(action==primaryAction||action==secondaryAction))return false;
        if (action == primaryAction || action == secondaryAction) RenderPose();
        var state = binding.left ? rig.LeftControls : rig.RightControls;
        // 0.1.128: the game's weapon in the left hand fires on the left trigger
        // (once that trigger was let go in the left hand); not while it only
        // hangs by its fore-end.
        if(action==primaryAction&&PrimaryLeft)
        {
            state=rig.LeftControls;
            if(leftTriggerLocked){if(!state.Valid||(state.Held&HandControls.Trigger)==0)leftTriggerLocked=false;else return false;}
        }
        if(foreEndOnly&&(action==primaryAction||action==secondaryAction))return false;
        if (!state.Valid) return false;
        bool onMountedGun=false;
        if(action==primaryAction||action==secondaryAction)
        {
            if(inventory!.isInTransit)return false;
            var slot=inventory.currentEquipable?.slot;
            bool gadget=slot==PlayerEquipableInventory.ActiveEquipmentSlot.C4 || slot==PlayerEquipableInventory.ActiveEquipmentSlot.MicroSpy
                || slot==PlayerEquipableInventory.ActiveEquipmentSlot.Zipline || slot==PlayerEquipableInventory.ActiveEquipmentSlot.Gadget;
            bool nativeCarryAction=GripCarry.Current?.HoldingBody==true&&slot==PlayerEquipableInventory.ActiveEquipmentSlot.Fist;
            // 0.1.119: the mounted gun has no tracked VR copy; its trigger is the game's.
            bool mountedGun=slot==PlayerEquipableInventory.ActiveEquipmentSlot.MountedWeapon;onMountedGun=mountedGun;
            if(!gadget && !nativeCarryAction && !mountedGun && (!poseValid || weapon==null))return false;
        }
        ulong bits = phase == 0 ? state.Held : phase == 1 ? state.Down : state.Up;
        bool pressed = (bits & binding.bit) != 0;
        // 0.1.124: the stationary machine gun fires with both triggers.
        if(onMountedGun&&action==primaryAction)pressed=MountedTriggers.Pressed(rig.LeftControls,rig.RightControls,phase);
        if (pressed && phase == 1 && (!lastCommandFrame.TryGetValue(action,out int frame) || frame != Time.frameCount))
        {
            lastCommandFrame[action] = Time.frameCount;
            Bootstrap.Write("WEAPON COMMAND rewired=" + action + " player=" + id + " selected=" + profile + " pose=" + poseValid);
        }
        // 0.1.123: a loaded gun the game still holds back after a hand reload.
        if (action == primaryAction && pressed && phase == 1 && ManualReady && (!PrimaryLeft||MirroredManual) && !reload.BlocksFire && !Redrawing) { ReleaseNativeFire("trigger"); NotePress(); }
        // 0.1.128: a gun in the left hand reloads by itself when empty
        // (0.1.133: unless the right hand reloads it by hand).
        if (action == primaryAction && pressed && phase == 1 && PrimaryLeft && !MirroredManual && !LeftRevolverManual) LeftHandReload();
        if (action == primaryAction && pressed && phase != 2) fireOwned = true;
        if (action == primaryAction && pressed && phase == 2) fireOwned = false;
        if(action==secondaryAction&&pressed)secondaryOwned=phase!=2;
        return pressed;
    }
    private static bool SurfaceSound(MeleeComponent __instance,string fmodEvent)
    {
        if(!PropImpactAudio.AllowNativeSound(__instance))return false;
        var selected=Current?.inventory?.currentEquipable;
        if(selected!=null&&__instance.baseEquipable!=null&&selected.Pointer==__instance.baseEquipable.Pointer)
            Bootstrap.Write("PUNCH SURFACE SOUND event="+fmodEvent);
        return true;
    }
    private static bool AllowButtonMelee(MeleeComponent __instance)
    {
        var c=Current;var selected=c?.inventory?.currentEquipable;
        return c==null||GripCarry.Current?.HoldingBody==true||!WeaponOptions.PhysicalPunches.Value||!c.CanControl(c.playerId)||selected==null
            ||(selected.slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Fist&&selected.slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental&&selected.slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Knife)||__instance.baseEquipable==null
            ||__instance.baseEquipable.Pointer!=selected.Pointer;
    }
    private static void Inject(int button, int playerID, int phase, ref bool result)
    {
        var current = Current; if (current == null) return;
        try
        {
            // 0.1.121: the game polls every button of every action each frame.
            // Only the weapon's own actions and the VR-bound buttons matter;
            // the control check (many native calls) runs once per poll.
            if(button!=current.primaryAction&&button!=current.secondaryAction&&button!=current.reloadAction&&!current.buttons.ContainsKey(button))return;
            bool can=current.CanControl(playerID);
            // 0.1.118: the grenade is thrown by hand (pin + swing), not by the button.
            if(can&&(current.profile=="prop"&&current.propThrow!=null||current.GrenadeOwned)&&(button==current.primaryAction||button==current.secondaryAction)){result=false;return;}
            // Knife: the physical trigger only drives the throw gesture; the
            // native throw is pressed once, on a valid release.
            if(can&&current.profile=="knife"&&button==current.primaryAction){result=current.KnifeButton(phase);return;}
            if(can&&(current.profile=="sniper"||current.profile=="crossbow"&&current.visual?.HasScope==true)&&button==current.secondaryAction){result=false;return;}
            if(can&&current.DualActive&&(button==current.primaryAction||button==current.reloadAction)){result=false;return;}
            // 0.1.131: the right B reloads the left hand's gun (or a weapon held
            // in the right hand while the game's weapon is in the left) by itself.
            // 0.1.133: with the manual reload the right B is the magazine
            // release of the left hand's gun (the right hand reloads it).
            // 0.1.142: the gun in the left hand reloads with the
            // left Y (the right B only reloads a gun in the right hand); the
            // revolver in the left hand is opened by the left Y and loaded by
            // the right hand (TickRevolver), its trigger held while it is open.
            if(can&&current.LeftRevolverManual&&(button==current.reloadAction||(button==current.primaryAction||button==current.secondaryAction)&&current.revolver.Open)){result=false;return;}
            if(can&&current.PrimaryLeft&&button==current.reloadAction)
            {
                if(phase==1&&!current.MirroredManual)
                {
                    if(current.rig.LeftControls.Valid&&(current.rig.LeftControls.Down&HandControls.B)!=0)current.StartHandReload(0,true);
                    else if(current.copyKey[1]>=0&&current.rig.RightControls.Valid&&(current.rig.RightControls.Down&HandControls.B)!=0)current.StartHandReload(1,true);
                }
                result=false;return;
            }
            if(can&&current.ManualEnabled&&(!current.PrimaryLeft||current.MirroredManual)
                &&(button==current.reloadAction||(button==current.primaryAction||button==current.secondaryAction&&current.profile!="pistol"&&!current.Underbarrel)&&(current.reload.BlocksFire||current.BreakBlocksFire||current.RevolverReady&&current.revolver.Open)))
            {result=false;return;}
            // SteamVR bindings can also feed the game's native fire action.
            // In physical-fist mode only tracked contacts may start an attack.
            // 0.1.98: the right trigger holding an NPC body never fires/punches.
            if(button==current.primaryAction&&can&&BodyGrabState.Holding(!current.PrimaryLeft)){result=false;return;}
            // 0.1.126: the right hand at the left hand's grenade pulls its pin, it does not fire.
            if(can&&current.RightAtLeftGrenade&&(button==current.primaryAction||button==current.secondaryAction)){result=false;return;}
            if(button==current.primaryAction&&can
                &&GripCarry.Current?.HoldingBody!=true&&WeaponOptions.PhysicalPunches.Value&&current.inventory?.currentEquipable?.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Fist)
            {result=false;return;}
            if(can)result |= current.VirtualButton(button, playerID, phase);
        }
        catch (Exception ex) { current.Fail(ex); }
    }
    private static void ButtonHeld(int button, int playerID, ref bool __result) => Inject(button,playerID,0,ref __result);
    private static void ButtonDown(int button, int playerID, ref bool __result) => Inject(button,playerID,1,ref __result);
    private static void ButtonUp(int button, int playerID, ref bool __result) => Inject(button,playerID,2,ref __result);
    private static bool HitPoint(PlayerEquipableHandler __instance, float maxRange, ref bool missTarget, ref U __result)
    {
        var current = Current;
        if (current == null || current.handler == null || current.handler.Pointer != __instance.Pointer) return true;
        try
        {
            // 0.1.130: a weapon in the other hand firing by itself aims along itself.
            bool copy=current.copyShooting;
            if(!copy){current.RenderPose();if (!current.poseValid) return true;}
            float range = float.IsFinite(maxRange) && maxRange > 0 ? maxRange : 1000;
            U origin = copy?current.copyShotOrigin:current.leftShooting?current.leftAimPosition:current.aimPosition, direction = copy?current.copyShotForward:current.leftShooting?current.leftAimForward:current.aimForward;
            U point = origin + direction * range; float nearest = range;
            foreach (var hit in Physics.RaycastAll(origin,direction,range,__instance.raycastCollisionMask,QueryTriggerInteraction.Ignore))
            {
                var collider = hit.collider;
                if (collider == null || (current.playerRoot != null && collider.transform.IsChildOf(current.playerRoot))) continue;
                if (hit.distance < nearest) { nearest = hit.distance; point = hit.point; }
            }
            // The original player GetHitPoint initializes missTarget to false
            // even on a ray miss; retain that convention for projectile launch.
            missTarget = false; __result = point; return false;
        }
        catch (Exception ex) { current.Fail(ex); return true; }
    }
    private static bool ForwardRay(PlayerEquipableHandler __instance, ref Ray __result)
    {
        var current = Current;
        if (current == null || current.handler == null || current.handler.Pointer != __instance.Pointer) return true;
        try
        {
            if(current.copyShooting){__result=new Ray(current.copyShotOrigin,current.copyShotForward);return false;}
            current.RenderPose();
            if (!current.poseValid) return true;
            __result = new Ray(current.leftShooting?current.leftAimPosition:current.aimPosition,current.leftShooting?current.leftAimForward:current.aimForward); return false;
        }
        catch (Exception ex) { current.Fail(ex); return true; }
    }
    private static void RefreshMuzzle(FireComponent __instance)
    {
        var current = Current; if (current == null) return;
        try { if (current.Owns(__instance)) { current.RenderPose(); if (current.poseValid) current.WriteMuzzle(__instance); } }
        catch (Exception ex) { current.Fail(ex); }
    }
    private static void BeforeProjectile(FireComponent __instance)
    {
        var c=Current;
        if(c!=null&&c.CopyShotBy(__instance)){c.copyLaunched=true;c.WriteCopyMuzzle(__instance);return;}
        RefreshMuzzle(__instance);
    }
    private static void SupportedSpread(BulletSpreadComponent __instance,ref U __result)
    {
        var c=Current;if(c==null)return;
        // 0.1.194: also the crossbow fired from the other hand.
        if(c.copyShooting&&c.copyShotProfile=="crossbow"&&c.copyShotFire?.bulletSpreadComponent!=null&&c.copyShotFire.bulletSpreadComponent.Pointer==__instance.Pointer)
        {__result*=SpreadPolicy.Multiplier(true,false,c.copyShotProfile,WeaponOptions.SupportedSpread.Value);return;}
        if(!c.poseValid||!c.CanControl(c.playerId)||c.fire?.bulletSpreadComponent==null)return;
        float multiplier=SpreadPolicy.Multiplier(c.fire.bulletSpreadComponent.Pointer==__instance.Pointer,c.SupportHeld,c.profile,WeaponOptions.SupportedSpread.Value);
        __result*=multiplier;
    }
    private float leftShotUntil;
    private static void Shot(FireComponent __instance)
    {
        var current = Current; if (current == null) return;
        try
        {
            if(current.CopyShotBy(__instance)){current.copyLaunched=true;current.PushBodies(__instance);return;}
            if(!current.Owns(__instance) || !current.poseValid || !current.CanControl(current.playerId)) return;
            current.PushBodies(__instance);
            float now=Time.realtimeSinceStartup;
            if(current.IsLeft(__instance)){current.leftFlashUntil=now+.06f;current.leftVisual?.NotifyShot(now);current.rig.LeftShotHaptics();return;}
            current.NoteLaunch();
            if(!current.feedback.Fire(now,current.profile,current.SupportHeld)) return;
            current.visual?.NotifyShot(now);
            // 0.1.159: a throw from the left hand (the game's own grenade or knife
            // taken for it) is felt in the left hand, not kicked in the right.
            bool left=current.PrimaryLeft||now<current.leftShotUntil;
            // 0.1.135: also the left hand's gun reloaded by the right hand (its slide, pump, bolt).
            if(current.ManualReady&&!current.BreakReady&&(!left||current.MirroredManual))current.reload.OnShot();
            if(left)current.rig.LeftShotHaptics();else current.rig.ShotHaptics(current.profile,current.SupportHeld);
            if(now>=current.nextShotReport)
            {
                current.nextShotReport=now+.5f;
                Bootstrap.Write("VR SHOT weapon="+current.profile+" hand="+(left?"left":"right")+" twoHands="+current.SupportHeld+" kickDegrees="+current.feedback.Pitch+" kickMeters="+current.feedback.Kickback+" muzzle="+current.aimPosition+" direction="+current.aimForward);
            }
        }
        catch (Exception ex) { Bootstrap.Warn("Shot report unavailable: " + ex.Message); }
    }
    // 0.1.146: a shot of the player's into a body lying still moves it.
    private void PushBodies(FireComponent f)
    {
        try{var o=f.projectileOrigin;if(o!=null)NpcHitReactions.Current?.Shot(o.position,o.forward,profile,rig.PlayerRoot);}
        catch(Exception ex){Bootstrap.Warn("NPC SHOT body push: "+ex.Message);}
    }
    private void UpdateSocket(U position)
    {
        // Support detection remains active, but no visible target sphere is made.
        HideSocket();
    }
    private void HideSocket() { }
    private void SuspendPose()
    {
        ClearThrow();
        StopDualFire();leftVisual?.Hide();leftPoseValid=false;
        CancelReloadGesture();
        ContactRig.Current?.ResetGun();
        muzzleEffects?.Cancel();
        ResetFeedback();
        rig.DisarmTrigger(); StopOwnedFire(); ReleaseSupport(); HideSocket();
        visual?.Hide();
        poseValid = false;inertia.Reset();scopeSteady.Reset();inertiaFrame=-1;
    }
    private void StopSecondary()
    {
        if(!secondaryOwned)return;secondaryOwned=false;
        try{weapon?.secondaryFireUsageComponent?.End(PlayerEquipableHandler.UsageType.secondary);}
        catch(Exception ex){Bootstrap.Warn("Stop VR secondary fire: "+ex.Message);}
    }
    private void StopOwnedFire()
    {
        StopSecondary();
        if (!fireOwned) return; fireOwned = false;
        try
        {
            if (fire != null)
            {
                fire.TryCast<AutoFireComponent>()?.StopAutoFireCoroutine();
                if (weapon != null && weapon.primaryFireUsageComponent != null) weapon.primaryFireUsageComponent.End(PlayerEquipableHandler.UsageType.primary);
                else fire.End(PlayerEquipableHandler.UsageType.primary);
            }
        }
        catch (Exception ex) { Bootstrap.Warn("Stop VR fire: " + ex.Message); }
    }
    private void Unbind()
    {
        HideHints();ResetChest();
        ClearThrow();HideLanding();PutRocketBack("");HideBazooka();
        CancelRevolver();revolver=new RevolverReloadState();
        ClearDualVisual();dual=null;
        CancelReloadGesture();ammo=null;
        ContactRig.Current?.ResetGun();
        punches.Reset();
        muzzleEffects?.Cancel();
        ResetFeedback();
        poseValid = false;inertia.Reset();scopeSteady.Reset();inertiaFrame=-1; StopOwnedFire(); ReleaseSupport(); HideSocket();
        visual?.Suspend(); visual = null;
        weapon = null; fire = null; muzzle = null;
    }
    private void Fail(Exception ex)
    {
        if (failed) return; failed = true; poseValid = false; nextDiscover=Time.realtimeSinceStartup+3;HideLanding();
        Bootstrap.Warn("WEAPON HANDS suspended; retry in 3 seconds; head tracking and stereo remain running. " + ex);
        try { Unbind(); } catch (Exception cleanup) { Bootstrap.Warn("Weapon cleanup: " + cleanup.Message); }
    }
    // 0.1.248: a firearm's aim dot, where its shot goes (not in a story scene or a menu).
    // The bazooka has its own (where its rocket lands, always shown).
    private void ShowGunDot(AimDot dot,U origin,U forward)
    {
        int mode=QualityOptions.AimDotMode;
        if(mode==0||!HolsterLayout.Firearm(profile)||profile=="bazooka"||rig.Scripted||GameUiControls.Current?.BlocksGameplay==true){dot.Hide();return;}
        dot.Show(origin,forward,ShotMask,playerRoot,mode);
    }
    public void Dispose()
    {
        gunDot.Dispose();leftGunDot.Dispose();
        ClearThrow();throwVisual?.Dispose();if(throwRoot!=null)UnityEngine.Object.Destroy(throwRoot);DisposeLanding();DisposeBazooka();
        if (disposed) return; disposed = true;
        if (Current == this) Current = null;
        try { Unbind(); } finally
        {
            ClearHolsters();pins?.Dispose();pins=null;punches.Dispose();HolsterCopy.StruckNpc=null;npcHits?.Dispose();npcHits=null;visuals.Dispose();pouch?.Dispose();drops?.Dispose();reloadAudio?.Dispose();impactAudio?.Dispose();impactAudio=null;reloadStates.Clear();glow?.Dispose();glow=null;outline?.Dispose();outline=null;DisposeChest();
            muzzleEffects?.Dispose(); muzzleEffects=null;
            patches.UnpatchSelf();
        }
    }
}
