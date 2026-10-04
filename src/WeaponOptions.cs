using BepInEx.Configuration;
namespace XiiiXR;
internal static class WeaponOptions
{
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<float> Pitch = null!, Yaw = null!, Roll = null!, GripRadius = null!, InertiaStrength=null!;
    internal static ConfigEntry<bool> PhysicalPunches=null!,ActionInput=null!,ManualReload=null!,NpcReactions=null!,NpcDisarm=null!,NpcGunGrab=null!,NpcFistFight=null!,NpcFistFightGameAi=null!,NpcPunchDisarm=null!,NpcDrawnByMod=null!;
    internal static ConfigEntry<float> NpcPunchDamage=null!;
    internal static ConfigEntry<float> SupportedSpread=null!,LeftPistolOffset=null!,ScopeReticleRoll=null!,ScopeSteadiness=null!;
    internal static void Load(ConfigFile config)
    {
        ScopeReticleRoll=config.Bind("Weapons","ScopeReticleRollDegrees",0f,"Extra turn of the scope cross (crossbow, SVD) about the view, degrees; + = counter-clockwise, -30..30. The cross is already lined up with the weapon's own level.");
        ScopeSteadiness=config.Bind("Weapons","ScopeSteadiness",1f,"With an eye at the scope (SVD, crossbow) the aim is steadied as if holding the breath: the hand's tremor and sway are smoothed away, a deliberate turn passes. 0 = off (the hands' aim as it is), 1 = default, up to 2 = steadier.");
        LeftPistolOffset=config.Bind("Weapons","LeftPistolRightOffsetMeters",.015f,"Local right offset of the left pistol relative to the tracked palm; meters, -0.04 to 0.04.");
        UiLanguage.ReadCode=()=>I2.Loc.LocalizationManager.CurrentLanguageCode;
        UiLanguage.ReadManual=()=>ManualReload.Value;UiLanguage.WriteManual=v=>ManualReload.Value=v;
        ActionInput=config.Bind("Controllers","SteamInputActions",true,"Use SteamVR Input buttons and haptic actions for oculus_touch. Restart game after changing.");
        ManualReload=config.Bind("Weapons","ManualReload",true,"Tap right B to drop magazine; hold B and left grip to extract. Left grip at left belt supplies magazines/shells (and the bazooka's rocket) when the gun needs them; push feed end into well; left grip pulls the pistol/AK/M16/M60 bolt and cycles the shotgun pump backward then forward after every shot. M60: B opens the cover, a hand pushes it shut; the box goes in where it was taken from. Crossbow: left grip at the belt takes a bolt, push it forward along the rail. Dropped ammo returns to reserve.");
        SupportedSpread=config.Bind("Weapons","TwoHandSpreadMultiplier",.3f,"Native spread multiplier while the support grip is held (pistol, revolver, AK, M16, Uzi, M60). SVD: 0 with two hands, twice this with one. Crossbow: always 0. Shotgun unchanged.");
        InertiaStrength=config.Bind("Weapons","InertiaStrength",1f,"Visual/aim inertia: 0 disables, 1 moderate, maximum 2. Support grip reduces inertia. Game menu > VR SETTINGS (Weapon inertia).");
        QualityOptions.WeaponInertia=InertiaStrength;
        PhysicalPunches=config.Bind("Hands","PhysicalPunches",true,"Swing a closed fist to hit NPCs while the native fists slot is selected.");
        NpcReactions=config.Bind("Hands","NpcHitReactions",true,"NPCs react to punches with their skeleton: a punch to the belly bends them over towards that side, an uppercut throws the head back, a hard punch pushes them back.");
        NpcGunGrab=config.Bind("Hands","NpcGunGrab",true,"Grip at the barrel of an enemy's gun holds it (it cannot shoot, push it aside); a blow of the other hand (open palm or fist) or a yank makes the enemy let go of it into that hand.");
        NpcDisarm=config.Bind("Hands","NpcDisarm",true,"An enemy lets go of its gun when you hold it by the barrel and yank it, or hit the enemy with your other hand; it goes on with its fists.");
        NpcDrawnByMod=config.Bind("Hands","NpcDrawnByMod",true,"While an enemy reacts to a blow or fights with its fists, the mod draws it (baked from its bent bones just before the cameras draw) - the game drew it in its animated pose and the reactions were not seen.");
        NpcPunchDisarm=config.Bind("Hands","NpcPunchDisarm",false,"Also a punch to the forearm of the hand holding the gun knocks it out of the enemy's hand (off: only a grab takes it).");
        NpcFistFight=config.Bind("Hands","NpcFistFight",true,"An enemy whose gun was taken switches to a closed-fist guard, planted steps, feints, combos and retreats. Its visible fist must reach the tracked head or torso to hurt; walls, dodging and stun stop the punch.");
        NpcFistFightGameAi=config.Bind("Hands","NpcFistFightGameAi",true,"Unused since 0.1.147 (the enemy's own AI is paused while it fights with its fists; the mod moves it).");
        NpcPunchDamage=config.Bind("Hands","NpcPunchDamagePercent",7f,"How much of your full health one punch of an unarmed enemy takes, percent (1..30); armour takes it first.");
        Enabled = config.Bind("Weapons", "Enabled", true, "Enable experimental render-only tracked pistol, shotgun and AK47. F8 toggles for this session.");
        Pitch = config.Bind("Weapons", "AimPitchDegrees", 0f, "Controller aiming pitch correction, degrees.");
        Yaw = config.Bind("Weapons", "AimYawDegrees", 0f, "Controller aiming yaw correction, degrees.");
        Roll = config.Bind("Weapons", "AimRollDegrees", 0f, "Controller aiming roll correction, degrees.");
        GripRadius = config.Bind("Weapons", "SupportGrabRadiusMeters", 0.18f, "Maximum distance from the invisible support grip point when pressing the left grip.");
    }
}
