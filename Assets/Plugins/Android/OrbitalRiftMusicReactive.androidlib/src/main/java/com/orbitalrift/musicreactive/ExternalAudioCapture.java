package com.orbitalrift.musicreactive;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Build;

import java.util.Locale;

/**
 * Static boundary between Unity and the foreground capture service. Only six scalar values are
 * retained. Raw PCM never leaves the service thread and is never written to disk.
 */
public final class ExternalAudioCapture {
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
