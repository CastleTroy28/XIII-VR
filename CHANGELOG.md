# Changelog

The version is in `Install-XIII-VR.ps1`, `src/Plugin.cs` and `src/XIII.XRBootstrap.csproj`.

## 0.1.231

- **The hand on the bazooka's handle sits up at the trigger.** The hand holding the handle (the grip with the trigger) sat 2 to 3 cm too low on it: its fingers wrapped the lower half and its index finger was below the trigger. The three lower fingers were centred on the middle of the handle's bar, and that bar leaves out the handle's top, where it joins the tube. Now the trigger itself is measured (its own bone) and the hand moves up the handle until the tip of its index finger is level with the trigger. It moves up by at most 4 cm, never down, and the web of the hand (the index knuckle) never goes above the handle's own top. Without a trigger, the hand moves up until the web reaches that top. The left hand on the handle does the same. The front grip, which players found right, is unchanged. The log gives the move (`BAZOOKA handle ... moved ... cm up it`) and the measured trigger and handle top (`BAZOOKA grips: ...`).
- The README's controls are up to date. They were missing holsters, throwing, the grenade pin, punching, taking an enemy's gun, dragging bodies, ladders, swimming, teleport, medkits, the grappling hook, the zipline, the next weapon, tasks and two keys (Shift+F5, F8), and the hostage row is now grouped with fighting.
- The memory fixes of 0.1.230 and the bazooka fix of 0.1.229 are included.

## 0.1.230

- **The memory's opening in the last mission can be skipped.** Entering the house starts a memory, and its opening (the story camera `Camera_blink_cs&seq_seq_01_01`) could not be skipped. Its Timeline is not one of the game's cutscenes, and the skip only looked for those, so the trigger did nothing. The skip now also fast-forwards the Timeline behind the story camera being shown. If there isn't one, it takes the story Timeline playing in that scene with the most time left, ignoring looping, hand-run and level-long ones. That fast-forward ends as soon as the story hands control back. A Timeline that holds its last frame now ends the fast-forward there, instead of after 20 seconds. If nothing can be skipped, the log says so (`STORY skip: no story timeline playing ...`).
- **Nobody is taken hostage in a memory.** In the memory, Kim (the ally in the next room) could be taken hostage by pointing at her. The game takes no hostage at all in a memory, but the mod's own hostage rule (from behind, unaware) didn't check for that, and Kim isn't one of the game's Ally characters. In a memory everyone is now left alone, as allies are: nobody is taken hostage, punched, knocked out, grabbed, disarmed or made to flinch. The log says why (`ALLY ...: in a memory (flashback)`).
- The bazooka changes of 0.1.229 are included.

## 0.1.229

- **The bazooka stays in the hands after a shot.** The hands are fitted to the bazooka's grips as they were measured when it was taken, but the bazooka itself was drawn as the game animates it. After a shot (its recoil, its reload, its lowered empty pose), the game moved the whole bazooka up to 15 cm from that place, so the hands held their grips in the air below and behind it, with their fingers closed round nothing. The bazooka is now drawn as it was when taken, where the hands hold it. Only its rocket still moves with the game: hidden when it fires and back in with its reload, or drawn by the mod with the hand reload. Its copies on the floor and on the body are drawn the same way.
- The log has a hold report every few seconds while a bazooka is in a hand (`BAZOOKA HOLD ...`). It gives where each hand is drawn and how far it is from its hold, why a hand isn't on its grip (with what keeps the gun out of the hands' control, if anything), and how far the game's animation would have moved the bazooka. Earlier versions wrote each reason only once, so a hand that came off its grip later said nothing.

## 0.1.228

- **Every bazooka the same size.** A bazooka was sized by its length, and its length counted the rocket in its tube. A second bazooka picked up, loaded, had its rocket held further back by the game, so it came out a third bigger (its handle 8 cm by 4.8 instead of 6 by 3.6), and the hands didn't close round its handles. Every bazooka is now sized without its rocket, at the size it has had in nearly every game, so its handles are always where the hands close. The log says so (`WEAPON VISUAL bazooka fitted without its rocket`). Its shot now starts at the mouth of the tube.
- **The thumb wraps round the bazooka's handle.** The thumb wrapped round the front grip and the rocket, but on the handle with the trigger it was left open along the side of the gun, above the handle. It now wraps round the handle too, clear of it and of the index finger on the trigger.
- The hold on a bazooka grip is also worked out when the game draws the hands before the gun in a frame, so a hand is not left open beside its grip for that frame. If the hand holding the bazooka is not drawn on its handle, the log says why (`BAZOOKA the hand holding it is not drawn on its handle: ...`), and each bazooka's holds are written once for each bazooka drawn.

## 0.1.227

- **The bazooka's hands close round its grips.** Earlier versions put each hand where one of the game's own hands had once been, or mirrored the other hand's hold. Drawn there, the closed fingers were 2 to 10 cm off the grips: the hand on the handle sat above it, up by the tube and beside it, and the hand on the front grip held it with the heel of the palm. Now each hand is fitted to the grip it holds:
  - The middle of the curl of its closed fingers is on the grip's line, at the grip's middle. That line runs through each finger's own curl, so the shorter little finger isn't pushed into the grip and the long middle finger isn't held off it.
  - The fingers close as far as the grip is thick (the bazooka's 3.5 to 3.6 cm grips at the hand's size: a little over half a fist), each one until it lies on the grip.
  - On the handle, the three lower fingers go round it and the index finger is on the trigger above them. On the front grip, all four go round it and the thumb wraps round it without going into it.
  - The hand is turned as the game holds the handle. The left hand is the right hand's hold mirrored, on either grip.
  - The fit comes from the player's own hand skeleton as the game draws it. A test checks it with a logged hand: every finger joint 2 to 11 mm off the grip's surface (the skin on it), the thumb clear of it, the index finger at the trigger.
- **The rocket in the fingers, not deep in the palm.** In 0.1.226 the fingers opened round the rocket's motor tube, but the tube stayed deep in the palm, where the zipline hook's handle lies, so the opened fingers stood off it. Now the tube lies along the fingers, through the middle of their curl. Each finger closes onto it, and the thumb wraps round it without going into it.
- The log describes each fit, with each finger's closure and the thumb's (`BAZOOKA handle (with the trigger) held by the right hand round it: ...`, `BAZOOKA rocket in the left hand: ...`). If the hand holding the bazooka's front grip with two hands is not drawn there, the log says why (`BAZOOKA two hands, but the support hand is not drawn on the front grip: ...`).

## 0.1.226

- **The bazooka's handle hands back in place.** 0.1.225 took every hand's place from the game's own hands. In one game all of them lay 9 cm off (down 7, forward 5, left 4 cm), because the bazooka's fit was taken while the game was still drawing it, so the hands on the handle slid forward and down. Now every hand is placed on the bazooka's measured grips, at the place players saw as right. The game's own hands are no longer read for places.
  - Right hand on the handle: where it sat in 0.1.215 to 0.1.224.
  - Either hand on the front grip: where it sat in 0.1.225 (unchanged).
  - Left hand on the handle (bazooka in the left hand): as much higher and further back than on the front grip as the right hand is.
  - Each hand keeps the turn the game gives it there, and the fingers stay closed as before.
- **The thumb stays out of the rocket.** The rocket's motor tube is thicker than the zipline hook's handle the hold was made for, so the thumb went into it. The fingers and the thumb now open round the tube, and the tube sits further out from the palm by as much. The log gives the tube's thickness and the closure (`BAZOOKA rocket in the ... hand: its tube ... cm thick`).

## 0.1.225

- **The bazooka's hands on its grips.** Every hand is now either where the game itself puts it, or that hand's own hold moved from one upright grip to the other. A hold mirrored from the other hand never landed on a grip. The game's left hand on the front grip has its wrist 4 cm to the right of the grip, while the mirrored right hand had it 6 cm to the left. From 0.1.218 on, the mirrored hands hung beside the grips.
  - Bazooka in the right hand: the right hand holds the handle as the game holds it (unchanged), and the left hand holds the front grip where the game's own left hand holds it.
  - Bazooka in the left hand: the left hand takes the handle with the game's own left-hand hold of the front grip. The right hand takes the front grip with the game's own right-hand hold of the handle.
  - The fingers are still closed on both grips, with the index finger on the trigger. The game's own hands there are open and flat.
- The log names each hold (`BAZOOKA front grip held by the left hand where the game holds it`, `BAZOOKA handle held by the left hand as the game's left hand holds the front grip`).

## 0.1.224

- **No "Smarter enemies" in VR SETTINGS.** The row is gone from the VR settings page. Enemies behave exactly as before: the smarter enemies stay on, as they were by default. The setting is still in the config file (`[VR] SmarterEnemies`) for anyone who wants to change it there.

## 0.1.223

- **The grip works the bolts too.** With manual reloading, the bolt, slide or charging handle of every gun (pistol, AK, M16, M60) is now pulled back with the other hand's grip, not its trigger. The shotgun's pump was already the grip. Magazines and rounds were already taken with the grip in 0.1.221, so the trigger now only fires. The VR CONTROLS page and the settings' description say so, in all 8 languages. The grenade's pin is still the other hand's trigger, because the other hand's grip on a grenade takes it into that hand.
- **The M60's box goes back in easily.** The box went in only when its front end was pushed along a feed line to a point behind it, which took a long time to find. Now it goes in as soon as you bring it to its place on the gun, the place it was taken from, however it is turned. The box you have just taken out doesn't go straight back in until it has been moved away.
- **Press the M60's cover shut.** The top cover that B opens can now be closed by pressing it down with either hand, as if it were solid. A hand that comes onto the open cover from above pushes it down as far as the hand goes, and it stays there. Pressed nearly flat, it snaps shut. The grip at its edge still works too.
- **The bazooka's hands, again.** The bazooka has three grips: the front grip, the handle with the trigger, and a shoulder rest behind it. 0.1.219 took the shoulder rest for the handle, so it found no handle. It now finds the real handle (the rig's mid grip), and the hand on the front grip is the game's own hold of that handle moved onto the front grip and mirrored. The game's right hand sits exactly on its handle, and both grips are upright handles, so it no longer hangs in the air beside the grip. With the bazooka in the left hand, it is mirrored across the handle's own middle.
- **Fingers closed on the bazooka's handle.** The hand on the handle has its fingers closed round it, with the index finger on the trigger. It used to take the game's own finger pose, and the game's bazooka hand is open and flat at times, so the hand looked flat in some games and closed in others.
- The log names the bazooka's grips (`BAZOOKA grips: the handle with the trigger ...`) and the front-grip hold (`BAZOOKA front grip held by the ... hand as the game's right hand holds the handle`). It also notes a cover pressed shut (`M60 COVER pressed shut by hand`).

## 0.1.222

- **Grab and throw in one motion.** A bottle, an ashtray or another throwable thing can now be thrown straight away: close the grip on it, swing and let go. The game takes a moment to put a thing you grab into your hand. A grip let go during that moment used to count for nothing: the bottle stayed stuck in the hand, and it took a second grip press, held through a swing, to throw it. Now the swing you let go with is kept, and the bottle flies with it as soon as the game has it in your hand. This works with "Weapon in hand: hold grip". Letting go without a swing still drops the thing.
- **No more bottle stuck in the hand.** While the game was putting the thing in your hand, it could briefly show nothing there, and the mod then forgot which grip had taken it. Letting go didn't drop it, and it stayed in the hand until the next press. The press that took it now still counts, so letting go drops it as it should. The log notes it (`GRIP prop ... left the hand without being let go`).

## 0.1.221

- **Ammo is taken with the grip.** With manual reloading, the other hand's grip (not its trigger) takes a magazine, rounds, shells or the bazooka's rocket from the belt pouch and holds it. Let go of the grip and it drops. With the gun in the right hand, that's the left grip; with the gun in the left hand, the right grip. The trigger still works the bolt, and the shotgun's pump is still the grip.
- **The belt pouch comes before the holster.** The pouch sits where the left belt holster is. While the gun in your hand wants rounds (its magazine out, a shotgun or revolver not full, the double-barrel open with an empty chamber, the bazooka empty), the grip at that spot takes rounds. Otherwise it draws the holstered weapon as before. So drop the magazine with B first, then reach for the pouch. A hand holding ammo doesn't punch.
- **The other hand closes on the bazooka's front grip.** The support hand on the bazooka's front grip used to hang in the air. Its place is now worked out in its own hand's frame, the way the rocket is held. The bracket that joins the grip to the tube is no longer counted as part of the grip. The hand on the handle keeps the game's own hold, with the finger on the trigger.
- **The bazooka in the left hand.** Both hands used to hang off the grips. The bazooka is now mirrored across its tube's line, not across a middle measured round the hand. The right hand on the front grip is placed directly, not mirrored from a left hand.
- The VR CONTROLS page has rows for the magazine and rounds (the other grip at the belt pouch) and for the bolt and pump. The bazooka row says grip.

## 0.1.220

- **Picking a lock takes the game's time.** Turning the lockpick in the lock used to open it at once, like a key. Now the turn starts the game's own lockpicking. Its timer counts down on the game's lockpicking HUD, the lockpick's own sound plays, and guards nearby can hear it, as in the game. Keep the pick in the lock until the timer runs out and the lock opens. Pull it out and the picking stops; turn it again to start over. The time is the game's own for that lockpick, and the game's instant lockpicking still opens at once.
- **The lockpick hint changes once the pick is out.** At a lockpick door the hint kept saying to click the stick after the pick was out. Now it says "Turn lockpick", like the key, and "Hold lockpick in lock" while the timer runs, in all 8 languages.
- The VR CONTROLS page has a "Pick a lock" row.

## 0.1.219

- **Closed hands on the bazooka.** The game holds the bazooka with open, flat hands, and the mod copied that pose. Now the hand on the handle grips it like a pistol grip, with the fingers closed round it, sized to the grip's thickness. The other hand does the same on the front grip. The mod finds both grips in the bazooka's own model.
- **No gap in the left hand.** With the bazooka in the left hand, the hand is the right hand's hold mirrored across the handle's own middle. The middle used to be measured round the flat hand, and the bazooka hung away from the left palm.
- **The rocket is held lower.** A rocket from the pouch is held near its tail, a tenth of its length up instead of a fifth.

## 0.1.218

- **Allies are left alone.** The mod no longer touches the player's allies, such as Major Jones in the canyon. You can't take them hostage, and punches, blows with a held weapon or prop, thrown weapons, gun grabs and body grabs do nothing to them. An ally is one of the game's Ally characters, or someone the game lets you neither hurt nor take hostage. In 0.1.216, pointing at Jones took her hostage; the game then failed to put her down and the level broke. The log names each ally the mod left alone (`ALLY ...`).
- **The VR hostage rule follows the game's own.** Taking someone from behind in VR, when the game's own check fails only because of the camera, now also needs the game's permission for that character: one you may hurt, or one marked to be taken even so.

## 0.1.217

- **No ball on the zipline.** While you ride a zipline, the hand holding the hook isn't drawn. A blue ball (the controller marker) used to show in its place; it's gone now.
- **The rocket sits in the fist.** A rocket taken from the pouch is held in the closed hand, round its motor tube, the way the zipline hook's handle is held: the warhead above the thumb, the tail below the little finger. It used to stick out of an open hand. Point the warhead forward (or hold the bazooka up) and put the tail into the tube.
- **The glow marks the tube's real mouth.** The glow and the point where the rocket goes in are now at the front end of the tube, measured from the bazooka's own model. They used to sit at the aim point, well ahead of the tube.
- **A rocket put in by hand stays in the tube.** After a hand reload, the rocket used to vanish, though the bazooka was loaded and fired. It now shows in the tube, where a loaded bazooka has it, until you fire. With manual reloading on, the mod draws the rocket in the tube: there while loaded, gone while empty.

## 0.1.216

- **Knock an enemy out from behind.** A hard punch of either fist into an enemy's back knocks him out at once, the game's own way: he falls and counts as taken down for the mission and the tutorials. It needs a real swing (3 m/s or more), from behind (within 60° of his back) and closer than 2 m. A light touch or a punch from the front stays an ordinary punch. Enemies the game never lets you take down, such as bosses, can't be knocked out this way. If a punch in the back was too light, the log says how fast it was.
- **The bank electrician no longer runs off.** He's the first man in the bank tutorial, the one you can knock out or take hostage. A punch used to only alarm him, and he ran upstairs, so the tutorial couldn't go on. A hard punch in his back now knocks him out.
- **A hostage needs a still grip.** Pointing at an enemy, hold the grip still for a moment (0.3 s) to take him. If the hand swings away at once, it's a punch, not a grab.
- The takedown hint reads "Fist in the back, swing hard", and the VR CONTROLS page lists it.

## 0.1.215

- **VR controls page.** The pause menu has a new **VR CONTROLS** button next to VR SETTINGS. It opens a page that lists every action and the buttons or gesture for it, in the game's language, for the hand you set in the VR settings. B or its Close line closes it.
- **Keys on the stick click.** At a key, keycard or lockpick lock, a right stick click (R3) takes the item out; for a left-hander it's the left stick click (L3). Grip + A still works too. While a lock prompt is shown, that click doesn't also fire the secondary fire, zoom a scope or sprint.
- **Controller icons in hints.** Hints show a controller with the button to press lit up in yellow: the grip, the trigger, A/B/X/Y or the stick. On a pick-up it replaces the game's hand icon; on other hints it sits right of the text. Doors and breakable things keep the game's own icon.
- **Pick-ups name both grips.** The pick-up hint now says "Left/Right Grip", because either hand takes things.
- **Bazooka reloaded by hand.** With manual reloading on, an empty bazooka no longer reloads the game's way. The hand not holding its handle takes a rocket from the belt pouch with its trigger. Put the rocket's tail into the front of the tube and it goes in with the game's own reload sound. Let go of the trigger anywhere else and the rocket goes back in the pouch. While the bazooka is empty, a glow marks the pouch, then the tube's mouth.
- **Bazooka aim dot.** While the bazooka is loaded, a red dot marks where the rocket will hit. The sight on its model doesn't match where the rocket flies.

## 0.1.214

- **Right A closes the weapon wheel tutorial.** In the first level the game opens the weapon wheel with a hint and locks the controls until you close it. The hint now names right A instead of left grip + X, and right A closes it. Left grip + X still works. Right A also works when the game hasn't opened its wheel yet.
- **Landing mark for throws.** While a knife, a grenade with its pin out, a bottle or an ashtray is ready to throw, a glowing ring shows where it would land, in either hand. During a swing it shows where letting go right now would throw it; otherwise it shows a normal throw in the direction the controller points. After the throw the ring stays where the thing will land until it gets there. The mod measures the game's knife flight after a throw, so later rings follow it. To switch the ring off, set `[VR] ThrowLandingMarker = false` in the settings file.
- **The next knife comes at once.** After a throw, the hand that threw can take the next knife from its place on the chest as soon as the thrown one has left. It no longer waits for the hand to empty and the knife to be drawn again. If you throw again before the game has finished its last throw, the throw waits for the game's knife, for up to 1.2 s.
- **Wider grab on the chest.** The knife and grenade places on the chest take from 17 cm away instead of 12 cm.

## 0.1.213

- **Take a hostage by pointing at him.** You no longer have to touch an enemy to take him hostage. Point a free hand's controller at him from behind: the game's hostage icon shows with "Hold left Grip" or "Hold right Grip" for that hand. That hand's grip takes him, and he stays in that hand while you hold it. Grabbing him with the left hand still works too.
- **The start screen takes the first press.** In 0.1.210 Enter came only half a second after the last button press, so pressing on kept the screen waiting. A controller press now types Enter at once, and works without the game's window in front.
- **The menu chord sends Escape again for the weapon wheel tutorial.** 0.1.210 closed the wheel the game opened by itself instead of sending Escape. Escape, which the game itself answers, comes first again. The wheel is closed directly only when Escape can't be typed.

## 0.1.212

- **Door and breakable prompts show only the game's icon.** Opening a door, cabinet or hatch and breaking a grate, panel or glass now show just the game's icon, with no text and no button. You do these with your hands; right grip + A still works. Prompts that take out a key, keycard or lockpick keep their text and icon as before. This replaces the hand hints of 0.1.211.

## 0.1.211

- **Doors, grates and glass tell you to use your hands.** The prompt at a door showed the button chord (right grip + A), so testers pressed it and never tried their hands. A door or cabinet you can move by hand now says "Push or pull it with your hand", and a grate or glass the game breaks says "Hit it with your hand or a weapon", in the game's language. The chord still works, it's just no longer shown there. Locked doors (key, keycard, lockpick) keep their chord, since it takes the key out.

## 0.1.210

- **The installer runs on a default Windows.** Windows blocks PowerShell scripts by default, so `Install-XIII-VR.cmd` stopped before doing anything unless the script policy had been changed. The launchers now allow the installer to run for that one start; no Windows setting is changed.
- **The start screen answers the controllers.** "Press any button" took only a typed Enter key, and a key reaches the game only when its window is in front, which is often not the case with Virtual Desktop. A controller press is now the game's own "any button", and works without the window in front. Enter is still typed if the screen keeps waiting.
- **The weapon wheel tutorial no longer locks the game.** In the first level the game opens its weapon wheel itself and locks the controls until the wheel is closed. That wheel didn't answer the hands, so the game was stuck. Now holding right A takes it over: choose with the left stick and let go of A to confirm. The menu chord (left grip + X) also closes it.
- In VR the menu pointer, the weapon wheel and the menu buttons work without the game's window in front.

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
