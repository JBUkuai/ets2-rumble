namespace Ets2Rumble.Config;

/// <summary>
/// 全部可调参数。改完保存即热重载，无需重启程序。
/// 单位统一：速度用 km/h，减速度用 m/s^2，时间用毫秒，强度 0..1。
/// </summary>
public sealed class AppConfig
{
    public string Source { get; set; } = "shm";
    public LoopConfig Loop { get; set; } = new();
    public ShiftConfig Shift { get; set; } = new();
    public BrakeConfig Brake { get; set; } = new();
    public BumpConfig Bump { get; set; } = new();
    public SwitchConfig Switches { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public LogConfig Log { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
}

/// <summary>界面偏好，与震动逻辑无关。</summary>
public sealed class UiConfig
{
    /// <summary>界面语言：zh 或 en。</summary>
    public string Language { get; set; } = "zh";
}

public sealed class LoopConfig
{
    /// <summary>轮询频率。30Hz 足够，想更省 CPU 可降到 20。</summary>
    public int PollHz { get; set; } = 30;

    /// <summary>游戏时间戳超过这个毫秒数没更新，就认为游戏暂停/卡住，清空震动。</summary>
    public int StaleTimeoutMs { get; set; } = 1000;
}

public sealed class ShiftConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>车速低于该值时换挡不触发（避免停车挂挡时乱震）。</summary>
    public double MinSpeedKph { get; set; } = 8;

    public double Strength { get; set; } = 0.35;
    public int DurationMs { get; set; } = 120;
    public int CooldownMs { get; set; } = 250;
}

public sealed class BrakeConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>刹车踏板阈值（0..1）。键盘玩家一脚就是 1.0，所以默认偏高。</summary>
    public double PedalThreshold { get; set; } = 0.85;

    /// <summary>减速度阈值（m/s^2，正数表示减速）。约 4 = 温和急刹，8 以上很猛。</summary>
    public double DecelThresholdMps2 { get; set; } = 4.0;

    /// <summary>条件持续多久才触发，过滤单帧抖动。</summary>
    public int HoldMs { get; set; } = 80;

    public double Strength { get; set; } = 0.6;
    public int DurationMs { get; set; } = 220;
    public int CooldownMs { get; set; } = 600;
}

public sealed class BumpConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 动态加速度阈值（m/s^2）。先做高通（减去慢速基线）再取三轴模长，
    /// 因此与重力是否包含在原始值里、以及轴的指向都无关。
    /// </summary>
    public double ThresholdMps2 { get; set; } = 4.0;

    /// <summary>货物损伤增量判据（0..1）。作为颠簸的辅助证据。</summary>
    public double CargoDamageDelta { get; set; } = 0.005;

    public double MinSpeedKph { get; set; } = 5;
    public double Strength { get; set; } = 0.8;
    public int DurationMs { get; set; } = 260;
    public int CooldownMs { get; set; } = 700;
}

/// <summary>
/// 小部件开关（转向灯、雨刮）。用的是 SDK 的“开关状态”而不是灯当前亮灭，
/// 因此拨一次只震一下，不会跟着转向灯闪烁反复震。
/// </summary>
public sealed class SwitchConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>左右转向灯拨杆。</summary>
    public bool Blinkers { get; set; } = true;

    /// <summary>雨刮开关。</summary>
    public bool Wipers { get; set; } = true;

    /// <summary>大灯（近光）开关。</summary>
    public bool Headlights { get; set; } = true;

    /// <summary>远近光切换（远光）。</summary>
    public bool HighBeam { get; set; } = true;

    /// <summary>双闪开关。</summary>
    public bool HazardLights { get; set; } = true;

    /// <summary>上面这些开关的震动强度，默认 30%。</summary>
    public double Strength { get; set; } = 0.30;

    /// <summary>开关的包络时长。太短会感觉不到，120~180 之间比较合适。</summary>
    public int DurationMs { get; set; } = 150;

    /// <summary>手刹（驻车制动）单独一组参数，因为它要更重、更长。</summary>
    public bool ParkingBrake { get; set; } = true;

    public double ParkingBrakeStrength { get; set; } = 0.45;

    /// <summary>拉手刹的真实动作约 0.5~0.7 秒，默认 600ms。</summary>
    public int ParkingBrakeDurationMs { get; set; } = 600;
}

public sealed class OutputConfig
{
    /// <summary>震动总开关。false = 完全不出力，但不影响遥测与日志。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>-1 = 自动找第一个已连接的 XInput 手柄；0..3 = 指定玩家索引。</summary>
    public int ControllerIndex { get; set; } = -1;

    public double LeftGain { get; set; } = 1.0;
    public double RightGain { get; set; } = 1.0;

    /// <summary>true = 只写日志不实际震动，用来安全试阈值。</summary>
    public bool DryRun { get; set; }
}

public sealed class LogConfig
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "logs/rumble.log";

    /// <summary>Debug 级别（含遥测采样）开关。</summary>
    public bool Verbose { get; set; }

    /// <summary>遥测采样间隔（毫秒），0 = 关闭。校准加速度轴时设为 200。</summary>
    public int TelemetrySampleMs { get; set; } = 0;
}
