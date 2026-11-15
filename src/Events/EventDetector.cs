using Ets2Rumble.Config;
using Ets2Rumble.Telemetry;

namespace Ets2Rumble.Events;

/// <summary>
/// 事件检测 + 冷却 + 迟滞。纯计算，无 IO、无分配（事件对象只在触发时创建）。
///
/// 三类事件：
/// 1. 换挡冲击：挡位变化且车速高于阈值；
/// 2. 急刹：踏板超过阈值，或减速度超过阈值并持续 HoldMs；
/// 3. 碰撞/大颠簸：线加速度的动态分量超过阈值，或货物损伤出现增量。
/// </summary>
public sealed class EventDetector
{
    private const int SpeedHistorySize = 32;

    private readonly double[] _speedT = new double[SpeedHistorySize];
    private readonly double[] _speedV = new double[SpeedHistorySize];

    private AppConfig _cfg = new();
    private int _speedIdx;
    private int _speedCount;

    private bool _hasGear;
    private int _lastGear;
    private float _lastRpm;
    private double _lastShiftMs = double.NegativeInfinity;

    private double _lastBrakeMs = double.NegativeInfinity;
    private double _brakeHoldMs;
    private double _brakeReleaseMs;
    private bool _brakeLatched;

    private double _lastBumpMs = double.NegativeInfinity;
    private float _lastDamage;
    private bool _hasDamage;

    private double _lastFrameMs = double.NegativeInfinity;
    private double _lastSpeedMps;

    private double _emaX, _emaY, _emaZ;
    private bool _hasEma;

    private bool _hasSwitchState;
    private bool _lastBlinkerLeft;
    private bool _lastBlinkerRight;
    private bool _lastWipers;
    private bool _lastLowBeam;
    private bool _lastHighBeam;
    private bool _lastHazard;
    private bool _lastParkingBrake;

    public void Configure(AppConfig cfg) => _cfg = cfg;

    public void Detect(in TelemetryFrame f, double nowMs, List<RumbleEvent> output)
    {
        double dt = double.IsNegativeInfinity(_lastFrameMs) ? 0 : Math.Max(0, nowMs - _lastFrameMs);
        _lastFrameMs = nowMs;

        // 传送/读档/重置保护：单帧速度跳变过大时，清掉历史，避免数据断层被当成撞车。
        if (Math.Abs(f.SpeedMps - _lastSpeedMps) > 15.0)
        {
            ResetTransient();
        }

        _lastSpeedMps = f.SpeedMps;
        PushSpeed(nowMs, f.SpeedMps);

        DetectShift(f, nowMs, output);
        DetectBrake(f, nowMs, dt, output);
        DetectBump(f, nowMs, output);
        DetectSwitches(f, output);
    }

    /// <summary>
    /// 小部件开关：转向灯拨杆、雨刮。只在状态变化时触发，所以拨开和拨回各震一次。
    /// 用的是 lblinker/rblinker/wipers（开关状态），不是 light.lblinker（会随闪烁翻转）。
    /// </summary>
    private void DetectSwitches(in TelemetryFrame f, List<RumbleEvent> output)
    {
        if (!_cfg.Switches.Enabled)
        {
            _hasSwitchState = false;
            return;
        }

        // 第一帧只建立基线，不触发。
        if (!_hasSwitchState)
        {
            _lastBlinkerLeft = f.BlinkerLeft;
            _lastBlinkerRight = f.BlinkerRight;
            _lastWipers = f.Wipers;
            _lastLowBeam = f.LightsLowBeam;
            _lastHighBeam = f.LightsHighBeam;
            _lastHazard = f.HazardLights;
            _lastParkingBrake = f.ParkingBrake;
            _hasSwitchState = true;
            return;
        }

        var cfg = _cfg.Switches;

        // 打开双闪时游戏可能同时把左右转向灯置为 true，这里跳过同帧的转向灯事件，
        // 避免一次双闪产生三个震动请求。
        bool hazardChanged = cfg.HazardLights && f.HazardLights != _lastHazard;

        if (!hazardChanged)
        {
            if (cfg.Blinkers && f.BlinkerLeft != _lastBlinkerLeft)
            {
                EmitSwitch("左转向灯", f.BlinkerLeft, cfg.Strength, cfg.DurationMs, output);
            }

            if (cfg.Blinkers && f.BlinkerRight != _lastBlinkerRight)
            {
                EmitSwitch("右转向灯", f.BlinkerRight, cfg.Strength, cfg.DurationMs, output);
            }
        }

        if (cfg.Wipers && f.Wipers != _lastWipers)
        {
            EmitSwitch("雨刮", f.Wipers, cfg.Strength, cfg.DurationMs, output);
        }

        if (cfg.Headlights && f.LightsLowBeam != _lastLowBeam)
        {
            EmitSwitch("大灯", f.LightsLowBeam, cfg.Strength, cfg.DurationMs, output);
        }

        if (cfg.HighBeam && f.LightsHighBeam != _lastHighBeam)
        {
            EmitSwitch("远光", f.LightsHighBeam, cfg.Strength, cfg.DurationMs, output);
        }

        if (hazardChanged)
        {
            EmitSwitch("双闪", f.HazardLights, cfg.Strength, cfg.DurationMs, output);
        }

        if (cfg.ParkingBrake && f.ParkingBrake != _lastParkingBrake)
        {
            EmitSwitch("手刹", f.ParkingBrake,
                cfg.ParkingBrakeStrength, cfg.ParkingBrakeDurationMs, output);
        }

        _lastBlinkerLeft = f.BlinkerLeft;
        _lastBlinkerRight = f.BlinkerRight;
        _lastWipers = f.Wipers;
        _lastLowBeam = f.LightsLowBeam;
        _lastHighBeam = f.LightsHighBeam;
        _lastHazard = f.HazardLights;
        _lastParkingBrake = f.ParkingBrake;
    }

    private static void EmitSwitch(string name, bool isOn, double strength, int durationMs,
        List<RumbleEvent> output)
    {
        output.Add(new RumbleEvent(
            name,
            Math.Clamp(strength, 0, 1),
            durationMs,
            isOn ? "开" : "关"));
    }

    private void DetectShift(in TelemetryFrame f, double nowMs, List<RumbleEvent> output)
    {
        if (_cfg.Shift.Enabled && _hasGear && f.Gear != _lastGear)
        {
            double speedKph = Math.Abs(f.SpeedKph);
            if (speedKph >= _cfg.Shift.MinSpeedKph && nowMs - _lastShiftMs >= _cfg.Shift.CooldownMs)
            {
                double rpmJump = Math.Abs(f.EngineRpm - _lastRpm);
                double k = Math.Clamp(1.0 + rpmJump / 3000.0, 0.8, 1.5);
                double strength = Math.Clamp(_cfg.Shift.Strength * k, 0, 1);
                output.Add(new RumbleEvent(
                    "换挡",
                    strength,
                    _cfg.Shift.DurationMs,
                    $"挡位 {_lastGear}->{f.Gear}，车速 {speedKph:F0} km/h，转速跳变 {rpmJump:F0}"));
                _lastShiftMs = nowMs;
            }
        }

        _hasGear = true;
        _lastGear = f.Gear;
        _lastRpm = f.EngineRpm;
    }

    private void DetectBrake(in TelemetryFrame f, double nowMs, double dt, List<RumbleEvent> output)
    {
        if (!_cfg.Brake.Enabled)
        {
            return;
        }

        double speedKph = Math.Abs(f.SpeedKph);
        double decel = ComputeDecelMps2(nowMs, f.SpeedMps);

        bool pedal = f.UserBrake >= _cfg.Brake.PedalThreshold && speedKph > 3;
        bool hardDecel = decel >= _cfg.Brake.DecelThresholdMps2 && speedKph > 3;

        if (pedal || hardDecel)
        {
            _brakeHoldMs += dt;
            _brakeReleaseMs = 0;
        }
        else
        {
            _brakeHoldMs = 0;
            _brakeReleaseMs += dt;

            // 条件解除 300ms 后才解锁，避免一次长踩刹车被切成十几下脉冲。
            if (_brakeReleaseMs >= 300)
            {
                _brakeLatched = false;
            }
        }

        if (!_brakeLatched &&
            _brakeHoldMs >= _cfg.Brake.HoldMs &&
            nowMs - _lastBrakeMs >= _cfg.Brake.CooldownMs)
        {
            double ratio = Math.Max(0, decel - _cfg.Brake.DecelThresholdMps2) /
                           Math.Max(0.001, _cfg.Brake.DecelThresholdMps2);
            double k = Math.Clamp(0.7 + ratio, 0.7, 1.3);
            output.Add(new RumbleEvent(
                "急刹",
                Math.Clamp(_cfg.Brake.Strength * k, 0, 1),
                _cfg.Brake.DurationMs,
                $"踏板 {f.UserBrake:F2}，减速度 {decel:F1} m/s²，车速 {speedKph:F0} km/h"));
            _lastBrakeMs = nowMs;
            _brakeHoldMs = 0;
            _brakeLatched = true;
        }
    }

    private void DetectBump(in TelemetryFrame f, double nowMs, List<RumbleEvent> output)
    {
        if (!_cfg.Bump.Enabled)
        {
            return;
        }

        // 高通：减去慢速基线，只保留突变。这样与重力是否计入、轴的指向都无关。
        if (!_hasEma)
        {
            _emaX = f.AccelX;
            _emaY = f.AccelY;
            _emaZ = f.AccelZ;
            _hasEma = true;
        }
        else
        {
            const double alpha = 0.03;
            _emaX += (f.AccelX - _emaX) * alpha;
            _emaY += (f.AccelY - _emaY) * alpha;
            _emaZ += (f.AccelZ - _emaZ) * alpha;
        }

        double dx = f.AccelX - _emaX;
        double dy = f.AccelY - _emaY;
        double dz = f.AccelZ - _emaZ;
        double deviation = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        double damageDelta = _hasDamage ? f.CargoDamage - _lastDamage : 0;
        _lastDamage = f.CargoDamage;
        _hasDamage = true;

        bool spike = deviation >= _cfg.Bump.ThresholdMps2;
        bool damageHit = damageDelta > _cfg.Bump.CargoDamageDelta && damageDelta < 0.3;
        double speedKph = Math.Abs(f.SpeedKph);

        if ((spike || damageHit) &&
            speedKph >= _cfg.Bump.MinSpeedKph &&
            nowMs - _lastBumpMs >= _cfg.Bump.CooldownMs)
        {
            double k = Math.Clamp(0.4 + (deviation - _cfg.Bump.ThresholdMps2) /
                                  Math.Max(0.001, _cfg.Bump.ThresholdMps2), 0.4, 1.0);
            string reason = spike ? $"加速度突变 {deviation:F1} m/s²" : $"货物损伤 +{damageDelta:F4}";
            output.Add(new RumbleEvent(
                "颠簸",
                Math.Clamp(_cfg.Bump.Strength * k, 0, 1),
                _cfg.Bump.DurationMs,
                $"{reason}，车速 {speedKph:F0} km/h，原始轴 {f.AccelX:F1}/{f.AccelY:F1}/{f.AccelZ:F1}"));
            _lastBumpMs = nowMs;
        }
    }

    private void PushSpeed(double nowMs, double speedMps)
    {
        _speedT[_speedIdx] = nowMs;
        _speedV[_speedIdx] = speedMps;
        _speedIdx = (_speedIdx + 1) % SpeedHistorySize;
        if (_speedCount < SpeedHistorySize)
        {
            _speedCount++;
        }
    }

    /// <summary>用约 200ms 前的速度样本算减速度，正数表示在减速。</summary>
    private double ComputeDecelMps2(double nowMs, double currentSpeedMps)
    {
        for (int k = 1; k <= _speedCount; k++)
        {
            int i = (_speedIdx - k + SpeedHistorySize) % SpeedHistorySize;
            double age = nowMs - _speedT[i];
            if (age >= 200.0)
            {
                double dv = currentSpeedMps - _speedV[i];
                return -dv / (age / 1000.0);
            }
        }

        return 0;
    }

    private void ResetTransient()
    {
        _hasGear = false;
        _hasEma = false;
        _hasDamage = false;
        _hasSwitchState = false;
        _brakeHoldMs = 0;
        _brakeReleaseMs = 0;
        _brakeLatched = false;
        _speedCount = 0;
    }
}
