namespace Ets2Rumble.Telemetry;

/// <summary>
/// HTTP 回退数据源（二期预留）。
///
/// 说明：http://localhost:25555/api/ets2/telemetry 由社区 ets2-telemetry-server 提供，
/// 不是 SCS 官方机制，且其 JSON 字段命名未在本地核实。按项目约定，不凭记忆编造字段，
/// 因此 MVP 只保留接口位置：启用即明确报错，避免返回假数据误导调参。
///
/// 二期实现要点（已写进文档）：
/// 1. HTTP 轮询必须跑在独立线程，转成 TelemetryFrame 后放入单槽位缓存；
/// 2. 震动主循环只读缓存，绝不等待网络；
/// 3. JSON 解析失败时保持上一帧并降低采样率。
/// </summary>
public sealed class HttpSource : ITelemetrySource
{
    public string Name => "HTTP 回退（二期，未实现）";

    public bool IsConnected => false;

    public bool TryRead(out TelemetryFrame frame)
    {
        frame = default;
        return false;
    }

    public void Dispose()
    {
    }
}
