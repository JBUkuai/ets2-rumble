namespace Ets2Rumble.Telemetry;

/// <summary>
/// 一帧遥测快照。设计成只读结构体，读取路径上零堆分配。
/// </summary>
public readonly struct TelemetryFrame
{
    public bool SdkActive { get; init; }
    public bool Paused { get; init; }

    /// <summary>插件写入是否发生了撕裂（我们是抢读，不阻塞游戏）。</summary>
    public bool Torn { get; init; }

    /// <summary>帧时间戳，微秒，来自游戏帧开始时间，用于判断数据是否新鲜。</summary>
    public ulong TimestampUs { get; init; }

    public int GameVersionMajor { get; init; }
    public int GameVersionMinor { get; init; }
    public int PluginRevision { get; init; }

    public int Gear { get; init; }
    public int GearDashboard { get; init; }

    /// <summary>m/s，倒车为负。</summary>
    public float SpeedMps { get; init; }
    public float EngineRpm { get; init; }

    /// <summary>刹车踏板输入 0..1。</summary>
    public float UserBrake { get; init; }

    /// <summary>模拟器实际刹车 0..1。</summary>
    public float GameBrake { get; init; }

    /// <summary>车身坐标系线加速度 m/s^2（重力/向心分量可能包含在内，检测时用动态分量）。</summary>
    public float AccelX { get; init; }
    public float AccelY { get; init; }
    public float AccelZ { get; init; }

    public float AngularAccelX { get; init; }
    public float AngularAccelY { get; init; }
    public float AngularAccelZ { get; init; }

    public float CabinAccelX { get; init; }
    public float CabinAccelY { get; init; }
    public float CabinAccelZ { get; init; }

    /// <summary>货物损伤 0..1，碰撞的辅助判据。</summary>
    public float CargoDamage { get; init; }

    // 小部件开关。注意用的是 SDK 的开关状态（lblinker/rblinker/wipers），
    // 不是灯当前是否点亮（light.lblinker 会随闪烁翻转）。
    public bool BlinkerLeft { get; init; }
    public bool BlinkerRight { get; init; }
    public bool Wipers { get; init; }

    /// <summary>近光（大灯开关）。</summary>
    public bool LightsLowBeam { get; init; }

    /// <summary>远光（远近光切换）。</summary>
    public bool LightsHighBeam { get; init; }

    /// <summary>危险报警闪光灯（双闪）。</summary>
    public bool HazardLights { get; init; }

    /// <summary>驻车制动（手刹）。</summary>
    public bool ParkingBrake { get; init; }

    public double SpeedKph => SpeedMps * 3.6;
}
