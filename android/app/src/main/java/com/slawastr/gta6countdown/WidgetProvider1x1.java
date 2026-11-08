package com.slawastr.gta6countdown;

import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;

public class WidgetProvider1x1 extends AppWidgetProvider {

    @Override
    public void onUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds) {
        for (int appWidgetId : appWidgetIds) {
            WidgetHelper.updateWidget1x1(context, appWidgetManager, appWidgetId);
        }
        WidgetHelper.startLiveTickerIfEnabled(context);
        WidgetHelper.scheduleNextAlarm(context);
    }

    @Override
    public void onAppWidgetOptionsChanged(Context context, AppWidgetManager appWidgetManager, int appWidgetId, Bundle newOptions) {
        super.onAppWidgetOptionsChanged(context, appWidgetManager, appWidgetId, newOptions);
        WidgetHelper.updateWidget1x1(context, appWidgetManager, appWidgetId);
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
