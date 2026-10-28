# After Hours

A gentle VR game about the weeks after losing a job: the shame, the worry, the anger, and what is still true about you. Built for Meta Quest 2, Quest Pro, Quest 3 and Quest 3S.

![The ocean of shame](<Documentation/Previews/LowPoly - Ocean of shame.png>)

## Why it exists

I've been laid off a few times. It was rough, but through therapy I found some closure. After Hours is my attempt to help other people find a little of that too. It is a gentle experience, not a substitute for professional support, and you can take a break whenever you like.

## What you can do

Seven short chapters, played in any order from a midnight lobby, then a sunrise rooftop to rest on:

1. **Ocean of shame**: row toward a lighthouse, by hand or with one button.
2. **Heavy things**: pick up five named feelings and send them off.
3. **A small spill**: a coffee machine leaks and a crowd gathers until you ask for help.
4. **The infinite archive**: sort notes into FACT, FEAR and STILL TRUE.
5. **The old résumé**: shred the old pages and keep one line that is still true.
6. **The rage room**: take a bat to your old work computer.
7. **The ambush**: name how you feel until two giant clowns shrink.

On the rooftop there is also a tower of wooden blocks to play while you rest, with real physics, by hand or with controllers.

Nothing is timed and nothing can be failed. Every small step saves, so a five-minute visit counts. A mirror in the lobby lets you change your look and wear pieces you earn by playing.

The game is meant to be free to play. Every chapter and comfort option is free, and the optional outfit add-ons are cosmetic only.

## Comfort and access

- Every chapter can be finished seated, within arm's reach.
- Hand tracking first, controllers whenever you pick them up.
- Snap turns, teleport, a comfort vignette, three seated heights and FACE FORWARD.
- Gentler options for the kitchen crowd and the ambush, and a CLEAN MODE that hides all text.
- Soft controller haptics: a firm tick that settles when you pick something up, and three slow, rising beats in both hands as you move between rooms.

## Run it

You need Unity 6000.6.3f1 with Android build support.

1. Open this folder in Unity.
2. Open `Assets/AfterHours/Scenes/AfterHours.unity` and press Play for desktop preview (WASD, right mouse to look, left click to select, Tab for the menu).
3. Use the **After Hours** menu to rebuild the scene, run the test suites, or build a Quest APK.

To try it on a headset, turn on Developer Mode, connect it by USB and use **After Hours → Install release APK on connected Quest**. Release builds need your own signing key (**After Hours → Create release signing key**).

## More detail

[README-AfterHours.md](README-AfterHours.md) is the full developer guide: every room, the menu, controls, sound, tests, builds, signing and the outfit add-on setup. Validation results and playtest notes are in [Documentation](Documentation/).

## Built with

Unity, the Meta XR SDK and AI coding tools. All geometry, wording, music and sound were made for this project, and the audio is synthesised by `Tools/make_audio.py`.

## What's next

More testing with real players, more rooms, publishing beyond Quest (Steam), and maybe Apple Vision Pro.
