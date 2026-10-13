# Headsets and input

One APK (`com.afterhours.mindoffice`, Android API 32 minimum, 34 target) is built for every Quest that can run it.

| Headset | Listed in the manifest | Measured |
|---|---|---|
| Quest 3 | yes | played on a headset; all earlier builds |
| Quest 3S | yes | not measured (same chip and runtime as the Quest 3) |
| Quest Pro | yes | not measured |
| Quest 2 | yes | **not measured**: no Quest 2 has run this build |

## Input

Hand tracking is optional in the manifest (`required=false`), so a headset with hand tracking off still starts, and the game works with either hands or controllers.

- **Hands are the default.** The game starts in hands wording. The headset's own report of which input is active decides what the signs and hints say: hands, or controllers when they are picked up. If the headset reports neither, a single tracked hand or controller decides; both or neither changes nothing. A change must last 0.6 s before the wording follows, and the first report is believed at once.
- **Controllers.** Point with the controller and squeeze the trigger to choose. The trigger and the grip both pick things up and hold them; letting go sends, throws or drops. Left stick walks (unless MOVEMENT is on TELEPORT ONLY), right stick snap-turns and pulls back to turn around, B/Y returns to the lobby, the menu button opens the menu.
- **Hands.** Point and pinch (index and thumb) to choose, hold and throw. A palm pinch on the left hand opens the menu. Movement is by teleport, and the menu has turn buttons.

## Quest 2 profile

`DeviceProfile` asks the runtime which headset it is (Quest 2 and Quest 2 over a link cable are recognised) and, only on a Quest 2, lowers the render scale to 0.9, the shadow distance to 20 m and requests a 72 Hz display if the headset offers it. These are conservative guesses for the Quest 2's lower resolution and weaker chip. They are not measured. If the rage room's 210 pieces or any scene is too heavy on a Quest 2, the next steps are 2x MSAA, a lower render scale, and fewer loose pieces.

## What the editor tests cover

The tests run on a desktop, so they check the wording rules, the choice between hands and controllers from the headset's reported state, the headset recognition, that the Quest 2 profile applies and undoes exactly, and the throw maths and the real throw path through the player code (with throwing switched on for the desktop). They cannot check that a Quest 2 or a controller actually behaves, which is what `Playtest.md` is for.
