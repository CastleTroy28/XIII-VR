using BepInEx.Configuration;
namespace XiiiXR;
internal static class LocomotionOptions
{
    internal static ConfigEntry<bool> Enabled = null!, HideModel = null!;
    internal static ConfigEntry<float> Deadzone = null!, TurnSpeed = null!;
    internal static ConfigEntry<int> LeftStickAxis = null!, RightStickAxis = null!;
    internal static ConfigEntry<bool> Teleport=null!,SnapTurn=null!;
    internal static ConfigEntry<float> SnapAngle=null!;
    internal static void Load(ConfigFile config)
    {
        Enabled = config.Bind("Locomotion", "Enabled", true, "Enable left-stick movement and right-stick turn/jump/crouch.");
        HideModel = config.Bind("Locomotion", "HideFirstPersonModel", true, "Hide original first-person arms/weapon during the locomotion test. Restored on F10.");
        Deadzone = config.Bind("Locomotion", "StickDeadzone", 0.2f, "Radial left-stick deadzone and horizontal-turn deadzone.");
        TurnSpeed = config.Bind("Locomotion", "TurnDegreesPerSecond", 75f, "Maximum smooth horizontal turn speed. 15 to 180 degrees/second.");
        Teleport=config.Bind("Locomotion","Teleport",false,"Left stick forward: aim with left controller; release stick to teleport to a clear reachable floor. Backward cancels. False keeps smooth head-relative walking.");
        SnapTurn=config.Bind("Locomotion","SnapTurn",false,"One turn per right-stick deflection; return to centre before the next turn.");
        SnapAngle=config.Bind("Locomotion","SnapTurnAngle",30f,"Snap turn angle in degrees, 15 to 90.");
        LeftStickAxis = config.Bind("Locomotion", "LeftStickAxis", -1, "OpenVR joystick axis index: -1 autodetect; 0..4 override.");
        RightStickAxis = config.Bind("Locomotion", "RightStickAxis", -1, "OpenVR joystick axis index: -1 autodetect; 0..4 override.");
    }
}
