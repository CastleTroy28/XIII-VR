using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
namespace XiiiXR;
// 0.1.149. One likely cause: the game's window on
// the desktop loses Windows focus for a moment (a notification, another
// program's window). A game that is not set to run in the background stops
// then - the picture in the headset stands still - and the mod stopped its
// hands, weapons and walking without focus too.
//  - in VR the game runs in the background (Application.runInBackground);
//  - the mod's gameplay (walking, hands, weapons, grabbing) goes on without
//    Windows focus while in VR (Playable); keys the mod types for the game's
//    menus still need it;
//  - who took the focus is written in the log (FreezeWatch);
//  - with the headset worn (the head moving), the game's window takes the
//    focus back after a moment (at most a few tries, not while the headset
//    lies still - someone at the desktop).
internal static class WindowFocus
{
    internal static bool InVr;
    // Gameplay may go on: the window has focus, or VR is running.
    internal static bool Playable=>InVr||Application.isFocused;
    private static bool background,backgroundWas,backgroundSet;
    private static float lostAt=-1,nextTry;private static int tries;
    internal const float RegainAfter=1.5f,RetryEvery=4f;internal const int MaxTries=3;
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window,StringBuilder text,int size);
    private static readonly Dictionary<IntPtr,string> names=new();
    // Once VR runs: the game goes on in the background.
    internal static void Tick(bool vr,bool headMoving)
    {
        InVr=vr;
        if(vr&&!backgroundSet)
        {
            backgroundSet=true;
            try{backgroundWas=Application.runInBackground;Application.runInBackground=true;background=true;
                Bootstrap.Write("FOCUS the game runs in the background in VR (it was set to "+(backgroundWas?"run":"stop")+" without Windows focus); the mod's gameplay goes on without focus");}
            catch(Exception ex){Bootstrap.Warn("FOCUS run in the background: "+ex.Message);}
        }
        if(!vr||!OperatingSystem.IsWindows()||!StartupFocus.Settled)return;
        float now=Time.realtimeSinceStartup;
        // 0.1.236: the window really in front (Unity's isFocused stays true for a game started behind Steam).
        if(StartupFocus.GameInFront()){lostAt=-1;tries=0;return;}
        if(lostAt<0){lostAt=now;nextTry=now+RegainAfter;}
        // Taken back only while the headset is worn (someone at the desktop keeps it).
        if(!headMoving||tries>=MaxTries||now<nextTry)return;
        nextTry=now+RetryEvery;tries++;
        bool ok=StartupFocus.BringForward();
        Bootstrap.Write("FOCUS taken back from "+(Foreground()??"another program")+" (try "+tries+" of "+MaxTries+(ok?"":", Windows refused")+")");
    }
    // The program whose window has the focus now (null: the game's own, or unknown).
    internal static string? Foreground()
    {
        if(!OperatingSystem.IsWindows())return null;
        try
        {
            var h=GetForegroundWindow();if(h==IntPtr.Zero)return "no window";
            if(names.TryGetValue(h,out var n))return n.Length>0?n:null;
            GetWindowThreadProcessId(h,out uint pid);
            if(pid==(uint)Environment.ProcessId){names[h]="";return null;}
            string process="?";try{using var p=Process.GetProcessById((int)pid);process=p.ProcessName;}catch(Exception){}
            var title=new StringBuilder(96);GetWindowText(h,title,title.Capacity);
            n=process+(title.Length>0?" ('"+title+"')":"");
            if(names.Count>64)names.Clear();names[h]=n;return n;
        }
        catch(Exception){return null;}
    }
    internal static void Restore()
    {
        if(!background)return;background=false;backgroundSet=false;
        try{Application.runInBackground=backgroundWas;}catch(Exception){}
    }
}
