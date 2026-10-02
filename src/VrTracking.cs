using System;
namespace XiiiXR;
// 0.1.181: what the rest of the mod reads from the headset and controllers,
// whichever way it comes: OpenVrTracking (SteamVR / OpenComposite, as up to
// 0.1.180) or OpenXrTracking (the mod's own OpenXR, xiii_openxr.dll). Poses
// in Unity's axes (left-handed, +Z forward) in the runtime's standing space;
// buttons as OpenVR's bits (HandControls).
internal abstract class VrTracking : IDisposable
{
    public PoseValue Head, Left, Right, EyeLeft, EyeRight;
    public bool HeadValid, LeftValid, RightValid;
    public bool LeftInput, RightInput;
    public bool RecenterPressed;
    public bool HasEyeOffsets;
    public float EyeDistance;
    public EyeFrustum FrustumLeft, FrustumRight;
    internal StickSample LeftStick, RightStick;
    // 0.1.155: the left hand too has a menu channel of its own (its trigger
    // clicks in menus, and that click never fires a gun on the way out).
    protected readonly RightControlChannels leftChannels=new();
    protected readonly RightControlChannels rightChannels=new();
    internal HandControls LeftControls=>leftChannels.Gameplay;
    internal HandControls MenuLeftControls=>leftChannels.Menu;
    internal HandControls RightControls=>rightChannels.Gameplay;
    internal HandControls MenuRightControls=>rightChannels.Menu;
    internal void DisarmTrigger()=>rightChannels.DisarmGameplay();
    internal void DisarmLeftTrigger()=>leftChannels.DisarmGameplay();
    internal void InvalidateControls()
    {
        CancelHaptics();
        leftChannels.Sample(false, 0); rightChannels.Sample(false, 0);
        LeftInput = RightInput = false;
        LeftStick = RightStick = default;
    }
    internal abstract void TestHaptics();
    internal abstract void CancelHaptics();
    internal abstract void CancelShotHaptics();
    internal abstract void ShotHaptics(string profile,bool support);
    internal abstract void LeftShotHaptics();
    internal abstract void RopeHaptics();
    internal abstract void ResistanceHaptics(float amplitude,bool right=false);
    internal abstract void ReloadHaptics(ReloadAction action,bool rightReloads=false);
    internal abstract void SwimHaptics(float amplitude);
    internal abstract void PunchHaptics(bool right);
    internal abstract void TickHaptics(bool allowed);
    public abstract void RefreshEyes();
    public abstract void RefreshPoses(bool force=false);
    public abstract void ReadButtons();
    internal abstract string FrameTimingReport();
    public abstract void Dispose();
}
