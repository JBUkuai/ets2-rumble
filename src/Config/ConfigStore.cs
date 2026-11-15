using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Ets2Rumble.Logging;

namespace Ets2Rumble.Config;

/// <summary>
/// 配置加载 + 热重载。文件保存后 300ms 防抖重载；解析失败保留上一份有效配置，
/// 绝不让一个写错的逗号把程序打挂。
/// </summary>
public sealed class ConfigStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _path;
    private readonly ILog _log;
    private readonly object _gate = new();

    private AppConfig _current = new();
    private long _version;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private bool _disposed;
    private DateTime _selfSaveUtc = DateTime.MinValue;

    /// <summary>外部（手工编辑 JSON）导致重新加载时触发；界面用它同步控件。</summary>
    public event Action<AppConfig>? Reloaded;

    private ConfigStore(string path, ILog log)
    {
        _path = Path.GetFullPath(path);
        _log = log;
    }

    public AppConfig Current => Volatile.Read(ref _current);

    /// <summary>每次成功重载 +1，主循环用它判断要不要刷新配置副本。</summary>
    public long Version => Interlocked.Read(ref _version);

    public static ConfigStore Load(string path, ILog log)
    {
        var store = new ConfigStore(path, log);
        store.EnsureFileExists();
        store.Reload(isStartup: true);
        store.StartWatcher();
        return store;
    }

    private void EnsureFileExists()
    {
        try
        {
            if (File.Exists(_path))
            {
                return;
            }

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(new AppConfig(), JsonOptions);
            File.WriteAllText(_path, json);
            _log.Info($"未找到配置文件，已生成默认配置：{_path}");
        }
        catch (Exception ex)
        {
            _log.Warn($"生成默认配置文件失败：{ex.Message}");
        }
    }

    private void StartWatcher()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            var file = Path.GetFileName(_path);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file))
            {
                return;
            }

            _watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };

            _debounce = new Timer(_ => Reload(isStartup: false), null, Timeout.Infinite, Timeout.Infinite);

            FileSystemEventHandler handler = (_, _) => ScheduleReload();
            _watcher.Changed += handler;
            _watcher.Created += handler;
            _watcher.Renamed += (_, _) => ScheduleReload();
        }
        catch (Exception ex)
        {
            _log.Warn($"配置热重载不可用（不影响运行）：{ex.Message}");
        }
    }

    private void ScheduleReload()
    {
        try
        {
            _debounce?.Change(300, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void Reload(bool isStartup)
    {
        AppConfig? notify = null;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            // 自己刚保存过，忽略自己写入引起的重载，避免界面被回灌。
            if (!isStartup && DateTime.UtcNow - _selfSaveUtc < TimeSpan.FromMilliseconds(800))
            {
                return;
            }

            try
            {
                if (!File.Exists(_path))
                {
                    return;
                }

                // 编辑器保存时可能短暂占用文件，重试几次。
                string text = string.Empty;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(fs);
                        text = reader.ReadToEnd();
                        break;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        Thread.Sleep(80);
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                var parsed = JsonSerializer.Deserialize<AppConfig>(text, JsonOptions);
                if (parsed is null)
                {
                    _log.Warn("配置内容为空，保留上一份有效配置");
                    return;
                }

                Normalize(parsed);
                Volatile.Write(ref _current, parsed);
                Interlocked.Increment(ref _version);
                notify = parsed;
                _log.Info(isStartup ? "配置加载成功" :
                    $"配置已热重载：轮询 {parsed.Loop.PollHz}Hz，换挡 {parsed.Shift.Strength:F2}/{parsed.Shift.DurationMs}ms，" +
                    $"急刹 {parsed.Brake.Strength:F2}/{parsed.Brake.DurationMs}ms，颠簸 {parsed.Bump.Strength:F2}/{parsed.Bump.DurationMs}ms");
            }
            catch (JsonException ex)
            {
                _log.Warn($"配置文件语法错误，已保留上一份有效配置（第 {ex.LineNumber + 1} 行）：{ex.Message}");
            }
            catch (Exception ex)
            {
                _log.Warn($"读取配置失败，已保留上一份有效配置：{ex.GetType().Name} {ex.Message}");
            }
        }

        if (notify is not null)
        {
            Reloaded?.Invoke(notify);
        }
    }

    /// <summary>界面改参数后写回磁盘，并让主循环立刻用上新值（无需重启）。</summary>
    public void Save(AppConfig cfg)
    {
        lock (_gate)
        {
            Normalize(cfg);

            try
            {
                var json = JsonSerializer.Serialize(cfg, JsonOptions);
                File.WriteAllText(_path, json);
            }
            catch (Exception ex)
            {
                _log.Warn($"保存配置失败：{ex.Message}");
                return;
            }

            _selfSaveUtc = DateTime.UtcNow;
            Volatile.Write(ref _current, cfg);
            Interlocked.Increment(ref _version);
        }
    }

    private static void Normalize(AppConfig cfg)
    {
        cfg.Loop.PollHz = Math.Clamp(cfg.Loop.PollHz, 5, 120);
        cfg.Loop.StaleTimeoutMs = Math.Clamp(cfg.Loop.StaleTimeoutMs, 200, 10_000);

        cfg.Shift.Strength = Math.Clamp(cfg.Shift.Strength, 0, 1);
        cfg.Shift.DurationMs = Math.Clamp(cfg.Shift.DurationMs, 20, 2000);
        cfg.Shift.CooldownMs = Math.Clamp(cfg.Shift.CooldownMs, 0, 5000);

        cfg.Brake.Strength = Math.Clamp(cfg.Brake.Strength, 0, 1);
        cfg.Brake.DurationMs = Math.Clamp(cfg.Brake.DurationMs, 20, 2000);
        cfg.Brake.CooldownMs = Math.Clamp(cfg.Brake.CooldownMs, 0, 5000);
        cfg.Brake.PedalThreshold = Math.Clamp(cfg.Brake.PedalThreshold, 0.05, 1.0);
        cfg.Brake.DecelThresholdMps2 = Math.Clamp(cfg.Brake.DecelThresholdMps2, 0.5, 30);
        cfg.Brake.HoldMs = Math.Clamp(cfg.Brake.HoldMs, 0, 1000);

        cfg.Bump.Strength = Math.Clamp(cfg.Bump.Strength, 0, 1);
        cfg.Bump.DurationMs = Math.Clamp(cfg.Bump.DurationMs, 20, 2000);
        cfg.Bump.CooldownMs = Math.Clamp(cfg.Bump.CooldownMs, 0, 5000);
        cfg.Bump.ThresholdMps2 = Math.Clamp(cfg.Bump.ThresholdMps2, 0.5, 50);
        cfg.Bump.CargoDamageDelta = Math.Clamp(cfg.Bump.CargoDamageDelta, 0, 1);

        cfg.Switches.Strength = Math.Clamp(cfg.Switches.Strength, 0, 1);
        cfg.Switches.DurationMs = Math.Clamp(cfg.Switches.DurationMs, 20, 1000);
        cfg.Switches.ParkingBrakeStrength = Math.Clamp(cfg.Switches.ParkingBrakeStrength, 0, 1);
        cfg.Switches.ParkingBrakeDurationMs = Math.Clamp(cfg.Switches.ParkingBrakeDurationMs, 20, 2000);

        cfg.Output.ControllerIndex = Math.Clamp(cfg.Output.ControllerIndex, -1, 3);
        cfg.Output.LeftGain = Math.Clamp(cfg.Output.LeftGain, 0, 1);
        cfg.Output.RightGain = Math.Clamp(cfg.Output.RightGain, 0, 1);

        cfg.Log.TelemetrySampleMs = cfg.Log.TelemetrySampleMs < 0 ? 0 : Math.Clamp(cfg.Log.TelemetrySampleMs, 0, 60_000);

        cfg.Ui.Language = string.Equals(cfg.Ui.Language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
    }

    public void Dispose()
    {
        _disposed = true;
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
