# The café's old radio (Resources/Audio/CafeRadio.ogg): the café music (CafeMusic.ogg, our own) as if from a small
# valve radio on the counter: the lows and highs gone, a little drive, a soft hiss and crackle, a slow wobble.
#   python3 tools/audio/radio.py CafeMusic.ogg out.wav   (then: ffmpeg -i out.wav -c:a libvorbis -q:a 2 CafeRadio.ogg)
import sys, wave, subprocess
import numpy as np
from scipy.signal import butter, sosfilt

SR = 32000
raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', sys.argv[1], '-ac', '1', '-ar', str(SR), '-f', 's16le', '-'], capture_output=True, check=True).stdout
x = np.frombuffer(raw, np.int16).astype(float) / 32768
rng = np.random.default_rng(5)
x = sosfilt(butter(3, [420, 3400], btype='band', fs=SR, output='sos'), x)
x = np.tanh(x * 2.2) / np.tanh(2.2)                       # a valve's gentle drive
t = np.arange(len(x)) / SR
x *= 0.92 + 0.08 * np.sin(2 * np.pi * 0.23 * t)            # the station drifting a little
hiss = sosfilt(butter(2, [1500, 6000], btype='band', fs=SR, output='sos'), rng.standard_normal(len(x))) * 0.012
crackle = np.zeros(len(x)); idx = rng.integers(0, len(x), len(x) // 900)
crackle[idx] = rng.uniform(-0.25, 0.25, len(idx)); crackle = sosfilt(butter(2, 2500, btype='high', fs=SR, output='sos'), crackle)
y = x * 0.85 + hiss + crackle * 0.5
y /= np.abs(y).max() * 1.1
w = wave.open(sys.argv[2], 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((y * 32767).astype(np.int16).tobytes()); w.close()
print('radio', len(y) / SR, 's')
