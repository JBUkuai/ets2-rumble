using System.IO;
using System.IO.MemoryMappedFiles;
using Ets2Rumble.Logging;

namespace Ets2Rumble.Telemetry;

/// <summary>
/// 读取 scs-sdk-plugin 写入的 Windows 文件映射对象 Local\SCSTelemetry。
/// 只读打开，绝不写入游戏内存；抢读造成的撕裂帧只做计数，不阻塞游戏。
/// </summary>
public sealed class SharedMemorySource : ITelemetrySource
{
    private readonly Logger _log;
    private readonly byte[] _buffer = new byte[ScsLayout.HeaderSize];

    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _view;
    private DateTime _nextOpenAttemptUtc = DateTime.MinValue;

    public SharedMemorySource(Logger log) => _log = log;

    public string Name => @"SCS 共享内存 (Local\SCSTelemetry)";

    public bool IsConnected => _view is not null;

    public bool TryRead(out TelemetryFrame frame)
    {
        frame = default;

        if (_view is null)
        {
            if (DateTime.UtcNow < _nextOpenAttemptUtc)
            {
                return false;
            }

            if (!TryOpen())
            {
                _nextOpenAttemptUtc = DateTime.UtcNow.AddSeconds(1);
                return false;
            }
        }

        try
        {
            // 先读时间戳，拷贝，再读一次。两次一致说明这一帧大概率是完整的。
            ulong before = _view!.ReadUInt64(8);
            _view.ReadArray(0, _buffer, 0, _buffer.Length);
            ulong after = _view.ReadUInt64(8);

            frame = ScsLayout.Parse(_buffer, before, after);
            return true;
        }
        catch (Exception ex)
        {
            _log.Warn($"读取共享内存失败，将自动重连：{ex.GetType().Name} {ex.Message}");
            Close();
            _nextOpenAttemptUtc = DateTime.UtcNow.AddSeconds(1);
            return false;
        }
    }

    private bool TryOpen()
    {
        try
        {
            _mmf = MemoryMappedFile.OpenExisting(
                ScsLayout.MapName, MemoryMappedFileRights.Read, HandleInheritability.None);
        }
        catch (UnauthorizedAccessException)
        {
            // 极少数情况下只读权限被拒，退回到默认（读写）打开。
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(ScsLayout.MapName);
            }
            catch (Exception ex)
            {
                _log.Debug($"打开共享内存失败：{ex.GetType().Name}");
                return false;
            }
        }
        catch (FileNotFoundException)
        {
            // 游戏没运行，静默等待，不打日志刷屏。
            return false;
        }
        catch (Exception ex)
        {
            _log.Debug($"打开共享内存失败：{ex.GetType().Name}");
            return false;
        }

        try
        {
            _view = _mmf.CreateViewAccessor(0, ScsLayout.HeaderSize, MemoryMappedFileAccess.Read);
            _log.Info($"已连接共享内存：{ScsLayout.MapName}");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warn($"映射共享内存视图失败：{ex.GetType().Name} {ex.Message}");
            Close();
            return false;
        }
    }

    private void Close()
    {
        _view?.Dispose();
        _view = null;
        _mmf?.Dispose();
        _mmf = null;
    }

    public void Dispose() => Close();
}
