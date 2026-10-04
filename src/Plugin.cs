using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using UnityEngine.XR;
using BepInEx.Unity.IL2CPP.Utils.Collections;
namespace XiiiXR;
[BepInPlugin("xiii.vr.xrbootstrap", "XIII XR Bootstrap", "0.1.232")]
[BepInProcess("XIII.exe")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Output = null!;
    internal static bool TrackingDriverAvailable;
    public override void Load()
    {
        Output = Log;
        Log.LogInfo("[XIII-XR] Bootstrap 0.1.232 loaded. left stick=head-relative move; right X=turn; right up=jump; right down=crouch. F3=performance; F9=start; F10=stop; F11=recenter; F6/F12=capture; F7=stereo guard; F8=weapon controls; Shift+F5=aim calibration after 3 seconds; right grip=pick up; other grip at a long gun's muzzle + let go of the handle=held by the barrel (a club); right grip+A=doors; grip=physical door hold. Render-only tracked weapons; right trigger=fire; right B=reload (by hand: the other grip takes rounds at the belt and works the bolt); grappling or zipline hook from the wheel=left hand (right for a left-hander; the zipline hook by its handle, as a pistol), the other grip at it takes it over; rope: left stick climbs, right swings, L3 lets go (fired from the right hand: mirrored, R3 lets go); hold right A=wheel; left stick=select; left grip+X=Escape (either first); menu pointer=the hand whose trigger was pulled last (either trigger clicks). Left-handed (VR settings): left grip=pick up, left grip+X=doors, keys and medkits in the left hand, right grip+A=Escape (either first).");
        LocomotionOptions.Load(Config);
        WeaponOptions.Load(Config);
        QualityOptions.Load(Config);
        EnemyOptions.Load(Config);
        RuntimeOptions.Load(Config);
        ChairSolveCache.FilePath=System.IO.Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-grip-cache.txt");
        StartupLogos.Install();
        // 0.1.191: Russian back among the game's voice languages (only selectable, not chosen);
        // 0.1.192: and, once chosen, the Russian banks really replace the English ones.
        AudioLanguages.Install();
        ControllerAim.Load(Config);
        try
        {
            Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<TrackedCamera>();
            TrackingDriverAvailable = true;
        }
        catch (Exception ex) { Log.LogWarning("[XIII-XR] Camera driver registration failed; base XR remains available. " + ex); }
        AddComponent<Bootstrap>();
    }
}
public sealed class Bootstrap : MonoBehaviour
{
    private XRDisplaySubsystem? display;
    private XRInputSubsystem? input;
    // 0.1.181: the mod's own OpenXR (OpenXrLoader) while it starts or runs.
    private OpenXrLoader? openxr;private bool openXrGaveUp;private float openXrRunningSince=-1;
    private bool active;
    private bool attempted;
    private bool inputPollingFailed;
    private bool diagnosticsDisabled;
    private float nextLog;
    private int qualityRevision;
    private CameraRig? rig;
    private bool rigFailed;
    private LocomotionDriver? locomotion;
    private bool locomotionFailed;
    private WeaponHands? weapons;
    private bool weaponsFailed;
    private InteractionDriver? interaction;
    private bool interactionFailed;
    private float calibrateAt=-1,nextAutoCheck,nextAdapterRetry;
    public Bootstrap(IntPtr p) : base(p) { }
    public void Update()
    {
        StartupFocus.Tick(active);
        // 0.1.149: the game goes on without Windows focus in VR (WindowFocus).
        try{WindowFocus.Tick(active,FreezeWatch.HeadMoving);}catch(Exception ex){Warn("FOCUS: "+ex.Message);}
        // Startup, input polling and optional diagnostics have separate error
        // boundaries. An interop failure while reporting state must not tear
        // down a successfully started display (the 0.1.2 regression).
        bool startPressed = false;
        bool stopPressed = false;
        bool recenterPressed = false;
        bool screenshotPressed = false;
        bool effectsPressed = false;
        bool weaponsPressed = false;
        bool calibratePressed = false;
        if (!inputPollingFailed)
        {
            try
            {
                if(Input.GetKeyDown(KeyCode.F2))QualityMenu.Toggle();
                if(QualityMenu.Open&&Input.GetKeyDown(KeyCode.Return))QualityMenu.Apply();
                stopPressed = Input.GetKeyDown(KeyCode.F10);
                startPressed = Input.GetKeyDown(KeyCode.F9);
                recenterPressed = Input.GetKeyDown(KeyCode.F11);
                screenshotPressed = Input.GetKeyDown(KeyCode.F12) || Input.GetKeyDown(KeyCode.F6);
                effectsPressed = Input.GetKeyDown(KeyCode.F7);
                weaponsPressed = Input.GetKeyDown(KeyCode.F8);
                // 0.1.157: Shift+F5 (F5 alone is often the game's quick save: an
                // aim calibration started by it took the controller in any pose).
                calibratePressed = Input.GetKeyDown(KeyCode.F5)&&(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
            }
            catch (Exception ex)
            {
                inputPollingFailed = true;
                Warn("INPUT polling disabled; current XR state preserved. " + ex);
            }
        }
        if (stopPressed) { attempted=true; StopXR("F10"); return; }
        if(startPressed)attempted=false;
        if(!attempted && QualityOptions.AutoStart.Value && Time.realtimeSinceStartup>=nextAutoCheck)
        {nextAutoCheck=Time.realtimeSinceStartup+1;try{if(Time.realtimeSinceStartup>=2&&QualityMenu.StartupReady()){startPressed=true;Write("AUTO START initial camera ready; frontend VR enabled");}}catch(Exception ex){Warn("AUTO START waiting: "+ex.Message);}}
        if (startPressed && !attempted)
        {
            attempted = true;
            try { StartXR(); }
            catch (Exception ex)
            {
                Warn("START ERROR " + ex);
                StopXR("startup exception");
            }
        }
        if(openxr!=null)TickOpenXr();
        if (active && !rigFailed && Plugin.TrackingDriverAvailable)
        {
            try
            {
                rig ??= new CameraRig();
                if (recenterPressed) rig.RequestRecenter();
                if (effectsPressed) rig.ToggleEffects();
                long partStart=FramePerformance.Begin(FramePerformance.Rig);
                try { rig.Tick(); } finally { FramePerformance.Part(FramePerformance.Rig,partStart); }
            }
            catch (Exception ex)
            {
                rigFailed = true;
                Warn("TRACKING setup failed; XR display kept running. " + ex);
                try { interaction?.Dispose(); } catch (Exception cleanup) { Warn("Interaction cleanup after tracking failure: " + cleanup.Message); }
                interaction = null;
                try { weapons?.Dispose(); } catch (Exception cleanup) { Warn("Weapon cleanup after tracking failure: " + cleanup.Message); }
                weapons = null;
                try { locomotion?.Dispose(); } catch (Exception cleanup) { Warn("Locomotion cleanup after tracking failure: " + cleanup.Message); }
                locomotion = null;
                try { rig?.Dispose(); } catch (Exception cleanup) { Warn("Tracking cleanup: " + cleanup.Message); }
                rig = null;
            }
        }
        if(active&&rig!=null&&Time.realtimeSinceStartup>=nextAdapterRetry)
        {nextAdapterRetry=Time.realtimeSinceStartup+5;locomotionFailed=weaponsFailed=interactionFailed=false;}
        if (active && rig != null && (!rig.Frontend||locomotion!=null) && !locomotionFailed)
        {
            try
            {
                locomotion ??= new LocomotionDriver(rig);
                long partStart=FramePerformance.Begin(FramePerformance.Move);
                try { locomotion.Tick(); } finally { FramePerformance.Part(FramePerformance.Move,partStart); }
            }
            catch (Exception ex)
            {
                locomotionFailed = true;
                Warn("LOCOMOTION setup failed; head tracking and stereo remain running. " + ex);
                try { locomotion?.Dispose(); } catch (Exception cleanup) { Warn("Locomotion cleanup: " + cleanup.Message); }
                locomotion = null;
            }
        }
        if (active && rig != null && (!rig.Frontend||weapons!=null) && !weaponsFailed)
        {
            try
            {
                weapons ??= new WeaponHands(rig);
                if (weaponsPressed) weapons.Toggle();
                long partStart=FramePerformance.Begin(FramePerformance.Weapons);
                try { weapons.Tick(); } finally { FramePerformance.Part(FramePerformance.Weapons,partStart); }
            }
            catch (Exception ex)
            {
                weaponsFailed = true;
                Warn("WEAPON setup failed; locomotion and XR remain running. " + ex);
                try { weapons?.Dispose(); } catch (Exception cleanup) { Warn("Weapon cleanup: " + cleanup.Message); }
                weapons = null;
            }
        }
        if (active && rig != null && (!rig.Frontend||interaction!=null) && !interactionFailed)
        {
            try { interaction ??= new InteractionDriver(rig); long partStart=FramePerformance.Begin(FramePerformance.Interact); try { interaction.Tick(); } finally { FramePerformance.Part(FramePerformance.Interact,partStart); } }
            catch (Exception ex)
            {
                interactionFailed=true;
                Warn("INTERACTION setup failed; locomotion, weapons and XR remain running. " + ex);
                try { interaction?.Dispose(); } catch (Exception cleanup) { Warn("Interaction cleanup: "+cleanup.Message); }
                interaction=null;
            }
        }
        if (active && rig != null)
        {
            string menuAction=QualityMenu.TakeAction();
            try
            {
                if(menuAction=="recenter"){rig.RequestRecenter();rig.DisarmTrigger();}
                if(menuAction=="reset"){ControllerAim.Reset();rig.ResetHandAlignment();rig.DisarmTrigger();calibrateAt=-1;}
                if(menuAction=="haptics")rig.TestHaptics();
                if(menuAction=="calibrate")calibratePressed=true;
            }
            catch(Exception ex){Warn("VR recovery action failed; XR kept running: "+menuAction+" "+ex.Message);}
            if(!Application.isFocused){calibrateAt=-1;calibratePressed=false;}
            if(calibratePressed) { calibrateAt=Time.realtimeSinceStartup+3; rig.DisarmTrigger(); Write("AIM CALIBRATION in 3 seconds: hold right controller naturally forward and look horizontally forward."); }
            if(calibrateAt>=0 && Time.realtimeSinceStartup>=calibrateAt)
            {
                calibrateAt=-1;
                try { ControllerAim.Calibrate(rig);rig.ResetHandAlignment(); }
                catch(Exception ex) { Warn("AIM CALIBRATION skipped; current aim preserved. "+ex.Message); }
            }
        }
        if(active&&display!=null)
        {
            QualityMenu.UpdateDisplay(display);
            RenderBudget.Tick(display,rig);
            if(qualityRevision!=QualityMenu.AppliedRevision){qualityRevision=QualityMenu.AppliedRevision;rig?.ResetRenderCaches();Write("QUALITY returned gameplay input; hand/render caches invalidated");}
        }
        if (active && !diagnosticsDisabled) ReportState();
        if (screenshotPressed)
        {
            if (active) CaptureBothEyes();
            else Write("EYE CAPTURE ignored: XR is not active; start with F9 first");
        }
    }
    private void CaptureBothEyes()
    {
        try
        {
            if (display == null) { Write("EYE CAPTURE ignored: display unavailable"); return; }
            StartCoroutine(EyeCapture.Capture(display).WrapToIl2Cpp());
        }
        catch (Exception ex) { Warn("EYE CAPTURE request failed; XR kept running. " + ex); }
    }
    internal static void Write(string s)
    {
        try { Plugin.Output.LogInfo("[XIII-XR] " + s); FreezeWatch.Note(s); }
        catch { /* A failed log sink must not change XR lifecycle. */ }
    }
    internal static void Warn(string s)
    {
        try { Plugin.Output.LogWarning("[XIII-XR] " + s); }
        catch { /* Do not let error reporting become another failure. */ }
    }
    private void ReportState()
    {
        try
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextLog) return;
            nextLog = now + 10;
            Write("STATE display=" + display?.running + " input=" + input?.running + " xrEnabled=" + XRSettings.enabled + " xrActive=" + XRSettings.isDeviceActive);
            foreach (var cam in Camera.allCameras)
                Write("CAM " + cam.name + " stereo=" + cam.stereoEnabled + " target=" + cam.stereoTargetEye);
        }
        catch (Exception ex)
        {
            diagnosticsDisabled = true;
            Warn("DIAGNOSTICS disabled; XR left running. " + ex);
        }
    }
    private void StartXR()
    {
        Write("START requested");
        // The generated BepInEx interop wrapper for SubsystemManager lacks the
        // native class-init call. The game's native .cctor constructs the scripting
        // class map required for native XR descriptors to acquire managed wrappers.
        // Use the IL2CPP once-only initializer; never invoke .cctor directly.
        Write("INIT native SubsystemManager begin");
        IntPtr managerClass = Il2CppInterop.Runtime.IL2CPP.GetIl2CppClass(
            "UnityEngine.SubsystemsModule.dll", "UnityEngine", "SubsystemManager");
        if (managerClass == IntPtr.Zero) { Write("STOP: native SubsystemManager class missing"); return; }
        Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_class_init(managerClass);
        Write("INIT native SubsystemManager complete");
        // 0.1.181: the mod's own OpenXR first (Auto / OpenXR), else the OpenVR way.
        string choice=RuntimeChoice.Normalize(RuntimeOptions.Runtime?.Value);
        if(RuntimeChoice.NativeOpenXr(choice)&&!openXrGaveUp)
        {
            if(StartOpenXr())return;
            if(!RuntimeChoice.FallBack(choice)){StopXR("OpenXR unavailable ([VR] Runtime = OpenXR: no OpenVR way)");nextAutoCheck=Time.realtimeSinceStartup+10;attempted=false;return;}
            openXrGaveUp=true;
            Write("START the OpenVR way instead (SteamVR, or OpenComposite for another default runtime)");
        }
        StartOpenVr();
    }
    // Avoid restored generic GetSubsystemDescriptors<T>: it fails verification in this game.
    private bool CreateSubsystems(string displayId,string inputId)
    {
        var descriptors = UnityEngine.SubsystemsImplementation.SubsystemDescriptorStore.s_IntegratedDescriptors;
        Write("Integrated descriptors=" + (descriptors == null ? -1 : descriptors.Count));
        if (descriptors != null)
        for (int i=0; i<descriptors.Count; i++)
        {
            var descriptor = descriptors[i];
            string id = descriptor.id;
            Write("DESCRIPTOR ID=" + id);
            if (id != displayId && id != inputId) continue;
            Write("CREATE " + id);
            IntPtr native = SubsystemDescriptorBindings.Create(descriptor.m_Ptr);
            if (native == IntPtr.Zero) { Write("CREATE null for " + id); continue; }
            var instance = SubsystemManager.GetIntegratedSubsystemByPtr(native);
            if (instance == null) { Write("No managed instance for " + id); continue; }
            instance.m_SubsystemDescriptor = new ISubsystemDescriptor(descriptor.Pointer);
            if (id == displayId) display = instance.TryCast<XRDisplaySubsystem>();
            else input = instance.TryCast<XRInputSubsystem>();
            Write("CREATED " + id);
        }
        return display != null && input != null;
    }
    private void StartOpenVr()
    {
        if (!CreateSubsystems("OpenVR Display","OpenVR Input")) { Write("OpenVR subsystems unavailable. Send BepInEx log and Player.log."); StopXR("subsystems unavailable"); return; }
        QualityMenu.Attach(display!);
        Write("START Display begin");
        display!.Start();
        Write("START Display complete");
        Write("START Input begin");
        input!.Start();
        Write("START Input complete");
        active = display.running && input.running;
        Write("START result=" + active);
        if (!active) { StopXR("subsystem did not start"); nextAutoCheck=Time.realtimeSinceStartup+10; attempted=false; }
    }
    private bool StartOpenXr()
    {
        Write("START the mod's own OpenXR (Unity's OpenXR plugin, the Windows default OpenXR runtime)");
        openxr=new OpenXrLoader();
        if(!openxr.Initialize()){ShutdownOpenXr();return false;}
        if(!CreateSubsystems("OpenXR Display","OpenXR Input")){Warn("OPENXR subsystems unavailable (UnitySubsystems\\UnityOpenXR missing?)");ShutdownOpenXr();return false;}
        QualityMenu.Attach(display!);
        if(!openxr.CreateSession()){ShutdownOpenXr();return false;}
        return true;
    }
    private void ShutdownOpenXr()
    {
        var loader=openxr;openxr=null;
        if(loader!=null)try{loader.Shutdown(ref display,ref input);}catch(Exception ex){Warn("OPENXR shutdown: "+ex.Message);}
        display=null;input=null;
    }
    // Every frame while the own OpenXR starts or runs: its events; the mod starts once the head and eyes come.
    private void TickOpenXr()
    {
        var loader=openxr;if(loader==null)return;
        var phase=loader.Tick(display,input);
        if(phase==OpenXrLoader.Phase.Running&&!active)
        {
            if(openXrRunningSince<0)openXrRunningSince=Time.realtimeSinceStartup;
            if(display!=null&&input!=null&&display.running&&input.running&&OpenXrTracking.ViewsReady()){active=true;openXrRunningSince=-1;Write("START result=True (OpenXR)");}
            else if(Time.realtimeSinceStartup-openXrRunningSince>OpenXrLoader.ReadyTimeout){loader.GiveUp("the session runs but the headset gave no head pose and eye views in "+OpenXrLoader.ReadyTimeout.ToString("F0")+" s");phase=loader.State;openXrRunningSince=-1;}
        }
        if(phase==OpenXrLoader.Phase.Failed||phase==OpenXrLoader.Phase.Stopped)
        {
            bool wasActive=active;string why=loader.Failure;
            StopXR(phase==OpenXrLoader.Phase.Failed?"OpenXR: "+why:"OpenXR session ended");
            if(!wasActive&&phase==OpenXrLoader.Phase.Failed&&RuntimeChoice.FallBack(RuntimeOptions.Runtime?.Value))
            {
                openXrGaveUp=true;attempted=true;
                Write("START the OpenVR way instead (SteamVR, or OpenComposite for another default runtime)");
                try{StartOpenVr();}catch(Exception ex){Warn("START ERROR "+ex);StopXR("startup exception");}
            }
            else{nextAutoCheck=Time.realtimeSinceStartup+10;attempted=false;}
        }
    }
    public void LateUpdate()
    {
        if(active&&rig!=null){long start=FramePerformance.Begin(FramePerformance.Late);try{rig.MenuLateUpdate();}finally{FramePerformance.Part(FramePerformance.Late,start);}}
        // 0.1.147: NPC hit reactions every frame, after the game's animation.
        try{NpcHitReactions.Current?.FromLateUpdate();}catch(Exception ex){Bootstrap.Warn("NPC HITS late: "+ex.Message);}
        // 0.1.151: a ceiling fan the player hangs on turns where his ride has it.
        try{CeilingFans.Current?.LateTick();}catch(Exception ex){Bootstrap.Warn("FAN late: "+ex.Message);}
    }

    private void StopXR(string reason)
    {
        Write("STOP requested: " + reason);
        RenderBudget.Release();
        active=false; calibrateAt=-1;QualityMenu.Close();
        try{WindowFocus.InVr=false;WindowFocus.Restore();}catch(Exception){}
        try { interaction?.Dispose(); } catch (Exception ex) { Warn("Interaction stop: " + ex.Message); }
        interaction=null;
        try { weapons?.Dispose(); } catch (Exception ex) { Warn("Weapon stop: " + ex.Message); }
        weapons=null;
        try { locomotion?.Dispose(); } catch (Exception ex) { Warn("Locomotion stop: " + ex.Message); }
        locomotion=null;
        try { rig?.Dispose(); } catch (Exception ex) { Warn("Tracking stop: " + ex.Message); }
        rig=null;
        if(openxr!=null){ShutdownOpenXr();return;}
        try { if (input != null) { input.Stop(); input.Destroy(); input=null; } } catch (Exception ex) { Write("Input stop: " + ex.Message); }
        try { if (display != null) { display.Stop(); display.Destroy(); display=null; } } catch (Exception ex) { Write("Display stop: " + ex.Message); }
    }
    public void OnApplicationQuit() { StopXR("application quit"); }
}
