package com.orbitalrift.musicreactive;

import android.app.Activity;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioRecord;
import android.media.MediaRecorder;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.IBinder;

/** Foreground service that turns permitted playback audio into transient visualizer metrics. */
public final class AudioReactiveCaptureService extends Service {
    private static final int NOTIFICATION_ID = 7814;
    private static final String CHANNEL_ID = "orbital_rift_music_visualizer";
    private static final int SAMPLE_RATE = 48000;

    private volatile boolean running;
    private AudioRecord recorder;
    private MediaProjection projection;
    private Thread analysisThread;
    private float smoothedEnergy;
    private float beatValue;
    private long lastBeatAt;

    @Override public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent == null || ExternalAudioCapture.ACTION_STOP.equals(intent.getAction())) {
            stopCapture();
            stopSelf();
            return START_NOT_STICKY;
        }
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return START_NOT_STICKY;
        int resultCode = intent.getIntExtra(ExternalAudioCapture.EXTRA_RESULT_CODE, Activity.RESULT_CANCELED);
        Intent resultData = intent.getParcelableExtra(ExternalAudioCapture.EXTRA_PROJECTION_DATA);
        if (resultCode != Activity.RESULT_OK || resultData == null) {
            stopCapture();
            stopSelf();
            return START_NOT_STICKY;
        }
        startCapture(resultCode, resultData);
        return START_NOT_STICKY;
    }

    @Override public void onDestroy() {
        stopCapture();
        super.onDestroy();
    }

    @Override public IBinder onBind(Intent intent) { return null; }

    private void startAsForeground() {
        NotificationManager manager = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && manager != null) {
            NotificationChannel channel = new NotificationChannel(CHANNEL_ID, "Orbital Rift music visuals",
                    NotificationManager.IMPORTANCE_LOW);
            channel.setDescription("External audio is analyzed locally for game visual effects.");
            manager.createNotificationChannel(channel);
        }
        Notification.Builder builder = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? new Notification.Builder(this, CHANNEL_ID) : new Notification.Builder(this);
        Notification notification = builder
                .setSmallIcon(android.R.drawable.ic_media_play)
                .setContentTitle("Orbital Rift: music visuals")
                .setContentText("Audio is analyzed locally for visual effects")
                .setOngoing(true)
                .build();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) startForeground(NOTIFICATION_ID, notification,
                android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION);
        else startForeground(NOTIFICATION_ID, notification);
    }

    private void startCapture(int resultCode, Intent resultData) {
        stopCapture();
        // Android 10+ requires the media-projection service type before we use the projection.
        // stopCapture() removes an older notification, so establish the foreground state again.
        startAsForeground();
        MediaProjectionManager manager = (MediaProjectionManager) getSystemService(MEDIA_PROJECTION_SERVICE);
        if (manager == null) return;
        projection = manager.getMediaProjection(resultCode, resultData);
        if (projection == null) return;
        projection.registerCallback(new MediaProjection.Callback() {
            @Override public void onStop() {
                stopCapture();
                stopSelf();
            }
        }, null);

        AudioFormat format = new AudioFormat.Builder()
                .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                .setSampleRate(SAMPLE_RATE)
                .setChannelMask(AudioFormat.CHANNEL_IN_MONO)
                .build();
        android.media.AudioPlaybackCaptureConfiguration configuration =
                new android.media.AudioPlaybackCaptureConfiguration.Builder(projection)
                        .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
                        .addMatchingUsage(AudioAttributes.USAGE_GAME)
                        .build();
        int minBuffer = AudioRecord.getMinBufferSize(SAMPLE_RATE, AudioFormat.CHANNEL_IN_MONO,
                AudioFormat.ENCODING_PCM_16BIT);
        int bufferSize = Math.max(8192, minBuffer > 0 ? minBuffer * 2 : 8192);
        recorder = new AudioRecord.Builder()
                .setAudioFormat(format)
                .setBufferSizeInBytes(bufferSize)
                .setAudioPlaybackCaptureConfig(configuration)
                .build();
        running = true;
        analysisThread = new Thread(new Runnable() {
            @Override public void run() { analyzeLoop(); }
        }, "OrbitalRiftAudioVisuals");
        analysisThread.start();
    }

    private void analyzeLoop() {
        short[] samples = new short[1024];
        AudioRecord activeRecorder = recorder;
        if (activeRecorder == null) return;
        try {
            activeRecorder.startRecording();
            while (running) {
                int count = activeRecorder.read(samples, 0, samples.length, AudioRecord.READ_BLOCKING);
                if (count > 0) publishMetrics(samples, count);
            }
        } catch (RuntimeException ignored) {
            // Projection withdrawal and playback opt-out both arrive here on some device vendors.
        } finally {
            ExternalAudioCapture.clear();
        }
    }

    private void publishMetrics(short[] samples, int count) {
        float sumSquares = 0f;
        float lowState = 0f;
        float midState = 0f;
        float lowSum = 0f;
        float midSum = 0f;
        float highSum = 0f;
        for (int i = 0; i < count; i++) {
            float sample = samples[i] / 32768f;
            sumSquares += sample * sample;
            lowState += (sample - lowState) * .045f;
            float highPass = sample - lowState;
            midState += (highPass - midState) * .20f;
            lowSum += Math.abs(lowState);
            midSum += Math.abs(midState);
            highSum += Math.abs(highPass - midState);
        }
        float rms = (float) Math.sqrt(sumSquares / Math.max(1, count));
        smoothedEnergy = smoothedEnergy * .90f + rms * .10f;
        long now = android.os.SystemClock.elapsedRealtime();
        float beatTrigger = rms > smoothedEnergy * 1.42f && rms > .018f && now - lastBeatAt > 120 ? 1f : 0f;
        if (beatTrigger > 0f) lastBeatAt = now;
        beatValue = Math.max(beatTrigger, beatValue * .82f);
        ExternalAudioCapture.publish(rms * 7.5f, lowSum / count * 10.5f,
                midSum / count * 10.5f, highSum / count * 10.5f, beatValue);
    }

    private void stopCapture() {
        running = false;
        if (recorder != null) {
            try { recorder.stop(); } catch (IllegalStateException ignored) { }
            recorder.release();
            recorder = null;
        }
        if (projection != null) {
            // Clear the field first: MediaProjection invokes its callback from stop(), and that
            // callback also calls stopCapture(). This avoids a recursive double-stop.
            MediaProjection previousProjection = projection;
            projection = null;
            previousProjection.stop();
        }
        analysisThread = null;
        ExternalAudioCapture.clear();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) stopForeground(STOP_FOREGROUND_REMOVE);
        else stopForeground(true);
    }
}
