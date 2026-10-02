using System;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
namespace XiiiXR;
// Bounded startup request, never a permanent topmost window or an Alt-Tab loop.
internal static class StartupFocus
{
    private static bool started,done;
    internal static bool Settled=>done;
    private static float next,until;
    private delegate bool WindowVisitor(IntPtr window,IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor,IntPtr state);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,StringBuilder name,int size);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h,int command);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
    internal static void Tick(bool vrActive)
    {
        if(done||!vrActive||!OperatingSystem.IsWindows())return;
        float now=Time.realtimeSinceStartup;
        if(!started){started=true;next=now+.5f;until=now+12;return;}
        if(now<next)return;next=now+1;
        if(Application.isFocused){done=true;return;}
        if(now>until){done=true;Bootstrap.Warn("VR startup focus request expired; Windows did not grant foreground focus.");return;}
        try{BringForward();}
        catch(Exception ex){done=true;Bootstrap.Warn("VR startup focus: "+ex.Message);}
    }
    // The game's own window to the front, without changing its size or place
    // (0.1.149: also used to take the focus back in VR, WindowFocus).
    internal static bool BringForward()
    {
        IntPtr h=IntPtr.Zero;
        EnumWindows((window,_)=>{GetWindowThreadProcessId(window,out uint id);
            if(id!=(uint)Environment.ProcessId)return true;
            var name=new StringBuilder(128);GetClassName(window,name,name.Capacity);
            if(name.ToString()!="UnityWndClass")return true;h=window;return false;},IntPtr.Zero);
        if(h==IntPtr.Zero)return false;
        if(IsIconic(h))ShowWindow(h,9);
        SetWindowPos(h,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0040);
        try{return SetForegroundWindow(h);}
        finally{SetWindowPos(h,new IntPtr(-2),0,0,0,0,0x0001|0x0002|0x0010);}
    }
}
