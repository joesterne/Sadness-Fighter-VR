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

## Still unverified

These still need a physical headset playtest (see `Playtest.md`):

- frame rate at render scale 1.0
- thermals
- text readability, including the menu labels
- seated reach on a real chair (the airplane-seat suite checks it in the editor)
- hands-only play from start to finish
- sound levels on the headset's speakers
- the left palm pinch that opens the menu
- how the comfort vignette feels
- switching between hands and controllers
- the mirror reflection with real head and hand tracking, and how the avatar's arms look
- sending pages into the shredder by hand on the headset
