package com.slawastr.gta6countdown;

import android.app.AlarmManager;
import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Build;
import android.os.Bundle;
import android.util.SizeF;
import android.view.View;
import android.widget.RemoteViews;

import java.util.HashMap;
import java.util.Map;

public class WidgetHelper {

    public static final String PREFS_NAME = "gta6_prefs";
    public static final String PREF_HIDE_PRELOAD = "hide_preload";
    public static final String PREF_THEME_MODE = "theme_mode";
    public static final String PREF_LIVE_SECONDS = "live_seconds";
    public static final String ACTION_REFRESH = "com.slawastr.gta6countdown.ACTION_REFRESH";

    // Theme modes
    public static final int THEME_MONOCHROME = 0;   // Dark matte (как на скрине пользователя) - DEFAULT!
    public static final int THEME_LIQUID_GLASS = 1; // Liquid Glass (матовое стекло 65-75%)
    public static final int THEME_VICE_CITY = 2;    // Vice City Неон
    public static final int THEME_OLED = 3;         // Чистый черный OLED

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
        public String timeOnly() {
            return String.format("%02d:%02d:%02d", Math.max(0, hours), Math.max(0, mins), Math.max(0, secs));
        }
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

    public static int getThemeMode(Context context) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        return prefs.getInt(PREF_THEME_MODE, THEME_MONOCHROME);
    }

    public static void setThemeMode(Context context, int mode) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().putInt(PREF_THEME_MODE, mode).apply();
        updateAllWidgets(context);
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

    public static boolean isLiveSecondsEnabled(Context context) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        return prefs.getBoolean(PREF_LIVE_SECONDS, true);
    }

    public static void setLiveSecondsEnabled(Context context, boolean enabled) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().putBoolean(PREF_LIVE_SECONDS, enabled).apply();
        if (enabled) {
            startLiveTickerIfEnabled(context);
        }
    }

    public static void startLiveTickerIfEnabled(Context context) {
        if (isLiveSecondsEnabled(context)) {
            try {
                Intent serviceIntent = new Intent(context, WidgetUpdateService.class);
                context.startService(serviceIntent);
            } catch (Exception ignored) { }
        }
    }

    public static int getWidgetBgDrawable(int themeMode) {
        switch (themeMode) {
            case THEME_LIQUID_GLASS:
                return R.drawable.bg_widget_liquid_glass;
            case THEME_VICE_CITY:
                return R.drawable.bg_widget_card;
            case THEME_OLED:
                return R.drawable.bg_widget_oled;
            case THEME_MONOCHROME:
            default:
                return R.drawable.bg_widget_monochrome;
        }
    }

    public static int getInnerBoxDrawable(int themeMode) {
        switch (themeMode) {
            case THEME_LIQUID_GLASS:
                return R.drawable.bg_inner_box_liquid;
            case THEME_VICE_CITY:
                return R.drawable.bg_inner_box;
            case THEME_OLED:
            case THEME_MONOCHROME:
            default:
                return R.drawable.bg_inner_box_monochrome;
        }
    }

    public static void updateAllWidgets(Context context) {
        AppWidgetManager manager = AppWidgetManager.getInstance(context);

        // 1x1
        ComponentName comp1x1 = new ComponentName(context, WidgetProvider1x1.class);
        int[] ids1x1 = manager.getAppWidgetIds(comp1x1);
        for (int id : ids1x1) {
            updateWidget1x1(context, manager, id);
        }

        // 1x2
        ComponentName comp1x2 = new ComponentName(context, WidgetProvider1x2.class);
        int[] ids1x2 = manager.getAppWidgetIds(comp1x2);
        for (int id : ids1x2) {
            updateWidget1x2(context, manager, id, null);
        }

        // 2x2
        ComponentName comp2x2 = new ComponentName(context, WidgetProvider2x2.class);
        int[] ids2x2 = manager.getAppWidgetIds(comp2x2);
        for (int id : ids2x2) {
            updateWidget2x2(context, manager, id, null);
        }

        scheduleNextAlarm(context);
    }

    public static void updateWidget1x1(Context context, AppWidgetManager manager, int appWidgetId) {
        RemoteViews views = new RemoteViews(context.getPackageName(), R.layout.widget_1x1);
        long now = System.currentTimeMillis();
        TimeParts rel = calculateTimeParts(TARGET_RELEASE_MS, now);
        int theme = getThemeMode(context);

        views.setInt(R.id.widget_1x1_root, "setBackgroundResource", getWidgetBgDrawable(theme));
        views.setTextViewText(R.id.txt_1x1_days, String.valueOf(rel.days));
        views.setTextViewText(R.id.txt_1x1_time_pill, rel.timeOnly());

        Intent intent = new Intent(context, MainActivity.class);
        PendingIntent pendingIntent = PendingIntent.getActivity(context, 100 + appWidgetId, intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        views.setOnClickPendingIntent(R.id.widget_1x1_root, pendingIntent);

        manager.updateAppWidget(appWidgetId, views);
    }

    public static void updateWidget1x2(Context context, AppWidgetManager manager, int appWidgetId) {
        updateWidget1x2(context, manager, appWidgetId, null);
    }

    public static void updateWidget1x2(Context context, AppWidgetManager manager, int appWidgetId, Bundle options) {
        long now = System.currentTimeMillis();
        TimeParts rel = calculateTimeParts(TARGET_RELEASE_MS, now);
        int theme = getThemeMode(context);

        if (options == null) {
            options = manager.getAppWidgetOptions(appWidgetId);
        }

        int minWidth = 200;
        if (options != null) {
            minWidth = options.getInt(AppWidgetManager.OPTION_APPWIDGET_MIN_WIDTH, 200);
        }

        Intent intent = new Intent(context, MainActivity.class);
        PendingIntent pendingIntent = PendingIntent.getActivity(context, 2000 + appWidgetId, intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);

        // Android 12+ Multi-size RemoteViews
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            Map<SizeF, RemoteViews> viewsMap = new HashMap<>();

            RemoteViews compactViews = build1x2CompactViews(context, rel, pendingIntent, theme);
            viewsMap.put(new SizeF(100f, 40f), compactViews);

            RemoteViews wideViews = build1x2WideViews(context, rel, pendingIntent, theme);
            viewsMap.put(new SizeF(170f, 40f), wideViews);

            manager.updateAppWidget(appWidgetId, new RemoteViews(viewsMap));
        } else {
            RemoteViews views;
            if (minWidth < 170) {
                views = build1x2CompactViews(context, rel, pendingIntent, theme);
            } else {
                views = build1x2WideViews(context, rel, pendingIntent, theme);
            }
            manager.updateAppWidget(appWidgetId, views);
        }
    }

    private static RemoteViews build1x2WideViews(Context context, TimeParts rel, PendingIntent pendingIntent, int theme) {
        RemoteViews views = new RemoteViews(context.getPackageName(), R.layout.widget_1x2);
        views.setInt(R.id.widget_1x2_root, "setBackgroundResource", getWidgetBgDrawable(theme));

        int innerBox = getInnerBoxDrawable(theme);
        views.setInt(R.id.box_1x2_hours, "setBackgroundResource", innerBox);
        views.setInt(R.id.box_1x2_mins, "setBackgroundResource", innerBox);
        views.setInt(R.id.box_1x2_secs, "setBackgroundResource", innerBox);

        if (rel.isDone) {
            views.setViewVisibility(R.id.layout_1x2_digits, View.GONE);
            views.setViewVisibility(R.id.txt_1x2_done, View.VISIBLE);
        } else {
            views.setViewVisibility(R.id.layout_1x2_digits, View.VISIBLE);
            views.setViewVisibility(R.id.txt_1x2_done, View.GONE);
            views.setTextViewText(R.id.txt_1x2_days, String.valueOf(rel.days));
            views.setTextViewText(R.id.txt_1x2_hours, rel.hoursStr());
            views.setTextViewText(R.id.txt_1x2_mins, rel.minsStr());
            views.setTextViewText(R.id.txt_1x2_secs, rel.secsStr()); // SECONDS ALWAYS SHOWN!
        }
        views.setOnClickPendingIntent(R.id.widget_1x2_root, pendingIntent);
        return views;
    }

    private static RemoteViews build1x2CompactViews(Context context, TimeParts rel, PendingIntent pendingIntent, int theme) {
        RemoteViews views = new RemoteViews(context.getPackageName(), R.layout.widget_1x2_compact);
        views.setInt(R.id.widget_1x2_root, "setBackgroundResource", getWidgetBgDrawable(theme));

        if (rel.isDone) {
            views.setViewVisibility(R.id.txt_1x2_pill_time, View.GONE);
            views.setViewVisibility(R.id.txt_1x2_done, View.VISIBLE);
        } else {
            views.setViewVisibility(R.id.txt_1x2_pill_time, View.VISIBLE);
            views.setViewVisibility(R.id.txt_1x2_done, View.GONE);
            views.setTextViewText(R.id.txt_1x2_days, String.valueOf(rel.days));
            views.setTextViewText(R.id.txt_1x2_pill_time, rel.timeOnly()); // LIVE SECONDS!
        }
        views.setOnClickPendingIntent(R.id.widget_1x2_root, pendingIntent);
        return views;
    }

    public static void updateWidget2x2(Context context, AppWidgetManager manager, int appWidgetId) {
        updateWidget2x2(context, manager, appWidgetId, null);
    }

    public static void updateWidget2x2(Context context, AppWidgetManager manager, int appWidgetId, Bundle options) {
        boolean hidePreload = isPreloadHidden(context);
        long now = System.currentTimeMillis();
        TimeParts rel = calculateTimeParts(TARGET_RELEASE_MS, now);
        int theme = getThemeMode(context);

        RemoteViews views;
        if (hidePreload) {
            views = new RemoteViews(context.getPackageName(), R.layout.widget_2x2_single);
            views.setInt(R.id.widget_root, "setBackgroundResource", getWidgetBgDrawable(theme));
            if (rel.isDone) {
                views.setViewVisibility(R.id.grid_rel_digits, View.GONE);
                views.setViewVisibility(R.id.txt_rel_done, View.VISIBLE);
            } else {
                views.setViewVisibility(R.id.grid_rel_digits, View.VISIBLE);
                views.setViewVisibility(R.id.txt_rel_done, View.GONE);
                views.setTextViewText(R.id.txt_rel_days, String.valueOf(rel.days));
                views.setTextViewText(R.id.txt_rel_hours, rel.hoursStr());
                views.setTextViewText(R.id.txt_rel_mins, rel.minsStr());
                views.setTextViewText(R.id.txt_rel_secs, rel.secsStr());
            }
        } else {
            views = new RemoteViews(context.getPackageName(), R.layout.widget_2x2);
            views.setInt(R.id.widget_root, "setBackgroundResource", getWidgetBgDrawable(theme));
            if (rel.isDone) {
                views.setViewVisibility(R.id.grid_rel_digits, View.GONE);
                views.setViewVisibility(R.id.txt_rel_done, View.VISIBLE);
            } else {
                views.setViewVisibility(R.id.grid_rel_digits, View.VISIBLE);
                views.setViewVisibility(R.id.txt_rel_done, View.GONE);
                views.setTextViewText(R.id.txt_rel_days, rel.daysStr());
                views.setTextViewText(R.id.txt_rel_hours, rel.hoursStr());
                views.setTextViewText(R.id.txt_rel_mins, rel.minsStr());
                views.setTextViewText(R.id.txt_rel_secs, rel.secsStr());
            }

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
                views.setTextViewText(R.id.txt_pre_secs, pre.secsStr());
            }
        }

        Intent intent = new Intent(context, MainActivity.class);
        PendingIntent pendingIntent = PendingIntent.getActivity(context, appWidgetId, intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        views.setOnClickPendingIntent(R.id.widget_root, pendingIntent);

        Intent refreshIntent = new Intent(context, WidgetProvider2x2.class);
        refreshIntent.setAction(ACTION_REFRESH);
        PendingIntent refreshPending = PendingIntent.getBroadcast(context, 1000 + appWidgetId, refreshIntent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        views.setOnClickPendingIntent(R.id.btn_refresh, refreshPending);

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
