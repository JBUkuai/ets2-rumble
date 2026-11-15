namespace Ets2Rumble.Logging;

public interface ILog
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

/// <summary>
/// 可替换的日志出口：配置读取阶段还没有正式 Logger（日志文件路径来自配置本身），
/// 先输出到控制台，Logger 建好后把后续日志接过去。
/// </summary>
public sealed class SwitchableLog : ILog
{
    private ILog? _inner;

    public ILog? Inner
    {
        get => _inner;
        set => _inner = value;
    }

    private void Write(string level, string message)
    {
        var inner = _inner;
        if (inner is not null)
        {
            switch (level)
            {
                case "DEBUG":
                    inner.Debug(message);
                    return;
                case "WARN":
                    inner.Warn(message);
                    return;
                case "ERROR":
                    inner.Error(message);
                    return;
                default:
                    inner.Info(message);
                    return;
            }
        }

        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{level,-5}] {message}");
    }

    public void Debug(string message) => Write("DEBUG", message);
    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);
}
