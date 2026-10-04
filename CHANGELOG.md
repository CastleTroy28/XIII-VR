# Changelog

The version is in `Install-XIII-VR.ps1`, `src/Plugin.cs` and `src/XIII.XRBootstrap.csproj`.

## 0.1.232

- The installer checks the package before it does anything. If the plugin file is missing, it now says why. The usual cause is GitHub's "Source code" archive instead of `XIII-VR-<version>.zip`. Other causes are a run from inside the zip, a half-unpacked folder or an antivirus. The old message blamed an earlier install inside `BepInEx\plugins` even when that wasn't the cause.

## 0.1.231

- The hand on the bazooka's handle (the grip with the trigger) sits up at the trigger. The trigger is measured from the bazooka's model, and the hand moves up until the index fingertip is level with it. It moves at most 4 cm, never down, and never above the handle's top. The front grip is unchanged.
- The README's controls table is complete and up to date.

## 0.1.230

- In the last mission, the opening of the memory in the house can be skipped with the right trigger. The skip now also fast-forwards story timelines that aren't the game's cutscenes.
- Nobody can be taken hostage, punched or grabbed in a memory. The game itself takes no hostages there, but Kim could be taken by pointing at her.

## 0.1.229

- The bazooka stays in the hands after a shot. The game's recoil, reload and empty-pose animations moved the whole bazooka up to 15 cm off the hands. It is now drawn as it was when taken, and only its rocket still moves with the game.
- Every few seconds the log has a `BAZOOKA HOLD` line. It says where each hand is and why one isn't on its grip.

## 0.1.215 – 0.1.228

- **0.1.228** Every bazooka is the same size, because it is sized without its rocket. The thumb wraps round the bazooka's handle.
- **0.1.227** The bazooka's hands are fitted to its grips, each finger closed onto the grip. The rocket lies along the fingers.
- **0.1.226** The bazooka's hands are placed on its measured grips. The thumb stays out of the rocket.
- **0.1.225** The bazooka's hands take the game's own holds instead of mirrored ones.
- **0.1.224** "Smarter enemies" is gone from VR SETTINGS. They stay on, and the config still has `[VR] SmarterEnemies`.
- **0.1.223** Bolts, slides and charging handles are worked with the grip. The M60's box goes back in easily, and its cover can be pressed shut by hand. The bazooka's real handle is found.
- **0.1.222** Grab and throw in one motion. Bottles no longer get stuck in the hand.
- **0.1.221** Ammo comes from the belt pouch with the grip. While the gun needs rounds, the pouch comes before the holster.
- **0.1.220** Picking a lock takes the game's own time, with its HUD timer and sound.
- **0.1.219** The hands close on the bazooka's grips.
- **0.1.218** Allies, like Major Jones, are left alone: no hostage, punches or grabs.
- **0.1.217** The rocket is held in the fist and shows in the tube after a hand reload. The marker ball no longer shows on the zipline.
- **0.1.216** A hard punch in the back knocks an enemy out. A hostage is taken with a still grip.
- **0.1.215** The pause menu has a VR CONTROLS page. Keys come out on the stick click, and hints show controller icons. The bazooka is reloaded by hand and has an aim dot.

## 0.1.204 – 0.1.214

- **0.1.214** A landing ring for throws. The next knife comes at once. Right A closes the weapon wheel tutorial.
- **0.1.213** A hostage is taken by pointing a free hand at him from behind.
- **0.1.210 – 0.1.212** The installer runs on a default Windows. The start screen and the weapon wheel tutorial answer the controllers. Door and breakable prompts show only the game's icon.
- **0.1.209** A smaller package: only the log collector goes into the game folder.
- **0.1.208** Lift buttons and alarm switches are pressed by touch.
- **0.1.207** The scope is steady at the eye (`[Weapons] ScopeSteadiness`).
- **0.1.206** Held chairs, brooms and clubs strike again after a touch.
- **0.1.205** Police, FBI and guards hold fire while you hold a hostage, and bullets aimed at you hit the hostage.
- **0.1.204** The installer installs over files left by an earlier install.

## 0.1.203 — first public release

- Story doors open through the game when moved by hand. Keycards are held to the reader.

## 0.1.191 – 0.1.202

- Long guns held by the barrel as clubs, the zipline, the grappling hook in either hand, double-barrel and Uzi manual reloading, two pistols reloaded against the chest, hostages held by the neck, a selectable Russian voice-over, and many melee fixes.

## Earlier

Versions before 0.1.191 were private test builds. They covered rendering, tracked hands, weapons, holsters, melee, doors, keys, locomotion, menus, the installer and the OpenXR/OpenVR runtimes.
