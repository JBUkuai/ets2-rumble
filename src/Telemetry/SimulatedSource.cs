using System.Diagnostics;

namespace Ets2Rumble.Telemetry;

/// <summary>
/// 模拟数据源：不装游戏插件也能调阈值、验证震动链路。
/// 20 秒一个循环：加速到 80 km/h 再减速，中途自动换挡、急刹、周期性颠簸。
/// </summary>
public sealed class SimulatedSource : ITelemetrySource
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private float _cargoDamage;

    public string Name => "模拟数据源（--sim）";

    public bool IsConnected => true;

    public bool TryRead(out TelemetryFrame frame)
    {
        double t = _clock.Elapsed.TotalSeconds;
        double phase = t % 20.0;

        // 0..10 秒加速到 80 km/h，10..20 秒滑行回落
        double speedKph = phase < 10.0 ? phase * 8.0 : (20.0 - phase) * 8.0;
        if (speedKph < 0.5)
        {
            speedKph = 0;
        }

        int gear = speedKph < 1.0 ? 0 : 1 + (int)(speedKph / 15.0);

        // 13..15 秒急刹
        bool braking = phase is > 13.0 and < 15.0;
        if (braking)
        {
            speedKph *= (15.0 - phase) / 2.0;
            gear = 0;
        }

        // 每 9 秒来一次 0.6 秒的“大颠簸”
        bool bumping = (int)(t / 0.6) % 9 == 0;
        if (bumping)
        {
            _cargoDamage = Math.Min(1f, _cargoDamage + 0.0006f);
        }

        double speedMps = speedKph / 3.6;

        frame = new TelemetryFrame
        {
            SdkActive = true,
            Paused = false,
            Torn = false,
            TimestampUs = (ulong)(t * 1_000_000.0),
            GameVersionMajor = 1,
            GameVersionMinor = 62,
            PluginRevision = 12,
            Gear = gear,
            GearDashboard = gear,
            SpeedMps = (float)speedMps,
            EngineRpm = (float)(600 + speedKph * 22.0),
            UserBrake = braking ? 1f : 0f,
            GameBrake = braking ? 0.9f : 0f,
            AccelX = (float)(0.6 * Math.Sin(t * 1.7)),
            AccelY = (float)(0.4 * Math.Sin(t * 2.3)),
            AccelZ = (float)(bumping ? 8.5 * Math.Sin(t * 18.0) : 0.8 * Math.Sin(t * 3.1)),
            AngularAccelX = (float)(0.05 * Math.Sin(t * 0.9)),
            AngularAccelY = (float)(0.03 * Math.Sin(t * 1.1)),
            AngularAccelZ = (float)(0.02 * Math.Sin(t * 1.3)),
            CabinAccelX = (float)(0.1 * Math.Sin(t * 2.1)),
            CabinAccelY = (float)(0.1 * Math.Sin(t * 2.7)),
            CabinAccelZ = (float)(bumping ? 1.2 * Math.Sin(t * 20.0) : 0.2 * Math.Sin(t * 3.3)),
            CargoDamage = _cargoDamage,
        };

        return true;
    }

    public void Dispose()
    {
    }
}
