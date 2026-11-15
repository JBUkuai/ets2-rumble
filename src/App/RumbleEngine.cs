using System.Diagnostics;
using Ets2Rumble.Config;
using Ets2Rumble.Events;
using Ets2Rumble.Logging;
using Ets2Rumble.Output;
using Ets2Rumble.Telemetry;

namespace Ets2Rumble.App;

/// <summary>给界面轮询的轻量快照，避免每帧跨线程回调。</summary>
public readonly record struct EngineSnapshot(
    bool SourceReady,
    bool Stale,
    bool Paused,
    float SpeedKph,
    int Gear,
    float Brake,
    double OutputLevel,
    long Frames,
    long Events);

/// <summary>
/// 主循环：单线程、定频采样 → 事件检测 → 包络混合 → 输出。
/// 不做任何磁盘 IO（日志走后台线程），不阻塞游戏。
/// </summary>
public sealed class RumbleEngine
{
    private readonly ConfigStore _configs;
    private readonly ITelemetrySource _source;
    private readonly IVibrationOutput _output;
    private readonly Logger _log;

    private readonly EventDetector _detector = new();
    private readonly PulseMixer _mixer = new();
    private readonly List<RumbleEvent> _events = new(4);

    private long _configVersion = long.MinValue;
    private AppConfig _cfg = new();

    private bool _loggedConnection;
    private bool _loggedStale;
    private ulong _lastTimestampUs;

    private long _frames;
    private long _eventsCount;
    private bool _snapReady;
    private bool _snapStale;
    private bool _snapPaused;
    private float _snapSpeed;
    private int _snapGear;
    private float _snapBrake;
    private double _snapLevel;

    public EngineSnapshot Snapshot => new(
        _snapReady, _snapStale, _snapPaused, _snapSpeed, _snapGear, _snapBrake,
        _snapLevel, Interlocked.Read(ref _frames), Interlocked.Read(ref _eventsCount));

    public RumbleEngine(ConfigStore configs, ITelemetrySource source, IVibrationOutput output, Logger log)
    {
        _configs = configs;
        _source = source;
        _output = output;
        _log = log;
    }

    public void Run(CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        double nextTickMs = 0;
        double nextSampleMs = 0;
        double nextStatsMs = 0;
        double lastTimestampChangeMs = 0;
        long tornFrames = 0;

        _cfg = _configs.Current;
        _configVersion = _configs.Version;
        _detector.Configure(_cfg);

        _log.Info($"开始运行：数据源={_source.Name}，输出={_output.Describe()}，轮询={_cfg.Loop.PollHz}Hz");

        while (!token.IsCancellationRequested)
        {
            double now = clock.Elapsed.TotalMilliseconds;

            if (_configs.Version != _configVersion)
            {
                _configVersion = _configs.Version;
                _cfg = _configs.Current;
                _detector.Configure(_cfg);
                nextTickMs = now;
            }

            if (_source.TryRead(out var frame))
            {
                _frames++;

                if (frame.Torn)
                {
                    tornFrames++;
                }

                if (!_loggedConnection)
                {
                    _loggedConnection = true;
                    _log.Info($"遥测已连通：游戏版本 {frame.GameVersionMajor}.{frame.GameVersionMinor}，插件修订 {frame.PluginRevision}，sdkActive={frame.SdkActive}");
                }

                if (frame.TimestampUs != _lastTimestampUs)
                {
                    _lastTimestampUs = frame.TimestampUs;
                    lastTimestampChangeMs = now;
                    if (_loggedStale)
                    {
                        _loggedStale = false;
                        _log.Info("遥测数据恢复更新");
                    }
                }

                bool stale = now - lastTimestampChangeMs > _cfg.Loop.StaleTimeoutMs;

                _snapReady = true;
                _snapStale = stale;
                _snapPaused = frame.Paused || !frame.SdkActive;
                _snapSpeed = (float)frame.SpeedKph;
                _snapGear = frame.Gear;
                _snapBrake = frame.UserBrake;

                if (stale || frame.Paused || !frame.SdkActive)
                {
                    if (!_loggedStale && stale)
                    {
                        _loggedStale = true;
                        _log.Warn($"遥测时间戳 {_cfg.Loop.StaleTimeoutMs}ms 未更新（游戏暂停、读档或菜单），已清空震动");
                    }

                    _mixer.Clear();
                }
                else
                {
                    _events.Clear();
                    _detector.Detect(frame, now, _events);

                    foreach (var evt in _events)
                    {
                        Interlocked.Increment(ref _eventsCount);
                        _mixer.Add(evt.Strength, evt.DurationMs, now);
                        _log.Info($"[事件] {evt.Kind} 强度 {evt.Strength:F2} 时长 {evt.DurationMs}ms | {evt.Detail}");
                    }
                }

                if (_cfg.Log.TelemetrySampleMs > 0 && now >= nextSampleMs)
                {
                    nextSampleMs = now + _cfg.Log.TelemetrySampleMs;
                    _log.Info($"[采样] 挡位 {frame.Gear} 车速 {frame.SpeedKph:F1}km/h 转速 {frame.EngineRpm:F0} " +
                              $"踏板 {frame.UserBrake:F2}/{frame.GameBrake:F2} " +
                              $"加速度 {frame.AccelX:F2}/{frame.AccelY:F2}/{frame.AccelZ:F2} " +
                              $"角加速度 {frame.AngularAccelX:F3}/{frame.AngularAccelY:F3}/{frame.AngularAccelZ:F3} " +
                              $"货损 {frame.CargoDamage:F4}");
                }
            }
            else
            {
                _snapReady = false;

                if (!_loggedStale)
                {
                    _loggedStale = true;
                    _log.Warn($"{_source.Name} 暂不可用，每 1 秒自动重试（游戏启动/加载存档后会自动连上）");
                }

                _mixer.Clear();
                _output.SetMotors(0, 0);
            }

            double level = _cfg.Output.Enabled && !_cfg.Output.DryRun ? _mixer.Evaluate(now) : 0;
            _snapLevel = level;
            _output.SetMotors(level * _cfg.Output.LeftGain, level * _cfg.Output.RightGain);

            if (now >= nextStatsMs)
            {
                nextStatsMs = now + 10_000;
                _log.Debug($"统计：帧 {_frames}，撕裂帧 {tornFrames}，事件 {Interlocked.Read(ref _eventsCount)}，" +
                           $"活动脉冲 {_mixer.ActiveCount}，输出 {(int)(level * 100)}%");
            }

            nextTickMs += 1000.0 / Math.Max(1, _cfg.Loop.PollHz);
            double sleepMs = nextTickMs - clock.Elapsed.TotalMilliseconds;
            if (sleepMs > 1)
            {
                Thread.Sleep((int)sleepMs);
            }
            else if (sleepMs < -200)
            {
                // 落后太多（系统卡顿/断点调试），重置节拍而不是疯狂补帧。
                nextTickMs = clock.Elapsed.TotalMilliseconds;
            }
        }

        _output.SetMotors(0, 0);
        _log.Info("主循环已停止");
    }
}
