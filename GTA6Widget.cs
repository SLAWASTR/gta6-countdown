using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

public enum DisplayMode {
    TransparentDigits = 0, // Просто огромные цифры без фона по центру экрана
    CompactCard = 1,       // Компактный виджет с карточкой в углу
    FullscreenCinema = 2   // Полноэкранный кинотеатр
}

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

    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

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
    private bool isTopmost = false;
    private DisplayMode currentMode = DisplayMode.TransparentDigits;

    // Dimensions backup for modes
    private double digitsLeft = 0;
    private double digitsTop = 0;
    private double digitsWidth = 1180;
    private double digitsHeight = 380;
    private double digitFontSize = 190;

    private double cardLeft = 2148;
    private double cardTop = 15;
    private double cardWidth = 400;
    private double cardHeight = 185;

    // Target Date (Release 18.11 21:30)
    private DateTime releaseDate;

    // Root UI
    private Grid rootGrid;
    private Grid transparentDigitsView;
    private Border compactCardView;
    private Border fullscreenCinemaView;

    // 1. Transparent Digits Controls ("ПРОСТО ЦИФРЫ БЕЗ ФОНА")
    private TextBlock tdDaysTop;
    private TextBlock tdHoursNum;
    private TextBlock tdHoursLbl;
    private TextBlock tdColon1;
    private TextBlock tdMinsNum;
    private TextBlock tdMinsLbl;
    private TextBlock tdColon2;
    private TextBlock tdSecsNum;
    private TextBlock tdSecsLbl;
    private TextBlock tdSubtitle;
    private Border tdDoneBox;
    private StackPanel tdDigitsRow;

    // 2. Compact Card Controls
    private TextBlock txtTopDays;
    private TextBlock txtDaysBig;
    private TextBlock txtDaysLbl;
    private TextBlock txtDaysSub;
    private TextBlock rH, rM, rS;
    private Border rDoneBox;
    private UniformGrid rGrid;

    // 3. Fullscreen Cinema Controls
    private TextBlock fsDaysBadge;
    private TextBlock fsNumHours, fsLblHours;
    private TextBlock fsNumMins, fsLblMins;
    private TextBlock fsNumSecs, fsLblSecs;
    private StackPanel fsDigitsRow;
    private Border fsDoneBox;

    private readonly string configFile = "widget_config.txt";

    public WidgetWindow() {
        this.Title = "GTA VI Countdown";
        this.WindowStyle = WindowStyle.None;
        this.AllowsTransparency = true;
        this.Background = Brushes.Transparent;
        this.ShowInTaskbar = false;
        this.ResizeMode = ResizeMode.NoResize;

        // Target Date (18 November 2026, 21:30 MSK)
        int currentYear = DateTime.Now.Year;
        releaseDate = new DateTime(currentYear, 11, 18, 21, 30, 0);
        if (DateTime.Now - releaseDate > TimeSpan.FromDays(30)) {
            releaseDate = new DateTime(currentYear + 1, 11, 18, 21, 30, 0);
        }

        double sw = SystemParameters.PrimaryScreenWidth;
        double sh = SystemParameters.PrimaryScreenHeight;
        if (sw <= 0) sw = 2560;
        if (sh <= 0) sh = 1440;

        // Default: Centered on screen (specifically 2560x1440)
        digitsWidth = 1180;
        digitsHeight = 380;
        digitsLeft = (sw - digitsWidth) / 2.0;
        digitsTop = (sh - digitsHeight) / 2.0 - 50.0; // slightly above dead center in the sky

        cardWidth = 400;
        cardHeight = 185;
        cardLeft = Math.Max(0, sw - cardWidth - 40);
        cardTop = 40;

        LoadConfig();

        BuildUI();
        BuildContextMenu();

        ApplyMode(currentMode, false);

        this.Loaded += OnLoaded;
        this.SourceInitialized += OnSourceInitialized;
        this.MouseLeftButtonDown += OnMouseLeftButtonDown;
        this.MouseDoubleClick += (s, e) => {
            if (currentMode == DisplayMode.TransparentDigits) {
                CenterOnScreen();
            } else {
                SwitchMode(DisplayMode.TransparentDigits);
            }
        };
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

    private void OnKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.F11) {
            if (currentMode == DisplayMode.FullscreenCinema) {
                SwitchMode(DisplayMode.TransparentDigits);
            } else {
                SwitchMode(DisplayMode.FullscreenCinema);
            }
            e.Handled = true;
        } else if (e.Key == Key.Escape) {
            if (currentMode != DisplayMode.TransparentDigits) {
                SwitchMode(DisplayMode.TransparentDigits);
                e.Handled = true;
            }
        }
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

        ApplyWindowPosition();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        ApplyWindowPosition();
    }

    private void ApplyWindowPosition() {
        var helper = new WindowInteropHelper(this);
        if (helper.Handle == IntPtr.Zero) return;

        if (isTopmost) {
            Win32.SetWindowPos(helper.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        } else if (currentMode == DisplayMode.FullscreenCinema) {
            Win32.SetWindowPos(helper.Handle, Win32.HWND_TOP, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_SHOWWINDOW);
        } else {
            Win32.SetWindowPos(helper.Handle, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
        if (currentMode != DisplayMode.FullscreenCinema && msg == Win32.WM_NCHITTEST) {
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
        if (currentMode != DisplayMode.FullscreenCinema && !isLocked && e.ButtonState == MouseButtonState.Pressed) {
            this.DragMove();
        }
    }

    private ImageSource GetLogoImage() {
        try {
            if (File.Exists("logo.png")) {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(System.IO.Path.GetFullPath("logo.png"));
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                return bmp;
            }
            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream("logo.png")) {
                if (stream != null) {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = stream;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    return bmp;
                }
            }
        } catch { }
        return null;
    }

    private void BuildUI() {
        rootGrid = new Grid {
            Background = Brushes.Transparent
        };

        // 1. Transparent Digits View ("ПРОСТО ЦИФРЫ БЕЗ ФОНА")
        transparentDigitsView = BuildTransparentDigitsUI();
        rootGrid.Children.Add(transparentDigitsView);

        // 2. Compact Card View
        compactCardView = BuildCompactWidgetUI();
        rootGrid.Children.Add(compactCardView);

        // 3. Fullscreen Cinema View
        fullscreenCinemaView = BuildFullscreenUI();
        rootGrid.Children.Add(fullscreenCinemaView);

        this.Content = rootGrid;
    }

    // =========================================================================
    // 1. MODE: "ПРОСТО ОГРОМНЫЕ ЦИФРЫ БЕЗ ФОНА НА ЦЕНТРЕ ЭКРАНА"
    // =========================================================================
    private Grid BuildTransparentDigitsUI() {
        var grid = new Grid {
            Background = Brushes.Transparent, // 100% ПРОЗРАЧНО, ВИДНЫ ТОЛЬКО САМИ ЦИФРЫ
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top Days
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Giant Digits
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Subtitle

        // Deep plum shadow to guarantee 100% crisp contrast against sunset pastel sky
        var dropShadow = new DropShadowEffect {
            Color = Color.FromArgb(235, 15, 6, 28),
            BlurRadius = 26,
            ShadowDepth = 3,
            Direction = 315,
            Opacity = 0.90
        };

        // ROW 0: Top Days Banner (Floating)
        tdDaysTop = new TextBlock {
            Text = "⚡ ОСТАЛОСЬ 2 ДНЯ (52 ЧАСА) ДО РЕЛИЗА ⚡",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 24,
            FontWeight = FontWeights.ExtraBold,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 65, 145)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
            Effect = dropShadow
        };
        Grid.SetRow(tdDaysTop, 0);
        grid.Children.Add(tdDaysTop);

        // ROW 1: THE GIANT DIGITS (Hours : Mins : Secs)
        var centerDigitsWrapper = new Grid {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        tdDigitsRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 1. HOURS
        tdDigitsRow.Children.Add(CreateHugeDigitCol(out tdHoursNum, out tdHoursLbl, "52", "ЧАСА", Brushes.White, Color.FromRgb(255, 65, 145)));

        // Colon 1
        tdColon1 = CreateHugeColon(dropShadow);
        tdDigitsRow.Children.Add(tdColon1);

        // 2. MINUTES
        tdDigitsRow.Children.Add(CreateHugeDigitCol(out tdMinsNum, out tdMinsLbl, "20", "МИНУТ", Brushes.White, Color.FromArgb(225, 230, 225, 255)));

        // Colon 2
        tdColon2 = CreateHugeColon(dropShadow);
        tdDigitsRow.Children.Add(tdColon2);

        // 3. SECONDS (ELECTRIC CYAN)
        tdDigitsRow.Children.Add(CreateHugeDigitCol(out tdSecsNum, out tdSecsLbl, "10", "СЕКУНД", new SolidColorBrush(Color.FromRgb(0, 242, 254)), Color.FromRgb(0, 242, 254)));

        centerDigitsWrapper.Children.Add(tdDigitsRow);

        // Done Box for Release
        tdDoneBox = new Border {
            Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 140)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(40, 20, 40, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        var txtDone = new TextBlock {
            Text = "🎉 ИГРА ВЫШЛА! РЕЛИЗ GTA VI СОСТОЯЛСЯ! 🎉",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 46,
            FontWeight = FontWeights.Heavy,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 140)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        tdDoneBox.Child = txtDone;
        centerDigitsWrapper.Children.Add(tdDoneBox);

        Grid.SetRow(centerDigitsWrapper, 1);
        grid.Children.Add(centerDigitsWrapper);

        // ROW 2: Subtitle
        tdSubtitle = new TextBlock {
            Text = "GRAND THEFT AUTO VI • 18 НОЯБРЯ 2026 • 21:30 МСК",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            Effect = dropShadow
        };
        Grid.SetRow(tdSubtitle, 2);
        grid.Children.Add(tdSubtitle);

        return grid;
    }

    private StackPanel CreateHugeDigitCol(out TextBlock valBlock, out TextBlock lblBlock, string initVal, string initLbl, Brush numBrush, Color lblColor) {
        var stack = new StackPanel {
            Width = 330,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var shadow = new DropShadowEffect {
            Color = Color.FromArgb(240, 12, 5, 24),
            BlurRadius = 26,
            ShadowDepth = 4,
            Direction = 315,
            Opacity = 0.95
        };

        valBlock = new TextBlock {
            Text = initVal,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = digitFontSize, // 190pt HUGE DIGITS!
            FontWeight = FontWeights.Bold,
            Foreground = numBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            LineHeight = digitFontSize * 0.95,
            Effect = shadow
        };

        lblBlock = new TextBlock {
            Text = initLbl,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 23,
            FontWeight = FontWeights.ExtraBold,
            Foreground = new SolidColorBrush(lblColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            Effect = shadow
        };

        stack.Children.Add(valBlock);
        stack.Children.Add(lblBlock);
        return stack;
    }

    private TextBlock CreateHugeColon(DropShadowEffect shadow) {
        return new TextBlock {
            Text = ":",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 130,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 40),
            Effect = shadow
        };
    }

    // =========================================================================
    // 2. COMPACT WIDGET WITH CARD (400x185 in corner)
    // =========================================================================
    private Border BuildCompactWidgetUI() {
        var outerBorder = new Border {
            CornerRadius = new CornerRadius(20),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
            Background = new LinearGradientBrush(
                Color.FromArgb(220, 22, 14, 40),
                Color.FromArgb(235, 12, 8, 26),
                new Point(0, 0),
                new Point(1, 1)
            ),
            Visibility = Visibility.Collapsed
        };

        var mainGrid = new Grid {
            Margin = new Thickness(12, 10, 12, 10)
        };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftStack = new StackPanel();
        txtTopDays = new TextBlock {
            Text = "⚡ 2 ДНЯ ДО РЕЛИЗА",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 9.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 89, 167)),
            Margin = new Thickness(0, 0, 0, 1)
        };
        leftStack.Children.Add(txtTopDays);

        var txtTitle = new TextBlock {
            Text = "Grand Theft Auto VI",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255))
        };
        leftStack.Children.Add(txtTitle);

        var daysRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 1, 0, 0)
        };
        txtDaysBig = new TextBlock {
            Text = "52",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 38,
            FontWeight = FontWeights.Light,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        daysRow.Children.Add(txtDaysBig);

        var daysLabelStack = new StackPanel {
            Margin = new Thickness(4, 0, 0, 4),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        txtDaysLbl = new TextBlock {
            Text = "ЧАСА",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 9.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255))
        };
        txtDaysSub = new TextBlock {
            Text = "до релиза",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 8.5,
            Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255))
        };
        daysLabelStack.Children.Add(txtDaysLbl);
        daysLabelStack.Children.Add(txtDaysSub);
        daysRow.Children.Add(daysLabelStack);

        leftStack.Children.Add(daysRow);
        Grid.SetColumn(leftStack, 0);
        topGrid.Children.Add(leftStack);

        var rightStack = new StackPanel {
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var logoSrc = GetLogoImage();
        if (logoSrc != null) {
            var imgLogo = new Image {
                Source = logoSrc,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 2)
            };
            rightStack.Children.Add(imgLogo);
        }

        var dateRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var txtRelDate = new TextBlock {
            Text = "18.11 в 21:30",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center
        };
        dateRow.Children.Add(txtRelDate);

        var btnMode = new Border {
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5, 1, 5, 2),
            Margin = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand,
            ToolTip = "Переключить на огромные цифры по центру"
        };
        var txtBtn = new TextBlock {
            Text = "✦",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        btnMode.Child = txtBtn;
        btnMode.MouseLeftButtonDown += (s, e) => { SwitchMode(DisplayMode.TransparentDigits); e.Handled = true; };
        dateRow.Children.Add(btnMode);

        rightStack.Children.Add(dateRow);

        Grid.SetColumn(rightStack, 1);
        topGrid.Children.Add(rightStack);

        Grid.SetRow(topGrid, 0);
        mainGrid.Children.Add(topGrid);

        var divider = new Rectangle {
            Height = 0.5,
            Fill = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
            Margin = new Thickness(0, 6, 0, 6)
        };
        Grid.SetRow(divider, 1);
        mainGrid.Children.Add(divider);

        // Countdown Card inside compact view
        var card = CreateCompactCountdownCard("🚀 ОБРАТНЫЙ ОТСЧЕТ", "18.11.2026 • 21:30", Color.FromRgb(255, 59, 136), out rH, out rM, out rS, out rDoneBox, out rGrid, "ИГРА ВЫШЛА! 🎉");
        Grid.SetRow(card, 2);
        mainGrid.Children.Add(card);

        outerBorder.Child = mainGrid;
        return outerBorder;
    }

    private Border CreateCompactCountdownCard(string title, string dateStr, Color accentColor, out TextBlock tbH, out TextBlock tbM, out TextBlock tbS, out Border doneBox, out UniformGrid grid, string doneText) {
        var cardBorder = new Border {
            Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(0.5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            Padding = new Thickness(6, 5, 6, 5)
        };

        var stack = new Grid();
        stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerGrid = new Grid();
        var txtHdr = new TextBlock {
            Text = title,
            FontSize = 9,
            FontWeight = FontWeights.Heavy,
            Foreground = new SolidColorBrush(accentColor)
        };
        var txtDate = new TextBlock {
            Text = dateStr,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        headerGrid.Children.Add(txtHdr);
        headerGrid.Children.Add(txtDate);
        Grid.SetRow(headerGrid, 0);
        stack.Children.Add(headerGrid);

        grid = new UniformGrid {
            Columns = 3,
            Margin = new Thickness(0, 4, 0, 0)
        };

        grid.Children.Add(CreateDigitBox(out tbH, "ЧАСОВ", accentColor));
        grid.Children.Add(CreateDigitBox(out tbM, "МИНУТ", Colors.White));
        grid.Children.Add(CreateDigitBox(out tbS, "СЕКУНД", Colors.White));
        Grid.SetRow(grid, 1);
        stack.Children.Add(grid);

        doneBox = new Border {
            Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 120)),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 4, 0, 0),
            Visibility = Visibility.Collapsed
        };
        var txtDone = new TextBlock {
            Text = doneText,
            FontWeight = FontWeights.Heavy,
            FontSize = 9.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 140)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        doneBox.Child = txtDone;
        Grid.SetRow(doneBox, 1);
        stack.Children.Add(doneBox);

        cardBorder.Child = stack;
        return cardBorder;
    }

    private Border CreateDigitBox(out TextBlock valBox, string unitText, Color accent) {
        var box = new Border {
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(1.5, 0, 1.5, 0),
            Padding = new Thickness(2, 3, 2, 2)
        };

        var stack = new StackPanel {
            VerticalAlignment = VerticalAlignment.Center
        };
        valBox = new TextBlock {
            Text = "00",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(accent),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var unitBox = new TextBlock {
            Text = unitText,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 7.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        };
        stack.Children.Add(valBox);
        stack.Children.Add(unitBox);
        box.Child = stack;
        return box;
    }

    // =========================================================================
    // 3. FULLSCREEN CINEMA VIEW
    // =========================================================================
    private Border BuildFullscreenUI() {
        var outer = new Border {
            Background = new LinearGradientBrush(
                Color.FromArgb(238, 14, 8, 28),
                Color.FromArgb(248, 6, 3, 15),
                new Point(0, 0),
                new Point(1, 1)
            ),
            Visibility = Visibility.Collapsed
        };

        var viewbox = new Viewbox {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var canvas = new Grid {
            Width = 2560,
            Height = 1440
        };

        var pinkGlow = new Ellipse {
            Width = 1100,
            Height = 1100,
            Margin = new Thickness(-200, -250, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Fill = new RadialGradientBrush(
                Color.FromArgb(48, 255, 42, 141),
                Color.FromArgb(0, 255, 42, 141)
            ),
            IsHitTestVisible = false
        };
        canvas.Children.Add(pinkGlow);

        var cyanGlow = new Ellipse {
            Width = 1100,
            Height = 1100,
            Margin = new Thickness(0, 0, -200, -250),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Fill = new RadialGradientBrush(
                Color.FromArgb(40, 0, 242, 254),
                Color.FromArgb(0, 0, 242, 254)
            ),
            IsHitTestVisible = false
        };
        canvas.Children.Add(cyanGlow);

        var contentGrid = new Grid {
            Margin = new Thickness(100, 50, 100, 50)
        };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(270) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(640) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        var topHeader = new Grid();
        topHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var daysBadgeBorder = new Border {
            Background = new SolidColorBrush(Color.FromArgb(45, 255, 42, 141)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 42, 141)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(30),
            Padding = new Thickness(36, 12, 36, 12),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        fsDaysBadge = new TextBlock {
            Text = "⚡ ОСТАЛОСЬ 2 ДНЯ (52 ЧАСА) ДО МИРОВОГО РЕЛИЗА ⚡",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 26,
            FontWeight = FontWeights.ExtraBold,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 110, 180)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        daysBadgeBorder.Child = fsDaysBadge;
        Grid.SetColumn(daysBadgeBorder, 0);
        topHeader.Children.Add(daysBadgeBorder);

        var controlsStack = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var btnBack = new Border {
            Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(22, 10, 22, 10),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 16, 0)
        };
        var txtBack = new TextBlock {
            Text = "🗗 ВЕРНУТЬСЯ (F11)",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        btnBack.Child = txtBack;
        btnBack.MouseLeftButtonDown += (s, e) => { SwitchMode(DisplayMode.TransparentDigits); e.Handled = true; };
        controlsStack.Children.Add(btnBack);

        Grid.SetColumn(controlsStack, 1);
        topHeader.Children.Add(controlsStack);
        Grid.SetRow(topHeader, 0);
        contentGrid.Children.Add(topHeader);

        // Logo
        var logoStack = new StackPanel {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var logoSrc = GetLogoImage();
        if (logoSrc != null) {
            var imgLogo = new Image {
                Source = logoSrc,
                Height = 175,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            logoStack.Children.Add(imgLogo);
        }
        var txtFullTitle = new TextBlock {
            Text = "GRAND THEFT AUTO VI",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };
        logoStack.Children.Add(txtFullTitle);
        Grid.SetRow(logoStack, 1);
        contentGrid.Children.Add(logoStack);

        // Center Digits
        var centerDigitsWrapper = new Grid {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        fsDigitsRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        fsDigitsRow.Children.Add(CreateFsCard(out fsNumHours, out fsLblHours, "52", "ЧАСА", Color.FromRgb(255, 42, 141)));
        fsDigitsRow.Children.Add(CreateFsSeparator());
        fsDigitsRow.Children.Add(CreateFsCard(out fsNumMins, out fsLblMins, "20", "МИНУТ", Color.FromArgb(220, 240, 235, 255)));
        fsDigitsRow.Children.Add(CreateFsSeparator());
        fsDigitsRow.Children.Add(CreateFsCard(out fsNumSecs, out fsLblSecs, "10", "СЕКУНД", Color.FromRgb(0, 242, 254)));

        centerDigitsWrapper.Children.Add(fsDigitsRow);

        fsDoneBox = new Border {
            Background = new SolidColorBrush(Color.FromArgb(60, 0, 255, 140)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 140)),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(32),
            Padding = new Thickness(60, 30, 60, 30),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        var txtFsDone = new TextBlock {
            Text = "🎉 РЕЛИЗ GTA VI СОСТОЯЛСЯ! ДОЖДАЛИСЬ! 🎉",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 54,
            FontWeight = FontWeights.Heavy,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 140)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        fsDoneBox.Child = txtFsDone;
        centerDigitsWrapper.Children.Add(fsDoneBox);

        Grid.SetRow(centerDigitsWrapper, 2);
        contentGrid.Children.Add(centerDigitsWrapper);

        canvas.Children.Add(contentGrid);
        viewbox.Child = canvas;
        outer.Child = viewbox;
        return outer;
    }

    private Border CreateFsCard(out TextBlock valBlock, out TextBlock lblBlock, string initVal, string initLbl, Color accent) {
        var card = new Border {
            Width = 430,
            Height = 360,
            Background = new SolidColorBrush(Color.FromArgb(80, 24, 14, 44)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(170, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(28),
            Margin = new Thickness(10, 0, 10, 0)
        };
        var stack = new StackPanel {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        valBlock = new TextBlock {
            Text = initVal,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 175,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        lblBlock = new TextBlock {
            Text = initLbl,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 28,
            FontWeight = FontWeights.ExtraBold,
            Foreground = new SolidColorBrush(accent),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };
        stack.Children.Add(valBlock);
        stack.Children.Add(lblBlock);
        card.Child = stack;
        return card;
    }

    private TextBlock CreateFsSeparator() {
        return new TextBlock {
            Text = ":",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 120,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(18, 0, 18, 45)
        };
    }

    // =========================================================================
    // MODE SWITCHING & WINDOW SIZING
    // =========================================================================
    public void SwitchMode(DisplayMode newMode) {
        ApplyMode(newMode, true);
    }

    private void ApplyMode(DisplayMode newMode, bool save) {
        // Save bounds of current mode
        if (currentMode == DisplayMode.TransparentDigits) {
            digitsLeft = this.Left;
            digitsTop = this.Top;
            digitsWidth = this.Width;
            digitsHeight = this.Height;
        } else if (currentMode == DisplayMode.CompactCard) {
            cardLeft = this.Left;
            cardTop = this.Top;
            cardWidth = this.Width;
            cardHeight = this.Height;
        }

        currentMode = newMode;

        transparentDigitsView.Visibility = Visibility.Collapsed;
        compactCardView.Visibility = Visibility.Collapsed;
        fullscreenCinemaView.Visibility = Visibility.Collapsed;

        double sw = SystemParameters.PrimaryScreenWidth;
        double sh = SystemParameters.PrimaryScreenHeight;
        if (sw <= 0) sw = 2560;
        if (sh <= 0) sh = 1440;

        if (currentMode == DisplayMode.TransparentDigits) {
            // "ПРОСТО ОГРОМНЫЕ ЦИФРЫ БЕЗ ФОНА ПО ЦЕНТРУ"
            transparentDigitsView.Visibility = Visibility.Visible;
            this.MinWidth = 800;
            this.MinHeight = 250;
            this.MaxWidth = double.PositiveInfinity;
            this.MaxHeight = double.PositiveInfinity;

            if (digitsLeft <= 0 || digitsLeft >= sw - 200) {
                digitsLeft = (sw - digitsWidth) / 2.0;
                digitsTop = (sh - digitsHeight) / 2.0 - 50.0;
            }

            this.Left = digitsLeft;
            this.Top = digitsTop;
            this.Width = digitsWidth;
            this.Height = digitsHeight;
        } else if (currentMode == DisplayMode.CompactCard) {
            // Компактный виджет в углу
            compactCardView.Visibility = Visibility.Visible;
            this.MinWidth = 260;
            this.MinHeight = 115;
            this.MaxWidth = 800;
            this.MaxHeight = 450;

            this.Left = cardLeft;
            this.Top = cardTop;
            this.Width = cardWidth;
            this.Height = cardHeight;
        } else if (currentMode == DisplayMode.FullscreenCinema) {
            // Полноэкранный кинотеатр
            fullscreenCinemaView.Visibility = Visibility.Visible;
            this.MinWidth = sw;
            this.MinHeight = sh;
            this.Left = 0;
            this.Top = 0;
            this.Width = sw;
            this.Height = sh;
        }

        ApplyWindowPosition();
        if (save) SaveConfig();
        UpdateCountdown();
    }

    public void CenterOnScreen() {
        double sw = SystemParameters.PrimaryScreenWidth;
        double sh = SystemParameters.PrimaryScreenHeight;
        if (sw <= 0) sw = 2560;
        if (sh <= 0) sh = 1440;

        this.Left = (sw - this.Width) / 2.0;
        this.Top = (sh - this.Height) / 2.0 - 40.0;
        SaveConfig();
    }

    private void SetDigitFontSize(double size) {
        digitFontSize = size;
        if (tdHoursNum != null) tdHoursNum.FontSize = size;
        if (tdMinsNum != null) tdMinsNum.FontSize = size;
        if (tdSecsNum != null) tdSecsNum.FontSize = size;
        if (tdColon1 != null) tdColon1.FontSize = size * 0.7;
        if (tdColon2 != null) tdColon2.FontSize = size * 0.7;

        this.Width = size * 6.2;
        this.Height = size * 2.0;
        CenterOnScreen();
    }

    private void BuildContextMenu() {
        var menu = new ContextMenu();

        var titleItem = new MenuItem { Header = "🎮 GTA VI Таймер", IsEnabled = false };
        menu.Items.Add(titleItem);
        menu.Items.Add(new Separator());

        // Mode Switching
        var mDigits = new MenuItem { Header = "⭐ Огромные цифры без фона (по центру)" };
        mDigits.Click += (s, e) => SwitchMode(DisplayMode.TransparentDigits);
        menu.Items.Add(mDigits);

        var mCenter = new MenuItem { Header = "🎯 Выровнять ровно по центру экрана" };
        mCenter.Click += (s, e) => CenterOnScreen();
        menu.Items.Add(mCenter);

        // Size sub-menu for digits
        var sizeSub = new MenuItem { Header = "📏 Размер цифр..." };
        var s1 = new MenuItem { Header = "Колоссальные (240px)" };
        s1.Click += (s, e) => SetDigitFontSize(240);
        sizeSub.Items.Add(s1);

        var s2 = new MenuItem { Header = "Огромные (190px — Стандарт 2K)" };
        s2.Click += (s, e) => SetDigitFontSize(190);
        sizeSub.Items.Add(s2);

        var s3 = new MenuItem { Header = "Крупные (140px)" };
        s3.Click += (s, e) => SetDigitFontSize(140);
        sizeSub.Items.Add(s3);
        menu.Items.Add(sizeSub);

        menu.Items.Add(new Separator());

        var mCard = new MenuItem { Header = "🗗 Компактный виджет с карточкой (в углу)" };
        mCard.Click += (s, e) => SwitchMode(DisplayMode.CompactCard);
        menu.Items.Add(mCard);

        var mCinema = new MenuItem { Header = "📺 Полноэкранный кинотеатр (2560×1440 / F11)" };
        mCinema.Click += (s, e) => SwitchMode(DisplayMode.FullscreenCinema);
        menu.Items.Add(mCinema);

        menu.Items.Add(new Separator());

        var itemTopmost = new MenuItem {
            Header = "Поверх всех окон",
            IsCheckable = true,
            IsChecked = isTopmost
        };
        itemTopmost.Click += (s, e) => {
            isTopmost = !isTopmost;
            itemTopmost.IsChecked = isTopmost;
            ApplyWindowPosition();
            SaveConfig();
        };
        menu.Items.Add(itemTopmost);

        var itemLock = new MenuItem {
            Header = "Закрепить положение",
            IsCheckable = true,
            IsChecked = isLocked
        };
        itemLock.Click += (s, e) => {
            isLocked = !isLocked;
            itemLock.IsChecked = isLocked;
            SaveConfig();
        };
        menu.Items.Add(itemLock);

        menu.Items.Add(new Separator());

        var itemClose = new MenuItem { Header = "Закрыть" };
        itemClose.Click += (s, e) => this.Close();
        menu.Items.Add(itemClose);

        this.ContextMenu = menu;
    }

    private void UpdateCountdown() {
        DateTime now = DateTime.Now;
        TimeSpan rDiff = releaseDate - now;

        if (rDiff.TotalSeconds <= 0) {
            // Transparent Digits View
            tdDaysTop.Text = "🎉 ИГРА ВЫШЛА!";
            tdDigitsRow.Visibility = Visibility.Collapsed;
            tdDoneBox.Visibility = Visibility.Visible;

            // Compact View
            txtTopDays.Text = "🎉 РЕЛИЗ СОСТОЯЛСЯ!";
            txtDaysBig.Text = "0";
            txtDaysLbl.Text = "ЧАСОВ";
            txtDaysSub.Text = "игра вышла!";
            rGrid.Visibility = Visibility.Collapsed;
            rDoneBox.Visibility = Visibility.Visible;

            // Fullscreen View
            fsDaysBadge.Text = "🎉 ИГРА ВЫШЛА! РЕЛИЗ СОСТОЯЛСЯ! 🎉";
            fsDigitsRow.Visibility = Visibility.Collapsed;
            fsDoneBox.Visibility = Visibility.Visible;
        } else {
            int d = (int)rDiff.TotalDays;
            int totalHours = (int)rDiff.TotalHours;
            int mins = rDiff.Minutes;
            int secs = rDiff.Seconds;

            string hoursWord = GetHoursDeclension(totalHours);
            string daysWord = GetDaysDeclension(d);

            // 1. Transparent Digits View ("ПРОСТО ЦИФРЫ БЕЗ ФОНА")
            if (d == 0) {
                tdDaysTop.Text = string.Format("⚡ ФИНАЛЬНЫЙ ДЕНЬ • ВСЕГО {0} {1} ДО РЕЛИЗА ⚡", totalHours, hoursWord);
            } else {
                tdDaysTop.Text = string.Format("⚡ ОСТАЛОСЬ {0} {1} ({2} {3}) ДО РЕЛИЗА ⚡", d, daysWord, totalHours, hoursWord);
            }

            tdHoursNum.Text = totalHours.ToString();
            tdHoursLbl.Text = hoursWord;

            tdMinsNum.Text = mins.ToString("D2");
            tdMinsLbl.Text = GetMinsDeclension(mins);

            tdSecsNum.Text = secs.ToString("D2");
            tdSecsLbl.Text = GetSecsDeclension(secs);

            tdDigitsRow.Visibility = Visibility.Visible;
            tdDoneBox.Visibility = Visibility.Collapsed;

            // 2. Compact View
            if (d == 0) {
                txtTopDays.Text = "⚡ ФИНАЛЬНЫЙ ДЕНЬ ДО РЕЛИЗА";
            } else if (d == 1) {
                txtTopDays.Text = "⚡ ОСТАЛСЯ 1 ДЕНЬ ДО РЕЛИЗА";
            } else {
                txtTopDays.Text = string.Format("⚡ ОСТАЛОСЬ {0} {1} ДО РЕЛИЗА", d, daysWord);
            }

            txtDaysBig.Text = totalHours.ToString();
            txtDaysLbl.Text = hoursWord;
            txtDaysSub.Text = "до релиза";

            rH.Text = totalHours.ToString();
            rM.Text = mins.ToString("D2");
            rS.Text = secs.ToString("D2");
            rGrid.Visibility = Visibility.Visible;
            rDoneBox.Visibility = Visibility.Collapsed;

            // 3. Fullscreen View
            fsDaysBadge.Text = tdDaysTop.Text;
            fsNumHours.Text = totalHours.ToString();
            fsLblHours.Text = hoursWord;
            fsNumMins.Text = mins.ToString("D2");
            fsLblMins.Text = GetMinsDeclension(mins);
            fsNumSecs.Text = secs.ToString("D2");
            fsLblSecs.Text = GetSecsDeclension(secs);
            fsDigitsRow.Visibility = Visibility.Visible;
            fsDoneBox.Visibility = Visibility.Collapsed;
        }
    }

    private string GetHoursDeclension(int n) {
        int r100 = n % 100;
        if (r100 >= 11 && r100 <= 19) return "ЧАСОВ";
        int r10 = n % 10;
        if (r10 == 1) return "ЧАС";
        if (r10 >= 2 && r10 <= 4) return "ЧАСА";
        return "ЧАСОВ";
    }

    private string GetDaysDeclension(int n) {
        int r100 = n % 100;
        if (r100 >= 11 && r100 <= 19) return "ДНЕЙ";
        int r10 = n % 10;
        if (r10 == 1) return "ДЕНЬ";
        if (r10 >= 2 && r10 <= 4) return "ДНЯ";
        return "ДНЕЙ";
    }

    private string GetMinsDeclension(int n) {
        int r100 = n % 100;
        if (r100 >= 11 && r100 <= 19) return "МИНУТ";
        int r10 = n % 10;
        if (r10 == 1) return "МИНУТА";
        if (r10 >= 2 && r10 <= 4) return "МИНУТЫ";
        return "МИНУТ";
    }

    private string GetSecsDeclension(int n) {
        int r100 = n % 100;
        if (r100 >= 11 && r100 <= 19) return "СЕКУНД";
        int r10 = n % 10;
        if (r10 == 1) return "СЕКУНДА";
        if (r10 >= 2 && r10 <= 4) return "СЕКУНДЫ";
        return "СЕКУНД";
    }

    private void SaveConfig() {
        try {
            string line = string.Format("{0};{1};{2};{3};{4};{5};{6}",
                (int)this.Left,
                (int)this.Top,
                (int)this.Width,
                (int)this.Height,
                isTopmost ? 1 : 0,
                isLocked ? 1 : 0,
                (int)currentMode);
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

                    if (parts.Length >= 7) {
                        int m = int.Parse(parts[6]);
                        if (m == 0) currentMode = DisplayMode.TransparentDigits;
                        else if (m == 1) currentMode = DisplayMode.CompactCard;
                        else if (m == 2) currentMode = DisplayMode.FullscreenCinema;
                    } else {
                        currentMode = DisplayMode.TransparentDigits;
                    }

                    if (currentMode == DisplayMode.TransparentDigits) {
                        if (w >= 600) digitsWidth = w;
                        if (h >= 200) digitsHeight = h;
                        if (l >= 0 && l < SystemParameters.VirtualScreenWidth - 100) digitsLeft = l;
                        if (t >= 0 && t < SystemParameters.VirtualScreenHeight - 100) digitsTop = t;
                    } else {
                        if (w >= 260) cardWidth = w;
                        if (h >= 115) cardHeight = h;
                        if (l >= 0 && l < SystemParameters.VirtualScreenWidth - 50) cardLeft = l;
                        if (t >= 0 && t < SystemParameters.VirtualScreenHeight - 50) cardTop = t;
                    }

                    if (parts.Length >= 5) isTopmost = parts[4] == "1";
                    if (parts.Length >= 6) isLocked = parts[5] == "1";
                }
            }
        } catch { }
    }
}
