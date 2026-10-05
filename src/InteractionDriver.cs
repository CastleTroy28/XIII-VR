using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class InteractionDriver : IDisposable
{
    internal static InteractionDriver? Current;
    internal bool BodyTarget=>rays.Count>0&&carry.IsBodyTarget(rays[0]);
    // 0.1.142: also a hand holding an enemy's gun by the barrel.
    // 0.1.166: a hand holds a ladder (HandClimbing) - for the LADDER log line.
    internal bool ClimbingActive=>climbing.Active;
    // 0.1.186: also a hand holding a medkit taken from a forearm.
    internal bool HandOccupied(bool right)=>props.Holding(right)||doors.Holding(right)||climbing.Holding(right)||bodies.Holding(right)||carry.HidesHand(right)||NpcHitReactions.Current?.Grabbing(right)==true
        ||GameUiControls.Current?.Items.ArmHeld(right)==true
        // 0.1.195: a grappling / zipline hook in that hand, or that hand on the zipline cable.
        ||GameUiControls.Current?.ItemHeldOn(right)==true;
    internal Transform? HeldRoot(bool right)=>props.HeldRoot(right);
    private readonly CameraRig rig;
    private readonly TouchButtons touch=new();
    private readonly KeyUnlockGesture keys;
    private int keysBlockedFrame=-1;
    internal bool KeyActive=>keys.Active;
    internal string KeyGripProfile=>keys.GripProfile;
    internal bool KeyLockpick=>keys.Lockpick;
    internal bool KeyPicking=>keys.Picking;
    internal void PrepareKeyHand(NativeHandVisual hand)=>keys.PrepareHand(hand);
    internal void RenderKey(Transform? hand)=>keys.Render(hand);
    // Pointing at something the right Grip alone picks up (not a door,
    // cabinet or locker, not a body/hostage).
    private bool PickupTarget()
    {
        try
        {
            if(rays.Count==0||props.Consumes(MainRight)||doors.Consumes(MainRight))return false;
            // 0.1.124: the grip at a weapon on the body takes that weapon.
            if(WeaponHands.Current?.MainNearHolster==true)return false;
            // 0.1.186: the grip that took a medkit from a forearm.
            if(GameUiControls.Current?.Items.ArmHeld(MainRight)==true)return false;
            // 0.1.125: a pointed weapon is taken by the pointing grab.
            // 0.1.171: the weapon this hand points at (or did a moment ago).
            if(WeaponHands.Current?.PointedWeaponBy(MainRight)==true)return false;
            // 0.1.245: a hand holding a weapon taken from the ground or the body (or whose grip has just taken one) picks nothing else up: one press took a revolver and the broom behind it.
            if(WeaponHands.Current?.HandTaken(MainRight)==true)return false;
            var ray=rays[0];
            if(ray==null||!ray.hasRayHit||ray.RaycastHittable==null||ray.rayHit.distance>3||!ray.IsRaycastHittablePingValid())return false;
            if(carry.IsBodyTarget(ray))return false;
            doorFrame=-1;return !DoorTarget;
        }
        catch(Exception ex){Bootstrap.Warn("INTERACTION pickup target: "+ex.Message);return false;}
    }
    // Doors/cabinets/lockers and key, card, lockpick or hook locks keep the
    // deliberate Grip + A. Cached per frame (HUD labels ask every frame).
    private int doorFrame=-1;private bool doorTarget;
    internal bool DoorTarget
    {
        get
        {
            if(doorFrame==Time.frameCount)return doorTarget;doorFrame=Time.frameCount;
            try
            {
                var a=TargetAction;
                var hit=rays.Count>0?rays[0].RaycastHittable?.TryCast<Component>():null;
                doorTarget=a!=null&&(a.conditional!=RaycastAction.InteractionConditionals.Nothing||InteractionHints.IsDoor(a))||InteractionHints.IsDoor(hit);
            }
            catch{doorTarget=false;}
            return doorTarget;
        }
    }
    // 0.1.215: a key, card or lockpick lock not opened yet: the stick click
    // of the hand that takes things (R3; L3 left-handed) takes the item out.
    private int lockFrame=-1;private bool lockAimed;private Func<bool>? lockQuery;
    internal bool LockTarget=>LockAimed();
    private bool LockAimed()
    {
        if(lockFrame==Time.frameCount)return lockAimed;lockFrame=Time.frameCount;
        try{var a=TargetAction;lockAimed=a!=null&&a.conditional is RaycastAction.InteractionConditionals.Key or RaycastAction.InteractionConditionals.Keycard or RaycastAction.InteractionConditionals.Lockpick&&!a.conditionalResolved;}
        catch{lockAimed=false;}
        return lockAimed;
    }
    // That click (held from the press at the lock, or pressed at it now) is the lock's.
    private bool StickForLock(bool right)
    {
        if(right!=MainRight)return false;
        if(input.LockHeld)return true;
        var c=MainControls;
        return c.Valid&&(c.Held&HandControls.Stick)!=0&&Allowed()&&LockAimed();
    }
    // 0.1.119: a mounted gun is taken with both grips.
    private int turretFrame=-1;private bool turretTarget;
    internal bool TurretTarget
    {
        get
        {
            if(turretFrame==Time.frameCount)return turretTarget;turretFrame=Time.frameCount;
            try
            {
                var hit=rays.Count>0?rays[0].RaycastHittable?.TryCast<Component>():null;
                turretTarget=hit!=null&&(hit.name.ToLowerInvariant().Contains("turret")||hit.GetComponentInParent(Il2CppType.Of<PlayMagic.Weapons.MountedWeaponComponent>())!=null);
            }
            catch{turretTarget=false;}
            return turretTarget;
        }
    }
    private int bothFrame=-1;private bool bothHeld,bothWas,bothDown,bothUp;
    private void SampleBothGrips()
    {
        if(bothFrame==Time.frameCount)return;bothFrame=Time.frameCount;
        bothWas=bothHeld;
        bothHeld=rig.RightControls.Valid&&rig.LeftControls.Valid&&(rig.RightControls.Held&HandControls.Grip)!=0&&(rig.LeftControls.Held&HandControls.Grip)!=0;
        bothDown=bothHeld&&!bothWas;bothUp=!bothHeld&&bothWas;
        if(bothDown&&TurretTarget)Bootstrap.Write("INTERACTION PRESS both grips (mounted gun) player="+playerId);
    }
    internal bool GrappleTarget{get{try{return TargetAction?.conditional==RaycastAction.InteractionConditionals.GrapplingHook;}catch{return false;}}}
    internal bool ZiplineTarget{get{try{var a=TargetAction;return a!=null&&ZiplineAction(a);}catch{return false;}}}
    // 0.1.125: the player as the game's interaction actor (weapon pickups).
    internal IInteractionActor? Actor{get{try{return rays.Count>0?rays[0].InteractionActor:null;}catch(Exception){return null;}}}
    internal RaycastAction? TargetAction=>rays.Count>0?rays[0].RaycastHittable?.TryCast<RaycastAction>():null;
    // 0.1.244: a blow on a thing the game breaks when used (TouchButtons.Strike).
    internal bool Strike(Collider collider,RaycastHit hit,out string note)
    {
        note="";
        try
        {
            if(!Allowed()||climbing.Active||keys.Active)return false;
            var actor=Actor;if(actor==null)return false;
            return touch.Strike(collider,hit,actor,out note);
        }
        catch(Exception ex){note="unreadable: "+ex.Message;return false;}
    }
    // 0.1.84: the grappling hook is held and aimed with the left hand.
    // 0.1.195: or the right one (rayFromRight); the zipline hook too.
    private bool rayFromLeft,rayFromRight;private float nextToolReport;
    private void RefreshFrom(int side)
    {
        if(side==0)rayFromLeft=true;else rayFromRight=true;
        refreshedFrame=-1;try{RefreshTarget();}finally{rayFromLeft=rayFromRight=false;}
    }
    // 0.1.195: the zipline hook pointed at a zipline starts the
    // ride (the game's own zipline interaction, as its interact button would).
    internal bool TryZipline(int side,bool pressed)
    {
        if(!Allowed())return false;
        RefreshFrom(side);
        if(rays.Count==0)return false;
        var ray=rays[0];var target=ray.RaycastHittable?.TryCast<RaycastAction>();
        if(target==null||!ZiplineAction(target))
        {
            if(pressed&&Time.realtimeSinceStartup>=nextToolReport){nextToolReport=Time.realtimeSinceStartup+1;Bootstrap.Write("ZIPLINE the hook points at no zipline: "+(ray.hasRayHit?ray.rayHit.collider?.name+" d="+ray.rayHit.distance.ToString("F1"):"nothing"));}
            return false;
        }
        if(target.GetConditionState()||!target.IsActorValid(ray.InteractionActor)||!ray.IsRaycastHittablePingValid()||target.IsInteractionBlocked(ray.InteractionActor))
        {
            if(Time.realtimeSinceStartup>=nextToolReport){nextToolReport=Time.realtimeSinceStartup+1;Bootstrap.Write("ZIPLINE "+target.name+" not usable from here yet (d="+ray.rayHit.distance.ToString("F1")+")");}
            return false;
        }
        target.PingRaycastHittable(ray.InteractionActor,ray.rayHit,out bool valid);return valid;
    }
    // The hand points at a zipline (without using it).
    internal bool ZiplineAt(int side)
    {
        if(!Allowed())return false;
        RefreshFrom(side);
        var target=rays.Count>0?rays[0].RaycastHittable?.TryCast<RaycastAction>():null;
        return target!=null&&ZiplineAction(target);
    }
    private readonly Dictionary<IntPtr,bool> ziplineActions=new();
    // An interaction whose events include the game's zipline ride.
    private bool ZiplineAction(RaycastAction a)
    {
        if(ziplineActions.TryGetValue(a.Pointer,out bool known))return known;
        bool found=false;
        try
        {
            if(a.GetComponent(Il2CppType.Of<Zipline>())!=null)found=true;
            foreach(var list in new[]{a.manualInteractions,a.automaticInteractionList})
            {
                if(found||list==null)continue;
                foreach(var receiver in list)
                {
                    if(receiver?.events==null)continue;
                    foreach(var e in receiver.events)if(e!=null&&e.TryCast<Zipline.ZiplineEvent>()!=null){found=true;break;}
                    if(found)break;
                }
            }
        }
        catch(Exception ex){Bootstrap.Warn("ZIPLINE action check: "+ex.Message);}
        ziplineActions[a.Pointer]=found;
        if(found)Bootstrap.Write("ZIPLINE interaction found: "+a.name);
        return found;
    }
    // 0.1.195: side: the hand the tool is aimed with (0 left, 1 right).
    internal bool TryUseTool(PlayerEquipableInventory.ActiveEquipmentSlot slot,int side)
    {
        if(!Allowed())return false;
        RefreshFrom(side);
        if(rays.Count==0)return false;
        var ray=rays[0];var target=ray.RaycastHittable?.TryCast<RaycastAction>();
        if(target==null||target.GetConditionState()||!target.IsActorValid(ray.InteractionActor)||!ray.IsRaycastHittablePingValid()||target.IsInteractionBlocked(ray.InteractionActor))
        {
            if(Time.realtimeSinceStartup>=nextToolReport){nextToolReport=Time.realtimeSinceStartup+1;Bootstrap.Write("TOOL no target slot="+slot+" side="+(side==0?"left":"right")+" hit="+(ray.hasRayHit?ray.rayHit.collider?.name+" d="+ray.rayHit.distance.ToString("F1"):"none")+" action="+(target!=null?target.name+" cond="+target.conditional:"none"));}
            return false;
        }
        if(slot==PlayerEquipableInventory.ActiveEquipmentSlot.Lockpick&&target.conditional!=RaycastAction.InteractionConditionals.Lockpick)return false;
        if(slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Lockpick&&target.conditional!=RaycastAction.InteractionConditionals.GrapplingHook)return false;
        target.PingRaycastHittable(ray.InteractionActor,ray.rayHit,out bool valid);return valid;
    }
    private readonly GripCarry carry;
    private readonly GrappleVr grapple;
    private readonly ZiplineVr zipline;
    private readonly HandClimbing climbing;
    private readonly LooseProps props=new();
    private readonly BodyGrab bodies=new();
    private readonly PhysicalDoors doors=new();
    private int physicalFrame=-1;
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.interaction");
    private readonly InteractionState input=new();
    private readonly List<RaycastSystem> rays=new();
    private readonly Dictionary<int,(GameObject obj,bool active)> crosshairs=new();
    private Transform? root;
    private GameObject? rayOrigin;
    private int playerId=-1,actionId,sampledFrame=-1,refreshedFrame=-1;
    private float nextDiscover,nextReport;
    private float retryAt;
    private int failures;
    private bool disposed,failed,refreshing,crosshairFailed,lastSampleAllowed;
    internal InteractionDriver(CameraRig cameraRig)
    {
        rig=cameraRig;keys=new KeyUnlockGesture(rig);carry=new GripCarry(rig);climbing=new HandClimbing(rig);grapple=new GrappleVr(rig);zipline=new ZiplineVr(rig);
        try
        {
            actionId=GameInputManager.InputActionToRewiredID(InputActions.Gameplay_Interact);
            if(actionId<0) throw new InvalidOperationException("Interaction mapping unavailable");
            // Native DoUpdate inlines ExecuteRaycast. Hook both entry points;
            // temporarily replace only ray parameters, never a game camera pose.
            foreach(string method in new[]{"DoUpdate","ExecuteRaycast"})
                patches.Patch(AccessTools.DeclaredMethod(typeof(RaycastSystem),method) ?? throw new MissingMethodException(method),
                    prefix:new HarmonyMethod(typeof(InteractionDriver),nameof(BeginRay)),
                    postfix:new HarmonyMethod(typeof(InteractionDriver),nameof(EndRay)),
                    finalizer:new HarmonyMethod(typeof(InteractionDriver),nameof(FinalizeRay)));
            Patch(typeof(GameInputManager),"GetButton_Internal",nameof(ButtonHeld));
            Patch(typeof(GameInputManager),"GetButtonDown_Internal",nameof(ButtonDown));
            Patch(typeof(GameInputManager),"GetButtonUp_Internal",nameof(ButtonUp));
            foreach(string method in new[]{"Update","SetCrosshairElements","SetCrosshairGold"}) Patch(typeof(HUDWeaponElements),method,nameof(HideCrosshairs));
            patches.Patch(AccessTools.DeclaredMethod(typeof(RaycastAction),"PingRaycastHittable"),prefix:new HarmonyMethod(typeof(InteractionDriver),nameof(BeginUnlock)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(BaseAction),"TriggerInteractions"),prefix:new HarmonyMethod(typeof(InteractionDriver),nameof(DeferDoorOpen)));
            LinkCarryInteraction();
            Patch(typeof(PlayerHUDControl),"Update",nameof(FilterHints));
            Patch(typeof(CustomAnimationTool),"StartCustomAnimationTool",nameof(ResumeCabinet));
            Current=this;LockStick.Query=right=>Current?.StickForLock(right)==true;
            Bootstrap.Write("INTERACTION 0.1.39 ready; right grip + A; native ray from calibrated controller; screen crosshair hidden; rewired="+actionId);
        }
        catch { carry.Dispose();climbing.Dispose();grapple.Dispose();zipline.Dispose();patches.UnpatchSelf(); throw; }
    }
    private static bool DeferDoorOpen(BaseAction __instance)=>KeyUnlockGesture.Completing==null||__instance.Pointer!=KeyUnlockGesture.Completing.Pointer;
    private static void ResumeCabinet(CustomAnimationTool __instance)
    {try{Current?.doors.ResumeNative(__instance);}catch(Exception ex){Bootstrap.Warn("CABINET resume: "+ex.Message);}}
    private static void FilterHints(PlayerHUDControl __instance)
    {
        InteractionHints.Apply(__instance);
        // 0.1.213: the hostage icon while a hand points at one.
        Current?.carry.HostagePrompt(__instance);
        try{VrPromptLabels.RefreshHud(__instance);}catch(Exception ex){Bootstrap.Warn("VR PROMPTS: "+ex.Message);}
    }
    private static bool BeginUnlock(RaycastAction __instance,IInteractionActor interactionActor,RaycastHit rayhit,ref bool pingValidity)
    {
        var c=Current;if(c==null||!c.Allowed()||interactionActor==null||c.rays.Count==0||c.rays[0].InteractionActor?.GetActorID()!=interactionActor.GetActorID())return true;
        try
        {
            if(__instance.conditional==RaycastAction.InteractionConditionals.Lockpick||__instance.conditional==RaycastAction.InteractionConditionals.GrapplingHook)
                GameUiControls.Current?.Items.ClearTool();
            bool original=c.keys.Intercept(__instance,interactionActor,c.rays[0],rayhit);if(!original)pingValidity=c.keys.Active;return original;
        }
        catch(Exception ex){c.keys.Cancel();pingValidity=false;Bootstrap.Warn("KEY gesture cancelled: "+ex.Message);return !KeyUnlockGesture.LockedKey(__instance);}
    }
    private void Patch(Type type,string method,string hook) => patches.Patch(AccessTools.DeclaredMethod(type,method) ?? throw new MissingMethodException(type.Name,method),postfix:new HarmonyMethod(typeof(InteractionDriver),hook));
    internal void Tick()
    {
        if(disposed) return;
        if(failed)
        {
            if(Time.realtimeSinceStartup<retryAt) return;
            failed=false; root=null; rays.Clear(); nextDiscover=0; crosshairFailed=false;
            Bootstrap.Write("INTERACTION retry after scene/object invalidation; release right grip to rearm.");
        }
        try
        {
            if(Time.realtimeSinceStartup>=nextDiscover) { nextDiscover=Time.realtimeSinceStartup+1; FindPlayer(); }
            Sample();
            if(keys.Active)keysBlockedFrame=Time.frameCount;
            keys.Tick(Allowed());
            grapple.Tick(root);
            zipline.Tick(root);
            if(input.Action.Down)RefreshTarget();
            else if(offInput.Action.Down&&!refreshing){forceOff=true;refreshedFrame=-1;try{RefreshTarget();}finally{forceOff=false;}}
            // 0.1.151: the grip of the hand that took the thing (either hand) holds it.
            int carrySide=carry.HoldSide(MainRight?1:0);bool carryRight=carrySide==1;
            // 0.1.156: held in both hands, either grip holds
            // it; it is let go only when both are open. A hand holding it never
            // gives its grip to a door or a loose thing meanwhile.
            var carryInput=carryRight?rig.RightControls:rig.LeftControls;
            if(carry.Owns&&WeaponHands.Current?.BothHandsOn==true)carryInput=CarryGripState.Either(carryInput,carryRight?rig.LeftControls:rig.RightControls);
            carry.Tick(root,carryInput,Allowed()&&(carry.Owns||!props.Consumes(carryRight)&&!doors.Consumes(carryRight)),rays.Count>0?rays[0]:null,Allowed());
            if(!Allowed()) HidePoint();
        }
        catch(Exception ex) { Fail(ex); }
    }
    private void FindPlayer()
    {
        var found=rig.PlayerRoot;
        if(found==root && rays.Count>0 && rays[0]!=null) return;
        keys.Cancel();touch.Reset();input.Reset();offInput.Reset(); sampledFrame=-1; rays.Clear(); RestoreCrosshairs();InteractionHints.Restore();
        // Scene loads destroy owned Unity objects without nulling the managed
        // wrapper. Never use ??= with UnityEngine.Object here.
        if(rayOrigin!=null) UnityEngine.Object.Destroy(rayOrigin);
        rayOrigin=null; HidePoint(); refreshedFrame=-1; crosshairFailed=false;
        root=found; playerId=-1;
        if(root==null) return;
        foreach(var component in root.GetComponentsInChildren(Il2CppType.Of<RaycastSystem>(),true))
        {
            var ray=component.TryCast<RaycastSystem>(); if(ray==null) continue;
            var owner=ray.GetOwner(); if(!owner.IsPlayer || owner.IsInvalid) continue;
            if(playerId>=0 && owner.Id!=playerId) continue;
            playerId=owner.Id; rays.Add(ray);
            Bootstrap.Write("INTERACTION RAY bound player="+playerId+" object="+ray.name+" distance="+ray.rayDistance+" mask="+(int)ray.rayLayerMasks);
        }
        if(rays.Count==0) Bootstrap.Write("INTERACTION waiting: no local player RaycastSystem");
    }
    private bool Owns(RaycastSystem ray)
    {
        foreach(var r in rays) if(r!=null && ray!=null && r.Pointer==ray.Pointer) return true;
        return false;
    }
    private bool Allowed() => !disposed && !failed && root!=null && root.gameObject.activeInHierarchy && playerId>=0 && rays.Count>0
        && !rig.Scripted && GameUiControls.Current?.BlocksGameplay!=true && WindowFocus.Playable && Time.timeScale>0 && !PauseMenuControl.HackGameIsPaused && rig.HeadTrackingValid && !GameInputManager.IsInputLocked(playerId);
    private void Sample()
    {
        rig.PollControls();
        bool allowed=Allowed() && rig.SampleWorldHands(out _,out _,out _);
        if(sampledFrame==Time.frameCount && lastSampleAllowed==allowed) return;
        sampledFrame=Time.frameCount; lastSampleAllowed=allowed;
        if(physicalFrame!=Time.frameCount)
        {
            physicalFrame=Time.frameCount;
            long timer=FramePerformance.Begin();
            climbing.Tick(allowed&&!keys.Active,doors,props);
            FramePerformance.Scope("climb-interactions",timer);timer=FramePerformance.Begin();
            doors.Tick(rig,allowed&&!climbing.Active&&!keys.Active,rays.Count>0?rays[0].InteractionActor:null,props);
            FramePerformance.Scope("door-interactions",timer);timer=FramePerformance.Begin();
            props.Tick(rig,allowed&&!climbing.Active&&!keys.Active,doors);
            touch.Tick(rig,allowed&&!climbing.Active&&!keys.Active,rays.Count>0?rays[0].InteractionActor:null);
            bodies.Tick(rig,allowed&&!climbing.Active&&!keys.Active);
            FramePerformance.Scope("prop-interactions",timer);
        }
        // 0.1.150: the hand that takes things: the right one, or the left one for a left-hander.
        input.Sample(MainControls,allowed&&!climbing.Active&&!carry.HidesHand(MainRight)&&NpcHitReactions.Current?.Grabbing(MainRight)!=true,PickupTarget,false,lockQuery??=LockAimed);
        // 0.1.151: the other hand's grip takes the game's things too, into that hand.
        offInput.Sample(OffControls,allowed&&!climbing.Active&&!keys.Active&&!carry.HidesHand(OffRight)&&!input.Action.Held&&NpcHitReactions.Current?.Grabbing(OffRight)!=true,OffPickupTarget,true);
        if(offInput.Action.Down)
        {
            int side=OffRight?1:0;carry.PressedBy(side);if(ThingTarget(OffReach))WeaponHands.ThingTakenBy(side);
            Bootstrap.Write("INTERACTION PRESS "+(OffRight?"right":"left")+" grip (the other hand, a thing) player="+playerId+" target="+(rays.Count>0?rays[0].RaycastHittable?.TryCast<Component>()?.name:"none"));
        }
        if(input.Action.Down){int side=MainRight?1:0;carry.PressedBy(side);if(ThingTarget(3))WeaponHands.ThingTakenBy(side);}
        if(input.Action.Down) Bootstrap.Write("INTERACTION PRESS "+(MainRight?"right ":"left ")+(input.LockHeld?(MainRight?"R3 (a lock)":"L3 (a lock)"):(MainControls.Held&HandControls.A)!=0?(MainRight?"grip+A":"grip+X"):"grip")+" player="+playerId+" target="+(rays.Count>0?rays[0].RaycastHittable?.TryCast<Component>()?.name:"none"));
    }
    private bool PrepareRay(out Vector3 position,out Quaternion rotation)
    {
        position=default; rotation=Quaternion.identity;
        if(!Allowed() || !rig.SampleWorldHands(out var left,out var right,out bool leftValid)) return false;
        // 0.1.88: while the grappling hook is in the left hand, the target ray
        // (and its marker) comes from the left hand too.
        // 0.1.150: a left-hander points with the left hand (it takes things).
        // 0.1.151: the other hand's grip on a thing: the ray from that hand while its press lasts.
        bool off=forceOff||offInput.Action.Held&&!input.Action.Held;
        var items=GameUiControls.Current?.Items;int toolSide=items?.HandTool==true?items.ToolSide:-1;
        bool fromLeft=rayFromLeft||!rayFromRight&&(off?MainRight:forceMain?!MainRight:toolSide>=0?toolSide==0:!MainRight);
        // 0.1.94: when the right hand points at nothing, alternate frames probe
        // with the free left hand, so the native "take hostage / pick up body"
        // prompt shows when the LEFT hand points at an NPC's back.
        probing=!fromLeft&&!forceMain&&!off&&!rayFromRight&&toolSide<0&&leftProbe&&leftValid&&LeftFreeForBody();
        fromLeft|=probing;
        var hand=fromLeft&&leftValid?left:right;
        position=CameraRig.UnityPosition(hand); rotation=ControllerAim.Rotation(hand);
        // The grappling hook's hand aims with the assist (either hand, 0.1.195).
        if(items?.GrappleTool==true&&(fromLeft&&leftValid?toolSide==0:toolSide==1))rotation=GrappleAssist(position,rotation);
        return true;
    }
    // 0.1.88: aim assist for the grappling hook. Grapple points are small
    // and far away; the left-hand ray snaps to the nearest one within a
    // 14 degree cone (and 45 m), so the marker and hint appear reliably.
    // 0.1.162: kept between rare scene searches (every 20 s since 0.1.164, after a scene
    // change and for a new player).
    private readonly SceneFind<RaycastAction> grapplePoints=new("grapple points",20,found=>
    {
        foreach(var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<RaycastAction>()))
        {var a=o.TryCast<RaycastAction>();if(a!=null&&a.gameObject.scene.IsValid()&&a.conditional==RaycastAction.InteractionConditionals.GrapplingHook)found.Add(a);}
    });
    private float nextGrappleScan;private Transform? grappleScene;
    private Quaternion GrappleAssist(Vector3 origin,Quaternion aim)
    {
        try
        {
            if(grappleScene!=rig.PlayerRoot){grappleScene=rig.PlayerRoot;grapplePoints.Reset();}
            if(Time.realtimeSinceStartup>=nextGrappleScan)grapplePoints.Refresh();
            var forward=aim*Vector3.forward;float best=14;Vector3 target=default;bool found=false;
            foreach(var a in grapplePoints.Items)
            {
                if(a==null||!a.isActiveAndEnabled)continue;
                var c=a.GetComponent(Il2CppType.Of<Collider>())?.TryCast<Collider>();
                var point=c!=null&&c.enabled?c.bounds.center:a.transform.position;
                var to=point-origin;float d=to.magnitude;if(d<.5f||d>45)continue;
                float angle=Vector3.Angle(forward,to);if(angle<best){best=angle;target=point;found=true;}
            }
            return found?Quaternion.LookRotation(target-origin,aim*Vector3.up):aim;
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextGrappleScan){Bootstrap.Warn("GRAPPLE aim assist: "+ex.Message);}nextGrappleScan=Time.realtimeSinceStartup+5;grapplePoints.Prune();return aim;}
    }
    private sealed class RayScope
    {
        private readonly RaycastSystem ray;
        private readonly Transform? origin;
        private readonly Vector3 forward;
        private readonly bool overridden;
        private bool restored;
        internal RayScope(RaycastSystem r) { ray=r; origin=r.rayOriginTransform; forward=r.rayCastForwardOverride; overridden=r.overrideRaycastForward; }
        internal void Restore()
        {
            if(restored) return;
            if(ray!=null) { ray.rayOriginTransform=origin!; ray.rayCastForwardOverride=forward; ray.overrideRaycastForward=overridden; }
            restored=true;
        }
    }
    private static void BeginRay(RaycastSystem __instance,out RayScope? __state)
    {
        __state=null; var c=Current;
        if(c==null) return;
        try
        {
            if(!c.Owns(__instance) || !c.PrepareRay(out var p,out var q)) return;
            if(c.rayOrigin==null) c.rayOrigin=new GameObject("XIII VR interaction ray origin");
            c.rayOrigin.transform.SetPositionAndRotation(p,q);
            __state=new RayScope(__instance);
            __instance.rayOriginTransform=c.rayOrigin.transform;
            __instance.rayCastForwardOverride=q*Vector3.forward;
            __instance.overrideRaycastForward=true;
        }
        catch(Exception ex)
        {
            try { __state?.Restore(); } catch(Exception cleanup) { Bootstrap.Warn("Interaction ray restore: "+cleanup.Message); }
            __state=null; c.Fail(ex);
        }
    }
    private bool leftProbe,probing,forceMain,forceOff;
    private readonly InteractionState offInput=new();
    private static bool OffRight=>!MainRight;
    private HandControls OffControls=>OffRight?rig.RightControls:rig.LeftControls;
    // A thing of the game's (EnvironmentalPickup: a bottle, a chair, a broom) within reach of the ray.
    private bool ThingTarget(float reach)
    {
        try
        {
            if(rays.Count==0)return false;var ray=rays[0];
            if(ray==null||!ray.hasRayHit||ray.RaycastHittable==null||ray.rayHit.distance>reach||!ray.IsRaycastHittablePingValid()||carry.IsBodyTarget(ray))return false;
            if(ray.RaycastHittable.TryCast<EnvironmentalPickup>()!=null)return true;
            var c=ray.RaycastHittable.TryCast<Component>();
            return c!=null&&c.GetComponentInParent(Il2CppType.Of<EnvironmentalPickup>())!=null;
        }
        catch(Exception){return false;}
    }
    // 0.1.153: the other hand's grip
    // takes whatever the game offers to take (a key, a card, ammunition, armour,
    // a medkit, a document...) as the main hand does - not only the game's
    // things to hit with. Doors and locks keep the main hand's Grip + A;
    // weapons on the ground are taken by the pointing grab of either hand.
    private bool OtherTarget(float reach)
    {
        try
        {
            if(rays.Count==0)return false;var ray=rays[0];
            if(ray==null||!ray.hasRayHit||ray.RaycastHittable==null||ray.rayHit.distance>reach||!ray.IsRaycastHittablePingValid()||carry.IsBodyTarget(ray))return false;
            doorFrame=-1;if(DoorTarget)return false;
            var c=ray.RaycastHittable.TryCast<Component>();
            if(c==null)return true;
            if(ray.RaycastHittable.TryCast<WeaponPickup>()!=null||c.GetComponentInParent(Il2CppType.Of<WeaponPickup>())!=null)return false;
            return true;
        }
        catch(Exception){return false;}
    }
    internal const float OffReach=2.2f;
    // The other hand's grip press: the ray from that hand, on a thing within reach.
    private bool OffPickupTarget()
    {
        try
        {
            bool right=OffRight;
            if(props.Consumes(right)||doors.Consumes(right)||HandOccupied(right)||keys.Active)return false;
            var hands=WeaponHands.Current;if(hands!=null&&!hands.HandFree(right))return false;
            // 0.1.171 (the log: the left grip at a pistol on the floor took a key lying near it):
            // the grip of a hand pointing at a weapon is for that weapon.
            if(hands?.PointedWeaponBy(right)==true||hands?.HandTaken(right)==true)return false;
            if(!right&&(GameUiControls.Current?.LeftItemHeld==true||carry.HidesLeft))return false;
            if(right&&GameUiControls.Current?.PendingConsumable==true)return false;
            forceOff=true;refreshedFrame=-1;
            try{RefreshTarget();}finally{forceOff=false;}
            bool thing=ThingTarget(OffReach)||OtherTarget(OffReach);
            refreshedFrame=-1;doorFrame=-1;
            return thing;
        }
        catch(Exception ex){Bootstrap.Warn("INTERACTION other hand's target: "+ex.Message);return false;}
    }
    // 0.1.150: the hand that takes things (pickups, doors, keys): the right
    // one, the left one for a left-hander (VR settings).
    private static bool MainRight=>!WeaponHands.LeftHanded;
    private HandControls MainControls=>MainRight?rig.RightControls:rig.LeftControls;
    private bool LeftFreeForBody()=>WeaponHands.Current?.HandFree(false)!=false&&!HandOccupied(false)&&GameUiControls.Current?.LeftItemHeld!=true;
    private void UpdateLeftProbe(RaycastSystem ray)
    {
        bool target=ray.hasRayHit&&ray.RaycastHittable!=null&&ray.rayHit.distance<=3&&ray.IsRaycastHittablePingValid();
        if(probing)leftProbe=target&&carry.IsBodyTarget(ray);   // keep the left hand only on a body/hostage
        else leftProbe=!target;                                  // nothing for the right hand: try the left next
    }
    private static void EndRay(RaycastSystem __instance,RayScope? __state)
    {
        try { __state?.Restore(); }
        catch(Exception ex) { Current?.Fail(ex); }
        if(__state==null) return;
        try { Current?.SkipCarriedNpc(__instance); Current?.UpdateLeftProbe(__instance); }
        catch(Exception ex) { Bootstrap.Warn("Left-hand target probe: "+ex.Message); if(Current!=null)Current.leftProbe=false; }
        try { Current?.ReportTarget(__instance); }
        catch(Exception ex) { Bootstrap.Warn("Interaction pointer unavailable: "+ex.Message); Current?.HidePoint(); }
    }
    private static Exception? FinalizeRay(Exception? __exception,RayScope? __state)
    {
        try { __state?.Restore(); }
        catch(Exception ex) { Bootstrap.Warn("Interaction ray final restore: "+ex.Message); }
        return __exception; // Never swallow a native game's exception.
    }
    private void RefreshTarget()
    {
        if(refreshing || refreshedFrame==Time.frameCount) return;
        refreshing=true; refreshedFrame=Time.frameCount;
        try { foreach(var r in rays) if(r!=null && r.isActiveAndEnabled) r.DoUpdate(); }
        finally { refreshing=false; }
    }
    private static void ButtonHeld(int button,int playerID,ref bool __result) => Inject(button,playerID,0,ref __result);
    private static void ButtonDown(int button,int playerID,ref bool __result) => Inject(button,playerID,1,ref __result);
    private static void ButtonUp(int button,int playerID,ref bool __result) => Inject(button,playerID,2,ref __result);
    private static void Inject(int button,int id,int phase,ref bool result)
    {
        var c=Current; if(c==null || button!=c.actionId || id!=c.playerId) return;
        try
        {
            // 0.1.119: on a mounted gun, Interact is only the short press that
            // leaves it (both grips let go); taking it needs both grips.
            var gun=MountedGunVr.Current;
            if(gun?.Mounted==true){result=gun.InteractPress(phase);return;}
            if(c.Allowed()&&c.TurretTarget)
            {
                c.SampleBothGrips();
                result=phase==0?c.bothHeld:phase==1?c.bothDown:c.bothUp;return;
            }
            if(c.touch.Injecting){result=phase!=2;return;}
            c.Sample(); var a=c.input.Action;
            // 0.1.151: or the other hand's grip on a thing.
            var o=c.offInput.Action;bool useOff=!a.Held&&!a.Down&&!a.Up&&(o.Held||o.Down||o.Up);bool pressRight=useOff?OffRight:MainRight;
            if(useOff)a=o;
            if(c.keys.Active||c.keysBlockedFrame==Time.frameCount||c.climbing.Active||c.carry.NativeDropInput||c.carry.Consumed||c.props.Consumes(pressRight)||c.doors.Consumes(pressRight)){result=false;return;}
            if(!c.Allowed()) { if(phase==2) result|=a.Up; return; }
            // A press always re-targets from the hand that pressed (the right one; the left for a left-hander; the other hand on a thing).
            if(a.Down && !c.refreshing){if(useOff)c.forceOff=true;else c.forceMain=true;c.refreshedFrame=-1;try{c.RefreshTarget();}finally{c.forceMain=false;c.forceOff=false;}}
            if(c.rays.Count>0&&c.carry.IsBodyTarget(c.rays[0])){result=false;return;}
            result|=phase==0?a.Held:phase==1?a.Down:a.Up;
        }
        catch(Exception ex) { c.Fail(ex); }
    }
    private void ReportTarget(RaycastSystem ray)
    {
        if(disposed || failed || !Allowed()) { HidePoint(); return; }
        var hit=ray.rayHit;
        bool target=ray.hasRayHit && ray.RaycastHittable!=null && hit.distance<=3 && ray.IsRaycastHittablePingValid();
        // 0.1.102: no marker dot at all. The target is only
        // logged; the hand/hint prompts still show what can be used.
        HidePoint();
        if(!target) return;
        if(Time.realtimeSinceStartup>=nextReport)
        {
            nextReport=Time.realtimeSinceStartup+2;
            Bootstrap.Write("INTERACTION TARGET "+hit.collider?.name+" distance="+hit.distance.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" valid=True");
        }
    }
    private static void HideCrosshairs(HUDWeaponElements __instance)
    {
        var c=Current; if(c==null || c.disposed || c.failed || c.crosshairFailed || c.root==null) return;
        try
        {
            var player=__instance.customCharacterController;
            bool local=player!=null && player.transform.IsChildOf(c.root);
            if(!local) { var inventory=__instance.inventoryRef; local=inventory!=null && inventory.transform.IsChildOf(c.root); }
            if(!local) return;
            c.HideRoot(__instance.m_crossHairsRoot); c.HideRoot(__instance.m_crossHairsGoldRoot);
        }
        catch(Exception ex) { c.crosshairFailed=true; c.RestoreCrosshairs(); Bootstrap.Warn("Crosshair hide disabled; interaction continues. "+ex.Message); }
    }
    private void HideRoot(GameObject obj)
    {
        if(obj==null) return;
        int id=obj.GetInstanceID();
        if(!crosshairs.ContainsKey(id)) { crosshairs.Add(id,(obj,obj.activeSelf)); Bootstrap.Write("CROSSHAIR hidden root="+obj.name); }
        obj.SetActive(false);
    }
    private void RestoreCrosshairs()
    {
        foreach(var entry in crosshairs.Values)
        {
            try { if(entry.obj!=null) entry.obj.SetActive(entry.active); }
            catch(Exception ex) { Bootstrap.Warn("Crosshair restore skipped: "+ex.Message); }
        }
        crosshairs.Clear();
    }
    private void HidePoint() { }
    private void Fail(Exception ex)
    {
        if(failed) return; failed=true; input.Reset();offInput.Reset();
        try{keys.Cancel();touch.Reset();climbing.Cancel();props.Cancel();doors.Cancel();bodies.Cancel();carry.Cancel();}catch(Exception cleanup){Bootstrap.Warn("Carry cleanup: "+cleanup.Message);}
        retryAt=Time.realtimeSinceStartup+Math.Min(5,1+failures++);
        try { HidePoint(); } catch(Exception cleanup) { Bootstrap.Warn("Interaction pointer cleanup: "+cleanup.Message); }
        RestoreCrosshairs();InteractionHints.Restore();
        if(rayOrigin!=null) UnityEngine.Object.Destroy(rayOrigin);
        rayOrigin=null; sampledFrame=refreshedFrame=-1;
        Bootstrap.Warn("INTERACTION suspended; automatic rebind scheduled. Movement, weapon and XR remain running. "+ex);
    }
    public void Dispose()
    {
        if(disposed) return; disposed=true; if(Current==this){Current=null;LockStick.Query=null;}
        input.Reset();offInput.Reset();keys.Dispose();NativeItemCue.Release();InteractionHints.Restore();touch.Reset();props.Dispose();doors.Dispose();bodies.Dispose();carry.Dispose();climbing.Dispose();zipline.Dispose();
        try { RestoreCrosshairs(); }
        finally
        {
            patches.UnpatchSelf();
            if(rayOrigin!=null) UnityEngine.Object.Destroy(rayOrigin);
        }
    }
}
