using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.XR;
using UVector = UnityEngine.Vector3;
using UQuat = UnityEngine.Quaternion;
namespace XiiiXR;
internal sealed class CameraRig : IDisposable
{
    internal static CameraRig? Current;
    private readonly VrTracking tracking;
    private readonly Dictionary<int, TrackedCamera> cameras = new();
    private readonly Harmony patches = new("xiii.vr.xrbootstrap.camera");
    private readonly CameraEffects effects = new();
    private ContactRig? contacts;
    private Camera? main;
    private Transform? localPlayerRoot;
    private Camera? menuCamera;
    private GameObject? menuCameraRoot;
    private readonly FrontendMenu frontend;
    private readonly StoryVideo movie=new();
    private PlayMagic.CustomCharacterController? storyCharacter;
    private Cinemachine.CinemachineBrain? storyBrain;
    private int storyPlayerId=-1,sceneHandle=int.MinValue;
    private StoryMode storyMode;
    private readonly List<Cutscene> storyScenes=new();
    private int storyBits=-1;
    private readonly VrMenuPointer menuPointer;
    private bool frontendMode;
    private GameObject? leftMarker, rightMarker;
    private Material? leftMaterial, rightMaterial;
    private float nextDiscover, nextReport;
    private int preparedFrame = -1;
    private int controlFrame = -1;
    private bool referenceSet, recenter = true, buttonReadFailed, markersDisabled, effectsUnavailable;
    private readonly TrackingRecovery recovery=new();
    private bool failed=>!recovery.Active;
    private PoseValue reference;
    private readonly BodyAnchor bodyAnchor = new();
    private Transform? anchorRoot;
    private float eyeHeight;
    private UVector anchorPosition;
    private UQuat anchorRotation;
    internal UVector HeadPosition;
    internal UQuat HeadRotation;
    internal bool Prepared;
    internal bool Scripted { get; private set; }
    internal bool Frontend=>main==null;
    internal bool MovieActive=>movie.Active;
    internal Camera? MainCamera => Frontend||MovieActive?menuCamera:main;
    private readonly StorySkipLatch skipInput=new();
    private string endedStoryCamera="";
    private void SkipStoryInput()
    {
        var input=MenuRightControls;
        if(!skipInput.Sample(Scripted&&WindowFocus.Playable&&input.Valid,(input.Held&HandControls.Trigger)!=0))return;
        if(movie.Skip()){DisarmTrigger();return;}
        // A checkpoint or Timeline track may play a director without StartCutscene.
        // 0.1.162: the kept scenes first; a fresh search only when none of them plays.
        DiscoverStoryScenes();
        if(!storyScenes.Exists(k=>k!=null&&k.director!=null&&k.director.state==UnityEngine.Playables.PlayState.Playing)){storyFind.RefreshNow();DiscoverStoryScenes();}
        foreach(var scene in storyScenes.ToArray())
        {
            var director=scene==null?null:scene.director;
            if(director==null||director.state!=UnityEngine.Playables.PlayState.Playing||!double.IsFinite(director.duration)||director.duration<=0)continue;
            // 0.1.93: fast-forward instead of jumping to the end. A jump
            // (time=duration + Evaluate) never fires the Timeline's signals,
            // so scripted follow-ups (an NPC walking on, the next cutscene)
            // did not start. At 20x game speed with the sound muted every
            // signal and end-of-cutscene callback runs as authored.
            StartFastForward(director,scene!.name);
            DisarmTrigger();return;
        }
        // 0.1.230: a story Timeline that is no Cutscene (the memory's opening in
        // the last mission, Camera_blink_cs&seq_seq_01_01): the director behind
        // the story camera shown, else the story director playing longest.
        var other=StoryDirector(out string from);
        if(other!=null){StartFastForward(other,other.name+" ("+from+")");fastFallback=true;DisarmTrigger();return;}
        if(Time.realtimeSinceStartup>=nextSkipMiss){nextSkipMiss=Time.realtimeSinceStartup+5;Bootstrap.Write("STORY skip: no story timeline playing to fast-forward (camera "+(storyBrain?.ActiveVirtualCamera?.Name??"none")+")");}
    }
    private float nextSkipMiss;
    private void StartFastForward(UnityEngine.Playables.PlayableDirector director,string name)
    {
        if(fastDirector!=null)return;
        fastDirector=director;fastFallback=false;fastSince=Time.realtimeSinceStartup;fastSaved=Time.timeScale>0?Time.timeScale:1;Time.timeScale=Math.Min(100,fastSaved*FastSpeed);
        try{FMODUnity.RuntimeManager.GetBus("bus:/").setMute(true);fastMuted=true;}catch(Exception ex){Bootstrap.Warn("STORY skip mute: "+ex.Message);}
        StoryAudioSkip.Begin();
        Bootstrap.Write("STORY skip requested via right trigger: "+name+" fast-forward x"+FastSpeed+" remaining="+(director.duration-director.time).ToString("F1")+"s");
    }
    // A director a skip can fast-forward: playing to an end on game time (not looping, not run by hand), with time left.
    private static bool Skippable(UnityEngine.Playables.PlayableDirector? d)
    {
        try
        {
            return d!=null&&d.isActiveAndEnabled&&d.state==UnityEngine.Playables.PlayState.Playing&&StorySkipPolicy.Skippable(d.duration,d.time,(int)d.extrapolationMode,(int)d.timeUpdateMode);
        }
        catch(Exception){return false;}
    }
    private UnityEngine.Playables.PlayableDirector? StoryDirector(out string from)
    {
        from="";
        Transform? camera=null;
        try{camera=storyBrain?.ActiveVirtualCamera?.VirtualCameraGameObject?.transform;}catch(Exception){camera=null;}
        // The story camera's own Timeline: on it or above it.
        for(var t=camera;t!=null;t=t.parent)
        {
            UnityEngine.Playables.PlayableDirector? d=null;
            try{d=t.GetComponent(Il2CppType.Of<UnityEngine.Playables.PlayableDirector>())?.TryCast<UnityEngine.Playables.PlayableDirector>();}catch(Exception){}
            if(Skippable(d)){from="the story camera's timeline";return d;}
        }
        // Else the playing story director with the most time left (its scene's first).
        UnityEngine.Playables.PlayableDirector? best=null;double left=0;bool sameScene=false;
        try
        {
            foreach(var obj in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<UnityEngine.Playables.PlayableDirector>()))
            {
                var d=obj.TryCast<UnityEngine.Playables.PlayableDirector>();if(!Skippable(d)||d!.duration-d.time>180)continue;
                bool same=camera!=null&&d!.gameObject.scene.handle==camera.gameObject.scene.handle;double remaining=d!.duration-d.time;
                if(best==null||same&&!sameScene||same==sameScene&&remaining>left){best=d;left=remaining;sameScene=same;}
            }
        }
        catch(Exception ex){Bootstrap.Warn("STORY skip search: "+ex.Message);}
        if(best!=null)from=sameScene?"a story timeline in the story camera's scene":"a story timeline playing";
        return best;
    }
    private const float FastSpeed=20;
    private UnityEngine.Playables.PlayableDirector? fastDirector;private float fastSince,fastSaved=1;private bool fastMuted;
    // A director found without a Cutscene: its fast-forward also ends when the story hands control back.
    private bool fastFallback;
    // Played to its end: stopped, or held at its last frame (a Hold Timeline stays "playing").
    private static bool Finished(UnityEngine.Playables.PlayableDirector? d)
    {
        try{return d==null||d.state!=UnityEngine.Playables.PlayState.Playing||StorySkipPolicy.HeldAtEnd(d.duration,d.time,(int)d.extrapolationMode);}
        catch(Exception){return true;}
    }
    private void TickStoryFastForward()
    {
        if(fastDirector==null&&!fastMuted)return;
        try
        {
            var director=fastDirector;
            bool playing=!Finished(director)&&!(fastFallback&&!Scripted);
            bool ours=Math.Abs(Time.timeScale-Math.Min(100,fastSaved*FastSpeed))<.01f;
            float elapsed=Time.realtimeSinceStartup-fastSince;
            if(playing&&ours&&elapsed<20){StoryAudioSkip.Tick();return;}
            if(playing&&ours&&director!=null&&double.IsFinite(director.duration))
            {
                // Timeline not driven by game time: the old jump as a fallback.
                director.time=director.duration;director.Evaluate();if(director.state==UnityEngine.Playables.PlayState.Playing)director.Stop();
                Bootstrap.Warn("STORY fast-forward timed out; jumped to the end");
            }
            if(ours)Time.timeScale=fastSaved;
            Bootstrap.Write("STORY fast-forward finished after "+elapsed.ToString("F1")+"s real time; timeScale="+Time.timeScale);
        }
        catch(Exception ex){Bootstrap.Warn("STORY fast-forward: "+ex.Message);if(Math.Abs(Time.timeScale-fastSaved*FastSpeed)<.01f)Time.timeScale=fastSaved;}
        finally{if(Finished(fastDirector)||fastFallback&&!Scripted||Math.Abs(Time.timeScale-Math.Min(100,fastSaved*FastSpeed))>=.01f)EndFastForward();}
    }
    private void EndFastForward()
    {
        if(fastDirector!=null)StoryAudioSkip.End();
        fastDirector=null;fastFallback=false;
        if(fastMuted){try{FMODUnity.RuntimeManager.GetBus("bus:/").setMute(false);}catch(Exception ex){Bootstrap.Warn("STORY skip unmute: "+ex.Message);}fastMuted=false;}
    }
    private float nextStoryScan,nextInspect;
    // 0.1.162: the game's story scenes, kept between rare searches (every
    // 15 s since 0.1.164, and at once after a scene change); the kept ones are checked as
    // often as the search ran before (1-2 times a second).
    private readonly SceneFind<Cutscene> storyFind=new("story scenes",15,found=>
    {
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Cutscene>()))
        {var c=obj.TryCast<Cutscene>();if(c!=null&&c.gameObject.scene.IsValid())found.Add(c);}
    });
    private void DiscoverStoryScenes()
    {
        foreach(var scene in storyFind.Items)
        {
            if(scene==null||!scene.gameObject.activeInHierarchy||scene.director==null)continue;
            if(scene.director.state==UnityEngine.Playables.PlayState.Playing&&!storyScenes.Exists(k=>k!=null&&k.Pointer==scene.Pointer)){storyScenes.Add(scene);storyExiting=false;}
        }
    }
    private bool storyExiting;
    private static void StoryUiTransition(bool intoCutscene)
    {
        var rig=Current;if(rig==null)return;
        rig.storyExiting=!intoCutscene;rig.preparedFrame=-1;
        if(!intoCutscene)rig.endedStoryCamera=rig.storyBrain?.ActiveVirtualCamera?.Name??"";
        Bootstrap.Write("STORY UI transition into="+intoCutscene);
    }
    private void SampleCameraMode()
    {
        bool broad=GameGlobals.Instance!=null&&GameGlobals.Instance.cutsceneFlags!=null&&GameGlobals.Instance.cutsceneFlags.isWatchingCutsceneOrInFlashBack;
        bool timeline=GameInputManager.inputLockedBasedOnTimeline;
        bool inputLock=storyPlayerId>=0&&GameInputManager.IsInputLocked(storyPlayerId);
        bool axisLock=storyPlayerId>=0&&GameInputManager.IsAxisLocked(storyPlayerId);
        bool restricted=storyCharacter!=null&&storyCharacter.restrictMovement;
        bool menu=PauseMenuControl.HackGameIsPaused||QualityMenu.Open||ControlsSheet.Open||GameUiControls.Current?.WheelOpen==true;
        storyScenes.RemoveAll(c=>c==null);
        // Native story cameras can stay live after the Timeline lock is cleared.
        // The new beach log shows this for Camera_blink_cs&seq_seq_02.
        string activeCamera=storyBrain?.ActiveVirtualCamera?.Name??"";
        if(activeCamera!=endedStoryCamera)endedStoryCamera="";
        bool authored=!storyExiting&&!menu&&activeCamera!=endedStoryCamera&&StoryModePolicy.AuthoredCamera(activeCamera);
        foreach(var scene in storyScenes)
            if(!storyExiting&&scene.director!=null&&scene.director.state==UnityEngine.Playables.PlayState.Playing){authored=true;break;}
        var mode=StoryModePolicy.Select(!Frontend,MovieActive,broad,timeline,inputLock,axisLock,restricted,menu,authored);
        // TransitionOut can run its decorative director for another three seconds.
        // Keep native locks untouched; return the camera/surround immediately.
        if(storyExiting&&!MovieActive)mode=broad?StoryMode.PlayableFlashback:StoryMode.Gameplay;
        bool scripted=mode==StoryMode.Cinematic||mode==StoryMode.Movie;
        if(storyMode!=mode)
        {
            bool returning=Scripted&&!scripted;
            storyMode=mode;Scripted=scripted;preparedFrame=-1;cutsceneView.Reset();screenSet=false;
            if(returning){recenter=true;bodyAnchor.Reset();WeaponHands.Current?.OnRelocated();}
            if(scripted)GameUiControls.Current?.CancelTransient();
        }
        int bits=(int)mode|(broad?8:0)|(timeline?16:0)|(inputLock?32:0)|(axisLock?64:0)|(restricted?128:0);
        if(bits!=storyBits)
        {
            storyBits=bits;
            Bootstrap.Write("STORY MODE "+mode+" broad="+broad+" timeline="+timeline+" authored="+authored+" inputLock="+inputLock+" axisLock="+axisLock+" restricted="+restricted+" player="+storyPlayerId+" camera="+(main==null?"none":main.name)+" virtual="+(storyBrain?.ActiveVirtualCamera?.Name??"none"));
        }
    }
    // 0.1.234: the steady cutscene view (CutsceneView; VR SETTINGS "Cutscene camera").
    private readonly CutsceneView cutsceneView=new();
    private float nextCutReport;
    // 0.1.238: where the cutscene's frame and subtitles are drawn (CinematicMask,
    // FrontendMenu): turned as the film camera on the screen that stands
    // still, else with the head as before.
    internal UVector CinemaPosition{get;private set;}
    internal UQuat CinemaRotation{get;private set;}=UQuat.identity;
    // The cutscene is on the screen that stands still (its surround a closed box).
    internal bool CinemaScreen{get;private set;}
    private bool screenSet;private System.Numerics.Quaternion screenFacing=System.Numerics.Quaternion.Identity;private int cinemaModeSeen=-1;
    private void ReadCinematicPose()
    {
        ReadFilmPose();
        int mode=QualityOptions.CutsceneMode;CinemaScreen=false;
        if(mode!=cinemaModeSeen){cinemaModeSeen=mode;screenSet=false;cutsceneView.Reset();}
        if(mode==0&&tracking.HeadValid)
        {
            var screenFilm=new System.Numerics.Quaternion(HeadRotation.x,HeadRotation.y,HeadRotation.z,HeadRotation.w);
            var screenHead=tracking.Head.Rotation;
            if(!screenSet){screenSet=true;screenFacing=CutsceneScreen.Facing(screenHead);Bootstrap.Write("STORY cutscene on a screen that stands still: in front of the head's heading "+CutsceneView.YawDegrees(screenHead).ToString("F0")+" degrees; the film turns its camera, the head looks about the screen");}
            var view=CutsceneScreen.View(screenFilm,screenFacing,screenHead);
            CinemaPosition=HeadPosition;CinemaRotation=HeadRotation;CinemaScreen=true;
            HeadRotation=new UQuat(view.X,view.Y,view.Z,view.W);
            return;
        }
        if(mode!=1||!tracking.HeadValid){if(cutsceneView.Active){cutsceneView.Reset();Bootstrap.Write("STORY cutscene view: the film camera's (VR SETTINGS)");}CinemaPosition=HeadPosition;CinemaRotation=HeadRotation;return;}
        var film=new System.Numerics.Vector3(HeadPosition.x,HeadPosition.y,HeadPosition.z);
        var filmTurn=new System.Numerics.Quaternion(HeadRotation.x,HeadRotation.y,HeadRotation.z,HeadRotation.w);
        var head=tracking.Head;
        var (p,q)=cutsceneView.View(film,filmTurn,head.Position,head.Rotation);
        HeadPosition=new UVector(p.X,p.Y,p.Z);HeadRotation=new UQuat(q.X,q.Y,q.Z,q.W);
        CinemaPosition=HeadPosition;CinemaRotation=HeadRotation;
        if(cutsceneView.CutThisCall&&Time.realtimeSinceStartup>=nextCutReport)
        {
            nextCutReport=Time.realtimeSinceStartup+2;
            Bootstrap.Write("STORY cutscene view steady: "+(cutsceneView.Cuts==1?"first shot":"cut "+cutsceneView.Cuts+" (the film camera jumped "+cutsceneView.LastJumpMeters.ToString("F2")+" m, "+cutsceneView.LastJumpDegrees.ToString("F0")+" degrees)")
                +"; the view turned to the film camera's heading "+CutsceneView.YawDegrees(filmTurn).ToString("F0")+" degrees; its tilt "+FilmTilt(filmTurn)+" not followed; the head turns the view");
        }
    }
    private static string FilmTilt(System.Numerics.Quaternion q)
    {
        var f=System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,q);
        return (MathF.Asin(Math.Clamp(f.Y,-1,1))*180/MathF.PI).ToString("F0")+" degrees";
    }
    private void ReadFilmPose()
    {
        if(storyBrain!=null&&storyBrain.isActiveAndEnabled&&StoryModePolicy.AuthoredCamera(storyBrain.ActiveVirtualCamera?.Name??""))
        {
            // Use Cinemachine's authored output for explicit story cameras;
            // the tracked camera transform may still carry a gameplay anchor.
            var state=storyBrain.CurrentCameraState;
            HeadPosition=state.FinalPosition;HeadRotation=state.FinalOrientation;return;
        }
        // The actual output transform includes both Cinemachine and the
        // authored CameraRig/CameraEffects animation (collapse, head tilt).
        // CurrentCameraState alone discards downstream animation offsets.
        if(main!=null&&cameras.TryGetValue(main.GetInstanceID(),out var native))
        {native.GetBasePose(out HeadPosition,out HeadRotation);return;}
        if(main!=null){HeadPosition=main.transform.position;HeadRotation=main.transform.rotation;}
    }
    private static void StartedStory(Cutscene __instance)
    {
        var rig=Current;if(rig==null||__instance==null)return;
        if(!rig.storyScenes.Exists(k=>k!=null&&k.Pointer==__instance.Pointer))rig.storyScenes.Add(__instance);
        rig.preparedFrame=-1;rig.endedStoryCamera="";rig.storyExiting=false;
        Bootstrap.Write("STORY authored timeline registered: "+__instance.name);
    }
    private static void StoppedStory(Cutscene __instance)
    {
        var rig=Current;if(rig==null)return;
        if(__instance!=null)rig.storyScenes.RemoveAll(k=>k==null||k.Pointer==__instance.Pointer);rig.endedStoryCamera=rig.storyBrain?.ActiveVirtualCamera?.Name??"";rig.preparedFrame=-1;rig.recenter=true;
        rig.bodyAnchor.Reset();
    }

    private static void StoryRelocated()
    {
        var rig=Current;if(rig==null)return;
        try
        {
            rig.recenter=true;rig.bodyAnchor.Reset();rig.preparedFrame=-1;
            WeaponHands.Current?.OnRelocated();rig.DisarmTrigger();
            Bootstrap.Write("STORY relocation: VR origin follows native player placement");
        }
        catch(Exception ex){Bootstrap.Warn("Story relocation reset: "+ex.Message);}
    }
    private void BindStoryCamera()
    {
        var root=PlayerRoot;
        storyCharacter=root==null?null:root.GetComponent(Il2CppType.Of<PlayMagic.CustomCharacterController>())?.TryCast<PlayMagic.CustomCharacterController>();
        storyBrain=main==null?null:main.GetComponent(Il2CppType.Of<Cinemachine.CinemachineBrain>())?.TryCast<Cinemachine.CinemachineBrain>();
        storyPlayerId=-1;
        if(storyCharacter!=null){var owner=storyCharacter.GetOwner();if(owner.IsPlayer&&!owner.IsInvalid)storyPlayerId=owner.Id;}
    }
    private int sceneCount=-1;
    private void CheckScene()
    {
        int next=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        // 0.1.162: a scene loaded or unloaded (also added to or taken from the
        // level) - the kept scene searches (SceneFind) run again.
        int count=UnityEngine.SceneManagement.SceneManager.sceneCount;
        if(count!=sceneCount||next!=sceneHandle){sceneCount=count;SceneScan.SceneChanged();}
        if(next==sceneHandle)return;
        recovery.SceneChanged();frontend.SceneChanged();controlFrame=-1;buttonReadFailed=false;
        sceneHandle=next;storyExiting=false;storyScenes.Clear();StoryColorEffect.SceneChanged();nextDiscover=0;preparedFrame=-1;Prepared=false;recenter=true;bodyAnchor.Reset();
        try{GameUiControls.Current?.OnSceneChanged();}catch(Exception ex){Bootstrap.Warn("STORY UI cleanup: "+ex.Message);}
        try{WeaponHands.Current?.OnSceneChanged();}catch(Exception ex){Bootstrap.Warn("STORY weapon cleanup: "+ex.Message);}
        Bootstrap.Write("STORY SCENE changed handle="+next+"; transient VR state cleared, native locks unchanged");
    }
    // 0.1.233: the eyes as drawn, for the world scale (VR SETTINGS).
    internal PoseValue EyeLeft => PoseMath.ScaledEye(tracking.EyeLeft, QualityOptions.WorldScaleValue);
    internal PoseValue EyeRight => PoseMath.ScaledEye(tracking.EyeRight, QualityOptions.WorldScaleValue);
    internal bool HasEyeOffsets => tracking.HasEyeOffsets;
    // 0.1.237: how far apart the eyes are drawn (STEREO CHECK).
    internal float EyeDistanceDrawn => System.Numerics.Vector3.Distance(EyeLeft.Position, EyeRight.Position);
    internal EyeFrustum FrustumLeft => tracking.FrustumLeft;
    internal EyeFrustum FrustumRight => tracking.FrustumRight;
    internal HandControls LeftControls => tracking.LeftControls;
    internal HandControls RightControls => tracking.RightControls;
    internal HandControls MenuRightControls => tracking.MenuRightControls;
    internal HandControls MenuLeftControls => tracking.MenuLeftControls;
    // 0.1.155: the menus' pointer comes from the hand whose trigger
    // was pulled last (at first: the left for a left-hander); that pull clicks
    // where that hand points.
    private bool pointerLeft,pointerChosen;
    internal bool PointerLeft
    {
        get
        {
            if(!pointerChosen){pointerChosen=true;pointerLeft=WeaponHands.LeftHanded;}
            var l=MenuLeftControls;var r=MenuRightControls;
            bool leftPull=l.Valid&&(l.Down&HandControls.Trigger)!=0,rightPull=r.Valid&&(r.Down&HandControls.Trigger)!=0;
            if(leftPull&&!rightPull&&!pointerLeft){pointerLeft=true;Bootstrap.Write("MENU POINTER now the left hand (its trigger)");}
            else if(rightPull&&!leftPull&&pointerLeft){pointerLeft=false;Bootstrap.Write("MENU POINTER now the right hand (its trigger)");}
            return pointerLeft&&tracking.LeftValid&&l.Valid||!tracking.RightValid&&tracking.LeftValid&&l.Valid;
        }
    }
    internal HandControls MenuPointerControls=>PointerLeft?MenuLeftControls:MenuRightControls;
    // The pointing direction of the menu hand (the left one: the aim mirrored).
    internal UQuat PointerRotation(PoseValue hand)=>PointerLeft?ControllerAim.Mirrored(hand):ControllerAim.Rotation(hand);
    // A menu click: neither trigger fires a gun after it until it is let go.
    internal void DisarmMenuTriggers(){tracking.DisarmTrigger();tracking.DisarmLeftTrigger();}
    internal void DisarmLeftTrigger()=>tracking.DisarmLeftTrigger();
    internal StickSample LeftStick => tracking.LeftStick;
    internal StickSample RightStick => tracking.RightStick;
    internal bool HeadTrackingValid => !failed && tracking.HeadValid;
    internal bool TryWorldHeadRotation(out System.Numerics.Quaternion rotation)
    {
        rotation = System.Numerics.Quaternion.Identity;
        if (failed || Scripted || main == null || !referenceSet) return false;
        PollControls();
        if (!tracking.HeadValid) return false;
        if (!ReadBodyAnchor(out _,out var baseRotation)) return false;
        var relative = PoseMath.Relative(tracking.Head,reference);
        var q = baseRotation * UnityRotation(relative);
        rotation = new System.Numerics.Quaternion(q.x,q.y,q.z,q.w);
        return true;
    }
    internal void DisarmTrigger() => tracking.DisarmTrigger();
    internal void LeftShotHaptics()=>tracking.LeftShotHaptics();
    internal void RopeHaptics()=>tracking.RopeHaptics();
    internal void ShotHaptics(string profile,bool support) => tracking.ShotHaptics(profile,support);
    internal bool PhysicalHand(bool right,out System.Numerics.Vector3 position)
    {
        position=(right?tracking.Right:tracking.Left).Position;
        return HeadTrackingValid&&(right?tracking.RightValid:tracking.LeftValid);
    }
    // 0.1.133: rightReloads: the right hand reloads (the gun in the left hand).
    internal void ReloadHaptics(ReloadAction action,bool rightReloads=false)=>tracking.ReloadHaptics(action,rightReloads);
    internal void ResistanceHaptics(float amplitude,bool right=false)=>tracking.ResistanceHaptics(amplitude,right);
    internal void PunchHaptics(bool right)=>tracking.PunchHaptics(right);
    internal void SwimHaptics(float amplitude)=>tracking.SwimHaptics(amplitude);
    internal bool PhysicalHead(out System.Numerics.Vector3 position){position=tracking.Head.Position;return HeadTrackingValid;}
    internal bool PhysicalHeadPose(out PoseValue head){head=tracking.Head;return HeadTrackingValid;}
    internal void TestHaptics()=>tracking.TestHaptics();
    internal void ResetHandAlignment()
    {
        WeaponHands.Current?.ResetHandAlignment();
        ContactRig.Current?.ResetGun();ContactRig.Current?.ResetHand(false);ContactRig.Current?.ResetHand(true);
        ResetRenderCaches();RequestRecenter();
    }
    internal void CancelHaptics() => tracking.CancelHaptics();
    internal void CancelShotHaptics() => tracking.CancelShotHaptics();
    private bool freezeWatchFailed;
    internal Transform? PlayerRoot
    {
        get
        {
            return localPlayerRoot==null?null:localPlayerRoot;
        }
    }
    internal bool SampleWorldHands(out PoseValue left, out PoseValue right, out bool leftValid)
    {
        left = right = default; leftValid = false;
        if (failed || Scripted || main == null || !referenceSet) return false;
        tracking.RefreshPoses();
        if (!tracking.HeadValid || !tracking.RightValid) return false;
        if (!ReadBodyAnchor(out var p,out var q)) return false;
        right = WorldHand(tracking.Right, p, q);
        leftValid = tracking.LeftValid;
        if (leftValid) left = WorldHand(tracking.Left, p, q);
        return true;
    }
    // Right controller in the tracking reference (unaffected by locomotion and
    // stick turns) plus the rotation that maps it into the world.
    internal bool SampleRightRelative(out UVector position,out UQuat toWorld)
    {
        position=default;toWorld=UQuat.identity;
        if(failed||Scripted||main==null||!referenceSet)return false;
        tracking.RefreshPoses();
        if(!tracking.HeadValid||!tracking.RightValid||!ReadBodyAnchor(out _,out var q))return false;
        position=UnityPosition(PoseMath.Relative(tracking.Right,reference));toWorld=q;return true;
    }
    // 0.1.126: the left controller in the tracking reference (a grenade thrown
    // from the left hand).
    internal bool SampleLeftRelative(out UVector position,out UQuat toWorld)
    {
        position=default;toWorld=UQuat.identity;
        if(failed||Scripted||main==null||!referenceSet)return false;
        tracking.RefreshPoses();
        if(!tracking.HeadValid||!tracking.LeftValid||!ReadBodyAnchor(out _,out var q))return false;
        position=UnityPosition(PoseMath.Relative(tracking.Left,reference));toWorld=q;return true;
    }
    // 0.1.241: a hand in the room (the tracking space, recentered): not moved by
    // walking, turning, the body's crouch or the head (reload flicks).
    internal bool SampleTrackedHand(bool right,out PoseValue hand)
    {
        hand=default;
        if(failed||!referenceSet)return false;
        tracking.RefreshPoses();
        if(right?!tracking.RightValid:!tracking.LeftValid)return false;
        hand=PoseMath.Relative(right?tracking.Right:tracking.Left,reference);return true;
    }
    internal bool SamplePointerHand(out PoseValue hand)
    {
        hand=default;PollControls();
        bool left=PointerLeft;
        if(failed||!referenceSet||!tracking.HeadValid||!(left?tracking.LeftValid:tracking.RightValid))return false;
        var pose=left?tracking.Left:tracking.Right;
        if(Frontend){hand=WorldHand(pose,new UVector(0,1.6f,0),UQuat.identity);return true;}
        if(!ReadBodyAnchor(out var p,out var q))return false;
        hand=WorldHand(pose,p,q);return true;
    }
    private PoseValue WorldHand(PoseValue pose, UVector p, UQuat q)
    {
        var relative = PoseMath.Relative(pose, reference);
        var wp = p + q * UnityPosition(relative); var wq = q * UnityRotation(relative);
        return new PoseValue(new System.Numerics.Vector3(wp.x,wp.y,wp.z), new System.Numerics.Quaternion(wq.x,wq.y,wq.z,wq.w));
    }
    // 0.1.83: while a body or hostage is carried, the character turns with
    // the physically turned head (the native carry places the NPC from the
    // character's own yaw; head yaw alone made it jump). The requested turn
    // is fed through the stick-turn input; once the character has actually
    // turned, the tracking reference turns by the same angle about the head,
    // so the view and the hands do not move.
    private float pendingCarryTurn,lastRootYaw=float.NaN,carryCompensated;
    internal float CarryFollowTurn(float dt)
    {
        // The NPC now follows the rendered HMD directly. No synthetic stick
        // turn or feedback through the tracking reference is needed.
        pendingCarryTurn=0;
        if(GripCarry.Current?.CarriesNpc==true)CarryDiagnostics();
        return 0;
    }
    // 0.1.88 diagnostics: where the carried NPC hangs in the hierarchy, and
    // any frame where it jumps relative to the character (the reported
    // "teleport" on a physical turn).
    private Transform? carryReported,carryHips;private UVector carryLast,carryLastHead;private bool carryHave;private int carryJumps;private float carryLastYaw,carryMaxHead,carryMaxRoot;
    private void CarryDiagnostics()
    {
        try
        {
            var body=GripCarry.Current?.BodyRoot;var root=PlayerRoot;if(body==null||root==null){carryHave=false;return;}
            if(body!=carryReported)
            {
                carryReported=body;carryJumps=0;carryHips=null;
                string Chain(Transform? t){string c="";for(;t!=null&&c.Length<400;t=t.parent)c+=t.name+"<";return c;}
                foreach(var c in body.GetComponentsInChildren(Il2CppInterop.Runtime.Il2CppType.Of<SkinnedMeshRenderer>(),true))
                {var r=c.TryCast<SkinnedMeshRenderer>();if(r?.rootBone!=null){carryHips=r.rootBone;break;}}
                var spawn=GripCarry.Current?.SpawnBone;
                Bootstrap.Write("CARRY BODY "+body.name+" parents="+Chain(body.parent)+" hips="+Chain(carryHips)+" spawnBone="+Chain(spawn)+" camera="+(main!=null?Chain(main.transform):"none"));
                foreach(var c in (spawn!=null?spawn.root:root).GetComponentsInChildren(Il2CppInterop.Runtime.Il2CppType.Of<Animator>(),true))
                {var a=c.TryCast<Animator>();if(a!=null)Bootstrap.Write("CARRY ANIMATOR "+a.name+" culling="+a.cullingMode+" enabled="+a.isActiveAndEnabled+" update="+a.updateMode);}
            }
            var probe=carryHips??body;
            var local=root.InverseTransformPoint(probe.position);
            var headYaw=Quaternion.Euler(0,HeadRotation.eulerAngles.y,0);
            var head=Quaternion.Inverse(headYaw)*(probe.position-HeadPosition);
            float yaw=root.eulerAngles.y;
            if(carryHave){carryMaxHead=Math.Max(carryMaxHead,(head-carryLastHead).magnitude);carryMaxRoot=Math.Max(carryMaxRoot,(local-carryLast).magnitude);}
            if(carryHave&&((local-carryLast).magnitude>.15f||(head-carryLastHead).magnitude>.15f)&&carryJumps++<60)
                Bootstrap.Write("CARRY JUMP root="+carryLast.ToString("F2")+"->"+local.ToString("F2")+" head="+carryLastHead.ToString("F2")+"->"+head.ToString("F2")
                    +" rootYaw="+yaw.ToString("F1")+" dYaw="+Mathf.DeltaAngle(carryLastYaw,yaw).ToString("F2")+" headYaw="+HeadRotation.eulerAngles.y.ToString("F1")+" pending="+pendingCarryTurn.ToString("F2")+" frame="+Time.frameCount);
            carryLast=local;carryLastHead=head;carryLastYaw=yaw;carryHave=true;
        }
        catch(Exception ex){Bootstrap.Warn("CARRY diagnostics: "+ex.Message);carryHave=false;}
    }
    // 0.1.94: physical crouch. The standing head height is the height at the
    // last recenter, raised when the player stands taller for 2 s. Crouching
    // 35 cm below it crouches the character; rising to 25 cm below stands it
    // up again (LocomotionDriver injects the native crouch input).
    private float standingHead=float.NaN,tallerSince=-1;private int crouchRecenter=-1;
    internal bool PhysicalCrouch {get;private set;}
    internal float StandingEyeHeight {get;private set;}
    private bool eyeHold;private float eyeHoldSince=-1;
    private int recenterCount;
    private void UpdatePhysicalCrouch()
    {
        if(!tracking.HeadValid||!referenceSet||Scripted||QualityOptions.PhysicalCrouch?.Value==false){PhysicalCrouch=false;return;}
        float y=tracking.Head.Position.Y;
        if(crouchRecenter!=recenterCount||!float.IsFinite(standingHead)){crouchRecenter=recenterCount;standingHead=y;tallerSince=-1;PhysicalCrouch=false;return;}
        float now=Time.realtimeSinceStartup;
        if(y>standingHead+.03f){if(tallerSince<0)tallerSince=now;else if(now-tallerSince>2){standingHead=y;tallerSince=-1;}}else tallerSince=-1;
        float drop=standingHead-y;
        bool was=PhysicalCrouch;
        // A drop of a metre or more is not crouching (headset off / lying).
        PhysicalCrouch=drop<1f&&(PhysicalCrouch?drop>.25f:drop>.35f);
        if(PhysicalCrouch!=was)Bootstrap.Write("PHYSICAL CROUCH "+(PhysicalCrouch?"down":"up")+" drop="+drop.ToString("F2")+" standing="+standingHead.ToString("F2"));
    }
    private void CompensateCarryTurn(float rootYaw)
    {
        float last=lastRootYaw;lastRootYaw=rootYaw;
        if(!float.IsFinite(last))return;
        float delta=Mathf.DeltaAngle(last,rootYaw);if(Math.Abs(delta)<1e-4f)return;
        // 0.1.94: while carrying, EVERY character turn without the stick is
        // compensated (a native turn spread over several frames was only
        // compensated in its first frame before), and the request shrinks by
        // what was actually applied.
        // 0.1.119: also on a mounted gun — the seat turns with the gun, the VR
        // view stays where it is (the player turns physically).
        if(GripCarry.Current?.CarriesNpc!=true&&MountedGunVr.Current?.Mounted!=true){pendingCarryTurn=0;return;}
        float stick=LocomotionDriver.Current?.LastStickTurn??0;
        if(Math.Abs(stick)>=1e-4f&&pendingCarryTurn==0)return;
        float turn=Math.Abs(stick)<1e-4f?delta:Math.Clamp(pendingCarryTurn,-Math.Abs(delta),Math.Abs(delta));
        if(pendingCarryTurn!=0&&Math.Sign(pendingCarryTurn)==Math.Sign(delta))
        {float applied=Math.Min(Math.Abs(delta),Math.Abs(pendingCarryTurn))*Math.Sign(pendingCarryTurn);pendingCarryTurn-=applied;if(Math.Abs(pendingCarryTurn)<.01f)pendingCarryTurn=0;}
        carryCompensated+=turn;
        TurnReference(turn);
    }
    // The tracking reference turned about the head (the view keeps its
    // direction while the body under it turns by the same angle).
    private void TurnReference(float degrees)
    {
        var a=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,degrees*MathF.PI/180);
        var head=tracking.Head.Position;var offset=reference.Position-head;
        var flat=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(offset.X,0,offset.Z),a);
        reference=new PoseValue(head+new System.Numerics.Vector3(flat.X,offset.Y,flat.Z),System.Numerics.Quaternion.Normalize(a*reference.Rotation));
    }
    // 0.1.140: on a mounted gun the view stays exactly where it was when the
    // gun was taken.
    // The game turns the seat (and the player's body on it) with the gun and
    // carries it round the gun's pivot; 0.1.119 kept only the direction, so
    // the view still swung sideways by up to a couple of metres.
    // 0.1.142:
    // the view is fixed to the gun: it turns with the gun about its pivot,
    // exactly by the gun's turn (no sway or lag of its own), never tilting or
    // rising with the barrel (the game's seat did). VR config
    // MountedGunViewTurns=false: still, as in 0.1.140.
    private bool mountFrozen,mountTurns;private Vector3 frozenMountPosition,mountPivot;private float frozenMountYaw,mountHeading,mountViewYaw;
    private void HoldMountView(ref Vector3 p,ref Quaternion q,Transform root)
    {
        bool mounted=MountedGunVr.Current?.Mounted==true&&!Scripted;
        float yaw=root.eulerAngles.y;
        if(mounted)
        {
            if(!mountFrozen)
            {
                mountFrozen=true;frozenMountPosition=p;frozenMountYaw=yaw;mountViewYaw=yaw;
                // 0.1.150:
                // taking the gun recenters the view - the head where it is now
                // becomes the seat behind the gun, looking along it.
                recenter=true;Bootstrap.Write("MOUNTED GUN view recentered: the head now is the seat behind the gun, looking along it");
                // Only when steered by the handles (measured in the tracking
                // space): aimed where the controllers point, a turning view
                // would turn the pointing with it and run away.
                mountTurns=QualityOptions.MountedViewTurns?.Value!=false&&QualityOptions.MountedHandles?.Value!=false&&MountedGunVr.Current!.TryHeading(out mountPivot,out mountHeading);
                Bootstrap.Write((mountTurns?"MOUNTED GUN view fixed to the gun: it turns with it about its pivot (not tilting with the barrel)"
                    :"MOUNTED GUN view held where it was taken (the seat turns and moves with the gun, the view does not)")+"; raised "+(QualityOptions.MountRaise*100).ToString("F0")+" cm");
            }
            var at=frozenMountPosition;float turn=0;
            if(mountTurns&&MountedGunVr.Current!.TryHeading(out var pivot,out var heading))
            {turn=Mathf.DeltaAngle(mountHeading,heading);at=pivot+Quaternion.Euler(0,turn,0)*(frozenMountPosition-mountPivot);}
            mountViewYaw=frozenMountYaw+turn;
            // 0.1.146: raised above the gun.
            p=at+Vector3.up*QualityOptions.MountRaise;q=Quaternion.Euler(0,mountViewYaw,0);lastRootYaw=yaw;
            return;
        }
        if(!mountFrozen)return;
        mountFrozen=false;
        // Off the gun: the body's real direction again, the view keeps its own.
        float back=Mathf.DeltaAngle(mountViewYaw,yaw);
        TurnReference(back);lastRootYaw=yaw;
        Bootstrap.Write("MOUNTED GUN view follows the body again (turned "+back.ToString("F0")+" deg; the gun had turned "+Mathf.DeltaAngle(frozenMountYaw,mountViewYaw).ToString("F0")+" deg)");
    }
    private Camera? mountCamera;private Transform? mountRoot,cachedMount;
    private bool ReadBodyAnchor(out UVector position,out UQuat rotation)
    {
        position=default; rotation=UQuat.identity;
        var root=PlayerRoot;
        if(root==null || main==null || !referenceSet || !tracking.HeadValid) return false;
        if(anchorRoot!=root) { anchorRoot=root; bodyAnchor.Reset(); bodyAlignmentFailed=false; mountFrozen=false; }
        // CameraRig.localPosition.y is the game's standing/crouching eye height.
        // Do not sample the child camera's position or its CameraEffects rotation.
        // 0.1.121: the "CameraRig" parent is looked up once per camera/body
        // (a name read per parent level, on 30+ hand/head samples a frame).
        Transform? mount=mountCamera==main&&mountRoot==root&&cachedMount!=null&&main.transform.IsChildOf(cachedMount)?cachedMount:null;
        if(mount==null)
        {
            mount=main.transform.parent;
            while(mount!=null && mount!=root && mount.name!="CameraRig") mount=mount.parent;
            mountCamera=main;mountRoot=root;cachedMount=mount!=null&&mount!=root?mount:null;
        }
        if(mount==null || mount==root) return false;
        eyeHeight=mount.localPosition.y;
        if(!float.IsFinite(eyeHeight) || eyeHeight<.05f || eyeHeight>4f) return false;
        // A crouch made by the real body already lowered the real head; the
        // native crouch camera drop would lower the view a second time.
        // Held until the native camera is back at standing height after the
        // real body stood up (the native stand-up animates for a moment).
        var loco=LocomotionDriver.Current;bool owned=loco?.PhysicalCrouchOwned==true;
        if(owned){eyeHold=true;eyeHoldSince=-1;}
        else if(eyeHold)
        {
            if(eyeHoldSince<0)eyeHoldSince=Time.realtimeSinceStartup;
            if(eyeHeight>=StandingEyeHeight-.01f||Time.realtimeSinceStartup-eyeHoldSince>3)eyeHold=false;
        }
        if(!eyeHold&&loco?.NativeCrouching!=true)StandingEyeHeight=eyeHeight;
        if(eyeHold&&StandingEyeHeight>eyeHeight)eyeHeight=StandingEyeHeight;
        var p=root.position; var q=root.rotation;
        HoldMountView(ref p,ref q,root);
        if(!mountFrozen)CompensateCarryTurn(root.eulerAngles.y);
        var relative=PoseMath.Relative(tracking.Head,reference);
        var anchor=bodyAnchor.Sample(new System.Numerics.Vector3(p.x,p.y,p.z),
            new System.Numerics.Quaternion(q.x,q.y,q.z,q.w),eyeHeight,relative.Position);
        position=UnityPosition(anchor); rotation=UnityRotation(anchor); return true;
    }
    private bool bodyAlignmentFailed;
    private int alignedFrame=-1;
    private float nextBodyReport;
    internal void AlignCollisionBody(PlayMagic.CustomCharacterController character)
    {
        if(bodyAlignmentFailed || alignedFrame==Time.frameCount || Scripted || character.isMounted || character.isDoingZipline
            || character.restrictMovement || character.IsSpawning)return;
        var state=character.CurrentPlayerState;
        if(state<PlayMagic.CustomCharacterController.PlayerStates.Idling || state>PlayMagic.CustomCharacterController.PlayerStates.Falling)return;
        alignedFrame=Time.frameCount;
        var controller=character.controller;
        if(controller==null || !controller.enabled || !ReadBodyAnchor(out _,out var yaw))return;
        var relative=PoseMath.Relative(tracking.Head,reference);
        var gap=bodyAnchor.BodyGap(new System.Numerics.Quaternion(yaw.x,yaw.y,yaw.z,yaw.w),relative.Position);
        float distance=gap.Length();if(!float.IsFinite(distance)||distance<.003f)return;
        // Bounded sweep through the original collision capsule, never teleport
        // or disable a collider. Run in Update, never in an eye-render callback.
        // 0.1.94: follow the head smoothly (a fraction per frame) instead of a
        // full catch-up whenever the gap passed 1.5 cm. Those catch-ups moved
        // the character — and a carried hostage — in visible ~2 cm hops while
        // turning on the spot (the head circles the neck).
        float follow=Math.Clamp(Time.deltaTime*10,.05f,1);
        var request=gap*follow;float length=request.Length();if(length>.08f)request*=.08f/length;
        var before=character.transform.position;
        try{controller.Move(new UVector(request.X,0,request.Z));}
        catch(Exception ex){bodyAlignmentFailed=true;Bootstrap.Warn("BODY ALIGN disabled until player rebind; stick movement remains active: "+ex);return;}
        var actual=character.transform.position-before;
        bodyAnchor.ConsumeBodyMove(new System.Numerics.Vector3(actual.x,0,actual.z));
        if(Time.realtimeSinceStartup>=nextBodyReport)
        {
            nextBodyReport=Time.realtimeSinceStartup+5;
            Bootstrap.Write("BODY ALIGN gap="+gap+" accepted="+actual+" collider="+controller.name+" radius="+controller.radius);
        }
    }
    internal CameraRig()
    {
        // 0.1.181: the mod's own OpenXR when it runs, else the OpenVR way.
        tracking = OpenXrLoader.Current?.State==OpenXrLoader.Phase.Running ? new OpenXrTracking() : new OpenVrTracking();
        CompositorTiming.Source=tracking.FrameTimingReport;
        frontend=new FrontendMenu(this);menuPointer=new VrMenuPointer(this);
        Current = this;
        try{contacts=new ContactRig(this);}catch(Exception ex){Bootstrap.Warn("COLLISION setup unavailable: "+ex);}
        try { _=new GameUiControls(this); } catch(Exception ex) { Bootstrap.Warn("VR menu unavailable: "+ex); }
        try { _=new WristHud(this); } catch(Exception ex) { Bootstrap.Warn("Wrist HUD unavailable: "+ex); }
        try { settingsPage=new VrSettingsPage(this); } catch(Exception ex) { Bootstrap.Warn("VR settings menu item unavailable: "+ex); }
        death=new DeathScreenVr(this);splash=new WaterSplash(this);enemies=new EnemyAi(this);mountedGun=new MountedGunVr(this);
        try
        {
            foreach(string method in new[]{nameof(FlashbackSequence.TeleportPlayerStart),nameof(FlashbackSequence.TeleportPlayerEnd)})
                patches.Patch(AccessTools.Method(typeof(FlashbackSequence),method),postfix:new HarmonyMethod(typeof(CameraRig),nameof(StoryRelocated)));
            patches.Patch(AccessTools.Method(typeof(TeleportPlayerWithExtraCamera),nameof(TeleportPlayerWithExtraCamera.TeleportPlayer)),postfix:new HarmonyMethod(typeof(CameraRig),nameof(StoryRelocated)));
            patches.Patch(AccessTools.Method(typeof(Cutscene),nameof(Cutscene.StartCutscene)),postfix:new HarmonyMethod(typeof(CameraRig),nameof(StartedStory)));
            patches.Patch(AccessTools.Method(typeof(CutsceneUI),nameof(CutsceneUI.PlayTransition)),postfix:new HarmonyMethod(typeof(CameraRig),nameof(StoryUiTransition)));
            patches.Patch(AccessTools.Method(typeof(Cutscene),nameof(Cutscene.OnTimelineStop)),postfix:new HarmonyMethod(typeof(CameraRig),nameof(StoppedStory)));
        }
        catch(Exception ex){Bootstrap.Warn("STORY timeline observation: "+ex.Message);}
        SpotIndicators.Install();
        // 0.1.159: the sound and the hit direction strip follow the head too.
        HeadListener.Install();DamageIndicators.Install();
        // 0.1.162: new movie players, death screens and weapon pickups handed over by the game.
        SceneHooks.Install();
        try
        {
            patches.Patch(AccessTools.PropertySetter(typeof(Camera), nameof(Camera.fieldOfView)),
                prefix: new HarmonyMethod(typeof(CameraRig), nameof(AllowGameFovWrite)));
            Bootstrap.Write("CAMERA FOV guard installed for tracked player cameras.");
        }
        catch (Exception ex) { Bootstrap.Warn("FOV guard unavailable; head tracking will continue. " + ex); }
    }
    // The headset projection defines FOV. Do not let Cinemachine/CameraSettingsCloner
    // repeatedly write a desktop FOV to these XR cameras (29,798 warnings in the log).
    private static bool AllowGameFovWrite(Camera __instance)
    {
        try { return Current == null || !Current.cameras.ContainsKey(__instance.GetInstanceID()); }
        catch { return true; }
    }
    internal void Tick()
    {
        if (recovery.Stopped) return;
        try
        {
            // Scene/checkpoint loading must still be observed after a failure.
            CheckScene();
            bool retry=recovery.Suspended;
            if(!recovery.Ready(Time.realtimeSinceStartup))return;
            if(retry)
            {
                preparedFrame=controlFrame=-1;nextDiscover=0;buttonReadFailed=false;
                Bootstrap.Write("HEAD TRACKING retry after transient failure");
            }
            FramePerformance.Tick();
            // 0.1.149: what stops, when something stops (FreezeWatch).
            try{FreezeWatch.Tick(this);}catch(Exception ex){if(!freezeWatchFailed){freezeWatchFailed=true;Bootstrap.Warn("FREEZE WATCH off: "+ex.Message);}}
            movie.Tick();
            if(!MovieActive)movieScreenSet=false;
            if(Frontend||MovieActive)EnsureMenuCamera();
            if(menuCamera!=null)menuCamera.enabled=Frontend||MovieActive;
            float now = Time.realtimeSinceStartup;
            if (now >= nextDiscover) { nextDiscover = now + 1; Discover(); }
            PollControls();
            SampleCameraMode();SkipStoryInput();TickStoryFastForward();UpdatePhysicalCrouch();
            StoryColorEffect.Tick();
            PrepareUiPose();
            frontend.Tick();
            settingsPage?.Tick();
            death?.Tick();splash?.Tick();enemies?.Tick();mountedGun?.Tick();
            GameUiControls.Current?.Tick();
            WristHud.Current?.Tick();
            tracking.TickHaptics(WindowFocus.Playable && !PauseMenuControl.HackGameIsPaused && GameUiControls.Current?.BlocksGameplay!=true);
            if (now >= nextReport)
            {
                nextReport = now + 5;
                tracking.RefreshEyes();
                Bootstrap.Write("TRACKING head=" + tracking.HeadValid + " left=" + tracking.LeftValid + " right=" + tracking.RightValid + " leftInput=" + tracking.LeftInput + " rightInput=" + tracking.RightInput + " cameras=" + cameras.Count + " eyeDistance=" + tracking.EyeDistance.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " worldScale=" + QualityOptions.WorldScaleValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " drawnEyeDistance=" + (tracking.EyeDistance / QualityOptions.WorldScaleValue).ToString("F4", System.Globalization.CultureInfo.InvariantCulture)+(tracking is OpenXrTracking?"; "+OpenXrLoader.ViewsReport():""));
                if(referenceSet && anchorRoot!=null)
                {
                    var relative=PoseMath.Relative(tracking.Head,reference);
                    Bootstrap.Write("BODY ANCHOR root="+anchorRoot.position+" yawOnly="+anchorRotation.eulerAngles+" eyeHeight="+eyeHeight+" turnOffset="+bodyAnchor.TurnOffset+" headRelative="+relative.Position+" headAngles="+UnityRotation(relative).eulerAngles);
                }
            }
        }
        catch (Exception ex) { Fail(ex); }
    }
    internal void ResetRenderCaches(){preparedFrame=-1;WristHud.Current?.InvalidatePose();WeaponHands.Current?.InvalidateRenderPose();DisarmTrigger();}
    internal void PinCanvas(Canvas canvas)=>frontend.Own(canvas);
    // 0.1.121: the screen UI was re-posed on every camera callback (both
    // pre-cull and pre-render of every tracked camera, plus the wrist HUD):
    // about 14 times a frame. Once in LateUpdate and once after the
    // pre-render head pose are enough (the pose is fixed for the frame).
    private int canvasFrame=-1,canvasSerial=-1,prepareSerial;
    // Changes whenever the head pose of the frame may have changed: a new
    // frame, or another pre-render pass in the same frame.
    internal (int frame,int serial) RenderPhase=>(Time.frameCount,prepareSerial);
    internal void RenderCanvases()
    {
        var (frame,serial)=RenderPhase;if(frame==canvasFrame&&serial==canvasSerial)return;canvasFrame=frame;canvasSerial=serial;
        frontend.Render();movie.Render(this);settingsPage?.Render();
        DamageIndicators.Tick(this);
    }
    private VrSettingsPage? settingsPage;
    private DeathScreenVr? death;private WaterSplash? splash;private EnemyAi? enemies;private MountedGunVr? mountedGun;
    // 0.1.107: the player's world camera is drawing (not switched off by the
    // death screen).
    internal bool GameplayCameraRendering=>main!=null&&main.isActiveAndEnabled;
    internal void AttachTrackedCamera(Camera camera)
    {
        var component=camera.gameObject.AddComponent(Il2CppType.Of<TrackedCamera>()).TryCast<TrackedCamera>()!;
        cameras.Add(camera.GetInstanceID(),component);component.Configure(this,camera);
    }
    internal void RecenterUi()=>frontend.Recenter();
    internal void MenuLateUpdate()
    {
        if(failed)return;
        try{PrepareUiPose();RenderCanvases();menuPointer.Tick();}
        catch(Exception ex){Bootstrap.Warn("VR menu presentation: "+ex.Message);}
    }
    private void PrepareUiPose()
    {
        if(!tracking.HeadValid)return;
        if(recenter||!referenceSet)
        {
            reference=tracking.Head;referenceSet=true;recenter=false;bodyAnchor.Reset();recenterCount++;
            frontend.Recenter();movieScreenSet=false;
            Bootstrap.Write("RECENTER applied (position and yaw). Head tracking active.");
        }
        if(Scripted&&!MovieActive&&main!=null){ReadCinematicPose();return;}
        var relative=PoseMath.Relative(tracking.Head,reference);
        if(Frontend||MovieActive){anchorPosition=new UVector(0,1.6f,0);anchorRotation=UQuat.identity;}
        else if(!ReadBodyAnchor(out anchorPosition,out anchorRotation))return;
        HeadPosition=anchorPosition+anchorRotation*UnityPosition(relative);HeadRotation=anchorRotation*UnityRotation(relative);
        if(MovieActive)PoseMovieScreen();
    }
    // 0.1.245: the game's story movies (the comic panels after the bank) on a
    // screen that stands still in the room, as the cutscenes since 0.1.238
    // (VR SETTINGS "Cutscene camera": screen or steady): in front of where the
    // head faced when the movie began, upright; turning or tilting the head
    // looks about it. The movie's subtitles and skip prompt (FrontendMenu) are
    // drawn on it. "The film's" keeps it in front of the head, as before. The
    // movie screen followed the head and tilted with it; the subtitles were put
    // where the last cutscene's screen had been.
    private bool movieScreenSet;private UVector movieScreenAt;private UQuat movieScreenTurn=UQuat.identity;
    private void PoseMovieScreen()
    {
        CinemaScreen=false;
        if(QualityOptions.CutsceneMode==2){movieScreenSet=false;CinemaPosition=HeadPosition;CinemaRotation=HeadRotation;return;}
        if(!movieScreenSet)
        {
            movieScreenSet=true;movieScreenAt=HeadPosition;
            float yaw=CutsceneView.YawDegrees(new System.Numerics.Quaternion(HeadRotation.x,HeadRotation.y,HeadRotation.z,HeadRotation.w));
            movieScreenTurn=UQuat.Euler(0,yaw,0);
            Bootstrap.Write("STORY VIDEO on a screen that stands still: in front of the head's heading "+yaw.ToString("F0")+" degrees, upright");
        }
        CinemaPosition=movieScreenAt;CinemaRotation=movieScreenTurn;
    }
    internal void PollControls()
    {
        if (failed || buttonReadFailed || controlFrame == Time.frameCount) return;
        controlFrame = Time.frameCount;
        try { tracking.RefreshPoses(); tracking.ReadButtons(); }
        catch (Exception ex) { buttonReadFailed = true; tracking.InvalidateControls(); Bootstrap.Warn("Controller input unavailable; camera tracking continues. " + ex); }
    }
    internal void RequestRecenter() { recenter = true; Bootstrap.Write("RECENTER requested"); }
    internal void ToggleEffects()
    {
        try { StoryColorEffect.Enabled=!StoryColorEffect.Enabled;effects.Toggle(); effectsUnavailable = false; nextDiscover = 0;Bootstrap.Write("STORY COLOR enabled="+StoryColorEffect.Enabled); }
        catch (Exception ex) { Bootstrap.Warn("Effect toggle failed; tracking continues. " + ex); }
    }
    internal void GuardStereoEffects(Camera camera) => effects.BeforeRender(camera);
    private static PlayMagic.CustomCharacterController? LocalCharacter(Camera? camera)
    {
        if(camera==null)return null;
        var c=camera.GetComponentInParent(Il2CppType.Of<PlayMagic.CustomCharacterController>())?.TryCast<PlayMagic.CustomCharacterController>();
        if(c==null)return null;
        var owner=c.GetOwner();return owner.IsPlayer&&!owner.IsInvalid?c:null;
    }
    internal static bool NeedsStoryColor(Camera camera)=>LocalCharacter(camera)?.environmentCamera==camera||camera.name=="Camera - Enviroments";
    private static bool IsPlayerCamera(Camera camera)
    {
        var c=LocalCharacter(camera);if(c==null)return false;
        return c.environmentCamera==camera||camera.name=="Camera - Enviroments"||camera.name=="Camera - Visual noise"||camera.name=="UIRenderingCamera(Clone)";
    }
    private void Discover()
    {
        var stale = new List<int>();
        foreach (var pair in cameras) if (pair.Value == null || pair.Value.Target == null) stale.Add(pair.Key);
        foreach (int id in stale) cameras.Remove(id);
        Camera? foundMain=null;
        bool inspectDue=Time.realtimeSinceStartup>=nextInspect;if(inspectDue)nextInspect=Time.realtimeSinceStartup+5;
        foreach (var camera in Camera.allCameras)
        {
            if (camera == null || !IsPlayerCamera(camera)) continue;
            if (LocalCharacter(camera)?.environmentCamera==camera || camera.name == "Camera - Enviroments") foundMain = camera;
            int id = camera.GetInstanceID();
            bool known = cameras.ContainsKey(id);
            // Disable desktop projection writers before attaching the pose driver.
            // 0.1.121: a known camera's scripts are re-read every 5 s (a new
            // camera at once); the cached effects are switched off per render.
            if (!effectsUnavailable && (!known || inspectDue))
            {
                try { effects.Inspect(camera, !known); }
                catch (Exception ex) { effectsUnavailable = true; Bootstrap.Warn("Effect inspection failed; tracking continues. " + ex); }
            }
            if (known) continue;
            var component = camera.gameObject.AddComponent(Il2CppType.Of<TrackedCamera>()).TryCast<TrackedCamera>();
            if (component == null) throw new InvalidOperationException("Cannot attach pose driver to " + camera.name);
            // Register ownership before configuring so failure cleanup can find it.
            cameras.Add(id, component);
            component.Configure(this, camera);
            Bootstrap.Write("TRACKED CAMERA attached: " + camera.name);
        }
        if(foundMain==null&&main!=null&&PlayerRoot?.gameObject.activeInHierarchy==true)foundMain=main;
        if(main!=foundMain){main=foundMain;recenter=true;preparedFrame=-1;Prepared=false;}
        localPlayerRoot=LocalCharacter(main)?.transform;
        BindStoryCamera();
        // 0.1.121: a scene-wide search; never in a frame with another one.
        // 0.1.162: the kept story scenes; the search itself rarely.
        bool storySearched=storyFind.Refresh();
        if(storySearched||Time.realtimeSinceStartup>=nextStoryScan)
        {
            nextStoryScan=Time.realtimeSinceStartup+(Scripted?.5f:1f);
            try{DiscoverStoryScenes();}catch(Exception){storyFind.Prune();}
        }
        // 0.1.164: the graphics settings, once per level (GRAPHICS).
        if(!Frontend&&main!=null)GraphicsReport.Level(PlayerRoot);
        if(main==null)EnsureMenuCamera();
        if(menuCamera!=null)menuCamera.enabled=main==null||MovieActive;
        if(frontendMode!=Frontend){frontendMode=Frontend;recenter=true;Bootstrap.Write("VR VIEW "+(Frontend?"frontend menu":"gameplay camera"));}
        if (main == null) { Prepared = false; HideMarkers(); }
    }
    private void EnsureMenuCamera()
    {
        if(menuCamera!=null)return;
        menuCameraRoot=new GameObject("XIII VR frontend camera");UnityEngine.Object.DontDestroyOnLoad(menuCameraRoot);
        menuCamera=menuCameraRoot.AddComponent(Il2CppType.Of<Camera>()).TryCast<Camera>()!;
        menuCamera.clearFlags=CameraClearFlags.SolidColor;menuCamera.backgroundColor=new Color(.025f,.03f,.04f,1);
        menuCamera.depth=1000;menuCamera.cullingMask=1<<5;menuCamera.nearClipPlane=.02f;menuCamera.farClipPlane=50;
        menuCamera.stereoTargetEye=StereoTargetEyeMask.Both;
        var component=menuCameraRoot.AddComponent(Il2CppType.Of<TrackedCamera>()).TryCast<TrackedCamera>()!;
        cameras.Add(menuCamera.GetInstanceID(),component);component.Configure(this,menuCamera);
        Bootstrap.Write("FRONTEND stereo camera ready");
    }
    // 0.1.162: which step of a long pre-render pass took the time (PERF SLOW render).
    private readonly StepClock renderClock=new("render");
    internal bool Prepare()
    {
        if (failed || MainCamera == null) return false;
        if (preparedFrame == Time.frameCount) return Prepared;
        preparedFrame = Time.frameCount; Prepared = false; prepareSerial++;
        long partStart=FramePerformance.Begin(FramePerformance.Render);
        try
        {
            // Read just before culling, after Cinemachine's LateUpdate. Never wait
            // on compositor poses here: Unity's native provider owns that timing.
            tracking.RefreshPoses(true);
            if (!tracking.HeadValid) { HideMarkers(); return false; }
            if(Frontend||MovieActive){PrepareUiPose();Prepared=true;HideMarkers();RenderCanvases();return true;}
            SampleCameraMode();
            if(Scripted)
            {
                // Cinemachine/Timeline already wrote the authored camera pose in
                // LateUpdate. Keep it rather than anchoring to the spawn point.
                ReadCinematicPose();
                Prepared=true; HideMarkers();
                LocomotionDriver.Current?.BeforeRender();
                WeaponHands.Current?.RenderPose(true);
                WristHud.Current?.Render();
                return true;
            }
            if (recenter || !referenceSet)
            {
                reference = tracking.Head; referenceSet = true; recenter = false;
                bodyAnchor.Reset();recenterCount++;
                Bootstrap.Write("RECENTER applied (position and yaw). Head tracking active.");
            }
            if(!ReadBodyAnchor(out anchorPosition,out anchorRotation)) { HideMarkers(); return false; }
            var relative = PoseMath.Relative(tracking.Head, reference);
            HeadPosition = anchorPosition + anchorRotation * UnityPosition(relative);
            HeadRotation = anchorRotation * UnityRotation(relative);
            Prepared = true;
            var clock=renderClock;clock.Begin();
            GripCarry.Current?.RenderBody();clock.Mark("carry");
            LocomotionDriver.Current?.BeforeRender();clock.Mark("locomotion");
            WeaponHands.Current?.RenderPose(true);clock.Mark("weapons");
            mountedGun?.Render();clock.Mark("mounted gun");
            NpcHitReactions.Current?.BeforeCameras();clock.Mark("npc hits");
            WristHud.Current?.Render();clock.Mark("wrist");
            try { UpdateMarkers(); }
            catch (Exception ex) { markersDisabled = true; HideMarkers(); Bootstrap.Warn("Hand markers disabled; tracking continues. " + ex); }
            clock.End();
            return true;
        }
        catch (Exception ex) { Fail(ex); return false; }
        finally { FramePerformance.Part(FramePerformance.Render,partStart); }
    }
    internal static UVector UnityPosition(PoseValue pose) => new(pose.Position.X, pose.Position.Y, pose.Position.Z);
    internal static UQuat UnityRotation(PoseValue pose) => new(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W);
    private void UpdateMarkers()
    {
        if (markersDisabled) return;
        // 0.1.123: none on the mounted gun (the game's arms hold it).
        // 0.1.217: none for the hand riding the zipline (that hand is not drawn: no ball in its place).
        bool mounted=MountedGunVr.HidesHands;
        UpdateMarker(ref leftMarker, ref leftMaterial, "XIII VR left hand", new Color(0.05f, 0.7f, 1f, 1f), !mounted && tracking.LeftValid && GripCarry.Current?.HidesLeft!=true && ZiplineVr.Current?.HidesHand(false)!=true && WristHud.Current?.LeftHandVisible!=true, tracking.Left);
        UpdateMarker(ref rightMarker, ref rightMaterial, "XIII VR right hand", new Color(1f, 0.45f, 0.05f, 1f), !mounted && tracking.RightValid && GripCarry.Current?.HidesHand(true)!=true && ZiplineVr.Current?.HidesHand(true)!=true && WristHud.Current?.RightHandVisible!=true, tracking.Right);
    }
    private void UpdateMarker(ref GameObject? marker, ref Material? material, string name, Color color, bool valid, PoseValue pose)
    {
        if (!valid) { if (marker != null) marker.SetActive(false); return; }
        if (marker == null)
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.name = name;
            var collider = marker.GetComponent(Il2CppType.Of<Collider>())?.TryCast<Collider>();
            if (collider != null) { collider.enabled = false; UnityEngine.Object.Destroy(collider); }
            marker.transform.localScale = new UVector(0.035f, 0.035f, 0.035f);
            var shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var renderer = marker.GetComponent(Il2CppType.Of<Renderer>())?.TryCast<Renderer>();
            if (shader != null && renderer != null)
            {
                if (material != null) UnityEngine.Object.Destroy(material);
                material = new Material(shader); material.color = color; renderer.material = material;
            }
        }
        var relative = PoseMath.Relative(pose, reference);
        marker.transform.SetPositionAndRotation(anchorPosition + anchorRotation * UnityPosition(relative), anchorRotation * UnityRotation(relative));
        marker.SetActive(true);
    }
    private void HideMarkers() { if (leftMarker != null) leftMarker.SetActive(false); if (rightMarker != null) rightMarker.SetActive(false); }
    private void Fail(Exception ex)
    {
        bool report=recovery.Suspend(Time.realtimeSinceStartup);Prepared=false;
        // Cleanup failure is not allowed to escape and trigger Bootstrap's
        // permanent setup-failure path. Do not alter any native story locks.
        try{GameUiControls.Current?.CancelTransient();}
        catch(Exception cleanup){if(report)Bootstrap.Warn("Tracking transient cleanup: "+cleanup.Message);}
        if(report)Bootstrap.Warn("HEAD TRACKING interrupted; automatic retry in 0.5s, XR display kept running. "+ex);
        foreach (var camera in cameras.Values) if (camera != null) camera.Restore();
        try{HideMarkers();}catch(Exception cleanup){if(report)Bootstrap.Warn("Tracking marker cleanup: "+cleanup.Message);}
    }
    public void Dispose()
    {
        CompositorTiming.Source=null;
        recovery.Stop();Prepared=false;
        menuPointer.Dispose();movie.Dispose();frontend.Dispose();
        contacts?.Dispose();contacts=null;
        if (Current == this) Current = null;
        GameUiControls.Current?.Dispose(); WristHud.Current?.Dispose();
        try{settingsPage?.Dispose();}catch(Exception ex){Bootstrap.Warn("VR settings dispose: "+ex.Message);}settingsPage=null;ControlsSheet.Close();
        try{PromptIcons.Dispose();}catch(Exception ex){Bootstrap.Warn("VR prompt icons dispose: "+ex.Message);}
        try{death?.Dispose();}catch(Exception ex){Bootstrap.Warn("VR death screen dispose: "+ex.Message);}death=null;
        try{splash?.Dispose();}catch(Exception ex){Bootstrap.Warn("VR water splash dispose: "+ex.Message);}splash=null;
        try{enemies?.Dispose();}catch(Exception ex){Bootstrap.Warn("VR enemy AI dispose: "+ex.Message);}enemies=null;
        try{mountedGun?.Dispose();}catch(Exception ex){Bootstrap.Warn("VR mounted gun dispose: "+ex.Message);}mountedGun=null;
        foreach (var camera in cameras.Values)
        {
            try { if (camera != null) { camera.Release(); UnityEngine.Object.Destroy(camera); } }
            catch (Exception ex) { Bootstrap.Warn("Camera cleanup: " + ex.Message); }
        }
        cameras.Clear();
        if(menuCameraRoot!=null)UnityEngine.Object.Destroy(menuCameraRoot);menuCameraRoot=null;menuCamera=null;
        effects.Dispose();
        try { patches.UnpatchSelf(); } catch (Exception ex) { Bootstrap.Warn("FOV guard cleanup: " + ex.Message); }
        if (leftMarker != null) UnityEngine.Object.Destroy(leftMarker);
        if (rightMarker != null) UnityEngine.Object.Destroy(rightMarker);
        if (leftMaterial != null) UnityEngine.Object.Destroy(leftMaterial);
        if (rightMaterial != null) UnityEngine.Object.Destroy(rightMaterial);
        tracking.Dispose();
    }
}
public sealed class TrackedCamera : MonoBehaviour
{
    private CameraRig? owner;
    internal Camera? Target;
    private UVector savedPosition;
    private UQuat savedRotation;
    private bool applied, released, failed, reportFailed;
    private RenderTexture? savedTarget;
    private bool redirected;
    private float nextReport;
    private StoryColorEffect? storyColor;
    // 0.1.164: this camera's main-thread time (CameraTiming).
    private long renderStart;private int timingSlot=-1;
    public TrackedCamera(IntPtr pointer) : base(pointer) { }
    [HideFromIl2Cpp]
    internal void Configure(CameraRig rig, Camera camera)
    {
        owner = rig; Target = camera;
        try{timingSlot=CameraTiming.Slot(camera.name);}catch(Exception){timingSlot=-1;}
        try{if(CameraRig.NeedsStoryColor(camera))storyColor=new StoryColorEffect(camera);}
        catch(Exception ex){Bootstrap.Warn("STORY COLOR attach failed; tracking continues: "+ex.Message);}
        XRDevice.DisableAutoXRCameraTracking(camera, true);
        try { Bootstrap.Write("RENDER CAMERA " + camera.name + " path=" + camera.actualRenderingPath + " targetTexture=" + (camera.targetTexture == null ? "XR/backbuffer" : camera.targetTexture.name)+" depth="+camera.depth+" clear="+camera.clearFlags+" cullingMask=0x"+camera.cullingMask.ToString("X8")+" layers="+CameraLayers(camera.cullingMask)+" far="+camera.farClipPlane.ToString("F0")+" occlusion="+camera.useOcclusionCulling+" hdr="+camera.allowHDR+" msaa="+camera.allowMSAA); }
        catch (Exception ex) { Bootstrap.Warn("Render camera diagnostics unavailable: " + ex.Message); }
    }
    // 0.1.165: the layers a camera draws (names), for the log.
    [HideFromIl2Cpp]
    private static string CameraLayers(int mask)
    {
        var names=new System.Collections.Generic.List<string>();
        for(int i=0;i<32;i++)if((mask&(1<<i))!=0){var n=LayerMask.LayerToName(i);names.Add(string.IsNullOrEmpty(n)?i.ToString():n);}
        return names.Count==32?"all":string.Join(",",names);
    }
    [HideFromIl2Cpp]
    internal void GetBasePose(out UVector position, out UQuat rotation)
    {
        var t=Target!.transform; var parent=t.parent;
        position = applied ? (parent==null?savedPosition:parent.TransformPoint(savedPosition)) : t.position;
        rotation = applied ? (parent==null?savedRotation:parent.rotation*savedRotation) : t.rotation;
    }
    public void OnPreCull()
    {
        renderStart=System.Diagnostics.Stopwatch.GetTimestamp();
        try{CameraTiming.PreCull(Time.frameCount,Time.realtimeSinceStartup-Time.unscaledTime);}catch(Exception){}
        if (released || failed || Target == null || owner == null) return;
        try
        {
            Restore();
            storyColor?.KeepEnabled();
            owner.GuardStereoEffects(Target);
            if (!owner.Prepare()) return;
            savedPosition = Target.transform.localPosition; savedRotation = Target.transform.localRotation; applied = true;
            Target.transform.SetPositionAndRotation(owner.HeadPosition, owner.HeadRotation);
            ApplyEyes();
        }
        catch (Exception ex) { failed = true; Release(); Bootstrap.Warn("Camera tracking failed for " + Target?.name + ": " + ex); }
    }
    public void OnPreRender()
    {
        if (!applied || released || failed || Target == null || owner == null) return;
        try { owner.GuardStereoEffects(Target); ApplyEyes(); }
        catch (Exception ex) { failed = true; Release(); Bootstrap.Warn("Stereo pre-render failed: " + ex); }
    }
    // 0.1.238: the camera at the eye of the pass Unity is drawing (left or
    // right), turned as that eye. Unity's XR display draws both eyes from the
    // camera's own place and ignores the stereo view matrices set below: the
    // STEREO CHECK of 0.1.237 found the two pictures moved only by their
    // fields of view, the same at any eye distance - no depth, the world flat
    // and huge, the world scale doing nothing. The stereo matrices stay set
    // (any setup that does read them gets the same eyes).
    [HideFromIl2Cpp]
    private (UVector position, UQuat rotation) PassPose()
    {
        var o = owner!;
        if (o.HasEyeOffsets && QualityOptions.EyesPerPassOn)
        {
            var eye = Target!.stereoActiveEye;
            if (eye == Camera.MonoOrStereoscopicEye.Left || eye == Camera.MonoOrStereoscopicEye.Right)
            {
                var offset = eye == Camera.MonoOrStereoscopicEye.Left ? o.EyeLeft : o.EyeRight;
                return (o.HeadPosition + o.HeadRotation * CameraRig.UnityPosition(offset), o.HeadRotation * CameraRig.UnityRotation(offset));
            }
        }
        return (o.HeadPosition, o.HeadRotation);
    }
    [HideFromIl2Cpp]
    private void ApplyEyes()
    {
            if (Target == null || owner == null) return;
            var (eyePosition, eyeRotation) = PassPose();
            Target.transform.SetPositionAndRotation(eyePosition, eyeRotation);
            Target.ResetWorldToCameraMatrix();
            // Desktop supersampling may have assigned a monoscopic RT. The XR
            // provider supplies the per-eye render targets for these cameras.
            if (Target.targetTexture != null)
            {
                if (!redirected)
                {
                    savedTarget = Target.targetTexture; redirected = true;
                    Bootstrap.Write("RENDER detached desktop target from " + Target.name);
                }
                Target.targetTexture = null;
            }
            if (owner.HasEyeOffsets)
            {
                SetEye(Camera.StereoscopicEye.Left, owner.EyeLeft);
                SetEye(Camera.StereoscopicEye.Right, owner.EyeRight);
                SetProjection(Camera.StereoscopicEye.Left, owner.FrustumLeft);
                SetProjection(Camera.StereoscopicEye.Right, owner.FrustumRight);
            }
            owner.RenderCanvases();
            WristHud.Current?.Render();
            ReportStereo();
    }
    [HideFromIl2Cpp]
    private void ReportStereo()
    {
        if (reportFailed || Target == null) return;
        try
        {
            if (Time.realtimeSinceStartup >= nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 5;
                var l = Target.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
                var r = Target.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
                var pl = Target.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                var pr = Target.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                float delta = MathF.Abs(l.m03-r.m03)+MathF.Abs(l.m13-r.m13)+MathF.Abs(l.m23-r.m23);
                Bootstrap.Write(FormattableString.Invariant($"STEREO camera={Target.name} activeEye={Target.stereoActiveEye} viewDelta={delta:F5} Lproj=({pl.m00:F5},{pl.m11:F5},{pl.m02:F5},{pl.m12:F5}) Rproj=({pr.m00:F5},{pr.m11:F5},{pr.m02:F5},{pr.m12:F5})"));
            }
        }
        catch (Exception ex) { reportFailed = true; Bootstrap.Warn("Stereo diagnostics disabled; tracking continues. " + ex.Message); }
    }
    [HideFromIl2Cpp]
    private void SetProjection(Camera.StereoscopicEye eye, EyeFrustum frustum)
    {
        var m = ProjectionMath.Build(frustum, Target!.nearClipPlane, Target.farClipPlane);
        var p = new Matrix4x4();
        p.m00 = m.M11; p.m02 = m.M13;
        p.m11 = m.M22; p.m12 = m.M23;
        p.m22 = m.M33; p.m23 = m.M34; p.m32 = m.M43;
        Target.SetStereoProjectionMatrix(eye, p);
    }
    [HideFromIl2Cpp]
    private void SetEye(Camera.StereoscopicEye eye, PoseValue offset)
    {
        var position = owner!.HeadPosition + owner.HeadRotation * CameraRig.UnityPosition(offset);
        var rotation = owner.HeadRotation * CameraRig.UnityRotation(offset);
        // Unity camera space looks down -Z. Preserve native eye offsets and canting.
        var view = Matrix4x4.Scale(new UVector(1, 1, -1)) * Matrix4x4.TRS(position, rotation, UVector.one).inverse;
        Target!.SetStereoViewMatrix(eye, view);
    }
    public void OnPostRender() { Restore(); CameraTiming.Add(timingSlot,renderStart); renderStart=0; try{CameraTiming.PostRender(Time.frameCount,Time.realtimeSinceStartup-Time.unscaledTime);}catch(Exception){} }
    public void OnDisable() { Restore(); ResetStereo(); }
    public void OnDestroy() { Release(); }
    [HideFromIl2Cpp]
    internal void Restore()
    {
        if (!applied) return;
        applied = false;
        try
        {
            if (Target != null)
            {
                // Parent may have moved between callbacks. Restoring stale WORLD
                // coordinates would alter the camera's local offset every frame.
                Target.transform.localPosition=savedPosition; Target.transform.localRotation=savedRotation;
                // Keep the same eye matrices throughout both render passes and
                // image effects. Only reset them when disabling/releasing VR.
                Target.ResetWorldToCameraMatrix();
            }
        }
        catch (Exception ex) { Bootstrap.Warn("Camera pose restore: " + ex.Message); }
    }
    [HideFromIl2Cpp]
    private void ResetStereo()
    {
        try { if (Target != null) { Target.ResetStereoViewMatrices(); Target.ResetStereoProjectionMatrices(); Target.ResetProjectionMatrix(); } }
        catch (Exception ex) { Bootstrap.Warn("Stereo cleanup: " + ex.Message); }
    }
    [HideFromIl2Cpp]
    internal void Release()
    {
        storyColor?.Dispose();storyColor=null;
        if (released) return;
        released = true; Restore(); ResetStereo();
        try { if (redirected && Target != null && savedTarget != null) Target.targetTexture = savedTarget; }
        catch (Exception ex) { Bootstrap.Warn("Render target restore: " + ex.Message); }
        try { if (Target != null) XRDevice.DisableAutoXRCameraTracking(Target, false); }
        catch (Exception ex) { Bootstrap.Warn("Camera tracking restore: " + ex.Message); }
        owner = null;
    }
}
