package com.orbitalrift.musicreactive;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.media.audiofx.Visualizer;
import android.os.Build;
import android.util.Log;

import java.util.Locale;

/**
 * Static boundary between Unity and the foreground capture service. Only six scalar values are
 * retained. Raw PCM never leaves the service thread and is never written to disk.
 */
public final class ExternalAudioCapture {
    private static final String TAG = "OrbitalRiftAudio";
    public static final String ACTION_START = "com.orbitalrift.musicreactive.START";
    public static final String ACTION_STOP = "com.orbitalrift.musicreactive.STOP";
    public static final String EXTRA_RESULT_CODE = "resultCode";
    public static final String EXTRA_PROJECTION_DATA = "projectionData";

    private static volatile boolean active;
    private static volatile float energy;
    private static volatile float bass;
    private static volatile float mid;
    private static volatile float treble;
    private static volatile float beat;
    private static Visualizer outputVisualizer;
    private static byte[] waveformBuffer;
    private static byte[] fftBuffer;
    private static float visualizerSmoothedEnergy;
    private static long visualizerLastBeatAt;

    private ExternalAudioCapture() { }

    public static void requestCapture(final Activity activity) {
        if (activity == null || Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return;
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                Intent request = new Intent(activity, AudioCaptureRequestActivity.class);
                activity.startActivity(request);
            }
        });
    }

    public static void stopCapture(Context context) {
        if (context == null) return;
        Intent stop = new Intent(context, AudioReactiveCaptureService.class);
        stop.setAction(ACTION_STOP);
        context.startService(stop);
        clear();
    }

    /**
     * Android 9-compatible route. Visualizer(session 0) reads the system output mix, so it can
     * see a track that is currently routed to wired/Bluetooth headphones without recording the
     * microphone signal. Android still protects this API with RECORD_AUDIO permission.
     */
    public static synchronized boolean startVisualizer() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.GINGERBREAD) return false;
        stopVisualizer();
        try {
            outputVisualizer = new Visualizer(0);
            int[] range = Visualizer.getCaptureSizeRange();
            int captureSize = 1024;
            // Visualizer accepts only power-of-two capture sizes. Most devices expose
            // [128, 1024], but a few OEMs advertise a different range, so normalize it
            // instead of letting setCaptureSize throw and silently disabling the visualizer.
            while (captureSize > range[1]) captureSize >>= 1;
            while (captureSize < range[0]) captureSize <<= 1;
            captureSize = Math.max(range[0], Math.min(range[1], captureSize));
            outputVisualizer.setCaptureSize(captureSize);
            waveformBuffer = new byte[captureSize];
            fftBuffer = new byte[captureSize];
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.JELLY_BEAN)
                outputVisualizer.setScalingMode(Visualizer.SCALING_MODE_NORMALIZED);
            outputVisualizer.setEnabled(true);
            visualizerSmoothedEnergy = 0f;
            visualizerLastBeatAt = 0L;
            Log.i(TAG, "Visualizer output-mix started, captureSize=" + captureSize);
            return true;
        } catch (RuntimeException failure) {
            Log.w(TAG, "Visualizer output-mix unavailable: " + failure.getClass().getSimpleName() + ": " + failure.getMessage());
            stopVisualizer();
            return false;
        }
    }

    public static synchronized void stopVisualizer() {
        if (outputVisualizer != null) {
            try { outputVisualizer.setEnabled(false); } catch (RuntimeException ignored) { }
            outputVisualizer.release();
            outputVisualizer = null;
        }
        waveformBuffer = null;
        fftBuffer = null;
        clear();
    }

    /** Polls the API-9 Visualizer and returns the same compact frame as playback capture. */
    public static synchronized String pollVisualizerFrame() {
        if (outputVisualizer == null || waveformBuffer == null || fftBuffer == null) return pollFrame();
        try {
            if (outputVisualizer.getWaveForm(waveformBuffer) != Visualizer.SUCCESS) return pollFrame();
            outputVisualizer.getFft(fftBuffer);

            float sumSquares = 0f;
            for (byte value : waveformBuffer) {
                // getWaveForm returns unsigned 8-bit PCM (0..255), centered at 128.
                float sample = ((value & 0xff) - 128) / 128f;
                sumSquares += sample * sample;
            }
            float rms = (float) Math.sqrt(sumSquares / Math.max(1, waveformBuffer.length));
            visualizerSmoothedEnergy = visualizerSmoothedEnergy * .88f + rms * .12f;

            float low = 0f;
            float middle = 0f;
            float high = 0f;
            int bins = Math.max(1, fftBuffer.length / 2);
            for (int bin = 1; bin < bins; bin++) {
                int offset = bin * 2;
                if (offset + 1 >= fftBuffer.length) break;
                float real = fftBuffer[offset] / 128f;
                float imaginary = fftBuffer[offset + 1] / 128f;
                float magnitude = (float) Math.sqrt(real * real + imaginary * imaginary);
                if (bin < 12) low += magnitude;
                else if (bin < 64) middle += magnitude;
                else high += magnitude;
            }

            long now = android.os.SystemClock.elapsedRealtime();
            float nextBeat = 0f;
            if (rms > Math.max(.018f, visualizerSmoothedEnergy * 1.42f) && now - visualizerLastBeatAt > 140L) {
                visualizerLastBeatAt = now;
                nextBeat = Math.min(1f, .35f + Math.max(0f, rms - visualizerSmoothedEnergy) * 3.2f);
            }
            publish(rms * 7.5f, low / Math.max(1, bins) * 18f,
                    middle / Math.max(1, bins) * 11f, high / Math.max(1, bins) * 10f, nextBeat);
            return pollFrame();
        } catch (RuntimeException failure) {
            clear();
            return pollFrame();
        }
    }

    static void publish(float nextEnergy, float nextBass, float nextMid, float nextTreble, float nextBeat) {
        energy = clamp(nextEnergy);
        bass = clamp(nextBass);
        mid = clamp(nextMid);
        treble = clamp(nextTreble);
        beat = clamp(nextBeat);
        active = true;
    }

    static void clear() {
        active = false;
        energy = bass = mid = treble = beat = 0f;
    }

    /** Called by Unity once per render frame. Format is intentionally locale-stable and compact. */
    public static String pollFrame() {
        return String.format(Locale.US, "%d|%.4f|%.4f|%.4f|%.4f|%.4f", active ? 1 : 0,
                energy, bass, mid, treble, beat);
    }

    private static float clamp(float value) {
        return Math.max(0f, Math.min(1f, value));
    }
}
