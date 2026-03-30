using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using Microsoft.Win32;
using Brushes = System.Windows.Media.Brushes;
using CheckBox = System.Windows.Controls.CheckBox;
using MahApps.Metro.IconPacks;

namespace ObsidianMonitor
{
    public partial class SettingsWindow : Window
    {
        private AppConfig _config;
        private MainWindow _main;
        private bool _isInitialized = false;

        public SettingsWindow(AppConfig config, MainWindow main)
        {
            InitializeComponent();
            _config = config;
            _main = main;
            LoadUI();
        }

        private void LoadUI()
        {
            // Core Elements
            AddFeatureToggle(CoreElementsPanel, "CPU", "CPU Core", PackIconMaterialKind.Cpu64Bit);
            AddFeatureToggle(CoreElementsPanel, "GPU", "Graphics", PackIconMaterialKind.ExpansionCardVariant);
            AddFeatureToggle(CoreElementsPanel, "RAM", "Memory", PackIconMaterialKind.Memory);

            // Game Elements
            AddFeatureToggle(GameElementsPanel, "FPS", "Current FPS", PackIconMaterialKind.Target);
            AddFeatureToggle(GameElementsPanel, "Latency", "Latency", PackIconMaterialKind.Speedometer);
            AddFeatureToggle(GameElementsPanel, "1% Low", "1% Lows", PackIconMaterialKind.ChartTimelineVariant);
            AddFeatureToggle(GameElementsPanel, "Avg FPS", "Avg FPS", PackIconMaterialKind.ChartLine);
            AddFeatureToggle(GameElementsPanel, "GPU Watts", "GPU Power (W)", PackIconMaterialKind.LightningBolt);

            // Dynamic Fans
            if (_main.AvailableFans != null && _main.AvailableFans.Count > 0)
            {
                foreach (var fanName in _main.AvailableFans)
                {
                    AddFeatureToggle(FansPanel, "FAN:" + fanName, fanName, PackIconMaterialKind.Fan);
                }
            }
            else
            {
                FansPanel.Children.Add(new TextBlock { Text = "No fans detected.", Foreground = Brushes.DimGray, Margin = new Thickness(0, 5, 0, 5) });
            }

            // Positions & Basics
            LockPositionCheck.IsChecked = _config.IsLocked;
            ShowTempCheck.IsChecked = _config.ShowTemperature;
            ScaleText.Text = $"{Math.Round(_config.SizeScale * 100)}%";
            CustomColorText.Text = _config.CustomColor;

            foreach (ComboBoxItem item in ThemeCombo.Items)
            {
                if (item.Content?.ToString() == _config.Theme)
                {
                    ThemeCombo.SelectedItem = item;
                    break;
                }
            }
            
            CustomColorPanel.Visibility = _config.Theme == "Custom" ? Visibility.Visible : Visibility.Collapsed;

            try
            {
                var psi = new ProcessStartInfo("schtasks", $"/query /tn \"ObsidianMonitorStartup\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit();
                AutoStartCheck.IsChecked = proc?.ExitCode == 0;
            }
            catch { }

            HotkeyTextBox.Text = _config.ToggleHotkeyText;

            _isInitialized = true;
        }

        private void AddFeatureToggle(WrapPanel panel, string key, string displayText, PackIconMaterialKind iconKind)
        {
            var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            stack.Children.Add(new PackIconMaterial { Kind = iconKind, Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) });
            stack.Children.Add(new TextBlock { Text = displayText, VerticalAlignment = VerticalAlignment.Center });

            var btn = new ToggleButton
            {
                Content = stack,
                Tag = key,
                IsChecked = _config.OrderedElements.Contains(key),
                Style = (Style)FindResource("CapsuleToggle")
            };
            btn.Checked += DynamicToggleChanged;
            btn.Unchecked += DynamicToggleChanged;
            panel.Children.Add(btn);
        }

        private void DynamicToggleChanged(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || sender is not ToggleButton btn || btn.Tag is not string key) return;

            if (btn.IsChecked == true)
            {
                if (!_config.OrderedElements.Contains(key)) {
                    _config.OrderedElements.Add(key);
                }
            }
            else
            {
                _config.OrderedElements.Remove(key);
            }

            _config.Save();
            _main.RebuildUIAndApplyConfig();
        }

        private void SettingChanged(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            _config.IsLocked = LockPositionCheck.IsChecked == true;
            _config.ShowTemperature = ShowTempCheck.IsChecked == true;
            
            if (ThemeCombo.SelectedItem is ComboBoxItem item)
            {
                _config.Theme = item.Content?.ToString() ?? "Black";
                if (CustomColorPanel != null)
                {
                    CustomColorPanel.Visibility = _config.Theme == "Custom" ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            
            if(CustomColorText != null) {
                _config.CustomColor = CustomColorText.Text;
            }
            
            _config.Save();
            _main.RebuildUIAndApplyConfig();
        }

        private void ScaleDown_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            if (_config.SizeScale > 0.5) _config.SizeScale -= 0.1;
            ScaleText.Text = $"{Math.Round(_config.SizeScale * 100)}%";
            _config.Save();
            _main.RebuildUIAndApplyConfig();
        }

        private void ScaleUp_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            if (_config.SizeScale < 2.0) _config.SizeScale += 0.1;
            ScaleText.Text = $"{Math.Round(_config.SizeScale * 100)}%";
            _config.Save();
            _main.RebuildUIAndApplyConfig();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Sleek Spring-In animation for settings pane
            MainContainer.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(300)));
            WindowTranslate.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(450)) { EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = 0.4, EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void AutoStartCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            string taskName = "ObsidianMonitorStartup";
            string path = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (AutoStartCheck.IsChecked == true)
                {
                    var psi = new ProcessStartInfo("schtasks", $"/create /tn \"{taskName}\" /tr \"\\\"{path}\\\"\" /sc onlogon /rl highest /f")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi)?.WaitForExit();
                }
                else
                {
                    var psi = new ProcessStartInfo("schtasks", $"/delete /tn \"{taskName}\" /f")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi)?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Could not configure AutoStart via Task Scheduler: " + ex.Message, "Notice");
            }
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingChanged(sender, e);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private bool _isRecordingHotkey = false;

        private void RecordHotkeyBtn_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingHotkey = true;
            HotkeyTextBox.Text = "Press keys...";
            RecordHotkeyBtn.Content = "Recording";
            this.PreviewKeyDown += SettingsWindow_PreviewKeyDown;
        }

        private void SettingsWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_isRecordingHotkey) return;
            
            e.Handled = true;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            
            // Ignore pure modifier presses until a real key is pressed
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin)
                return;
                
            this.PreviewKeyDown -= SettingsWindow_PreviewKeyDown;
            _isRecordingHotkey = false;
            RecordHotkeyBtn.Content = "Record";

            uint modifiers = 0;
            string keyText = "";
            var modKeys = Keyboard.Modifiers;
            
            if (modKeys.HasFlag(ModifierKeys.Alt)) { modifiers |= 0x0001; keyText += "Alt + "; }
            if (modKeys.HasFlag(ModifierKeys.Control)) { modifiers |= 0x0002; keyText += "Ctrl + "; }
            if (modKeys.HasFlag(ModifierKeys.Shift)) { modifiers |= 0x0004; keyText += "Shift + "; }
            
            keyText += key.ToString();
            
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

            _config.ToggleModifier = modifiers;
            _config.ToggleKey = vk;
            _config.ToggleHotkeyText = keyText;
            
            HotkeyTextBox.Text = keyText;
            _config.Save();
            
            _main.ReRegisterHotkey();
        }

        private void RestoreHotkeyBtn_Click(object sender, RoutedEventArgs e)
        {
            _config.ToggleModifier = 0x0001; // Alt
            _config.ToggleKey = 0x53; // S
            _config.ToggleHotkeyText = "Alt + S";
            HotkeyTextBox.Text = _config.ToggleHotkeyText;
            _config.Save();
            _main.ReRegisterHotkey();
        }
    }
}
