# XIII VR

[![tests](https://github.com/CastleTroy28/XIII-VR/actions/workflows/tests.yml/badge.svg)](https://github.com/CastleTroy28/XIII-VR/actions/workflows/tests.yml)

A VR mod for **XIII** (the 2020 remake, Windows). You play the whole game in a PC VR headset with tracked hands: aim and reload weapons by hand, punch, throw, take hostages, open doors and use keys, keycards and the grappling hook with your hands.

It is a [BepInEx 6](https://github.com/BepInEx/BepInEx) plugin. A one-click installer sets everything up and can undo it.

> **Status:** in active development. It is played and tested on a Pimax Crystal with SteamVR and OpenXR. Bug reports with logs are very welcome (see [Reporting a problem](#reporting-a-problem)).

## Features

- **Headset rendering on any PC VR runtime.** The mod uses OpenXR and runs on whatever the Windows default OpenXR runtime is: SteamVR, Pimax Play, Meta/Oculus, Virtual Desktop or WMR. If OpenXR can't start, it falls back to OpenVR/SteamVR.
- XIII's own first-person arms follow the controllers.
- **Weapons in your hands:**
  - Aim with the controller, and hold long guns with both hands for a steadier aim.
  - Working scopes on the crossbow and the sniper rifle.
  - Manual reloading for magazines, shells, revolvers, the double-barrel and bolts. You can switch to automatic reloading in the settings.
  - Weapon holsters on your body.
  - A weapon wheel for everything else.
- **Physical melee:**
  - Punch with your hands clenched into fists, or pick up a shovel or a broom, whatever you like.
- **Throwing.** Knives, grenades and props fly where your hand throws them (it takes some practice).
- **A world you can touch:**
  - Doors and cabinets move with your hand.
  - Turn a key or a lockpick in the lock, and hold a keycard to the reader.
  - Press buttons, lift controls and switches by touch.
  - Medkits sit on your forearm.
- **The grappling hook** is adapted to the controllers.
- **Locomotion:**
  - Smooth, head-relative movement.
  - Smooth or snap turning, with optional teleport.
- **Menus and story:**
  - Menus, the HUD, cutscenes and the death screen all work in VR, and you point at menus with a laser pointer.
  - A VR settings page is added to the pause menu.
  - Cutscenes are fast-forwarded with the trigger.
- **More:**
  - Left-handed mode (not tested yet, but it should work) and controller haptics.
  - The mod's own texts follow the game's language: English, Russian, German, French, Spanish, Italian, Polish and Portuguese.

## Requirements

- XIII (2020 remake) for Windows, 64-bit. The Steam version is the one tested.
- A PC VR headset with an OpenXR runtime (SteamVR, Pimax Play, Meta/Oculus, Virtual Desktop, WMR…) or SteamVR.
- An internet connection on the first install. The installer downloads the tested BepInEx build (6.0.0-be.788, Windows IL2CPP x64) and checks its hash. To skip the download, pass a local copy with `-BepInExZip <path>`.
- No administrator rights are needed.

## Installing

1. Download the latest `XIII-VR-<version>.zip` from [Releases](https://github.com/CastleTroy28/XIII-VR/releases).
2. Close the game.
3. Unpack the whole archive into its own folder, outside the game folder (for example Downloads).
4. Run `Install-XIII-VR.cmd`. If it doesn't find the game, it asks you to pick the XIII folder.
5. Start XIII with your headset ready. The very first start takes a few minutes longer while BepInEx prepares itself.

The installer never overwrites a file that has changed since it was staged. It keeps a restore point in `XIII-VR-backups` inside the game folder, and your existing settings are kept.

- **Update:** run the new version's `Install-XIII-VR.cmd`.
- **Uninstall:** run `Restore-XIII-VR.cmd` from the package. It restores the game folder as it was before the install.
- **Settings:** use **VR settings** in the pause menu, or edit `BepInEx\config\xiii.vr.xrbootstrap.cfg`. For example, `[VR] Runtime` is `Auto`, `OpenXR`, `OpenVR`, `SteamVR` or `OpenComposite`, and `[Weapons] ScopeSteadiness` (0 to 2) sets how steady a scope is at your eye.
- **Install check:** the log `BepInEx\LogOutput.log` should contain `Loading [XIII XR Bootstrap <version>]`.

## Controls

These are the right-handed controls. Left-handed mode (VR settings) mirrors them.

| Action | Control |
|---|---|
| Move | left stick (in the direction you look) |
| Turn | right stick left/right |
| Jump / crouch | right stick up / down (or crouch for real) |
| Pick up, hold, grab a door | grip near it |
| Take a hostage, carry a body | left grip on them (a hostage from behind, before they notice you) |
| Fire | trigger of the hand holding the gun |
| Reload | right B (with manual reloading: drop the magazine, then reload by hand) |
| Hold a long gun with both hands | other grip on the barrel |
| Use a long gun as a club | grip the muzzle with the other hand, then let go of the handle |
| Interact (doors, keys, cards, lockpicks) | right grip + A |
| Weapon and item wheel | hold right A, select with the left stick |
| Pause menu | left grip + X |
| Menu pointer | the hand whose trigger you pulled last |
| Rope | left stick climbs, right stick swings, L3 lets go |
| Skip / fast-forward a cutscene | right trigger |

Keyboard:

| Key | Action |
|---|---|
| F11 | recenter |
| F3 | performance overlay on/off |
| F6, F12 | screenshot |

## Reporting a problem

The installer puts `Collect-XIII-Logs.cmd` in the game folder. Run it after the problem happens. It gathers the BepInEx log, the mod's settings and its timing files into one archive.

[Open an issue](https://github.com/CastleTroy28/XIII-VR/issues) and include:

- the log archive;
- your headset and VR runtime;
- what you did and what you expected;
- a screenshot or short video if it helps.

## Building from source

The plugin compiles against the game's own interop assemblies. BepInEx generates them in `<XIII>\BepInEx\interop` on the first start of the game with BepInEx installed, so install the mod (or BepInEx be.788) once first.

**Plugin** (.NET SDK 6 or newer):

```
dotnet build src/XIII.XRBootstrap.csproj -c Release -p:BepInExRoot="D:\SteamLibrary\steamapps\common\XIII\BepInEx"
```

The result is `src/bin/Release/net6.0/XIII.XRBootstrap.dll`. To try it, copy it to `<XIII>\BepInEx\plugins`.

**Native helpers:** `xiii_openxr.dll` (the OpenXR input/tracking helper) and `openvr_api.dll` (the OpenVR runtime switch) are small C DLLs. They build with clang and lld-link, without the Windows SDK, on Linux, macOS, WSL or Windows with LLVM:

```
sh buildtools/openxr-helper/build.sh
sh buildtools/openvr-shim/build.sh
```

**Player package** (the zip attached to a release):

```
python buildtools/package.py --plugin src/bin/Release/net6.0/XIII.XRBootstrap.dll
```

This builds the native helpers, then writes `dist/XIII-VR-<version>.zip` with `SHA256.txt` inside. The version comes from `Install-XIII-VR.ps1` and must match `src/Plugin.cs` and `src/XIII.XRBootstrap.csproj`.

**Tests:** 148 test programs check the mod's logic with stand-ins for Unity and the game, so they need no game files:

```
python tests/run_tests.py
pwsh tests/Installer.Tests.ps1
```

Two more checks inspect the built plugin against the game's interop assemblies:

```
python tests/run_tests.py VerifyCompiledDll InteropApiTests --plugin src/bin/Release/net6.0/XIII.XRBootstrap.dll --bepinex "<XIII>\BepInEx"
```

## Project layout

| Path | What |
|---|---|
| `src/` | the plugin (C#, BepInEx 6 IL2CPP) |
| `src/vendor/` | Valve's C# OpenVR bindings |
| `tests/` | test programs, `run_tests.py`, `test_sets.json`, installer checks |
| `buildtools/openxr-helper/` | `xiii_openxr.dll` source |
| `buildtools/openvr-shim/` | `openvr_api.dll` runtime switch source |
| `buildtools/package.py` | builds the player package |
| `Install-XIII-VR.ps1`, `*.cmd` | installer and restore |
| `GameFolder/` | files the installer copies into the game folder (Unity's OpenXR plugin, Valve's OpenVR plugin, the log collector) |
| `OpenXR/` | licenses and notes for the OpenXR files |
| `Fresh-config/` | default settings |

## License and credits

- The mod is released under the **MIT License** ([LICENSE](LICENSE)).
- Third-party components keep their own licenses: Valve's OpenVR plugin and bindings (BSD-3-Clause), Unity's OpenXR plugin, the Khronos OpenXR loader and headers, and a CC BY 4.0 sound. See [THIRD-PARTY.txt](THIRD-PARTY.txt).
- The mod's own sounds were made by the author. Some were generated with [ElevenLabs](https://elevenlabs.io).

This is a fan-made mod. It is not affiliated with or endorsed by the publisher or developers of XIII. XIII and its assets belong to their respective owners. No game files are included: you need your own copy of the game.

## Support

If you enjoy the mod, you can support me on Ko-fi:

[![Support me on Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20me-FF5E5B?logo=ko-fi&logoColor=white)](https://ko-fi.com/castletroy)

https://ko-fi.com/castletroy
