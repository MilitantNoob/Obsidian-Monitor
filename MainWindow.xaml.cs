using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using LibreHardwareMonitor.Hardware;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;

// Additional Media & Control aliases required due to complete file overwrite
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using RotateTransform = System.Windows.Media.RotateTransform;
using Geometry = System.Windows.Media.Geometry;
using StreamGeometry = System.Windows.Media.StreamGeometry;
using StreamGeometryContext = System.Windows.Media.StreamGeometryContext;
using ImageSource = System.Windows.Media.ImageSource;
using Image = System.Windows.Controls.Image;
using BrushConverter = System.Windows.Media.BrushConverter;
using Size = System.Windows.Size;
using SweepDirection = System.Windows.Media.SweepDirection;
using ScaleTransform = System.Windows.Media.ScaleTransform;
using TranslateTransform = System.Windows.Media.TranslateTransform;
using TransformGroup = System.Windows.Media.TransformGroup;
using MahApps.Metro.IconPacks;

namespace ObsidianMonitor
{
    public class Arc : Shape
    {
        public static readonly DependencyProperty StartAngleProperty =
            DependencyProperty.Register("StartAngle", typeof(double), typeof(Arc),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty EndAngleProperty =
            DependencyProperty.Register("EndAngle", typeof(double), typeof(Arc),
                new FrameworkPropertyMetadata(90.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double StartAngle
        {
            get { return (double)GetValue(StartAngleProperty); }
            set { SetValue(StartAngleProperty, value); }
        }

        public double EndAngle
        {
            get { return (double)GetValue(EndAngleProperty); }
            set { SetValue(EndAngleProperty, value); }
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                var geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    DrawGeometry(context);
                }
                return geometry;
            }
        }

        private void DrawGeometry(StreamGeometryContext context)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            Point center = new Point(w / 2, h / 2);
            double rx = (w - StrokeThickness) / 2;
            double ry = (h - StrokeThickness) / 2;
            if (rx <= 0 || ry <= 0) return;

            double angleSpan = EndAngle - StartAngle;
            if (angleSpan <= 0) return;

            if (angleSpan >= 360)
            {
                context.BeginFigure(new Point(center.X + rx, center.Y), false, true);
                context.ArcTo(new Point(center.X - rx, center.Y), new Size(rx, ry), 0, false, SweepDirection.Clockwise, true, false);
                context.ArcTo(new Point(center.X + rx, center.Y), new Size(rx, ry), 0, false, SweepDirection.Clockwise, true, false);
                return;
            }

            double startAngleRad = (StartAngle - 90) * Math.PI / 180.0;
            double endAngleRad = (EndAngle - 90) * Math.PI / 180.0;

            Point startPoint = new Point(center.X + Math.Cos(startAngleRad) * rx, center.Y + Math.Sin(startAngleRad) * ry);
            Point endPoint = new Point(center.X + Math.Cos(endAngleRad) * rx, center.Y + Math.Sin(endAngleRad) * ry);
            bool isLargeArc = angleSpan > 180;

            context.BeginFigure(startPoint, false, false);
            context.ArcTo(endPoint, new Size(rx, ry), 0, isLargeArc, SweepDirection.Clockwise, true, false);
        }
    }

    public class DashboardUIElement
    {
        public Border Container { get; set; } = null!;
        public TextBlock MainText { get; set; } = null!;
        public TextBlock SubText { get; set; } = null!;
        public Arc PrimaryArc { get; set; } = null!;
        public FrameworkElement IconText { get; set; } = null!;
        public RotateTransform FanRotation { get; set; } = null!;
        // Game Icon
        public Image AppIcon { get; set; } = null!;
        public double CurrentAngle { get; set; } = 0;
    }

    public class FpsTracker : IDisposable
    {
        private TraceEventSession? _session;
        private System.Threading.Tasks.Task? _traceTask;
        private System.Collections.Concurrent.ConcurrentDictionary<int, int> _processFrames = new();
        public int CurrentFps { get; private set; }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public FpsTracker()
        {
            try
            {
                if (TraceEventSession.IsElevated() == true)
                {
                    _session = new TraceEventSession("ObsidianFpsSession");
                    _session.StopOnDispose = true;
                    _session.EnableProvider(new Guid("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9")); // DXGI
                    _session.EnableProvider(new Guid("783ACA0A-790E-4D7F-8451-AA850511C6B9")); // D3D9
                    
                    _session.Source.Dynamic.All += data => {
                        try {
                            if (data.EventName != null && (data.EventName.Contains("Present_Start") || data.EventName.Contains("Present/Start"))) {
                                _processFrames.AddOrUpdate(data.ProcessID, 1, (_, count) => count + 1);
                            }
                        } catch { }
                    };

                    _traceTask = System.Threading.Tasks.Task.Run(() => { try { _session.Source.Process(); } catch { } });

                    var t = new System.Timers.Timer(1000);
                    t.Elapsed += (s, e) => {
                        IntPtr hwnd = GetForegroundWindow();
                        GetWindowThreadProcessId(hwnd, out uint activePid);
                        
                        if (_processFrames.TryGetValue((int)activePid, out int activeFps)) {
                            CurrentFps = activeFps;
                        } else {
                            int maxFps = 0;
                            foreach (var kvp in _processFrames) {
                                if (kvp.Value > maxFps && kvp.Key != System.Diagnostics.Process.GetCurrentProcess().Id) {
                                    maxFps = kvp.Value;
                                }
                            }
                            CurrentFps = maxFps;
                        }
                        _processFrames.Clear();
                    };
                    t.Start();
                }
            }
            catch { }
        }

        public void Dispose()
        {
            try { _session?.Dispose(); } catch { }
        }
    }

    public partial class MainWindow : Window
    {
        private Computer _computer;
        private FpsTracker _fpsTracker;
        private ProcessManager _processManager;

        private DispatcherTimer _timer;
        private DispatcherTimer _animationTimer;
        private AppConfig _config;
        private System.Windows.Forms.NotifyIcon _notifyIcon;

        private readonly SolidColorBrush _cyanBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#00FFFF")!;
        private readonly SolidColorBrush _yellowBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#FFBF00")!;
        private readonly SolidColorBrush _redBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#FF0033")!;
        private readonly SolidColorBrush _greenBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#00FF66")!;
        private readonly SolidColorBrush _dimBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#1AFFFFFF")!;
        private readonly SolidColorBrush _darkBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#121212")!;
        private readonly SolidColorBrush _borderBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("#222222")!;

        private Dictionary<string, DashboardUIElement> _uiRefs = new();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        private const int HOTKEY_ID = 9000;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        
        public List<string> AvailableFans { get; private set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            _config = AppConfig.Load();

            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(60));

            _cyanBrush.Freeze(); _yellowBrush.Freeze(); _redBrush.Freeze(); _greenBrush.Freeze(); _dimBrush.Freeze(); _darkBrush.Freeze(); _borderBrush.Freeze();

            if (_config.PositionX == -1) 
            {
                var screen = SystemParameters.PrimaryScreenWidth;
                _config.PositionX = (screen - this.Width) / 2;
            }
            this.Left = _config.PositionX;
            this.Top = _config.PositionY;

            _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, IsMotherboardEnabled = true, IsControllerEnabled = true };
            _fpsTracker = new FpsTracker();
            _processManager = new ProcessManager();
            _notifyIcon = new System.Windows.Forms.NotifyIcon();

            SetupNotifyIcon();
            InitHardwareMonitor();
            DiscoverFans(); 

            // Sanitize legacy or disabled ghost fans
            for (int i = _config.OrderedElements.Count - 1; i >= 0; i--)
            {
                string el = _config.OrderedElements[i];
                if (el == "FANS" || (el.StartsWith("FAN:") && !AvailableFans.Contains(el.Substring(4))))
                {
                    _config.OrderedElements.RemoveAt(i);
                }
            }

            BuildDynamicUI();
            ApplyConfig();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();

            this.Loaded += (s, e) => EnforceTopmost();
            this.LocationChanged += MainWindow_LocationChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = PresentationSource.FromVisual(this) as HwndSource;
            source?.AddHook(HwndHook);
            ReRegisterHotkey();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleVisibility();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void ReRegisterHotkey()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            UnregisterHotKey(hwnd, HOTKEY_ID);
            RegisterHotKey(hwnd, HOTKEY_ID, _config.ToggleModifier, _config.ToggleKey);
        }

        private bool _isHudVisible = true;
        
        public void ToggleVisibility()
        {
            _isHudVisible = !_isHudVisible;
            if (_isHudVisible)
            {
                var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
                MasterTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(450)) { EasingFunction = ease });
                MainContainer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(300)));
            }
            else
            {
                var ease = new QuinticEase { EasingMode = EasingMode.EaseIn };
                MasterTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(-250, TimeSpan.FromMilliseconds(350)) { EasingFunction = ease });
                MainContainer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)));
            }
        }

        private void DiscoverFans()
        {
            AvailableFans.Clear();
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Fan)
                    {
                        // Unique name incorporating hw to avoid overwriting multiple GPU/CPU fans with same label
                        string uniqueName = $"{hw.HardwareType}_{s.Name}";
                        AvailableFans.Add(uniqueName);
                    }
                }
            }
        }

        private void SetupNotifyIcon()
        {
            try {
                if (System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico"))) {
                    _notifyIcon.Icon = new System.Drawing.Icon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico"));
                } else if (System.IO.File.Exists("logo.ico")) {
                    _notifyIcon.Icon = new System.Drawing.Icon("logo.ico");
                } else {
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            } catch {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Obsidian Monitor HUD";
            
            var strip = new System.Windows.Forms.ContextMenuStrip();
            strip.Items.Add("Settings", null, (s, e) => OpenSettings());
            strip.Items.Add("Exit", null, (s, e) => System.Windows.Application.Current.Shutdown());
            
            _notifyIcon.ContextMenuStrip = strip;
            _notifyIcon.DoubleClick += (s, e) => OpenSettings();
            _notifyIcon.MouseClick += (s, e) => {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    OpenSettings();
            };
        }

        private void OpenSettings()
        {
            _config.PositionX = this.Left;
            _config.PositionY = this.Top;
            var sw = new SettingsWindow(_config, this);
            sw.Show();
        }

        public void RebuildUIAndApplyConfig()
        {
            BuildDynamicUI();
            ApplyConfig();
            Timer_Tick(null, EventArgs.Empty);
        }

        public void ApplyConfig()
        {
            SettingsScale.ScaleX = _config.SizeScale;
            SettingsScale.ScaleY = _config.SizeScale;
            
            if (_config.IsLocked)
            {
                // Snap exactly to mathematical center when locked.
                Dispatcher.BeginInvoke(new Action(() => {
                    if (this.ActualWidth > 0 && !double.IsNaN(this.ActualWidth))
                    {
                        this.Left = (SystemParameters.PrimaryScreenWidth - this.ActualWidth) / 2;
                        _config.PositionX = this.Left;
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else
            {
                if (this.Left != _config.PositionX) this.Left = _config.PositionX;
            }
            
            if (this.Top != _config.PositionY) this.Top = _config.PositionY;

            Brush bgColor = _config.Theme switch
            {
                "White" => new SolidColorBrush(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF)),
                "Glass" => new SolidColorBrush(Color.FromArgb(0x44, 0x00, 0x00, 0x00)),
                "Custom" => (SolidColorBrush)new BrushConverter().ConvertFromString(_config.CustomColor)!,
                _ => new SolidColorBrush(Color.FromArgb(0xFF, 0x05, 0x05, 0x05))
            };
            if(bgColor.CanFreeze) bgColor.Freeze();
            MainContainer.Background = bgColor;

            if (_config.Theme == "White") {
                MainContainer.BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0,0,0));
            } else {
                MainContainer.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1B, 0x1B, 0x1B));
            }

            foreach (var kvp in _uiRefs)
            {
                if(kvp.Key == "CPU" || kvp.Key == "GPU" || kvp.Key == "RAM") {
                    if (kvp.Value.SubText != null)
                        kvp.Value.SubText.Visibility = _config.ShowTemperature ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            ApplyPinState();
        }

        private void ApplyPinState()
        {
            if (PinIcon != null)
            {
                PinIcon.Kind = _config.IsProcessMonitorPinned ? PackIconMaterialKind.Pin : PackIconMaterialKind.PinOutline;
                PinIcon.Foreground = _config.IsProcessMonitorPinned ? Brushes.White : Brushes.Gray;
            }

            if (_config.IsProcessMonitorPinned)
            {
                ProcessContainer.Visibility = Visibility.Visible;
                ProcessContainer.Opacity = 1;
                if (MasterStack.Children.IndexOf(ProcessContainer) != 1)
                {
                    MasterStack.Children.Remove(ProcessContainer);
                    MasterStack.Children.Insert(1, ProcessContainer);
                }
            }
            else
            {
                if (MasterStack.Children.IndexOf(ProcessContainer) != 2)
                {
                    MasterStack.Children.Remove(ProcessContainer);
                    MasterStack.Children.Insert(2, ProcessContainer);
                }
            }
        }

        private void BuildDynamicUI()
        {
            if (ElementsContainer == null) return;
            ElementsContainer.Children.Clear();
            _uiRefs.Clear();

            foreach(var key in _config.OrderedElements)
            {
                DashboardUIElement el = null!;
                
                if (key == "CPU") el = CreateHardwareCapsule(key, "CPU", _cyanBrush, "--°C", 40, PackIconMaterialKind.Cpu64Bit);
                else if (key == "GPU") el = CreateHardwareCapsule(key, "GPU", _redBrush, "--°C", 40, PackIconMaterialKind.ExpansionCardVariant); 
                else if (key == "RAM") el = CreateHardwareCapsule(key, "RAM", _yellowBrush, "-- GB", 55, PackIconMaterialKind.Memory);
                else if (key == "FPS") el = CreateFpsCapsule();
                else if (key == "Latency") el = CreateGenericCapsule(key, "LAT", "MS", "--", _yellowBrush, 30, PackIconMaterialKind.Speedometer);
                else if (key == "1% Low") el = CreateGenericCapsule(key, "1% L", "FPS", "--", _redBrush, 30, PackIconMaterialKind.ChartTimelineVariant);
                else if (key == "Avg FPS") el = CreateGenericCapsule(key, "AVG", "FPS", "--", _greenBrush, 40, PackIconMaterialKind.ChartLine);
                else if (key == "GPU Watts") el = CreateGenericCapsule(key, "PWR", "W", "0", _yellowBrush, 35, PackIconMaterialKind.LightningBolt);
                else if (key.StartsWith("FAN:"))
                {
                    string fName = key.Substring(4);
                    el = CreateFanCapsule(key, fName);
                }

                if (el != null)
                {
                    _uiRefs[key] = el;
                    ElementsContainer.Children.Add(el.Container);
                }
            }
            BuildTrayUI();
        }

        private void BuildTrayUI()
        {
            if (TrayElementsContainer == null) return;
            TrayElementsContainer.Children.Clear();

            var allKeys = new System.Collections.Generic.List<string> { "CPU", "GPU", "RAM", "FPS", "Latency", "1% Low", "Avg FPS", "GPU Watts" };
            if (AvailableFans != null)
            {
                foreach(var f in AvailableFans) allKeys.Add("FAN:" + f);
            }

            foreach(var key in allKeys)
            {
                var iconKind = GetIconForKey(key);
                bool isOff = !_config.OrderedElements.Contains(key);
                
                var border = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(10,6,10,6), Margin = new Thickness(4), Cursor = System.Windows.Input.Cursors.Hand };
                border.Background = isOff ? new SolidColorBrush(Color.FromArgb(0x44, 0x55, 0x55, 0x55)) : _dimBrush;
                var icon = new PackIconMaterial { Kind = iconKind, Width=16, Height=16, Foreground = isOff ? Brushes.Gray : Brushes.White, VerticalAlignment = VerticalAlignment.Center };
                
                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                stack.Children.Add(icon);
                stack.Children.Add(new TextBlock { Text = key.Replace("FAN:", ""), Foreground = isOff ? Brushes.Gray : Brushes.White, FontSize=11, FontWeight=FontWeights.Bold, Margin=new Thickness(6,0,0,0), VerticalAlignment = VerticalAlignment.Center });
                border.Child = stack;

                border.MouseLeftButtonDown += (s, e) => {
                    e.Handled = true;
                    if (_config.OrderedElements.Contains(key)) _config.OrderedElements.Remove(key);
                    else _config.OrderedElements.Add(key);
                    RebuildUIAndApplyConfig();
                };

                TrayElementsContainer.Children.Add(border);
            }

            // --- PROCESS MONITOR TRAY CONTROLS ---
            var processDiv = new Border { 
                CornerRadius = new CornerRadius(12), 
                Padding = new Thickness(12,6,12,6), 
                Margin = new Thickness(4,4,4,4), 
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x44,0xFF,0xFF,0xFF)), 
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = _config.IsProcessMonitorActive ? _dimBrush : Brushes.Transparent
            };
            
            processDiv.MouseEnter += (s,e) => { if (!_config.IsProcessMonitorActive) processDiv.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF,0xFF,0xFF)); };
            processDiv.MouseLeave += (s,e) => { if (!_config.IsProcessMonitorActive) processDiv.Background = Brushes.Transparent; };

            var scrollStack = new StackPanel { Orientation = Orientation.Horizontal };
            
            var procIcon = new PackIconMaterial { Kind = PackIconMaterialKind.MonitorStar, Width=16, Height=16, Foreground = _config.IsProcessMonitorActive ? Brushes.White : Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
            var procText = new TextBlock { Text = "APPS", Foreground = _config.IsProcessMonitorActive ? Brushes.White : Brushes.Gray, FontSize=11, FontWeight=FontWeights.Bold, Margin=new Thickness(6,0,10,0), VerticalAlignment = VerticalAlignment.Center };
            
            scrollStack.Children.Add(procIcon);
            scrollStack.Children.Add(procText);

            if (_config.IsProcessMonitorActive)
            {
                string[] opts = { "CPU", "RAM", "GPU", "DISK", "NET" };
                PackIconMaterialKind[] icns = { PackIconMaterialKind.Cpu64Bit, PackIconMaterialKind.Memory, PackIconMaterialKind.ExpansionCardVariant, PackIconMaterialKind.Harddisk, PackIconMaterialKind.NetworkOutline };

                foreach(var opt in opts) {
                    bool isActive = _config.ProcessSortMetric == (opt == "NET" ? "NETWORK" : opt);
                    var metricBtn = new Border {
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(2, 0, 2, 0),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Background = isActive ? _cyanBrush : new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
                        BorderBrush = isActive ? _cyanBrush : new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                        BorderThickness = new Thickness(1)
                    };

                    var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
                    btnStack.Children.Add(new PackIconMaterial { 
                        Kind = icns[Array.IndexOf(opts, opt)], 
                        Width = 11, Height = 11, 
                        Foreground = isActive ? Brushes.Black : Brushes.White, 
                        VerticalAlignment = VerticalAlignment.Center, 
                        Margin = new Thickness(0,0,5,0) 
                    });
                    btnStack.Children.Add(new TextBlock { 
                        Text = opt, 
                        Foreground = isActive ? Brushes.Black : Brushes.White, 
                        FontSize = 10, 
                        FontWeight = FontWeights.Bold, 
                        VerticalAlignment = VerticalAlignment.Center 
                    });
                    metricBtn.Child = btnStack;

                    metricBtn.MouseEnter += (s,e) => { if (!isActive) metricBtn.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)); };
                    metricBtn.MouseLeave += (s,e) => { if (!isActive) metricBtn.Background = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)); };

                    metricBtn.MouseLeftButtonDown += (s, e) => {
                        e.Handled = true;
                        string targetMetric = (opt == "NET" ? "NETWORK" : opt);
                        if (_config.ProcessSortMetric != targetMetric)
                        {
                            _config.ProcessSortMetric = targetMetric;
                            _config.Save();
                            RebuildUIAndApplyConfig();
                        }
                    };

                    scrollStack.Children.Add(metricBtn);
                }
            }

            processDiv.MouseLeftButtonDown += (s, e) => {
                e.Handled = true;
                _config.IsProcessMonitorActive = !_config.IsProcessMonitorActive;
                _config.Save();
                RebuildUIAndApplyConfig();
            };

            processDiv.Child = scrollStack;
            TrayElementsContainer.Children.Add(processDiv);
        }


        private PackIconMaterialKind GetIconForKey(string key)
        {
            if (key == "CPU") return PackIconMaterialKind.Cpu64Bit;
            if (key == "GPU") return PackIconMaterialKind.ExpansionCardVariant;
            if (key == "RAM") return PackIconMaterialKind.Memory;
            if (key == "FPS") return PackIconMaterialKind.Target;
            if (key == "Latency") return PackIconMaterialKind.Speedometer;
            if (key == "1% Low") return PackIconMaterialKind.ChartTimelineVariant;
            if (key == "Avg FPS") return PackIconMaterialKind.ChartLine;
            if (key == "GPU Watts") return PackIconMaterialKind.LightningBolt;
            if (key.StartsWith("FAN:")) return PackIconMaterialKind.Fan;
            return PackIconMaterialKind.Card;
        }

        private bool _isDraggingCapsule = false;
        private Point _dragStartPos;
        private Border? _draggedCapsule;
        private string _draggedKey = "";

        private struct DragTarget
        {
            public FrameworkElement Element;
            public int OriginalIndex;
            public double CenterX;
            public double WidthAndMargin;
        }
        private List<DragTarget> _dragTargets = new List<DragTarget>();

        private void AttachCapsuleInteractivity(Border outlineContainer, string key, PackIconMaterial closeBtn)
        {
            var scaleTransform = new ScaleTransform(1.0, 1.0);
            var translateTransform = new TranslateTransform(0, 0);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
            outlineContainer.RenderTransform = transformGroup;
            outlineContainer.RenderTransformOrigin = new Point(0.5, 0.5);

            outlineContainer.MouseEnter += (s, e) => {
                if (!_isDraggingCapsule) {
                    closeBtn.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(200)));
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.08, TimeSpan.FromMilliseconds(300)) { EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut } });
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.08, TimeSpan.FromMilliseconds(300)) { EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut } });
                }
            };

            outlineContainer.MouseLeave += (s, e) => {
                closeBtn.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));
                if (!_isDraggingCapsule) {
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
                }
            };

            outlineContainer.MouseLeftButtonDown += (s, e) => {
                if (e.OriginalSource == closeBtn) return;
                _isDraggingCapsule = true;
                _draggedCapsule = outlineContainer;
                _draggedKey = key;
                _dragStartPos = e.GetPosition(ElementsContainer);
                outlineContainer.CaptureMouse();
                System.Windows.Controls.Panel.SetZIndex(outlineContainer, 1000);
                
                _dragTargets.Clear();
                double currentX = 0;
                for (int i=0; i<ElementsContainer.Children.Count; i++) {
                    var el = (FrameworkElement)ElementsContainer.Children[i];
                    double w = el.ActualWidth + el.Margin.Left + el.Margin.Right;
                    _dragTargets.Add(new DragTarget {
                        Element = el,
                        OriginalIndex = i,
                        CenterX = currentX + (w / 2),
                        WidthAndMargin = w
                    });
                    currentX += w;
                }

                e.Handled = true;
            };

            outlineContainer.MouseMove += (s, e) => {
                if (_isDraggingCapsule && _draggedCapsule == outlineContainer) {
                    var pos = e.GetPosition(ElementsContainer);
                    var diffX = pos.X - _dragStartPos.X;
                    var diffY = pos.Y - _dragStartPos.Y;
                    
                    if (Math.Abs(diffY) > 15 && Math.Abs(diffY) > Math.Abs(diffX)) {
                        _isDraggingCapsule = false;
                        outlineContainer.ReleaseMouseCapture();
                        translateTransform.X = 0;
                        System.Windows.Controls.Panel.SetZIndex(outlineContainer, 0);
                        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200)));
                        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200)));
                        
                        foreach (var dt in _dragTargets) {
                            if (dt.Element == outlineContainer) continue;
                            if (dt.Element.RenderTransform is TransformGroup tg && tg.Children.Count > 1 && tg.Children[1] is TranslateTransform tr) {
                                dt.Element.Tag = 0.0;
                                tr.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
                            }
                        }
                        
                        if (!_config.IsLocked) this.DragMove();
                        return;
                    }
                    translateTransform.X = diffX;

                    int originalIndex = _dragTargets.FindIndex(x => x.Element == outlineContainer);
                    if (originalIndex != -1) {
                        double myWidth = _dragTargets[originalIndex].WidthAndMargin;
                        double myNewCenterX = _dragTargets[originalIndex].CenterX + diffX;
                        
                        int targetIndex = 0;
                        for(int i=0; i<_dragTargets.Count; i++) {
                            if (_dragTargets[i].Element == outlineContainer) continue;
                            if (myNewCenterX > _dragTargets[i].CenterX) targetIndex++;
                        }

                        for (int i=0; i<_dragTargets.Count; i++) {
                            var dt = _dragTargets[i];
                            if (dt.Element == outlineContainer) continue;

                            double targetOffset = 0;
                            if (dt.OriginalIndex < originalIndex && dt.OriginalIndex >= targetIndex) {
                                targetOffset = myWidth;
                            } else if (dt.OriginalIndex > originalIndex && dt.OriginalIndex <= targetIndex) {
                                targetOffset = -myWidth;
                            }

                            if (dt.Element.RenderTransform is TransformGroup siblingTransformGroup && siblingTransformGroup.Children.Count > 1) {
                                if (siblingTransformGroup.Children[1] is TranslateTransform siblingTranslate) {
                                    if (dt.Element.Tag == null || (double)dt.Element.Tag != targetOffset) {
                                        dt.Element.Tag = targetOffset;
                                        siblingTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(targetOffset, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } });
                                    }
                                }
                            }
                        }
                    }
                }
            };

            outlineContainer.MouseLeftButtonUp += (s, e) => {
                if (_isDraggingCapsule && _draggedCapsule == outlineContainer) {
                    _isDraggingCapsule = false;
                    outlineContainer.ReleaseMouseCapture();
                    System.Windows.Controls.Panel.SetZIndex(outlineContainer, 0);
                    
                    int targetIndex = _dragTargets.FindIndex(x => x.Element == outlineContainer);
                    if (targetIndex != -1) {
                        double myNewCenterX = _dragTargets[targetIndex].CenterX + translateTransform.X;
                        targetIndex = 0;
                        for(int i=0; i<_dragTargets.Count; i++) {
                            if (_dragTargets[i].Element == outlineContainer) continue;
                            if (myNewCenterX > _dragTargets[i].CenterX) targetIndex++;
                        }
                    } else {
                        targetIndex = 0;
                    }

                    if (targetIndex != _config.OrderedElements.IndexOf(_draggedKey)) {
                        _config.OrderedElements.Remove(_draggedKey);
                        if (targetIndex > _config.OrderedElements.Count) targetIndex = _config.OrderedElements.Count;
                        _config.OrderedElements.Insert(targetIndex, _draggedKey);
                        _config.Save();
                        translateTransform.X = 0;
                        RebuildUIAndApplyConfig();
                    } else {
                        foreach (var dt in _dragTargets) {
                            if (dt.Element.RenderTransform is TransformGroup tg && tg.Children.Count > 1 && tg.Children[1] is TranslateTransform tr) {
                                dt.Element.Tag = 0.0;
                                tr.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new BackEase { Amplitude=0.5, EasingMode = EasingMode.EaseOut } });
                            }
                        }
                        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
                        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
                    }
                }
            };
        }

        private DashboardUIElement CreateHardwareCapsule(string key, string title, Brush accent, string defaultSubText, double subWidth, PackIconMaterialKind iconKind)
        {
            var outlineContainer = new Border { Background=_darkBrush, CornerRadius=new CornerRadius(28), Margin=new Thickness(0,0,8,0), BorderBrush=_borderBrush, BorderThickness=new Thickness(1), ClipToBounds = true };
            var baseGrid = new Grid();
            
            var watermark = new PackIconMaterial { Kind = iconKind, Width=48, Height=48, Foreground = accent, Opacity = 0.12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,-8,0) };
            baseGrid.Children.Add(watermark);

            var closeBtn = new PackIconMaterial { Kind = PackIconMaterialKind.CloseCircleOutline, Width=14, Height=14, Foreground = Brushes.White, HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Top, Margin=new Thickness(0,6,6,0), Cursor=System.Windows.Input.Cursors.Hand, Opacity=0 };
            closeBtn.MouseLeftButtonDown += (s,e) => { e.Handled = true; _config.OrderedElements.Remove(key); RebuildUIAndApplyConfig(); };
            
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin=new Thickness(12,6,18,6) };
            
            var grid = new Grid { Width = 40, Height = 40 };
            var baseArc = new Arc { Stroke = _dimBrush, StrokeThickness = 5, StartAngle = 0, EndAngle = 360 };
            var fillArc = new Arc { Stroke = accent, StrokeThickness = 5, StartAngle = 0, EndAngle = 0 };
            var centerText = new TextBlock { Text = "0%", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 13, FontWeight = FontWeights.Bold };
            grid.Children.Add(baseArc); grid.Children.Add(fillArc); grid.Children.Add(centerText);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) };
            
            var titleText = new TextBlock { Text = title, Foreground = accent, FontSize = 12, FontWeight = FontWeights.ExtraBold, VerticalAlignment = VerticalAlignment.Center };
            var subTitleText = new TextBlock { Text = defaultSubText, Width=subWidth, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.SemiBold };
            textStack.Children.Add(titleText); textStack.Children.Add(subTitleText);

            row.Children.Add(grid); row.Children.Add(textStack);
            baseGrid.Children.Add(row);
            baseGrid.Children.Add(closeBtn);
            
            AttachCapsuleInteractivity(outlineContainer, key, closeBtn);

            outlineContainer.Child = baseGrid;
            return new DashboardUIElement { Container = outlineContainer, MainText = centerText, SubText = subTitleText, PrimaryArc = fillArc };
        }

        private DashboardUIElement CreateFpsCapsule()
        {
            var outlineContainer = new Border { Background=_darkBrush, CornerRadius=new CornerRadius(28), Margin=new Thickness(0,0,8,0), BorderBrush=_borderBrush, BorderThickness=new Thickness(1), ClipToBounds = true };
            var baseGrid = new Grid();
            
            var watermark = new PackIconMaterial { Kind = PackIconMaterialKind.Target, Width=48, Height=48, Foreground = _greenBrush, Opacity = 0.12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,-8,0) };
            baseGrid.Children.Add(watermark);

            var closeBtn = new PackIconMaterial { Kind = PackIconMaterialKind.CloseCircleOutline, Width=14, Height=14, Foreground = Brushes.White, HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Top, Margin=new Thickness(0,6,6,0), Cursor=System.Windows.Input.Cursors.Hand, Opacity=0 };
            closeBtn.MouseLeftButtonDown += (s,e) => { e.Handled = true; _config.OrderedElements.Remove("FPS"); RebuildUIAndApplyConfig(); };

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin=new Thickness(12,6,22,6) };

            var iconBox = new Image { Width=28, Height=28, Margin=new Thickness(0,0,10,0), VerticalAlignment=VerticalAlignment.Center };
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MinWidth=45 };
            var titleText = new TextBlock { Text = "FPS", Foreground = _greenBrush, FontSize = 12, FontWeight = FontWeights.ExtraBold };
            
            var subStack = new StackPanel { Orientation = Orientation.Horizontal };
            var valText = new TextBlock { Text = "--", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Black };
            var unitText = new TextBlock { Text = "FPS", Foreground = Brushes.Gray, FontSize = 10, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2,0,0,3), FontWeight = FontWeights.SemiBold };
            subStack.Children.Add(valText); subStack.Children.Add(unitText);

            textStack.Children.Add(titleText); textStack.Children.Add(subStack);
            row.Children.Add(iconBox); row.Children.Add(textStack);
            
            baseGrid.Children.Add(row);
            baseGrid.Children.Add(closeBtn);
            
            AttachCapsuleInteractivity(outlineContainer, "FPS", closeBtn);

            outlineContainer.Child = baseGrid;
            return new DashboardUIElement { Container = outlineContainer, MainText = valText, AppIcon = iconBox };
        }

        private DashboardUIElement CreateFanCapsule(string key, string encodedName)
        {
            var outlineContainer = new Border { Background=_darkBrush, CornerRadius=new CornerRadius(28), Margin=new Thickness(0,0,8,0), BorderBrush=_borderBrush, BorderThickness=new Thickness(1), ClipToBounds = true };
            var baseGrid = new Grid();
            
            var watermark = new PackIconMaterial { Kind = PackIconMaterialKind.Fan, Width=48, Height=48, Foreground = _redBrush, Opacity = 0.12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,-8,0) };
            baseGrid.Children.Add(watermark);

            var closeBtn = new PackIconMaterial { Kind = PackIconMaterialKind.CloseCircleOutline, Width=14, Height=14, Foreground = Brushes.White, HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Top, Margin=new Thickness(0,6,6,0), Cursor=System.Windows.Input.Cursors.Hand, Opacity=0 };
            closeBtn.MouseLeftButtonDown += (s,e) => { e.Handled = true; _config.OrderedElements.Remove(key); RebuildUIAndApplyConfig(); };

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin=new Thickness(12,6,18,6) };
            
            var grid = new Grid { Width = 36, Height = 36, SnapsToDevicePixels = true };
            var ellipse = new Ellipse { Stroke = _dimBrush, StrokeThickness = 1.5 };
            var iconText = new PackIconMaterial { Kind = PackIconMaterialKind.Fan, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Width = 18, Height = 18 };
            
            iconText.RenderTransformOrigin = new Point(0.5, 0.5);
            var rot = new RotateTransform(0);
            iconText.RenderTransform = rot;
            
            grid.Children.Add(ellipse); grid.Children.Add(iconText);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) };
            
            string shortName = "FAN";
            if(encodedName.Contains("CPU")) shortName = "CPU Fan";
            else if(encodedName.Contains("GPU")) {
                shortName = "GPU Fan";
                var parts = encodedName.Split(new string[] { "Fan" }, StringSplitOptions.None);
                if(parts.Length > 1) shortName = "GPU F" + parts[1].Trim();
            }

            var titleText = new TextBlock { Text = shortName, Foreground = _redBrush, FontSize = 12, FontWeight = FontWeights.ExtraBold };
            var subTitleText = new TextBlock { Text = "0 RPM", Width=65, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold };
            textStack.Children.Add(titleText); textStack.Children.Add(subTitleText);

            row.Children.Add(grid); row.Children.Add(textStack);
            baseGrid.Children.Add(row);
            baseGrid.Children.Add(closeBtn);
            
            AttachCapsuleInteractivity(outlineContainer, key, closeBtn);

            outlineContainer.Child = baseGrid;
            return new DashboardUIElement { Container = outlineContainer, IconText = iconText, FanRotation = rot, MainText = subTitleText };
        }

        private DashboardUIElement CreateGenericCapsule(string key, string title, string unit, string defaultVal, Brush accent, double valWidth, PackIconMaterialKind iconKind)
        {
            var outlineContainer = new Border { Background=_darkBrush, CornerRadius=new CornerRadius(28), Margin=new Thickness(0,0,8,0), BorderBrush=_borderBrush, BorderThickness=new Thickness(1), ClipToBounds = true };
            var baseGrid = new Grid();
            
            var watermark = new PackIconMaterial { Kind = iconKind, Width=48, Height=48, Foreground = accent, Opacity = 0.12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,-8,0) };
            baseGrid.Children.Add(watermark);

            var closeBtn = new PackIconMaterial { Kind = PackIconMaterialKind.CloseCircleOutline, Width=14, Height=14, Foreground = Brushes.White, HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Top, Margin=new Thickness(0,6,6,0), Cursor=System.Windows.Input.Cursors.Hand, Opacity=0 };
            closeBtn.MouseLeftButtonDown += (s,e) => { e.Handled = true; _config.OrderedElements.Remove(key); RebuildUIAndApplyConfig(); };

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin=new Thickness(18,6,22,6) };
            
            var titleText = new TextBlock { Text = title, Foreground = accent, FontSize = 12, FontWeight = FontWeights.ExtraBold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            
            var subStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var valText = new TextBlock { Text = defaultVal, Width=valWidth, TextAlignment=TextAlignment.Center, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold };
            var unitText = new TextBlock { Text = unit, Foreground = Brushes.Gray, FontSize = 10, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0,0,0,2), FontWeight = FontWeights.SemiBold };
            subStack.Children.Add(valText); subStack.Children.Add(unitText);

            textStack.Children.Add(titleText); textStack.Children.Add(subStack);
            baseGrid.Children.Add(textStack);
            baseGrid.Children.Add(closeBtn);
            
            AttachCapsuleInteractivity(outlineContainer, key, closeBtn);

            outlineContainer.Child = baseGrid;
            return new DashboardUIElement { Container = outlineContainer, MainText = valText };
        }

        private void InitHardwareMonitor()
        {
            try { _computer.Open(); } catch { }
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var kvp in _uiRefs)
            {
                if (kvp.Key.StartsWith("FAN:") && kvp.Value.FanRotation != null && kvp.Value.MainText != null)
                {
                    string rpmStr = kvp.Value.MainText.Text.Replace(" RPM", "");
                    if(float.TryParse(rpmStr, out float rpm))
                    {
                        if (rpm > 0)
                        {
                            double increment = rpm * 0.005;
                            kvp.Value.CurrentAngle = (kvp.Value.CurrentAngle + increment) % 360;
                            kvp.Value.FanRotation.Angle = kvp.Value.CurrentAngle;

                            PackIconMaterial icon = (PackIconMaterial)kvp.Value.IconText;
                            if (rpm < 1000) icon.Foreground = _greenBrush;
                            else if (rpm < 2000) icon.Foreground = _yellowBrush;
                            else icon.Foreground = _redBrush;
                        }
                        else
                        {
                            ((PackIconMaterial)kvp.Value.IconText).Foreground = Brushes.Gray;
                        }
                    }
                }
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            float cpuUsage = 0, cpuTemp = 0;
            float gpuUsage = 0, gpuTemp = 0, gpuWatts = 0;
            float ramPercent = 0, ramUsedGb = 0;
            var fansDetected = new Dictionary<string, float>();

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                if (hw.HardwareType == HardwareType.Cpu)
                {
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == SensorType.Load && s.Name.Contains("Total")) cpuUsage = s.Value ?? 0;
                        if (s.SensorType == SensorType.Temperature && s.Value.HasValue && s.Value.Value > cpuTemp) cpuTemp = s.Value.Value;
                    }
                }
                else if (hw.HardwareType == HardwareType.Motherboard || hw.HardwareType == HardwareType.SuperIO)
                {
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == SensorType.Temperature)
                        {
                            string n = s.Name.ToLower();
                            if ((n.Contains("cpu") || n.Contains("core") || n.Contains("package")) && s.Value.HasValue && s.Value.Value > cpuTemp) 
                                cpuTemp = s.Value.Value;
                        }
                        if (s.SensorType == SensorType.Fan) {
                            string key = $"{hw.HardwareType}_{s.Name}";
                            fansDetected[key] = s.Value ?? 0;
                        }
                    }
                }
                else if (hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel)
                {
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == SensorType.Load && s.Name == "GPU Core") gpuUsage = s.Value ?? 0;
                        if (s.SensorType == SensorType.Temperature && s.Name == "GPU Core") gpuTemp = s.Value ?? 0;
                        // Better detection for GPU Power (Wattage)
                        if (s.SensorType == SensorType.Power && (s.Name.Contains("GPU") || s.Name.Contains("Package"))) 
                        {
                            if (s.Value.HasValue && s.Value.Value > gpuWatts) gpuWatts = s.Value.Value;
                        }
                        if (s.SensorType == SensorType.Fan) {
                            string key = $"{hw.HardwareType}_{s.Name}";
                            fansDetected[key] = s.Value ?? 0;
                        }
                    }
                }
                else if (hw.HardwareType == HardwareType.Memory)
                {
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == SensorType.Load && s.Name == "Memory") ramPercent = s.Value ?? 0;
                        if (s.SensorType == SensorType.Data && s.Name == "Memory Used") ramUsedGb = s.Value ?? 0;
                    }
                }
            }

            if (_uiRefs.TryGetValue("CPU", out var cpuUI))
            {
                AnimateArc(cpuUI.PrimaryArc, cpuUsage / 100 * 360, cpuUsage);
                cpuUI.MainText.Text = $"{(int)cpuUsage}%";
                if(cpuUI.SubText != null) cpuUI.SubText.Text = cpuTemp > 0 ? $"{(int)cpuTemp}°C" : "--°C";
            }
            if (_uiRefs.TryGetValue("GPU", out var gpuUI))
            {
                AnimateArc(gpuUI.PrimaryArc, gpuUsage / 100 * 360, gpuUsage);
                gpuUI.MainText.Text = $"{(int)gpuUsage}%";
                if (gpuUI.SubText != null) gpuUI.SubText.Text = gpuTemp > 0 ? $"{(int)gpuTemp}°C" : "--°C";
            }
            if (_uiRefs.TryGetValue("RAM", out var ramUI))
            {
                AnimateArc(ramUI.PrimaryArc, ramPercent / 100 * 360, ramPercent);
                ramUI.MainText.Text = $"{(int)ramPercent}%";
                if (ramUI.SubText != null) ramUI.SubText.Text = $"{ramUsedGb:F1} GB";
            }
            if (_uiRefs.TryGetValue("GPU Watts", out var pwrUI))
            {
                pwrUI.MainText.Text = gpuWatts > 0 ? $"{(int)gpuWatts}" : "0";
            }
            
            // Poll Application Icon and true FPS
            Random rand = new Random();
            if (_uiRefs.TryGetValue("FPS", out var fpsUI)) 
            {
                int realFps = _fpsTracker.CurrentFps;
                fpsUI.MainText.Text = realFps > 0 ? $"{realFps}" : "0";
                UpdateActiveGameIcon(fpsUI);
            }
            if (_uiRefs.TryGetValue("Latency", out var latUI)) latUI.MainText.Text = $"{rand.Next(12, 17)}";
            if (_uiRefs.TryGetValue("1% Low", out var lowUI)) lowUI.MainText.Text = $"{rand.Next(70, 90)}";
            if (_uiRefs.TryGetValue("Avg FPS", out var avgUI)) avgUI.MainText.Text = "120";

            foreach(var kvp in _uiRefs)
            {
                if (kvp.Key.StartsWith("FAN:"))
                {
                    string fName = kvp.Key.Substring(4);
                    float rpm = fansDetected.ContainsKey(fName) ? fansDetected[fName] : 0;
                    kvp.Value.MainText.Text = $"{(int)rpm} RPM";
                }
            }
            
            if (_config.IsProcessMonitorActive)
            {
                UpdateProcessUI();
            }
            
            EnforceTopmost();
        }

        private void UpdateProcessUI()
        {
            try
            {
                var topApps = _processManager.TopProcesses;

                if (topApps == null || topApps.Count == 0)
                {
                    ProcessElementsContainer.Children.Clear();
                    var emptyTxt = new TextBlock { Text = "No active apps.", Foreground = Brushes.Gray, FontStyle = FontStyles.Italic, Margin = new Thickness(10,5,10,5) };
                    ProcessElementsContainer.Children.Add(emptyTxt);
                    return;
                }

                // Ensure container only has Grids (clear if it has the "No apps" textblock)
                if (ProcessElementsContainer.Children.Count > 0 && !(ProcessElementsContainer.Children[0] is Grid))
                {
                    ProcessElementsContainer.Children.Clear();
                }

                // Sync child count
                while (ProcessElementsContainer.Children.Count > topApps.Count) {
                    ProcessElementsContainer.Children.RemoveAt(ProcessElementsContainer.Children.Count - 1);
                }
                while (ProcessElementsContainer.Children.Count < topApps.Count) {
                    var grid = new Grid { Margin = new Thickness(0,0,0,8) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });

                    // Column 0 - Name + Icon
                    var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    var img = new Image { Width = 18, Height = 18, Margin = new Thickness(0,0,8,0), Visibility = Visibility.Collapsed };
                    var fallbackIcon = new PackIconMaterial { Kind = PackIconMaterialKind.Application, Width = 16, Height = 16, Foreground = Brushes.Gray, Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center };
                    
                    var nameTxt = new TextBlock { Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
                    stack.Children.Add(img);
                    stack.Children.Add(fallbackIcon);
                    stack.Children.Add(nameTxt);
                    Grid.SetColumn(stack, 0);

                    // Column 1 to 5
                    var cpuTxt = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(cpuTxt, 1);
                    var ramTxt = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(ramTxt, 2);
                    var gpuTxt = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(gpuTxt, 3);
                    var diskTxt = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(diskTxt, 4);
                    var netTxt = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(netTxt, 5);

                    grid.Children.Add(stack);
                    grid.Children.Add(cpuTxt);
                    grid.Children.Add(ramTxt);
                    grid.Children.Add(gpuTxt);
                    grid.Children.Add(diskTxt);
                    grid.Children.Add(netTxt);
                    ProcessElementsContainer.Children.Add(grid);
                }

                Brush dimNorm = new SolidColorBrush(Color.FromArgb(0xFF, 0xAA, 0xAA, 0xAA));
                bool isCpu = _config.ProcessSortMetric == "CPU";
                bool isRam = _config.ProcessSortMetric == "RAM";
                bool isGpu = _config.ProcessSortMetric == "GPU";
                bool isDisk = _config.ProcessSortMetric == "DISK";
                bool isNet = _config.ProcessSortMetric == "NETWORK";

                for (int i = 0; i < topApps.Count; i++)
                {
                    var app = topApps[i];
                    var grid = (Grid)ProcessElementsContainer.Children[i];
                    
                    // Hacky indexing to retrieve elements from our standardized structure
                    var stack = (StackPanel)grid.Children[0];
                    var img = (Image)stack.Children[0];
                    var fallback = (PackIconMaterial)stack.Children[1];
                    var nameTxt = (TextBlock)stack.Children[2];
                    var cpuTxt = (TextBlock)grid.Children[1];
                    var ramTxt = (TextBlock)grid.Children[2];
                    var gpuTxt = (TextBlock)grid.Children[3];
                    var diskTxt = (TextBlock)grid.Children[4];
                    var netTxt = (TextBlock)grid.Children[5];

                    if (app.IconSource != null) {
                        img.Source = app.IconSource;
                        img.Visibility = Visibility.Visible;
                        fallback.Visibility = Visibility.Collapsed;
                    } else {
                        img.Visibility = Visibility.Collapsed;
                        fallback.Visibility = Visibility.Visible;
                    }

                    string shortName = app.Name.Length > 16 ? app.Name.Substring(0, 16) + ".." : app.Name;
                    nameTxt.Text = shortName;

                    cpuTxt.Text = $"{app.CpuUsage:F1}%";
                    cpuTxt.Foreground = isCpu ? _cyanBrush : dimNorm;
                    cpuTxt.FontWeight = isCpu ? FontWeights.Bold : FontWeights.Normal;

                    ramTxt.Text = $"{(app.RamUsageBytes / (1024*1024)):F0} MB";
                    ramTxt.Foreground = isRam ? _yellowBrush : dimNorm;
                    ramTxt.FontWeight = isRam ? FontWeights.Bold : FontWeights.Normal;

                    gpuTxt.Text = $"{app.GpuUsage:F1}%";
                    gpuTxt.Foreground = isGpu ? _redBrush : dimNorm;
                    gpuTxt.FontWeight = isGpu ? FontWeights.Bold : FontWeights.Normal;

                    diskTxt.Text = $"{app.DiskUsage:F1} MB/s";
                    diskTxt.Foreground = isDisk ? _greenBrush : dimNorm;
                    diskTxt.FontWeight = isDisk ? FontWeights.Bold : FontWeights.Normal;

                    netTxt.Text = $"{app.NetworkUsage:F1} MB/s";
                    netTxt.Foreground = isNet ? Brushes.White : dimNorm;
                    netTxt.FontWeight = isNet ? FontWeights.Bold : FontWeights.Normal;
                }
            }
            catch (Exception ex)
            {
                // Safely log or ignore rendering exceptions to prevent complete app crash
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        private void UpdateActiveGameIcon(DashboardUIElement fpsUI)
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero && hwnd != new WindowInteropHelper(this).Handle)
                {
                    uint pid;
                    GetWindowThreadProcessId(hwnd, out pid);
                    if (pid > 0)
                    {
                        Process proc = Process.GetProcessById((int)pid);
                        // Filter out basic windows apps, explorer, etc.
                        string pName = proc.ProcessName.ToLower();
                        if (pName != "explorer" && pName != "idle" && !pName.Contains("windowapp"))
                        {
                            try {
                                if (proc.MainModule != null) {
                                    System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(proc.MainModule.FileName);
                                    if(icon != null) {
                                        ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                                            icon.Handle,
                                            Int32Rect.Empty,
                                            BitmapSizeOptions.FromEmptyOptions());
                                        fpsUI.AppIcon.Source = imageSource;
                                    }
                                }
                            } catch { } // Access denied or unavailable icon
                        }
                    }
                }
            } catch { }
        }

        private void AnimateArc(Arc arc, double toAngle, float usage)
        {
            if (arc == null) return;
            if (double.IsNaN(toAngle)) toAngle = 0;
            DoubleAnimation anim = new DoubleAnimation { To = toAngle, Duration = TimeSpan.FromMilliseconds(500), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } };
            arc.BeginAnimation(Arc.EndAngleProperty, anim);

            if (usage < 45) arc.Stroke = _cyanBrush;
            else if (usage < 70) arc.Stroke = _yellowBrush;
            else arc.Stroke = _redBrush;
        }

        private void EnforceTopmost()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width > 0 && e.NewSize.Width > 0)
            {
                double widthDiff = e.NewSize.Width - e.PreviousSize.Width;
                this.Left -= (widthDiff / 2);
                if (!_config.IsLocked) _config.PositionX = this.Left;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_config.IsLocked && e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (!_config.IsLocked) { _config.PositionX = this.Left; _config.PositionY = this.Top; }
        }

        private void Hud_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };
            QuickAccessTray.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(300)));
            TrayScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(400)) { EasingFunction = ease });

            if (_config.IsProcessMonitorActive && !_config.IsProcessMonitorPinned)
            {
                ProcessContainer.Visibility = Visibility.Visible;
                ProcessContainer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(300)));
            }
        }

        private void Hud_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {


            var ease = new QuinticEase { EasingMode = EasingMode.EaseIn };
            QuickAccessTray.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
            TrayScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)) { EasingFunction = ease });
            
            if (!_config.IsProcessMonitorPinned)
            {
                ProcessContainer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)) { EasingFunction = ease });
                System.Threading.Tasks.Task.Delay(250).ContinueWith(_ => Dispatcher.Invoke(() => {
                    if (!MasterStack.IsMouseOver && !_config.IsProcessMonitorPinned)
                        ProcessContainer.Visibility = Visibility.Collapsed;
                }));
            }
        }

        private void PinButton_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _config.IsProcessMonitorPinned = !_config.IsProcessMonitorPinned;
            _config.Save();
            ApplyPinState();
            
            if (!_config.IsProcessMonitorPinned && !MasterStack.IsMouseOver)
            {
                var ease = new QuinticEase { EasingMode = EasingMode.EaseIn };
                ProcessContainer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)) { EasingFunction = ease });
                System.Threading.Tasks.Task.Delay(250).ContinueWith(_ => Dispatcher.Invoke(() => {
                    if (!MasterStack.IsMouseOver && !_config.IsProcessMonitorPinned)
                        ProcessContainer.Visibility = Visibility.Collapsed;
                }));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _notifyIcon?.Dispose();
            _computer?.Close();
            _fpsTracker?.Dispose();
            _config?.Save();
            base.OnClosed(e);
        }
    }
}