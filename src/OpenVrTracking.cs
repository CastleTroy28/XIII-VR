using System;
using System.IO;
using System.Runtime.InteropServices;
using Valve.VR;
namespace XiiiXR;
internal sealed class OpenVrTracking : VrTracking
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr InterfaceCall([MarshalAs(UnmanagedType.LPStr)] string version, ref EVRInitError error);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private delegate bool VersionCheck([MarshalAs(UnmanagedType.LPStr)] string version);
    // 0.1.174: OpenComposite (OpenVR on OpenXR, put in by
    // the installer; since 0.1.180 chosen by the runtime switch) answers "valid" for any interface version,
    // even one that does not exist; SteamVR does not. Under OpenComposite the
    // mod asks only for the interfaces the game's own OpenVR plugin uses (the
    // SteamVR action input is left out: the controllers' own buttons work).
    internal static bool OpenComposite{get;private set;}
    // 0.1.178: the OpenXR package's openvr_api.dll adapts the interface
    // versions the game asks for (IVRSystem_023, ...) to the older ones
    // OpenComposite has; it says which it adapted (written to the log).
    // 0.1.180: it is the runtime switch now (SteamVR or OpenComposite).
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ShimReport();
    private IntPtr library;
    private readonly CVRSystem system;
    private readonly CVRCompositor compositor;
    private readonly TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    private readonly uint stateSize = (uint)Marshal.SizeOf<VRControllerState_t>();
    private readonly ulong[] oldButtons = new ulong[2];
    private readonly uint[] oldIndices = { uint.MaxValue, uint.MaxValue };
    private readonly int[] stickIndices = { -1, -1 };
    private readonly HapticChannel leftHaptic=new(),rightHaptic=new();
    private readonly HapticOutput providerHaptics=new();
    private float testUntil;
    private SteamInput? actions;
    private InterfaceCall? interfaceCall;private float nextActionAttempt;private bool actionAttempted;
    private float testStart;private int testStep=3;
    internal override void TestHaptics()
    {
        providerHaptics.Reset();hapticFailed=false;
        testStart=UnityEngine.Time.realtimeSinceStartup;testUntil=testStart+1.6f;testStep=0;
        Bootstrap.Write("HAPTIC TEST: action LEFT, action RIGHT after 0.6s, legacy BOTH after 1.2s");
    }
    private bool hapticFailed;
    private int hapticPulses;
    private float nextHapticReport;
    internal override void CancelHaptics() { leftHaptic.Cancel(); rightHaptic.Cancel(); }
    internal override void CancelShotHaptics(){leftHaptic.CancelShot();rightHaptic.CancelShot();}
    internal override void ShotHaptics(string profile,bool support)
    {
        if(hapticFailed || !RightValid || !RightControls.Valid) return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        ushort strength=(ushort)(profile=="shotgun"?3500:profile=="ak47"?1800:profile=="revolver"?3600:2400);
        if(actions?.Vibrate(true,(support?strength*.75f:strength)/3999f,.055f)!=true)rightHaptic.Queue(now,oldIndices[1],(ushort)(support?strength*.75f:strength),.055f);
        if(support && LeftValid && LeftControls.Valid && actions?.Vibrate(false,strength*.45f/3999f,.045f)!=true) leftHaptic.Queue(now,oldIndices[0],(ushort)(strength*.45f),.045f);
        TickHaptics(true);
        if(now>=nextHapticReport)
        {
            nextHapticReport=now+2;
            Bootstrap.Write("HAPTIC shot="+profile+" support="+support+" rightDevice="+oldIndices[1]+" leftDevice="+oldIndices[0]+" requestedUs="+strength+" sentPulses="+hapticPulses);
        }
    }
    internal override void LeftShotHaptics()
    {
        if(hapticFailed||!LeftValid||!LeftControls.Valid)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(actions?.Vibrate(false,.60f,.055f)!=true)leftHaptic.Queue(now,oldIndices[0],2400,.055f);
        TickHaptics(true);
    }
    internal override void RopeHaptics()
    {
        if(hapticFailed||!LeftValid||!LeftControls.Valid)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(actions?.Vibrate(false,.30f,.04f)!=true)leftHaptic.Queue(now,oldIndices[0],1200,.04f);
        TickHaptics(true);
    }
    // 0.1.122: the crossbow bolt drawn back along its rail — a short left
    // buzz whose strength follows how fast it is drawn (felt as resistance).
    internal override void ResistanceHaptics(float amplitude,bool right=false)
    {
        if(hapticFailed||!float.IsFinite(amplitude)||(right?!RightValid||!RightControls.Valid:!LeftValid||!LeftControls.Valid))return;
        amplitude=Math.Clamp(amplitude,0,1);if(amplitude<.05f)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        actions?.Vibrate(right,amplitude,.035f);
        (right?rightHaptic:leftHaptic).Mechanism(now,oldIndices[right?1:0],(ushort)(3999*amplitude*.7f),.035f);
        TickHaptics(true);
    }
    internal override void ReloadHaptics(ReloadAction action,bool rightReloads=false)
    {
        if(hapticFailed)return;
        bool magazine=action==ReloadAction.DropInstalled||action==ReloadAction.TakeInstalled;
        bool latch=action==ReloadAction.Insert||action==ReloadAction.Chamber||action==ReloadAction.OpenCover||action==ReloadAction.CloseCover;
        if(!magazine&&!latch&&action!=ReloadAction.RackBack)return;
        float now=UnityEngine.Time.realtimeSinceStartup,seconds=action==ReloadAction.RackBack?.085f:.065f;
        // Accepted SteamVR action calls did not demonstrate motor output in
        // prior logs. Also send the bounded legacy envelope, independently of
        // support grip / fire release (magazines are held with the trigger).
        void Send(bool right,float amplitude)
        {
            if(right?(!RightValid||!RightControls.Valid):(!LeftValid||!LeftControls.Valid))return;
            actions?.Vibrate(right,amplitude,seconds);
            (right?rightHaptic:leftHaptic).Mechanism(now,oldIndices[right?1:0],(ushort)(3999*amplitude),seconds);
        }
        Send(!rightReloads,action==ReloadAction.DropInstalled?.75f:.5f);
        if(action!=ReloadAction.DropInstalled)Send(rightReloads,.9f);
        TickHaptics(true);
        Bootstrap.Write("HAPTIC reload="+action+" action+legacy envelope="+seconds+" pulses="+hapticPulses);
    }
    // 0.1.108: short buzz on both controllers during a swimming arm stroke.
    internal override void SwimHaptics(float amplitude)
    {
        if(hapticFailed||!float.IsFinite(amplitude))return;
        amplitude=Math.Clamp(amplitude,0,1);if(amplitude<.05f)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        void Send(bool right)
        {
            if(right?(!RightValid||!RightControls.Valid):(!LeftValid||!LeftControls.Valid))return;
            actions?.Vibrate(right,amplitude,.06f);
            (right?rightHaptic:leftHaptic).Mechanism(now,oldIndices[right?1:0],(ushort)(3999*amplitude*.6f),.06f);
        }
        Send(true);Send(false);TickHaptics(true);
    }
    internal override void PunchHaptics(bool right)
    {
        if(hapticFailed)return;
        int hand=right?1:0;
        if(right?(!RightValid||!RightControls.Valid):(!LeftValid||!LeftControls.Valid))return;
        if(actions?.Vibrate(right,1,.16f)==true)(right?rightHaptic:leftHaptic).Cancel();
        else (right?rightHaptic:leftHaptic).Impact(UnityEngine.Time.realtimeSinceStartup,oldIndices[hand]);
        TickHaptics(true);
        Bootstrap.Write("HAPTIC impact side="+(right?"R":"L")+" device="+oldIndices[hand]+" requestedUs=3999 durationMs=160 sentPulses="+hapticPulses);
    }
    internal override void TickHaptics(bool allowed)
    {
        if(hapticFailed) return;
        try
        {
            float now=UnityEngine.Time.realtimeSinceStartup;
            allowed|=UnityEngine.Application.isFocused&&now<testUntil;
            if(allowed&&testStep<3&&now>=testStart+testStep*.6f)
            {
                if(testStep<2) {bool sent=actions?.Vibrate(testStep==1,1,.2f)==true;Bootstrap.Write("HAPTIC TEST action="+testStep+" dispatched="+sent);}
                else {leftHaptic.Impact(now,oldIndices[0]);rightHaptic.Impact(now,oldIndices[1]);}
                testStep++;
            }
            if(rightHaptic.TryPulse(now,oldIndices[1],allowed && HeadValid && RightValid && RightControls.Valid,true,out ushort right))
            { if(!providerHaptics.Send(true,right))system.TriggerHapticPulse(oldIndices[1],0,right); hapticPulses++; }
            if(leftHaptic.TryPulse(now,oldIndices[0],allowed && HeadValid && LeftValid && LeftControls.Valid,(LeftControls.Held & HandControls.Grip)!=0,out ushort left))
            { if(!providerHaptics.Send(false,left))system.TriggerHapticPulse(oldIndices[0],0,left); hapticPulses++; }
        }
        catch(Exception ex) { hapticFailed=true; CancelHaptics(); Bootstrap.Warn("HAPTIC unavailable; weapons and XR continue. "+ex.Message); }
    }
    public OpenVrTracking()
    {
        // Reuse the DLL and session owned by Unity's provider. Never call VR_Init,
        // VR_Shutdown, WaitGetPoses, or Submit from this tracking client.
        string path = Path.Combine(BepInEx.Paths.GameRootPath, "XIII_Data", "Plugins", "openvr_api.dll");
        library = NativeLibrary.Load(path);
        try
        {
            var getInterface = Marshal.GetDelegateForFunctionPointer<InterfaceCall>(NativeLibrary.GetExport(library, "VR_GetGenericInterface"));interfaceCall=getInterface;
            try{OpenComposite=NativeLibrary.TryGetExport(library,"VR_IsInterfaceVersionValid",out var check)&&Marshal.GetDelegateForFunctionPointer<VersionCheck>(check)("IVRCompositor_999");}catch(Exception){OpenComposite=false;}
            if(OpenComposite)Bootstrap.Write("TRACKING runtime: OpenComposite (OpenVR on the OpenXR runtime, SteamVR not used); SteamVR action input left out, the controllers' own buttons used");
            EVRInitError error = EVRInitError.None;
            IntPtr sys = getInterface("FnTable:" + OpenVR.IVRSystem_Version, ref error);
            if (sys == IntPtr.Zero || error != EVRInitError.None) throw new InvalidOperationException("IVRSystem FnTable: " + error);
            system = new CVRSystem(sys);
            error = EVRInitError.None;
            IntPtr comp = getInterface("FnTable:" + OpenVR.IVRCompositor_Version, ref error);
            if (comp == IntPtr.Zero || error != EVRInitError.None) throw new InvalidOperationException("IVRCompositor FnTable: " + error);
            compositor = new CVRCompositor(comp);
            // 0.1.180: which runtime the mod's openvr_api.dll chose (SteamVR or OpenComposite) and why.
            try{if(NativeLibrary.TryGetExport(library,"XIIIVR_ShimReport",out var report))Bootstrap.Write("TRACKING "+Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<ShimReport>(report)()));else Bootstrap.Write("TRACKING openvr_api.dll is Valve's own (no runtime switch): SteamVR");}catch(Exception ex){Bootstrap.Warn("TRACKING runtime switch report not read: "+ex.Message);}
            TryAttachActions();
            RefreshEyes();
            Bootstrap.Write("TRACKING attached to existing OpenVR session; native eye distance=" + EyeDistance.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " m");
        }
        catch { Dispose(); throw; }
    }
    private void TryAttachActions()
    {
        if(actionAttempted||actions!=null||interfaceCall==null||OpenComposite||!WeaponOptions.ActionInput.Value||UnityEngine.Time.realtimeSinceStartup<nextActionAttempt)return;
        nextActionAttempt=UnityEngine.Time.realtimeSinceStartup+2;
        try
        {
            uint device=system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
            if(device>=OpenVR.k_unMaxTrackedDeviceCount)return;
            var name=new System.Text.StringBuilder(256);ETrackedPropertyError propError=0;
            system.GetStringTrackedDeviceProperty(device,ETrackedDeviceProperty.Prop_ControllerType_String,name,256,ref propError);
            if(propError!=ETrackedPropertyError.TrackedProp_Success)return;
            actionAttempted=true;Bootstrap.Write("STEAM INPUT controller type="+name+" error="+propError);
            if(name.ToString()!="oculus_touch")return;
            var error=EVRInitError.None;var table=interfaceCall("FnTable:"+OpenVR.IVRInput_Version,ref error);
            if(table!=IntPtr.Zero&&error==EVRInitError.None)actions=new SteamInput(table,SteamInputManifest.Write(Path.Combine(BepInEx.Paths.ConfigPath,"XIII-SteamInput")));
        }
        catch(Exception ex){Bootstrap.Warn("STEAM INPUT not attached: "+ex.Message);}
    }
    public override void RefreshEyes()
    {
        EyeLeft = PoseMath.FromOpenVR(system.GetEyeToHeadTransform(EVREye.Eye_Left));
        EyeRight = PoseMath.FromOpenVR(system.GetEyeToHeadTransform(EVREye.Eye_Right));
        EyeDistance = System.Numerics.Vector3.Distance(EyeLeft.Position, EyeRight.Position);
        HasEyeOffsets = PoseMath.Valid(EyeLeft) && PoseMath.Valid(EyeRight) && EyeDistance > 0.0001f && EyeDistance < 0.2f;
        FrustumLeft = ReadFrustum(EVREye.Eye_Left);
        FrustumRight = ReadFrustum(EVREye.Eye_Right);
        if (!HasEyeOffsets || !FrustumLeft.Valid || !FrustumRight.Valid)
            throw new InvalidOperationException("Runtime returned invalid eye transforms/projections");
    }
    private EyeFrustum ReadFrustum(EVREye eye)
    {
        float l = 0, r = 0, t = 0, b = 0;
        system.GetProjectionRaw(eye, ref l, ref r, ref t, ref b);
        return new EyeFrustum(l, r, t, b);
    }
    // 0.1.121: the poses were re-read from OpenVR (64 devices marshalled) on
    // every hand sample, 25-35 times a frame. A sample younger than 1.5 ms is
    // reused; the pre-render refresh is always forced.
    private long lastRefresh;
    private static readonly long RefreshReuse=System.Diagnostics.Stopwatch.Frequency*3/2000;
    public override void RefreshPoses(bool force=false)
    {
        long now=System.Diagnostics.Stopwatch.GetTimestamp();
        if(!force&&lastRefresh!=0&&now-lastRefresh>=0&&now-lastRefresh<RefreshReuse)return;
        lastRefresh=now;
        system.GetDeviceToAbsoluteTrackingPose(compositor.GetTrackingSpace(), 0, poses);
        HeadValid = ReadPose(OpenVR.k_unTrackedDeviceIndex_Hmd, out Head);
        LeftValid = ReadPose(system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand), out Left);
        RightValid = ReadPose(system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand), out Right);
    }
    // 0.1.121: compositor timing of the frames since the last report, and
    // the headset (model, refresh rate) once.
    private Compositor_FrameTiming[]? timings;private uint lastTimingFrame;private string headset="";
    internal override string FrameTimingReport()
    {
        timings??=new Compositor_FrameTiming[256];
        timings[0].m_nSize=(uint)Marshal.SizeOf<Compositor_FrameTiming>();
        uint n=Math.Min(compositor.GetFrameTimings(timings),(uint)timings.Length);
        var frames=new System.Collections.Generic.List<CompositorFrame>((int)n);
        for(int i=0;i<n;i++)
        {
            var t=timings[i];if(t.m_nFrameIndex<=lastTimingFrame)continue;
            frames.Add(new CompositorFrame(t.m_nReprojectionFlags,t.m_flTotalRenderGpuMs,t.m_flClientFrameIntervalMs,t.m_nNumFramePresents,t.m_nNumDroppedFrames,t.m_nNumMisPresented));
        }
        if(n>0)lastTimingFrame=Math.Max(lastTimingFrame,timings[n-1].m_nFrameIndex);
        if(headset.Length==0)
        {
            var error=ETrackedPropertyError.TrackedProp_Success;
            float hz=system.GetFloatTrackedDeviceProperty(OpenVR.k_unTrackedDeviceIndex_Hmd,ETrackedDeviceProperty.Prop_DisplayFrequency_Float,ref error);
            if(float.IsFinite(hz)&&hz>0)CompositorTiming.HeadsetHz=hz;
            headset="hmd="+Property(ETrackedDeviceProperty.Prop_ModelNumber_String).Replace(' ','_')+" via="+Property(ETrackedDeviceProperty.Prop_TrackingSystemName_String).Replace(' ','_')
                +" hz="+(float.IsFinite(hz)?hz.ToString("F1",System.Globalization.CultureInfo.InvariantCulture):"?");
        }
        return CompositorTiming.Summarize(frames)+" "+headset;
    }
    private string Property(ETrackedDeviceProperty prop)
    {
        var error=ETrackedPropertyError.TrackedProp_Success;var text=new System.Text.StringBuilder(128);
        system.GetStringTrackedDeviceProperty(OpenVR.k_unTrackedDeviceIndex_Hmd,prop,text,(uint)text.Capacity,ref error);
        return error==ETrackedPropertyError.TrackedProp_Success&&text.Length>0?text.ToString():"?";
    }
    private bool ReadPose(uint index, out PoseValue pose)
    {
        pose = default;
        if (index >= poses.Length || !poses[index].bDeviceIsConnected || !poses[index].bPoseIsValid) return false;
        pose = PoseMath.FromOpenVR(poses[index].mDeviceToAbsoluteTracking);
        return PoseMath.Valid(pose);
    }
    public override void ReadButtons()
    {
        RecenterPressed = false;
        bool available = system.IsInputAvailable();
        TryAttachActions();actions?.Update();
        for (int hand = 0; hand < 2; hand++)
        {
            uint index = system.GetTrackedDeviceIndexForControllerRole(hand == 0 ? ETrackedControllerRole.LeftHand : ETrackedControllerRole.RightHand);
            bool deviceChanged = index != oldIndices[hand];
            if (deviceChanged)
            {
                (hand==0?leftHaptic:rightHaptic).Cancel();
                Bootstrap.Write("CONTROLLER " + (hand == 0 ? "left" : "right") + " index=" + index);
                oldIndices[hand] = index; oldButtons[hand] = 0;
                stickIndices[hand] = -1;
                if(hand==0)leftChannels.Sample(false,0);else rightChannels.Sample(false,0);
            }
            var state = new VRControllerState_t();
            bool ok = available && index < poses.Length && system.GetControllerState(index, ref state, stateSize);
            if (ok && stickIndices[hand] < 0) stickIndices[hand] = FindStick(index, hand);
            var axis = ReadAxis(state, stickIndices[hand]);
            var stick = new StickSample(ok && !deviceChanged && stickIndices[hand] >= 0, new System.Numerics.Vector2(axis.x,axis.y),
                ok && StickSample.AxisClick(state.ulButtonPressed,stickIndices[hand]));
            ulong actionButtons=0;StickSample actionStick=default;bool actionRead=available&&actions?.Read(hand,out actionButtons,out actionStick)==true;
            if(actionRead){ok=true;stick=actionStick;}
            if (hand == 0) LeftStick = stick; else RightStick = stick;
            if (hand == 0) LeftInput = ok; else RightInput = ok;
            ulong buttons = ok ? (actionRead?actionButtons:state.ulButtonPressed) : 0;
            if(stick.Valid && stick.Clicked)buttons|=HandControls.Stick;
            if (hand == 0) leftChannels.Sample(ok, buttons);
            else rightChannels.Sample(ok, buttons);
            ulong down = buttons & ~oldButtons[hand];
            ulong up = oldButtons[hand] & ~buttons;
            if (down != 0 || up != 0)
                Bootstrap.Write("BUTTON " + (hand == 0 ? "left" : "right") + " down=0x" + down.ToString("X") + " up=0x" + up.ToString("X") + " available=" + ok);
            // Only right grip interacts; F11 remains the recenter key.
            oldButtons[hand] = buttons;
        }
    }
    private int FindStick(uint device, int hand)
    {
        int configured = hand == 0 ? LocomotionOptions.LeftStickAxis.Value : LocomotionOptions.RightStickAxis.Value;
        if (configured >= 0 && configured <= 4)
        { Bootstrap.Write("STICK AXIS " + hand + " configured=" + configured); return configured; }
        int pad = -1; string types = "";
        for (int i = 0; i < 5; i++)
        {
            ETrackedPropertyError error = 0;
            int type = system.GetInt32TrackedDeviceProperty(device, (ETrackedDeviceProperty)((int)ETrackedDeviceProperty.Prop_Axis0Type_Int32+i), ref error);
            types += i + ":" + type + "/" + error + " ";
            if (error == 0 && type == (int)EVRControllerAxisType.k_eControllerAxis_Joystick)
            { Bootstrap.Write("STICK AXIS " + hand + " joystick=" + i + " types=" + types); return i; }
            if (error == 0 && type == (int)EVRControllerAxisType.k_eControllerAxis_TrackPad) pad = i;
        }
        int fallback = pad >= 0 ? pad : 0;
        Bootstrap.Write("STICK AXIS " + hand + " fallback=" + fallback + " types=" + types + "; check STICKS diagnostics if movement is absent.");
        return fallback;
    }
    private static VRControllerAxis_t ReadAxis(VRControllerState_t state, int index) => index switch
    { 0 => state.rAxis0, 1 => state.rAxis1, 2 => state.rAxis2, 3 => state.rAxis3, 4 => state.rAxis4, _ => default };
    public override void Dispose()
    {
        CancelHaptics();
        // Release only our library reference. The XR provider owns the VR session.
        if (library != IntPtr.Zero) { NativeLibrary.Free(library); library = IntPtr.Zero; }
    }
}
