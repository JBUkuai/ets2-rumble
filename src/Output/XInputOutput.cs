using System.Reflection;
using System.Runtime.InteropServices;
using Ets2Rumble.Logging;

namespace Ets2Rumble.Output;

/// <summary>
/// XInput 握把马达输出。
/// xinput1_4.dll 在 Win10/11 自带；缺失时按 1_4 -> 1_3 -> 9_1_0 顺序回退。
/// </summary>
public sealed class XInputOutput : IVibrationOutput
{
    private readonly Logger _log;
    private readonly int _requestedIndex;

    private int _index = -1;
    private ushort _lastLeft;
    private ushort _lastRight;
    private DateTime _nextScanUtc = DateTime.MinValue;
    private bool _loggedMissing;

    public XInputOutput(Logger log, int requestedIndex)
    {
        _log = log;
        _requestedIndex = requestedIndex;
        Scan();
    }

    public bool IsReady => _index >= 0;

    /// <summary>当前使用的手柄索引，-1 表示未连接。</summary>
    public int Index => _index;

    public string Describe() => _index >= 0 ? $"XInput 手柄 #{_index}" : "XInput 手柄（未连接）";

    public void SetMotors(double left, double right)
    {
        if (_index < 0)
        {
            if (DateTime.UtcNow >= _nextScanUtc)
            {
                _nextScanUtc = DateTime.UtcNow.AddSeconds(2);
                Scan();
            }

            if (_index < 0)
            {
                return;
            }
        }

        ushort l = ToMotor(left);
        ushort r = ToMotor(right);
        if (l == _lastLeft && r == _lastRight)
        {
            return;
        }

        var vibration = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed = l,
            wRightMotorSpeed = r,
        };

        uint result = XInputNative.XInputSetState((uint)_index, ref vibration);
        if (result == 0)
        {
            _lastLeft = l;
            _lastRight = r;
            return;
        }

        _log.Warn($"XInputSetState 失败（错误码 {result}），手柄可能已断开，2 秒后重试");
        _lastLeft = 0;
        _lastRight = 0;
        _index = -1;
        _nextScanUtc = DateTime.UtcNow.AddSeconds(2);
    }

    private void Scan()
    {
        if (_requestedIndex >= 0)
        {
            if (XInputNative.XInputGetState((uint)_requestedIndex, out _) == 0)
            {
                Attach(_requestedIndex);
            }
            else
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    _log.Warn($"XInput 索引 {_requestedIndex} 没有手柄响应，请改成 -1 自动检测");
                }
            }

            return;
        }

        for (uint i = 0; i < 4; i++)
        {
            if (XInputNative.XInputGetState(i, out _) == 0)
            {
                Attach((int)i);
                return;
            }
        }

        // 只在“从有到无”或首次扫描时提示，避免每 2 秒刷一行日志。
        if (!_loggedMissing)
        {
            _loggedMissing = true;
            _log.Warn("未检测到 XInput 手柄。请确认：手柄处于 XInput 模式，且 Steam 未接管（Steam 设置 → 控制器 → 关闭“为 Xbox 控制器启用 Steam 输入”）");
        }
    }

    private void Attach(int index)
    {
        _index = index;
        _nextScanUtc = DateTime.MinValue;
        _loggedMissing = false;

        if (XInputNative.XInputGetCapabilities((uint)index, XInputNative.XINPUT_FLAG_GAMEPAD, out var caps) == 0)
        {
            _log.Info($"已连接 XInput 手柄 #{index}：类型 {caps.Type}/{caps.SubType}，功能标志 0x{caps.Flags:X4}");
        }
        else
        {
            _log.Info($"已连接 XInput 手柄 #{index}（能力信息不可读）");
        }
    }

    private static ushort ToMotor(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        return (ushort)Math.Clamp(value * 65535.0, 0, 65535);
    }

    public void Dispose()
    {
        if (_index >= 0)
        {
            var stop = new XINPUT_VIBRATION { wLeftMotorSpeed = 0, wRightMotorSpeed = 0 };
            XInputNative.XInputSetState((uint)_index, ref stop);
            _index = -1;
        }
    }
}

internal static class XInputNative
{
    public const uint XINPUT_FLAG_GAMEPAD = 0x00000001;

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    public static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    public static extern uint XInputSetState(uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
    public static extern uint XInputGetCapabilities(uint dwUserIndex, uint dwFlags, out XINPUT_CAPABILITIES pCapabilities);

    static XInputNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(XInputNative).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.StartsWith("xinput", StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" })
        {
            if (NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct XINPUT_STATE
{
    public uint dwPacketNumber;
    public XINPUT_GAMEPAD Gamepad;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XINPUT_GAMEPAD
{
    public ushort wButtons;
    public byte bLeftTrigger;
    public byte bRightTrigger;
    public short sThumbLX;
    public short sThumbLY;
    public short sThumbRX;
    public short sThumbRY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XINPUT_VIBRATION
{
    public ushort wLeftMotorSpeed;
    public ushort wRightMotorSpeed;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XINPUT_CAPABILITIES
{
    public byte Type;
    public byte SubType;
    public ushort Flags;
    public XINPUT_GAMEPAD Gamepad;
    public XINPUT_VIBRATION Vibration;
}
