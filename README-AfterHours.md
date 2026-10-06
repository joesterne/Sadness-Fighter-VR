# After Hours

A Meta Quest game about finding room for yourself after losing a job.

Open `Assets/AfterHours/Scenes/AfterHours.unity` in Unity 6000.6.3f1 and press Play. The `After Hours` menu can rebuild the scene or build a Quest APK. The original sample scene is retained.

![The ocean of shame](<Documentation/Previews/LowPoly - Ocean of shame.png>)

## What is playable

- **The lobby**: choose a chapter in any order. Detailed office memories sit behind glass. The surrounding spaces use a midnight/teal palette with restrained coral and brass accents.
- **Ocean of shame**: fourteen paddle strokes bring a lighthouse and welcoming shore closer. Hold either paddle and pull toward yourself, or select the accessible ROW button. Short fades move the scenery while keeping the boat and player stable.
- **Heavy things**: carry five named feelings—anger, fear, relief, grief, uncertainty—to a truck. Release each box in the marked processing bay. Every feeling is accepted; processing is not disposal.
- **A small spill**: the coffee machine leaks and additional people watch with each mistake, up to eight. ASK FOR HELP is available immediately, releases the pressure fault, and dismisses the audience. Place your cup under the spout and brew three small pours. **KITCHEN CROWD** in the menu hides the crowd without changing the task.
- **The infinite archive**: sort six notes into FACT, FEAR, and STILL TRUE. Repeated filing cabinets fade into the distance. Incorrect placements invite another try, without penalties.
- **Room for tomorrow**: a rooftop for rest, available from the beginning. Completing all four chapters changes the ending to “You are more than your job.”

Completed chapters and partial progress persist locally between launches. Every paddle stroke, delivered box, sorted note, and coffee step saves automatically. Loose objects return to their starting positions when the app reopens; accepted items stay processed. CONTINUE in the lobby returns to the last room. **TRY AGAIN** in the menu resets the current puzzle without deleting chapter completion. Nothing is timed, and there is no minimum session length.

## The menu

Press the left controller's **menu button**, or with tracked hands look at your left palm and pinch, to open the menu. Press it again, or select **CLOSE**, to put it away. Desktop preview uses Tab. The menu opens at arm's length, a little below eye level, wherever you are facing, and it moves and turns with you. It closes on its own when you change rooms.

| Row | Buttons |
|---|---|
| Turning | **< TURN**, **TURN AROUND**, **TURN >** |
| Seated play | **SEATED VIEW**, **SEATED HEIGHT**, **MOVEMENT** |
| Comfort | **COMFORT VIGNETTE**, **KITCHEN CROWD**, **FACE FORWARD** |
| Room | **TRY AGAIN** (in the four chapters), **CLOSE**, **LOBBY** (outside the lobby) |

All the settings used to be signs in the lobby, and each chapter had LOBBY and TRY AGAIN signs. Those signs are gone. The lobby keeps its doors and CONTINUE, and every chapter keeps its own task buttons (ROW, BREW, ASK FOR HELP).

## Seated play and short sessions

Select **SEATED VIEW** in the menu to calibrate a seated view. **SEATED HEIGHT** cycles between 1.45, 1.65 and 1.85 metres (from standing, the first press switches seated view on). This raises the view and tracked hands together, and saves the chosen default for the next launch. Switch seated view off and on again to recalibrate after changing chairs or posture. Standing view restores the headset's tracked height.

Movement is set up so you never have to twist in the chair:

- **Snap turns.** The right stick turns 30 degrees. Pull it straight back to turn around. With walking off (**MOVEMENT: TELEPORT ONLY**), and in the boat, the left stick turns too. With tracked hands, use the turn buttons in the menu.
- **Teleports turn you.** In seated view, a floor teleport turns you to face the way you pointed, so you can turn by teleporting.
- **FACE FORWARD** turns the room so its main view is straight ahead again, wherever your chair points.
- **Comfort vignette.** The edges of your view soften while you walk with the stick, and clear when you stop. **COMFORT VIGNETTE** switches it off. The setting is saved.

Objects can be picked up from a distance, the boat has a ROW button, and floor teleport avoids physical walking. The experience supports sitting, but reach and comfort still need checking on a physical headset.

For a short visit, row a few strokes, deliver one box, sort one note, or make one coffee step, then select LOBBY in the menu or close the app. Resume later without repeating those completed steps.

## Controls

| Action | Quest controllers | Tracked hands |
|---|---|---|
| Point and select | Aim and press index trigger | Aim the hand ray and pinch index + thumb |
| Carry objects | Grip near an object, or aim and grip within 3.5 m | Pinch near an object, or aim and pinch within 3.5 m |
| Place | Release grip | Release pinch |
| Teleport | Point at clear floor, hold trigger, release | Point at clear floor, pinch, release |
| Walk | Left joystick; MOVEMENT in the menu turns it off | Use teleport |
| Turn | Right joystick, 30-degree snap turns; pull back to turn around. Left joystick too when walking is off | Turn buttons in the menu, or turn physically |
| Open or close the menu | Left menu button | Look at your left palm and pinch |
| Return to lobby | B/Y, or LOBBY in the menu | LOBBY in the menu |
| Row | Grip paddle and pull, or select ROW | Pinch paddle and pull, or select ROW |

Desktop preview: WASD movement, right mouse drag to look, Q/E to turn, left click to select, hold left mouse to carry, click/release on floor to teleport, Tab for the menu, Escape to lobby. A carry point in front of the camera makes object placement possible without a headset.

For desktop preview without an XR runtime, turn off **Initialize XR on Startup** in the Standalone XR Plug-in Management settings. Keep it enabled for Android. The scene builder restores XR startup for both platforms. When using the Meta XR Simulator, activate it through its Window menu before Play Mode.

## Validation

**After Hours → Tests** runs each suite in desktop Play Mode, using real mouse and keyboard input, raycasts and collision triggers. Keep the mouse pointer off the Game view while a suite runs: the editor's own mouse would otherwise take over from the test's. All three suites passed on October 5, after the menu change:

- **Gameplay journey**, 53 checks: every chapter from start to finish, with returns to the lobby through the menu. Results are in `Documentation/Gameplay-validation.txt`.
- **Menu and seated checks**, 42 checks, including an actual scene reload: every menu button by pointer, seated height cycling, snap turns and turning around with the stick, FACE FORWARD, the walking vignette, the menu fitting in front of a nearby wall, saved preferences, partial progress in every chapter, CONTINUE, and TRY AGAIN. Results are in `Documentation/Seated-session-validation.txt`.
- **Navigation**, 94 checks: every chapter and its menu return, walking and teleporting through doorways, safe spawns, fade cleanup, a button press that can't turn into a teleport, carrying an object during travel, held input, invalid destinations, competing requests, and leaving the ocean during its final stroke. Results are in `Documentation/Navigation-validation.txt`.

Earlier Meta XR Simulator checks verified controller teleport, joystick movement, trigger selection, hand pinch selection, and hand grabbing, carrying, and release. See `Documentation/XR-validation.md` for their scope. They predate the menu.

A physical Quest playtest is still needed for comfort, frame rate, seated reach, the left-palm menu gesture, and hand/controller switching. The simulator does not measure headset performance.

## Quest build

There are two build commands in the **After Hours** menu:

- **Build shareable Quest APK (release)** writes `Builds/Quest/AfterHours-release.apk`. Give this one to playtesters. It leaves out Meta's XR Operator test layer and the Immersive Debugger, and it clears the DevAgent's network address and access token from the build.
- **Build Quest APK (development)** writes `Builds/Quest/AfterHours.apk`. It includes Meta's test tooling for Meta XR Simulator and agent-driven testing, so keep it on your own headset. In this build, clicking the left thumbstick toggles the Immersive Debugger. The left menu button stays reserved for the game's menu, and B/Y for returning to the lobby.

To play on your own headset, connect the Quest with a USB data cable, turn on Developer Mode, and accept **Allow USB debugging** in the headset. Then use **After Hours → Install release APK on connected Quest** (or the development equivalent). It installs the APK with Unity's bundled `adb`, starts the game, and reports each step in the Console. If the headset hasn't been authorized, more than one device is connected, or the APK doesn't exist yet, the Console says so.

**After Hours → Commit and push to GitHub…** commits everything git doesn't ignore and pushes the current branch to `origin`. The shareable release APK is committed too, through Git LFS, so playtesters can download `Builds/Quest/AfterHours-release.apk` from GitHub. Development APKs, `Library`, removed packages and the Immersive Debugger's per-computer settings (`Assets/Resources/DevAgentSettings.asset`, which holds this computer's network address and an access token) stay out of the repository. The push stops if GitHub has commits this computer doesn't, and the first push may open Git Credential Manager's GitHub sign-in window.

Both build commands build from the Android platform settings. If a custom build profile such as Unity's **Meta Quest** profile is active, they switch back to the platform profile first, because a custom profile carries its own player, quality and XR settings.

Both commands apply the Quest settings first: OpenXR with Meta XR, controllers and hands, ARM64, IL2CPP, Vulkan, linear color, 4x MSAA, render scale 1.0, 28 m shadow distance, and Android API 32 minimum. Mixed-reality features stay off, so the APK requests no scene, anchor, passthrough or headset-camera permissions. Both APKs are signed with Unity's debug key, which is fine for sideloading; a store submission would need a release keystore. The game is a prototype, not a store submission. The project doesn't include the Meta Voice SDK, Meta XR Audio or Unity IAP packages. The game doesn't use them, and the Voice SDK's build step crashed the Android build. Only the current room is active. Static geometry is combined by material. Anything the player can pick up, or that moves or switches on and off, keeps its own mesh, and the scene builder logs an error if a movable object is ever merged.

## Art style

The game uses a refined low-poly look:

- Every box has a small bevel. The bevel stays the same size in metres however the box is scaled, so edges catch the light the same way on a keyboard key and on a wall.
- Cylinders, spheres and capsules are faceted, and every facet is flat shaded.
- Materials are matte flat colours in the midnight, teal, coral and brass palette. There are no photographic textures.
- Floors are laid as bevelled tiles, walls as panels and ceilings as coffers.
- Gradient ambient light makes up-facing facets lighter and down-facing ones darker.

Tiles and panels don't cast their own shadows. One invisible, square-edged slab per surface casts the shadow instead, so no light leaks through the seams. Floors cast no shadows.

**After Hours → Capture room previews** renders a still from each room's arrival point into `Documentation/Previews/`, plus the menu in the lobby and the kitchen and the walking vignette, so art and comfort changes can be checked without a headset.

## More rooms to add

1. **The elevator of comparison**: every floor appears to be someone else's promotion. Choose a destination using your own values instead of chasing the highest number.
2. **The meeting that never ends**: chairs repeat imagined criticisms. Move one chair into daylight and replace guesses with things you actually know.
3. **The server room of rumination**: replaying conversations power an increasingly noisy machine. Unplug one loop at a time and route attention to an ordinary present-tense task.
4. **The lost-and-found of identity**: find parts of yourself that never appeared on a job description—friend, maker, parent, neighbor, learner—and arrange them on an empty name badge.
5. **The greenhouse of small beginnings**: plant modest intentions. Growth depends on returning gently, not speed, streaks, or productivity.
6. **The interview mirror**: practice telling your story while the reflection changes from an idealized employee into an ordinary, complete person.

## Editing and asset provenance

All game geometry, wording, and musical acknowledgement tones were created for this project. The rig and hand rendering use the installed Meta XR Core SDK. Text uses the project's existing Liberation Sans TMP asset. Third-party packages retain their own licenses.

Game scripts are in `Assets/AfterHours/Scripts`; the deterministic scene builder is in `Assets/AfterHours/Editor`. The embedded Unity OpenXR Meta package contains a narrow compatibility fix for the editor's EntityId API. See `Documentation/Compatibility.md`.
