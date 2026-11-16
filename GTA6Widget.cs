using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

public class GTA6WidgetApp : Application {
    [STAThread]
    public static void Main(string[] args) {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            try { File.WriteAllText("crash.log", e.ExceptionObject.ToString()); } catch { }
        };
        try {
            var app = new GTA6WidgetApp();
            app.DispatcherUnhandledException += (s, e) => {
                try { File.WriteAllText("crash.log", e.Exception.ToString()); } catch { }
                e.Handled = false;
            };
            app.Run(new WidgetWindow());
        } catch (Exception ex) {
            try { File.WriteAllText("crash.log", ex.ToString()); } catch { }
        }
    }
}

public static class Win32 {
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public static readonly IntPtr HWND_TOP = new IntPtr(0);
    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;

    public const int WM_NCHITTEST = 0x0084;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}

public class WidgetWindow : Window {
    private DispatcherTimer timer;
    private bool isLocked = false;
    private int layerMode = 0; // 0 = Desktop level (bottom), 1 = Normal window, 2 = Topmost (always on top)
    private double digitFontSize = 200;
    private bool showColons = true;
    private bool cyanSeconds = true;
    private bool enableShadow = true;

    // Target Date (Release 18.11 21:30 MSK)
    private DateTime releaseDate;

    // UI Elements: PURE DIGITS ONLY, NO TEXT
    private TextBlock txtHours;
    private TextBlock txtColon1;
    private TextBlock txtMins;
    private TextBlock txtColon2;
    private TextBlock txtSecs;

    private StackPanel digitsPanel;
    private Border doneBox;
    private DropShadowEffect digitShadow;

    private readonly string configFile = "widget_config.txt";

    public WidgetWindow() {
        this.Title = "GTA VI Countdown Digits";
        this.WindowStyle = WindowStyle.None;
        this.AllowsTransparency = true;
        this.Background = Brushes.Transparent;
        this.ShowInTaskbar = false;
        this.ResizeMode = ResizeMode.NoResize;

        // Target Date
        int currentYear = DateTime.Now.Year;
        releaseDate = new DateTime(currentYear, 11, 18, 21, 30, 0);
        if (DateTime.Now - releaseDate > TimeSpan.FromDays(30)) {
            releaseDate = new DateTime(currentYear + 1, 11, 18, 21, 30, 0);
        }

        // Screen Dimensions (Default 2560x1440)
        double sw = SystemParameters.PrimaryScreenWidth;
        double sh = SystemParameters.PrimaryScreenHeight;
        if (sw <= 0) sw = 2560;
        if (sh <= 0) sh = 1440;

        // Default: Centered on screen, pure digits
        this.Width = digitFontSize * 4.4;
        this.Height = digitFontSize * 1.35;
        this.Left = (sw - this.Width) / 2.0;
        this.Top = (sh - this.Height) / 2.0;

        LoadConfig();

        BuildUI();
        BuildContextMenu();
        ApplyShadowState();

        this.Loaded += (s, e) => ApplyLayerMode();
        this.SourceInitialized += OnSourceInitialized;
        this.MouseLeftButtonDown += OnMouseLeftButtonDown;
        this.MouseDoubleClick += (s, e) => CenterOnScreen();
        this.KeyDown += OnKeyDown;
        this.LocationChanged += (s, e) => SaveConfig();
        this.SizeChanged += (s, e) => SaveConfig();

        // Timer
        timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (s, e) => UpdateCountdown();
        timer.Start();

        UpdateCountdown();
    }

    private void OnSourceInitialized(object sender, EventArgs e) {
        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        HwndSource source = HwndSource.FromHwnd(hwnd);
        if (source != null) {
            source.AddHook(WndProc);
        }

        int exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, exStyle | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);

        ApplyLayerMode();
    }

    private void ApplyLayerMode() {
        var helper = new WindowInteropHelper(this);
        if (helper.Handle == IntPtr.Zero) return;

        if (layerMode == 2) {
            // Слой 2: Поверх всех окон (Topmost)
            this.Topmost = true;
            Win32.SetWindowPos(helper.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        } else if (layerMode == 1) {
            // Слой 1: Обычное окно (Normal)
            this.Topmost = false;
            Win32.SetWindowPos(helper.Handle, Win32.HWND_NOTOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        } else {
            // Слой 0: На рабочем столе позади окон (Desktop / Bottom)
            this.Topmost = false;
            Win32.SetWindowPos(helper.Handle, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
    }

    public void CycleLayer() {
        layerMode = (layerMode + 1) % 3;
        ApplyLayerMode();
        SaveConfig();
        BuildContextMenu();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
        if (!isLocked && msg == Win32.WM_NCHITTEST) {
            int x = (short)(lParam.ToInt32() & 0xFFFF);
            int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);

            Point pt = this.PointFromScreen(new Point(x, y));
            int margin = 8;
            int corner = 14;

            bool isLeft = pt.X <= margin;
            bool isRight = pt.X >= this.ActualWidth - margin;
            bool isTop = pt.Y <= margin;
            bool isBottom = pt.Y >= this.ActualHeight - margin;

            bool isCornerLeft = pt.X <= corner;
            bool isCornerRight = pt.X >= this.ActualWidth - corner;
            bool isCornerTop = pt.Y <= corner;
            bool isCornerBottom = pt.Y >= this.ActualHeight - corner;

            if (isCornerLeft && isCornerTop) { handled = true; return (IntPtr)Win32.HTTOPLEFT; }
            if (isCornerRight && isCornerTop) { handled = true; return (IntPtr)Win32.HTTOPRIGHT; }
            if (isCornerLeft && isCornerBottom) { handled = true; return (IntPtr)Win32.HTBOTTOMLEFT; }
            if (isCornerRight && isCornerBottom) { handled = true; return (IntPtr)Win32.HTBOTTOMRIGHT; }

            if (isLeft) { handled = true; return (IntPtr)Win32.HTLEFT; }
            if (isRight) { handled = true; return (IntPtr)Win32.HTRIGHT; }
            if (isTop) { handled = true; return (IntPtr)Win32.HTTOP; }
            if (isBottom) { handled = true; return (IntPtr)Win32.HTBOTTOM; }
        }
        return IntPtr.Zero;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (!isLocked && e.ButtonState == MouseButtonState.Pressed) {
            this.DragMove();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e) {
        double step = 5.0;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) step = 25.0;
        else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) step = 1.0;

        if (e.Key == Key.Left) {
            // Движение по оси X влево
            this.Left -= step;
            SaveConfig();
            e.Handled = true;
        } else if (e.Key == Key.Right) {
            // Движение по оси X вправо
            this.Left += step;
            SaveConfig();
            e.Handled = true;
        } else if (e.Key == Key.Up) {
            // Движение по оси Y вверх
            this.Top -= step;
            SaveConfig();
            e.Handled = true;
        } else if (e.Key == Key.Down) {
            // Движение по оси Y вниз
            this.Top += step;
            SaveConfig();
            e.Handled = true;
        } else if (e.Key == Key.Home) {
            CenterOnScreen();
            e.Handled = true;
        } else if (e.Key == Key.Tab) {
            CycleLayer();
            e.Handled = true;
        } else if (e.Key == Key.S) {
            // Включение / выключение тени клавишей S
            enableShadow = !enableShadow;
            ApplyShadowState();
            SaveConfig();
            BuildContextMenu();
            e.Handled = true;
        } else if (e.Key == Key.OemPlus || e.Key == Key.Add) {
            ChangeFontSize(digitFontSize + 15);
            e.Handled = true;
        } else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) {
            ChangeFontSize(digitFontSize - 15);
            e.Handled = true;
        } else if (e.Key == Key.C) {
            showColons = !showColons;
            SaveConfig();
            UpdateCountdown();
            e.Handled = true;
        }
    }

    private void BuildUI() {
        var root = new Grid {
            Background = Brushes.Transparent, // ЧИСТЫЙ ПРОЗРАЧНЫЙ ФОН, НИКАКИХ КАРТОЧЕК И РАМОК
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        digitsPanel = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Deep shadow for optional contrast
        digitShadow = new DropShadowEffect {
            Color = Color.FromArgb(240, 12, 4, 24),
            BlurRadius = 28,
            ShadowDepth = 4,
            Direction = 315,
            Opacity = 0.92
        };

        txtHours = CreateDigitBlock("52", Brushes.White);
        txtColon1 = CreateColonBlock();
        txtMins = CreateDigitBlock("20", Brushes.White);
        txtColon2 = CreateColonBlock();
        txtSecs = CreateDigitBlock("10", new SolidColorBrush(Color.FromRgb(0, 242, 254)));

        digitsPanel.Children.Add(txtHours);
        digitsPanel.Children.Add(txtColon1);
        digitsPanel.Children.Add(txtMins);
        digitsPanel.Children.Add(txtColon2);
        digitsPanel.Children.Add(txtSecs);

        root.Children.Add(digitsPanel);

        // Done box on release
        doneBox = new Border {
            Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 140)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(30, 15, 30, 15),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        var txtDone = new TextBlock {
            Text = "🎉 ИГРА ВЫШЛА! 🎉",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 50,
            FontWeight = FontWeights.Heavy,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 140)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        doneBox.Child = txtDone;
        root.Children.Add(doneBox);

        this.Content = root;
    }

    private void ApplyShadowState() {
        var effect = enableShadow ? digitShadow : null;
        if (txtHours != null) txtHours.Effect = effect;
        if (txtColon1 != null) txtColon1.Effect = effect;
        if (txtMins != null) txtMins.Effect = effect;
        if (txtColon2 != null) txtColon2.Effect = effect;
        if (txtSecs != null) txtSecs.Effect = effect;
    }

    private TextBlock CreateDigitBlock(string text, Brush brush) {
        return new TextBlock {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = digitFontSize,
            FontWeight = FontWeights.Bold,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LineHeight = digitFontSize * 0.95
        };
    }

    private TextBlock CreateColonBlock() {
        return new TextBlock {
            Text = ":",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = digitFontSize * 0.7,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(digitFontSize * 0.08, 0, digitFontSize * 0.08, digitFontSize * 0.12)
        };
    }

    public void CenterOnScreen() {
        CenterAxisX();
        CenterAxisY();
    }

    public void CenterAxisX() {
        double sw = SystemParameters.PrimaryScreenWidth;
        if (sw <= 0) sw = 2560;
        this.Left = (sw - this.Width) / 2.0;
        SaveConfig();
    }

    public void CenterAxisY() {
        double sh = SystemParameters.PrimaryScreenHeight;
        if (sh <= 0) sh = 1440;
        this.Top = (sh - this.Height) / 2.0;
        SaveConfig();
    }

    public void ChangeFontSize(double newSize) {
        if (newSize < 60) newSize = 60;
        if (newSize > 350) newSize = 350;

        digitFontSize = newSize;
        if (txtHours != null) txtHours.FontSize = digitFontSize;
        if (txtMins != null) txtMins.FontSize = digitFontSize;
        if (txtSecs != null) txtSecs.FontSize = digitFontSize;
        if (txtColon1 != null) {
            txtColon1.FontSize = digitFontSize * 0.7;
            txtColon1.Margin = new Thickness(digitFontSize * 0.08, 0, digitFontSize * 0.08, digitFontSize * 0.12);
        }
        if (txtColon2 != null) {
            txtColon2.FontSize = digitFontSize * 0.7;
            txtColon2.Margin = new Thickness(digitFontSize * 0.08, 0, digitFontSize * 0.08, digitFontSize * 0.12);
        }

        double oldW = this.Width;
        double oldH = this.Height;
        this.Width = digitFontSize * 4.4;
        this.Height = digitFontSize * 1.35;

        // Keep center in same spot
        this.Left -= (this.Width - oldW) / 2.0;
        this.Top -= (this.Height - oldH) / 2.0;

        SaveConfig();
    }

    private void BuildContextMenu() {
        var menu = new ContextMenu();

        var titleItem = new MenuItem { Header = "🎮 GTA VI — Чистые цифры", IsEnabled = false };
        menu.Items.Add(titleItem);
        menu.Items.Add(new Separator());

        // Center on screen
        var itemCenter = new MenuItem { Header = "🎯 Выровнять ровно по центру (Home / Двойной клик)" };
        itemCenter.Click += (s, e) => CenterOnScreen();
        menu.Items.Add(itemCenter);

        var itemCenterX = new MenuItem { Header = "↔ Центрировать по горизонтали (Ось X)" };
        itemCenterX.Click += (s, e) => CenterAxisX();
        menu.Items.Add(itemCenterX);

        var itemCenterY = new MenuItem { Header = "↕ Центрировать по вертикали (Ось Y)" };
        itemCenterY.Click += (s, e) => CenterAxisY();
        menu.Items.Add(itemCenterY);

        menu.Items.Add(new Separator());

        // Layers (Z-Order) Submenu
        var layerMenu = new MenuItem { Header = "🥞 Слой окна (по слоям / Tab)..." };

        var l0 = new MenuItem { Header = "🖼️ На рабочем столе (позади всех окон)", IsCheckable = true, IsChecked = (layerMode == 0) };
        l0.Click += (s, e) => { layerMode = 0; ApplyLayerMode(); SaveConfig(); BuildContextMenu(); };
        layerMenu.Items.Add(l0);

        var l1 = new MenuItem { Header = "🪟 Обычный слой окон", IsCheckable = true, IsChecked = (layerMode == 1) };
        l1.Click += (s, e) => { layerMode = 1; ApplyLayerMode(); SaveConfig(); BuildContextMenu(); };
        layerMenu.Items.Add(l1);

        var l2 = new MenuItem { Header = "📌 Поверх всех окон (Topmost)", IsCheckable = true, IsChecked = (layerMode == 2) };
        l2.Click += (s, e) => { layerMode = 2; ApplyLayerMode(); SaveConfig(); BuildContextMenu(); };
        layerMenu.Items.Add(l2);

        menu.Items.Add(layerMenu);

        // Axis nudge hints
        var axisInfo = new MenuItem { Header = "📐 Стрелки ← ↑ → ↓ — движение по осям X и Y", IsEnabled = false };
        menu.Items.Add(axisInfo);

        menu.Items.Add(new Separator());

        // Shadow toggle (Опциональная тень)
        var shadowItem = new MenuItem {
            Header = "Мягкая тень цифр (Клавиша S)",
            IsCheckable = true,
            IsChecked = enableShadow
        };
        shadowItem.Click += (s, e) => {
            enableShadow = !enableShadow;
            ApplyShadowState();
            SaveConfig();
            BuildContextMenu();
        };
        menu.Items.Add(shadowItem);

        // Font Sizes Submenu
        var sizeMenu = new MenuItem { Header = "📏 Размер цифр..." };
        var s1 = new MenuItem { Header = "Колоссальные (250px)" };
        s1.Click += (s, e) => ChangeFontSize(250);
        sizeMenu.Items.Add(s1);

        var s2 = new MenuItem { Header = "Огромные (200px — 2K Стандарт)" };
        s2.Click += (s, e) => ChangeFontSize(200);
        sizeMenu.Items.Add(s2);

        var s3 = new MenuItem { Header = "Крупные (160px)" };
        s3.Click += (s, e) => ChangeFontSize(160);
        sizeMenu.Items.Add(s3);

        var s4 = new MenuItem { Header = "Средние (120px)" };
        s4.Click += (s, e) => ChangeFontSize(120);
        sizeMenu.Items.Add(s4);
        menu.Items.Add(sizeMenu);

        // Options
        var colonsItem = new MenuItem {
            Header = "Разделитель «:» (Клавиша C)",
            IsCheckable = true,
            IsChecked = showColons
        };
        colonsItem.Click += (s, e) => {
            showColons = !showColons;
            colonsItem.IsChecked = showColons;
            SaveConfig();
            UpdateCountdown();
        };
        menu.Items.Add(colonsItem);

        var cyanItem = new MenuItem {
            Header = "Неоновый циан для секунд",
            IsCheckable = true,
            IsChecked = cyanSeconds
        };
        cyanItem.Click += (s, e) => {
            cyanSeconds = !cyanSeconds;
            cyanItem.IsChecked = cyanSeconds;
            SaveConfig();
            UpdateCountdown();
        };
        menu.Items.Add(cyanItem);

        var lockItem = new MenuItem {
            Header = "Закрепить положение",
            IsCheckable = true,
            IsChecked = isLocked
        };
        lockItem.Click += (s, e) => {
            isLocked = !isLocked;
            lockItem.IsChecked = isLocked;
            SaveConfig();
        };
        menu.Items.Add(lockItem);

        menu.Items.Add(new Separator());

        var closeItem = new MenuItem { Header = "Закрыть виджет" };
        closeItem.Click += (s, e) => this.Close();
        menu.Items.Add(closeItem);

        this.ContextMenu = menu;
    }

    private void UpdateCountdown() {
        DateTime now = DateTime.Now;
        TimeSpan rDiff = releaseDate - now;

        if (rDiff.TotalSeconds <= 0) {
            digitsPanel.Visibility = Visibility.Collapsed;
            doneBox.Visibility = Visibility.Visible;
        } else {
            int totalHours = (int)rDiff.TotalHours;
            int mins = rDiff.Minutes;
            int secs = rDiff.Seconds;

            txtHours.Text = totalHours.ToString();
            txtMins.Text = mins.ToString("D2");
            txtSecs.Text = secs.ToString("D2");

            txtColon1.Visibility = showColons ? Visibility.Visible : Visibility.Collapsed;
            txtColon2.Visibility = showColons ? Visibility.Visible : Visibility.Collapsed;

            if (cyanSeconds) {
                txtSecs.Foreground = new SolidColorBrush(Color.FromRgb(0, 242, 254));
            } else {
                txtSecs.Foreground = Brushes.White;
            }

            digitsPanel.Visibility = Visibility.Visible;
            doneBox.Visibility = Visibility.Collapsed;
        }
    }

    private void SaveConfig() {
        try {
            string line = string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9}",
                (int)this.Left,
                (int)this.Top,
                (int)this.Width,
                (int)this.Height,
                layerMode,
                isLocked ? 1 : 0,
                (int)digitFontSize,
                showColons ? 1 : 0,
                cyanSeconds ? 1 : 0,
                enableShadow ? 1 : 0);
            File.WriteAllText(configFile, line);
        } catch { }
    }

    private void LoadConfig() {
        try {
            if (File.Exists(configFile)) {
                string[] parts = File.ReadAllText(configFile).Trim().Split(';');
                if (parts.Length >= 4) {
                    double l = double.Parse(parts[0]);
                    double t = double.Parse(parts[1]);
                    double w = double.Parse(parts[2]);
                    double h = double.Parse(parts[3]);

                    if (w >= 300) this.Width = w;
                    if (h >= 100) this.Height = h;
                    if (l >= -500 && l < SystemParameters.VirtualScreenWidth - 100) this.Left = l;
                    if (t >= -500 && t < SystemParameters.VirtualScreenHeight - 100) this.Top = t;

                    if (parts.Length >= 5) layerMode = int.Parse(parts[4]);
                    if (parts.Length >= 6) isLocked = parts[5] == "1";
                    if (parts.Length >= 7) digitFontSize = double.Parse(parts[6]);
                    if (parts.Length >= 8) showColons = parts[7] == "1";
                    if (parts.Length >= 9) cyanSeconds = parts[8] == "1";
                    if (parts.Length >= 10) enableShadow = parts[9] == "1";
                }
            }
        } catch { }
    }
}
