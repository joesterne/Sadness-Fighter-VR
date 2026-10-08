# After Hours

A Meta Quest game about finding room for yourself after losing a job.

Open `Assets/AfterHours/Scenes/AfterHours.unity` in Unity 6000.6.3f1 and press Play. The `After Hours` menu can rebuild the scene or build a Quest APK. The original sample scene is retained.

![The ocean of shame](<Documentation/Previews/LowPoly - Ocean of shame.png>)

## What is playable

- **The lobby**: choose a chapter in any order. Detailed office memories sit behind glass. The surrounding spaces use a midnight/teal palette with restrained coral and brass accents.
- **Ocean of shame**: fourteen paddle strokes bring a lighthouse and welcoming shore closer. Hold either paddle and pull toward yourself, or select the accessible ROW button. Short fades move the scenery while keeping the boat and player stable.
- **Heavy things**: five named feelings—anger, fear, relief, grief, uncertainty—wait in front of you. Pinch a box to pick it up, point at the truck, and let go: the box glides into the processing bay. Every feeling is accepted; processing is not disposal.
- **A small spill**: the coffee machine leaks and additional people watch with each mistake, up to eight. A low murmur grows with the crowd. ASK FOR HELP is available immediately, releases the pressure fault, dismisses the audience and quiets the room. Pick up your cup, point under the spout and let go, then brew three small pours. **KITCHEN CROWD** in the menu hides the crowd without changing the task.
- **The infinite archive**: sort six notes into FACT, FEAR, and STILL TRUE: pick one up, point at its tray and let go. Repeated filing cabinets fade into the distance. A note sent to the wrong tray drifts back to the desk for another try, without penalties.
- **The old résumé**: five old résumé pages stand on a desk: a job title, "always available", "exceeded every target", ten years at one company, and a reason for leaving left blank. Pick one up, point at the shredder and let it go. Each page is drawn into the slot, the paper in the bin grows, and a line answers it ("Your worth was never a quarterly number."). When the last page is gone, a blank page asks you to keep one line that is still true about you: *I learn fast*, *I care about people*, *I keep going*, or *I make things better*. The line you choose goes on the mirror in the lobby.
- **The rage room**: the old work computer sits on a desk in a bare room with plywood on the wall: monitor, keyboard, tower and mouse, the inbox still open ("Your access has been revoked"). Point at the bat beside the desk and pinch: it comes to your hand and stays there until you put it back. Swing at the computer. A short flick of the forearm counts as a full hit, so the whole room plays from a seat, and a slow push through a part does nothing. Each part breaks in stages, with its own sound, a jolt and flying pieces: the screen cracks and argues back ("ARE YOU SURE? Unsaved feelings will be lost."), then goes dark and falls; keys fly and the keyboard snaps in two; the tower dents, loses its side panel and fan in sparks, and topples; the mouse is flattened, then split. A line answers each part as it goes. When everything is broken, the room's hum fades to silence, a panel says it is allowed to be angry about losing something that mattered, and the chapter completes. **WHEEL IN A NEW ONE** brings back a whole computer to go again, and **PUT THE BAT BACK** returns the bat to its rack.
- **Room for tomorrow**: a rooftop for rest, available from the beginning. Completing all six chapters changes the ending to “You are more than your job.”

Completed chapters and partial progress persist locally between launches. Every paddle stroke, delivered box, sorted note, coffee step, shredded page and broken computer part saves automatically. Loose objects return to their starting positions when the app reopens; accepted items stay processed. CONTINUE in the lobby returns to the last room. **TRY AGAIN** in the menu resets the current puzzle without deleting chapter completion. Nothing is timed, and there is no minimum session length.

## First visit

The first time the game opens, a small card in the lobby shows a content note, then teaches the two things you need by having you do them once:

1. **Before you begin**: After Hours is about losing a job: the shame, the worry, and what is still true. It is a gentle experience, not a substitute for professional support, and you can take a break whenever you like.
2. **Point and pinch**: select the card's button.
3. **Your menu**: open the menu (left palm pinch, or the controller's menu button). The card moves on by itself once the menu opens.
4. **Begin anywhere**: choose a door, or CONTINUE. Small steps also earn things to wear at the mirror.

SKIP closes the card at any step, and walking through a door counts as finishing it. It doesn't appear again.

## The mirror and your wardrobe

A full-length mirror is built into the lobby's left wall. **MIRROR** on the desk (under CONTINUE) brings you to the spot in front of it, facing the glass, so a seated player never has to walk there. The sign also counts any new pieces waiting for you.

Your reflection is a faceted figure in the game's style. It copies your head, and your hands or controllers, mirrored as a real mirror would: lean in and it leans in, raise your right hand and it raises the hand on your right. With no hands tracked, its arms rest at its sides. It is drawn without a second camera: the room behind the glass is a mirror-image copy of this end of the lobby, and the figure's parent is flipped across the glass. Only the lobby is ever active, so it costs nothing in the other rooms.

The wardrobe is on both sides of the glass. Each row has **<** and **>**:

| You | What you wear |
|---|---|
| SKIN TONE (six tones) | TOP |
| HAIR (short, curls, long, bun, shaved) | HAT |
| HAIR COLOUR (black, brown, auburn, blond, silver) | NECK |
| BUILD (narrow, medium, broad) | PIN |

The outfit rows only offer pieces you have. Everyone starts with a plain tee, a work shirt, and nothing on their head, neck or chest. Ten more pieces are earned by playing:

| Piece | Where | How to earn it |
|---|---|---|
| Harbour jumper | Top | Reach the shore in Ocean of shame |
| Mover's cap | Hat | Finish Heavy things |
| Warm cardigan | Top | Finish A small spill |
| Still true pin | Pin | Finish The infinite archive |
| Your own lanyard | Neck | Finish The old résumé |
| Let it out pin | Pin | Finish The rage room |
| Sunrise jacket | Top | Finish all six chapters |
| Small step pin | Pin | Take one small step anywhere: a paddle stroke, a box, a note, a pour, a page or a broken part |
| Kind scarf | Neck | Ask for help in the kitchen |
| Evening beanie | Hat | Rest on the rooftop for 20 seconds |

Nothing can be missed for good, and nothing is taken away. A new piece is announced with a chime a few seconds after whatever the room has just said. The mirror marks it NEW until you try it on, and the left board lists up to four pieces still to find with how to find them. The wardrobe saves on its own (`AfterHours.Wardrobe.v1`), so TRY AGAIN never removes a piece. Progress from before the wardrobe existed still earns its pieces at the next launch.

## The menu

Press the left controller's **menu button**, or with tracked hands look at your left palm and pinch, to open the menu. Press it again, or select **CLOSE**, to put it away. Desktop preview uses Tab. The menu opens at arm's length, a little below eye level, wherever you are facing, and it moves and turns with you. It closes on its own when you change rooms.

| Row | Buttons |
|---|---|
| Turning | **< TURN**, **TURN AROUND**, **TURN >** |
| Seated play | **SEATED VIEW**, **SEATED HEIGHT**, **MOVEMENT** |
| Comfort | **COMFORT VIGNETTE**, **KITCHEN CROWD**, **FACE FORWARD** |
| Room | **TRY AGAIN** (in each chapter), **CLOSE**, **LOBBY** (outside the lobby) |

All the settings used to be signs in the lobby, and each chapter had LOBBY and TRY AGAIN signs. Those signs are gone. The lobby keeps its doors and CONTINUE, and every chapter keeps its own task buttons (ROW, BREW, ASK FOR HELP).

## Seated play and short sessions

Select **SEATED VIEW** in the menu to calibrate a seated view. **SEATED HEIGHT** cycles between 1.45, 1.65 and 1.85 metres (from standing, the first press switches seated view on). This raises the view and tracked hands together, and saves the chosen default for the next launch. Switch seated view off and on again to recalibrate after changing chairs or posture. Standing view restores the headset's tracked height.

Movement is set up so you never have to twist in the chair:

- **Snap turns.** The right stick turns 30 degrees. Pull it straight back to turn around. With walking off (**MOVEMENT: TELEPORT ONLY**), and in the boat, the left stick turns too. With tracked hands, use the turn buttons in the menu.
- **Teleports turn you.** In seated view, a floor teleport turns you to face the way you pointed, so you can turn by teleporting.
- **FACE FORWARD** turns the room so its main view is straight ahead again, wherever your chair points.
- **Comfort vignette.** The edges of your view soften while you walk with the stick, and clear when you stop. **COMFORT VIGNETTE** switches it off. The setting is saved.

**Every chapter can be finished from the seat you arrive in, within arm's reach.** Nothing has to be walked across a room:

- **Pick things up from where you sit.** Point at an object up to 10 m away and pinch (or grip). It glides to your hand. The pointer leans toward the nearest object within a few degrees, so a shaky hand ray still finds it.
- **Send things where they belong.** While holding something, point at its destination and let go. The destination lights up while you point at it (the truck's bay, the spout, each archive tray), and the object glides there in a short arc. A note sent to the wrong tray drifts back to the desk.
- **The boat rows with a button**: ROW does the same as a paddle stroke.
- **The kitchen crowd stays in front of you.** Watchers gather on both sides of the coffee machine, within 60 degrees of it, so a seated player sees them without turning round.
- **The shredder is in plain view.** It stands beyond the résumé rack, tall enough that its slot shows over the pages from the seat.
- **The mirror comes to you.** MIRROR on the desk teleports you to the glass, and every wardrobe button can be pointed at from there.
- **The rage room is sized for a seat.** The computer is on a desk just in front of you: every part is within 70 cm of the right hand of a seated player, well inside the bat's 85 cm, and the monitor and keyboard are within reach of either hand. Swing speed is measured at the bat's tip and smoothed over a few frames, so a quick flick of the forearm counts as a full hit while tracking jitter and slow movements never do. Strength only changes how loud the hit sounds. The bat stays in your hand when the pinch opens, and through a moment of lost tracking during a fast swing; PUT THE BAT BACK, or leaving the room, returns it to its rack.

The **airplane-seat test** (below) checks this: it completes every chapter from its arrival point without moving the player once. Comfort and readability still need checking on a physical headset.

For a short visit, row a few strokes, deliver one box, sort one note, or make one coffee step, then select LOBBY in the menu or close the app. Resume later without repeating those completed steps.

## Controls

| Action | Quest controllers | Tracked hands |
|---|---|---|
| Point and select | Aim and press index trigger | Aim the hand ray and pinch index + thumb |
| Pick up | Grip near an object, or aim and grip within 10 m | Pinch near an object, or aim and pinch within 10 m |
| Send or place | Point at the lit destination and release grip; or carry it there and release | Point at the lit destination and release pinch; or carry it there and release |
| Teleport | Point at clear floor, hold trigger, release | Point at clear floor, pinch, release |
| Walk | Left joystick; MOVEMENT in the menu turns it off | Use teleport |
| Turn | Right joystick, 30-degree snap turns; pull back to turn around. Left joystick too when walking is off | Turn buttons in the menu, or turn physically |
| Open or close the menu | Left menu button | Look at your left palm and pinch |
| Return to lobby | B/Y, or LOBBY in the menu | LOBBY in the menu |
| Row | Grip paddle and pull, or select ROW | Pinch paddle and pull, or select ROW |
| Swing the bat | Aim at the bat and grip; it stays in your hand. Swing the controller: a short flick counts | Aim at the bat and pinch; it stays in your hand. Swing your hand: a short flick counts |
| Put the bat down | PUT THE BAT BACK, or leave the room | PUT THE BAT BACK, or leave the room |
| Try things on | MIRROR on the desk, then < and > beside the glass | MIRROR on the desk, then < and > beside the glass |

Desktop preview: WASD movement, right mouse drag to look, Q/E to turn, left click to select, hold left mouse to carry, click/release on floor to teleport, Tab for the menu, Escape to lobby. A carry point in front of the camera makes object placement possible without a headset. On desktop the bat is held low and to the right of the view, so a quick right-mouse drag down swings it.

For desktop preview without an XR runtime, turn off **Initialize XR on Startup** in the Standalone XR Plug-in Management settings. Keep it enabled for Android. The scene builder restores XR startup for both platforms. When using the Meta XR Simulator, activate it through its Window menu before Play Mode.

## Sound

Every room has its own sound, all synthesised for this project by `Tools/make_audio.py` (numpy and scipy, no samples, no third-party audio). Run `python make_audio.py <folder>` to regenerate the 30 WAV files in `Assets/AfterHours/Audio`. New sounds are always added at the end of the list, so regenerating leaves every earlier file exactly as it was.

- **Ambience**: a low room tone in the lobby with a slow pad-and-bell theme, swells and foam on the ocean, a deep rumble with distant metal clanks in the warehouse, room tone and a fridge hum in the kitchen, a hush with occasional paper rustles in the archive, fluorescent hum and rain on the window in the résumé room, a buzzing tube light and a ventilation duct in the rage room, and gusting wind over a distant city on the rooftop, where the theme returns. Ambience and music loop seamlessly.
- **The kitchen murmur** rises with each watcher and falls silent when you ask for help.
- **Feedback**: select, grab, send, land, menu open and close, teleport, snap turn, paddle stroke, coffee spill and pour, the shredder's motor and cut paper, a short chime for a new piece to wear, and in the rage room the bat's swish, plastic, glass and metal hits, a final smash when a part breaks apart, a cart wheeling in a new computer, and a soft chord when the room goes quiet. Controllers buzz on each bat hit, harder for a harder swing.
- Changing rooms fades the sound out and back in with the picture.

The builder imports loops as compressed Vorbis kept in memory and short effects decompressed on load, all mono. All of it adds about 2 MB to the APK.

## Validation

**After Hours → Tests** runs each suite in desktop Play Mode, using real mouse and keyboard input, raycasts and collision triggers. Keep the mouse pointer off the Game view while a suite runs: the editor's own mouse would otherwise take over from the test's. The gameplay journey changes the game's state as it plays, so run it first after entering Play Mode; the other four suites reload the scene themselves. All five suites passed on October 8, after the rage room was added:

- **Airplane-seat checks**, 92 checks: the first-visit guide step by step, then all six chapters completed from their arrival points using only pointer presses and the menu key, then the mirror. The player never moves. It checks every pickup and send from the seat, that each destination lights up when pointed at, that a wrong-tray note returns, that the cup lands upright under the spout, the kitchen crowd and murmur, every résumé shredded and a true line chosen, every part of the rage room's computer within 0.65 m of a seated hand, the bat picked up from the seat and every part broken by short swings, MIRROR on the desk, and that all 16 wardrobe buttons can be reached from the mirror. Results are in `Documentation/Seated-reach-validation.txt`.
- **Mirror and wardrobe checks**, 41 checks: the reflection's head lands exactly where a mirror would show yours, turns the mirrored way and comes a metre closer when you step half a metre closer; every wardrobe row by pointer; each piece earned by the real event that earns it (a first paddle stroke, asking for help, each chapter, choosing a true line, the quiet after the rage room, all six chapters, 20 seconds on the rooftop) and announced; trying pieces on; and everything surviving TRY AGAIN and a scene reload. Results are in `Documentation/Wardrobe-validation.txt`.
- **Gameplay journey**, 95 checks: every chapter from start to finish, including all five résumés sent into the shredder and a true line chosen, and the rage room played with real desktop input: the bat picked up and kept in hand, a slow push through the monitor that does not count, every part broken by short swings (a swing at the monitor or tower often breaks the keyboard or mouse beside it too), the quiet and completion, WHEEL IN A NEW ONE, and PUT THE BAT BACK. Results are in `Documentation/Gameplay-validation.txt`.
- **Menu and seated checks**, 46 checks, including an actual scene reload: every menu button by pointer, seated height cycling, snap turns and turning around with the stick, FACE FORWARD, the walking vignette, the menu fitting in front of a nearby wall, saved preferences, partial progress in all six chapters (a broken mouse stays broken after the reload), CONTINUE, and TRY AGAIN. Results are in `Documentation/Seated-session-validation.txt`.
- **Navigation**, 126 checks: every chapter and its menu return, walking and teleporting through doorways, safe spawns, fade cleanup, a button press that can't turn into a teleport, carrying an object during travel, the bat staying in hand and going back to its rack when you leave, held input, invalid destinations, competing requests, and leaving the ocean during its final stroke. Results are in `Documentation/Navigation-validation.txt`.

On desktop the bat is held low and to the right of the view, so the tests swing it by sweeping the view down through a part in an eighth of a second, about the speed of a short flick of the forearm.

**If Unity crashes when you press Play** without a headset: Meta's XR Operator layer (`XrApiLayer_METAX_operator`, part of Meta's agent test tooling) crashed the editor twice on October 6 as OpenXR started. The suites were run with OpenXR off for desktop Play Mode on October 8 too. Turning off **Initialize XR on Startup** on the Windows, Mac, Linux tab of **Project Settings → XR Plug-in Management** stops OpenXR from starting in desktop Play Mode, which the tests don't need. Turn it back on to play through Quest Link or the Meta XR Simulator; the scene builder and both build commands also turn it back on.

Earlier Meta XR Simulator checks verified controller teleport, joystick movement, trigger selection, hand pinch selection, and hand grabbing, carrying, and release. See `Documentation/XR-validation.md` for their scope. They predate the menu.

A physical Quest playtest is still needed for comfort, frame rate, seated reach, the left-palm menu gesture, hand/controller switching, how the reflection's arms follow real hands, and how a bat swing feels with real hands and controllers. The simulator does not measure headset performance.

## Quest build

There are two build commands in the **After Hours** menu:

- **Build shareable Quest APK (release)** writes `Builds/Quest/AfterHours-release.apk`. Give this one to playtesters. It leaves out Meta's XR Operator test layer and the Immersive Debugger, and it clears the DevAgent's network address and access token from the build.
- **Build Quest APK (development)** writes `Builds/Quest/AfterHours.apk`. It includes Meta's test tooling for Meta XR Simulator and agent-driven testing, so keep it on your own headset. In this build, clicking the left thumbstick toggles the Immersive Debugger. The left menu button stays reserved for the game's menu, and B/Y for returning to the lobby.

To play on your own headset, connect the Quest with a USB data cable, turn on Developer Mode, and accept **Allow USB debugging** in the headset. Then use **After Hours → Install release APK on connected Quest** (or the development equivalent). It installs the APK with Unity's bundled `adb`, starts the game, and reports each step in the Console. If the headset hasn't been authorized, more than one device is connected, or the APK doesn't exist yet, the Console says so.

## Release signing

The Meta Developer Dashboard only accepts APKs signed with your own key. **After Hours → Create release signing key** makes one, once, with the JDK that ships with Unity: `UserSettings/AfterHours-release.keystore` and a random password in `UserSettings/AfterHours-release-key.txt`. `UserSettings` is never committed. The release build signs with this key and puts the project back on the debug key afterwards, so no keystore path or password stays in the project settings. The development build stays debug-signed.

**Back up both files somewhere private.** An app on the Meta store can only ever be updated with the key it was first uploaded with.

Every build gets a new version code: the number of minutes since January 1, 2026, and always higher than the last build's. The version name is 0.4.0. The dashboard rejects an upload whose version code it has seen before.

**After Hours → Commit and push to GitHub…** commits everything git doesn't ignore and pushes the current branch to `origin`. The shareable release APK is committed too, through Git LFS, so playtesters can download `Builds/Quest/AfterHours-release.apk` from GitHub. Development APKs, `Library`, removed packages and the Immersive Debugger's per-computer settings (`Assets/Resources/DevAgentSettings.asset`, which holds this computer's network address and an access token) stay out of the repository. The push stops if GitHub has commits this computer doesn't, and the first push may open Git Credential Manager's GitHub sign-in window. Commits use the name and email under **Commit as**. Your GitHub account keeps its email private, so use your GitHub noreply address there; GitHub refuses pushes that show the private one.

Both build commands build from the Android platform settings. If a custom build profile such as Unity's **Meta Quest** profile is active, they switch back to the platform profile first, because a custom profile carries its own player, quality and XR settings.

Both commands apply the Quest settings first: OpenXR with Meta XR, controllers and hands, ARM64, IL2CPP, Vulkan, linear color, 4x MSAA, render scale 1.0, 28 m shadow distance, and Android API 32 minimum. Mixed-reality features stay off, so the APK requests no scene, anchor, passthrough or headset-camera permissions. The target API is Android 34, which the Meta Quest Store and its release channels expect, and dynamic resolution is on, as Meta's Project Setup Tool recommends. The release APK is signed with your release key (see above); the development APK with Unity's debug key. The project doesn't include the Meta Voice SDK, Meta XR Audio or Unity IAP packages. The game doesn't use them, and the Voice SDK's build step crashed the Android build. Only the current room is active. Static geometry is combined by material. Anything the player can pick up, or that moves or switches on and off, keeps its own mesh, and the scene builder logs an error if a movable object is ever merged.

## Art style

The game uses a refined low-poly look:

- Every box has a small bevel. The bevel stays the same size in metres however the box is scaled, so edges catch the light the same way on a keyboard key and on a wall.
- Cylinders, spheres and capsules are faceted, and every facet is flat shaded.
- Materials are matte flat colours in the midnight, teal, coral and brass palette. There are no photographic textures.
- Floors are laid as bevelled tiles, walls as panels and ceilings as coffers.
- Gradient ambient light makes up-facing facets lighter and down-facing ones darker.

Tiles and panels don't cast their own shadows. One invisible, square-edged slab per surface casts the shadow instead, so no light leaks through the seams. Floors cast no shadows.

**After Hours → Capture room previews** renders a still from each room's arrival point into `Documentation/Previews/`, plus the menu in the lobby and the kitchen, the walking vignette, the first-visit card, each chapter's destinations lit up as a seated player sees them, the rage room's computer from the seat, and the mirror with four different looks. The gameplay journey also saves stills of the rage room with the bat in hand, broken, and quiet. Art and comfort changes can be checked without a headset.

## More rooms to add

1. **The elevator of comparison**: every floor appears to be someone else's promotion. Choose a destination using your own values instead of chasing the highest number.
2. **The meeting that never ends**: chairs repeat imagined criticisms. Move one chair into daylight and replace guesses with things you actually know.
3. **The server room of rumination**: replaying conversations power an increasingly noisy machine. Unplug one loop at a time and route attention to an ordinary present-tense task.
4. **The lost-and-found of identity**: find parts of yourself that never appeared on a job description—friend, maker, parent, neighbor, learner—and arrange them on an empty name badge.
5. **The greenhouse of small beginnings**: plant modest intentions. Growth depends on returning gently, not speed, streaks, or productivity.
6. **The interview mirror**: practice telling your story while the reflection changes from an idealized employee into an ordinary, complete person.

## Editing and asset provenance

All game geometry, wording, music and sound were created for this project. The audio is synthesised by `Tools/make_audio.py`. The rig and hand rendering use the installed Meta XR Core SDK. Text uses the project's existing Liberation Sans TMP asset. Third-party packages retain their own licenses.

Game scripts are in `Assets/AfterHours/Scripts`; the deterministic scene builder is in `Assets/AfterHours/Editor`. The embedded Unity OpenXR Meta package contains a narrow compatibility fix for the editor's EntityId API. See `Documentation/Compatibility.md`.
