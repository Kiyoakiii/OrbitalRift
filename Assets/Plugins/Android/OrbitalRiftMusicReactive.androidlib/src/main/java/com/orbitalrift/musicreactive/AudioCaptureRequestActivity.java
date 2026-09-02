package com.orbitalrift.musicreactive;

import android.Manifest;
import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.Bundle;

/** A short-lived transparent activity used solely for Android's mandatory permission flows. */
public final class AudioCaptureRequestActivity extends Activity {
    private static final int REQUEST_RECORD_AUDIO = 4221;
    private static final int REQUEST_MEDIA_PROJECTION = 4222;

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
            finish();
            return;
        }
        requestRecordPermissionOrProjection();
    }

    @Override public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != REQUEST_RECORD_AUDIO) return;
        if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) requestProjection();
        else finish();
    }

    @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == REQUEST_MEDIA_PROJECTION && resultCode == RESULT_OK && data != null) {
            Intent start = new Intent(this, AudioReactiveCaptureService.class);
            start.setAction(ExternalAudioCapture.ACTION_START);
            start.putExtra(ExternalAudioCapture.EXTRA_RESULT_CODE, resultCode);
            start.putExtra(ExternalAudioCapture.EXTRA_PROJECTION_DATA, data);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) startForegroundService(start);
            else startService(start);
        }
        finish();
    }

    private void requestRecordPermissionOrProjection() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M &&
                checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(new String[] { Manifest.permission.RECORD_AUDIO }, REQUEST_RECORD_AUDIO);
            return;
        }
        requestProjection();
    }

    private void requestProjection() {
        MediaProjectionManager manager = (MediaProjectionManager) getSystemService(MEDIA_PROJECTION_SERVICE);
        if (manager == null) {
            finish();
            return;
        }
        startActivityForResult(manager.createScreenCaptureIntent(), REQUEST_MEDIA_PROJECTION);
    }
}
