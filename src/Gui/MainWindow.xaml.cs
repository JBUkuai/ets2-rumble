using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ets2Rumble.Config;
using Ets2Rumble.Logging;

namespace Ets2Rumble.Gui;

public partial class MainWindow : Window
{
    private const int MaxLogLines = 100;

    private readonly ConfigStore _configs;
    private readonly Logger _log;
    private readonly EngineHost _host;
    private readonly string _configPath;

    private readonly ObservableCollection<LogRow> _rows = new();
    private readonly ConcurrentQueue<LogEntry> _pending = new();

    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _logTimer;
    private readonly DispatcherTimer _saveTimer;

    private AppConfig _cfg;
    private bool _suppress;
    private bool _started;

    public MainWindow(ConfigStore configs, Logger log, EngineHost host, string configPath)
    {
        InitializeComponent();

        _configs = configs;
        _log = log;
        _host = host;
        _configPath = configPath;
        _cfg = configs.Current;

        LogList.ItemsSource = _rows;

        _statusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _statusTimer.Tick += (_, _) => UpdateStatus();

        _logTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(120),
        };
        _logTimer.Tick += (_, _) => DrainLog();

        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _saveTimer.Tick += (_, _) => FlushSave();

        Loaded += OnLoaded;
        Closing += OnClosing;
        _configs.Reloaded += OnConfigReloaded;
    }

    private static string T(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;

        ConfigPathText.Text = _configPath;
        ApplyLanguage(_cfg.Ui.Language);
        ApplyToUi();

        _log.Emitted += OnLogEntry;
        _host.Start();

        _statusTimer.Start();
        _logTimer.Start();
        UpdateStatus();
    }

    // ---------- 日志 ----------

    private void OnLogEntry(LogEntry entry) => _pending.Enqueue(entry);

    private void DrainLog()
    {
        if (_pending.IsEmpty)
        {
            return;
        }

        bool atBottom = LogScroll.VerticalOffset >= LogScroll.ScrollableHeight - 28;
        int appended = 0;

        while (appended < 25 && _pending.TryDequeue(out var entry))
        {
            _rows.Add(new LogRow(entry));
            appended++;
        }

        while (_rows.Count > MaxLogLines)
        {
            _rows.RemoveAt(0);
        }

        LogEmpty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (atBottom)
        {
            LogScroll.ScrollToEnd();
        }
    }

    private void LogClear_Click(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        while (_pending.TryDequeue(out _))
        {
        }

        LogEmpty.Visibility = Visibility.Visible;
    }

    private sealed class LogRow
    {
        public LogRow(LogEntry entry)
        {
            Time = entry.Time.ToString("HH:mm:ss");
            Level = entry.Level switch
            {
                LogLevel.Debug => "DBG",
                LogLevel.Warn => "WRN",
                LogLevel.Error => "ERR",
                _ => "INF",
            };

            Message = entry.Message;
            Brush = (Brush)Application.Current.FindResource(entry.Level switch
            {
                LogLevel.Warn => "b.warn",
                LogLevel.Error => "b.err",
                LogLevel.Debug => "b.log.debug",
                _ => "b.log.info",
            });

            LevelBrush = (Brush)Application.Current.FindResource(entry.Level switch
            {
                LogLevel.Warn => "b.warn",
                LogLevel.Error => "b.err",
                _ => "b.muted",
            });
        }

        public string Time { get; }
        public string Level { get; }
        public string Message { get; }
        public Brush Brush { get; }
        public Brush LevelBrush { get; }
    }

    // ---------- 状态 ----------

    private void UpdateStatus()
    {
        var snap = _host.Engine?.Snapshot ?? default;

        string key;
        string dotKey;

        if (!_host.IsRunning)
        {
            key = "status.connecting";
            dotKey = "b.muted";
        }
        else if (!snap.SourceReady)
        {
            key = "status.waiting";
            dotKey = "b.warn";
        }
        else if (snap.Stale || snap.Paused)
        {
            key = "status.paused";
            dotKey = "b.muted";
        }
        else
        {
            key = "status.connected";
            dotKey = "b.ok";
        }

        StatusText.Text = T(key);
        StatusDot.Fill = (Brush)FindResource(dotKey);

        SrcText.Text = _host.IsSimulated ? T("source.sim") : T("source.shm");

        int index = _host.ControllerIndex;
        OutText.Text = index >= 0 ? $"XInput #{index}" : T("output.none");
        StatText.Text = $"{snap.SpeedKph:0} {T("unit.kph")}  ·  {T("unit.gear")} {snap.Gear}  ·  " +
                        $"{T("status.level")} {snap.OutputLevel * 100:0}%";

        double width = Math.Max(0, LevelBar.ActualWidth - 2);
        LevelFill.Width = width * Math.Clamp(snap.OutputLevel, 0, 1);
    }

    // ---------- 配置 <-> 控件 ----------

    private void ApplyToUi()
    {
        _suppress = true;

        ShiftEnabled.IsChecked = _cfg.Shift.Enabled;
        ShiftStrength.Value = _cfg.Shift.Strength;
        ShiftDuration.Value = _cfg.Shift.DurationMs;
        ShiftSpeed.Value = _cfg.Shift.MinSpeedKph;

        BrakeEnabled.IsChecked = _cfg.Brake.Enabled;
        BrakeStrength.Value = _cfg.Brake.Strength;
        BrakeDuration.Value = _cfg.Brake.DurationMs;
        BrakePedal.Value = _cfg.Brake.PedalThreshold;

        BumpEnabled.IsChecked = _cfg.Bump.Enabled;
        BumpStrength.Value = _cfg.Bump.Strength;
        BumpDuration.Value = _cfg.Bump.DurationMs;
        BumpThreshold.Value = _cfg.Bump.ThresholdMps2;

        SwEnabled.IsChecked = _cfg.Switches.Enabled;
        SwBlinkers.IsChecked = _cfg.Switches.Blinkers;
        SwWipers.IsChecked = _cfg.Switches.Wipers;
        SwHeadlights.IsChecked = _cfg.Switches.Headlights;
        SwHighBeam.IsChecked = _cfg.Switches.HighBeam;
        SwHazard.IsChecked = _cfg.Switches.HazardLights;
        SwStrength.Value = _cfg.Switches.Strength;
        SwDuration.Value = _cfg.Switches.DurationMs;

        HbEnabled.IsChecked = _cfg.Switches.ParkingBrake;
        HbStrength.Value = _cfg.Switches.ParkingBrakeStrength;
        HbDuration.Value = _cfg.Switches.ParkingBrakeDurationMs;

        MasterEnabled.IsChecked = _cfg.Output.Enabled;
        DryRunEnabled.IsChecked = _cfg.Output.DryRun;

        UpdateValueTexts();
        _suppress = false;
    }

    private void UpdateValueTexts()
    {
        ShiftStrengthText.Text = $"{ShiftStrength.Value * 100:0}%";
        ShiftDurationText.Text = $"{(int)Math.Round(ShiftDuration.Value)} {T("unit.ms")}";
        ShiftSpeedText.Text = $"{ShiftSpeed.Value:0} {T("unit.kph")}";

        BrakeStrengthText.Text = $"{BrakeStrength.Value * 100:0}%";
        BrakeDurationText.Text = $"{(int)Math.Round(BrakeDuration.Value)} {T("unit.ms")}";
        BrakePedalText.Text = $"{BrakePedal.Value * 100:0}%";

        BumpStrengthText.Text = $"{BumpStrength.Value * 100:0}%";
        BumpDurationText.Text = $"{(int)Math.Round(BumpDuration.Value)} {T("unit.ms")}";
        BumpThresholdText.Text = $"{BumpThreshold.Value:0.0} {T("unit.mps2")}";

        SwStrengthText.Text = $"{SwStrength.Value * 100:0}%";
        SwDurationText.Text = $"{(int)Math.Round(SwDuration.Value)} {T("unit.ms")}";
        HbStrengthText.Text = $"{HbStrength.Value * 100:0}%";
        HbDurationText.Text = $"{(int)Math.Round(HbDuration.Value)} {T("unit.ms")}";
    }

    private void SyncFromUi()
    {
        _cfg.Shift.Enabled = ShiftEnabled.IsChecked == true;
        _cfg.Shift.Strength = ShiftStrength.Value;
        _cfg.Shift.DurationMs = (int)Math.Round(ShiftDuration.Value);
        _cfg.Shift.MinSpeedKph = Math.Round(ShiftSpeed.Value, 1);

        _cfg.Brake.Enabled = BrakeEnabled.IsChecked == true;
        _cfg.Brake.Strength = BrakeStrength.Value;
        _cfg.Brake.DurationMs = (int)Math.Round(BrakeDuration.Value);
        _cfg.Brake.PedalThreshold = BrakePedal.Value;

        _cfg.Bump.Enabled = BumpEnabled.IsChecked == true;
        _cfg.Bump.Strength = BumpStrength.Value;
        _cfg.Bump.DurationMs = (int)Math.Round(BumpDuration.Value);
        _cfg.Bump.ThresholdMps2 = Math.Round(BumpThreshold.Value, 1);

        _cfg.Switches.Enabled = SwEnabled.IsChecked == true;
        _cfg.Switches.Blinkers = SwBlinkers.IsChecked == true;
        _cfg.Switches.Wipers = SwWipers.IsChecked == true;
        _cfg.Switches.Headlights = SwHeadlights.IsChecked == true;
        _cfg.Switches.HighBeam = SwHighBeam.IsChecked == true;
        _cfg.Switches.HazardLights = SwHazard.IsChecked == true;
        _cfg.Switches.Strength = SwStrength.Value;
        _cfg.Switches.DurationMs = (int)Math.Round(SwDuration.Value);

        _cfg.Switches.ParkingBrake = HbEnabled.IsChecked == true;
        _cfg.Switches.ParkingBrakeStrength = HbStrength.Value;
        _cfg.Switches.ParkingBrakeDurationMs = (int)Math.Round(HbDuration.Value);

        _cfg.Output.Enabled = MasterEnabled.IsChecked == true;
        _cfg.Output.DryRun = DryRunEnabled.IsChecked == true;
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress || !_started)
        {
            return;
        }

        SyncFromUi();
        UpdateValueTexts();
        ScheduleSave();
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress || !_started)
        {
            return;
        }

        SyncFromUi();
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        SaveStateText.Text = T("foot.saving");
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void FlushSave()
    {
        _saveTimer.Stop();
        _configs.Save(_cfg);
        SaveStateText.Text = T("foot.saved");
    }

    private void OnConfigReloaded(AppConfig cfg)
    {
        if (ReferenceEquals(cfg, _cfg))
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            _cfg = cfg;
            ApplyLanguage(_cfg.Ui.Language);
            ApplyToUi();
            UpdateStatus();
        }, DispatcherPriority.Background);
    }

    // ---------- 语言 ----------

    private void LangZh_Click(object sender, RoutedEventArgs e) => SetLanguage("zh");

    private void LangEn_Click(object sender, RoutedEventArgs e) => SetLanguage("en");

    private void SetLanguage(string code)
    {
        if (string.Equals(_cfg.Ui.Language, code, StringComparison.Ordinal))
        {
            return;
        }

        _cfg.Ui.Language = code;
        ApplyLanguage(code);
        ScheduleSave();
    }

    private void ApplyLanguage(string code)
    {
        var dict = Lang.Load(code);
        var merged = Application.Current.Resources.MergedDictionaries;

        for (int i = 0; i < merged.Count; i++)
        {
            var src = merged[i].Source?.OriginalString ?? string.Empty;
            if (src.Contains("Strings.", StringComparison.OrdinalIgnoreCase))
            {
                merged[i] = dict;
                break;
            }
        }

        var accent = (Brush)FindResource("b.accent");
        var dim = (Brush)FindResource("b.accent.dim");
        var muted = (Brush)FindResource("b.muted");

        LangZh.Background = code == "zh" ? dim : Brushes.Transparent;
        LangZh.Foreground = code == "zh" ? accent : muted;
        LangEn.Background = code == "en" ? dim : Brushes.Transparent;
        LangEn.Foreground = code == "en" ? accent : muted;

        UpdateValueTexts();
        UpdateStatus();
    }

    // ---------- 窗口 ----------

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            // Win11 圆角（失败无影响）
            var hwnd = new WindowInteropHelper(this).Handle;
            int preference = 2;
            _ = DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
        }
        catch
        {
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _statusTimer.Stop();
        _logTimer.Stop();
        _saveTimer.Stop();

        _log.Emitted -= OnLogEntry;
        _configs.Reloaded -= OnConfigReloaded;

        _host.Dispose();
        _configs.Save(_cfg);
    }
}
