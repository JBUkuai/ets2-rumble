namespace Ets2Rumble.Telemetry;

/// <summary>
/// 遥测数据源接口。MVP 使用共享内存实现；HTTP 作为二期回退（见 HttpSource）。
/// </summary>
public interface ITelemetrySource : IDisposable
{
    /// <summary>人类可读的数据源名称，写日志用。</summary>
    string Name { get; }

    /// <summary>当前是否已连接。</summary>
    bool IsConnected { get; }

    /// <summary>
    /// 尝试读取一帧。返回 false 表示暂时不可用（游戏未启动 / 已退出），
    /// 调用方应继续轮询而不是退出。实现不得阻塞。
    /// </summary>
    bool TryRead(out TelemetryFrame frame);
}
