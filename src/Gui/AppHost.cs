using System.Windows;
using Ets2Rumble.Config;
using Ets2Rumble.Logging;
using Ets2Rumble.Telemetry;

namespace Ets2Rumble.Gui;

/// <summary>图形界面的启动装配：配置、日志、引擎宿主、窗口。</summary>
public static class AppHost
{
    /// <summary>
    /// XAML 编译后的资源名取自源码目录（src/Gui），因此这里必须与实际路径一致。
    /// 若移动 Gui 目录，只需改这一处。
    /// </summary>
    internal const string ResourceRoot = "pack://application:,,,/Ets2Rumble;component/src/gui/";
    public static int Run(string configPath, bool sim, bool noOutput)
    {
        var sink = new SwitchableLog();
        ConfigStore? configs = null;
        Logger? log = null;
        EngineHost? host = null;

        try
        {
            configs = ConfigStore.Load(configPath, sink);
            var cfg = configs.Current;

            string logPath = Program.ResolvePath(AppContext.BaseDirectory,
                string.IsNullOrWhiteSpace(cfg.Log.Path) ? "logs/rumble.log" : cfg.Log.Path);

            log = new Logger(cfg.Log.Enabled ? logPath : null, console: false)
            {
                MinLevel = cfg.Log.Verbose ? LogLevel.Debug : LogLevel.Info,
            };
            sink.Inner = log;

            log.Info($"配置文件：{configPath}");

            string layoutError = ScsLayout.Verify();
            if (layoutError.Length != 0)
            {
                log.Error("共享内存结构体偏移自检失败，程序退出：" + layoutError);
                MessageBox.Show(layoutError, "ETS2 Rumble", MessageBoxButton.OK, MessageBoxImage.Error);
                return 2;
            }

            log.Info(Program.LayoutSummary);

            host = new EngineHost(configs, log, sim, noOutput);

            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose,
            };

            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(ResourceRoot + "theme.xaml", UriKind.Absolute),
            });
            app.Resources.MergedDictionaries.Add(Lang.Load(cfg.Ui.Language));

            var window = new MainWindow(configs, log, host, configPath);
            app.Run(window);
            return 0;
        }
        catch (Exception ex)
        {
            log?.Error("界面启动失败：" + ex);
            MessageBox.Show(ex.Message, "ETS2 Rumble", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        finally
        {
            host?.Dispose();
            log?.Dispose();
            configs?.Dispose();
        }
    }
}

internal static class Lang
{
    public static ResourceDictionary Load(string code)
    {
        string name = string.Equals(code, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
        return new ResourceDictionary
        {
            Source = new Uri(AppHost.ResourceRoot + $"strings.{name}.xaml", UriKind.Absolute),
        };
    }
}
