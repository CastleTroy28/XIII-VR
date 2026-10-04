using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using PlayMagic;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class LocomotionDriver : IDisposable
{
    internal static LocomotionDriver? Current;
    private readonly CameraRig rig;
    private readonly Harmony patches = new("xiii.vr.xrbootstrap.locomotion");
    private readonly LocomotionState state = new();
    private readonly ComfortState comfort=new();
    private readonly TeleportDriver teleport=new();
    private float turnDegrees;
    internal float LastStickTurn {get;private set;}
    private int turnConsumedFrame=-1;
    private bool runningAxes;
    private int horizontalAction,verticalAction;
    private bool wasTeleport,wasSnap;
    private readonly FirstPersonVisibility visibility = new();
    private CustomCharacterController? character;
    private CharacterControllerInputProvider? provider;
    private Transform? root;
    private int playerId = -1, sampledFrame = -1, jumpAction, crouchAction, sprintAction;
    private float nextDiscover, nextReport;
    private float lastWorldHeading;
    private bool enabled, failed, disposed;
    // 0.1.94: physical crouch → native crouch input (toggle or hold setting).
    private bool physicalWas,physicalOwned,physicalHold,physicalDown,physicalUp;private float physicalCheck=-1;private int physicalPulse;
    // 0.1.108: in water the sticks swap roles: right stick swims (forward/
    // back/sideways, head-relative), left stick X turns, left stick up/down
    // rises/dives. Arm strokes swim forward; looking down/up while moving
    // forward dives/rises.
    private readonly SwimStroke stroke=new();private readonly SwimVertical swim=new();
    private bool inWater,submerged;
    // 0.1.171: the game's own ladder climb (its Climbing state).
    private bool onLadder;private float nextSwimReport,swimPitch,gazeVertical=float.NaN;
    internal bool Swimming=>inWater;
    // 0.1.149 (FreezeWatch): how far the move stick is pushed, and whether
    // the character may walk now (not teleport mode, not swimming).
    internal float MoveInput{get{try{var l=rig.LeftStick;return l.Valid?l.Value.Length():0;}catch{return 0;}}}
    internal bool MoveAllowed=>!inWater&&!LocomotionOptions.Teleport.Value&&Allowed(true);
    // 0.1.170: under water (HandClimbing: the arms swim there, no ladder is taken).
    internal bool Submerged=>inWater&&submerged;
    internal bool NativeCrouching{get{try{return character!=null&&character.IsCrouching;}catch{return false;}}}
    internal bool PhysicalCrouchOwned=>physicalOwned;
    internal LocomotionDriver(CameraRig cameraRig)
    {
        rig = cameraRig; enabled = LocomotionOptions.Enabled.Value;
        try
        {
            jumpAction = GameInputManager.InputActionToRewiredID(InputActions.Movement_Jump);
            crouchAction = GameInputManager.InputActionToRewiredID(InputActions.Movement_Crouch);
            sprintAction = GameInputManager.InputActionToRewiredID(InputActions.Movement_Sprint);
            if (jumpAction < 0 || crouchAction < 0 || sprintAction<0 || jumpAction == crouchAction || sprintAction==jumpAction || sprintAction==crouchAction)
                throw new InvalidOperationException("Invalid locomotion action mapping");
            Patch(typeof(CharacterControllerInputProvider),"CalculatePlayerMovement",nameof(Move));
            Patch(typeof(CharacterControllerInputProvider),"GetLookInput",nameof(Look));
            Patch(typeof(GameInputManager),"GetButton_Internal",nameof(ButtonHeld));
            Patch(typeof(GameInputManager),"GetButtonDown_Internal",nameof(ButtonDown));
            Patch(typeof(GameInputManager),"GetButtonUp_Internal",nameof(ButtonUp));
            horizontalAction=GameInputManager.InputActionToRewiredID(InputActions.Movement_LeftRight);
            verticalAction=GameInputManager.InputActionToRewiredID(InputActions.Movement_ForwardBackwards);
            Patch(typeof(GameInputManager),"GetAxis",nameof(RunningAxis));
            patches.Patch(AccessTools.DeclaredMethod(typeof(CustomCharacterController),"UpdateCharacterPosition"),prefix:new HarmonyMethod(typeof(LocomotionDriver),nameof(SwimVelocity)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(CustomCharacterController),"UpdateRunningInput"),
                prefix:new HarmonyMethod(typeof(LocomotionDriver),nameof(BeginRunning)),
                finalizer:new HarmonyMethod(typeof(LocomotionDriver),nameof(EndRunning)));
            wasTeleport=LocomotionOptions.Teleport.Value;wasSnap=LocomotionOptions.SnapTurn.Value;
            Current = this;
            Bootstrap.Write("SPRINT mapped to left stick click (L3); rewired="+sprintAction+"; native hold/toggle setting retained.");
            Bootstrap.Write("LOCOMOTION 0.1.39 ready; enabled=" + enabled + "; jump=" + jumpAction + " crouch=" + crouchAction + "; left=head-relative move, right X=smooth turn, right Y=jump/crouch.");
        }
        catch { patches.UnpatchSelf(); throw; }
    }
    private void Patch(Type type, string method, string hook)
    {
        var original = AccessTools.DeclaredMethod(type,method) ?? throw new MissingMethodException(type.FullName,method);
        patches.Patch(original,postfix:new HarmonyMethod(typeof(LocomotionDriver),hook));
    }
    internal void Toggle()
    {
        enabled = !enabled; state.Reset(); comfort.Reset(); teleport.Cancel(); turnDegrees=0; sampledFrame = -1;
        Bootstrap.Write("LOCOMOTION enabled=" + enabled + "; center both sticks to resume.");
    }
    internal void Tick()
    {
        if (disposed) return;
        if(failed){if(Time.realtimeSinceStartup<nextDiscover&&rig.PlayerRoot==root)return;failed=false;root=null;character=null;provider=null;nextDiscover=0;}
        try
        {
            if (Time.realtimeSinceStartup >= nextDiscover) { nextDiscover = Time.realtimeSinceStartup + 0.5f; FindPlayer(); }
            if(wasTeleport!=LocomotionOptions.Teleport.Value||wasSnap!=LocomotionOptions.SnapTurn.Value)
            {state.Reset();comfort.Reset();teleport.Cancel();sampledFrame=-1;wasTeleport=LocomotionOptions.Teleport.Value;wasSnap=LocomotionOptions.SnapTurn.Value;Bootstrap.Write("LOCOMOTION mode="+(wasTeleport?"teleport":"slide")+"; turn="+(wasSnap?"snap":"smooth"));}
            Sample();
            bool commit=comfort.Teleport(rig.LeftStick,Allowed(true)&&LocomotionOptions.Teleport.Value&&!inWater);
            teleport.Tick(rig,character,comfort.Aiming,commit,Allowed(true)&&LocomotionOptions.Teleport.Value&&!inWater);
            if(Allowed(true) && character!=null)rig.AlignCollisionBody(character);
            SwimReport();
            visibility.Tick(character,LocomotionOptions.HideModel.Value && !rig.Scripted);
            if (Time.realtimeSinceStartup >= nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 3;
                var l = rig.LeftStick; var r = rig.RightStick;
                Bootstrap.Write(FormattableString.Invariant($"STICKS L=({l.Value.X:F2},{l.Value.Y:F2}) valid={l.Valid} R=({r.Value.X:F2},{r.Value.Y:F2}) valid={r.Valid} mode={(LocomotionOptions.Teleport.Value?"teleport":"slide")} aiming={comfort.Aiming} move=({state.Move.X:F2},{state.Move.Y:F2}) turn={state.Turn:F2} jump={state.Jump.Held} crouch={state.Crouch.Held} allowed={Allowed(true)} player={playerId} enabled={enabled} axisLock={(playerId>=0&&GameInputManager.IsAxisLocked(playerId))} inputLock={(playerId>=0&&GameInputManager.IsInputLocked(playerId))} ui={GameUiControls.Current?.BlocksGameplay==true} swim={(inWater?(submerged?"under":"surface"):"no")}"));
            }
        }
        catch (Exception ex) { Fail(ex); }
    }
    internal void BeforeRender()
    {
        if (disposed || failed) return;
        try { visibility.Tick(character,LocomotionOptions.HideModel.Value && !rig.Scripted); }
        catch (Exception ex) { Bootstrap.Warn("Model visibility update skipped; XR continues. " + ex.Message); }
    }
    private void FindPlayer()
    {
        var found = rig.PlayerRoot;
        if (found == root && character != null && provider != null) return;
        state.Reset(); comfort.Reset(); teleport.Cancel(); turnDegrees=0; sampledFrame = -1; visibility.Restore();
        root = found; character = null; provider = null; playerId = -1;
        if (root == null) return;
        var c = root.GetComponent(Il2CppType.Of<CustomCharacterController>())?.TryCast<CustomCharacterController>();
        if (c == null) { Bootstrap.Write("LOCOMOTION waiting: local character component unavailable"); return; }
        var owner = c.GetOwner();
        if (!owner.IsPlayer || owner.IsInvalid) return;
        var p = c.inputProvider?.TryCast<CharacterControllerInputProvider>();
        p ??= root.GetComponent(Il2CppType.Of<CharacterControllerInputProvider>())?.TryCast<CharacterControllerInputProvider>();
        if (p == null || p.ownerID != owner.Id || !p.transform.IsChildOf(root))
        { Bootstrap.Write("LOCOMOTION waiting: local input provider unavailable"); return; }
        character = c; provider = p; playerId = owner.Id;
        var body = c.transform.rotation;
        lastWorldHeading = HeadingMath.Yaw(new System.Numerics.Quaternion(body.x,body.y,body.z,body.w),0);
        Bootstrap.Write("LOCOMOTION PLAYER bound id=" + playerId + " root=" + root.name + " provider=" + p.GetIl2CppType().FullName);
        try { Bootstrap.Write("LOCOMOTION crouchToggle=" + p.Options.GetToggleCrouch()+" sprintToggle="+p.Options.GetToggleSprint()); }
        catch (Exception ex) { Bootstrap.Warn("Crouch-mode report unavailable; locomotion continues. " + ex.Message); }
    }
    private bool Owns(CharacterControllerInputProvider p) => provider != null && p != null && provider.Pointer == p.Pointer;
    private bool Allowed(bool axes)
    {
        return !disposed && !failed && enabled && playerId >= 0 && character != null && provider != null
            && character.gameObject.activeInHierarchy && provider.isActiveAndEnabled
            && !rig.Scripted && GameUiControls.Current?.BlocksGameplay!=true && WindowFocus.Playable && Time.timeScale > 0 && !PauseMenuControl.HackGameIsPaused
            && rig.HeadTrackingValid && !GameInputManager.IsInputLocked(playerId)
            && (!axes || !GameInputManager.IsAxisLocked(playerId));
    }
    private void Sample()
    {
        rig.PollControls();
        bool allowed = Allowed(true);
        // Locks may change within the frame; suppress input immediately.
        if (!allowed) { state.Reset(); comfort.Reset(); teleport.Cancel(); turnDegrees=0; sampledFrame = -1; stroke.Reset(); swim.Reset(); return; }
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        UpdateWater();
        // 0.1.110: same sticks in water as on land: left stick swims, right
        // stick X turns, right stick up/down rises/dives.
        state.Sample(rig.LeftStick,rig.RightStick,true,LocomotionOptions.Deadzone.Value);
        turnDegrees=comfort.Turn(state.Turn,LocomotionOptions.SnapTurn.Value,LocomotionOptions.SnapAngle.Value,LocomotionOptions.TurnSpeed.Value,Time.deltaTime);
        if(inWater){LevelGamePitch();SampleSwim();}else SamplePhysicalCrouch();
        if (state.Jump.Down) Bootstrap.Write("LOCOMOTION jump gesture");
        if (state.Crouch.Down) Bootstrap.Write("LOCOMOTION crouch gesture");
        if (state.Sprint.Down || state.Sprint.Up) Bootstrap.Write("SPRINT left stick held="+state.Sprint.Held);
    }
    private void UpdateWater()
    {
        bool water=false,under=false;
        try
        {
            var s=character!.CurrentPlayerState;
            onLadder=s==CustomCharacterController.PlayerStates.Climbing;
            water=s==CustomCharacterController.PlayerStates.Surfaced||s==CustomCharacterController.PlayerStates.Submerged;
            under=s==CustomCharacterController.PlayerStates.Submerged;
        }
        catch(Exception){water=under=false;onLadder=false;}
        submerged=under;
        if(water==inWater)return;
        inWater=water;state.WaterChanged();stroke.Reset();swim.Reset();
        // A physical crouch held when entering the water must not dive.
        physicalOwned=physicalHold=physicalDown=false;physicalPulse=0;physicalCheck=-1;physicalUp=true;
        gazeVertical=float.NaN;
        Bootstrap.Write(water?"SWIM controls on: left stick swims, right stick X turns, right stick up/down rises/dives, arm strokes and forward swim follow the gaze":"SWIM controls off");
    }
    private void SampleSwim()
    {
        float now=Time.realtimeSinceStartup,amplitude=0;
        if(rig.PhysicalHand(false,out var left)&&rig.PhysicalHand(true,out var right)&&rig.PhysicalHead(out var head))
            amplitude=stroke.Sample(left,right,head,Time.deltaTime,now);
        else stroke.Reset();
        if(amplitude>0)rig.SwimHaptics(amplitude);
        float pitch=0;
        if(rig.TryWorldHeadRotation(out var q))
        {var f=System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,q);pitch=MathF.Asin(Math.Clamp(f.Y,-1,1))*180/MathF.PI;}
        swimPitch=pitch;float forward=state.Move.Y+stroke.Forward;
        swim.Sample(state.Jump.Held,state.Crouch.Held,forward,pitch,submerged);
        float speed=0;try{speed=character!.swimSpeed;}catch(Exception){speed=0;}
        // The stick's own up/down keeps the native vertical swim.
        gazeVertical=submerged&&!state.Jump.Held&&!state.Crouch.Held?SwimGaze.Vertical(forward,pitch,speed):float.NaN;
        if(swim.Up.Down||swim.Down.Down)Bootstrap.Write("SWIM "+(swim.Up.Down?"up":"down")+" pitch="+pitch.ToString("F0")+" stick="+(state.Jump.Held||state.Crouch.Held)+" forward="+(state.Move.Y+stroke.Forward).ToString("F2")+" "+(submerged?"under":"surface"));
        if(stroke.Active&&now>=nextSwimReport){nextSwimReport=now+1;Bootstrap.Write("SWIM stroke power="+stroke.Power.ToString("F2")+" forward="+stroke.Forward.ToString("F2"));}
    }
    private void SamplePhysicalCrouch()
    {
        physicalDown=physicalUp=false;
        bool now=rig.PhysicalCrouch;float t=Time.realtimeSinceStartup;
        if(physicalPulse>0){physicalPulse--;if(physicalPulse==0)physicalUp=true;}
        if(now!=physicalWas)
        {
            physicalWas=now;
            if(now&&!NativeCrouching){physicalOwned=true;physicalHold=true;physicalDown=true;Bootstrap.Write("PHYSICAL CROUCH native crouch requested");}
            else if(!now&&physicalOwned){physicalOwned=false;physicalHold=false;physicalUp=true;physicalCheck=t+.3f;}
        }
        // Toggle-crouch setting: releasing does not stand up; press once more.
        if(physicalCheck>0&&t>=physicalCheck)
        {
            physicalCheck=-1;
            if(!physicalWas&&NativeCrouching){physicalDown=true;physicalPulse=3;Bootstrap.Write("PHYSICAL CROUCH toggle released with a second press");}
        }
    }
    // 0.1.166:
    // at a ladder, what the game was given and why (LADDER, LocomotionDriver.Ladder).
    partial void Ladder(CharacterControllerInputProvider provider,Vector2 given,string path);
    // 0.1.170: in water, how the swim went (SWIM MOVE, LocomotionDriver.Swim).
    partial void SwimGiven(Vector2 given,string path);
    partial void SwimReport();
    // 0.1.185: the game's own camera pitch levelled in the water (LocomotionDriver.Swim).
    partial void LevelGamePitch();
    private static void Move(CharacterControllerInputProvider __instance, ref Vector2 __result)
    {
        var current = Current; if (current == null) return;
        string path="native only";
        try
        {
            if (!current.Owns(__instance)) return;
            current.Sample();
            if (!current.Allowed(true)) {path="not allowed";return;}
            // 0.1.84: on the grappling rope the left stick climbs (native
            // up/down input), the right stick forward/back swings.
            // 0.1.95: checked BEFORE CanProcessInputDirection (the rope state
            // may report no direction input), and the same value is supplied
            // to the native axis reads while on the rope (RunningAxis).
            if(GrappleVr.Current?.OnRope==true)
            {
                var swing=current.RopeSwingInBody();
                if(Time.realtimeSinceStartup>=current.nextRopeReport){current.nextRopeReport=Time.realtimeSinceStartup+2;Bootstrap.Write("ROPE input native="+__result.x.ToString("F2")+","+__result.y.ToString("F2")+" swing="+current.RopeSwing.ToString("F2")+" inBody="+swing.X.ToString("F2")+","+swing.Y.ToString("F2")+" (head "+current.ropeHeadYaw.ToString("F0")+"°, body "+current.ropeBodyYaw.ToString("F0")+"°) canDirection="+__instance.CanProcessInputDirection()+" axisReads="+current.ropeAxisReads);current.ropeAxisReads=0;}
                __result=new Vector2(swing.X,swing.Y);path="rope";return;
            }
            if (!__instance.CanProcessInputDirection()) {path="game takes no direction";return;}
            // Combine native keyboard/gamepad movement and the VR stick; clamp
            // the vector so diagonal input cannot increase movement speed.
            if(LocomotionOptions.Teleport.Value&&!current.inWater){__result=new Vector2(0,0);path="teleport mode";return;}
            var m = current.state.Move;
            if(current.inWater)
            {
                float forward=Math.Clamp(m.Y+current.stroke.Forward,-1,1);
                // Under water the forward part is shared with the vertical swim (gaze).
                if(current.submerged&&!float.IsNaN(current.gazeVertical))forward=SwimGaze.Horizontal(forward,current.swimPitch);
                m=new System.Numerics.Vector2(m.X,forward);
            }
            // 0.1.171: on
            // the game's ladder the stick is given as it is. The game turns
            // the character to face the ladder; turned from where the head
            // looks, forward became partly sideways (not up, and not
            // "towards the ladder", which the game needs to step off at the top).
            if (!current.onLadder && current.rig.TryWorldHeadRotation(out var head))
            {
                var q = current.character!.transform.rotation;
                m = HeadingMath.InBodyFrame(m,head,new System.Numerics.Quaternion(q.x,q.y,q.z,q.w),ref current.lastWorldHeading);
            }
            __result = Vector2.ClampMagnitude(__result + new Vector2(m.X,m.Y),1);
            path=current.onLadder?"stick as it is (on the ladder)":"stick";
        }
        catch (Exception ex) { current.Fail(ex); }
        // 0.1.170: in water
        // nothing of the ladder check runs; what the game was given for the
        // swim is kept for the SWIM MOVE line (LocomotionDriver.Swim).
        finally { if(current.Owns(__instance)){if(current.inWater)current.SwimGiven(__result,path);else current.Ladder(__instance,__result,path);} }
    }
    private static void Look(CharacterControllerInputProvider __instance, ref Vector2 __result)
    {
        var current = Current; if (current == null) return;
        try
        {
            if (!current.Owns(__instance)) return;
            current.Sample();
            if (!current.Allowed(true)) return;
            // Native UpdateRotation consumes lookHorizontal in degrees per
            // update and rotates the character, then updates its camera rig.
            // Y belongs exclusively to jump/crouch, never desktop camera pitch.
            bool fresh=current.turnConsumedFrame!=Time.frameCount;
            float stick=fresh?current.turnDegrees:0;
            // Carrying a body/hostage: the character also follows the head.
            float carry=fresh?current.rig.CarryFollowTurn(Time.deltaTime):0;
            // 0.1.151: hanging on a ceiling fan the body turns with it.
            if(fresh)carry+=CeilingFans.Current?.ConsumeTurn()??0;
            if(fresh)current.LastStickTurn=stick;
            __result = new Vector2(stick+carry,0);
            current.turnConsumedFrame=Time.frameCount;
        }
        catch (Exception ex) { current.Fail(ex); }
    }
    // 0.1.110: under water, forward swimming follows the gaze: the vertical
    // part of the character's velocity, just before the native move.
    private static void SwimVelocity(CustomCharacterController __instance,ref Vector3 velocity)
    {
        var c=Current;if(c==null||c.character==null||!c.inWater||!c.submerged||float.IsNaN(c.gazeVertical))return;
        try{if(c.character.Pointer!=__instance.Pointer||!c.Allowed(true))return;velocity.y=c.gazeVertical;}
        catch(Exception ex){c.Fail(ex);}
    }
    private static void ButtonHeld(int button,int playerID,ref bool __result) => Inject(button,playerID,0,ref __result);
    private static void ButtonDown(int button,int playerID,ref bool __result) => Inject(button,playerID,1,ref __result);
    private static void ButtonUp(int button,int playerID,ref bool __result) => Inject(button,playerID,2,ref __result);
    private static void Inject(int button,int id,int phase,ref bool result)
    {
        var c = Current; if (c == null || id != c.playerId || (button != c.jumpAction && button != c.crouchAction && button!=c.sprintAction)) return;
        try
        {
            c.Sample();
            if (!c.Allowed(true)) return;
            if(GrappleVr.Current?.OnRope==true)
            {
                // Right stick Y swings on the rope; left stick click (L3) or
                // right B lets go (native jump).
                // 0.1.197: fired from the right hand: R3.
                if(button==c.jumpAction)result|=RopeRelease.Pressed(GrappleVr.Current?.RightHanded==true,c.state.Sprint,c.rig.LeftControls,c.rig.RightControls,phase);
                return;
            }
            if(c.inWater&&button!=c.sprintAction)
            {
                var e=button==c.jumpAction?c.swim.Up:c.swim.Down;
                result|=phase==0?e.Held:phase==1?e.Down:e.Up;
                return;
            }
            // 0.1.215: L3 at a key, card or lockpick lock (left-handed) takes the item out, not a sprint.
            if(button==c.sprintAction&&LockStick.Taken(false))return;
            var action = button == c.jumpAction ? c.state.Jump : button==c.sprintAction ? c.state.Sprint : c.state.Crouch;
            result |= phase == 0 ? action.Held : phase == 1 ? action.Down : action.Up;
            if(button==c.crouchAction)result|=phase==0?c.physicalHold||c.physicalPulse>0:phase==1?c.physicalDown:c.physicalUp;
        }
        catch (Exception ex) { c.Fail(ex); }
    }
    // Native toggle sprint reads GetAxis directly to detect standing still.
    // Supply VR axes ONLY inside that native method, so movement is not injected twice.
    private static void BeginRunning(CustomCharacterController __instance)
    {
        var c=Current;if(c==null)return;
        try{c.Sample();c.runningAxes=c.character!=null&&c.character.Pointer==__instance.Pointer&&c.Allowed(true)&&!LocomotionOptions.Teleport.Value;}
        catch(Exception ex){c.runningAxes=false;c.Fail(ex);}
    }
    private static void EndRunning(){if(Current!=null)Current.runningAxes=false;}
    private float nextRopeReport;private int ropeAxisReads;
    // 0.1.197: the left stick swings when the hook was fired from the right hand.
    private float RopeSwing{get{var swing=GrappleVr.Current?.RightHanded==true?rig.LeftStick:rig.RightStick;return swing.Valid&&Math.Abs(swing.Value.Y)>.2f?Math.Clamp(swing.Value.Y,-1,1):0;}}
    // 0.1.163: the game swings along the character's own forward,
    // which in VR is not where the player looks (the log: the body faced
    // 250°, the head about 190° - the swing went off to the side). The
    // stick's forward/back is turned from the head's heading into the
    // character's frame, as for walking (HeadingMath.InBodyFrame); once a frame.
    private int ropeFrame=-1;private System.Numerics.Vector2 ropeSwing;private float ropeHeadYaw,ropeBodyYaw;
    private System.Numerics.Vector2 RopeSwingInBody()
    {
        if(ropeFrame==Time.frameCount)return ropeSwing;
        ropeFrame=Time.frameCount;
        var stick=new System.Numerics.Vector2(0,RopeSwing);ropeSwing=stick;
        try
        {
            if(character!=null&&rig.TryWorldHeadRotation(out var head))
            {
                var q=character.transform.rotation;var body=new System.Numerics.Quaternion(q.x,q.y,q.z,q.w);
                ropeSwing=HeadingMath.InBodyFrame(stick,head,body,ref lastWorldHeading);
                ropeHeadYaw=lastWorldHeading*180/MathF.PI;ropeBodyYaw=HeadingMath.Yaw(body,0)*180/MathF.PI;
            }
        }
        catch(Exception){ropeSwing=stick;}
        return ropeSwing;
    }
    private static void RunningAxis(int action,int playerID,ref float __result)
    {
        var c=Current;if(c==null||playerID!=c.playerId)return;
        if(GrappleVr.Current?.OnRope==true&&(action==c.verticalAction||action==c.horizontalAction))
        {
            try
            {
                if(!c.Allowed(true))return;
                var swing=c.RopeSwingInBody();
                if(action==c.verticalAction){c.ropeAxisReads++;__result=Math.Clamp(__result+swing.Y,-1,1);}
                else __result=Math.Clamp(__result+swing.X,-1,1);
            }
            catch(Exception ex){c.Fail(ex);}
            return;
        }
        if(!c.runningAxes)return;
        if(action!=c.horizontalAction&&action!=c.verticalAction)return;
        float add=action==c.horizontalAction?c.state.Move.X:c.state.Move.Y;
        __result=Math.Clamp(__result+add,-1,1);
    }
    private void Fail(Exception ex)
    {
        if (failed) return;
        failed = true; nextDiscover=Time.realtimeSinceStartup+3; state.Reset(); comfort.Reset(); teleport.Cancel(); turnDegrees=0;
        Bootstrap.Warn("LOCOMOTION suspended; automatic rebind scheduled; XR remains running. " + ex);
        visibility.Restore();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; state.Reset(); comfort.Reset(); teleport.Cancel(); turnDegrees=0; if (Current == this) Current = null;
        try { patches.UnpatchSelf(); } finally { visibility.Restore(); teleport.Dispose(); }
    }
}
