package com.slawastr.gta6countdown;

import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.content.ComponentName;
import android.content.Intent;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.View;
import android.widget.RadioButton;
import android.widget.RadioGroup;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;
import com.google.android.material.switchmaterial.SwitchMaterial;

public class MainActivity extends AppCompatActivity {

    private final Handler handler = new Handler(Looper.getMainLooper());
    private Runnable ticker;

    // Release Views
    private TextView txtRelDays, txtRelHours, txtRelMins, txtRelSecs, txtRelDone;
    private View layoutRelDigits;

    // Preload Views
    private TextView txtPreDays, txtPreHours, txtPreMins, txtPreSecs, txtPreDone;
    private View layoutPreDigits;
    private MaterialCardView cardPreload;

    // Controls
    private SwitchMaterial switchHidePreload;
    private SwitchMaterial switchLiveSeconds;
    private RadioGroup rgWidgetTheme;
    private RadioButton rbMonochrome, rbLiquidGlass, rbViceCity, rbOled;
    private MaterialButton btnPin1x1, btnPin1x2, btnPin2x2;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        // Bind Release UI
        txtRelDays = findViewById(R.id.main_rel_days);
        txtRelHours = findViewById(R.id.main_rel_hours);
        txtRelMins = findViewById(R.id.main_rel_mins);
        txtRelSecs = findViewById(R.id.main_rel_secs);
        txtRelDone = findViewById(R.id.main_rel_done);
        layoutRelDigits = findViewById(R.id.main_rel_digits_layout);

        // Bind Preload UI
        cardPreload = findViewById(R.id.card_preload_main);
        txtPreDays = findViewById(R.id.main_pre_days);
        txtPreHours = findViewById(R.id.main_pre_hours);
        txtPreMins = findViewById(R.id.main_pre_mins);
        txtPreSecs = findViewById(R.id.main_pre_secs);
        txtPreDone = findViewById(R.id.main_pre_done);
        layoutPreDigits = findViewById(R.id.main_pre_digits_layout);

        // Bind Preload Settings
        switchHidePreload = findViewById(R.id.switch_hide_preload);
        boolean isHidden = WidgetHelper.isPreloadHidden(this);
        switchHidePreload.setChecked(isHidden);
        cardPreload.setVisibility(isHidden ? View.GONE : View.VISIBLE);

        switchHidePreload.setOnCheckedChangeListener((buttonView, isChecked) -> {
            WidgetHelper.setPreloadHidden(MainActivity.this, isChecked);
            cardPreload.setVisibility(isChecked ? View.GONE : View.VISIBLE);
        });

        // Bind Live Seconds Settings
        switchLiveSeconds = findViewById(R.id.switch_live_seconds);
        boolean isLiveEnabled = WidgetHelper.isLiveSecondsEnabled(this);
        switchLiveSeconds.setChecked(isLiveEnabled);
        switchLiveSeconds.setOnCheckedChangeListener((buttonView, isChecked) -> {
            WidgetHelper.setLiveSecondsEnabled(MainActivity.this, isChecked);
        });

        // Bind Theme RadioGroup
        rgWidgetTheme = findViewById(R.id.rg_widget_theme);
        rbMonochrome = findViewById(R.id.rb_theme_monochrome);
        rbLiquidGlass = findViewById(R.id.rb_theme_liquid_glass);
        rbViceCity = findViewById(R.id.rb_theme_vice_city);
        rbOled = findViewById(R.id.rb_theme_oled);

        int currentTheme = WidgetHelper.getThemeMode(this);
        switch (currentTheme) {
            case WidgetHelper.THEME_LIQUID_GLASS:
                rbLiquidGlass.setChecked(true);
                break;
            case WidgetHelper.THEME_VICE_CITY:
                rbViceCity.setChecked(true);
                break;
            case WidgetHelper.THEME_OLED:
                rbOled.setChecked(true);
                break;
            case WidgetHelper.THEME_MONOCHROME:
            default:
                rbMonochrome.setChecked(true);
                break;
        }

        rgWidgetTheme.setOnCheckedChangeListener((group, checkedId) -> {
            int selectedTheme = WidgetHelper.THEME_MONOCHROME;
            if (checkedId == R.id.rb_theme_liquid_glass) {
                selectedTheme = WidgetHelper.THEME_LIQUID_GLASS;
            } else if (checkedId == R.id.rb_theme_vice_city) {
                selectedTheme = WidgetHelper.THEME_VICE_CITY;
            } else if (checkedId == R.id.rb_theme_oled) {
                selectedTheme = WidgetHelper.THEME_OLED;
            }
            WidgetHelper.setThemeMode(MainActivity.this, selectedTheme);
        });

        // Pin Widgets
        btnPin1x1 = findViewById(R.id.btn_pin_1x1);
        btnPin1x2 = findViewById(R.id.btn_pin_1x2);
        btnPin2x2 = findViewById(R.id.btn_pin_2x2);

        btnPin1x1.setOnClickListener(v -> pinWidget(WidgetProvider1x1.class));
        btnPin1x2.setOnClickListener(v -> pinWidget(WidgetProvider1x2.class));
        btnPin2x2.setOnClickListener(v -> pinWidget(WidgetProvider2x2.class));

        // Start live ticker
        ticker = new Runnable() {
            @Override
            public void run() {
                updateLiveCountdowns();
                handler.postDelayed(this, 1000);
            }
        };
    }

    @Override
    protected void onResume() {
        super.onResume();
        handler.post(ticker);
        WidgetHelper.startLiveTickerIfEnabled(this);
        WidgetHelper.updateAllWidgets(this);
    }

    @Override
    protected void onPause() {
        super.onPause();
        handler.removeCallbacks(ticker);
    }

    private void updateLiveCountdowns() {
        long now = System.currentTimeMillis();

        // Release
        WidgetHelper.TimeParts rel = WidgetHelper.calculateTimeParts(WidgetHelper.TARGET_RELEASE_MS, now);
        if (rel.isDone) {
            layoutRelDigits.setVisibility(View.GONE);
            txtRelDone.setVisibility(View.VISIBLE);
        } else {
            layoutRelDigits.setVisibility(View.VISIBLE);
            txtRelDone.setVisibility(View.GONE);
            txtRelDays.setText(rel.daysStr());
            txtRelHours.setText(rel.hoursStr());
            txtRelMins.setText(rel.minsStr());
            txtRelSecs.setText(rel.secsStr());
        }

        // Preload
        if (!switchHidePreload.isChecked()) {
            WidgetHelper.TimeParts pre = WidgetHelper.calculateTimeParts(WidgetHelper.TARGET_PRELOAD_MS, now);
            if (pre.isDone) {
                layoutPreDigits.setVisibility(View.GONE);
                txtPreDone.setVisibility(View.VISIBLE);
            } else {
                layoutPreDigits.setVisibility(View.VISIBLE);
                txtPreDone.setVisibility(View.GONE);
                txtPreDays.setText(pre.daysStr());
                txtPreHours.setText(pre.hoursStr());
                txtPreMins.setText(pre.minsStr());
                txtPreSecs.setText(pre.secsStr());
            }
        }
    }

    private void pinWidget(Class<?> providerClass) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            AppWidgetManager appWidgetManager = getSystemService(AppWidgetManager.class);
            if (appWidgetManager != null && appWidgetManager.isRequestPinAppWidgetSupported()) {
                ComponentName componentName = new ComponentName(this, providerClass);
                Intent pinnedWidgetCallbackIntent = new Intent(this, MainActivity.class);
                PendingIntent successCallback = PendingIntent.getActivity(
                        this, 0, pinnedWidgetCallbackIntent,
                        PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
                );
                appWidgetManager.requestPinAppWidget(componentName, null, successCallback);
                return;
            }
        }
        Toast.makeText(this, R.string.widget_instructions, Toast.LENGTH_LONG).show();
    }
}
