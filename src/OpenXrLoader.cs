using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
namespace XiiiXR;
// 0.1.181: the game renders through Unity's own OpenXR plugin
// (UnityOpenXR.dll, com.unity.xr.openxr 1.8.2, the last for Unity 2020.3)
// into the Windows default OpenXR runtime (SteamVR, Pimax Play, Oculus/Meta,
// Virtual Desktop, WMR, ...), no OpenComposite. The game has no managed XR
// loader, so this does what Unity's OpenXRLoader does with the plugin's
// native calls: load the OpenXR loader, hand the plugin the mod's
// xrGetInstanceProcAddr wrapper (xiii_openxr.dll: input and tracking),
// initialize, create the display and input subsystems (Bootstrap), create the
// session, and when the runtime says it is ready: start the display, begin
// the session, start the input. Events come through the plugin's callback.
// Anything that fails before the session runs is a failure; Bootstrap then
// starts the OpenVR way (SteamVR or OpenComposite) as before.
internal sealed unsafe class OpenXrLoader
{
    internal enum Phase { Waiting, Starting, Running, Failed, Stopped }
    internal Phase State{get;private set;}=Phase.Waiting;
    internal string Failure{get;private set;}="";
    internal static OpenXrLoader? Current{get;private set;}
    private IntPtr unity,helper;
    private delegate* unmanaged[Cdecl]<byte*,byte> loadLibrary;
    private delegate* unmanaged[Cdecl]<void> unloadLibrary,requestExit,beginSession,endSession,destroySession,pump;
    private delegate* unmanaged[Cdecl]<IntPtr,void> setCallbacks,setProcAddress;
    private delegate* unmanaged[Cdecl]<IntPtr,IntPtr,uint,IntPtr,void> setApplicationInfo;
    private delegate* unmanaged[Cdecl]<byte> initializeSession,createSessionIfNeeded;
    private delegate* unmanaged[Cdecl]<int,void> setSuccessfullyInitialized,setRenderMode,setDepthMode;
    private delegate* unmanaged[Cdecl]<IntPtr,byte> requestExtension;
    private delegate* unmanaged[Cdecl]<int,IntPtr> getProcAddress;
    // xiii_openxr.dll
    internal static delegate* unmanaged[Cdecl]<IntPtr,IntPtr> Hook;
    internal static delegate* unmanaged[Cdecl]<IntPtr> Report;
    internal static delegate* unmanaged[Cdecl]<int> SessionState;
    internal static delegate* unmanaged[Cdecl]<float*,int*,int> Sync;
    internal static delegate* unmanaged[Cdecl]<float*,int*,int,int> Locate;
    internal static delegate* unmanaged[Cdecl]<int,float,float,int> Vibrate;
    internal static delegate* unmanaged[Cdecl]<int,int> StopVibration;
    // 0.1.235: the world scale on the plugin's own eyes (what Unity renders from and hands the
    // runtime): optional, an older xiii_openxr.dll has neither.
    internal static delegate* unmanaged[Cdecl]<float,void> SetViewScale;
    internal static delegate* unmanaged[Cdecl]<float*,void> Views;
    private static float appliedViewScale=1;
    // The plugin's eyes drawn 1/worldScale as far apart (PoseMath.ScaledEye on the mod's side). False: no helper for it.
    internal static bool ApplyWorldScale(float worldScale)
    {
        if(SetViewScale==null)return false;
        float k=PoseMath.ViewScale(worldScale);
        if(Math.Abs(k-appliedViewScale)>1e-4f){SetViewScale(k);appliedViewScale=k;}
        return true;
    }
    // "the plugin's eyes 0.0669 m apart, drawn 0.0744 m (n located, m moved)", or why not.
    internal static string ViewsReport()
    {
        if(Views==null)return "the plugin's eyes not reached (an older xiii_openxr.dll: install again)";
        var f=stackalloc float[4];Views(f);
        var c=System.Globalization.CultureInfo.InvariantCulture;
        if(f[2]<1)return "the plugin has located no eyes yet";
        return "the plugin's eyes "+MathF.Sqrt(Math.Max(0,f[0])).ToString("F4",c)+" m apart, drawn "+MathF.Sqrt(Math.Max(0,f[1])).ToString("F4",c)+" m ("+f[2].ToString("F0",c)+" located, "+f[3].ToString("F0",c)+" moved)";
    }
    internal static string HelperReport(){try{return Report==null?"":Marshal.PtrToStringAnsi(Report())??"";}catch(Exception){return "";}}
    // the plugin's events (OpenXRFeature.NativeEvent)
    internal enum NativeEvent { SetupConfigValues, SystemIdChanged, InstanceChanged, SessionChanged, BeginSession, SessionStateChanged, ChangedSpaceApp,
        EndSession, DestroySession, DestroyInstance, Idle, Ready, Synchronized, Visible, Focused, Stopping, Exiting, LossPending, InstanceLossPending,
        RestartRequested, RequestRestartLoop, RequestGetSystemLoop }
    private static volatile int lastEvent=-1;
    private static volatile bool ready,stopping,exiting,noHeadset;
    private float since,nextPump;
    private bool sessionBegun;
    internal const float ReadyTimeout=30;

    [DllImport("kernel32",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string name);
    private static string Plugins=>Path.Combine(BepInEx.Paths.GameRootPath,"XIII_Data","Plugins");
    private static IntPtr Optional(IntPtr module,string name)=>NativeLibrary.TryGetExport(module,name,out var f)?f:IntPtr.Zero;
    private IntPtr Export(IntPtr module,string name)
    {
        if(!NativeLibrary.TryGetExport(module,name,out var f))throw new InvalidOperationException(name+" missing");
        return f;
    }
    // Everything up to the subsystems. False: not available (Failure says why).
    internal bool Initialize()
    {
        Current=this;since=Time.realtimeSinceStartup;
        try
        {
            string unityPath=Path.Combine(Plugins,"UnityOpenXR.dll"),helperPath=Path.Combine(Plugins,"xiii_openxr.dll");
            if(!File.Exists(unityPath)||!File.Exists(helperPath)||!File.Exists(Path.Combine(Plugins,"openxr_loader.dll")))return Fail("UnityOpenXR.dll, openxr_loader.dll or xiii_openxr.dll not in XIII_Data\\Plugins (install again)");
            unity=GetModuleHandleW("UnityOpenXR.dll");if(unity==IntPtr.Zero)unity=NativeLibrary.Load(unityPath);
            helper=NativeLibrary.Load(helperPath);
            loadLibrary=(delegate* unmanaged[Cdecl]<byte*,byte>)Export(unity,"main_LoadOpenXRLibrary");
            unloadLibrary=(delegate* unmanaged[Cdecl]<void>)Export(unity,"main_UnloadOpenXRLibrary");
            setCallbacks=(delegate* unmanaged[Cdecl]<IntPtr,void>)Export(unity,"NativeConfig_SetCallbacks");
            setApplicationInfo=(delegate* unmanaged[Cdecl]<IntPtr,IntPtr,uint,IntPtr,void>)Export(unity,"NativeConfig_SetApplicationInfo");
            requestExit=(delegate* unmanaged[Cdecl]<void>)Export(unity,"session_RequestExitSession");
            initializeSession=(delegate* unmanaged[Cdecl]<byte>)Export(unity,"session_InitializeSession");
            createSessionIfNeeded=(delegate* unmanaged[Cdecl]<byte>)Export(unity,"session_CreateSessionIfNeeded");
            beginSession=(delegate* unmanaged[Cdecl]<void>)Export(unity,"session_BeginSession");
            endSession=(delegate* unmanaged[Cdecl]<void>)Export(unity,"session_EndSession");
            destroySession=(delegate* unmanaged[Cdecl]<void>)Export(unity,"session_DestroySession");
            pump=(delegate* unmanaged[Cdecl]<void>)Export(unity,"messagepump_PumpMessageLoop");
            setSuccessfullyInitialized=(delegate* unmanaged[Cdecl]<int,void>)Export(unity,"session_SetSuccessfullyInitialized");
            requestExtension=(delegate* unmanaged[Cdecl]<IntPtr,byte>)Export(unity,"unity_ext_RequestEnableExtensionString");
            getProcAddress=(delegate* unmanaged[Cdecl]<int,IntPtr>)Export(unity,"NativeConfig_GetProcAddressPtr");
            setProcAddress=(delegate* unmanaged[Cdecl]<IntPtr,void>)Export(unity,"NativeConfig_SetProcAddressPtrAndLoadStage1");
            setRenderMode=(delegate* unmanaged[Cdecl]<int,void>)Export(unity,"NativeConfig_SetRenderMode");
            setDepthMode=(delegate* unmanaged[Cdecl]<int,void>)Export(unity,"NativeConfig_SetDepthSubmissionMode");
            Hook=(delegate* unmanaged[Cdecl]<IntPtr,IntPtr>)Export(helper,"XO_Hook");
            Report=(delegate* unmanaged[Cdecl]<IntPtr>)Export(helper,"XO_Report");
            SessionState=(delegate* unmanaged[Cdecl]<int>)Export(helper,"XO_State");
            Sync=(delegate* unmanaged[Cdecl]<float*,int*,int>)Export(helper,"XO_Sync");
            Locate=(delegate* unmanaged[Cdecl]<float*,int*,int,int>)Export(helper,"XO_Locate");
            Vibrate=(delegate* unmanaged[Cdecl]<int,float,float,int>)Export(helper,"XO_Vibrate");
            StopVibration=(delegate* unmanaged[Cdecl]<int,int>)Export(helper,"XO_StopVibration");
            SetViewScale=(delegate* unmanaged[Cdecl]<float,void>)Optional(helper,"XO_SetViewScale");
            Views=(delegate* unmanaged[Cdecl]<float*,void>)Optional(helper,"XO_Views");appliedViewScale=1;
            ready=stopping=exiting=noHeadset=false;lastEvent=-1;
            setSuccessfullyInitialized(0);
            // The loader beside the plugin, by its full path (without .dll, as the plugin wants it).
            var loader=Encoding.Unicode.GetBytes(Path.Combine(Plugins,"openxr_loader")+"\0");
            fixed(byte* p=loader)if(loadLibrary(p)==0)return Fail("the OpenXR loader (openxr_loader.dll) did not load");
            IntPtr gipa=getProcAddress(1);if(gipa==IntPtr.Zero)return Fail("no xrGetInstanceProcAddr from the OpenXR loader");
            IntPtr hooked=Hook(gipa);if(hooked==IntPtr.Zero)return Fail("xiii_openxr.dll refused the loader");
            setProcAddress(hooked);
            if(initializeSession()==0)return Fail("no OpenXR runtime or headset (the default OpenXR runtime did not start)");
            SetApplicationInfo();
            // The HP Reverb G2 controller's bindings need its extension (others are core).
            RequestExtension("XR_EXT_hp_mixed_reality_controller");
            setCallbacks((IntPtr)(delegate* unmanaged[Cdecl]<int,ulong,void>)&OnNativeEvent);
            // Two eye textures (as the OpenVR way had); no depth to the runtime.
            setRenderMode(0);setDepthMode(0);
            Bootstrap.Write("OPENXR initialized (Unity's OpenXR plugin, the Windows default OpenXR runtime); creating the display and input");
            return true;
        }
        catch(Exception ex){return Fail(ex.Message);}
    }
    // After Bootstrap created the subsystems: the session.
    internal bool CreateSession()
    {
        try
        {
            if(createSessionIfNeeded()==0)return Fail("the OpenXR session was not created"+Why());
            State=Phase.Waiting;since=Time.realtimeSinceStartup;
            Bootstrap.Write("OPENXR session created; waiting for the runtime to be ready");
            return true;
        }
        catch(Exception ex){return Fail(ex.Message);}
    }
    // Every frame: the plugin's events; the display, session and input start when the runtime is ready.
    internal Phase Tick(XRDisplaySubsystem? display,XRInputSubsystem? input)
    {
        if(State==Phase.Failed||State==Phase.Stopped)return State;
        try
        {
            float now=Time.realtimeSinceStartup;
            // As Unity's loader: while idle or stopping, events are looked at every 0.1 s.
            int e=lastEvent;bool idle=e==(int)NativeEvent.Idle||e==(int)NativeEvent.Stopping||e==(int)NativeEvent.Exiting||e==(int)NativeEvent.LossPending||e==(int)NativeEvent.InstanceLossPending;
            if(!idle||now>=nextPump){nextPump=now+.1f;pump();}
            if(noHeadset){Fail("the OpenXR runtime has no headset ready"+Why());return State;}
            if(exiting){Bootstrap.Write("OPENXR the runtime ends the session (exiting)");State=Phase.Stopped;return State;}
            if(stopping&&sessionBegun){stopping=false;endSession();sessionBegun=false;Bootstrap.Write("OPENXR the runtime stopped the session; waiting for it to be ready again");State=Phase.Waiting;since=now;}
            if(ready&&!sessionBegun&&display!=null&&input!=null)
            {
                ready=false;State=Phase.Starting;
                // As Unity's loader: the display first (the input needs the session), then the session, then the input.
                display.Start();
                if(!display.running){Fail("the OpenXR display did not start");return State;}
                beginSession();sessionBegun=true;
                if(!input.running)input.Start();
                if(!input.running){Fail("the OpenXR input did not start");return State;}
                State=Phase.Running;
                Bootstrap.Write("OPENXR running: "+HelperReport());
            }
            if(State==Phase.Waiting&&now-since>ReadyTimeout)Fail("the runtime was not ready in "+ReadyTimeout.ToString("F0")+" s"+Why());
        }
        catch(Exception ex){Fail(ex.Message);}
        return State;
    }
    // Stop and take everything down (the subsystems in between, as Unity's loader does).
    internal void Shutdown(ref XRDisplaySubsystem? display,ref XRInputSubsystem? input)
    {
        try{if(input!=null&&input.running)input.Stop();}catch(Exception ex){Bootstrap.Warn("OPENXR input stop: "+ex.Message);}
        try{if(display!=null&&display.running)display.Stop();}catch(Exception ex){Bootstrap.Warn("OPENXR display stop: "+ex.Message);}
        try{if(sessionBegun&&endSession!=null){endSession();sessionBegun=false;}}catch(Exception ex){Bootstrap.Warn("OPENXR end session: "+ex.Message);}
        try{if(requestExit!=null){requestExit();pump();}}catch(Exception ex){Bootstrap.Warn("OPENXR exit: "+ex.Message);}
        try{input?.Destroy();}catch(Exception ex){Bootstrap.Warn("OPENXR input destroy: "+ex.Message);}
        try{display?.Destroy();}catch(Exception ex){Bootstrap.Warn("OPENXR display destroy: "+ex.Message);}
        input=null;display=null;
        try{if(destroySession!=null){destroySession();pump();}}catch(Exception ex){Bootstrap.Warn("OPENXR destroy session: "+ex.Message);}
        try{if(unloadLibrary!=null)unloadLibrary();}catch(Exception ex){Bootstrap.Warn("OPENXR unload: "+ex.Message);}
        if(State!=Phase.Failed)State=Phase.Stopped;
        if(Current==this)Current=null;
        Bootstrap.Write("OPENXR shut down");
    }
    internal void GiveUp(string why)=>Fail(why+Why());
    private bool Fail(string why)
    {
        State=Phase.Failed;Failure=why;
        Bootstrap.Warn("OPENXR not available: "+why);
        return false;
    }
    private static string Why(){string r=HelperReport();return r.Length>0?" ["+r+"]":"";}
    private void SetApplicationInfo()
    {
        string version=Application.version??"";uint hash=0;
        using(var md5=System.Security.Cryptography.MD5.Create())
        {var data=md5.ComputeHash(Encoding.UTF8.GetBytes(version));if(BitConverter.IsLittleEndian)Array.Reverse(data);hash=BitConverter.ToUInt32(data,0);}
        IntPtr name=Marshal.StringToHGlobalAnsi(Application.productName??"XIII"),v=Marshal.StringToHGlobalAnsi(version),engine=Marshal.StringToHGlobalAnsi(Application.unityVersion??"");
        try{setApplicationInfo(name,v,hash,engine);}
        finally{Marshal.FreeHGlobal(name);Marshal.FreeHGlobal(v);Marshal.FreeHGlobal(engine);}
    }
    private void RequestExtension(string name)
    {
        IntPtr s=Marshal.StringToHGlobalAnsi(name);
        try{bool ok=requestExtension(s)!=0;Bootstrap.Write("OPENXR extension "+name+(ok?" requested":" not offered by this runtime"));}
        finally{Marshal.FreeHGlobal(s);}
    }
    [UnmanagedCallersOnly(CallConvs=new[]{typeof(CallConvCdecl)})]
    private static void OnNativeEvent(int e,ulong payload)
    {
        try
        {
            lastEvent=e;
            switch((NativeEvent)e)
            {
                case NativeEvent.Ready:ready=true;break;
                case NativeEvent.Stopping:stopping=true;break;
                case NativeEvent.Exiting:case NativeEvent.LossPending:case NativeEvent.InstanceLossPending:exiting=true;break;
                case NativeEvent.RequestRestartLoop:case NativeEvent.RequestGetSystemLoop:noHeadset=true;break;
            }
            if(e>=(int)NativeEvent.Idle||e==(int)NativeEvent.InstanceChanged||e==(int)NativeEvent.SessionChanged)Bootstrap.Write("OPENXR event "+(NativeEvent)e);
        }
        catch(Exception){}
    }
}
