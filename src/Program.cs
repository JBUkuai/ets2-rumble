using System.Diagnostics;
using System.IO;
using System.Text;
using Ets2Rumble.App;
using Ets2Rumble.Config;
using Ets2Rumble.Gui;
using Ets2Rumble.Logging;
using Ets2Rumble.Output;
using Ets2Rumble.Telemetry;

namespace Ets2Rumble;

internal static class Program
{
    internal const string LayoutSummary =
        "结构体偏移自检通过：sdkActive=0 paused=4 time=8 gear=504 speed=948 userBrake=964 " +
        "cargoDamage=1468 parkBrake=1566 wipers=1577 blinkerL/R=1578/1579 " +
        "lights 1583/1584/1588 accel=1892";

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine("启动失败：" + ex);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var options = CliOptions.Parse(args);
        if (options.Help)
        {
            PrintHelp();
            return 0;
        }

        string baseDir = AppContext.BaseDirectory;
        string configPath = options.ConfigPath is null
            ? Path.Combine(baseDir, "config.json")
            : Path.GetFullPath(options.ConfigPath);

        // 默认进图形界面；--console 与 --probe 保留原本的控制台行为。
        if (!options.ConsoleMode && !options.Probe)
        {
            HideConsoleWindow();
            return AppHost.Run(configPath, options.Sim, options.NoOutput);
        }

        return RunConsole(configPath, options);
    }

    private static int RunConsole(string configPath, CliOptions options)
    {
        string baseDir = AppContext.BaseDirectory;

        // 配置里包含日志路径，所以先用可切换出口把配置读出来，再建正式 Logger。
        var sink = new SwitchableLog();
        using var configs = ConfigStore.Load(configPath, sink);
        var cfg = configs.Current;

        string logPath = ResolvePath(baseDir,
            string.IsNullOrWhiteSpace(cfg.Log.Path) ? "logs/rumble.log" : cfg.Log.Path);

        using var log = new Logger(cfg.Log.Enabled ? logPath : null, console: true)
        {
            MinLevel = cfg.Log.Verbose ? LogLevel.Debug : LogLevel.Info,
        };
        sink.Inner = log;

        PrintBanner();
        log.Info($"配置文件：{configPath}");
        if (cfg.Log.Enabled)
        {
            log.Info($"日志文件：{logPath}");
        }

        string layoutError = ScsLayout.Verify();
        if (layoutError.Length != 0)
        {
            log.Error("共享内存结构体偏移自检失败，为避免错误震动，程序退出：" + layoutError);
            return 2;
        }

        log.Info(LayoutSummary);

        ITelemetrySource source;
        if (options.Sim)
        {
            source = new SimulatedSource();
        }
        else if (string.Equals(cfg.Source, "http", StringComparison.OrdinalIgnoreCase))
        {
            log.Error("config.source=http 属于二期功能：社区 ets2-telemetry-server 的 JSON 字段未核实，" +
                      "按项目约定不凭猜测实现。请把 source 改回 \"shm\"。");
            return 3;
        }
        else
        {
            source = new SharedMemorySource(log);
        }

        using (source)
        {
            if (options.Probe)
            {
                return Probe(source, log, options.ProbeSeconds);
            }

            IVibrationOutput output = options.NoOutput || cfg.Output.DryRun
                ? new NullOutput(log)
                : new XInputOutput(log, cfg.Output.ControllerIndex);

            using (output)
            {
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    log.Info("收到 Ctrl+C，正在退出…");
                    cts.Cancel();
                };

                // 直接关窗口时也要把马达归零，避免手柄一直震。
                AppDomain.CurrentDomain.ProcessExit += (_, _) => output.SetMotors(0, 0);

                var engine = new RumbleEngine(configs, source, output, log);
                engine.Run(cts.Token);
            }
        }

        return 0;
    }

    private static int Probe(ITelemetrySource source, Logger log, int seconds)
    {
        log.Info($"探针模式：每 0.5 秒读取一次 {source.Name}，共 {seconds} 秒（Ctrl+C 提前结束）");
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed.TotalSeconds < seconds)
        {
            if (source.TryRead(out var f))
            {
                log.Info($"[探针] sdkActive={f.SdkActive} paused={f.Paused} 游戏 {f.GameVersionMajor}.{f.GameVersionMinor} " +
                         $"插件修订 {f.PluginRevision} | 挡位 {f.Gear} 车速 {f.SpeedKph:F1}km/h 转速 {f.EngineRpm:F0} " +
                         $"踏板 {f.UserBrake:F2}/{f.GameBrake:F2} 加速度 {f.AccelX:F2}/{f.AccelY:F2}/{f.AccelZ:F2} " +
                         $"货损 {f.CargoDamage:F4} 撕裂 {f.Torn}");
            }
            else
            {
                log.Warn($"[探针] 读不到 {source.Name}：游戏未运行，或插件未加载（bin\\win_x64\\plugins\\scs-telemetry.dll）");
            }

            Thread.Sleep(500);
        }

        return 0;
    }

    internal static string ResolvePath(string baseDir, string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDir, path));

    private static void PrintBanner()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine(" ETS2 Rumble 0.2.0  (MVP: 握把马达)");
        Console.WriteLine(" 数据源: scs-sdk-plugin 共享内存 Local\\SCSTelemetry");
        Console.WriteLine(" 提示: Steam 设置 → 控制器 → 关闭“为 Xbox 控制器启用 Steam 输入”");
        Console.WriteLine("==============================================");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
        ETS2 Rumble — ETS2 手柄震动增强（MVP：握把马达）

        用法:
          Ets2Rumble.exe [选项]

        默认行为:
          启动图形界面（开关功能、调强度/时长、看实时日志、中英文切换）

        选项:
          --console           不启动界面，按原来的控制台方式运行
          --config <文件>     指定配置文件（默认 exe 同目录 config.json）
          --sim               使用模拟数据源，不依赖游戏（界面同样可用）
          --probe             探针模式：只读遥测并打印，不输出震动（控制台）
          --probe-seconds <n> 探针持续时间，默认 10 秒
          --no-output         不驱动手柄，只写日志
          -h, --help          显示本帮助

        示例:
          Ets2Rumble.exe
          Ets2Rumble.exe --sim
          Ets2Rumble.exe --probe --probe-seconds 15
          Ets2Rumble.exe --console --no-output
        """);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static void HideConsoleWindow()
    {
        try
        {
            var hwnd = GetConsoleWindow();
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, 0);
            }
        }
        catch
        {
        }
    }

    private sealed class CliOptions
    {
        public bool Sim { get; private set; }
        public bool Probe { get; private set; }
        public bool ConsoleMode { get; private set; }
        public int ProbeSeconds { get; private set; } = 10;
        public bool NoOutput { get; private set; }
        public string? ConfigPath { get; private set; }
        public bool Help { get; private set; }

        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--sim":
                        options.Sim = true;
                        break;
                    case "--probe":
                        options.Probe = true;
                        break;
                    case "--console":
                        options.ConsoleMode = true;
                        break;
                    case "--probe-seconds":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int seconds))
                        {
                            options.ProbeSeconds = Math.Clamp(seconds, 1, 600);
                            i++;
                        }

                        break;
                    case "--no-output":
                        options.NoOutput = true;
                        break;
                    case "--config":
                        if (i + 1 < args.Length)
                        {
                            options.ConfigPath = args[i + 1];
                            i++;
                        }

                        break;
                    case "-h":
                    case "--help":
                        options.Help = true;
                        break;
                    default:
                        Console.WriteLine($"未知参数：{args[i]}（用 --help 查看用法）");
                        break;
                }
            }

            return options;
        }
    }
}
