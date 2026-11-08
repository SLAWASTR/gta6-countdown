package com.slawastr.gta6countdown;

import android.app.Service;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.os.PowerManager;

public class WidgetUpdateService extends Service {

    private final Handler handler = new Handler(Looper.getMainLooper());
    private boolean isScreenOn = true;
    private BroadcastReceiver screenReceiver;

    private final Runnable ticker = new Runnable() {
        @Override
        public void run() {
            if (isScreenOn && WidgetHelper.isLiveSecondsEnabled(WidgetUpdateService.this)) {
                WidgetHelper.updateAllWidgets(WidgetUpdateService.this);
                handler.postDelayed(this, 1000);
            }
        }
    };

    @Override
    public void onCreate() {
        super.onCreate();

        PowerManager pm = (PowerManager) getSystemService(Context.POWER_SERVICE);
        if (pm != null) {
            isScreenOn = pm.isInteractive();
        }

        screenReceiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                if (Intent.ACTION_SCREEN_ON.equals(intent.getAction()) ||
                    Intent.ACTION_USER_PRESENT.equals(intent.getAction())) {
                    isScreenOn = true;
                    handler.removeCallbacks(ticker);
                    handler.post(ticker);
                } else if (Intent.ACTION_SCREEN_OFF.equals(intent.getAction())) {
                    isScreenOn = false;
                    handler.removeCallbacks(ticker);
                }
            }
        };

        IntentFilter filter = new IntentFilter();
        filter.addAction(Intent.ACTION_SCREEN_ON);
        filter.addAction(Intent.ACTION_SCREEN_OFF);
        filter.addAction(Intent.ACTION_USER_PRESENT);
        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU) {
            registerReceiver(screenReceiver, filter, Context.RECEIVER_NOT_EXPORTED);
        } else {
            registerReceiver(screenReceiver, filter);
        }

        if (isScreenOn) {
            handler.post(ticker);
        }
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (isScreenOn) {
            handler.removeCallbacks(ticker);
            handler.post(ticker);
        }
        return START_STICKY;
    }

    @Override
    public void onDestroy() {
        handler.removeCallbacks(ticker);
        if (screenReceiver != null) {
            try {
                unregisterReceiver(screenReceiver);
            } catch (Exception ignored) { }
        }
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }
}
