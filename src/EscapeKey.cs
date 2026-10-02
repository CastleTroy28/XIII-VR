using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// One short physical Escape keystroke for native Unity/Rewired/tutorial paths.
// Key-down is permitted only in this game's foreground window. No global hook.
// 0.1.158: the reason is written; the game's window is
// brought to the front first when another program's window is there; a key
// Windows still reads as held (its release lost) is released, then pressed.
internal static class EscapeKey
{
    [StructLayout(LayoutKind.Explicit,Size=40)]
    private struct InputEvent
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public ushort key;
        [FieldOffset(12)] public uint flags;
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint id);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,InputEvent[] input,int size);
    // Why the last key-down was not typed ("" when it was).
    internal static string LastRefusal="";
    internal static bool Send(bool down)=>SendKey(0x1b,down);
    internal static bool SendKey(ushort virtualKey,bool down)
    {
        LastRefusal="";
        try
        {
            if(!OperatingSystem.IsWindows()){LastRefusal="not Windows";return false;}
            bool held=false;
            if(down)
            {
                if(!OwnForeground())
                {
                    bool brought=StartupFocus.BringForward();
                    if(!OwnForeground())
                    {
                        LastRefusal="the game's window is not in front: "+(WindowFocus.Foreground()??"no window")+(brought?"":", Windows refused to bring the game forward");
                        return false;
                    }
                    Bootstrap.Write("VR MENU key: the game's window brought to the front first");
                }
                held=(GetAsyncKeyState(virtualKey)&0x8000)!=0;
            }
            var keys=held?new[]{Key(virtualKey,false),Key(virtualKey,true)}:new[]{Key(virtualKey,down)};
            bool ok=SendInput((uint)keys.Length,keys,40)==(uint)keys.Length;
            if(!ok){LastRefusal="SendInput failed: "+Marshal.GetLastWin32Error();Bootstrap.Warn("VR MENU key "+LastRefusal);}
            else if(held)Bootstrap.Write("VR MENU key 0x"+virtualKey.ToString("X2")+": Windows still read it as held (its release was lost); released, then pressed");
            return ok;
        }
        catch(Exception ex){LastRefusal=ex.Message;Bootstrap.Warn("VR MENU key unavailable: "+ex.Message);return false;}
    }
    private static InputEvent Key(ushort key,bool down)=>new InputEvent{type=1,key=key,flags=down?0u:2u};
    private static bool OwnForeground()
    {
        GetWindowThreadProcessId(GetForegroundWindow(),out uint id);
        return id==(uint)Environment.ProcessId;
    }
}
// 0.1.158: the game's pause menu opened by the mod itself when Escape cannot
// be typed (as the game does for its pause key; not while the game blocks it).
internal static class GamePause
{
    internal static bool TryOpen(out string how)
    {
        how="";
        try
        {
            if(PauseMenuControl.HackGameIsPaused){how="the game is paused already";return false;}
            if(PauseMenuControl.BlockTogglePause){how="the game does not allow its pause menu right now";return false;}
            foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<PauseMenuControl>()))
            {
                var pause=obj.TryCast<PauseMenuControl>();
                if(pause==null||!pause.gameObject.scene.IsValid()||!pause.isActiveAndEnabled)continue;
                pause.TriggerPauseSequence();
                return true;
            }
            how="no pause menu in this scene";return false;
        }
        catch(Exception ex){how="the game's pause menu: "+ex.Message;return false;}
    }
}
