namespace Ets2Rumble.Events;

/// <summary>一次震动请求。Strength 0..1，DurationMs 为包络时长。</summary>
public readonly record struct RumbleEvent(string Kind, double Strength, int DurationMs, string Detail);
