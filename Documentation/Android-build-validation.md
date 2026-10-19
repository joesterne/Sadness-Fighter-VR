# Android build validation

Checked on October 2, 2026. Rebuilt on October 5, then updated with the box fix and low-poly pass (see below). Codex's development build was reviewed first, the issues below were fixed, and a release build was made and inspected.

## October 2 result

The shareable release APK builds with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 69 MB (the earlier development APK was 109 MB) |
| Build | Release, IL2CPP, arm64-v8a only, not debuggable |
| Signing | APK Signature Scheme v2 with Unity's debug key. It can be sideloaded with `adb install`. |
| SDK | minSdk 32, targetSdk 36, Horizon OS SDK 60–207 |
| Permissions | Hand tracking and internet only |
| Manifest | VR launcher category, head tracking required, hand tracking optional, focus-aware, Vulkan required. Supported devices are Quest 2, Pro, 3 and 3S. |
| Warnings | 16, none of them from game code: 9 deprecated-API warnings in the embedded Unity OpenXR Meta package, plus Unity and IL2CPP notes |

The only error in the console comes from Meta's AgentBridge editor server. It could not open port 48735 because another process already holds it. That is editor tooling and does not affect the build.

The APK was searched for the PC's LAN address, the DevAgent access token, Inference Engine shaders and the XR Operator screen-capture service. None of them are present.

## Fixed

1. **Unused Inference Engine package.** It took up 75.7 MB of the old APK's assets. It has been removed from `Packages/manifest.json`, and the embedded copy was moved to `_RemovedPackages/`. The project compiles without it. Meta's AI Building Blocks reference it only behind `UNITY_INFERENCE_INSTALLED`.
2. **Test tooling in the shareable build.** There is now a menu command, **After Hours → Build shareable Quest APK (release)**. It leaves out Meta's XR Operator layer, its screen-capture service and the Immersive Debugger. `ReleaseBuildPrivacy.cs` clears the LAN address and access token that the DevAgent injects into every build. The development build is still available for simulator and agent testing.
3. **Unused mixed-reality permissions.** Scene, anchor, passthrough and headset-camera support are off in the Meta project config. The unused Unity OpenXR Meta features (Session, Anchors, Planes, Meshing, Bounding Boxes, Raycasts, Occlusion, Camera, Colocation Discovery and Boundary Visibility) are disabled for Android. Those entries were also removed from the manifest template. `ConfigureQuest()` enforces all of this.
4. **Quest render settings.** `Mobile_RPAsset`, the URP asset Quest uses, now has render scale 1.0 and a 28 m shadow distance, as the builder intended. Before, it was 0.8 and 50 m. The builder now applies these values to every quality level's pipeline asset, not just the editor's active one.
5. **B/Y opened the Immersive Debugger.** This one was found during the fixes. Meta's Immersive Debugger was enabled in every build, and its show/hide button was B/Y, which is also the game's return-to-lobby button. It now runs only in development builds, and its toggle is on the left controller's menu button.

## Development build

`Builds/Quest/AfterHours.apk` was rebuilt with these fixes. It is 84 MB, built with zero errors, and debuggable. It has the same permissions as the release build plus the XR Operator's screen-capture service, which is expected in a development build. The Inference Engine shaders are gone. Keep it on your own headset.

The first rebuild came out at 109 MB, about 25 MB of which was dead space. Gradle had updated its previous APK in place and left the removed entries behind. Both build commands now delete Gradle's packaged output first, so every APK is written fresh.

## October 5 rebuild

Packages added after October 2 broke the Android build. Meta's Voice SDK, which came in with `com.meta.xr.sdk.all`, crashed during its build step with "An item with the same key has already been added. Key: Assembly-CSharp". The game doesn't use voice input, Meta XR Audio or in-app purchases, so `com.meta.xr.sdk.all`, `com.meta.xr.sdk.audio` and `com.unity.purchasing` were removed from `Packages/manifest.json`. The Meta XR Core, Interaction, Haptics, Platform and MR Utility Kit packages are still listed individually. The audio spatializer and ambisonic decoder in `ProjectSettings/AudioManager.asset` were reset to none because they pointed at Meta XR Audio.

The project compiles with zero errors without them. The release build succeeded with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 67 MB |
| Manifest | Unchanged from October 2: hand tracking and internet permissions only, minSdk 32, targetSdk 36, Horizon OS SDK 60–207 |
| Checks | No LAN address, Voice SDK, Wit.ai or Google Play Billing code in the APK. The XR Operator layer is excluded. |

Someone also added Unity's **Meta Quest** build profile. A custom build profile carries its own player, quality and XR settings, so both build commands now switch to the Android platform profile before building.

### Installing on a headset

The new **After Hours → Install release APK on connected Quest** command installs the APK with Unity's bundled `adb` and starts the game. It was tested on a Quest 3 connected over USB. Unity reported "Success" and the game's activity started.

## October 5: box fix and low-poly pass

### Boxes in Heavy things could not be picked up

The scene builder merges static scenery into a few large meshes. Its check for objects the player can pick up didn't see objects in inactive rooms, and every room except the lobby is inactive while the scene is built. So the visible boxes were frozen, merged copies. The real boxes were invisible: grabbing one moved it, and only its label moved with your hand. The paddles, the coffee cup (including the coffee in it) and the archive notes had the same problem.

- The check now includes inactive objects. Anything switched on and off at runtime also keeps its own mesh.
- The builder confirms that all 14 movable objects keep a visible mesh of their own. In the rebuilt scene, all 14 do.
- The boxes started 8.5 cm inside their plinths, so physics shoved them askew when the room loaded. They now rest on top.
- Holding feels more natural. A close grab keeps the object where your hand closed on it. A distance grab brings it along the pointer to just clear of your hand. Before, the object's centre snapped 13 cm in front of the hand, so a box swallowed it.

### Refined low-poly art

See "Art style" in the README. Checked with **After Hours → Capture room previews**, which also exposed three problems, now fixed:

- **Light leaking where walls met the ceiling.** Bevelled wall and ceiling edges left a gap that shadow bias widened. The invisible shadow slabs are now square-edged.
- **Sparkling floor seams.** Floors are now matte and cast no shadows.
- **Missing objects in editor previews.** The builder overwrote mesh assets in place, and the editor kept drawing the old data. It now replaces the assets, and reopens the scene after building.

Estimated static geometry per room, from the generated mesh assets: about 60,000 triangles in the archive, the heaviest room, and under 20,000 in each other room. That is well within what a Quest 3 can draw, but frame rate still needs measuring on the headset.

### Release build

The shareable release APK built with zero errors. It is 68 MB. Permissions are unchanged (hand tracking and internet only), and the APK contains no LAN address.

## October 5: menu button and seated movement

### Everything menu-like is now in one menu

The left controller's menu button opens a menu at arm's length, a little below eye level. With tracked hands, a left palm pinch opens it; desktop preview uses Tab. It moves and turns with the player, closes when you change rooms, and fits itself in front of a nearby wall or counter by coming closer and shrinking to match.

These signs are gone from the rooms, and their actions are in the menu:

- the lobby's WALK / TELEPORT, AUDIENCE ON / OFF, SEATED / STANDING and HEIGHT signs
- every chapter's LOBBY and TRY AGAIN signs
- QUIET KITCHEN (now KITCHEN CROWD) and the rooftop's BACK TO THE LOBBY
- the TURN / LOBBY / TURN controls that floated below the view in seated mode

The lobby doors, CONTINUE, and each chapter's task buttons (ROW, BREW, ASK FOR HELP) stay where they were. In development builds the Immersive Debugger moved from the menu button to a left thumbstick click.

### Seated movement

- Pulling the right stick straight back turns you around. With walking off, and in the boat, the left stick snap turns too.
- In seated view, a floor teleport turns you to face where you pointed.
- FACE FORWARD turns the room's main view back in front of you.
- A comfort vignette softens the edges of the view while you walk with the stick. It is on by default and saved; the menu switches it off. Its shader measures the angle from each eye's own view direction, so both eyes see the soft edge in the same place.

### Fixed along the way

- **The coffee cup tipped over.** Unity's cylinder has a capsule collider with a rounded base, so a cup released at any angle fell over and rolled off the drip tray. Since the low-poly pass, held objects turn with the hand, so it was easy to release it tilted. The cup now stays upright while held and has a flat-bottomed collider that matches its shape. The gameplay test found this.
- **The automated tests could miss clicks.** With the mouse pointer resting over the Game view, the editor's own mouse took over from the test's. The tests now claim their devices before each input. **After Hours → Tests** runs them.

### Results

- Gameplay journey: 53 of 53 checks passed.
- Menu and seated checks: 42 of 42 passed, including a scene reload.
- Navigation: 94 of 94 passed.
- The shareable release APK built with zero errors. It is 70 MB. Permissions are unchanged (hand tracking and internet only), it contains no XR Operator layer, and it contains no LAN address.
- It was not installed: Unity's `adb` found no Quest over USB.

## October 6: competition build

Prepared for the Meta VR Start Developer Competition, whose rules require hands-only play from an airplane seat and a release-signed APK in a "Competition" release channel.

### Seated, hands-only play

- **Pick up from the seat.** Pointing and pinching picks up an object up to 10 m away (was 3.5 m), and it glides to the hand. The pointer snaps to the nearest object within 4 degrees, and the hand ray stops at it, so it is clear what a pinch will take.
- **Send to a destination.** Holding an object and pointing at where it belongs lights the destination up. Letting go sends the object there in a short arc: the truck's bay, the spout (the cup only, and it lands upright), and each archive tray. A note on the wrong tray drifts back to the desk. Drop zones ignore objects while they glide, so nothing counts twice.
- **Arrival points** in the warehouse, kitchen and archive are now in front of the work. The kitchen watchers stand on both sides of the machine, all within 60 degrees of it.
- **Compact menu.** The panel is 53 × 52 cm at 65 cm, so it fits inside the central field of view, with CLOSE in the top corner.
- **First-visit guide** with a content note.

### Sound

Nineteen original sounds, synthesised by `Tools/make_audio.py`: an ambience for every room, a theme in the lobby and on the rooftop, a crowd murmur that follows the number of watchers, and feedback for selecting, grabbing, sending, landing, the menu, teleporting, turning, rowing, spilling and pouring. Room changes fade the sound with the picture. The audio adds 1.6 MB to the APK.

### Release signing

**After Hours → Create release signing key** created `UserSettings/AfterHours-release.keystore` (RSA 2048, valid until 2054) and a random password in `UserSettings/AfterHours-release-key.txt`. `UserSettings` is not committed. The release build applies the key and clears it from the project settings afterwards.

### Results

All four suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Airplane-seat checks (new) | 58 of 58 |
| Gameplay journey | 53 of 53 |
| Menu and seated checks | 43 of 43 |
| Navigation | 94 of 94 |

The airplane-seat suite found one problem, now fixed: six of the eight kitchen watchers stood 66 to 94 degrees to the side of the machine, beyond the edge of the view for a player facing it. They now stand within 59 degrees.

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 72 MB |
| Version | 0.2.0, version code 401376 |
| Signing | APK Signature Scheme v2, release key `CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:42:6E:26:3A:9A:E9:F7:51:6D:55:A9:E1:3C:24:01:63:1B:3F:F8:26:44:9E:42:7D:51:5E:CF:E3`. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 36 |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | The DevAgent settings in the APK are empty, and the XR Operator layer is excluded. |

## October 6 evening: The old résumé, the mirror and the wardrobe

### New content

- **The old résumé** (door 05, the fifth chapter). Five old résumé pages are sent into a shredder from the seat. Each page is drawn into the slot, shredded with a sound and paper strips, and leaves a short kind line. When all five are gone, a blank page asks for one line that is still true. The choice is saved and shown on the mirror's nameplate.
- **A full-length mirror** on the lobby's left wall. A faceted, game-style avatar copies the player's head and hands as a reflection would. It uses no second camera: a mirror-image copy of the lobby sits behind the glass. **MIRROR** on the lobby desk brings a seated player to it.
- **A wardrobe** beside the mirror: skin tone, hair, hair colour, build, top, hat, neck and pin. Nine pieces are earned by playing, one for each chapter, one for all five, and three for kind moments: a first small step, asking for help in the kitchen, and resting on the rooftop. Pieces are saved on their own key, so TRY AGAIN never takes one away. A chime and a spoken line announce each new piece.
- Three new sounds: print-room rain, the shredder and the unlock chime (22 in all).

### Project Setup Tool

Meta's Project Setup Tool's **Fix All** was applied during the session. The builder now keeps two of its changes: target API 34 (was 36) and dynamic resolution (Quest 2: 0.7–1.3, Quest 3: 0.7–1.6). The Touch controller proximity profile stays off, because the Meta XR Simulator rejects it.

### A crash when entering Play Mode

Unity closed twice when entering Play Mode, inside Meta's XR Operator layer (`XrApiLayer_METAX_operator`), before any game code ran. Turning off **Initialize XR on Startup** for the Standalone platform avoided it, and all suites then ran. The release build turns it back on (`ConfigureQuest`), and the APK does not include the Operator layer, so the headset is not affected.

### Results

All five suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Mirror and wardrobe (new) | 38 of 38 |
| Airplane-seat checks | 82 of 82 |
| Gameplay journey | 75 of 75 |
| Menu and seated checks | 44 of 44 |
| Navigation | 105 of 105 |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 72.6 MB (72,559,221 bytes) |
| SHA-256 of the APK | `b2e59c94dd7dcf4371f3cd154687bf20bb7611c0283731ac8d2a6e06492c2d88` |
| Version | 0.3.0, version code 402665 (first built as 401611 on October 6; rebuilt from the same project on October 7) |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`). Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | The shredder, mirror and wardrobe scripts and the "Still true" scene text are in the APK. No DevAgent data and no XR Operator layer. |

It was not installed: no Quest was connected.

On October 7 the navigation suite passed again (105 of 105) after a fix to the port of Meta's Remote Agent Server, an Editor preference on this computer (see `Debug-validation.md`). The release APK was then rebuilt; it was checked again before it was committed, with the same results as above.

## October 8: The rage room

### New content

- **The rage room** (door 06, the sixth chapter). An old work computer sits on a desk, still showing the old job. The player picks up a bat and swings at it. Short forearm swings count, so the room can be played from a seat. The monitor, keyboard, tower and mouse break in stages (13 hits in all), with glass, plastic and metal sounds, shards, sparks and a haptic pulse on each hit.
- When everything is broken, the room goes quiet. The ambience fades and a calm panel appears. After a pause the chapter completes, and **WHEEL IN A NEW ONE** brings a new computer to break again.
- The bat stays in the hand when the pinch opens and through tracking loss. **PUT THE BAT BACK**, or leaving the room, returns it to its rack.
- Broken parts are saved with the checkpoint. TRY AGAIN mends them.
- New wardrobe piece: the **Let it out pin** (ten pieces in all). The jacket now needs all six chapters.
- Eight new sounds (30 in all). Version 0.4.0.

### Tests

The navigation suite now reloads the scene and clears saved progress before it starts, so it no longer depends on what ran before it. The gameplay suite must still run first in a fresh Play Mode session.

### Results

All five suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 95 of 95 |
| Navigation | 126 of 126 |
| Airplane-seat checks | 92 of 92 |
| Menu and seated checks | 46 of 46 |
| Mirror and wardrobe | 41 of 41 |

The airplane-seat suite checks that every part of the computer is within 0.65 m of a seated player's hand. It then breaks each part with short swings without moving the player.

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 73.0 MB (73,045,601 bytes; 0.5 MB more than 0.3.0) |
| SHA-256 of the APK | `75af9656e2d16c2507030b27e75cca8f92f142e9adac85e4086d2905558cb299` |
| Version | 0.4.0, version code 404414 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`). Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | The rage room scripts (`RageRoom`, `Breakable`, `Bat`) and the room's scene text are in the APK. No DevAgent data and no XR Operator layer. |

It was not installed: no Quest was connected.

## October 11: more flying parts

### What changed

- **Many more pieces fly off.** The old computer now breaks into 152 pieces (it was 94): extra circuit boards, fans, drive parts, key caps, cable ends and plastic shards. Each stage of damage throws a handful.
- **Desk clutter breaks too.** A mug, a phone, a stapler, a pen cup and a name plate sit within 80 cm of the player's hand. They scatter when hit. They are not needed for the chapter (58 pieces more, 210 in all).
- **Loose pieces can be knocked around.** Once a piece lands it can be batted again (`Loose`). Every hit throws something, and WHEEL IN A NEW ONE (or TRY AGAIN) mends everything.
- Two new sounds, a clatter and a paper hit (32 in all). Version 0.4.1.

### Tests

The tests now switch the editor's real mouse and keyboard off while a suite runs and use the test devices only (`TestInput`). A real mouse left in a pressed state had been taking over as the current mouse and blocking the gameplay test's floor click. This was a test-setup fault, not a game fault.

### Results

All five suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 108 of 108 |
| Navigation | 126 of 126 |
| Airplane-seat checks | 98 of 98 |
| Menu and seated checks | 46 of 46 |
| Mirror and wardrobe | 41 of 41 |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 74.5 MB (74,471,727 bytes; 1.4 MB more than 0.4.0) |
| SHA-256 of the APK | `620875cebe081db1075b7821221ef3eeb20379f376b81bffea422fa8d4eb548e` |
| Version | 0.4.1, version code 408526 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`). Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | The rage room scripts (`Loose`, `RageRoom`, `Breakable`, `Bat`) are in the APK. No DevAgent data, no LAN address and no XR Operator layer (the only match for its name is Unity's list of OpenXR extension names). |

It was not installed: no Quest was connected.

## October 11 (evening): oars, clean mode, night ocean and sea sounds

### What changed

- **The oars turn on oarlocks.** Each oar is held in a lock on the side of the boat and can only turn about it, never slide through it. Pushing the handle forward swings the blade back through the water (the drive) and drawing it back lifts it out (the recovery), so the blade always moves opposite the hand. A stroke counts once, when a forward push is followed by a pull back. A push alone counts nothing, and ROW sweeps both oars by itself. Letting go eases the oar back to rest with the blade above the water.
- **Messages and the menu can be dismissed.** The menu button now closes everything: one press clears a message, the hint under it and the menu, and never opens the menu on that press. The first-visit card keeps its own behaviour while it waits for the menu.
- **Clean mode** (a menu button, saved) hides every sign, caption, counter, hint and the first-visit card, in every room. Buttons keep their labels. The comfort menu stays reachable.
- **The lighthouse has a light** that turns clockwise (40 degrees a second) with a second, fainter beam behind it. The beams are soft crossed fins with vertex fades, drawn with additive glow, with no real-time lights.
- **Ocean at night** (a menu button, saved) changes only the ocean of shame: night sky, a faceted moon, a starfield (260 small stars in one mesh), dim moonlight, dark water, glowing lanterns and a stronger beam. Leaving the room puts the day light back for every other room.
- **Sea sounds.** Six new sounds (38 in all): water lapping at the hull and a wind-and-night-sea bed that crossfade between day and night, two gull cries by day, a bell buoy at night and an oarlock creak with each stroke.
- **A mirror secret.** Three taps on the lobby mirror's glass turn the reflection into an X-ray: a skeleton of glowing white bones (a soft halo around each, in one shared material that pulses gently) over a dark backdrop just behind the glass, with a rising hum. Three more taps, or leaving the lobby, switch it back. The skeleton hangs from the same joints as the body, so it follows the head, hands and height. Nothing is saved.
- Version 0.5.0.

### Tests

The mirror suite gained the X-ray checks (skeleton built and hidden at first; two taps do nothing; three turn it on with the body hidden and a dark backdrop; slow taps do not add up; three more turn it off; leaving the lobby ends it). A new suite (ocean, clean mode and night checks) covers the oars, the light, night, the menu button, the menu's reach and clean mode. The menu-opening steps in the other suites press the menu key up to twice, since a message can now take the first press. `Logs/AfterHours-console.txt` now holds every console warning and error from an editor session (`ConsoleCapture`).

### Results

All six suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 108 |
| Navigation | 126 |
| Airplane-seat checks | 98 |
| Menu and seated checks | 46 |
| Mirror and wardrobe | 53 |
| Ocean, clean mode and night | 72 |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 73.7 MB (73,692,101 bytes; 0.8 MB less than 0.4.1) |
| SHA-256 of the APK | `ab7a2ad4ea45a30d2b8113ef5cd46340bc9f910d2a993f97612aa325f63d50da` |
| Version | 0.5.0, version code 408996 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`). Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | The new scripts (`Oar`, `Spin`, `Guidance`, `SeaSounds`, `MirrorAvatar`) and the X-ray's two sounds and backdrop are in the APK. No DevAgent data, no LAN address and no XR Operator layer. |

It was installed on the connected Quest with **After Hours → Install release APK on connected Quest**. The copy already on the headset was signed with a different key, so it was uninstalled first (its saved progress went with it); Unity then reported "Installed AfterHours-release.apk … and started com.afterhours.mindoffice". Nothing has been played on the headset yet.

## October 12: throwing, controllers and Quest 2

### What changed

- **Boxes can be thrown into the truck** (heavy things). Let go of a box while your hand is moving and it leaves with that speed (a little boosted, never over 14 m/s) and direction, flies under physics and is caught by the bay. A throw aimed within 25 degrees of the truck is pulled most of the way onto a lofted arc that lands in it; the pull fades to nothing at 55 degrees, so a throw the other way goes where it was thrown. A box that stops short goes back on its stand after about 1.6 s of rest, with a gentle message. Point-and-let-go is unchanged, and so is desktop play, where nothing is thrown.
- **Hands first, controllers automatic.** The wording of the first-visit guide, the lobby board, the comfort note, the menu's tip, hold hints and room messages follows what the headset reports: hands by default, controllers once they are picked up (after 0.6 s, so a passing controller does not flicker the signs). The trigger now also picks things up with controllers.
- **Quest 2.** One APK for Quest 2, Quest Pro, Quest 3 and Quest 3S (the manifest already listed all four). On a Quest 2 the game asks for render scale 0.9, shadows to 20 m and 72 Hz if offered. Unmeasured.
- **Boxes settle exactly into the truck.** A box drawn into its place used to end a few centimetres out because the physics smoothing lagged behind the hand-set position; found by the new throw checks and fixed in `Grabbable` (the position is now given to the body as well).
- Version 0.6.0.

### Tests

A new suite (throwing, controllers and Quest 2) covers the throw maths, the wording and the real throw path (see the README). The other six suites were run again.

### Results

All seven suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 108 |
| Navigation | 126 |
| Airplane-seat checks | 98 |
| Menu and seated checks | 46 |
| Mirror and wardrobe | 56 |
| Ocean, clean mode and night | 72 |
| Throwing, controllers and Quest 2 | 71 |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 73.7 MB (73,698,625 bytes; 6.5 KB more than 0.5.0) |
| SHA-256 of the APK | `95bb1dea25409ef998fcd64be9be838a74e20e1d17bae56cc8c114a2b9bad8b0` |
| Version | 0.6.0, version code 410541 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | The manifest lists Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley (`quest2`, `questpro`, `quest3`, `quest3s`, `stanley`); hand tracking stays optional (`required=false`) so controllers work without it |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | The new scripts (`Throwing`, `DeviceProfile`, `Wording`, `InputText`) and the throw and hand-speed code are in the APK. No DevAgent settings or session data, no LAN address and no XR Operator layer (Meta's own debugger classes are code in the SDK, as in 0.5.0). |

It has not been installed on a headset or played there yet.

## October 13: text that nothing stands in front of

### What changed

- **A new text visibility check.** Every sign in the game (138 of them, counting door signs, counters, buttons, headings, captions and the words on the wardrobe) is tested from each place a player can read it: rays from the three seated eye heights at every arrival point and from a grid of floor places 1.5 m apart, to a 5 by 3 grid of points across the lettering. A ray that meets the visible faces of anything solid, or another sign, first counts as blocked. A sign fails if it faces away, is hidden from the arrival point, is hidden from at least half of the places it could be read from, is in fog, or sits at under 1.8 to 1 contrast against what is behind it as seen from the arrival point. The ocean is scanned at every stroke from 0 to 14, because the lighthouse comes toward the boat.
- **What it found.** 31 problem signs on the first scan, including the ones behind the lighthouse and its beam in the ocean (the rowing note straight ahead, the heading and counter), the lobby counter and its two buttons hiding door signs, the getting-started board covering the door numbers, and the truck hiding the heavy room's heading. After several rounds of changes the scan reports 138 of 138 signs clear.
- **Layout changes.** The ocean's heading is on the right, turned toward the boat, and the rowing note is on the left on its own plate, both clear of the lighthouse's path; the distance counter and the welcome sign on the island have plates. The lobby counter is narrower, its buttons moved, the getting-started board is higher, and the far wall's lines are larger. The heavy room's heading is high on the back wall below the ceiling, and its truck sign carries both lines on one plate. The kitchen's help note, the archive's heading and category signs and the rooftop's message each have a dark plate. Light lettering has a thin dark outline (and dark lettering a light one) through two new text materials.
- **Clean mode** hides the new plates along with their text (`Guidance.backing`). The plates are left out of the merged room geometry.
- Version 0.6.1.

### Tests

A new suite (text visibility) was added and the other seven were run again. The queue runner starts it with `text`. The first run of the gameplay journey after Unity was reopened stopped at its first movement check; the same suite passed in full when run again.

### Results

All eight suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 108 |
| Navigation | 126 |
| Airplane-seat checks | 98 |
| Menu and seated checks | 46 |
| Mirror and wardrobe | 56 |
| Ocean, clean mode and night | 72 |
| Throwing, controllers and Quest 2 | 71 |
| Text visibility | 138 signs |

The text check also records 63 notes: 62 of signs that are partly hidden from a few places in a room (a shelf or the truck in the way from one corner), and one for the words on the whole mug, which are too small to read from anywhere by design.

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 73.7 MB (73,733,017 bytes; 34 KB more than 0.6.0) |
| SHA-256 of the APK | `f86ac3a624b280e66affb2fc7c60e3f7f9271b52a5ec4757579a7ea264933df9` |
| Version | 0.6.1, version code 411316 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | No DevAgent settings or session data, no LAN address and no XR Operator layer (Meta's own debugger classes are code in the SDK, as before). |

Unity crashed once during this round when entering Play Mode (Meta's XR Operator layer, the crash described in the README). Desktop XR start-up was switched off for the test run and switched back on before the build.

## October 13 (later): a fresh boat, and signs that fit their plates

### What changed

- **The boat starts fresh.** Saved progress used to put the shore back where it was left even after a finished crossing, so the lighthouse stood right beside the boat on the next visit (and when the game was started with a finished ocean saved). Now `OceanRoom.OnArrive` starts a finished crossing again from open water, with the counter at 14, and the chapter stays marked as done. A crossing still under way carries on where it was left, and TRY AGAIN still starts it over. The oars are also put back at rest on arrival.
- **Plates fitted to their words.** Eleven plates were sized by hand, so some had a lot of empty space (the ocean's rowing note, the lobby's getting-started board, the archive heading) and some had words touching their edges (the island's welcome sign, the distance counter, the archive trays). `WorldBuilder.FitPlates` now measures the words that stand on each plate, including every wording a sign can show (hands and controllers, both distance counters), grows or shrinks them to fill the plate less a margin, and closes the plate in around them (6 cm of margin or more on every side). A plate is never bigger than before. The three archive trays share a size. The rowing note's lettering is noticeably larger. The rage room's rules board is now built facing the room like the other plates, and the lobby's getting-started board goes with its text in clean mode.
- **A plate check in the text suite.** The suite now also tests the built scene: every word on a plate must sit inside the plate with at least 3 cm to spare, and each plate's smallest margin is reported. All 11 plates were found (smallest margin 6.0 cm) and all 138 signs pass.
- Version 0.6.2.

### Tests

The navigation suite now checks that returning after a finished crossing starts from open water with the chapter still done, and that returning halfway carries on where it was left (131 checks; it had one for the old behaviour).

### Results

All eight suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 108 |
| Navigation | 131 |
| Airplane-seat checks | 98 |
| Menu and seated checks | 46 |
| Mirror and wardrobe | 56 |
| Ocean, clean mode and night | 72 |
| Throwing, controllers and Quest 2 | 71 |
| Text visibility | 138 signs, 11 plates |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 73.7 MB (73,737,873 bytes; 4.9 KB more than 0.6.1 bytes) |
| SHA-256 of the APK | `bf6c99f6564e02ddee0173a52a81deaa799053dadcc54cde2b39e92090e905a6` |
| Version | 0.6.2, version code 411420 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.6.2, versionCode 411420, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. No private-network address and no session data in any file (Meta's own debugger classes appear as code and type names in the SDK, as before). |

Desktop XR start-up was switched off for the test run and switched back on before the build.

## October 13 (evening): waves on the water, and a sea you can hear

### What changed

- **The water moves.** It was one static mesh of flat facets. `OceanWaves` (new) now rebuilds it thirty times a second from four crossing swells: a main swell (24 m long, an eight second beat), a longer swell underneath, a cross swell and a little chop. Together they are a little under half a metre from trough to crest, and never reach the deck. The facets stay flat and use the same matte water material by day and by night, and each is lit afresh as it tilts, so the light slides over the sea. The player and the boat do not move with it. The water now casts no shadows. The mesh is rebuilt on the CPU (about 1,500 height samples and 2,800 triangles) and only while the ocean room is on.
- **Waves in the sound, in step with the water.** A new sound, `Ambience - Waves`, is four waves to a 32 second loop, one every eight seconds: a low swell lifts, the crest hisses past, the water drains away with a trickle, about 14 dB from trough to crest. It is the main sound of the sea by day. `SeaSounds` keeps a clock that follows the sound's own position, and the main swell's crest passes the boat 3.3 seconds into each wave, as the sound crests.
- **Surf from the island.** A new sound, `Ambience - Surf` (waves breaking on a beach: a thud, a long hiss of foam, the water sliding back), is played from the island's own beach, so it is a real place: the island is part of the moving horizon, so the sound grows louder and clearer as you row nearer.
- **The sea is louder and clearer.** The old swell loop (random timing) is now a quiet bed of open sea under the waves. Overall the sea is about 5 dB louder than before.
- **New sounds go at the end of `Tools/make_audio.py`**, so regenerating still leaves every earlier file as it was (42 WAV files in all).
- Version 0.6.3.

### Tests

The ocean suite has 16 new checks (88 in all): the water is one moving mesh kept in step with the sea sounds, its size and bounds, no shadows, the main swell repeats every 8 seconds with a crest at 3.3 seconds, all the swells together are under 0.26 m, the water is rebuilt as the game runs (260 times in ten seconds), the sea at the boat rises and falls (0.35 m in ten seconds), no wave climbs above the deck (highest 0.12 m, lowest -0.52 m), the beat keeps time with the game, one rebuild takes 0.33 ms in the editor, the surf comes from the shore and gets nearer as the boat is rowed (17 m, then 11 m), the new sounds are loaded, looping and the right length, the day and night mix includes them, and the water keeps rolling at night. The sound was not playing in the editor run, so the water's beat was tested against the game clock and not against the sound's own position.

### Results

All eight suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 108 |
| Navigation | 131 |
| Airplane-seat checks | 98 |
| Menu and seated checks | 46 |
| Mirror and wardrobe | 56 |
| Ocean, clean mode and night | 88 |
| Throwing, controllers and Quest 2 | 71 |
| Text visibility | 138 signs, 11 plates |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 74.2 MB (74,195,057 bytes) |
| SHA-256 of the APK | `9cb56ebcfd8b26f98e21158692f39d3e66a82b3255a5ef61bf2bade269cfdd7d` |
| Version | 0.6.3, version code 411472 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.6.3, versionCode 411472, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. No private-network address and no session data in any file (Meta's own debugger classes appear as code and type names in the SDK, as before). 457 KB larger than 0.6.2: the two new sounds, compressed. |

Desktop XR start-up was switched off for the test run and switched back on before the build.

## October 13 (night): paper everywhere when a résumé is shredded

### What changed

- **A flurry of loose bits.** Each shredded page now sends up a burst of loose paper bits (two particle systems, white and ivory, about 160 bits each) that fountain out of the shredder's head, tumble on noise and drift down across the room, and rest on a floor plane until their time is up. There is no world collision, so it costs next to nothing.
- **Scraps that stay.** Once the bits have come down (1.8 seconds after the page is gone), that page's set of scraps appears: about 240 thin flecks and strips, thickest around the shredder and the desk, with more across the whole floor, on the desk either side of the page rack, on the shredder's top clear of the slot, and on the window sill. Five pages leave 1,195 scraps in all over 10.8 by 14.3 m. Each set is merged into two meshes (white and ivory) with no colliders, and sits hidden until its page is shredded, so it is two draws a set.
- **Fuller bins.** Each heap in the bin is now 56 strips and chips merged into two meshes, in place of ten long strips that were each an object of their own.
- **They keep.** If the app is closed midway, the scraps of every page already shredded are there when it reopens, and a page that is restored is restored once. Resetting the room clears every set and the flurry.
- Version 0.6.4.

### Tests

The gameplay journey has 2 new checks (110 in all): a flurry of paper bits (326 in the air) is over the shredder after the first page, and every shredded page has left its scraps on the floor, the desk and the shredder. The menu and seated suite has 3 new checks (49 in all, one existing check now also covers the scraps): a shredded résumé restores exactly once with its paper in the bin and on the floor, every page leaves at least 100 scraps (1,195 in all), the scraps lie across the room, on the desk, the shredder's top and the window sill (10.8 by 14.3 m), and they have no colliders and cost two draws a set. On one early run the gameplay journey failed its first check (keyboard movement in the lobby), long before any paper is involved, and the cause was not found; the same suite passed when run again on its own and in the full run after the final changes.

### Results

All eight suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 110 |
| Navigation | 131 |
| Airplane-seat checks | 98 |
| Menu and seated checks | 49 |
| Mirror and wardrobe | 56 |
| Ocean, clean mode and night | 88 |
| Throwing, controllers and Quest 2 | 71 |
| Text visibility | 138 signs, 11 plates |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 74.3 MB (74,300,877 bytes) |
| SHA-256 of the APK | `0e8669bbef2c0a8c791c14eee70929764426ff854cc4a8b9b4aa5d7eb4dd8b92` |
| Version | 0.6.4, version code 411613 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.6.4, versionCode 411613, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. No private-network address and no session data in any file (the only matches for an address pattern are version numbers and encoding identifiers in the .NET libraries, and Meta's own debugger classes appear as code and type names in the SDK, as before). 106 KB larger than 0.6.3: the paper scraps, which are part of the scene, and the new code. |

Desktop XR start-up was switched off for the test run and switched back on before the build.

## October 13 (late night): confetti when a résumé page hits the floor

### What changed

- **An easter egg.** In the old résumé room, a page thrown or dropped onto the floor vanishes in a burst of multi-coloured confetti. A burst of 220 flat bits flies up and out from where the page landed and slows in the air, and a shower of about 150 more falls from two metres overhead. There are seven colours (pink, amber, green, blue, violet, orange and yellow), each bit taking one at random, so each of the two particle systems shares one flat unlit material and is one draw; the bits cast no shadows, tumble, rest on a floor plane and shrink away after about eight seconds. A pop with a flutter of paper and four bell notes (`SFX - Confetti`, 1.8 seconds, generated in `Tools/make_audio.py` at the end of the list, so every earlier sound regenerates as it was; 43 WAV files in all) plays where the page landed.
- **Nothing is lost.** The page is back on its stand 1.6 seconds later, set down gently, and nothing is counted. It only counts as a throw when the page hits the floor at 1.5 m/s or more, within 20 cm of the floor, on a flat knock, so a page set on the desk or lowered gently does nothing. Leaving the room mid-burst brings the page back at once, and TRY AGAIN in the room clears the confetti.
- **Pages are tossed, not thrown.** A page let go while the hand is moving and nothing is pointed at is tossed with the hand's own speed. A page let go while pointing at the shredder is sent in, as before, and a page is never helped toward anything in flight. Pages are a new kind of thing that can be tossed (`tossable`), not the throwable boxes: the throwing suite still insists on exactly five throwable boxes, and a tossed page that comes to rest stays where it fell (the confetti takes it away before long). The hint for a page does not mention throwing, so the egg stays a surprise.
- Version 0.6.5.
- The notes for 0.6.4 gave the scraps as 1,300 in all; the committed scene has 1,195 (the suite prints the count), so that figure is corrected here and in the README.

### A bug the tests caught

A page that came back from the confetti sprang back to where it had landed a moment after it was set on its stand: its physics body woke where the page lay and was never told where the stand was. The page is now given its stand's pose on the body as well. The throwing suite also had to move the player with the page in hand (placing the player lets go of everything held), which is why its first run missed the burst.

### Tests

The gameplay journey has 8 new checks (118 in all): the room has its confetti and the pages can be tossed; a page thrown onto the floor bursts and goes; 220 bits in the burst, more than 20 in the shower and at least 6 of the 7 colours are in the air; the page is back on its stand and nothing is counted; the confetti comes to rest on the floor with none falling through it; a page dropped on the desk does not burst; another page bursts; and TRY AGAIN brings it back and clears the confetti. The menu and seated suite has 4 new checks (53 in all) on how the egg is wired. The throwing suite has 8 new checks (79 in all) that use the real player code: only the five pages can be tossed and a toss is never helped; a page does not mention throwing; the page stays in hand when the player steps clear of the desk; a page flicked at the floor flies off the hand and bursts; it is back on its stand with nothing counted; and a page let go while pointing at the shredder is sent in, not tossed. A picture of the confetti is saved as `Documentation/Previews/Old resume-Confetti.png`; it shows the colours and the burst and shower plainly.

### Results

All eight suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 118 |
| Navigation | 131 |
| Airplane-seat checks | 98 |
| Menu and seated checks | 53 |
| Mirror and wardrobe | 56 |
| Ocean, clean mode and night | 88 |
| Throwing, controllers and Quest 2 | 79 |
| Text visibility | 138 signs, 11 plates |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 74.3 MB (74,337,197 bytes) |
| SHA-256 of the APK | `3e565c028c2b7812ec000a66501a5c8228a18c07a7d6f61c8272ada9617ac635` |
| Version | 0.6.5, version code 411796 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.6.5, versionCode 411796, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. The confetti code, its material and its sound are in the build. No private-network address and no session data in any file (the only matches for an address pattern are version numbers and encoding identifiers in the .NET libraries, and Meta's own debugger classes appear as code and type names in the SDK, as before). The account name appears once, beside the project name in the player's global settings. 36 KB larger than 0.6.4: the confetti sound and the new code. |

Desktop XR start-up was switched off for the test run and switched back on before the build.

## October 13 (evening): two boxes slammed together burst

### What changed

- **An easter egg.** In the heavy room, hold a box in each hand and slam them together and both burst: a flash, a spray of sparks, a puff of smoke and a shower of chips in the boxes' own colours, with a boom. A held box is carried by the hand and is not a physics body, so two held boxes never collide. A new component, `BoxBlast`, watches each carried box instead: it works out how fast each is moving (smoothed over about 50 ms, after every box has followed its hand for the frame, so the speed is the hand's speed between the last two frames divided by the time between them) and bursts a pair when they are in different hands, their colliders overlap, and they are closing on each other at 2.4 m/s or more. A box must have been in the hand 0.3 s first, so one drawn in from across the room is never a slam.
- **The burst.** Four one-shot particle systems are moved to the midpoint and played: a flash (one warm cream ball, a fifth of a second), 70 sparks (four fire colours, stretched along their flight), 9 puffs of dust-grey smoke and 110 chips of box (seven colours, taken from the boxes, the tape and the plinth wood) that fall, bounce on a floor plane and lie there before they shrink away. 190 bits at once, no more than 300 in all, one shared flat unlit material, no shadows. A boom (`SFX - Blast`, 2.2 seconds: a crack, a low boom, a rumble, a spray of sparks and a rattle of debris) plays where they met; it is generated at the end of the list in `Tools/make_audio.py`, so every earlier sound regenerates as it was. 44 WAV files in all.
- **Nothing is lost.** Both boxes are back on their stands two seconds later, set down gently, and nothing is counted. Leaving the room mid-blast brings them back as soon as it reopens, and TRY AGAIN in the room brings them back at once and clears the smoke. Boxes held apart, pressed gently together, or carried side by side at speed do nothing.
- A box that vanishes from a hand is now cleared of its holder as well (`Grabbable.Vanish`), and a new `Grabbable.BringBack` sets a returning box's physics pose as well as its transform (the confetti page uses it too).
- Version 0.6.6.

### Bugs the tests caught

The chips were gone too soon. After 1.2 seconds only 9 of the 110 were left: each bounce on the floor plane cost a quarter of a chip's life, so they died after a few bounces rather than lying there. Bouncing no longer costs any life, and all 110 are still there after 1.2 seconds, resting on the floor (0.014 m at the lowest). One structure check also looked for the floor plane under the wrong object. The first run of the full set was cut short when the editor ended Play Mode on its own (it left a scene-recovery copy, which git ignores), so the whole set was run again from a rebuilt scene.

### Tests

The gameplay journey has 20 new checks (138 in all): the room has its blast and all five boxes are in it; two boxes can be held at once, one in each hand; held apart does nothing; pressed gently together does nothing; carried side by side at speed does nothing; a slam bursts both; the burst is in the air (1 flash, 70 sparks, 9 smoke, 110 chips in 7 colours) and where the boxes met; the chips land on the floor and none fall through it; both boxes are back on their stands and neither is held or counted; with the player's own hand holding one box and a stand-in hand sweeping the other in, both burst and the player's hand lets go, and it can pick up another box straight after; both are back and nothing is counted; a burst made by hand sends chips flying; and TRY AGAIN brings the boxes back at once and clears the smoke. Stand-in hands move at set speeds, so a slam is exactly as fast as it is written. The menu and seated suite has 4 new checks (57 in all) on how the blast is wired: the boxes, the sound and the four systems, a single burst of 1, 70, 9 and 110, one floor plane and seven colours, and one shared material with no shadows and no more than 300 bits. A picture of the burst is saved as `Documentation/Previews/Warehouse-Blast.png`. The throwing suite is unchanged (79 checks).

### Results

All eight suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 138 |
| Navigation | 131 |
| Airplane-seat checks | 98 |
| Menu and seated checks | 57 |
| Mirror and wardrobe | 56 |
| Ocean, clean mode and night | 88 |
| Throwing, controllers and Quest 2 | 79 |
| Text visibility | 138 signs, 11 plates |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 74.4 MB (74,362,089 bytes) |
| SHA-256 of the APK | `df4659e622ab534fea7350ef9e7353beec003a6a1d9cd2ee9071124c798c7bf5` |
| Version | 0.6.6, version code 411875 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.6.6, versionCode 411875, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. The box blast code, its four particle systems and its sound are in the build. No private-network address and no session data in any file (the only matches for an address pattern are version numbers and encoding identifiers in the .NET libraries, and Meta's own debugger classes appear as code and type names in the SDK, as before). The account name appears once, beside the project name in the player's global settings. 25 KB larger than 0.6.5: the boom and the new code. |

Desktop XR start-up was switched off for the test run and switched back on before the build.

## October 13 (night): mugs, cupboards and a better espresso machine

### What changed

- **Five mugs that break.** The kitchen has five mugs: your cup, a teal, a coral and a navy mug on the counter, and an orange one hidden in a cupboard. Any of them can be carried, sent under the spout by pointing (the spout takes any mug, and a second mug sent there sends the first one back where it started) or thrown. A thrown mug, or one let go of at carrying height, breaks: it hits something at 2.6 m/s or more, which is a fall of about a third of a metre or more, so setting one down gently never breaks it. It bursts into shards (34 chips over the counter or the floor, in the mug's colour and white), a puff of dust and, if it held coffee, a splash that lies flat where it landed, with a crash (`SFX - Shatter`). It is back on its shelf, whole and empty, 2.5 seconds later, and nothing is counted: the pours, the mistakes and the chapter are untouched. A hand sweeping through a mug, one mug gliding in beside another and the first 0.4 s after a mug lands from a send are never knocks.
- **Cupboards, and an orange mug.** The six cupboards under the counter are hollow now, and each door hangs from a hinge on its outer edge and swings open when pointed at and pressed (`SFX - Cabinet`, a creak and a knock). Five hold plates, jars, bowls, tins, a kettle and cloths and say so. The fifth from the left hides an orange mug. Putting it under the spout and brewing earns the **Orange mug pin**, an eleventh piece to wear (it shows as a brass disc with a tiny orange mug and a dab of coffee on the reflection's chest). After the three pours it can still be filled once, and nothing is counted for it.
- **The machine has a lever and a steam wand.** A lever on its side pulls down as the machine brews (and gives a short jerk when the pressure is faulty); pointing at it brews exactly as BREW does. There is also a gauge, a portafilter handle, a steam valve and a steam wand with a nozzle: pointing at the wand or the valve blows a plume of steam with a hiss (`SFX - Steam`), even while the pressure is faulty. Three sounds were added at the end of `Tools/make_audio.py` (47 WAV files in all), so every earlier sound regenerates as it was.
- `Mug` sets its own physics limit for pushing apart from another mug (1.5 m/s) when it wakes, because the editor does not save it with the scene.
- Version 0.7.0.

### Bugs the tests caught

Most of the first failures were in the new checks, not the game. A comfort check expected every coffee disc hidden, but a saved partial-progress fixture legitimately fills your cup. The wardrobe check looked for the new pin's announcement too soon: announcements are queued four seconds apart, and the kind scarf comes first. A test expected ten earnable pieces, and there are now eleven. One check read the mugs' physics limit while their room was still hidden, where it always reads 0, which showed that nothing was setting it in the game: that is now set when a mug wakes, and the throwing suite checks it in the live kitchen (1.5 m/s). Twice the gameplay journey failed at its first step (movement input moving the player) on the first run after a scene rebuild or a recompile, and passed on the next run with nothing changed; it is a start-up timing issue in the test that was not tracked down. The mug throws, the shatter, the send under the spout and the cupboards all passed the first time they were run.

### Tests

The gameplay journey has 8 new checks (146 in all): the lever brews and pulls down, the cup placement and three pours (the second with the lever) complete the kitchen, the lever goes back up, the steam wand blows steam, nothing orange has been found yet, a cupboard opens and the orange mug is inside, the mug is taken from the open cupboard, it glides to the spout while the mug already there goes back to the counter and nothing breaks, and brewing into it earns the pin and still counts as nothing more than a little warmth. The menu and seated suite has 14 new checks (71 in all) on how the kitchen is wired: five mugs and their settings, one spout that takes any mug, six doors on their outer edges, the orange mug in the fifth cupboard, the eleventh piece, the lever and steam wand, the shard, dust and splash systems, and effects that share the flat paper material, cast no shadows and never hold more than 300 bits (224). The mirror and wardrobe suite has 7 new checks (63 in all): the pin waits for the hidden mug, opening the cupboard only shows it, brewing into it earns the pin and it is announced after the scarf, and the pin can be worn at the mirror. The throwing suite has 24 new checks (102 in all): a mug picked up says that throwing it will break it, one flicked at the floor flies off the hand and breaks once, bursts into shards and dust, is out of the room for a moment and comes back whole on its shelf; one set down from a hand's height does not break; one let go while pointing at the spout is sent and not thrown; a second mug sent to the spout sends the first one home unbroken; the pour goes into the mug that was sent; and a full mug let go of at carrying height breaks, splashes coffee and comes back empty with no mistake counted. Three new stills are saved: the whole machine, the open orange cupboard and the mirror with the new pin (`Documentation/Previews/`).

### Results

All eight suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 146 |
| Navigation | 131 |
| Airplane-seat checks | 98 |
| Menu and seated checks | 71 |
| Mirror and wardrobe | 63 |
| Ocean, clean mode and night | 88 |
| Throwing, controllers and Quest 2 | 102 |
| Text visibility | 138 signs, 11 plates |

Navigation, the ocean suite and the text visibility suite last ran before one small late change (`Mug` setting its own physics limit), which touches only the kitchen's mugs; the other five suites ran after it. The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 74.5 MB (74,495,119 bytes) |
| SHA-256 of the APK | `c2592395ce9b1a9c06f11c7ce8158a8a55b4d598f5adbd91903ac9af3b80396e` |
| Version | 0.7.0, version code 411989 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.7.0, versionCode 411989, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. The new code and content are in the build: the `Mug` and `CabinetDoor` classes, the three new sounds (`SFX - Shatter`, `SFX - Cabinet`, `SFX - Steam`), the orange mug material, the machine lever and steam wand, the mug breaking effects, and the Orange mug pin. No private-network address and no session data in any file (the only matches for an address pattern are version numbers and encoding identifiers in the .NET libraries, and Meta's own debugger classes appear as code and type names in the SDK, as before). The account name appears once, beside the project name in the player's global settings. 133 KB larger than 0.6.6: the three sounds and the new code. |

Desktop XR start-up was switched off for the test run and switched back on before the build.

## October 14: the ambush

### What changed
- **A seventh chapter, The ambush.** A boardroom, a table nine metres long and you in a chair at the end of it. You arrive in complete darkness; after about four seconds the lights flicker on and two clowns, nearly three times life size, are leaning in across the table. Six feeling cards, each of which makes both clowns smaller and sillier when named; five replies, from confused to furious, that the clowns answer like a company; six pieces of paperwork that can be sent at a clown, tossed or shaken hard to tear (they always come back); six balloons to pop and two noses to honk; and STAND UP, which appears when all six feelings are named, rolls the chair back, turns the room warm and opens a door. It is the seventh room (room 8) and counts toward finishing the journey, so the sunrise jacket and the rooftop ending now wait for all seven chapters (the saved progress number for all seven is 478). A new pin, the Stood up pin, is a twelfth piece to earn. The lobby has a seventh door.
- **Two menu options.** **GENTLE AMBUSH** starts the clowns at 1.9 times life size and upright, like KITCHEN CROWD for the kitchen, and **DARK OPENING** (on by default) can be switched off so the lights are on at arrival. Both are saved. The menu gained a row, so its panel is a little lower (the drop went from 0.20 to 0.16 metres) and its bottom row is still within 42 degrees of straight ahead (all 14 buttons shown in the lobby within 39).
- **Six new sounds** at the end of `Tools/make_audio.py` (53 WAV files in all, every earlier sound regenerating as it was): `Ambience - Boardroom`, `SFX - Lights`, `SFX - Squeak`, `SFX - Balloon`, `SFX - Tear` and `SFX - Stand`.
- The pointer can look through one thing: a `SendTarget` can be marked `seeThrough`, so a clown's chest can be aimed at across the table's screen. The player's own aim code (`VRPlayer.FindTarget`) and its obstruction check honour it.
- A new `Dynamic()` rule in the scene builder keeps everything under the room's moving parts out of the merged meshes, so the clowns, cards, papers, chair and lamps can move.
- Version 0.8.0.

### Bugs the tests caught

The first compile failed on one line that declared three properties together, which C# does not allow, and a duplicate variable name in the gameplay journey. Most of the other first failures were in the new checks, not the game. The checks for popping balloons expected every balloon to be reachable one by one, but balloons float in clusters and the pointer reaches the nearest first (the ambush suite now pops the nearest reachable one each time, and the airplane-seat suite only asks for one reachable balloon in each cluster). A check that a slow wave does not tear a page found that the tear detector measured speed from single-frame jumps, which spike when frames are uneven, so a steady wave could count as a shake: the room now measures the speed over the last tenth of a second. The same check also revealed that the test's stand-in hand moved twice per frame when the editor ticked twice, doubling its speed. One check measured the clowns' lean a fifth of a second after a feeling was named, while they were still flinching from the kind line. The throwing suite counts what can be tossed and had to learn that six ambush papers join the five résumé pages and five mugs. The text visibility suite found eight problem signs in the new room (three wall posters standing behind the wall's columns, the tissue box hiding the paperwork sign, and reply strips on stairs too low to clear one another); they were fixed in the room, not in the test, and the stairs are steeper, the posters hang between the columns and the tissue box moved to the end of the table. The menu check on how low its bottom row sits failed at 47 degrees with the new row, which is why the panel was lowered. Once, Unity froze as Play Mode started, inside Meta's XR operator layer shutting down (nothing in the game was running yet); it needed the editor restarted, and the run was repeated. As on earlier runs, the gameplay journey failed its first check (movement input) on the first run after the scene was rebuilt and passed when run again with nothing changed.

### Tests

The ambush has its own suite with 200 checks (`Documentation/Ambush-validation.txt`): the structure, the dark opening and its flicker, the menu ending the dark, DARK OPENING, the clowns' size, lean and facing, the replies, balloons and noses, the paperwork sent, thrown, dropped, torn and returned, naming six feelings, standing up, the way out, revisiting, TRY AGAIN, GENTLE AMBUSH and saving. The gameplay journey has 20 new checks (166 in all): every chapter's journey now ends with the ambush played with real input and all seven chapters complete. The menu and seated suite has 4 new checks (75 in all), including the dark-opening choice and two named feelings surviving a scene reload. Navigation has 11 more (142), the airplane-seat suite 20 more (118), the wardrobe suite 4 more (67) and the text visibility suite 32 more signs (170 in all).

### Results

All nine suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 166 |
| Navigation | 142 |
| Airplane-seat checks | 118 |
| Menu and seated checks | 75 |
| Mirror and wardrobe | 67 |
| Ocean, clean mode and night | 88 |
| Throwing, controllers and Quest 2 | 102 |
| Text visibility | 170 signs, 11 plates |
| The ambush | 200 |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 75.2 MB (75,212,507 bytes) |
| SHA-256 of the APK | `58455ac70c1a30c0af7aaebc2e51f8cf7475fe478b14c11d24f4d4edd0a9bb7f` |
| Version | 0.8.0, version code 412936 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.8.0, versionCode 412936, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. The new code and content are in the build: the `AmbushRoom` class, the six new sounds (`Ambience - Boardroom`, `SFX - Lights`, `SFX - Squeak`, `SFX - Balloon`, `SFX - Tear`, `SFX - Stand`), the two clowns and their parts, the STAND UP button, the GENTLE AMBUSH and DARK OPENING menu buttons and the Stood up pin. No private-network address and no session data in any file (the only matches for an address pattern are version numbers and encoding identifiers in the .NET libraries, and Meta's own debugger classes appear as code and type names in the SDK, as before). The account name appears once, beside the project name in the player's global settings. 717 KB larger than 0.7.0: the six sounds and the new code. |

Desktop XR start-up was switched off for the test run and switched back on before the build.

## October 17: foveated rendering

### What changed
- **The game now asks every Quest for foveated rendering.** Nothing in 0.8.2 set a foveation level, so the headset ran with none. `DeviceProfile` now asks for one as soon as it knows which headset this is: a ceiling of High on a Quest 2 and Medium on a Quest Pro, Quest 3 and Quest 3S (`DeviceProfile.FoveationFor`), with dynamic foveation on so the headset uses none while the scene is cheap to draw and only reaches for more when the graphics chip runs short of time. An unrecognised headset is left alone.
- **How the request reaches the runtime.** The game sets `OVRManager.foveatedRenderingLevel` and `OVRManager.useDynamicFoveatedRendering`. Under the OpenXR loader, which this project uses, Meta XR SDK 207.0.0 routes both to `MetaXRFoveationFeature` (the `XR_FB_foveation`, `XR_FB_foveation_configuration` and `XR_FB_foveation_vulkan` extensions), checked by reading the SDK's source. The SDK's `HighTop` level is mapped to High by the feature, so the game does not use it. Eye-tracked foveation is Quest Pro only and needs an extra permission, so it stays off, and the manifest is unchanged.
- **Safe on every path.** The request is only made in a headset build (never in the editor or the tests), only when the runtime reports the extensions as enabled, and is repeated every half second for up to 40 checks if the runtime is not ready at start-up. If the runtime never offers it, or fails when asked, the game logs one line and carries on with the picture as it was. After a successful request the game reads the level back and logs it (the `DeviceProfile` lines in the headset's log), so a headset check can see whether the runtime really took it.
- **The scene builder now enforces the feature setting.** It switches Meta's foveation feature on for both Android and desktop and switches Unity's own foveated rendering feature and Meta's eye-tracked one off (two foveation features would compete). The project already had Meta's on; this makes it a rule instead of a coincidence.
- Changed: `DeviceProfile.cs`, `VRPlayer.cs` (it keeps asking until foveation is settled, not only until the headset is known), the scene builder, the throwing suite. Version 0.8.3.

### Tests
The throwing suite gained 15 checks, using stand-ins for the headset runtime so the requests can be read back: the ceilings per headset, the exact request for each of the four headsets, an unrecognised headset asking for nothing, a runtime that is not ready yet being asked again until it takes the request and then not asked twice, a runtime that never offers it being given up on after 40 checks, a runtime that throws being logged and left alone, the real runtime never being reached outside a headset build, putting the profile back putting foveation back, and the project's OpenXR settings for Android having Meta's foveation feature on and Unity's own off. What no test can show is whether the headset's compositor really applies it and how the edges look: that needs a headset, and the README and Playtest ask for it.

### Results

All nine suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 172 |
| Navigation | 142 |
| Airplane-seat checks | 118 |
| Menu and seated checks | 101 |
| Mirror and wardrobe | 67 |
| Ocean, clean mode and night | 88 |
| Throwing, controllers, Quest 2 and foveation | 117 |
| Text visibility | 170 signs, 11 plates |
| The ambush | 200 |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 76.0 MB (76,045,877 bytes) |
| SHA-256 of the APK | `ec679cd0a79f55dd795713e725dc0fce19a352cb49698b6a6d5821125b9422e2` |
| Version | 0.8.3, version code 416414 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.8.3, versionCode 416414, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. The new code is in the build: Meta's foveation feature (`MetaXRFoveationFeature` and the name of its Vulkan extension, `XR_FB_foveation_vulkan`) and the `DeviceProfile` foveation request with its retry limit and its three log messages (what was asked, what the headset reads back, and giving up); the earlier code (`SaveBackup`, `ThumbShortcuts`, `ShowSaved`, the backup file name `AfterHours-save.json`, the "Thumb gesture" log line, and the rooftop's and the ambush's code) is still there. No private-network address and no session data in any file (the only matches for an address pattern are version numbers and encoding identifiers in the .NET libraries and the code metadata; the word session_id appears only inside Meta's and Unity's own native libraries and code metadata, and Meta's own debugger classes appear as code and type names in the SDK and, by name only, in one 308-byte data index). The account name appears once, beside the project name in the player's global settings. Only 2,404 bytes larger than 0.8.2: the new code is small. |

Desktop XR start-up was switched off for the test run and switched back on before the build.


## October 16: background saving and a thumb shortcut

### What changed
- **The game saves by itself between rooms.** A snapshot is taken as you leave a room (as before) and now also as you arrive in the next, and a quiet timer takes one every 45 seconds. Saving builds one JSON snapshot from the movable objects the director already holds for each room (the old code searched every room's hierarchy each time) and writes the preferences only when the snapshot differs from what is stored, so a save with nothing new costs no disk write. After a room change a faint "Saved" shows in the upper right of the view for about a second (`VRPlayer.ShowSaved`); the timed saves show nothing; the cue steps aside for the menu and clean mode never shows it.
- **A second copy of the progress survives differently signed reinstalls.** `SaveBackup` keeps the checkpoint, the chapters finished, the wardrobe and the comfort settings as `AfterHours-save.json` in the app's external files folder (`Android/data/com.afterhours.mindoffice/files`), written only when something changed, through a temporary file. Before the first scene loads, a fresh install with no progress of its own restores from it; it never replaces existing progress and only restores the game's own keys. Android removes the folder on uninstall, so `QuestInstall` now copies the file off the headset before it uninstalls a differently signed copy and pushes it back before the game starts, and says in the Console what it did. Nothing is written or restored in the editor unless a test points the backup at a temporary file.
- **Thumb microgestures take hands back to the lobby.** `ThumbShortcuts` reads `OVRHand.GetMicrogestureType()` on both hands: a thumb swipe back asks "Back to the lobby?", a thumb tap within four seconds goes, anything else stays. It is the hands-only version of the controller's B button, with the extra step so a stray swipe cannot send anyone anywhere. Each gesture it receives is logged on the headset. No manifest change was needed: hand tracking was already requested, and the permissions are still hand tracking and internet only.
- New files: `SaveBackup.cs`, `ThumbShortcuts.cs`. Changed: `CheckpointStore.cs`, `GameDirector.cs`, `VRPlayer.cs`, the scene builder (the cue and the shortcut's component), `QuestInstall.cs`, the test queue's clearprefs job and the menu and seated checks. Version 0.8.2.

### Tests
The menu and seated suite gained 26 checks that cover every behaviour above except what only a headset can show: the backup is written once and holds exactly what is stored; saving twice with nothing changed writes nothing; a change is written once to both; the quiet timer saves after 45 seconds and shows nothing; a change made during a room change is saved on arrival; the cue shows after a room change, is gone within a couple of seconds, and stays away for the menu and for clean mode; a fresh install restores from the backup, never overwrites progress it already has, and ignores a damaged file and a file with foreign keys; the thumb shortcut asks, goes, stays, expires, and does nothing in the lobby, mid-move or on a tap alone. The editor has no hands and no headset, so the gestures are handed to the code, and the pull-and-push the installer does around an uninstall has not been exercised against a real differently signed install. The first run of the menu and seated suite failed one of the new checks because the cue from the previous room change was still showing when the timer test began; the test now waits for it to end. The gameplay journey failed once on a kitchen mug check before that and passed on its rerun with the same code.

### Results

All nine suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 172 |
| Navigation | 142 |
| Airplane-seat checks | 118 |
| Menu and seated checks | 101 |
| Mirror and wardrobe | 67 |
| Ocean, clean mode and night | 88 |
| Throwing, controllers and Quest 2 | 102 |
| Text visibility | 170 signs, 11 plates |
| The ambush | 200 |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 76.0 MB (76,043,473 bytes) |
| SHA-256 of the APK | `13b0cd54feb5e68393d483c408d0747e8647bec1504df0f1f6bfbc80b4d2c5ae` |
| Version | 0.8.2, version code 416362 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.8.2, versionCode 416362, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. The new code is in the build: the `SaveBackup` and `ThumbShortcuts` classes, `VRPlayer.ShowSaved`, the backup file name `AfterHours-save.json`, the "Saved" cue object and the thumb shortcut's question text and its "Thumb gesture" log line; the rooftop's and the ambush's code and content are still there. No private-network address and no session data in any file (the only matches for an address pattern are version numbers and encoding identifiers in the .NET libraries and the code metadata; the word session_id appears only inside Meta's and Unity's own native libraries and code metadata, and Meta's own debugger classes appear as code and type names in the SDK, as before). The account name appears once, beside the project name in the player's global settings. Only 448 bytes larger than 0.8.1: the new code is small. |

Desktop XR start-up was switched off for the test run and switched back on before the build.


## October 16: the sunrise rooftop

### What changed
- **Room for tomorrow was restyled to be bright, saturated, chunky and toy-like.** Sky, sun and light first: a new skybox shader written for the project (`After Hours/Sunrise sky`, no texture) paints a gradient from gold at the horizon through peach and pink to morning blue, a dawn glow along the horizon and a flat glowing sun with a golden rim and two halos. The room sets its own warm sunlight, bright lilac, peach and terracotta ambient colours and a peach haze when the player arrives and puts the day's back when they leave. Then richer scenery: a skyline of four rings of chunky pastel buildings (lit windows, rooftop water towers, aerials), ten drifting clouds and a bobbing hot-air balloon, bunting over the deck, a striped rug, two benches, six potted plants, two deck chairs and a side table, flower boxes, a stairwell hut, a wooden water tower and four beanbags. The layout, the board and its three lines of text are unchanged.
- New files: `RooftopRoom.cs`, `WorldBuilderRooftop.cs` (the old 18-line `Rooftop()` in `WorldBuilder.cs` was replaced) and `SunriseSky.shader`; new materials (a sunrise deck, a dozen toy colours, six skyline colours and window materials, white and pink clouds) in `Art` and the scene builder; the scene builder's `Dynamic()` rule now also keeps the room's moving group (clouds and balloon) out of the merged meshes.
- The room-preview tool captures the rooftop twelve ways in its sunrise look (the arrival, the sun, the skyline each side, the deck, the board, the chairs, the bunting, the sky, the hut, the water tower and the balloon).
- The gameplay journey gained six checks for the rooftop's look (sky, light, clouds, balloon, and the day look restored on leaving).
- Version 0.8.1.

### Problems found while looking at the pictures

None of the tests failed on the new room; every change below came from reading the stills before running them. The first picture of the sun was a plain pale disc that washed out against the glow, so it now has a golden rim and a larger, softer halo. The water towers' domes looked like cakes, so they are cones. The foreground beside the rug was empty, so four beanbags were added (without colliders). The balloon hid behind the second ring of buildings, so it moved. Aerials near the sun looked like lollipops against it, so they are only built away from the sun. The "behind you" still was not useful and was replaced by the hut, the water tower and the balloon. A leftover still from the first pass, `Sunrise - Rooftop - behind you.png`, is still in `Documentation/Previews` (the tool does not delete old pictures). The gameplay journey failed its first check (movement input) on its first run after the scene was rebuilt and passed when run again with nothing changed, as on earlier runs. The first release build still reported version 0.8.0, because the edited builder script had not reached the computer when the build ran (a file-copy slip, found by reading the version in the build log); it was rebuilt after the file was checked on disk, and the version, version code and signature of the final APK were read back out of the file itself.

### Tests

The text visibility suite passed on its first run with the new room (170 signs, as before, since no sign was added): the board's three lines are clear from almost every place, and three notes record a line of the board partly hidden from a few floor places out of about a hundred: the two upper lines from two and four places by the gold door frame of the stairwell hut, and the lowest line from seven by a yellow flower on a plant. The gameplay journey has 6 new checks (172 in all). Every other suite has the same number of checks as in 0.8.0, and all passed.

### Results

All nine suites pass in desktop Play Mode:

| Suite | Checks |
|---|---|
| Gameplay journey | 172 |
| Navigation | 142 |
| Airplane-seat checks | 118 |
| Menu and seated checks | 75 |
| Mirror and wardrobe | 67 |
| Ocean, clean mode and night | 88 |
| Throwing, controllers and Quest 2 | 102 |
| Text visibility | 170 signs, 11 plates |
| The ambush | 200 |

The release APK built with zero errors.

| | |
|---|---|
| Output | `Builds/Quest/AfterHours-release.apk`, 76.0 MB (76,043,025 bytes) |
| SHA-256 of the APK | `af2321c2c3a2c4a844b8c615a672992b093486834303d9a20765d744c08a9c6d` |
| Version | 0.8.1, version code 415478 |
| Signing | APK Signature Scheme v2, the same release key (`CN=After Hours, O=After Hours Studio`, SHA-256 `33:A8:91:83:…:5E:CF:E3`), checked by reading the certificate out of the signing block. Not debuggable. |
| Package | `com.afterhours.mindoffice`, minSdk 32, targetSdk 34 |
| Devices | Quest 2, Quest Pro, Quest 3, Quest 3S and Stanley, as before; hand tracking stays optional |
| Permissions | Hand tracking and internet only, unchanged |
| Checks | Manifest read from the APK: versionName 0.8.1, versionCode 415478, minSdk 32, targetSdk 34, not debuggable, hand tracking and internet permissions only. The new code and content are in the build: the `RooftopRoom` class, the `After Hours/Sunrise sky` material and shader, the toy, skyline and cloud materials, and the beanbags, stairwell hut and water tower; the ambush's code and content are still there. No private-network address and no session data in any file (the only matches for an address pattern are version numbers and encoding identifiers in the .NET libraries; the word session_id appears only inside Meta's and Unity's own native libraries and code metadata, and Meta's own debugger classes appear as code and type names in the SDK, as before). The account name appears once, beside the project name in the player's global settings. 830 KB larger than 0.8.0: the new sky shader and the rooftop's meshes and materials. |

Desktop XR start-up was switched off for the test run and switched back on before the build.

## Still unverified

These still need a physical headset playtest (see `Playtest.md`):

- frame rate at render scale 1.0
- thermals
- text readability on the headset, including the menu labels (the text visibility suite proves no sign is hidden behind something in the editor; it cannot say how sharp small lettering looks in the lenses)
- seated reach on a real chair (the airplane-seat suite checks it in the editor)
- hands-only play from start to finish
- sound levels on the headset's speakers, including the new waves and surf
- whether the moving water and the wave sound feel in step on the headset, and how the moving water looks and feels in the lenses (the editor run had no sound playing)
- how the paper looks and feels in the headset: the flurry of thin bits (flicker or shimmer in the lenses), how thick the scraps look on the floor, whether you can see a set pop in, and the frame rate with all five sets on screen (the editor only counts them)
- how the confetti looks and feels in the headset: whether the thin flat bits flicker or shimmer in the lenses, whether the burst and the shower read well, how loud the pop is on the headset's speakers, the frame rate during a burst (about 400 bits at once), and whether a real hand toss reliably counts as a throw (the editor test simulates the hand's speed)
- how the box blast looks and feels in the headset: whether the flash (a cream ball up to about 0.6 m across for a fifth of a second) and the smoke puffs are comfortable so close to the face, whether the stretched sparks and the thin chips flicker in the lenses, how loud the boom is on the headset's speakers, the frame rate during a blast (about 190 bits at once), and whether a real two-handed slam reliably bursts the boxes while a gentle tap or two boxes carried side by side do not (the editor test drives stand-in hands at set speeds; tracked hands and controllers jitter, so 2.4 m/s may turn out too easy or too hard)
- how the new kitchen looks and feels in the headset: whether throwing a mug feels like a throw and a shatter is satisfying rather than startling (the crash is a sharp sound close to the head), whether the shards, the dust and the steam plume are comfortable and legible in the lenses, the frame rate while they play (at most 224 bits at once) and in the kitchen generally with five physics mugs, whether the mugs and the cupboard doors are easy to point at and open from a seat, whether the orange mug can be seen on its shelf from a seated eye height, and how the Orange mug pin reads on the avatar's chest
- how the ambush looks and feels in the headset: whether the dark opening and its flicker (six flashes in about a second) are comfortable, and whether the lights coming on so close to the eyes after four seconds of black are too sudden; whether two clowns at 2.8 times life size read as towering and absurd rather than frightening from a seated eye height (GENTLE AMBUSH starts them at 1.9), how the lean and sway look in the lenses, whether the cards, the replies on their staircase and the two lines on the screen are easy to read and point at from a seat, whether a page flying across the table to a clown and a real hand's hard shake that tears one feel like the player's own, how the balloons, noses, paper bits and the new sounds (lights, balloon, tear, stand) sound and look at that distance, and the frame rate in a large room with two big clowns, balloons and particles
- how the sunrise rooftop looks and runs in the headset: whether the sky shader renders correctly in both eyes on a Quest (it was written to the same stereo rules as the comfort vignette and checked only on the desktop), whether its gradient and sun show colour steps or shimmer in the lenses, whether the brighter, more saturated look and the sun low in the sky are comfortable, whether the board's lines read well against the warm sky and pastel buildings, and the frame rate with four rings of buildings, the bunting, the clouds and the balloon (the far buildings cast no shadows and use their own materials, but no frame-rate measurement has been made on a headset)
- the left palm pinch that opens the menu
- whether foveated rendering shows: blocky or shimmering edges, signs and plates reading as well away from the middle of the view, and a steadier frame rate (the `DeviceProfile` lines in the headset's log say what level was asked for and what the headset reported back)
- whether the Quest reports thumb microgestures to the game, how reliably a thumb swipe back and a thumb tap are recognised from a seat, and whether a stray swipe ever asks about the lobby by accident (the editor has no hands; the headset's log shows a "Thumb gesture" line for each one received)
- whether the backup copy of the save is written where the installer looks for it (`Android/data/com.afterhours.mindoffice/files/AfterHours-save.json`) and whether the installer's pull and push really carry it across a differently signed reinstall on a real Quest
- how the faint "Saved" cue in the upper right of the view reads in the lenses
- how the comfort vignette feels
- switching between hands and controllers
- the mirror reflection with real head and hand tracking, and how the avatar's arms look
- sending pages into the shredder by hand on the headset
- how swinging the bat feels with real hands and with controllers, including short swings from a seat
- whether the hit pulse is felt on the controllers, and how loud the rage room's breaking sounds are
- that a swing never reaches past the desk into the real room around a seated player
- frame rate with about 210 loose rigid bodies in the rage room at once
- how knocked pieces feel to bat around
- how the mirror's X-ray looks on the headset: the glow of the bones, how dark the backdrop reads, and whether the skeleton keeps up with real hands
- how throwing a box feels, and whether the pull toward the truck is right, with real hands and with controllers
- the trigger picking things up with controllers, and the wording switching between hands and controllers on a real headset
- everything on a Quest 2: that it installs and runs, frame rate at render scale 0.9 and 72 Hz, hand tracking, and controllers
- how rowing with the oars feels with real hands and controllers, including the direction of the push stroke and whether the grip zone is easy to find
- whether the taller menu (two more buttons) reads well and is reachable from a seat
- how the lighthouse beam looks on the headset (soft edges, no banding) and what it costs in frame rate
- the level of the new sea sounds, gulls and bell buoy on the headset's speakers
- that clean mode really is calm for someone who wants nothing on screen, and that nothing needed was hidden by it
