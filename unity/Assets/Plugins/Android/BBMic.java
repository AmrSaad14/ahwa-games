// The phone's microphone for table voice chat, opened the way calling apps open it (VOICE_COMMUNICATION), with the
// phone's echo canceller, noise suppressor and level control on: the game's own music and sounds coming out of the
// speaker are removed from what the others hear. Unity's Microphone records the raw mic, which carried them along.
// Scripts/Voice.cs reads the samples every frame (read()).
package com.ahwa.voice;

import android.media.AudioFormat;
import android.media.AudioRecord;
import android.media.MediaRecorder;
import android.media.audiofx.AcousticEchoCanceler;
import android.media.audiofx.AutomaticGainControl;
import android.media.audiofx.NoiseSuppressor;

public class BBMic {
    static AudioRecord rec;
    static AcousticEchoCanceler aec;
    static NoiseSuppressor ns;
    static AutomaticGainControl agc;
    static short[] buf = new short[9600];
    static float[] out = new float[0];
    public static final int RATE = 48000;

    /** Starts recording; true when it works (the RECORD_AUDIO permission must be granted first). */
    public static boolean start() {
        stop();
        try {
            int min = AudioRecord.getMinBufferSize(RATE, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT);
            rec = new AudioRecord(MediaRecorder.AudioSource.VOICE_COMMUNICATION, RATE, AudioFormat.CHANNEL_IN_MONO,
                AudioFormat.ENCODING_PCM_16BIT, Math.max(min * 4, RATE / 5 * 2));
            if (rec.getState() != AudioRecord.STATE_INITIALIZED) { stop(); return false; }
            int id = rec.getAudioSessionId();
            if (AcousticEchoCanceler.isAvailable()) { aec = AcousticEchoCanceler.create(id); if (aec != null) aec.setEnabled(true); }
            if (NoiseSuppressor.isAvailable()) { ns = NoiseSuppressor.create(id); if (ns != null) ns.setEnabled(true); }
            if (AutomaticGainControl.isAvailable()) { agc = AutomaticGainControl.create(id); if (agc != null) agc.setEnabled(true); }
            rec.startRecording();
            return true;
        } catch (Throwable t) { stop(); return false; }
    }

    /** Whether the phone's echo canceller is on (for the log). */
    public static boolean echoCancel() { return aec != null && aec.getEnabled(); }

    /** The samples recorded since the last call, as floats in -1…1 (empty if none). Never blocks. */
    public static float[] read() {
        if (rec == null) return new float[0];
        int n;
        try { n = rec.read(buf, 0, buf.length, AudioRecord.READ_NON_BLOCKING); } catch (Throwable t) { return new float[0]; }
        if (n <= 0) return new float[0];
        if (out.length != n) out = new float[n];
        for (int i = 0; i < n; i++) out[i] = buf[i] / 32768f;
        return out;
    }

    public static void stop() {
        try { if (rec != null) { rec.stop(); rec.release(); } } catch (Throwable ignored) { }
        try { if (aec != null) aec.release(); if (ns != null) ns.release(); if (agc != null) agc.release(); } catch (Throwable ignored) { }
        rec = null; aec = null; ns = null; agc = null;
    }
}
