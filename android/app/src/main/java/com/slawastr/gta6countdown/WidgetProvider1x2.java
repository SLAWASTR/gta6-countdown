package com.slawastr.gta6countdown;

import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.Context;
import android.content.Intent;

public class WidgetProvider1x2 extends AppWidgetProvider {

    @Override
    public void onUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds) {
        for (int appWidgetId : appWidgetIds) {
            WidgetHelper.updateWidget1x2(context, appWidgetManager, appWidgetId);
        }
        WidgetHelper.scheduleNextAlarm(context);
    }

    @Override
    public void onReceive(Context context, Intent intent) {
        super.onReceive(context, intent);
        if (WidgetHelper.ACTION_REFRESH.equals(intent.getAction()) ||
            Intent.ACTION_TIME_TICK.equals(intent.getAction()) ||
            Intent.ACTION_TIME_CHANGED.equals(intent.getAction()) ||
            Intent.ACTION_TIMEZONE_CHANGED.equals(intent.getAction())) {
            WidgetHelper.updateAllWidgets(context);
        }
    }
}
