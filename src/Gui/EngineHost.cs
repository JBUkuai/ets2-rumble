using Ets2Rumble.App;
using Ets2Rumble.Config;
using Ets2Rumble.Logging;
using Ets2Rumble.Output;
using Ets2Rumble.Telemetry;

namespace Ets2Rumble.Gui;

/// <summary>
/// 在后台线程承载引擎。界面开关功能只改配置，不重启进程；
/// 只有关窗或切换数据源时才真正停止线程。
/// </summary>
public sealed class EngineHost : IDisposable
{
    private readonly ConfigStore _configs;
    private readonly Logger _log;
    private readonly bool _sim;
    private readonly bool _noOutput;

    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private IVibrationOutput? _output;

    public EngineHost(ConfigStore configs, Logger log, bool sim, bool noOutput)
    {
        _configs = configs;
        _log = log;
        _sim = sim;
        _noOutput = noOutput;
    }

    public RumbleEngine? Engine { get; private set; }

    public string SourceName { get; private set; } = "—";

    public string OutputName { get; private set; } = "—";

    public bool IsSimulated => _sim;

    /// <summary>当前手柄索引，-1 表示未连接。</summary>
    public int ControllerIndex => (_output as XInputOutput)?.Index ?? -1;

    public bool IsRunning => _thread is { IsAlive: true };

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _thread = new Thread(() => RunLoop(token))
        {
            IsBackground = true,
            Name = "rumble-engine",
        };
        _thread.Start();
    }

    private void RunLoop(CancellationToken token)
    {
        try
        {
            var cfg = _configs.Current;

            using ITelemetrySource source = _sim
                ? new SimulatedSource()
                : new SharedMemorySource(_log);

            using IVibrationOutput output = _noOutput
                ? new NullOutput(_log)
                : new XInputOutput(_log, cfg.Output.ControllerIndex);

            SourceName = source.Name;
            OutputName = output.Describe();
            _output = output;

            var engine = new RumbleEngine(_configs, source, output, _log);
            Engine = engine;
            engine.Run(token);
        }
        catch (Exception ex)
        {
            _log.Error("引擎异常退出：" + ex.Message);
        }
        finally
        {
            _log.Info("引擎线程已结束");
        }
    }

    public void Stop()
    {
        var cts = _cts;
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(3));
        cts.Dispose();
        _cts = null;
        _thread = null;
        Engine = null;
        _output = null;
    }

    public void Dispose() => Stop();
}
