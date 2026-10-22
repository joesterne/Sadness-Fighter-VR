"""Original sound design for After Hours, synthesised from scratch (no samples, no third-party audio).

Run: python3 make_audio.py <output folder>
Writes 32 kHz mono 16-bit WAV files. Ambience and music loop seamlessly.
"""
import os
import sys

import numpy as np
from scipy import signal

SR = 32000
rng = np.random.default_rng(20261006)


# ---------- building blocks ----------
def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR

def white(n):
    return rng.standard_normal(n)

def pink(n):
    # Voss-style approximation by filtering white noise (Paul Kellet's coefficients).
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    return signal.lfilter(b, a, white(n)) * 4

def brown(n):
    x = np.cumsum(white(n))
    x = signal.lfilter([1, -1], [1, -0.999], x)  # remove drift
    return x / (np.std(x) + 1e-9)

def lowpass(x, hz, order=2):
    b, a = signal.butter(order, hz / (SR / 2), 'low')
    return signal.lfilter(b, a, x)

def highpass(x, hz, order=2):
    b, a = signal.butter(order, hz / (SR / 2), 'high')
    return signal.lfilter(b, a, x)

def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), hi / (SR / 2)], 'band')
    return signal.lfilter(b, a, x)

def smooth_noise(n, rate_hz, seed_scale=1.0):
    """Slow random control signal in [0,1] changing at roughly rate_hz."""
    k = max(4, int(n / SR * rate_hz) + 4)
    pts = rng.random(k)
    x = np.interp(np.linspace(0, k - 3, n), np.arange(k), pts)
    return lowpass(x, max(rate_hz, 0.05), 1)

def env_adsr(n, attack, release, curve=3.0):
    e = np.ones(n)
    a = min(n, int(attack * SR)); r = min(n - a, int(release * SR))
    if a > 0: e[:a] = np.linspace(0, 1, a) ** 2
    if r > 0: e[n - r:] = np.linspace(1, 0, r) ** curve
    return e

def decay(n, seconds):
    return np.exp(-np.arange(n) / (seconds * SR))

def reverb_ir(seconds, damp_hz=4000, predelay=0.012):
    n = int(seconds * SR)
    ir = white(n) * np.exp(-np.arange(n) / (seconds * SR / 6.9))
    ir = lowpass(ir, damp_hz, 1)
    ir = np.concatenate([np.zeros(int(predelay * SR)), ir])
    return ir / np.sqrt(np.sum(ir ** 2))

def reverb(x, seconds=1.8, mix=0.3, damp_hz=4000):
    wet = signal.fftconvolve(x, reverb_ir(seconds, damp_hz))[:len(x)]
    return x * (1 - mix) + wet * mix

def make_loop(x, fade_seconds):
    """x holds the loop plus fade_seconds of extra material; fold the tail over the head."""
    f = int(fade_seconds * SR); n = len(x) - f
    out = x[:n].copy()
    w = np.linspace(0, np.pi / 2, f)
    out[:f] = x[:f] * np.sin(w) + x[n:n + f] * np.cos(w)
    return out

def normalise(x, peak_db=-3.0, rms_db=None):
    x = x - np.mean(x)
    if rms_db is not None:
        x = x / (np.sqrt(np.mean(x ** 2)) + 1e-12) * 10 ** (rms_db / 20)
    p = np.max(np.abs(x))
    limit = 10 ** (peak_db / 20)
    if p > limit: x = x / p * limit
    return x

def fade_edges(x, ms=4):
    k = int(ms / 1000 * SR)
    x = x.copy(); x[:k] *= np.linspace(0, 1, k); x[-k:] *= np.linspace(1, 0, k)
    return x

def write(folder, name, x):
    data = np.clip(x, -1, 1)
    from scipy.io import wavfile
    wavfile.write(os.path.join(folder, name + '.wav'), SR, (data * 32767).astype(np.int16))


# ---------- ambience ----------
def amb_lobby(seconds=30, fade=4):
    n = int((seconds + fade) * SR)
    tone = lowpass(brown(n), 220, 2) * 0.9 + lowpass(pink(n), 1800, 1) * 0.12
    air = highpass(lowpass(white(n), 6000, 1), 2500, 1) * 0.015
    x = tone + air
    return normalise(make_loop(x, fade), -12, rms_db=-34)

def amb_ocean(seconds=32, fade=4):
    n = int((seconds + fade) * SR)
    swell = np.zeros(n); foam = np.zeros(n)
    t = 0.0
    while t < seconds + fade:
        period = rng.uniform(6.5, 9.0); peak = t + period * 0.42
        i0, ip, i1 = int(t * SR), int(peak * SR), int(min(seconds + fade, t + period) * SR)
        if ip >= n: break
        i1 = min(i1, n)
        swell[i0:ip] += np.linspace(0, 1, ip - i0) ** 2 * rng.uniform(0.7, 1.0)
        swell[ip:i1] += np.linspace(1, 0, i1 - ip) ** 1.6 * rng.uniform(0.7, 1.0)
        f0 = min(n, ip + int(0.2 * SR)); f1 = min(n, f0 + int(rng.uniform(2.2, 3.4) * SR))
        foam[f0:f1] += decay(f1 - f0, 0.9) * rng.uniform(0.6, 1.0)
        t += period
    swell = lowpass(swell, 2, 1); foam = lowpass(foam, 8, 1)
    wash = lowpass(brown(n), 500, 2) * (0.25 + 0.75 * swell)
    hiss = bandpass(white(n), 900, 5500, 2) * foam * 0.22
    breeze = lowpass(pink(n), 700, 1) * 0.06 * (0.6 + 0.4 * smooth_noise(n, 0.15))
    x = wash + hiss + breeze
    return normalise(make_loop(x, fade), -6, rms_db=-24)

def metal_hit(seconds=2.5, base=None):
    n = int(seconds * SR); tt = np.arange(n) / SR
    base = base or rng.uniform(380, 520)
    x = np.zeros(n)
    for ratio, amp, d in [(1, 1, 0.9), (2.76, 0.6, 0.5), (5.40, 0.35, 0.3), (8.93, 0.2, 0.2)]:
        x += np.sin(2 * np.pi * base * ratio * tt) * amp * decay(n, d)
    x += lowpass(white(n), 3000, 1) * decay(n, 0.01) * 0.5
    return x

def amb_warehouse(seconds=30, fade=4):
    n = int((seconds + fade) * SR); tt = np.arange(n) / SR
    rumble = lowpass(brown(n), 110, 2) * 0.9
    hum = (np.sin(2 * np.pi * 120 * tt) * 0.5 + np.sin(2 * np.pi * 240 * tt) * 0.25 + np.sin(2 * np.pi * 360 * tt) * 0.12)
    hum *= 0.02 * (0.85 + 0.15 * smooth_noise(n, 0.3))
    room = lowpass(pink(n), 1200, 1) * 0.08
    clanks = np.zeros(n)
    for when in (6.5, 19.0, 27.5):
        h = lowpass(metal_hit(), 2200, 1) * rng.uniform(0.18, 0.28)
        i = int(when * SR); clanks[i:i + len(h)] += h[:max(0, n - i)]
    clanks = reverb(clanks, 2.4, 0.75, 2500)
    x = rumble + hum + room + clanks
    return normalise(make_loop(x, fade), -8, rms_db=-30)

def amb_kitchen(seconds=30, fade=4):
    n = int((seconds + fade) * SR); tt = np.arange(n) / SR
    room = lowpass(brown(n), 260, 2) * 0.7 + lowpass(pink(n), 2000, 1) * 0.1
    fridge = (np.sin(2 * np.pi * 60 * tt) + 0.4 * np.sin(2 * np.pi * 180 * tt)) * 0.03 * (0.8 + 0.2 * np.sin(2 * np.pi * 0.21 * tt))
    x = room + fridge
    return normalise(make_loop(x, fade), -10, rms_db=-33)

VOWELS = [(730, 1090), (530, 1840), (270, 2290), (570, 840), (300, 870), (660, 1720), (440, 1020)]

def voice(n):
    """One indistinct voice: buzz through shifting vowel formants, chopped into syllables."""
    out = np.zeros(n); i = int(rng.uniform(0, 1.5) * SR)
    f0 = rng.uniform(95, 230)
    while i < n:
        phrase = int(rng.uniform(1.2, 3.5) * SR)
        end = min(n, i + phrase)
        while i < end:
            syl = int(rng.uniform(0.11, 0.26) * SR); j = min(end, i + syl)
            m = j - i
            if m > 32:
                tt = np.arange(m) / SR
                pitch = f0 * (1 + 0.06 * np.sin(2 * np.pi * rng.uniform(1, 3) * tt + rng.uniform(0, 6)))
                phase = np.cumsum(pitch) / SR
                buzz = signal.sawtooth(2 * np.pi * phase) + white(m) * 0.15
                f1, f2 = VOWELS[rng.integers(len(VOWELS))]
                v = bandpass(buzz, f1 * 0.8, f1 * 1.2, 1) + 0.6 * bandpass(buzz, f2 * 0.85, min(f2 * 1.15, 3800), 1)
                out[i:j] += v * np.sin(np.linspace(0, np.pi, m)) ** 0.8
            i = j + int(rng.uniform(0.0, 0.06) * SR)
        i = end + int(rng.uniform(0.4, 1.6) * SR)
    return out

def amb_murmur(seconds=30, fade=4, voices=9):
    n = int((seconds + fade) * SR)
    x = sum(voice(n) * rng.uniform(0.5, 1.0) for _ in range(voices))
    x = lowpass(x, 2600, 2)
    x = reverb(x, 1.4, 0.55, 3000)
    return normalise(make_loop(x, fade), -6, rms_db=-24)

def amb_archive(seconds=30, fade=4):
    n = int((seconds + fade) * SR)
    hush = lowpass(pink(n), 650, 2) * 0.5 + lowpass(brown(n), 140, 2) * 0.4
    rustle = np.zeros(n)
    for when in (4.0, 13.5, 22.0, 29.0):
        m = int(rng.uniform(0.25, 0.6) * SR)
        crackle = highpass(white(m), 2200, 2) * (rng.random(m) < 0.25) * env_adsr(m, 0.05, 0.2)
        i = int(when * SR); rustle[i:i + m] += crackle[:max(0, n - i)] * 0.12
    rustle = reverb(lowpass(rustle, 6000, 1), 2.8, 0.8, 3500)
    x = hush + rustle
    return normalise(make_loop(x, fade), -10, rms_db=-35)

def amb_rooftop(seconds=32, fade=5):
    n = int((seconds + fade) * SR)
    gust = 0.35 + 0.65 * smooth_noise(n, 0.12) ** 1.5
    centre = 350 + 450 * smooth_noise(n, 0.08)
    src = pink(n)
    # Wind: noise through a slowly moving band, by blending three fixed bands.
    low, mid, high = bandpass(src, 200, 420, 2), bandpass(src, 380, 800, 2), bandpass(src, 700, 1400, 2)
    w = np.clip((centre - 350) / 450, 0, 1)
    wind = (low * (1 - w) + mid * (1 - abs(w - 0.5) * 2) * 0.8 + high * w * 0.6) * gust
    city = lowpass(brown(n), 160, 2) * 0.35
    x = wind + city
    return normalise(make_loop(x, fade), -6, rms_db=-28)


# ---------- music ----------
NOTE = {'C': -9, 'C#': -8, 'D': -7, 'D#': -6, 'E': -5, 'F': -4, 'F#': -3, 'G': -2, 'G#': -1, 'A': 0, 'A#': 1, 'B': 2}

def hz(name, octave):
    return 440.0 * 2 ** ((NOTE[name] + 12 * (octave - 4)) / 12)

def pad_note(freq, n):
    tt = np.arange(n) / SR
    x = np.zeros(n)
    for detune, amp in [(1.0, 1.0), (1.0017, 0.7), (0.9983, 0.7), (2.0, 0.18), (3.0, 0.05)]:
        x += np.sin(2 * np.pi * freq * detune * tt + rng.uniform(0, 6.28)) * amp
    return x * (0.85 + 0.15 * np.sin(2 * np.pi * rng.uniform(0.1, 0.25) * tt))

def bell(freq, seconds=3.0):
    n = int(seconds * SR); tt = np.arange(n) / SR
    x = (np.sin(2 * np.pi * freq * tt) + 0.35 * np.sin(2 * np.pi * freq * 2.001 * tt) * decay(n, 0.6)
         + 0.12 * np.sin(2 * np.pi * freq * 3.003 * tt) * decay(n, 0.3))
    return x * decay(n, 1.1) * env_adsr(n, 0.006, 0.05)

def music_theme(seconds=64, fade=6):
    n = int((seconds + fade) * SR)
    chords = [
        [('D', 3), ('A', 3), ('C#', 4), ('E', 4), ('F#', 4)],   # Dmaj9
        [('B', 2), ('F#', 3), ('A', 3), ('D', 4), ('E', 4)],    # Bm11
        [('G', 2), ('D', 3), ('F#', 3), ('B', 3), ('E', 4)],    # Gmaj13
        [('A', 2), ('E', 3), ('G', 3), ('B', 3), ('D', 4)],     # A7sus/add9
    ]
    seg = 16.0
    pad = np.zeros(n)
    for k in range(int(np.ceil((seconds + fade) / seg)) + 1):
        chord = chords[k % len(chords)]
        start = int(k * seg * SR); length = int((seg + 5) * SR)
        if start >= n: break
        m = min(length, n - start)
        e = env_adsr(length, 3.5, 5.0, 2.0)[:m]
        for name, octv in chord:
            pad[start:start + m] += pad_note(hz(name, octv), m) * e * 0.18
    pad = lowpass(pad, 1800, 2)
    melody = np.zeros(n)
    scale = [('D', 5), ('E', 5), ('F#', 5), ('A', 5), ('B', 5), ('C#', 6), ('D', 6)]
    t = 2.0
    while t < seconds + fade - 1:
        name, octv = scale[rng.integers(len(scale))]
        b = bell(hz(name, octv)) * rng.uniform(0.05, 0.09)
        i = int(t * SR); melody[i:i + len(b)] += b[:max(0, n - i)]
        t += rng.choice([2.0, 3.0, 4.0, 2.5])
    x = pad + melody
    x = reverb(x, 3.5, 0.45, 3500)
    return normalise(make_loop(x, fade), -6, rms_db=-24)

def soft_key(freq, seconds=5.0):
    # A felt-piano-like note: a round fundamental, a quieter octave that fades first, and a soft hammer.
    n = int(seconds * SR); tt = np.arange(n) / SR
    x = (np.sin(2 * np.pi * freq * tt) + 0.3 * np.sin(2 * np.pi * freq * 2.002 * tt) * decay(n, 0.9)
         + 0.08 * np.sin(2 * np.pi * freq * 3.004 * tt) * decay(n, 0.4))
    return lowpass(x * decay(n, 1.6) * env_adsr(n, 0.012, 0.6), 2400, 1)

def music_quiet(seconds=144, fade=8):
    # The calm music under the chapters: slower and softer than the theme, in F. Warm pads change every 18 seconds over four
    # chords, and now and then a short phrase of soft piano notes drifts over them, with long rests between.
    n = int((seconds + fade) * SR)
    chords = [
        [('F', 2), ('C', 3), ('A', 3), ('E', 4), ('G', 4)],     # Fmaj9
        [('D', 2), ('A', 2), ('F', 3), ('C', 4), ('E', 4)],     # Dm9
        [('A#', 2), ('F', 3), ('A', 3), ('C', 4), ('D', 4)],    # Bbmaj9/6
        [('C', 3), ('G', 3), ('A#', 3), ('D', 4), ('F', 4)],    # C9sus
    ]
    seg = 18.0
    pad = np.zeros(n)
    for k in range(int(np.ceil((seconds + fade) / seg)) + 1):
        chord = chords[k % len(chords)]
        start = int(k * seg * SR); length = int((seg + 6) * SR)
        if start >= n: break
        m = min(length, n - start)
        e = env_adsr(length, 5.0, 6.0, 2.0)[:m]
        for name, octv in chord:
            pad[start:start + m] += pad_note(hz(name, octv), m) * e * 0.15
    pad = lowpass(pad, 1400, 2)
    keys = np.zeros(n)
    scale = [('F', 4), ('G', 4), ('A', 4), ('C', 5), ('D', 5), ('F', 5)]
    t = 6.0
    while t < seconds + fade - 6:
        step = int(rng.integers(len(scale)))
        for _ in range(int(rng.integers(2, 5))):
            name, octv = scale[step]
            b = soft_key(hz(name, octv)) * rng.uniform(0.05, 0.08)
            i = int(t * SR); keys[i:i + len(b)] += b[:max(0, n - i)]
            t += rng.choice([1.0, 1.25, 1.5])
            step = int(np.clip(step + rng.choice([-2, -1, 1, 2]), 0, len(scale) - 1))
        t += rng.uniform(5.0, 9.0)
    x = pad + keys
    x = reverb(x, 4.5, 0.5, 3000)
    return normalise(make_loop(x, fade), -8, rms_db=-25)

def pad_bed(chords, seg, n, level=0.15, attack=5.0, release=6.0, cutoff=1400):
    # Warm pads that move through the chords, one every seg seconds, each overlapping the next.
    pad = np.zeros(n)
    for k in range(int(np.ceil(n / SR / seg)) + 1):
        chord = chords[k % len(chords)]
        start = int(k * seg * SR); length = int((seg + release) * SR)
        if start >= n: break
        m = min(length, n - start)
        e = env_adsr(length, attack, release, 2.0)[:m]
        for name, octv in chord:
            pad[start:start + m] += pad_note(hz(name, octv), m) * e * level
    return lowpass(pad, cutoff, 2)

def place(into, sound, at):
    i = int(at * SR)
    if i >= len(into): return
    into[i:i + len(sound)] += sound[:len(into) - i]

def music_tide(seconds=128, fade=8):
    # Slow tide: a rolling, unhurried arpeggio in A, three notes up and three down like water coming in and going out,
    # over soft pads that change every 16 seconds (Amaj9, F#m11, Dmaj9, E6sus).
    n = int((seconds + fade) * SR)
    chords = [
        [('A', 2), ('E', 3), ('G#', 3), ('B', 3), ('C#', 4)],
        [('F#', 2), ('C#', 3), ('E', 3), ('A', 3), ('B', 3)],
        [('D', 2), ('A', 2), ('F#', 3), ('C#', 4), ('E', 4)],
        [('E', 2), ('B', 2), ('C#', 3), ('F#', 3), ('A', 3)],
    ]
    seg = 16.0
    pad = pad_bed(chords, seg, n, level=0.12)
    roll = np.zeros(n)
    t = 2.0
    while t < seconds + fade - 4:
        chord = chords[int(t // seg) % len(chords)]
        up = [hz(name, octv + 1) for name, octv in chord[1:4]]
        for j, f in enumerate(up + up[::-1]):
            place(roll, soft_key(f, 3.5) * (0.05 + 0.015 * np.sin(j)) * rng.uniform(0.85, 1.1), t + j * 0.62)
        t += 6 * 0.62 + rng.choice([0.6, 1.2, 2.4])
    x = reverb(pad + roll, 4.0, 0.5, 3200)
    return normalise(make_loop(x, fade), -8, rms_db=-25)

def music_box(freq, seconds=2.5):
    # A small music-box tine: bright, quick to fade, with a faint metallic overtone.
    n = int(seconds * SR); tt = np.arange(n) / SR
    x = np.sin(2 * np.pi * freq * tt) + 0.25 * np.sin(2 * np.pi * freq * 3.95 * tt) * decay(n, 0.15)
    return lowpass(x * decay(n, 0.7) * env_adsr(n, 0.003, 0.3), 5000, 1)

def music_lanterns(seconds=120, fade=8):
    # Paper lanterns: a gentle music-box tune in G, a four-bar motif that comes back with small changes, over pads
    # (Gmaj7, Em9, Cmaj9, Dsus) and long breaths of rest.
    n = int((seconds + fade) * SR)
    chords = [
        [('G', 2), ('D', 3), ('F#', 3), ('B', 3)],
        [('E', 2), ('B', 2), ('G', 3), ('D', 4), ('F#', 4)],
        [('C', 3), ('G', 3), ('B', 3), ('D', 4), ('E', 4)],
        [('D', 3), ('A', 3), ('C', 4), ('G', 4)],
    ]
    seg = 12.0
    pad = pad_bed(chords, seg, n, level=0.1, attack=4.0, release=5.0, cutoff=1600)
    scale = [('G', 5), ('A', 5), ('B', 5), ('D', 6), ('E', 6), ('G', 6)]
    motif = [2, 3, 4, 3, 2, 1, 0, 1]
    tune = np.zeros(n)
    t = 3.0
    while t < seconds + fade - 6:
        phrase = [int(np.clip(m + rng.choice([0, 0, 0, 1, -1]), 0, len(scale) - 1)) for m in motif]
        for j, step in enumerate(phrase):
            name, octv = scale[step]
            place(tune, music_box(hz(name, octv)) * rng.uniform(0.045, 0.06), t + j * 0.55)
        t += len(phrase) * 0.55 + rng.uniform(4.0, 7.0)
    x = reverb(pad + tune, 3.2, 0.45, 4200)
    return normalise(make_loop(x, fade), -8, rms_db=-25)

def music_night(seconds=144, fade=8):
    # Night window: warm, slightly wavering electric-piano chords in E flat, one every 6 seconds, over a slow low line,
    # like a late street seen from a quiet room (Ebmaj9, Cm9, Abmaj7(#11), Bb7sus).
    n = int((seconds + fade) * SR)
    chords = [
        [('D#', 2), ('A#', 3), ('D', 4), ('G', 4), ('F', 4)],
        [('C', 2), ('A#', 3), ('D#', 4), ('G', 4), ('D', 5)],
        [('G#', 1), ('G', 3), ('C', 4), ('D', 4), ('D#', 4)],
        [('A#', 1), ('G#', 3), ('C', 4), ('D#', 4), ('F', 4)],
    ]
    keys = np.zeros(n)
    t = 1.0; k = 0
    while t < seconds + fade - 4:
        chord = chords[(k // 2) % len(chords)]
        for j, (name, octv) in enumerate(chord):
            note = soft_key(hz(name, octv), 6.0) * (0.09 if j == 0 else 0.05)
            place(keys, note, t + j * rng.uniform(0.0, 0.03))
        t += 6.0; k += 1
    tt = np.arange(n) / SR
    keys *= 0.88 + 0.12 * np.sin(2 * np.pi * 4.2 * tt)   # a soft tremolo, like an old electric piano
    pad = pad_bed([c[:1] + c[2:4] for c in chords], 12.0, n, level=0.06, cutoff=900)
    x = reverb(keys + pad, 3.8, 0.42, 2800)
    return normalise(make_loop(x, fade), -8, rms_db=-25)


# ---------- effects ----------
def sfx_select():
    n = int(0.09 * SR); tt = np.arange(n) / SR
    x = (np.sin(2 * np.pi * 1760 * tt) * 0.6 + np.sin(2 * np.pi * 880 * tt) * 0.5) * decay(n, 0.018)
    x += lowpass(white(n), 4000, 1) * decay(n, 0.003) * 0.4
    return fade_edges(normalise(x, -6))

def sfx_grab():
    n = int(0.14 * SR); tt = np.arange(n) / SR
    f = 520 * np.exp(-tt * 3)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * decay(n, 0.04) + np.sin(2 * np.pi * 160 * tt) * decay(n, 0.05) * 0.6
    return fade_edges(normalise(x, -6))

def sweep_noise(seconds, f_start, f_peak, f_end, bandwidth=0.6):
    n = int(seconds * SR); src = white(n); out = np.zeros(n)
    centres = np.concatenate([np.geomspace(f_start, f_peak, n // 2), np.geomspace(f_peak, f_end, n - n // 2)])
    block = 256
    zi = None
    for s in range(0, n, block):
        c = centres[s]; lo, hi = c * (1 - bandwidth / 2), min(c * (1 + bandwidth / 2), SR / 2 - 100)
        b, a = signal.butter(2, [lo / (SR / 2), hi / (SR / 2)], 'band')
        seg = src[s:s + block]
        if zi is None: zi = signal.lfilter_zi(b, a) * 0
        y, zi = signal.lfilter(b, a, seg, zi=zi)
        out[s:s + block] = y
    return out

def sfx_send():
    x = sweep_noise(0.5, 500, 2400, 800) * np.sin(np.linspace(0, np.pi, int(0.5 * SR))) ** 1.5
    return fade_edges(normalise(x, -6))

def sfx_place():
    n = int(0.3 * SR); tt = np.arange(n) / SR
    x = np.sin(2 * np.pi * 105 * tt) * decay(n, 0.07) + lowpass(white(n), 900, 2) * decay(n, 0.02) * 0.8
    return fade_edges(normalise(x, -5))

def chime(notes, gap=0.09, seconds=0.9):
    n = int(seconds * SR); x = np.zeros(n)
    for k, f in enumerate(notes):
        b = bell(f, seconds) * (1 - 0.2 * k); i = int(k * gap * SR); x[i:] += b[:n - i]
    return fade_edges(normalise(reverb(x, 1.2, 0.25), -8))

def sfx_teleport():
    x = sweep_noise(0.32, 2600, 1800, 700, 0.8) * np.sin(np.linspace(0, np.pi, int(0.32 * SR)))
    return fade_edges(normalise(x, -9))

def sfx_turn():
    n = int(0.04 * SR); tt = np.arange(n) / SR
    x = np.sin(2 * np.pi * 1250 * tt) * decay(n, 0.008)
    return fade_edges(normalise(x, -12))

def bubbles(seconds, count, lo=350, hi=1000, rise=1.6):
    n = int(seconds * SR); x = np.zeros(n)
    for _ in range(count):
        m = int(rng.uniform(0.015, 0.04) * SR); tt = np.arange(m) / SR
        f = rng.uniform(lo, hi) * (1 + rise * tt / tt[-1] * 0.5)
        b = np.sin(2 * np.pi * np.cumsum(f) / SR) * decay(m, 0.012)
        i = int(rng.uniform(0, seconds - 0.05) * SR); x[i:i + m] += b * rng.uniform(0.3, 1.0)
    return x

def sfx_row():
    n = int(0.9 * SR)
    splash = lowpass(white(n), 1800, 2) * env_adsr(n, 0.02, 0.75, 2.5)
    swish = sweep_noise(0.9, 400, 900, 300, 0.7) * env_adsr(n, 0.15, 0.6) * 0.6
    x = splash + swish + bubbles(0.9, 14) * 0.25
    return fade_edges(normalise(reverb(x, 0.8, 0.2), -6))

def sfx_spill():
    n = int(1.3 * SR)
    hiss = highpass(white(n), 1800, 2) * env_adsr(n, 0.01, 0.9)
    gate = (lowpass(rng.random(n), 18, 1) > 0.5).astype(float) * 0.7 + 0.3
    gurgle = lowpass(white(n), 500, 2) * env_adsr(n, 0.1, 0.8) * 0.8
    x = hiss * gate * 0.6 + gurgle + bubbles(1.3, 25, 250, 700) * 0.3
    return fade_edges(normalise(x, -6))

def sfx_pour():
    n = int(1.6 * SR)
    stream = lowpass(white(n), 1400, 2) * (0.6 + 0.4 * smooth_noise(n, 6)) * env_adsr(n, 0.08, 0.5)
    x = stream * 0.5 + bubbles(1.6, 60, 380, 1100, 2.2) * 0.45
    return fade_edges(normalise(reverb(x, 0.6, 0.15), -6))


# ---------- the old résumé ----------
def amb_printroom(seconds=30, fade=4):
    n = int((seconds + fade) * SR); tt = np.arange(n) / SR
    room = lowpass(brown(n), 200, 2) * 0.6 + lowpass(pink(n), 1500, 1) * 0.08
    tubes = (np.sin(2 * np.pi * 100 * tt) * 0.4 + np.sin(2 * np.pi * 200 * tt) * 0.2) * 0.012
    # Rain on the window: a soft wash and scattered drops.
    wash = bandpass(white(n), 1500, 6000, 2) * 0.05 * (0.7 + 0.3 * smooth_noise(n, 0.1))
    drops = np.zeros(n)
    for _ in range(int(seconds * 18)):
        m = int(rng.uniform(0.004, 0.012) * SR); i = int(rng.uniform(0, seconds + fade - 0.02) * SR)
        d = highpass(white(m), 2500, 1) * decay(m, 0.003) * rng.uniform(0.2, 1.0)
        drops[i:i + m] += d[:max(0, n - i)]
    drops = lowpass(drops, 7000, 1) * 0.25
    x = room + tubes + wash + drops
    return normalise(make_loop(x, fade), -10, rms_db=-32)

def sfx_shred():
    n = int(1.5 * SR); tt = np.arange(n) / SR
    motor = lowpass(signal.sawtooth(2 * np.pi * 95 * tt) * 0.3 + np.sin(2 * np.pi * 190 * tt) * 0.2, 900, 2) * env_adsr(n, 0.05, 0.25)
    # Paper being cut: dense crackle at the rhythm of the cutters.
    crackle = bandpass(white(n), 1200, 6000, 2) * (rng.random(n) < 0.35)
    rhythm = 0.55 + 0.45 * np.sin(2 * np.pi * 22 * tt) ** 2
    paper = crackle * rhythm * env_adsr(n, 0.12, 0.35) * 0.6
    return fade_edges(normalise(reverb(motor + paper, 0.5, 0.12), -6))

def sfx_unlock():
    return chime([hz('D', 5), hz('F#', 5), hz('A', 5), hz('D', 6)], gap=0.11, seconds=1.6)


# ---------- the rage room ----------
def amb_rageroom(seconds=30, fade=4):
    n = int((seconds + fade) * SR); tt = np.arange(n) / SR
    # A bare storage room after hours: concrete room tone, a buzzing tube light and a ventilation duct.
    room = lowpass(brown(n), 160, 2) * 0.7
    buzz = (np.sin(2 * np.pi * 120 * tt) * 0.5 + np.sin(2 * np.pi * 240 * tt) * 0.3 + np.sin(2 * np.pi * 360 * tt) * 0.12) * 0.02
    buzz *= 0.8 + 0.2 * smooth_noise(n, 0.4)
    duct = bandpass(pink(n), 200, 900, 2) * 0.12 * (0.6 + 0.4 * smooth_noise(n, 0.08))
    x = room + buzz + duct
    return normalise(make_loop(x, fade), -12, rms_db=-33)

def sfx_swing():
    n = int(0.34 * SR)
    x = sweep_noise(0.34, 260, 1500, 380, 0.9) * np.sin(np.linspace(0, np.pi, n)) ** 2
    return fade_edges(normalise(x, -8))

def clicks(seconds, count, lo=1500, hi=6000, spread=1.0):
    n = int(seconds * SR); x = np.zeros(n)
    for _ in range(count):
        m = int(rng.uniform(0.003, 0.01) * SR); i = int(rng.uniform(0, seconds * spread) * SR)
        c = bandpass(white(m), lo, hi, 1) * decay(m, 0.002) * rng.uniform(0.2, 1.0)
        x[i:i + m] += c[:max(0, n - i)]
    return x

def thump(n, freq, seconds):
    tt = np.arange(n) / SR
    return np.sin(2 * np.pi * freq * tt * (1 + 0.6 * np.exp(-tt * 40))) * decay(n, seconds)

def sfx_hit_plastic():
    n = int(0.5 * SR)
    crack = bandpass(white(n), 700, 4500, 2) * decay(n, 0.018)
    x = thump(n, 150, 0.05) * 0.9 + crack * 0.8 + clicks(0.5, 9, 1800, 7000, 0.6) * 0.5 * np.linspace(1, 0.3, n)
    return fade_edges(normalise(reverb(x, 0.5, 0.12), -4))

def sfx_hit_glass():
    n = int(0.9 * SR); x = thump(n, 95, 0.06) * 0.8 + highpass(white(n), 2500, 2) * decay(n, 0.03) * 0.9
    for _ in range(9):
        f = rng.uniform(2600, 6200); i = int(rng.uniform(0.02, 0.5) * SR); b = bell(f, 0.3) * rng.uniform(0.08, 0.25)
        x[i:i + len(b)] += b[:n - i]
    x += clicks(0.9, 14, 3000, 9000, 0.6) * 0.35
    return fade_edges(normalise(reverb(x, 0.7, 0.15), -4))

def sfx_hit_metal():
    n = int(1.2 * SR); clang = metal_hit(1.2, base=rng.uniform(240, 320))
    clang *= decay(n, 0.35)
    x = clang * 0.55 + thump(n, 110, 0.07) * 0.9 + lowpass(white(n), 3500, 1) * decay(n, 0.015) * 0.6
    return fade_edges(normalise(reverb(x, 0.6, 0.12), -4))

def sfx_smash():
    n = int(1.6 * SR)
    boom = thump(n, 60, 0.22) * 1.0
    burst = lowpass(white(n), 3500, 2) * decay(n, 0.12) * 0.9
    debris = clicks(1.6, 60, 1200, 8000, 0.85) * np.exp(-np.arange(n) / (0.5 * SR)) * 0.8
    x = boom + burst + debris
    for _ in range(6):
        f = rng.uniform(2400, 5600); i = int(rng.uniform(0.05, 0.7) * SR); b = bell(f, 0.35) * rng.uniform(0.05, 0.15)
        x[i:i + len(b)] += b[:n - i]
    return fade_edges(normalise(reverb(x, 0.9, 0.18), -3))

def sfx_cart():
    n = int(1.8 * SR); tt = np.arange(n) / SR
    rumble = lowpass(brown(n), 140, 2) * 0.8
    bumps = (np.sin(2 * np.pi * 6.5 * tt) > 0.92).astype(float)
    tick = bandpass(white(n), 600, 2400, 1) * lowpass(bumps, 60, 1) * 2.5
    squeak = np.sin(2 * np.pi * (1900 + 120 * np.sin(2 * np.pi * 3 * tt)) * tt) * 0.03 * (np.sin(2 * np.pi * 1.1 * tt) > 0.6)
    x = (rumble + tick + squeak) * env_adsr(n, 0.35, 0.6, 2)
    return fade_edges(normalise(reverb(x, 0.6, 0.15), -8))

def sfx_calm():
    n = int(4.0 * SR)
    x = (pad_note(hz('D', 3), n) * 0.6 + pad_note(hz('A', 3), n) * 0.5 + pad_note(hz('F#', 4), n) * 0.25) * env_adsr(n, 1.2, 2.2, 2)
    x = lowpass(x, 2200, 1)
    return fade_edges(normalise(reverb(x, 2.2, 0.35), -10))

def sfx_clatter():
    # A loose piece knocked along by the bat: a light tap and a short rattle.
    n = int(0.3 * SR)
    tap = bandpass(white(n), 1200, 6000, 2) * decay(n, 0.006)
    x = tap * 0.9 + thump(n, 320, 0.02) * 0.35 + clicks(0.3, 5, 2000, 8000, 0.7) * 0.5
    return fade_edges(normalise(reverb(x, 0.3, 0.08), -9))

def sfx_hit_paper():
    # A stack of paper struck: a soft thud, then sheets riffling apart.
    n = int(0.9 * SR)
    riffle = bandpass(white(n), 1500, 7000, 2) * decay(n, 0.25) * (0.6 + 0.4 * smooth_noise(n, 25))
    x = thump(n, 110, 0.04) * 0.6 + riffle * 0.7 + clicks(0.9, 12, 2500, 9000, 0.7) * 0.25
    return fade_edges(normalise(reverb(x, 0.4, 0.1), -6))


# ---------- the sea: swell, lapping, night wind, gulls, a bell buoy and an oarlock ----------
def amb_sealapping(seconds=28, fade=4):
    """Small waves slapping and gurgling against a hull, in a slow swell. Plays under the main ocean swell."""
    n = int((seconds + fade) * SR)
    swell = lowpass(smooth_noise(n, 0.18), 1.0, 1)
    x = np.zeros(n)
    t = 0.2
    while t < seconds + fade - 0.8:
        # Slaps come closer together and harder as the swell lifts. Each is soft: a rounded burst of water noise, not a click.
        level = 0.3 + 0.7 * float(swell[min(n - 1, int(t * SR))])
        m = int(rng.uniform(0.45, 0.9) * SR); tt = np.arange(m) / SR
        slap = lowpass(bandpass(white(m), 350, 1700, 2), 1900, 1) * decay(m, rng.uniform(0.1, 0.22)) * env_adsr(m, 0.035, 0.2, 2)
        body = np.sin(2 * np.pi * rng.uniform(70, 120) * tt * (1 + 0.4 * np.exp(-tt * 10))) * decay(m, 0.14) * env_adsr(m, 0.02, 0.1, 2)
        grain = lowpass(bubbles(m / SR, int(rng.integers(4, 12)), 260, 800, 1.1), 1500, 1) * 0.6 * decay(m, 0.3)
        hit = (slap * 0.9 + body * 0.35 + grain) * level * rng.uniform(0.5, 1.0)
        i = int(t * SR); x[i:i + m] += hit[:max(0, n - i)]
        t += rng.uniform(0.4, 2.0) * (1.3 - 0.55 * level)
    wash = lowpass(brown(n), 380, 2) * (0.2 + 0.4 * swell)
    return normalise(make_loop(x * 0.9 + wash * 0.5, fade), -8, rms_db=-28)

def amb_nightsea(seconds=32, fade=4):
    """Night on open water: low wind with slow gusts, a deep swell and a few drops of lapping."""
    n = int((seconds + fade) * SR)
    gust = 0.35 + 0.65 * smooth_noise(n, 0.12)
    wind = bandpass(pink(n), 180, 1100, 2) * gust * 0.5
    whistle = bandpass(white(n), 1400, 2100, 2) * (0.04 + 0.1 * smooth_noise(n, 0.07) ** 3)
    deep = lowpass(brown(n), 140, 2) * (0.5 + 0.5 * smooth_noise(n, 0.15))
    rigging = np.zeros(n)
    for _ in range(int(seconds * 0.35)):
        m = int(rng.uniform(0.05, 0.12) * SR); i = int(rng.uniform(0, seconds + fade - 0.2) * SR)
        c = bandpass(white(m), 900, 3200, 1) * decay(m, 0.02) * rng.uniform(0.2, 0.8)
        rigging[i:i + m] += c[:max(0, n - i)]
    x = wind + whistle * 0.5 + deep * 0.7 + rigging * 0.12
    return normalise(make_loop(x, fade), -9, rms_db=-27)

def gull_cry(seconds, base, spread):
    n = int(seconds * SR); tt = np.arange(n) / SR; p = tt / seconds
    # A cry that climbs fast, then slides down: harmonics over a wobbling pitch, with a little breath.
    f = base * (1 + 0.55 * np.sin(np.pi * np.minimum(1, p * 2.2)) - 0.28 * p) * (1 + 0.018 * np.sin(2 * np.pi * 26 * tt))
    phase = np.cumsum(f) / SR * 2 * np.pi
    x = sum(np.sin(k * phase) / k ** 1.3 for k in range(1, 7))
    x = x * np.sin(np.pi * np.clip(p, 0, 1)) ** 0.7 * decay(n, seconds * spread)
    x += bandpass(white(n), 1500, 4500, 1) * 0.04 * np.sin(np.pi * np.clip(p, 0, 1))
    return x

def sfx_gull(far=False):
    n = int((2.4 if far else 1.7) * SR); x = np.zeros(n)
    t = 0.05
    for k in range(4 if far else 3):
        d = rng.uniform(0.32, 0.5) if not far else rng.uniform(0.4, 0.6)
        c = gull_cry(d, rng.uniform(1000, 1250) * (0.8 if far else 1.0), 0.8) * (1 - 0.15 * k)
        i = int(t * SR); x[i:i + len(c)] += c[:max(0, n - i)]
        t += d + rng.uniform(0.06, 0.2)
    if far:
        x = lowpass(x, 2800, 2)
    return fade_edges(normalise(reverb(x, 1.4 if far else 0.6, 0.35 if far else 0.12), -9 if not far else -14))

def sfx_buoy():
    """A bell buoy heard from a distance: a few irregular strokes of a dull iron bell."""
    n = int(7.0 * SR); x = np.zeros(n)
    for t, amp in [(0.0, 1.0), (1.9, 0.75), (3.4, 0.9), (5.0, 0.55)]:
        b = bell(rng.uniform(370, 400), 3.2); h = bell(rng.uniform(595, 620), 2.2) * 0.5; b[:len(h)] += h
        i = int(t * SR); x[i:i + len(b)] += b[:max(0, n - i)] * amp
    x = lowpass(x, 3200, 2)
    return fade_edges(normalise(reverb(x, 2.6, 0.45, 2500), -10))

def sfx_oarlock():
    """Wood turning in a metal lock: a short, dry creak and a soft knock."""
    n = int(0.55 * SR); tt = np.arange(n) / SR; p = tt / (n / SR)
    f = 260 + 90 * np.sin(2 * np.pi * 2.2 * tt) + 60 * p
    creak = signal.sawtooth(2 * np.pi * np.cumsum(f) / SR) * (0.5 + 0.5 * np.sin(2 * np.pi * 38 * tt))
    creak = bandpass(creak, 300, 1600, 2) * np.sin(np.pi * np.clip(p, 0, 1)) ** 1.5 * 0.5
    knock = thump(n, 150, 0.025) * 0.5
    return fade_edges(normalise(reverb(creak + knock, 0.3, 0.1), -10))


def sfx_xray(on=True):
    """The mirror's X-ray: a cold electric hum climbing (on) or sinking (off), with a high shimmer and a few ticks of static."""
    seconds = 1.25; n = int(seconds * SR); tt = np.arange(n) / SR; p = tt / seconds
    f = 70 * (5.0 ** p) if on else 70 * (5.0 ** (1 - p))
    ph = np.cumsum(f) / SR
    hum = np.sin(2 * np.pi * ph) + 0.5 * np.sin(4 * np.pi * ph) + 0.25 * np.sin(6 * np.pi * ph)
    sf = (1800 + 1700 * p) if on else (3500 - 1700 * p)
    shimmer = np.sin(2 * np.pi * np.cumsum(sf) / SR) * (0.5 + 0.5 * np.sin(2 * np.pi * 13 * tt)) * 0.22
    static = bandpass(white(n), 2500, 7000, 2) * (rng.random(n) < 0.05) * 0.5
    if on: env = np.clip(p / 0.85, 0, 1) ** 1.5 * np.clip((1 - p) / 0.15, 0, 1)
    else: env = np.clip((1 - p) / 0.9, 0, 1) ** 1.3 * np.clip(p / 0.03, 0, 1)
    x = (lowpass(hum, 2200, 2) * 0.6 + shimmer + static) * env
    return fade_edges(normalise(reverb(x, 0.6, 0.18), -8))


# ---------- waves that roll in time with the water, and surf on the shore ----------
WAVE_PERIOD = 8.0   # seconds from one wave to the next; the water in the game rolls with this same beat (see OceanWaves)
WAVE_CREST = 3.3    # seconds into each wave when it peaks, and when the crest passes the boat

def amb_waves(seconds=32, fade=4):
    """Rolling waves at a steady 8 second beat, four to the loop: a low swell lifts, the crest hisses past, and the water drains away
    with a trickle. The loop is a whole number of waves long, so the game can keep the moving water in step with it."""
    n = int((seconds + fade) * SR); tt = np.arange(n) / SR
    u = np.mod(tt, WAVE_PERIOD); k = np.floor(tt / WAVE_PERIOD).astype(int) % 4
    amp = np.array([1.0, 0.82, 0.94, 0.74])[k]
    lift = np.where(u < WAVE_CREST, (u / WAVE_CREST) ** 1.8, np.clip(1 - (u - WAVE_CREST) / (WAVE_PERIOD - WAVE_CREST), 0, 1) ** 1.5) * amp
    rumble = lowpass(brown(n), 260, 2) * 1.0 + lowpass(pink(n), 650, 1) * 0.35
    # The crest: foam hissing past as the wave tips over, then thinning as it moves on.
    onset = np.clip((u - (WAVE_CREST - 0.35)) / 0.7, 0, 1) ** 2
    tail = np.exp(-np.clip(u - (WAVE_CREST + 0.2), 0, None) / 1.25) * np.clip(1 - (u - 6.4) / 1.4, 0, 1)
    hiss_env = onset * tail * amp
    hiss = bandpass(white(n), 1700, 7200, 2) * (0.65 + 0.35 * smooth_noise(n, 8))
    # The draw back: a softer, lower fizz and a scatter of small bubbles while the water runs off.
    back_env = np.clip(np.sin(np.pi * np.clip((u - 4.1) / 3.6, 0, 1)), 0, 1) ** 1.4 * amp
    back = bandpass(pink(n), 500, 2600, 2) * back_env
    pops = lowpass(bubbles(seconds + fade, int(46 * (seconds + fade) / 8), 320, 1500, 1.2), 3200, 1) * back_env
    x = rumble * (0.2 + 0.8 * lift) + hiss * hiss_env * 0.5 + back * 0.32 + pops * 0.42
    return normalise(make_loop(x, fade), -4, rms_db=-20)

def amb_surf(seconds=32, fade=4):
    """Surf breaking on a beach, heard from out at sea: now and then a low thud and a long hiss of foam as a wave falls, and the water
    sliding back down the sand. Played from the shore, so it swells as the boat gets nearer."""
    n = int((seconds + fade) * SR)
    x = np.zeros(n)
    t = 0.8
    while t < seconds + fade - 3.5:
        m = int(rng.uniform(4.6, 6.0) * SR); tt = np.arange(m) / SR
        strength = rng.uniform(0.65, 1.0)
        fall = bandpass(white(m), 350, 4200, 2) * env_adsr(m, 0.35, 2.4, 2.0) * decay(m, 1.5)
        foam = highpass(white(m), 2600, 1) * env_adsr(m, 0.5, 3.0, 1.6) * decay(m, 2.2) * 0.38
        thud = np.sin(2 * np.pi * rng.uniform(46, 70) * tt * (1 + 0.5 * np.exp(-tt * 5))) * decay(m, 0.5) * env_adsr(m, 0.12, 0.3, 2) * 0.9
        drain = lowpass(pink(m), 1100, 1) * np.clip(np.sin(np.pi * np.clip((tt - 1.6) / 3.0, 0, 1)), 0, 1) ** 1.3 * 0.35
        hit = (fall + foam + thud + drain) * strength
        i = int(t * SR); x[i:i + m] += hit[:max(0, n - i)]
        t += rng.uniform(6.5, 11.0)
    bed = lowpass(brown(n), 320, 2) * (0.22 + 0.2 * smooth_noise(n, 0.12)) + bandpass(pink(n), 700, 3000, 1) * 0.04
    return normalise(make_loop(x + bed, fade), -4, rms_db=-19)


# ---------- a quiet easter egg: confetti ----------
def sfx_confetti():
    # A party popper: a sharp pop, streamers hissing out, a few bright notes climbing over it, and paper bits fluttering down.
    n = int(1.8 * SR)
    pop = lowpass(white(n), 2600, 2) * decay(n, 0.012) + thump(n, 210, 0.04) * 0.9
    hiss = bandpass(white(n), 3000, 9000, 2) * decay(n, 0.22) * env_adsr(n, 0.01, 0.4) * 0.5
    flutter = clicks(1.8, 70, 2500, 9500, 1.0) * decay(n, 0.7) * 0.6
    notes = np.zeros(n)
    for k, f in enumerate([hz('C', 6), hz('E', 6), hz('G', 6), hz('C', 7)]):
        b = bell(f, 1.2) * 0.18; i = int((0.05 + k * 0.07) * SR); notes[i:i + len(b)] += b[:max(0, n - i)]
    x = pop + hiss + flutter + notes
    return fade_edges(normalise(reverb(x, 0.5, 0.12), -5))


def sfx_blast():
    # Two boxes slammed together and bursting: a sharp crack, a deep boom that falls away, a rush of sparks, and bits of box clattering down.
    n = int(2.2 * SR)
    crack = lowpass(white(n), 5200, 2) * decay(n, 0.03) * 1.0
    boom = thump(n, 52, 0.34) * 1.1 + thump(n, 96, 0.14) * 0.5
    rumble = lowpass(brown(n), 260, 2) * decay(n, 0.5) * 0.5
    sparks = bandpass(white(n), 2500, 8500, 2) * decay(n, 0.28) * 0.45
    debris = clicks(2.2, 90, 900, 7000, 0.9) * np.exp(-np.arange(n) / (0.7 * SR)) * 0.8
    x = crack + boom + rumble + sparks + debris
    return fade_edges(normalise(reverb(x, 0.8, 0.16), -4))


# ---------- the kitchen: a mug breaking, a cupboard door, the steam wand ----------
def sfx_shatter():
    # A mug breaking on something hard: a sharp crack and a hollow knock, a handful of bright ringing pieces, then shards skittering and settling.
    n = int(1.6 * SR)
    crack = highpass(white(n), 1800, 2) * decay(n, 0.02) * 1.0
    knock = thump(n, 180, 0.05) * 0.8 + bandpass(white(n), 500, 2200, 2) * decay(n, 0.06) * 0.6
    ring = np.zeros(n)
    for _ in range(7):
        f = rng.uniform(2200, 5200); i = int(rng.uniform(0.0, 0.18) * SR); b = bell(f, 0.28) * rng.uniform(0.06, 0.2)
        ring[i:i + len(b)] += b[:n - i]
    shards = clicks(1.6, 45, 1800, 9000, 0.95) * np.exp(-np.arange(n) / (0.45 * SR)) * 0.7
    x = crack + knock + ring + shards
    return fade_edges(normalise(reverb(x, 0.5, 0.12), -4))


def sfx_cabinet():
    # A cupboard door: a short wooden creak as it swings, then a soft knock where the hinge stops it.
    n = int(0.9 * SR); tt = np.arange(n) / SR
    pitch = 220 + 90 * np.sin(2 * np.pi * 3.2 * tt) + 160 * tt
    creak = bandpass(signal.sawtooth(2 * np.pi * np.cumsum(pitch) / SR), 500, 2400, 2) * env_adsr(n, 0.04, 0.3, 2) * (tt < 0.5) * 0.35
    k = int(0.4 * SR); i = int(0.5 * SR)
    knock = np.zeros(n)
    knock[i:i + k] = thump(k, 130, 0.05) * 0.9 + bandpass(white(k), 300, 1800, 2) * decay(k, 0.02) * 0.7
    x = creak + knock
    return fade_edges(normalise(reverb(x, 0.35, 0.1), -6))


def sfx_steam():
    # A steam wand opened: a hiss that rises quickly and holds with a little gurgle of water, then sighs out.
    n = int(2.4 * SR)
    hiss = bandpass(white(n), 2500, 11000, 2) * (0.75 + 0.25 * smooth_noise(n, 14)) * env_adsr(n, 0.12, 0.9, 2)
    gurgle = lowpass(white(n), 500, 2) * (0.5 + 0.5 * smooth_noise(n, 9)) * env_adsr(n, 0.3, 0.8, 2) * 0.25
    x = hiss + gurgle
    return fade_edges(normalise(reverb(x, 0.3, 0.08), -9))


# ---------- the ambush: a boardroom, its lights, a squeaking nose, a balloon, paper torn, a chair pushed back ----------
def amb_boardroom(seconds=30, fade=4):
    # A meeting room with the lights on: a low ventilation rumble, the hum of fluorescent tubes and a wall clock ticking once a second. Cold and a little too quiet.
    n = int((seconds + fade) * SR); tt = np.arange(n) / SR
    room = lowpass(brown(n), 170, 2) * 0.55 + lowpass(pink(n), 1300, 1) * 0.05
    hum = (np.sin(2 * np.pi * 100 * tt) * 0.5 + np.sin(2 * np.pi * 200 * tt) * 0.24 + np.sin(2 * np.pi * 300 * tt) * 0.1) * 0.014
    vent = bandpass(white(n), 600, 3200, 1) * 0.03 * (0.6 + 0.4 * smooth_noise(n, 0.08))
    ticks = np.zeros(n)
    for k in range(int(seconds) + 1):
        m = int(0.02 * SR); i = int(k * SR)
        tick = bandpass(white(m), 1800, 5200, 1) * decay(m, 0.004) * (0.55 if k % 2 == 0 else 0.4)
        ticks[i:i + m] += tick[:max(0, n - i)]
    x = room + hum + vent + ticks * 0.1
    return normalise(make_loop(x, fade), -10, rms_db=-33)

def sfx_lights():
    # The lights coming on: a breaker thunk, then fluorescent tubes stuttering (the same pattern as the flicker on screen) and settling to a steady hum.
    n = int(3.2 * SR)
    x = thump(n, 62, 0.25) * 0.5
    for start, length in [(0.0, 0.07), (0.21, 0.06), (0.57, 0.45)]:
        i = int(start * SR); m = int(length * SR); tt = np.arange(m) / SR
        buzz = np.sin(2 * np.pi * 120 * tt) + 0.5 * np.sin(2 * np.pi * 240 * tt) + 0.3 * white(m)
        x[i:i + m] += lowpass(buzz, 3000, 1) * 0.35 * env_adsr(m, 0.004, 0.02, 1)
        k = int(0.02 * SR); tick = bandpass(white(k), 1500, 6000, 1) * decay(k, 0.004)
        x[i:i + k] += tick * 0.6
    i = int(1.11 * SR); m = n - i; tt = np.arange(m) / SR
    hum = (np.sin(2 * np.pi * 100 * tt) * 0.5 + np.sin(2 * np.pi * 200 * tt) * 0.22) * np.exp(-np.arange(m) / (1.3 * SR)) * 0.3 * env_adsr(m, 0.15, 0.6, 2)
    k = int(0.03 * SR); click = bandpass(white(k), 1200, 5000, 1) * decay(k, 0.006)
    x[i:i + m] += hum; x[i:i + k] += click * 0.7
    return fade_edges(normalise(reverb(x, 0.9, 0.14), -6))

def sfx_squeak():
    # A clown nose: a rubber squeak that rises and falls.
    n = int(0.42 * SR); tt = np.arange(n) / SR
    f = 620 + 950 * np.sin(np.pi * np.clip(tt / 0.4, 0, 1)) + 25 * np.sin(2 * np.pi * 28 * tt)
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = (np.sin(ph) + 0.5 * np.sin(2 * ph) + 0.2 * np.sin(3 * ph)) * env_adsr(n, 0.01, 0.14, 2)
    x = bandpass(x, 400, 4600, 1) + highpass(white(n), 3000, 1) * decay(n, 0.01) * 0.1
    return fade_edges(normalise(x, -7))

def sfx_balloon():
    # A balloon popping: a sharp crack, a papery snap, a puff of air and a few scraps of rubber flapping.
    n = int(0.7 * SR)
    crack = highpass(white(n), 1500, 2) * decay(n, 0.012) * 1.0
    snap = thump(n, 240, 0.02) * 0.7 + bandpass(white(n), 800, 4000, 2) * decay(n, 0.05) * 0.5
    puff = lowpass(white(n), 1800, 2) * decay(n, 0.12) * env_adsr(n, 0.005, 0.3, 2) * 0.35
    flap = clicks(0.7, 6, 1200, 5000, 0.6) * 0.3
    x = crack + snap + puff + flap
    return fade_edges(normalise(reverb(x, 0.4, 0.1), -4))

def sfx_tear():
    # Paper torn: a long ripping rasp with fibres snapping, and a dull thud where it gave way.
    n = int(0.8 * SR)
    rasp = bandpass(white(n), 1200, 7500, 2) * (0.5 + 0.5 * smooth_noise(n, 60)) * env_adsr(n, 0.02, 0.18, 2)
    grit = clicks(0.8, 55, 2500, 9000, 0.85) * 0.55
    x = rasp * 0.7 + grit + thump(n, 130, 0.03) * 0.15
    return fade_edges(normalise(reverb(x, 0.3, 0.08), -6))

def sfx_stand():
    # Standing up: a chair rolling back across carpet, a soft knock where it stops, and a slow low chord like a held breath let go.
    n = int(3.4 * SR); m = int(0.9 * SR); k = int(0.25 * SR); j = int(0.6 * SR)
    roll = np.zeros(n)
    roll[:m] = (lowpass(brown(m), 420, 2) * 0.5 + bandpass(white(m), 900, 3200, 1) * 0.06) * env_adsr(m, 0.05, 0.5, 2)
    knock = np.zeros(n); knock[m:m + k] = thump(k, 120, 0.05) * 0.5
    chord = np.zeros(n)
    for f in [hz('D', 3), hz('A', 3), hz('F#', 4)]:
        chord[j:] += pad_note(f, n - j) * (0.5 if f < 200 else 0.3)
    chord = lowpass(chord * env_adsr(n, 1.2, 1.4, 2), 2000, 1)
    x = roll + knock + chord * 0.5
    return fade_edges(normalise(reverb(x, 1.6, 0.3), -8))


def main(folder):
    os.makedirs(folder, exist_ok=True)
    items = {
        'Ambience - Lobby': amb_lobby, 'Ambience - Ocean': amb_ocean, 'Ambience - Warehouse': amb_warehouse,
        'Ambience - Kitchen': amb_kitchen, 'Ambience - Kitchen murmur': amb_murmur, 'Ambience - Archive': amb_archive,
        'Ambience - Rooftop': amb_rooftop, 'Music - After hours theme': music_theme,
        'SFX - Select': sfx_select, 'SFX - Grab': sfx_grab, 'SFX - Send': sfx_send, 'SFX - Place': sfx_place,
        'SFX - Menu open': lambda: chime([hz('A', 5), hz('E', 6)]), 'SFX - Menu close': lambda: chime([hz('E', 6), hz('A', 5)]),
        'SFX - Teleport': sfx_teleport, 'SFX - Turn': sfx_turn, 'SFX - Row': sfx_row, 'SFX - Spill': sfx_spill, 'SFX - Pour': sfx_pour,
        # Added later: new sounds go last, so the random sequence, and every earlier file, stays the same.
        'Ambience - Print room': amb_printroom, 'SFX - Shred': sfx_shred, 'SFX - Unlock': sfx_unlock,
        'Ambience - Rage room': amb_rageroom, 'SFX - Swing': sfx_swing, 'SFX - Hit plastic': sfx_hit_plastic, 'SFX - Hit glass': sfx_hit_glass,
        'SFX - Hit metal': sfx_hit_metal, 'SFX - Smash': sfx_smash, 'SFX - Cart': sfx_cart, 'SFX - Calm': sfx_calm,
        'SFX - Clatter': sfx_clatter, 'SFX - Hit paper': sfx_hit_paper,
        'Ambience - Sea lapping': amb_sealapping, 'Ambience - Night sea': amb_nightsea,
        'SFX - Gull': sfx_gull, 'SFX - Gull far': lambda: sfx_gull(far=True), 'SFX - Buoy bell': sfx_buoy, 'SFX - Oarlock': sfx_oarlock,
        'SFX - X-ray on': lambda: sfx_xray(True), 'SFX - X-ray off': lambda: sfx_xray(False),
        'Ambience - Waves': amb_waves, 'Ambience - Surf': amb_surf,
        'SFX - Confetti': sfx_confetti,
        'SFX - Blast': sfx_blast,
        'SFX - Shatter': sfx_shatter, 'SFX - Cabinet': sfx_cabinet, 'SFX - Steam': sfx_steam,
        'Ambience - Boardroom': amb_boardroom, 'SFX - Lights': sfx_lights, 'SFX - Squeak': sfx_squeak, 'SFX - Balloon': sfx_balloon, 'SFX - Tear': sfx_tear, 'SFX - Stand': sfx_stand,
        'Music - Quiet hours': music_quiet,
        'Music - Slow tide': music_tide, 'Music - Paper lanterns': music_lanterns, 'Music - Night window': music_night,
    }
    for name, make in items.items():
        x = make(); write(folder, name, x)
        print(f'{name}: {len(x) / SR:.2f} s, peak {20 * np.log10(np.max(np.abs(x)) + 1e-12):.1f} dB, rms {20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12):.1f} dB')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'Audio')
