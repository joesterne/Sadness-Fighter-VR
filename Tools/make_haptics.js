// Original haptic clips for After Hours, written in the .haptic format that Meta Haptics Studio exports and the Haptics SDK plays.
// Each clip is an amplitude envelope with emphasis points (short, sharp transients) and a frequency envelope. All values run from
// 0 to 1; low frequency reads as a soft thud, high as a crisp tick. Kept soft for comfort, like the rest of the game.
// The files open in Haptics Studio for fine-tuning; re-running this script overwrites them.
//
// Run: node Tools/make_haptics.js   (writes Assets/AfterHours/Resources/Haptics/*.haptic)
const fs = require("fs");
const path = require("path");

const out = path.join(__dirname, "..", "Assets", "AfterHours", "Resources", "Haptics");

const r = v => Math.round(v * 10000) / 10000;
// A point on the amplitude envelope, optionally with an emphasis (a transient on top of the envelope).
const a = (time, amplitude, emphasis, sharpness) =>
    emphasis === undefined ? { time: r(time), amplitude: r(amplitude) }
        : { time: r(time), amplitude: r(amplitude), emphasis: { amplitude: r(Math.max(emphasis, amplitude)), frequency: r(sharpness) } };
const f = (time, frequency) => ({ time: r(time), frequency: r(frequency) });

// A hit that starts at `at` with strength `peak` and dies away over `length`, sampled on an exponential curve.
function strike(at, peak, length, emphasis, sharpness, steps = 6) {
    const points = [a(at, 0), a(at + .002, peak, emphasis, sharpness)];
    for (let i = 1; i <= steps; i++) { const t = i / steps; points.push(a(at + .002 + length * t, i === steps ? 0 : peak * Math.exp(-4 * t))); }
    return points;
}
// A soft beat: eases in and out with no transient.
function beat(at, peak, length) {
    return [a(at, 0), a(at + length * .3, peak), a(at + length * .6, peak * .8), a(at + length, 0)];
}

const clips = {
    // A button, a throw, a send: one crisp, light click.
    tap: {
        amplitude: strike(0, .4, .05, .6, .9, 4),
        frequency: [f(0, .75), f(.06, .6)],
    },
    // Taking hold of something: a firm tick that settles into the hand.
    pickup: {
        amplitude: [...strike(0, .45, .045, .7, .6, 4), a(.08, 0), a(.1, .2), a(.13, .14), a(.17, 0)],
        frequency: [f(0, .55), f(.06, .4), f(.17, .3)],
    },
    // Changing rooms: three slow, rising beats in both hands while the view fades.
    room_change: {
        amplitude: [...beat(0, .14, .09), ...beat(.2, .2, .09), ...beat(.4, .28, .11)],
        frequency: [f(0, .2), f(.2, .3), f(.51, .42)],
    },
    // The bat or a tool landing on something: a deep thud. Played louder for a harder swing.
    hit: {
        amplitude: strike(0, .8, .14, 1, .3, 7),
        frequency: [f(0, .35), f(.05, .2), f(.15, .12)],
    },
    // Something breaking apart: a heavy crunch, then a crackle of pieces as it goes.
    smash: {
        amplitude: [
            a(0, 0), a(.002, .9, 1, .35), a(.03, .62), a(.06, .42),
            a(.085, .34, .55, .8), a(.11, .3), a(.13, .28, .45, .85), a(.165, .22), a(.18, .2, .38, .9),
            a(.23, .14, .3, .8), a(.27, .1), a(.3, .08, .22, .9), a(.36, .04), a(.42, 0),
        ],
        frequency: [f(0, .25), f(.07, .45), f(.2, .65), f(.42, .7)],
    },
    // The machete through a web: a quick, rising swish that ends in a snap.
    cut: {
        amplitude: [a(0, 0), a(.015, .18), a(.03, .38), a(.036, .55, .75, .95), a(.05, .3), a(.07, .12), a(.095, 0)],
        frequency: [f(0, .6), f(.035, .9), f(.095, .75)],
    },
};

fs.mkdirSync(out, { recursive: true });
for (const [name, clip] of Object.entries(clips)) {
    const data = {
        version: { major: 1, minor: 0, patch: 0 },
        metadata: { editor: "After Hours make_haptics.js", source: "", project: "After Hours", tags: [], description: "" },
        signals: { continuous: { envelopes: { amplitude: clip.amplitude, frequency: clip.frequency } } },
    };
    for (let i = 1; i < clip.amplitude.length; i++) if (clip.amplitude[i].time <= clip.amplitude[i - 1].time) throw new Error(name + ": amplitude times must rise");
    fs.writeFileSync(path.join(out, name + ".haptic"), JSON.stringify(data) + "\n");
    console.log(name, (clip.amplitude[clip.amplitude.length - 1].time * 1000).toFixed(0) + " ms");
}
