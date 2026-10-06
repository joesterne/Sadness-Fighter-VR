# XR simulator validation

Validated in Unity 6000.6.3f1 with Meta XR Simulator v207 on October 1, 2026. Input was injected through the Meta XR Operator OpenXR layer, then observed through the game's actual input and physics paths.

| Check | Observed result |
|---|---|
| XR startup | Session reached FOCUSED; headset position and orientation were valid and tracked. |
| Controller floor teleport | Trigger release moved the player from (0, 0.02, -4) to (0.78, 0.02, -3.04). |
| Left joystick | A forward input moved the player from z=-3.04 to z=-2.57. |
| Controller selection | A trigger press on the movement setting changed smooth motion from true to false and displayed “Teleport comfort mode enabled.” |
| Hand tracking | Right hand reported tracked, high confidence, and a valid pointer pose. |
| Hand selection | An index pinch on the movement setting changed smooth motion from false to true and displayed “Joystick walking enabled.” |
| Hand grab | Pinching the FEAR box set its held state to true. |
| Hand carry | Moving the wrist moved the held box by approximately (0.30, 0.10, 0.15) metres. |
| Hand release | Opening the hand cleared held state and restored dynamic rigidbody physics. |
| Rendering | Reviewed the composited left-eye image of the lobby. Geometry, labels, lighting, and interaction rays rendered. |
| Runtime errors | Unity's current console reported zero errors after the input tests. Older editor bridge socket errors remained in historical logs. |

The warehouse and player position were set up before the focused grab test; grabbing, carrying, and releasing used synthetic tracked-hand input. A first controller aim reached the floor because the simulator's grip orientation includes an offset; subsequent targeting used the observed anchor orientation. A hand ray toward the warehouse door was obstructed by lobby geometry, so it was not counted as a successful navigation test.

The editor needed explicit frame stepping while remotely controlled. `SimulatorTestSession` provides a bounded editor-only frame pump. All synthetic inputs were released and Play Mode stopped after testing.

The separate desktop gameplay run passed all 46 checks in `Gameplay-validation.txt`, covering all four chapter completions, object delivery, coffee mistakes and help, archive sorting, saving, and the final rooftop text.

Not yet verified on a physical Quest: frame rate, thermal behavior, controller grip carrying, hand/controller switching during play, seated reach, and comfort. Simulator evidence does not establish on-device performance.
