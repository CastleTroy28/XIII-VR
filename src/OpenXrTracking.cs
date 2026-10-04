using System;
using System.Numerics;
using System.Globalization;
namespace XiiiXR;
// 0.1.181: the headset and controllers through the mod's own OpenXR
// (xiii_openxr.dll, OpenXrLoader). Poses at the next frame's display time in
// the floor (STAGE) space, the hands as SteamVR gives them (the grip-to-
// SteamVR transform of the controller in use), the eyes and fields of view of
// the runtime, buttons as OpenVR's bits (trigger and grip held from 0.55,
// let go below 0.40), vibration by OpenXR's own haptic output with the same
// strengths and lengths the OpenVR way sends.
internal sealed unsafe class OpenXrTracking : VrTracking
{
    private readonly float[] pose=new float[64],input=new float[8];
    private readonly int[] poseFlags=new int[4],inputFlags=new int[8];
    private readonly bool[] triggerHeld=new bool[2],gripHeld=new bool[2];
    private readonly ulong[] oldButtons=new ulong[2];
    private readonly int[] oldProfile={-1,-1};
    private bool eyesSeen,hapticFailed;private int vibrations;
    private float testStart=-1;private int testStep=3;
    private string headset="";private float hz;
    // Unity's OpenXR plugin waits for a frame (xrWaitFrame) and locates its
    // eyes before the game renders that frame, so the last display time is
    // the one of the frame being rendered: the head and eyes the game renders
    // with are those the plugin hands the runtime with it.
    internal const int FramesAhead=0;
    public OpenXrTracking()
    {
        RefreshPoses(true);
        RefreshEyes();
        Bootstrap.Write("TRACKING OpenXR (the mod's own, no OpenComposite): "+OpenXrLoader.HelperReport()+" eye distance="+EyeDistance.ToString("F4",CultureInfo.InvariantCulture)+" m");
    }
    private long lastRefresh;
    private static readonly long RefreshReuse=System.Diagnostics.Stopwatch.Frequency*3/2000;
    public override void RefreshPoses(bool force=false)
    {
        long now=System.Diagnostics.Stopwatch.GetTimestamp();
        if(!force&&lastRefresh!=0&&now-lastRefresh>=0&&now-lastRefresh<RefreshReuse)return;
        lastRefresh=now;
        int ok;
        fixed(float* f=pose)fixed(int* i=poseFlags)ok=OpenXrLoader.Locate==null?0:OpenXrLoader.Locate(f,i,FramesAhead);
        int flags=ok==1?poseFlags[0]:0;
        HeadValid=(flags&1)!=0;LeftValid=(flags&2)!=0;RightValid=(flags&4)!=0;
        if(HeadValid)Head=OpenXrMath.Pose(pose,0);
        if(LeftValid)Left=OpenXrMath.Pose(pose,7);
        if(RightValid)Right=OpenXrMath.Pose(pose,14);
        HeadValid&=PoseMath.Valid(Head);LeftValid&=PoseMath.Valid(Left);RightValid&=PoseMath.Valid(Right);
        if((flags&32)!=0)ReadEyes();
        // 0.1.235: the world scale on the eyes Unity's plugin renders from (the mod's own eyes: PoseMath.ScaledEye).
        try{OpenXrLoader.ApplyWorldScale(QualityOptions.WorldScaleValue);}catch(Exception){}
        if(ok==1&&pose[57]>1&&pose[57]<1000){hz=pose[57];CompositorTiming.HeadsetHz=hz;}
    }
    private void ReadEyes()
    {
        var left=OpenXrMath.Pose(pose,35);var right=OpenXrMath.Pose(pose,42);
        var fl=OpenXrMath.Frustum(pose[49],pose[50],pose[51],pose[52]);var fr=OpenXrMath.Frustum(pose[53],pose[54],pose[55],pose[56]);
        float distance=Vector3.Distance(left.Position,right.Position);
        if(!PoseMath.Valid(left)||!PoseMath.Valid(right)||!(distance>.0001f&&distance<.2f)||!fl.Valid||!fr.Valid)return;
        EyeLeft=left;EyeRight=right;EyeDistance=distance;FrustumLeft=fl;FrustumRight=fr;HasEyeOffsets=true;eyesSeen=true;
    }
    // Bootstrap starts the mod's work only once the runtime gives the head and both eyes.
    internal static bool ViewsReady()
    {
        if(OpenXrLoader.Locate==null)return false;
        var f=stackalloc float[64];var i=stackalloc int[4];
        return OpenXrLoader.Locate(f,i,FramesAhead)==1&&(i[0]&33)==33;
    }
    public override void RefreshEyes()
    {
        RefreshPoses(true);
        if(!eyesSeen)throw new InvalidOperationException("OpenXR runtime returned no valid eye views yet");
    }
    public override void ReadButtons()
    {
        RecenterPressed=false;
        int ok;
        fixed(float* f=input)fixed(int* i=inputFlags)ok=OpenXrLoader.Sync==null?0:OpenXrLoader.Sync(f,i);
        for(int hand=0;hand<2;hand++)
        {
            bool present=ok==1&&(inputFlags[0]&(1<<hand))!=0;
            int profile=ok==1?inputFlags[3+hand]:0;
            if(profile!=oldProfile[hand]&&ok==1)
            {
                oldProfile[hand]=profile;oldButtons[hand]=0;triggerHeld[hand]=gripHeld[hand]=false;
                Bootstrap.Write("CONTROLLER "+(hand==0?"left":"right")+" OpenXR "+OpenXrMath.Profile(profile));
                if(hand==0)leftChannels.Sample(false,0);else rightChannels.Sample(false,0);
            }
            if(!present){triggerHeld[hand]=gripHeld[hand]=false;}
            else{triggerHeld[hand]=OpenXrMath.Held(input[hand],triggerHeld[hand]);gripHeld[hand]=OpenXrMath.Held(input[2+hand],gripHeld[hand]);}
            int bits=inputFlags[1+hand];
            var stick=new StickSample(present,new System.Numerics.Vector2(input[4+2*hand],input[5+2*hand]),present&&OpenXrMath.StickClick(bits));
            if(hand==0){LeftStick=stick;LeftInput=present;}else{RightStick=stick;RightInput=present;}
            ulong buttons=present?OpenXrMath.Buttons(triggerHeld[hand],gripHeld[hand],bits):0;
            if(stick.Valid&&stick.Clicked)buttons|=HandControls.Stick;
            if(hand==0)leftChannels.Sample(present,buttons);else rightChannels.Sample(present,buttons);
            ulong down=buttons&~oldButtons[hand],up=oldButtons[hand]&~buttons;
            if(down!=0||up!=0)Bootstrap.Write("BUTTON "+(hand==0?"left":"right")+" down=0x"+down.ToString("X")+" up=0x"+up.ToString("X")+" available="+present);
            oldButtons[hand]=buttons;
        }
    }
    // ---- vibration: the OpenVR way's strengths and lengths, sent once ----
    private bool Send(bool right,float amplitude,float seconds)
    {
        if(hapticFailed||OpenXrLoader.Vibrate==null||!float.IsFinite(amplitude)||amplitude<=0)return false;
        if(right?(!RightValid||!RightControls.Valid):(!LeftValid||!LeftControls.Valid))return false;
        try{bool sent=OpenXrLoader.Vibrate(right?1:0,Math.Min(1,amplitude),seconds)!=0;if(sent)vibrations++;return sent;}
        catch(Exception ex){hapticFailed=true;Bootstrap.Warn("HAPTIC unavailable; weapons and XR continue. "+ex.Message);return false;}
    }
    internal override void ShotHaptics(string profile,bool support)
    {
        float strength=(profile=="shotgun"?3500:profile=="ak47"?1800:profile=="revolver"?3600:2400)/3999f;
        Send(true,support?strength*.75f:strength,.055f);
        if(support)Send(false,strength*.45f,.045f);
    }
    internal override void LeftShotHaptics()=>Send(false,.60f,.055f);
    internal override void RopeHaptics()=>Send(false,.30f,.04f);
    internal override void ResistanceHaptics(float amplitude,bool right=false)
    {
        if(!float.IsFinite(amplitude))return;amplitude=Math.Clamp(amplitude,0,1);if(amplitude<.05f)return;
        Send(right,amplitude,.035f);
    }
    internal override void ReloadHaptics(ReloadAction action,bool rightReloads=false)
    {
        bool magazine=action==ReloadAction.DropInstalled||action==ReloadAction.TakeInstalled;
        bool latch=action==ReloadAction.Insert||action==ReloadAction.Chamber||action==ReloadAction.OpenCover||action==ReloadAction.CloseCover;
        if(!magazine&&!latch&&action!=ReloadAction.RackBack)return;
        float seconds=action==ReloadAction.RackBack?.085f:.065f;
        Send(!rightReloads,action==ReloadAction.DropInstalled?.75f:.5f,seconds);
        if(action!=ReloadAction.DropInstalled)Send(rightReloads,.9f,seconds);
        Bootstrap.Write("HAPTIC reload="+action+" OpenXR length="+seconds+" sent="+vibrations);
    }
    internal override void SwimHaptics(float amplitude)
    {
        if(!float.IsFinite(amplitude))return;amplitude=Math.Clamp(amplitude,0,1);if(amplitude<.05f)return;
        Send(true,amplitude*.6f,.06f);Send(false,amplitude*.6f,.06f);
    }
    internal override void PunchHaptics(bool right)
    {
        Send(right,1,.16f);
        Bootstrap.Write("HAPTIC impact side="+(right?"R":"L")+" OpenXR 1.0 for 160 ms sent="+vibrations);
    }
    internal override void TestHaptics()
    {
        testStart=UnityEngine.Time.realtimeSinceStartup;testStep=0;
        Bootstrap.Write("HAPTIC TEST: LEFT, RIGHT after 0.6s, BOTH after 1.2s (OpenXR)");
    }
    internal override void TickHaptics(bool allowed)
    {
        if(testStep>=3||testStart<0)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(now<testStart+testStep*.6f)return;
        bool l=testStep!=1,r=testStep!=0;
        bool sent=(l&&Send(false,1,.2f))|(r&&Send(true,1,.2f));
        Bootstrap.Write("HAPTIC TEST step="+testStep+" sent="+sent);
        testStep++;
    }
    internal override void CancelHaptics()
    {
        try{if(OpenXrLoader.StopVibration!=null){OpenXrLoader.StopVibration(0);OpenXrLoader.StopVibration(1);}}catch(Exception){}
    }
    internal override void CancelShotHaptics()=>CancelHaptics();
    internal override string FrameTimingReport()
    {
        if(headset.Length==0){string r=OpenXrLoader.HelperReport();int at=r.IndexOf(" system ",StringComparison.Ordinal);if(at>=0){int end=r.IndexOf(';',at);headset=r.Substring(at+8,(end>at?end:r.Length)-at-8).Replace(' ','_');}}
        return "OpenXR hmd="+(headset.Length>0?headset:"?")+" hz="+(hz>0?hz.ToString("F1",CultureInfo.InvariantCulture):"?")+" (no compositor timing in OpenXR)";
    }
    public override void Dispose(){CancelHaptics();}
}
