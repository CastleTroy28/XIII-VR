// Executes the real Plugin.cs control flow against simulated Unity/BepInEx APIs.
// This does not load IL2CPP, render frames, or test Windows/Pimax integration.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;
using XiiiXR;

internal static class RegressionHarness
{
    private static object Field(Bootstrap b, string name) => typeof(Bootstrap).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(b)!;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
    private static Bootstrap New()
    {
        InteractionDriver.FailConstruct=false; InteractionDriver.FailTick=false; InteractionDriver.Disposed=false; ControllerAim.Count=0; ControllerAim.Fail=false;
        WeaponHands.FailConstruct = false; WeaponHands.FailTick = false; WeaponHands.Disposed = false; WeaponHands.ToggleCount = 0;
        LocomotionDriver.FailConstruct = false; LocomotionDriver.FailTick = false; LocomotionDriver.Disposed = false; LocomotionDriver.ToggleCount = 0; CameraRig.Disposed = false; CameraRig.FailTick = false;
        CameraRig.FrontendFlag=false;QualityOptions.AutoStart.Value=false;
        Application.isFocused=true;CameraRig.Resets=CameraRig.Haptics=ControllerAim.Resets=0;QualityMenu.Action="";Input.Keys.Clear(); MonoBehaviour.FailCoroutine = false; Time.Value = 1; Camera.Fail = false;
        Plugin.Output = new BepInEx.Logging.ManualLogSource(); Plugin.TrackingDriverAvailable = true;
        SubsystemManager.Display = new XRDisplaySubsystem();
        SubsystemManager.InputSubsystem = new XRInputSubsystem();
        return new Bootstrap(IntPtr.Zero);
    }
    private static void Start(Bootstrap b) { Input.Keys.Add(KeyCode.F9); b.Update(); }
    private static void StillRunning(Bootstrap b)
    {
        Check((bool)Field(b,"active"),"active was cleared by diagnostics");
        Check(SubsystemManager.Display.running && SubsystemManager.InputSubsystem.running,"diagnostics stopped subsystem");
        Check(SubsystemManager.Display.StopCount == 0 && SubsystemManager.Display.DestroyCount == 0,"display was torn down");
    }
    public static void Main()
    {
        var b = New(); Camera.Fail = true; Start(b);
        StillRunning(b);
        Check((bool)Field(b,"diagnosticsDisabled"),"failing diagnostic was not disabled");
        Time.Value = 30; b.Update(); StillRunning(b);
        Input.Keys.Add(KeyCode.F10); b.Update();
        Check(!(bool)Field(b,"active"),"F10 failed after diagnostic exception");
        Check(SubsystemManager.Display.StopCount == 1 && SubsystemManager.Display.DestroyCount == 1,"F10 display lifecycle");
        Check(SubsystemManager.InputSubsystem.StopCount == 1 && SubsystemManager.InputSubsystem.DestroyCount == 1,"F10 input lifecycle");
        Console.WriteLine("PASS: injected diagnostic exception preserves XR; F10 still stops and destroys both subsystems.");
        Check(OpenXrLoader.Attempts > 0 && Plugin.Output.Lines.Exists(l => l.Contains("START the OpenVR way instead")), "Auto did not try the own OpenXR first and then the OpenVR way");
        Console.WriteLine("PASS: Auto tries the mod's own OpenXR first; when it is not available the OpenVR way starts.");

        b = New(); Start(b); StillRunning(b);
        Check(!(bool)Field(b,"diagnosticsDisabled"),"healthy diagnostics disabled");
        int count = Plugin.Output.Lines.Count; b.Update(); Check(Plugin.Output.Lines.Count == count,"diagnostic throttle");
        Time.Value = 12; b.Update(); Check(Plugin.Output.Lines.Count > count,"periodic diagnostics stopped");
        StillRunning(b);
        Console.WriteLine("PASS: healthy diagnostics remain periodic without XR teardown.");

        b = New(); Plugin.Output.Fail = true; Camera.Fail = true; Start(b); StillRunning(b);
        Input.Keys.Add(KeyCode.F10); b.Update();
        Check(SubsystemManager.Display.DestroyCount == 1,"broken log sink prevented explicit stop");
        Console.WriteLine("PASS: simultaneous diagnostic/log-sink errors do not stop XR or prevent F10.");

        b = New(); SubsystemManager.Display.ThrowOnStart = true; Start(b);
        Check(!(bool)Field(b,"active"),"failed startup became active");
        Check(SubsystemManager.Display.DestroyCount == 1 && SubsystemManager.InputSubsystem.DestroyCount == 1,"failed startup did not release partial resources");
        Console.WriteLine("PASS: genuine startup failure still cleans up partial resources.");
        b = New(); Start(b); MonoBehaviour.FailCoroutine = true; Input.Keys.Add(KeyCode.F12); b.Update(); StillRunning(b);
        Input.Keys.Add(KeyCode.F10); b.Update(); Check(SubsystemManager.Display.DestroyCount==1,"capture exception prevented stop");
        Console.WriteLine("PASS: screenshot coroutine-bridge failure preserves XR and F10.");
        b = New(); LocomotionDriver.FailConstruct = true; Start(b); StillRunning(b);
        Check((bool)Field(b,"locomotionFailed"),"locomotion constructor exception not isolated");
        Check(!CameraRig.Disposed,"locomotion failure disposed head tracking");
        Console.WriteLine("PASS: failed locomotion hook setup preserves head tracking and XR.");
        b = New(); Start(b); LocomotionDriver.FailTick = true; b.Update(); StillRunning(b);
        Check(LocomotionDriver.Disposed && !CameraRig.Disposed,"locomotion tick failure did not isolate cleanup");
        Console.WriteLine("PASS: locomotion update exception disposes only locomotion driver.");
        b = New(); Start(b); Input.Keys.Add(KeyCode.F4); b.Update(); StillRunning(b);
        Check(LocomotionDriver.ToggleCount == 0 && !CameraRig.Disposed,"F4 unexpectedly disabled locomotion");
        Input.Keys.Add(KeyCode.F10); b.Update();
        Check(LocomotionDriver.Disposed && CameraRig.Disposed,"F10 leaked drivers");
        Check(SubsystemManager.Display.DestroyCount == 1,"F10 did not destroy XR after driver cleanup");
        Console.WriteLine("PASS: F4 cannot disable locomotion; F10 cleans locomotions before camera/XR teardown.");
        b = New(); WeaponHands.FailConstruct = true; Start(b); StillRunning(b);
        Check((bool)Field(b,"weaponsFailed") && !LocomotionDriver.Disposed && !CameraRig.Disposed,"weapon constructor failure broke locomotion/tracking");
        b = New(); Start(b); WeaponHands.FailTick = true; b.Update(); StillRunning(b);
        Check(WeaponHands.Disposed && !LocomotionDriver.Disposed && !CameraRig.Disposed,"weapon tick failure broke locomotion/tracking");
        Console.WriteLine("PASS: weapon constructor/update failures preserve locomotion, head tracking and XR.");
        b = New(); Start(b); Input.Keys.Add(KeyCode.F8); b.Update(); StillRunning(b);
        Check(WeaponHands.ToggleCount == 1 && LocomotionDriver.ToggleCount == 0,"F8 toggles wrong driver");
        Input.Keys.Add(KeyCode.F10); b.Update();
        Check(WeaponHands.Disposed && LocomotionDriver.Disposed && CameraRig.Disposed,"F10 leaked weapon or movement drivers");
        Console.WriteLine("PASS: F8 toggles weapons alone; F10 releases weapons before movement and tracking.");
        b = New(); Start(b); CameraRig.FailTick = true; b.Update(); StillRunning(b);
        Check(WeaponHands.Disposed && LocomotionDriver.Disposed && CameraRig.Disposed,"tracking failure leaves controls alive");
        Console.WriteLine("PASS: tracking setup failure releases weapon/movement controls while preserving XR display.");
        b=New(); InteractionDriver.FailConstruct=true; Start(b); StillRunning(b);
        Check((bool)Field(b,"interactionFailed") && !WeaponHands.Disposed && !LocomotionDriver.Disposed,"interaction constructor broke other controls");
        b=New(); Start(b); InteractionDriver.FailTick=true; b.Update(); StillRunning(b);
        Check(InteractionDriver.Disposed && !WeaponHands.Disposed && !LocomotionDriver.Disposed,"interaction tick error broke weapons/movement");
        Console.WriteLine("PASS: interaction setup/update failures isolated from weapons, movement and XR.");
        // 0.1.157: F5 alone (often the game's quick save) does not start it.
        b=New(); Start(b); Input.Keys.Add(KeyCode.F5); b.Update(); Time.Value=5; b.Update(); Check(ControllerAim.Count==0,"F5 alone started an aim calibration");
        Time.Value=0; Input.Held.Add(KeyCode.LeftShift);
        b=New(); Start(b); Input.Keys.Add(KeyCode.F5); b.Update();
        Check(ControllerAim.Count==0,"calibration did not wait for natural grip");
        Time.Value=3.9f; b.Update(); Check(ControllerAim.Count==0,"calibrated before countdown");
        Time.Value=4; b.Update(); Check(ControllerAim.Count==1,"calibration did not execute after countdown");
        b.Update(); Check(ControllerAim.Count==1,"calibration repeats");
        ControllerAim.Fail=true; Input.Keys.Add(KeyCode.F5); b.Update(); Time.Value=7; b.Update(); StillRunning(b);
        Check(!WeaponHands.Disposed && !InteractionDriver.Disposed,"calibration failure stopped controls");
        Input.Keys.Add(KeyCode.F10); b.Update(); Check(InteractionDriver.Disposed,"F10 leaked interaction");
        b=New(); Start(b); Input.Keys.Add(KeyCode.F5); b.Update(); Input.Keys.Add(KeyCode.F10); b.Update(); Time.Value=10; b.Update();
        Check(ControllerAim.Count==0,"queued calibration survived XR stop");
        Input.Held.Clear();
        Console.WriteLine("PASS: Shift+F5 (not F5 alone) has three-second delay, executes once, tolerates failure and cancels on F10.");
        b=New();QualityOptions.AutoStart.Value=true;CameraRig.FrontendFlag=true;Time.Value=3;b.Update();StillRunning(b);
        Check(Field(b,"weapons")==null&&Field(b,"locomotion")==null&&Field(b,"interaction")==null,"frontend creates gameplay adapters before game systems exist");
        Check((bool)Field(b,"attempted"),"auto startup never attempted without F9");
        b.LateUpdate();CameraRig.FrontendFlag=false;b.Update();StillRunning(b);
        Check(Field(b,"weapons")!=null&&Field(b,"locomotion")!=null&&Field(b,"interaction")!=null,"menu-to-level transition does not initialize gameplay controls");
        Check(!(bool)Field(b,"weaponsFailed")&&!(bool)Field(b,"locomotionFailed"),"frontend startup permanently fails game input");
        Console.WriteLine("PASS: automatic frontend startup without F9; gameplay adapters deferred until player context; menu-to-level handoff keeps XR running.");
        b=New();Start(b);QualityMenu.Action="reset";b.Update();StillRunning(b);
        Check(ControllerAim.Resets==1&&CameraRig.Resets==1,"settings hand reset not dispatched");b.Update();Check(ControllerAim.Resets==1,"reset repeats");
        QualityMenu.Action="haptics";b.Update();Check(CameraRig.Haptics==1,"settings haptics test not dispatched");
        QualityMenu.Action="calibrate";b.Update();Application.isFocused=false;Time.Value=5;b.Update();Application.isFocused=true;b.Update();
        Check(ControllerAim.Count==0,"calibrates hand while game minimized");StillRunning(b);
        Console.WriteLine("PASS: recovery actions consumed once without restarting XR; focus loss cancels delayed calibration.");
        Console.WriteLine("Native IL2CPP/Windows/Pimax behavior remains untested by this harness.");
    }
}
namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class BepInPlugin : Attribute { public BepInPlugin(string a,string b,string c) {} }
    [AttributeUsage(AttributeTargets.Class)] public sealed class BepInProcess : Attribute { public BepInProcess(string a) {} }
}
namespace BepInEx.Logging
{
    public sealed class ManualLogSource
    {
        public readonly List<string> Lines = new(); public bool Fail;
        public void LogInfo(object value) { if(Fail) throw new System.IO.IOException("injected broken log sink"); Lines.Add(value.ToString()!); }
        public void LogWarning(object value) => LogInfo(value);
    }
}
namespace BepInEx.Unity.IL2CPP
{
    public abstract class BasePlugin
    {
        public BepInEx.Logging.ManualLogSource Log = new();
        public object Config = new();
        public abstract void Load(); public void AddComponent<T>() {}
    }
}
namespace Il2CppInterop.Runtime
{
    public static class IL2CPP
    {
        public static IntPtr GetIl2CppClass(string a,string b,string c) => new IntPtr(1);
        public static void il2cpp_runtime_class_init(IntPtr p) {}
    }
}
namespace UnityEngine
{
    public class MonoBehaviour { public MonoBehaviour(IntPtr p) {} public static bool FailCoroutine; public void StartCoroutine(System.Collections.IEnumerator r) { if(FailCoroutine) throw new InvalidOperationException("injected coroutine bridge failure"); } }
    public enum KeyCode { Return, F2, F4, F5, F6, F7, F8, F9, F10, F11, F12, LeftShift, RightShift }
    public enum StereoTargetEyeMask { Both }
    public static class Input { public static readonly HashSet<KeyCode> Keys = new(),Held = new(); public static bool GetKeyDown(KeyCode k) => Keys.Remove(k); public static bool GetKey(KeyCode k) => Held.Contains(k); }
    public static class Time { public static float Value; public static float realtimeSinceStartup => Value; }
    public sealed class Camera
    {
        public static bool Fail;
        public static Camera[] allCameras => Fail ? throw new TypeInitializationException("injected diagnostic interop failure", new System.Runtime.InteropServices.SEHException()) : new[] { new Camera() };
        public string name => "test camera"; public bool stereoEnabled => true; public StereoTargetEyeMask stereoTargetEye => StereoTargetEyeMask.Both;
    }
    public class ISubsystemDescriptor
    {
        public ISubsystemDescriptor(IntPtr p) { Pointer=p; m_Ptr=p; }
        public IntPtr Pointer, m_Ptr; public string id => Pointer.ToInt32()==1 ? "OpenVR Input" : "OpenVR Display";
    }
    public class IntegratedSubsystem
    {
        public bool running; public int StopCount,DestroyCount; public bool ThrowOnStart; public ISubsystemDescriptor? m_SubsystemDescriptor;
        public void Start() { if(ThrowOnStart) throw new InvalidOperationException("injected native startup failure"); running=true; }
        public void Stop() { running=false; StopCount++; } public void Destroy() { running=false; DestroyCount++; }
        public T? TryCast<T>() where T:class => this as T;
    }
    public static class SubsystemDescriptorBindings { public static IntPtr Create(IntPtr p) => p; }
    public static class SubsystemManager
    {
        public static XRDisplaySubsystem Display = new(); public static XRInputSubsystem InputSubsystem = new();
        public static IntegratedSubsystem GetIntegratedSubsystemByPtr(IntPtr p) => p.ToInt32()==1 ? InputSubsystem : Display;
    }
}
namespace UnityEngine.SubsystemsImplementation
{
    public static class SubsystemDescriptorStore
    {
        public static readonly List<ISubsystemDescriptor> s_IntegratedDescriptors = new() { new(new IntPtr(1)), new(new IntPtr(2)) };
    }
}
namespace UnityEngine.XR
{
    public sealed class XRDisplaySubsystem : IntegratedSubsystem { public float scaleOfAllRenderTargets; }
    public sealed class XRInputSubsystem : IntegratedSubsystem {}
    public static class XRSettings { public static bool enabled => SubsystemManager.Display.running; public static bool isDeviceActive => enabled; }
}

namespace Il2CppInterop.Runtime.Injection { public static class ClassInjector { public static void RegisterTypeInIl2Cpp<T>() {} } }
namespace XiiiXR { public class TrackedCamera {} internal sealed class CameraRig : System.IDisposable
{internal void ResetRenderCaches(){} internal static int Resets,Haptics;internal void ResetHandAlignment(){Resets++;}internal void TestHaptics(){Haptics++;}
        internal static bool FrontendFlag;internal bool Frontend=>FrontendFlag;internal void MenuLateUpdate(){}
    public static bool Disposed, FailTick;
    public void Tick() { if(FailTick) throw new InvalidOperationException("injected tracking failure"); }
    public void RequestRecenter() {} public void ToggleEffects() {} public void DisarmTrigger() {}
    public void Dispose() { Disposed = true; }
} }

namespace BepInEx { public static class Paths { public static string GameRootPath => "/tmp/xiii-test-root"; public static string ConfigPath => "/tmp/xiii-test-root/config"; } }
namespace UnityEngine { public static class ScreenCapture { public enum StereoScreenCaptureMode { BothEyes } public static void CaptureScreenshot(string file,int size,StereoScreenCaptureMode mode) {} } }

namespace BepInEx.Unity.IL2CPP.Utils.Collections { public static class CollectionExtensions { public static System.Collections.IEnumerator WrapToIl2Cpp(this System.Collections.IEnumerator e) => e; } }
namespace XiiiXR { internal sealed class NpcHitReactions { internal static NpcHitReactions? Current=null; internal void FromLateUpdate(){} } }
namespace XiiiXR { internal sealed class CeilingFans { internal static CeilingFans? Current=null; internal void LateTick(){} } }
namespace XiiiXR { internal static class AudioLanguages { internal static int Installs; internal static void Install(){Installs++;} } }
namespace XiiiXR { internal static class RenderBudget { internal static int Ticks,Releases; internal static void Tick(UnityEngine.XR.XRDisplaySubsystem d,CameraRig? r){Ticks++;} internal static void Release(){Releases++;} } }
namespace XiiiXR { internal static class EyeCapture { internal static System.Collections.IEnumerator Capture(UnityEngine.XR.XRDisplaySubsystem d) { yield break; } } }
namespace XiiiXR { internal static class StereoCheck { internal static int Ticks; internal static void Tick(UnityEngine.MonoBehaviour owner,UnityEngine.XR.XRDisplaySubsystem? d,CameraRig? r){Ticks++;} } }

namespace XiiiXR
{
    internal static class LocomotionOptions { internal static void Load(object c) {} }
    internal sealed class LocomotionDriver : System.IDisposable
    {
        internal static bool FailConstruct, FailTick, Disposed;
        internal static int ToggleCount;
        internal LocomotionDriver(CameraRig r) { if(FailConstruct) throw new InvalidOperationException("injected patch failure"); }
        internal void Toggle() { ToggleCount++; }
        internal void Tick() { if(FailTick) throw new InvalidOperationException("injected locomotion tick failure"); }
        public void Dispose()
        {
            if(CameraRig.Disposed) throw new InvalidOperationException("camera disposed before locomotions");
            Disposed = true;
        }
    }
}

namespace XiiiXR
{
    internal static class WeaponOptions { internal static void Load(object c) {} }
    internal static class EnemyOptions { internal static void Load(object c) {} }
    internal static class RuntimeOptions { internal sealed class Entry { public string Value=""; } internal static Entry? Runtime=null; internal static void Load(object c) {} }
    // 0.1.181: the own OpenXR is tried first (Auto); here it is never available, so the OpenVR way runs (the scenarios below).
    internal sealed class OpenXrLoader
    {
        internal enum Phase { Waiting, Starting, Running, Failed, Stopped }
        internal static int Attempts;
        internal Phase State { get; private set; } = Phase.Waiting;
        internal string Failure => "not in this harness";
        internal const float ReadyTimeout = 30;
        internal static OpenXrLoader? Current => null;
        internal bool Initialize() { Attempts++; State = Phase.Failed; return false; }
        internal bool CreateSession() => false;
        internal Phase Tick(XRDisplaySubsystem? d, XRInputSubsystem? i) => State;
        internal void Shutdown(ref XRDisplaySubsystem? d, ref XRInputSubsystem? i) { d = null; i = null; }
        internal void GiveUp(string why) { State = Phase.Failed; }
    }
    internal static class OpenXrTracking { internal static bool ViewsReady() => false; }
    internal static class FramePerformance { internal const int Rig=0,Move=1,Weapons=2,Interact=3,Late=4,Render=5; internal static long Begin()=>0; internal static long Begin(int part)=>0; internal static void Part(int part,long start) {} }
    internal static class ChairSolveCache { internal static string? FilePath {get;set;} }
    internal sealed class WeaponHands : System.IDisposable
    {
        internal static bool FailConstruct, FailTick, Disposed;
        internal static int ToggleCount;
        internal WeaponHands(CameraRig r) { if(FailConstruct) throw new InvalidOperationException("injected weapon hook failure"); }
        internal void Toggle() { ToggleCount++; }
        internal void Tick() { if(FailTick) throw new InvalidOperationException("injected weapon tick failure"); }
        public void Dispose()
        {
            if(CameraRig.Disposed) throw new InvalidOperationException("camera disposed before weapons");
            Disposed = true;
        }
    }
}

namespace XiiiXR
{
    internal static class ControllerAim
    {
        internal static int Count,Resets; internal static bool Fail;internal static void Reset(){Resets++;}
        internal static void Load(object c) {}
        internal static void Calibrate(CameraRig r) { Count++; if(Fail) throw new InvalidOperationException("injected calibration failure"); }
    }
    internal sealed class InteractionDriver : System.IDisposable
    {
        internal static bool FailConstruct,FailTick,Disposed;
        internal InteractionDriver(CameraRig r) { if(FailConstruct) throw new InvalidOperationException("injected interaction hook failure"); }
        internal void Tick() { if(FailTick) throw new InvalidOperationException("injected interaction update failure"); }
        public void Dispose() { if(CameraRig.Disposed) throw new InvalidOperationException("tracking disposed before interaction"); Disposed=true; }
    }
}

namespace XiiiXR {
 internal static class QualityOptions { internal sealed class Entry{internal bool Value {get;set;}} internal static Entry AutoStart=new(); internal static void Load(object c){} }
 internal static class StartupLogos {internal static void Install(){}}
 internal static class QualityMenu {internal static string Action="";internal static string TakeAction(){var a=Action;Action="";return a;} internal static int AppliedRevision=>0; internal static bool Open=>false; internal static void Apply(){} internal static void Toggle(){} internal static bool StartupReady()=>true; internal static void Close(){} internal static void Attach(UnityEngine.XR.XRDisplaySubsystem d){d.scaleOfAllRenderTargets=.75f;} internal static void UpdateDisplay(UnityEngine.XR.XRDisplaySubsystem d){} }
}

namespace UnityEngine{public static class Application{public static bool isFocused=true;}}

namespace XiiiXR {static class StartupFocus {internal static void Tick(bool active){}}}
namespace XiiiXR {static class WindowFocus {internal static bool InVr;internal static void Tick(bool vr,bool moving){InVr=vr;}internal static void Restore(){}} static class FreezeWatch {internal static bool HeadMoving=>false;internal static void Note(string s){}}}
