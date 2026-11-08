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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

public class GTA6WidgetApp : Application {
    [STAThread]
    public static void Main() {
        var app = new GTA6WidgetApp();
        app.Run(new WidgetWindow());
    }
}

public static class Win32 {
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

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
    private bool isTopmost = false;

    // Target Dates (Release 18.11 21:30, Preload 09.11 21:30)
    private DateTime releaseDate;
    private DateTime preloadDate;

    // UI Controls
    private TextBlock txtDaysBig;
    private TextBlock txtDaysSub;
    private TextBlock txtPreloadStatus;

    // Release digits
    private TextBlock rD, rH, rM, rS;
    private Border rDoneBox;
    private UniformGrid rGrid;

    // Preload digits
    private TextBlock pD, pH, pM, pS;
    private Border pDoneBox;
    private UniformGrid pGrid;

    // Card borders & toggle
    private Border cardRelease;
    private Border cardPreload;
    private bool hidePreload = false;

    private readonly string configFile = "widget_config.txt";

    public WidgetWindow() {
        this.Title = "GTA VI Countdown Widget";
        this.WindowStyle = WindowStyle.None;
        this.AllowsTransparency = true;
        this.Background = Brushes.Transparent;
        this.ShowInTaskbar = false;
        this.ResizeMode = ResizeMode.NoResize;

        // Calculate Target Dates
        int currentYear = DateTime.Now.Year;
        releaseDate = new DateTime(currentYear, 11, 18, 21, 30, 0);
        if (DateTime.Now - releaseDate > TimeSpan.FromDays(30)) {
            releaseDate = new DateTime(currentYear + 1, 11, 18, 21, 30, 0);
        }

        preloadDate = new DateTime(currentYear, 11, 9, 21, 30, 0);
        if (DateTime.Now - preloadDate > TimeSpan.FromDays(30)) {
            preloadDate = new DateTime(currentYear + 1, 11, 9, 21, 30, 0);
        }

        // Default dimensions (Exact Apple Medium Widget 342 × 158)
        this.Width = 342;
        this.Height = 158;
        this.MinWidth = 260;
        this.MinHeight = 115;
        this.MaxWidth = 800;
        this.MaxHeight = 450;

        // Position on top-right of main screen
        double screenW = SystemParameters.WorkArea.Width;
        double screenH = SystemParameters.WorkArea.Height;
        this.Left = screenW - this.Width - 40;
        this.Top = 40;

        LoadConfig();

        BuildUI();
        BuildContextMenu();

        this.Loaded += OnLoaded;
        this.SourceInitialized += OnSourceInitialized;
        this.MouseLeftButtonDown += OnMouseLeftButtonDown;
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

        // Apply WS_EX_TOOLWINDOW (hides from Alt+Tab) and WS_EX_NOACTIVATE (never steals focus!)
        int exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, exStyle | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);

        // Put on desktop level (behind all normal windows)
        if (!isTopmost) {
            Win32.SetWindowPos(hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        } else {
            Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
        // Note: No DwmSetWindowAttribute backdrop call! That was the cause of the square translucent box.
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        if (!isTopmost) {
            var helper = new WindowInteropHelper(this);
            Win32.SetWindowPos(helper.Handle, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
        if (msg == Win32.WM_NCHITTEST) {
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
        // Clean rounded card: dark twilight gradient with rounded corners and subtle border, NO square artifacts!
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

        // ROW 0: Top Section (Weather Metaphor)
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Top Left: Title & Big Days
        var leftStack = new StackPanel();
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
            Text = "10",
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
        var txtDaysLbl = new TextBlock {
            Text = "ДНЕЙ",
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

        // Top Right: Official Logo & Dates
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

        var txtRelDate = new TextBlock {
            Text = "18.11 в 21:30",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        rightStack.Children.Add(txtRelDate);

        txtPreloadStatus = new TextBlock {
            Text = "⚡ Загрузка: 9.11",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 214, 255)),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        rightStack.Children.Add(txtPreloadStatus);

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

        // ROW 2: Bottom Section (Two Countdown Cards)
        var bottomGrid = new Grid();
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // CARD 1: RELEASE (18.11)
        cardRelease = CreateCountdownCard("🚀 РЕЛИЗ", "18.11", Color.FromRgb(255, 59, 136), out rD, out rH, out rM, out rS, out rDoneBox, out rGrid, "ИГРА ВЫШЛА! 🎉");
        cardRelease.Margin = new Thickness(0, 0, 4, 0);
        Grid.SetColumn(cardRelease, 0);
        bottomGrid.Children.Add(cardRelease);

        // CARD 2: PRELOAD (09.11)
        cardPreload = CreateCountdownCard("⚡ ЗАГРУЗКА", "09.11", Color.FromRgb(0, 242, 254), out pD, out pH, out pM, out pS, out pDoneBox, out pGrid, "ДОСТУПНА! ⚡");
        cardPreload.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(cardPreload, 1);
        bottomGrid.Children.Add(cardPreload);

        Grid.SetRow(bottomGrid, 2);
        mainGrid.Children.Add(bottomGrid);

        UpdatePreloadVisibility(hidePreload);

        outerBorder.Child = mainGrid;
        this.Content = outerBorder;
    }

    private Border CreateCountdownCard(string title, string dateStr, Color accentColor, out TextBlock tbD, out TextBlock tbH, out TextBlock tbM, out TextBlock tbS, out Border doneBox, out UniformGrid grid, string doneText) {
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
            Columns = 4,
            Margin = new Thickness(0, 4, 0, 0)
        };

        grid.Children.Add(CreateDigitBox(out tbD, "ДН", accentColor));
        grid.Children.Add(CreateDigitBox(out tbH, "ЧАС", Colors.White));
        grid.Children.Add(CreateDigitBox(out tbM, "МИН", Colors.White));
        grid.Children.Add(CreateDigitBox(out tbS, "СЕК", Colors.White));
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

    private void BuildContextMenu() {
        var menu = new ContextMenu();

        var titleItem = new MenuItem { Header = "🎮 GTA VI Виджет", IsEnabled = false };
        menu.Items.Add(titleItem);
        menu.Items.Add(new Separator());

        // Preset sizes
        var sizeMenu = new MenuItem { Header = "Сбросить размер к..." };

        var m1 = new MenuItem { Header = "Виджет погоды (342 × 158)" };
        m1.Click += (s, e) => SetSize(342, 158);
        sizeMenu.Items.Add(m1);

        var m2 = new MenuItem { Header = "Компактный (290 × 134)" };
        m2.Click += (s, e) => SetSize(290, 134);
        sizeMenu.Items.Add(m2);

        var m3 = new MenuItem { Header = "Крупный (400 × 185)" };
        m3.Click += (s, e) => SetSize(400, 185);
        sizeMenu.Items.Add(m3);

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
                Win32.SetWindowPos(helper.Handle, Win32.HWND_BOTTOM, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
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

        // Toggle Preload Timer
        var itemPreload = new MenuItem {
            Header = "⚡ Показывать таймер предзагрузки",
            IsCheckable = true,
            IsChecked = !hidePreload
        };
        itemPreload.Click += (s, e) => {
            hidePreload = !itemPreload.IsChecked;
            UpdatePreloadVisibility(hidePreload);
            SaveConfig();
        };
        menu.Items.Add(itemPreload);

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

    private void UpdateCountdown() {
        DateTime now = DateTime.Now;

        // Release Diff
        TimeSpan rDiff = releaseDate - now;
        if (rDiff.TotalSeconds <= 0) {
            txtDaysBig.Text = "0";
            txtDaysSub.Text = "игра вышла!";
            rGrid.Visibility = Visibility.Collapsed;
            rDoneBox.Visibility = Visibility.Visible;
        } else {
            int d = (int)rDiff.TotalDays;
            txtDaysBig.Text = d.ToString();
            txtDaysSub.Text = "до релиза";

            rD.Text = d.ToString("D2");
            rH.Text = rDiff.Hours.ToString("D2");
            rM.Text = rDiff.Minutes.ToString("D2");
            rS.Text = rDiff.Seconds.ToString("D2");
            rGrid.Visibility = Visibility.Visible;
            rDoneBox.Visibility = Visibility.Collapsed;
        }

        // Preload Diff
        TimeSpan pDiff = preloadDate - now;
        if (pDiff.TotalSeconds <= 0) {
            txtPreloadStatus.Text = "⚡ Загрузка активна";
            txtPreloadStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 140));
            pGrid.Visibility = Visibility.Collapsed;
            pDoneBox.Visibility = Visibility.Visible;
        } else {
            txtPreloadStatus.Text = "⚡ Загрузка: 9.11";
            txtPreloadStatus.Foreground = new SolidColorBrush(Color.FromRgb(51, 214, 255));
            int pd = (int)pDiff.TotalDays;
            pD.Text = pd.ToString("D2");
            pH.Text = pDiff.Hours.ToString("D2");
            pM.Text = pDiff.Minutes.ToString("D2");
            pS.Text = pDiff.Seconds.ToString("D2");
            pGrid.Visibility = Visibility.Visible;
            pDoneBox.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdatePreloadVisibility(bool hide) {
        if (cardPreload == null || cardRelease == null) return;
        if (hide) {
            cardPreload.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(cardRelease, 2);
            cardRelease.Margin = new Thickness(0);
            if (txtPreloadStatus != null) txtPreloadStatus.Visibility = Visibility.Collapsed;
        } else {
            cardPreload.Visibility = Visibility.Visible;
            Grid.SetColumnSpan(cardRelease, 1);
            cardRelease.Margin = new Thickness(0, 0, 4, 0);
            if (txtPreloadStatus != null) txtPreloadStatus.Visibility = Visibility.Visible;
        }
    }

    private void SaveConfig() {
        try {
            string line = string.Format("{0};{1};{2};{3};{4};{5};{6}",
                (int)this.Left, (int)this.Top, (int)this.Width, (int)this.Height, isTopmost ? 1 : 0, isLocked ? 1 : 0, hidePreload ? 1 : 0);
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

                    if (w >= this.MinWidth && w <= this.MaxWidth) this.Width = w;
                    if (h >= this.MinHeight && h <= this.MaxHeight) this.Height = h;
                    if (l >= 0 && l < SystemParameters.VirtualScreenWidth - 50) this.Left = l;
                    if (t >= 0 && t < SystemParameters.VirtualScreenHeight - 50) this.Top = t;

                    if (parts.Length >= 5) isTopmost = parts[4] == "1";
                    if (parts.Length >= 6) isLocked = parts[5] == "1";
                    if (parts.Length >= 7) {
                        hidePreload = parts[6] == "1";
                        UpdatePreloadVisibility(hidePreload);
                    }
                }
            }
        } catch { }
    }
}
