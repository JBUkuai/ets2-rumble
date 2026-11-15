using System.Runtime.InteropServices;

namespace Ets2Rumble.Telemetry;

/// <summary>
/// scs-sdk-plugin 共享内存结构体的 C# 镜像。
/// 逐字段照抄 scs-telemetry-common.hpp 的 scsTelemetryMap_t（只到第 6 区末尾，2000 字节），
/// 区尾的 buffer_xx[n] 补齐数组原样保留，因此必须使用 Pack=1。
/// 注意：C++ 的 bool 是 1 字节，这里一律用 byte，不能用 C# bool（默认按 4 字节封送会错位）。
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct ScsTelemetryMap
{
    // ---- 第 1 区 0..39 ----
    public byte sdkActive;                  // 0
    public fixed byte placeHolder[3];
    public byte paused;                     // 4
    public fixed byte placeHolder2[3];
    public ulong time;                      // 8   帧时间戳（微秒，单调）
    public ulong simulatedTime;             // 16
    public ulong renderTime;                // 24
    public long multiplayerTimeOffset;      // 32

    // ---- 第 2 区 40..499 ----
    public ScsValues scsValues;             // 40
    public CommonUi commonUi;               // 64
    public ConfigUi configUi;               // 68
    public TruckUi truckUi;                 // 104
    public GameplayUi gameplayUi;           // 440
    public fixed byte bufferUi[48];         // 452..499

    // ---- 第 3 区 500..699 ----
    public CommonI commonI;                 // 500
    public TruckI truckI;                   // 504
    public GameplayI gameplayI;             // 640
    public fixed byte bufferI[56];          // 644..699

    // ---- 第 4 区 700..1499 ----
    public CommonF commonF;                 // 700
    public ConfigF configF;                 // 704
    public TruckF truckF;                   // 948
    public GameplayF gameplayF;             // 1456
    public JobF jobF;                       // 1468
    public fixed byte bufferF[28];          // 1472..1499

    // ---- 第 5 区 1500..1639 ----
    public ConfigB configB;                 // 1500
    public TruckB truckB;                   // 1566
    public GameplayB gameplayB;             // 1613
    public fixed byte bufferB[25];          // 1615..1639

    // ---- 第 6 区 1640..1999 ----
    public ConfigFv configFv;               // 1640
    public TruckFv truckFv;                 // 1868
    public fixed byte bufferFv[60];         // 1940..1999
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct ScsValues
{
    public uint telemetryPluginRevision;
    public uint versionMajor;
    public uint versionMinor;
    public uint game;
    public uint telemetryVersionGameMajor;
    public uint telemetryVersionGameMinor;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct CommonUi
{
    public uint timeAbs;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct ConfigUi
{
    public uint gears;
    public uint gearsReverse;
    public uint retarderStepCount;
    public uint truckWheelCount;
    public uint selectorCount;
    public uint timeAbsDelivery;
    public uint maxTrailerCount;
    public uint unitCount;
    public uint plannedDistanceKm;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct TruckUi
{
    public uint shifterSlot;
    public uint retarderBrake;
    public uint lightsAuxFront;
    public uint lightsAuxRoof;
    public fixed uint truck_wheelSubstance[16];
    public fixed uint hshifterPosition[32];
    public fixed uint hshifterBitmask[32];
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct GameplayUi
{
    public uint jobDeliveredDeliveryTime;
    public uint jobStartingTime;
    public uint jobFinishedTime;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct CommonI
{
    public int restStop;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct TruckI
{
    public int gear;                        // >0 前进挡, 0 空挡, <0 倒挡
    public int gearDashboard;
    public fixed int hshifterResulting[32];
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct GameplayI
{
    public int jobDeliveredEarnedXp;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct CommonF
{
    public float scale;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct ConfigF
{
    public float fuelCapacity;
    public float fuelWarningFactor;
    public float adblueCapacity;
    public float adblueWarningFactor;
    public float airPressureWarning;
    public float airPressurEmergency;       // 字段名沿用头文件拼写
    public float oilPressureWarning;
    public float waterTemperatureWarning;
    public float batteryVoltageWarning;
    public float engineRpmMax;
    public float gearDifferential;
    public float cargoMass;
    public fixed float truckWheelRadius[16];
    public fixed float gearRatiosForward[24];
    public fixed float gearRatiosReverse[8];
    public float unitMass;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct TruckF
{
    public float speed;                     // m/s，倒车为负
    public float engineRpm;
    public float userSteer;
    public float userThrottle;
    public float userBrake;                 // 刹车踏板 0..1
    public float userClutch;
    public float gameSteer;
    public float gameThrottle;
    public float gameBrake;                 // 实际刹车 0..1
    public float gameClutch;
    public float cruiseControlSpeed;
    public float airPressure;
    public float brakeTemperature;
    public float fuel;
    public float fuelAvgConsumption;
    public float fuelRange;
    public float adblue;
    public float oilPressure;
    public float oilTemperature;
    public float waterTemperature;
    public float batteryVoltage;
    public float lightsDashboard;
    public float wearEngine;
    public float wearTransmission;
    public float wearCabin;
    public float wearChassis;
    public float wearWheels;
    public float truckOdometer;
    public float routeDistance;
    public float routeTime;
    public float speedLimit;
    public fixed float truck_wheelSuspDeflection[16];
    public fixed float truck_wheelVelocity[16];
    public fixed float truck_wheelSteering[16];
    public fixed float truck_wheelRotation[16];
    public fixed float truck_wheelLift[16];
    public fixed float truck_wheelLiftOffset[16];
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct GameplayF
{
    public float jobDeliveredCargoDamage;
    public float jobDeliveredDistanceKm;
    public float refuelAmount;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct JobF
{
    public float cargoDamage;                // 0..1，碰撞的辅助信号
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct ConfigB
{
    public fixed byte truckWheelSteerable[16];
    public fixed byte truckWheelSimulated[16];
    public fixed byte truckWheelPowered[16];
    public fixed byte truckWheelLiftable[16];
    public byte isCargoLoaded;
    public byte specialJob;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct TruckB
{
    public byte parkBrake;
    public byte motorBrake;
    public byte airPressureWarning;
    public byte airPressureEmergency;
    public byte fuelWarning;
    public byte adblueWarning;
    public byte oilPressureWarning;
    public byte waterTemperatureWarning;
    public byte batteryVoltageWarning;
    public byte electricEnabled;
    public byte engineEnabled;
    public byte wipers;
    public byte blinkerLeftActive;
    public byte blinkerRightActive;
    public byte blinkerLeftOn;
    public byte blinkerRightOn;
    public byte lightsParking;
    public byte lightsBeamLow;
    public byte lightsBeamHigh;
    public byte lightsBeacon;
    public byte lightsBrake;
    public byte lightsReverse;
    public byte lightsHazard;
    public byte cruiseControl;
    public fixed byte truck_wheelOnGround[16];
    public fixed byte shifterToggle[2];
    public byte differentialLock;
    public byte liftAxle;
    public byte liftAxleIndicator;
    public byte trailerLiftAxle;
    public byte trailerLiftAxleIndicator;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct GameplayB
{
    public byte jobDeliveredAutoparkUsed;
    public byte jobDeliveredAutoloadUsed;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct ConfigFv
{
    public float cabinPositionX;
    public float cabinPositionY;
    public float cabinPositionZ;
    public float headPositionX;
    public float headPositionY;
    public float headPositionZ;
    public float truckHookPositionX;
    public float truckHookPositionY;
    public float truckHookPositionZ;
    public fixed float truckWheelPositionX[16];
    public fixed float truckWheelPositionY[16];
    public fixed float truckWheelPositionZ[16];
}

/// <summary>
/// 第 6 区的 fvector 组。注意命名陷阱（见 scs_telemetry.cpp 883-891 行）：
/// lv_acceleration* 实际是“线速度”，acceleration* 才是“线加速度”，
/// aa_acceleration* 是“角加速度”，av_acceleration* 是“角速度”。
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct TruckFv
{
    public float lv_accelerationX;          // 线速度 m/s
    public float lv_accelerationY;
    public float lv_accelerationZ;
    public float av_accelerationX;          // 角速度 转/秒
    public float av_accelerationY;
    public float av_accelerationZ;
    public float accelerationX;             // 线加速度 m/s^2
    public float accelerationY;
    public float accelerationZ;
    public float aa_accelerationX;          // 角加速度 转/秒^2
    public float aa_accelerationY;
    public float aa_accelerationZ;
    public float cabinAVX;
    public float cabinAVY;
    public float cabinAVZ;
    public float cabinAAX;
    public float cabinAAY;
    public float cabinAAZ;
}

/// <summary>
/// 布局常量与解析入口。所有偏移在启动时用 Marshal.OffsetOf 校验，对不上就直接报错退出，
/// 绝不在偏移错误的情况下继续震动。
/// </summary>
internal static class ScsLayout
{
    /// <summary>Windows 文件映射对象名，与插件源码 SCS_PLUGIN_MMF_NAME 完全一致。</summary>
    public const string MapName = @"Local\SCSTelemetry";

    /// <summary>只读前 2000 字节（第 6 区末尾），足够覆盖 MVP 用到的全部字段。</summary>
    public const int HeaderSize = 2000;

    public static TelemetryFrame Parse(byte[] buffer, ulong timeBefore, ulong timeAfter)
    {
        var map = MemoryMarshal.Cast<byte, ScsTelemetryMap>(
            buffer.AsSpan(0, HeaderSize))[0];

        return new TelemetryFrame
        {
            SdkActive = map.sdkActive != 0,
            Paused = map.paused != 0,
            Torn = timeBefore != timeAfter,
            TimestampUs = map.time,
            GameVersionMajor = (int)map.scsValues.versionMajor,
            GameVersionMinor = (int)map.scsValues.versionMinor,
            PluginRevision = (int)map.scsValues.telemetryPluginRevision,
            Gear = map.truckI.gear,
            GearDashboard = map.truckI.gearDashboard,
            SpeedMps = map.truckF.speed,
            EngineRpm = map.truckF.engineRpm,
            UserBrake = map.truckF.userBrake,
            GameBrake = map.truckF.gameBrake,
            AccelX = map.truckFv.accelerationX,
            AccelY = map.truckFv.accelerationY,
            AccelZ = map.truckFv.accelerationZ,
            AngularAccelX = map.truckFv.aa_accelerationX,
            AngularAccelY = map.truckFv.aa_accelerationY,
            AngularAccelZ = map.truckFv.aa_accelerationZ,
            CabinAccelX = map.truckFv.cabinAAX,
            CabinAccelY = map.truckFv.cabinAAY,
            CabinAccelZ = map.truckFv.cabinAAZ,
            CargoDamage = map.jobF.cargoDamage,
            BlinkerLeft = map.truckB.blinkerLeftActive != 0,
            BlinkerRight = map.truckB.blinkerRightActive != 0,
            Wipers = map.truckB.wipers != 0,
            LightsLowBeam = map.truckB.lightsBeamLow != 0,
            LightsHighBeam = map.truckB.lightsBeamHigh != 0,
            HazardLights = map.truckB.lightsHazard != 0,
            ParkingBrake = map.truckB.parkBrake != 0,
        };
    }

    /// <summary>
    /// 校验结构体镜像与插件头文件是否一致。返回空字符串表示通过。
    /// </summary>
    public static string Verify()
    {
        var errors = new List<string>();

        void Expect(string what, long actual, long expected)
        {
            if (actual != expected)
            {
                errors.Add($"{what}: 实际 {actual}，期望 {expected}");
            }
        }

        long Off(string field) => Marshal.OffsetOf<ScsTelemetryMap>(field).ToInt64();

        if (Marshal.SizeOf<ScsTelemetryMap>() != HeaderSize)
        {
            errors.Add($"结构体大小: {Marshal.SizeOf<ScsTelemetryMap>()}，期望 {HeaderSize}");
        }

        Expect("sdkActive", Off(nameof(ScsTelemetryMap.sdkActive)), 0);
        Expect("paused", Off(nameof(ScsTelemetryMap.paused)), 4);
        Expect("time", Off(nameof(ScsTelemetryMap.time)), 8);
        Expect("scsValues", Off(nameof(ScsTelemetryMap.scsValues)), 40);
        Expect("scsValues.versionMinor",
            Off(nameof(ScsTelemetryMap.scsValues)) + Marshal.OffsetOf<ScsValues>(nameof(ScsValues.versionMinor)), 48);
        Expect("truckI", Off(nameof(ScsTelemetryMap.truckI)), 504);
        Expect("truckI.gear",
            Off(nameof(ScsTelemetryMap.truckI)) + Marshal.OffsetOf<TruckI>(nameof(TruckI.gear)), 504);
        Expect("truckI.gearDashboard",
            Off(nameof(ScsTelemetryMap.truckI)) + Marshal.OffsetOf<TruckI>(nameof(TruckI.gearDashboard)), 508);
        Expect("truckF", Off(nameof(ScsTelemetryMap.truckF)), 948);
        Expect("truckF.speed",
            Off(nameof(ScsTelemetryMap.truckF)) + Marshal.OffsetOf<TruckF>(nameof(TruckF.speed)), 948);
        Expect("truckF.userBrake",
            Off(nameof(ScsTelemetryMap.truckF)) + Marshal.OffsetOf<TruckF>(nameof(TruckF.userBrake)), 964);
        Expect("truckF.gameBrake",
            Off(nameof(ScsTelemetryMap.truckF)) + Marshal.OffsetOf<TruckF>(nameof(TruckF.gameBrake)), 980);
        Expect("jobF.cargoDamage",
            Off(nameof(ScsTelemetryMap.jobF)) + Marshal.OffsetOf<JobF>(nameof(JobF.cargoDamage)), 1468);
        Expect("configB", Off(nameof(ScsTelemetryMap.configB)), 1500);
        Expect("truckB", Off(nameof(ScsTelemetryMap.truckB)), 1566);
        Expect("truckB.wipers",
            Off(nameof(ScsTelemetryMap.truckB)) + Marshal.OffsetOf<TruckB>(nameof(TruckB.wipers)), 1577);
        Expect("truckB.blinkerLeftActive",
            Off(nameof(ScsTelemetryMap.truckB)) + Marshal.OffsetOf<TruckB>(nameof(TruckB.blinkerLeftActive)), 1578);
        Expect("truckB.blinkerRightActive",
            Off(nameof(ScsTelemetryMap.truckB)) + Marshal.OffsetOf<TruckB>(nameof(TruckB.blinkerRightActive)), 1579);
        Expect("truckB.parkBrake",
            Off(nameof(ScsTelemetryMap.truckB)) + Marshal.OffsetOf<TruckB>(nameof(TruckB.parkBrake)), 1566);
        Expect("truckB.lightsBeamLow",
            Off(nameof(ScsTelemetryMap.truckB)) + Marshal.OffsetOf<TruckB>(nameof(TruckB.lightsBeamLow)), 1583);
        Expect("truckB.lightsBeamHigh",
            Off(nameof(ScsTelemetryMap.truckB)) + Marshal.OffsetOf<TruckB>(nameof(TruckB.lightsBeamHigh)), 1584);
        Expect("truckB.lightsHazard",
            Off(nameof(ScsTelemetryMap.truckB)) + Marshal.OffsetOf<TruckB>(nameof(TruckB.lightsHazard)), 1588);
        Expect("configFv", Off(nameof(ScsTelemetryMap.configFv)), 1640);
        Expect("truckFv", Off(nameof(ScsTelemetryMap.truckFv)), 1868);
        Expect("truckFv.accelerationX",
            Off(nameof(ScsTelemetryMap.truckFv)) + Marshal.OffsetOf<TruckFv>(nameof(TruckFv.accelerationX)), 1892);
        Expect("truckFv.aa_accelerationX",
            Off(nameof(ScsTelemetryMap.truckFv)) + Marshal.OffsetOf<TruckFv>(nameof(TruckFv.aa_accelerationX)), 1904);
        Expect("truckFv.cabinAAX",
            Off(nameof(ScsTelemetryMap.truckFv)) + Marshal.OffsetOf<TruckFv>(nameof(TruckFv.cabinAAX)), 1928);

        return errors.Count == 0 ? string.Empty : string.Join("；", errors);
    }
}
