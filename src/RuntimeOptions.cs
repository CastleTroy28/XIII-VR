using System;
using BepInEx.Configuration;
namespace XiiiXR;
// 0.1.180: [VR] Runtime (RuntimeChoice), read at the next game start.
// 0.1.181: Auto / OpenXR / OpenVR / SteamVR / OpenComposite.
internal static class RuntimeOptions
{
    internal static ConfigEntry<string> Runtime=null!;
    internal static void Load(ConfigFile c)
    {
        Runtime=c.Bind("VR","Runtime","Auto",new ConfigDescription("Which way the game goes into VR (read at the next game start). Auto: the mod's own OpenXR into the Windows default OpenXR runtime (SteamVR, Pimax Play, Oculus/Meta, Virtual Desktop, WMR...), and when that cannot start, the OpenVR way. OpenXR: only the mod's own OpenXR. OpenVR: only the OpenVR way (SteamVR directly, or OpenComposite for another default runtime). SteamVR / OpenComposite: the OpenVR way through that one.",new AcceptableValueList<string>(RuntimeChoice.Values)));
        string value=RuntimeChoice.Normalize(Runtime.Value);
        try{Environment.SetEnvironmentVariable(RuntimeChoice.Variable,RuntimeChoice.SwitchMode(value));}catch(Exception){}
        Plugin.Output.LogInfo("[XIII-XR] VR RUNTIME setting "+value+(value=="Auto"?" (the mod's own OpenXR into the Windows default runtime; if it cannot start, the OpenVR way)":""));
    }
}
