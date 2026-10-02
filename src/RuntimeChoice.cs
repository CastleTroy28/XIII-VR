using System;
namespace XiiiXR;
// 0.1.180:
// the mod's openvr_api.dll (buildtools\openvr-shim) chooses at each game
// start: the Windows default OpenXR runtime SteamVR (or none) -> SteamVR
// directly; any other (Pimax Play, Oculus/Meta, Virtual Desktop, ...) ->
// OpenComposite. The setting [VR] Runtime overrides it; it reaches the DLL
// as the process variable XIIIVR_RUNTIME (set before VR starts).
// 0.1.181: first of all the mod's own OpenXR (OpenXrLoader); the switch is the
// way when that is not available or not wanted.
internal static class RuntimeChoice
{
    internal const string Variable="XIIIVR_RUNTIME";
    // 0.1.181: Auto = the mod's own OpenXR (Unity's OpenXR plugin, the Windows
    // default runtime), and when that cannot start, the OpenVR way (the
    // runtime switch: SteamVR, or OpenComposite for another default runtime).
    // OpenXR: only the own OpenXR. OpenVR: only the OpenVR way (as 0.1.180).
    // SteamVR / OpenComposite: the OpenVR way through that one.
    internal static readonly string[] Values={"Auto","OpenXR","OpenVR","SteamVR","OpenComposite"};
    internal static string Normalize(string? value)
    {
        var v=(value??"").Trim();
        foreach(var known in Values)if(string.Equals(v,known,StringComparison.OrdinalIgnoreCase))return known;
        return "Auto";
    }
    internal static bool NativeOpenXr(string? value){var v=Normalize(value);return v=="Auto"||v=="OpenXR";}
    internal static bool FallBack(string? value)=>Normalize(value)=="Auto";
    // What the runtime switch (openvr-shim/shim.c) reads: auto / steamvr / openxr (= OpenComposite).
    internal static string SwitchMode(string? value)=>Normalize(value) switch{"SteamVR"=>"steamvr","OpenComposite"=>"openxr",_=>"auto"};
}
