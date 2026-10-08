"""Original sound design for After Hours, synthesised from scratch (no samples, no third-party audio).

Run: python3 make_audio.py <output folder>
Writes 32 kHz mono 16-bit WAV files. Ambience and music loop seamlessly.
"""
import os, sys
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
    }
    for name, make in items.items():
        x = make(); write(folder, name, x)
        print(f'{name}: {len(x) / SR:.2f} s, peak {20 * np.log10(np.max(np.abs(x)) + 1e-12):.1f} dB, rms {20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12):.1f} dB')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'Audio')
