# Room-navigation bug fixes

Verified October 2, 2026. The final results comprise 74 passing checks from the route test and focused remaining cases, plus 19 passing seated/persistence checks. Current Unity console after the tests: zero errors and zero warnings.

- **Button press becoming teleport:** continuous floor aiming could arm a teleport after a button moved the view, including a seated-height change. A press now starts either a button action or a floor gesture. Only a gesture that started on the floor can teleport; it follows the pointer while held and cancels if released over an invalid target.
- **Teleport landing inside a doorway:** the portal's enter event could occur during the teleport fade, while room travel was locked. Occupied portals now retry after the fade through their stay event.
- **Input carried across room boundaries:** both hands must release their trigger/grip/pinch after a room change or tracking recovery. Held objects are released in their original room before that room is disabled.
- **Warehouse spawn intersecting a wall:** its arrival point moved from local z=-5 to z=-4.2, clearing the entrance wall and the player's capsule.
- **Missed lobby shortcut:** the return control now tracks the held state and press edge in the game update, consistently with the other interactions. Desktop Escape and Quest B/Y use this path.
- **Leaving during a paddle stroke:** departure cancels the running rowing animation, synchronizes the shore to recorded strokes, and awards completion if the final stroke already registered. This prevents overlapping fades and incomplete final-stroke progress.
- **Seated control collision:** the floating controls no longer collide with the player capsule. Teleport clearance ignores the player's own body and controls and checks the current standing or seated body height.

The route tests exercise input, floor raycasts, CharacterController movement, actual portal triggers, safe spawn positions, one active environment, fade clearing, release during travel, invalid destinations, competing requests, and interrupted rowing. Test setup uses direct placement to reach each starting point. The final walking cases ran separately after fixing a test fixture that had pointed the camera away from the door; the earlier report is retained in `Navigation-before-fixture-correction.txt`.

These are editor tests. Controller movement and hand interaction were separately checked in the Meta XR Simulator before this regression pass. Physical Quest performance and seated comfort still need an on-device playtest.
