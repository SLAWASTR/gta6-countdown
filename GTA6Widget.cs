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

public class GTA6WidgetApp : Application {
    [STAThread]
    public static void Main(string[] args) {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            try { File.WriteAllText("crash.log", e.ExceptionObject.ToString()); } catch { }
        };
        try {
            bool startFullscreen = false;
            if (args != null) {
                for (int i = 0; i < args.Length; i++) {
                    if (args[i].ToLower().Contains("full") || args[i] == "-f") {
                        startFullscreen = true;
                    }
                }
            }
            var app = new GTA6WidgetApp();
            app.DispatcherUnhandledException += (s, e) => {
                try { File.WriteAllText("crash.log", e.Exception.ToString()); } catch { }
            };
            app.Run(new WidgetWindow(startFullscreen));
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
    private bool isFullscreen = false;

    // Previous dimensions for restore
    private double prevLeft = 2148;
    private double prevTop = 15;
    private double prevWidth = 400;
    private double prevHeight = 185;

    // Target Date (Release 18.11 21:30)
    private DateTime releaseDate;

    // Root UI
    private Grid rootGrid;
    private Border widgetCard;
    private Border fullscreenCard;

    // Compact Widget Controls
    private TextBlock txtTopDays;
    private TextBlock txtDaysBig;
    private TextBlock txtDaysLbl;
    private TextBlock txtDaysSub;
    private TextBlock rH, rM, rS;
    private Border rDoneBox;
    private UniformGrid rGrid;
    private Border cardRelease;

    // Fullscreen 2560x1440 Controls
    private TextBlock fsDaysBadge;
    private TextBlock fsNumHours;
    private TextBlock fsLblHours;
    private TextBlock fsNumMins;
    private TextBlock fsLblMins;
    private TextBlock fsNumSecs;
    private TextBlock fsLblSecs;
    private TextBlock fsRelDate;
    private StackPanel fsDigitsRow;
    private Border fsDoneBox;

    private readonly string configFile = "widget_config.txt";

    public WidgetWindow() : this(false) { }

    public WidgetWindow(bool forceFullscreen) {
        this.Title = "GTA VI Countdown";
        this.WindowStyle = WindowStyle.None;
        this.AllowsTransparency = true;
        this.Background = Brushes.Transparent;
        this.ShowInTaskbar = false;
        this.ResizeMode = ResizeMode.NoResize;

        // Calculate Target Date
        int currentYear = DateTime.Now.Year;
        releaseDate = new DateTime(currentYear, 11, 18, 21, 30, 0);
        if (DateTime.Now - releaseDate > TimeSpan.FromDays(30)) {
            releaseDate = new DateTime(currentYear + 1, 11, 18, 21, 30, 0);
        }

        this.Width = 400;
        this.Height = 185;
        this.MinWidth = 260;
        this.MinHeight = 115;
        this.MaxWidth = double.PositiveInfinity;
        this.MaxHeight = double.PositiveInfinity;

        // Default position
        double screenW = SystemParameters.WorkArea.Width;
        this.Left = Math.Max(0, screenW - this.Width - 40);
        this.Top = 40;

        LoadConfig();
        if (forceFullscreen) {
            isFullscreen = true;
        }

        BuildUI();
        BuildContextMenu();

        this.Loaded += OnLoaded;
        this.SourceInitialized += OnSourceInitialized;
        this.MouseLeftButtonDown += OnMouseLeftButtonDown;
        this.MouseDoubleClick += (s, e) => ToggleFullscreen();
        this.KeyDown += OnKeyDown;
        this.LocationChanged += (s, e) => { if (!isFullscreen) SaveConfig(); };
        this.SizeChanged += (s, e) => { if (!isFullscreen) SaveConfig(); };

        // Timer
        timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (s, e) => UpdateCountdown();
        timer.Start();

        UpdateCountdown();
    }

    private void OnKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.F11) {
            ToggleFullscreen();
            e.Handled = true;
        } else if (e.Key == Key.Escape) {
            if (isFullscreen) {
                SetFullscreen(false);
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

        if (isFullscreen) {
            Win32.SetWindowPos(hwnd, isTopmost ? Win32.HWND_TOPMOST : Win32.HWND_TOP, 0, 0, (int)this.Width, (int)this.Height, Win32.SWP_SHOWWINDOW);
        } else {
            if (!isTopmost) {
                Win32.SetWindowPos(hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            } else {
                Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        var helper = new WindowInteropHelper(this);
        if (isFullscreen) {
            bool wantFs = isFullscreen;
            isFullscreen = false;
            SetFullscreen(wantFs);
        } else if (!isTopmost) {
            Win32.SetWindowPos(helper.Handle, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
        if (!isFullscreen && msg == Win32.WM_NCHITTEST) {
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
        if (!isFullscreen && !isLocked && e.ButtonState == MouseButtonState.Pressed) {
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
        rootGrid = new Grid();

        // 1. Build Compact Widget
        widgetCard = BuildCompactWidgetUI();
        rootGrid.Children.Add(widgetCard);

        // 2. Build Fullscreen 2560x1440 UI
        fullscreenCard = BuildFullscreenUI();
        rootGrid.Children.Add(fullscreenCard);

        this.Content = rootGrid;
    }

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
            )
        };

        var mainGrid = new Grid {
            Margin = new Thickness(12, 10, 12, 10)
        };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ROW 0: Top Section
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Top Left: Days on top, Title & Big Total Hours
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

        // Top Right: Logo, Date & Fullscreen Button
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

        var btnFs = new Border {
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5, 1, 5, 2),
            Margin = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand,
            ToolTip = "На весь экран (2560×1440) [F11]"
        };
        var txtFs = new TextBlock {
            Text = "⛶",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        btnFs.Child = txtFs;
        btnFs.MouseLeftButtonDown += (s, e) => { SetFullscreen(true); e.Handled = true; };
        dateRow.Children.Add(btnFs);

        rightStack.Children.Add(dateRow);

        Grid.SetColumn(rightStack, 1);
        topGrid.Children.Add(rightStack);

        Grid.SetRow(topGrid, 0);
        mainGrid.Children.Add(topGrid);

        // ROW 1: Subtle Divider Line
        var divider = new Rectangle {
            Height = 0.5,
            Fill = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
            Margin = new Thickness(0, 6, 0, 6)
        };
        Grid.SetRow(divider, 1);
        mainGrid.Children.Add(divider);

        // ROW 2: Bottom Section
        cardRelease = CreateCountdownCard("🚀 ОБРАТНЫЙ ОТСЧЕТ", "18.11.2026 • 21:30", Color.FromRgb(255, 59, 136), out rH, out rM, out rS, out rDoneBox, out rGrid, "ИГРА ВЫШЛА! 🎉");
        Grid.SetRow(cardRelease, 2);
        mainGrid.Children.Add(cardRelease);

        outerBorder.Child = mainGrid;
        return outerBorder;
    }

    private Border CreateCountdownCard(string title, string dateStr, Color accentColor, out TextBlock tbH, out TextBlock tbM, out TextBlock tbS, out Border doneBox, out UniformGrid grid, string doneText) {
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
    // FULLSCREEN 2560x1440 UI BUILDER ("ПО КРАСОТЕ")
    // =========================================================================
    private Border BuildFullscreenUI() {
        var outer = new Border {
            Background = new LinearGradientBrush(
                Color.FromArgb(238, 14, 8, 28), // 93% deep nocturnal violet with acrylic feel
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

        // Ambient Neon Glow Orbs (Vice City Neon Lighting)
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

        // Main Vertical Grid
        var contentGrid = new Grid {
            Margin = new Thickness(100, 50, 100, 50)
        };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140) }); // Top header
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(270) }); // Logo & Title
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(640) }); // GIANT DIGITS
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Footer

        // ROW 0: Top Header
        var topHeader = new Grid();
        topHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Centered Top Days Badge
        var daysBadgeBorder = new Border {
            Background = new SolidColorBrush(Color.FromArgb(45, 255, 42, 141)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 42, 141)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(30),
            Padding = new Thickness(36, 12, 36, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new DropShadowEffect {
                Color = Color.FromRgb(255, 42, 141),
                BlurRadius = 30,
                ShadowDepth = 0,
                Opacity = 0.55
            }
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

        // Top Right Controls (Switch back to widget / Close)
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
            Text = "🗗 ВЕРНУТЬСЯ В ВИДЖЕТ (F11)",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        btnBack.Child = txtBack;
        btnBack.MouseLeftButtonDown += (s, e) => { SetFullscreen(false); e.Handled = true; };
        controlsStack.Children.Add(btnBack);

        var btnClose = new Border {
            Background = new SolidColorBrush(Color.FromArgb(50, 255, 50, 80)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 50, 80)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(16),
            Width = 44,
            Height = 44,
            Cursor = Cursors.Hand
        };
        var txtClose = new TextBlock {
            Text = "✕",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        btnClose.Child = txtClose;
        btnClose.MouseLeftButtonDown += (s, e) => { this.Close(); e.Handled = true; };
        controlsStack.Children.Add(btnClose);

        Grid.SetColumn(controlsStack, 1);
        topHeader.Children.Add(controlsStack);

        Grid.SetRow(topHeader, 0);
        contentGrid.Children.Add(topHeader);

        // ROW 1: Logo & Title
        var logoStack = new StackPanel {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var logoSrc = GetLogoImage();
        if (logoSrc != null) {
            var imgLogo = new Image {
                Source = logoSrc,
                Height = 175,
                HorizontalAlignment = HorizontalAlignment.Center,
                Effect = new DropShadowEffect {
                    Color = Color.FromRgb(255, 42, 141),
                    BlurRadius = 45,
                    ShadowDepth = 0,
                    Opacity = 0.65
                }
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

        // ROW 2: THE GIANT DIGITS (Cinematic Cards)
        var centerDigitsWrapper = new Grid {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        fsDigitsRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 1. HOURS CARD
        fsDigitsRow.Children.Add(CreateGiantDigitCard(out fsNumHours, out fsLblHours, "52", "ЧАСА", Color.FromRgb(255, 42, 141), true));

        // Separator 1
        fsDigitsRow.Children.Add(CreateGiantSeparator());

        // 2. MINUTES CARD
        fsDigitsRow.Children.Add(CreateGiantDigitCard(out fsNumMins, out fsLblMins, "20", "МИНУТ", Color.FromArgb(220, 240, 235, 255), false));

        // Separator 2
        fsDigitsRow.Children.Add(CreateGiantSeparator());

        // 3. SECONDS CARD (ELECTRIC CYAN LIVE SECONDS)
        fsDigitsRow.Children.Add(CreateGiantDigitCard(out fsNumSecs, out fsLblSecs, "10", "СЕКУНД", Color.FromRgb(0, 242, 254), true));

        centerDigitsWrapper.Children.Add(fsDigitsRow);

        // Done Box for Fullscreen
        fsDoneBox = new Border {
            Background = new SolidColorBrush(Color.FromArgb(60, 0, 255, 140)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 140)),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(32),
            Padding = new Thickness(60, 30, 60, 30),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Effect = new DropShadowEffect {
                Color = Color.FromRgb(0, 255, 140),
                BlurRadius = 50,
                ShadowDepth = 0,
                Opacity = 0.7
            }
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

        // ROW 3: Footer
        var footerStack = new StackPanel {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        fsRelDate = new TextBlock {
            Text = "🚀 МИРОВОЙ РЕЛИЗ: 18 НОЯБРЯ 2026 ГОДА • 21:30 МСК (ВТОРНИК)",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        footerStack.Children.Add(fsRelDate);

        var txtPlatform = new TextBlock {
            Text = "PlayStation 5 • Xbox Series X|S • Vice City & Leonida",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 16,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        footerStack.Children.Add(txtPlatform);

        var txtHints = new TextBlock {
            Text = "[ F11 ] или Двойной клик — Свернуть в виджет  •  [ Esc ] — Выход  •  [ ПКМ ] — Меню настроек",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0)
        };
        footerStack.Children.Add(txtHints);

        Grid.SetRow(footerStack, 3);
        contentGrid.Children.Add(footerStack);

        canvas.Children.Add(contentGrid);
        viewbox.Child = canvas;
        outer.Child = viewbox;
        return outer;
    }

    private Border CreateGiantDigitCard(out TextBlock valBlock, out TextBlock lblBlock, string initVal, string initLbl, Color accent, bool withGlow) {
        var card = new Border {
            Width = 430,
            Height = 360,
            Background = new SolidColorBrush(Color.FromArgb(80, 24, 14, 44)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(170, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(28),
            Margin = new Thickness(10, 0, 10, 0)
        };

        if (withGlow) {
            card.Effect = new DropShadowEffect {
                Color = accent,
                BlurRadius = 40,
                ShadowDepth = 0,
                Opacity = 0.45
            };
        }

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

    private TextBlock CreateGiantSeparator() {
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

    private void BuildContextMenu() {
        var menu = new ContextMenu();

        var titleItem = new MenuItem { Header = "🎮 GTA VI Отсчет до релиза", IsEnabled = false };
        menu.Items.Add(titleItem);
        menu.Items.Add(new Separator());

        // Fullscreen Toggle
        var itemFs = new MenuItem { Header = "📺 На весь экран (2560×1440 / F11)" };
        itemFs.Click += (s, e) => ToggleFullscreen();
        menu.Items.Add(itemFs);

        var itemWidget = new MenuItem { Header = "🗗 Режим виджета (400 × 185)" };
        itemWidget.Click += (s, e) => SetFullscreen(false);
        menu.Items.Add(itemWidget);

        menu.Items.Add(new Separator());

        // Preset sizes
        var sizeMenu = new MenuItem { Header = "Размер виджета..." };

        var m1 = new MenuItem { Header = "Компактный (290 × 134)" };
        m1.Click += (s, e) => { SetFullscreen(false); SetSize(290, 134); };
        sizeMenu.Items.Add(m1);

        var m2 = new MenuItem { Header = "Классический (342 × 158)" };
        m2.Click += (s, e) => { SetFullscreen(false); SetSize(342, 158); };
        sizeMenu.Items.Add(m2);

        var m3 = new MenuItem { Header = "Крупный (400 × 185)" };
        m3.Click += (s, e) => { SetFullscreen(false); SetSize(400, 185); };
        sizeMenu.Items.Add(m3);

        var m4 = new MenuItem { Header = "Большой (550 × 255)" };
        m4.Click += (s, e) => { SetFullscreen(false); SetSize(550, 255); };
        sizeMenu.Items.Add(m4);

        menu.Items.Add(sizeMenu);
        menu.Items.Add(new Separator());

        // Always on top
        var itemTopmost = new MenuItem {
            Header = "Поверх всех окон",
            IsCheckable = true,
            IsChecked = isTopmost
        };
        itemTopmost.Click += (s, e) => {
            isTopmost = !isTopmost;
            itemTopmost.IsChecked = isTopmost;
            var helper = new WindowInteropHelper(this);
            if (isTopmost) {
                this.Topmost = true;
                Win32.SetWindowPos(helper.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            } else {
                this.Topmost = false;
                Win32.SetWindowPos(helper.Handle, isFullscreen ? Win32.HWND_TOP : Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            }
            SaveConfig();
        };
        menu.Items.Add(itemTopmost);

        // Lock Position
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

        // Close
        var itemClose = new MenuItem { Header = "Закрыть виджет" };
        itemClose.Click += (s, e) => this.Close();
        menu.Items.Add(itemClose);

        this.ContextMenu = menu;
    }

    private void SetSize(double w, double h) {
        this.Width = w;
        this.Height = h;
        SaveConfig();
    }

    public void ToggleFullscreen() {
        SetFullscreen(!isFullscreen);
    }

    public void SetFullscreen(bool full) {
        if (full == isFullscreen) return;

        isFullscreen = full;
        if (isFullscreen) {
            // Save widget bounds
            prevLeft = this.Left;
            prevTop = this.Top;
            prevWidth = this.Width;
            prevHeight = this.Height;

            widgetCard.Visibility = Visibility.Collapsed;
            fullscreenCard.Visibility = Visibility.Visible;

            double sw = SystemParameters.PrimaryScreenWidth;
            double sh = SystemParameters.PrimaryScreenHeight;
            if (sw <= 0) sw = 2560;
            if (sh <= 0) sh = 1440;

            this.Left = 0;
            this.Top = 0;
            this.Width = sw;
            this.Height = sh;

            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero) {
                Win32.SetWindowPos(helper.Handle, isTopmost ? Win32.HWND_TOPMOST : Win32.HWND_TOP, 0, 0, (int)sw, (int)sh, Win32.SWP_SHOWWINDOW);
            }
        } else {
            fullscreenCard.Visibility = Visibility.Collapsed;
            widgetCard.Visibility = Visibility.Visible;

            this.Left = prevLeft;
            this.Top = prevTop;
            this.Width = prevWidth;
            this.Height = prevHeight;

            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero) {
                if (!isTopmost) {
                    Win32.SetWindowPos(helper.Handle, Win32.HWND_BOTTOM, (int)prevLeft, (int)prevTop, (int)prevWidth, (int)prevHeight, Win32.SWP_NOACTIVATE);
                } else {
                    Win32.SetWindowPos(helper.Handle, Win32.HWND_TOPMOST, (int)prevLeft, (int)prevTop, (int)prevWidth, (int)prevHeight, Win32.SWP_NOACTIVATE);
                }
            }
        }
        SaveConfig();
        UpdateCountdown();
    }

    private void UpdateCountdown() {
        DateTime now = DateTime.Now;
        TimeSpan rDiff = releaseDate - now;

        if (rDiff.TotalSeconds <= 0) {
            // Compact Widget
            txtTopDays.Text = "🎉 РЕЛИЗ СОСТОЯЛСЯ!";
            txtDaysBig.Text = "0";
            txtDaysLbl.Text = "ЧАСОВ";
            txtDaysSub.Text = "игра вышла!";
            rGrid.Visibility = Visibility.Collapsed;
            rDoneBox.Visibility = Visibility.Visible;

            // Fullscreen
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

            // Compact Widget
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

            // Fullscreen ("По красоте")
            if (d == 0) {
                fsDaysBadge.Text = string.Format("⚡ ФИНАЛЬНЫЙ ДЕНЬ • ВСЕГО {0} {1} ДО РЕЛИЗА ⚡", totalHours, hoursWord);
            } else {
                fsDaysBadge.Text = string.Format("⚡ ОСТАЛОСЬ {0} {1} ({2} {3}) ДО МИРОВОГО РЕЛИЗА ⚡", d, daysWord, totalHours, hoursWord);
            }

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
                (int)(isFullscreen ? prevLeft : this.Left),
                (int)(isFullscreen ? prevTop : this.Top),
                (int)(isFullscreen ? prevWidth : this.Width),
                (int)(isFullscreen ? prevHeight : this.Height),
                isTopmost ? 1 : 0,
                isLocked ? 1 : 0,
                isFullscreen ? 1 : 0);
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

                    if (w >= this.MinWidth) prevWidth = w;
                    if (h >= this.MinHeight) prevHeight = h;
                    if (l >= 0 && l < SystemParameters.VirtualScreenWidth - 50) prevLeft = l;
                    if (t >= 0 && t < SystemParameters.VirtualScreenHeight - 50) prevTop = t;

                    this.Left = prevLeft;
                    this.Top = prevTop;
                    this.Width = prevWidth;
                    this.Height = prevHeight;

                    if (parts.Length >= 5) isTopmost = parts[4] == "1";
                    if (parts.Length >= 6) isLocked = parts[5] == "1";
                    if (parts.Length >= 7) isFullscreen = parts[6] == "1";
                }
            }
        } catch { }
    }
}
