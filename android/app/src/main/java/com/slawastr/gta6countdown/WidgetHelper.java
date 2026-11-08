package com.slawastr.gta6countdown;

import android.app.AlarmManager;
import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;
import android.view.View;
import android.widget.RemoteViews;

import java.util.Calendar;
import java.util.TimeZone;

public class WidgetHelper {

    public static final String PREFS_NAME = "gta6_prefs";
    public static final String PREF_HIDE_PRELOAD = "hide_preload";
    public static final String ACTION_REFRESH = "com.slawastr.gta6countdown.ACTION_REFRESH";

    // Target timestamps (MSK: UTC+3)
    // 2026-11-18 21:30:00 -> 1795026600000 ms
    // 2026-11-09 21:30:00 -> 1794249000000 ms
    public static final long TARGET_RELEASE_MS = 1795026600000L;
    public static final long TARGET_PRELOAD_MS = 1794249000000L;

    public static class TimeParts {
        public boolean isDone;
        public long days;
        public long hours;
        public long mins;
        public long secs;

        public String daysStr() { return String.format("%02d", Math.max(0, days)); }
        public String hoursStr() { return String.format("%02d", Math.max(0, hours)); }
        public String minsStr() { return String.format("%02d", Math.max(0, mins)); }
        public String secsStr() { return String.format("%02d", Math.max(0, secs)); }
    }

    public static TimeParts calculateTimeParts(long targetMs, long nowMs) {
        TimeParts parts = new TimeParts();
        long diff = targetMs - nowMs;
        if (diff <= 0) {
            parts.isDone = true;
            parts.days = 0;
            parts.hours = 0;
            parts.mins = 0;
            parts.secs = 0;
            return parts;
        }

        parts.isDone = false;
        parts.days = diff / (24L * 3600L * 1000L);
        long remDays = diff % (24L * 3600L * 1000L);
        parts.hours = remDays / (3600L * 1000L);
        long remHours = remDays % (3600L * 1000L);
        parts.mins = remHours / (60L * 1000L);
        long remMins = remHours % (60L * 1000L);
        parts.secs = remMins / 1000L;
        return parts;
    }

    public static boolean isPreloadHidden(Context context) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        return prefs.getBoolean(PREF_HIDE_PRELOAD, false);
    }

    public static void setPreloadHidden(Context context, boolean hide) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().putBoolean(PREF_HIDE_PRELOAD, hide).apply();
        updateAllWidgets(context);
    }

    public static void updateAllWidgets(Context context) {
        AppWidgetManager manager = AppWidgetManager.getInstance(context);

        // Update 2x2 widgets
        ComponentName comp2x2 = new ComponentName(context, WidgetProvider2x2.class);
        int[] ids2x2 = manager.getAppWidgetIds(comp2x2);
        for (int id : ids2x2) {
            updateWidget2x2(context, manager, id);
        }

        // Update 1x2 widgets
        ComponentName comp1x2 = new ComponentName(context, WidgetProvider1x2.class);
        int[] ids1x2 = manager.getAppWidgetIds(comp1x2);
        for (int id : ids1x2) {
            updateWidget1x2(context, manager, id);
        }

        scheduleNextAlarm(context);
    }

    public static void updateWidget2x2(Context context, AppWidgetManager manager, int appWidgetId) {
        RemoteViews views = new RemoteViews(context.getPackageName(), R.layout.widget_2x2);
        long now = System.currentTimeMillis();

        // Release calculation
        TimeParts rel = calculateTimeParts(TARGET_RELEASE_MS, now);
        if (rel.isDone) {
            views.setViewVisibility(R.id.grid_rel_digits, View.GONE);
            views.setViewVisibility(R.id.txt_rel_done, View.VISIBLE);
        } else {
            views.setViewVisibility(R.id.grid_rel_digits, View.VISIBLE);
            views.setViewVisibility(R.id.txt_rel_done, View.GONE);
            views.setTextViewText(R.id.txt_rel_days, rel.daysStr());
            views.setTextViewText(R.id.txt_rel_hours, rel.hoursStr());
            views.setTextViewText(R.id.txt_rel_mins, rel.minsStr());
        }

        // Preload calculation & toggle
        boolean hidePreload = isPreloadHidden(context);
        if (hidePreload) {
            views.setViewVisibility(R.id.layout_preload, View.GONE);
        } else {
            views.setViewVisibility(R.id.layout_preload, View.VISIBLE);
            TimeParts pre = calculateTimeParts(TARGET_PRELOAD_MS, now);
            if (pre.isDone) {
                views.setViewVisibility(R.id.grid_pre_digits, View.GONE);
                views.setViewVisibility(R.id.txt_pre_done, View.VISIBLE);
            } else {
                views.setViewVisibility(R.id.grid_pre_digits, View.VISIBLE);
                views.setViewVisibility(R.id.txt_pre_done, View.GONE);
                views.setTextViewText(R.id.txt_pre_days, pre.daysStr());
                views.setTextViewText(R.id.txt_pre_hours, pre.hoursStr());
                views.setTextViewText(R.id.txt_pre_mins, pre.minsStr());
            }
        }

        // Click on widget opens MainActivity
        Intent intent = new Intent(context, MainActivity.class);
        PendingIntent pendingIntent = PendingIntent.getActivity(context, 0, intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        views.setOnClickPendingIntent(R.id.widget_root, pendingIntent);

        // Click on refresh button
        Intent refreshIntent = new Intent(context, WidgetProvider2x2.class);
        refreshIntent.setAction(ACTION_REFRESH);
        PendingIntent refreshPending = PendingIntent.getBroadcast(context, 101, refreshIntent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        views.setOnClickPendingIntent(R.id.btn_refresh, refreshPending);

        manager.updateAppWidget(appWidgetId, views);
    }

    public static void updateWidget1x2(Context context, AppWidgetManager manager, int appWidgetId) {
        RemoteViews views = new RemoteViews(context.getPackageName(), R.layout.widget_1x2);
        long now = System.currentTimeMillis();

        TimeParts rel = calculateTimeParts(TARGET_RELEASE_MS, now);
        if (rel.isDone) {
            views.setViewVisibility(R.id.layout_1x2_digits, View.GONE);
            views.setViewVisibility(R.id.txt_1x2_done, View.VISIBLE);
        } else {
            views.setViewVisibility(R.id.layout_1x2_digits, View.VISIBLE);
            views.setViewVisibility(R.id.txt_1x2_done, View.GONE);
            views.setTextViewText(R.id.txt_1x2_days, rel.daysStr());
            views.setTextViewText(R.id.txt_1x2_hours, rel.hoursStr());
            views.setTextViewText(R.id.txt_1x2_mins, rel.minsStr());
        }

        Intent intent = new Intent(context, MainActivity.class);
        PendingIntent pendingIntent = PendingIntent.getActivity(context, 0, intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        views.setOnClickPendingIntent(R.id.widget_1x2_root, pendingIntent);

        manager.updateAppWidget(appWidgetId, views);
    }

    public static void scheduleNextAlarm(Context context) {
        try {
            AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
            if (alarmManager != null) {
                Intent refreshIntent = new Intent(context, WidgetProvider2x2.class);
                refreshIntent.setAction(ACTION_REFRESH);
                PendingIntent pendingIntent = PendingIntent.getBroadcast(context, 999, refreshIntent,
                        PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);

                // Update once every minute on the minute mark
                long nextMinute = (System.currentTimeMillis() / 60000L + 1) * 60000L;
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                    alarmManager.setAndAllowWhileIdle(AlarmManager.RTC, nextMinute, pendingIntent);
                } else {
                    alarmManager.set(AlarmManager.RTC, nextMinute, pendingIntent);
                }
            }
        } catch (Exception ignored) { }
    }
}
