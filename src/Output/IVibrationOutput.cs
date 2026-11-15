namespace Ets2Rumble.Output;

/// <summary>
/// 震动输出接口。MVP 只有左右握把马达；
/// 二期扳机震动会新增实现（需要 HID / Windows.Gaming.Input，而非 XInputSetState）。
/// </summary>
public interface IVibrationOutput : IDisposable
{
    bool IsReady { get; }

    string Describe();

    /// <summary>左右马达强度 0..1。实现内部应做去重，避免无意义的重复调用。</summary>
    void SetMotors(double left, double right);
}
