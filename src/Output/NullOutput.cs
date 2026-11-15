using Ets2Rumble.Logging;

namespace Ets2Rumble.Output;

/// <summary>
/// 只记录不震动。用于手持校准阈值（--no-output 或 config.output.dryRun）。
/// </summary>
public sealed class NullOutput : IVibrationOutput
{
    private readonly Logger _log;
    private double _lastLogged = -1;

    public NullOutput(Logger log) => _log = log;

    public bool IsReady => true;

    public string Describe() => "空输出（仅日志）";

    public void SetMotors(double left, double right)
    {
        double value = Math.Max(left, right);
        if (Math.Abs(value - _lastLogged) < 0.02)
        {
            return;
        }

        _lastLogged = value;
        _log.Debug($"[dry-run] 马达输出 {value:F2}");
    }

    public void Dispose()
    {
    }
}
