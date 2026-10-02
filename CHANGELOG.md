# Changelog

The version is in `Install-XIII-VR.ps1`, `src/Plugin.cs` and `src/XIII.XRBootstrap.csproj`.

## 0.1.209

- **A smaller package.** The sound extractors and the audio collector are gone. Only the log collector (`Collect-XIII-Logs.cmd`) is installed in the game folder. An update removes the old helper files from the game folder and keeps them in the restore point, so `Restore-XIII-VR.cmd` can bring them back.
- The installer's messages are in English only.
- Source comments now describe each change by what it fixes.

## 0.1.208

- **Lift buttons and the switches that turn the alarms off are pressed by touch.** Before, a touch pressed only interactions whose objects were named "button", "switch" or "panel". The lift of `cp_elevator_02` and the power boxes that cut the alarms (`power_box_06_b`) are named `raycast_target`, `trigger` and `Collider`, so a hand right on them pressed nothing. Now what the interaction runs when used decides. A small interaction that does something (an animation, a script, a lift, the alarms off) is pressed by a touch. A touch never does these: taking or carrying (pick-ups, ladders, the hook, ziplines), a door or cabinet leaf that the hand moves itself, raising the alarm (an accidental touch would fail the mission), an interaction that runs nothing, and volumes larger than 1 m. When a touch moves into an interaction and presses nothing, the log says why (`TOUCH BUTTON nothing pressed at …`).

## 0.1.207

- **A steady scope, as if always holding the breath.** A tracked hand always trembles and sways a little, about a fifth of a degree. Without a scope you don't see it, but the SVD's and the crossbow's scopes show only 3–5 degrees, so the picture shook at the slightest move and far targets were very hard to hit. Now, while an eye is at the scope, the aim is steadied: the tremor and the sway are smoothed away (about ten times less shake), while a deliberate turn passes almost at once and never lags more than 2.5 degrees behind the hands. The shot goes where the reticle points. Away from the eye the weapon follows the hands as before, with a quarter-second blend and no jump. `[Weapons] ScopeSteadiness` sets the strength: 0 is off, 1 the default, up to 2 is steadier. The log notes it (`SCOPE … at the eye: the aim steadied …`).

## 0.1.206

- **A held chair (or broom, or gun held by the barrel) strikes again after a touch.** A swing counts only when it is armed. A fist rearms when you open and close your hand. A held thing never lets go of the grip, so after any touch (a table, the floor, a soft touch on an enemy) it rearmed only when pulled back along that touch's stroke. That was often an old line, or no line at all, so swinging along another line never rearmed it again: several tries did nothing, while the other hand struck at once. Now it also rearms when carried 20 cm or more off the touch on another line (not within 60 degrees of going on along the same stroke). Following through along the same stroke still never hits twice. The log notes a fast swing that wasn't counted (`… not rearmed since its last touch`).

## 0.1.205

- **Police, FBI and guards hold fire while you hold a hostage.** The game tells its enemies about a hostage only at the moment it is taken, and only the enemies already there. The bank's guards come in after the hostage is taken, so they never knew and shot. An enemy that arrives later is now told the same way the game tells, and it receives the hostage's later events from then on. Police, FBI and guards don't shoot the holder of a live hostage. Once he fires with the hostage in his hands, the game decides again (they may fire back). Bandits still shoot anyway.
- **Every bullet aimed at you goes into the hostage.** Before, the hostage shielded you only when a bullet's path crossed his body. Now bullets from the side, from above or from behind also go into his body, on the side facing the shooter. The hostage never dies in your hands. He cries out when hit, and if the hits would have killed him, he dies once you let go. Explosions and punches still hit you.
- **A long gun held by the barrel and a chair now land their blows.** A disarmed enemy's raised guard stops the club before his chest and head. In that same moment, another part of the long gun (or one of the chair's 62 parts) often touched a wall, a table or the floor, and that touch took the stroke. An enemy the weapon touches, or is stopped by, now wins over the world. A wall in the way of the same part still blocks the blow.
- The invisible walking blockers named `collider player` (with a space) no longer stop hands and weapons, the same as `collider_player`. They held weapons 0.4–2 m away from anything visible.
- When a held weapon's stroke finds no enemy but one is close by, the log says what it met instead (`VR PUNCH held weapon … met …`).

## 0.1.204

- **The installer goes over what an earlier install left behind.** An uninstall or a Steam reinstall leaves the `BepInEx` folder in the game folder: its settings, logs and the files BepInEx generates. On a game without `doorstop_config.ini`, the installer took these for a broken loader and stopped ("Unconfigured loader files already exist"). Now the loader is installed over them, and your settings are kept. A Doorstop `winhttp.dll` left from BepInEx is replaced and backed up in the restore point. Only a `winhttp.dll` that belongs to another program stops the install, and the message names it.

## 0.1.203 — first public release

- **Story doors open through the game.** Some doors run story events when the game opens them. The bank's hostage door, for example, lets in the two guards. Moving such a door by hand used to run none of these events. Now, when you push or pull a closed story door by about 3 degrees, it swings open through its own game interaction, exactly as Grip+A opens it. Ordinary doors still follow your hand. The log lists what each door's interaction does (`PHYSICAL DOOR bound=… events: …`).
- **Keycards are held to the reader** instead of swiped. The card itself (not the controller) has to touch the reader for 0.15 s. Swiping still works. The hint reads "Hold card to reader" in every language the mod supports.

## 0.1.202

- Brooms, shovels and long guns held by the barrel hit enemies again. Each part of a long weapon now finds its own contact, so the far end touching a wall no longer cancels a blow that hits an enemy.
- Guns with hand-made collision shapes hit with the whole gun.

## 0.1.201

- Long guns held by the barrel damage enemies when they hit the body.
- A third revolver or pistol is picked up even when two of a kind are already on the body.

## 0.1.200

- Chairs hit properly (timed by their far end, not by the hand).
- The pointer on the death screen is drawn above the panel.
- The M4's thin barrel is gripped tighter, and the zipline hook's handle sits nearer the palm.

## 0.1.198 – 0.1.199

- The zipline hook is held by its real rubber handle, like a pistol grip, at its own size.
- A long gun held by the barrel sits deeper in the fist.

## 0.1.191 – 0.1.197

- Holding long guns by the barrel to use them as clubs.
- The zipline hook in the hand and riding the zipline.
- The grappling hook in either hand, with mirrored rope controls.
- Uzi top handle.
- Double-barrel and Uzi manual reloading.
- A silenced pistol in the left hand.
- Two pistols reloaded one-handed against the chest: B/Y drops the magazine, a knock of the grip on the chest puts in a full one.
- Accurate crossbows.
- Hostages held against you with a hand on the neck.
- A selectable Russian voice-over.

## Earlier

Versions before 0.1.191 were private test builds. They covered rendering, tracked hands, weapons, holsters, physical melee, doors, keys, locomotion, menus, the installer and the OpenXR/OpenVR runtimes.
