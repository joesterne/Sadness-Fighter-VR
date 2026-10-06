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

## Still unverified

These still need a physical headset playtest (see `Playtest.md`):

- frame rate at render scale 1.0
- thermals
- text readability, including the menu labels
- seated reach
- the left palm pinch that opens the menu
- how the comfort vignette feels
- switching between hands and controllers
