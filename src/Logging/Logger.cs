using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace Ets2Rumble.Logging;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
}

/// <summary>一条日志。界面直接消费它，不需要再解析文本。</summary>
public readonly record struct LogEntry(DateTime Time, LogLevel Level, string Message)
{
    public string Format() => $"{Time:yyyy-MM-dd HH:mm:ss.fff} [{Level.ToString().ToUpperInvariant(),-5}] {Message}";
}

/// <summary>
/// 单写线程的日志器：主循环只入队，落盘由后台线程批量完成，避免高频磁盘 IO 拖慢 30Hz 循环。
/// </summary>
public sealed class Logger : ILog, IDisposable
{
    private const long MaxFileBytes = 5 * 1024 * 1024;

    private readonly BlockingCollection<LogEntry> _queue =
        new(new ConcurrentQueue<LogEntry>(), 16 * 1024);

    private readonly Thread _thread;
    private readonly string? _path;
    private readonly bool _console;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private volatile bool _disposed;

    public LogLevel MinLevel { get; set; } = LogLevel.Info;

    /// <summary>每条日志落盘后回调（在日志线程上），界面订阅它做滚动显示。</summary>
    public event Action<LogEntry>? Emitted;

    public Logger(string? path, bool console = true)
    {
        _path = path;
        _console = console;
        _thread = new Thread(WriteLoop)
        {
            IsBackground = true,
            Name = "logger",
        };
        _thread.Start();
    }

    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warn(string message) => Write(LogLevel.Warn, message);
    public void Error(string message) => Write(LogLevel.Error, message);

    private void Write(LogLevel level, string message)
    {
        if (_disposed || level < MinLevel)
        {
            return;
        }

        var entry = new LogEntry(DateTime.Now, level, message);
        _queue.TryAdd(entry);
    }

    private void WriteLoop()
    {
        if (_path is not null)
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                RotateIfNeeded(_path);
                _writer = new StreamWriter(
                    new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = false,
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[logger] 无法打开日志文件 {_path}：{ex.Message}");
            }
        }

        var lastFlush = Environment.TickCount64;
        foreach (var entry in _queue.GetConsumingEnumerable())
        {
            var line = entry.Format();

            if (_console)
            {
                Console.WriteLine(line);
            }

            try
            {
                _writer?.WriteLine(line);
            }
            catch
            {
                // 日志失败不能影响主功能。
            }

            try
            {
                Emitted?.Invoke(entry);
            }
            catch
            {
                // 订阅方异常不能拖垮日志线程。
            }

            if (Environment.TickCount64 - lastFlush >= 1000 && _queue.Count == 0)
            {
                Flush();
                lastFlush = Environment.TickCount64;
            }
        }

        Flush();
        _writer?.Dispose();
        _writer = null;
    }

    private static void RotateIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (info.Exists && info.Length > MaxFileBytes)
        {
            var bak = path + ".1";
            if (File.Exists(bak))
            {
                File.Delete(bak);
            }

            File.Move(path, bak);
        }
    }

    private void Flush()
    {
        lock (_gate)
        {
            try
            {
                _writer?.Flush();
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(3));
        Flush();
        _queue.Dispose();
    }
}
