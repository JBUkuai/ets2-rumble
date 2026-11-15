namespace Ets2Rumble.Output;

/// <summary>
/// 把多个震动请求混合成一路输出，并施加强度包络（线性衰减）。
/// 用"峰值 + 衰减"而不是开关，是为了让短脉冲也能被手感感知。
/// </summary>
public sealed class PulseMixer
{
    private struct Pulse
    {
        public double StartMs;
        public double DurationMs;
        public double Strength;
    }

    private readonly List<Pulse> _pulses = new(8);

    public int ActiveCount => _pulses.Count;

    public void Add(double strength, int durationMs, double nowMs)
    {
        if (durationMs <= 0)
        {
            return;
        }

        strength = Math.Clamp(strength, 0, 1);
        if (strength <= 0.001)
        {
            return;
        }

        _pulses.Add(new Pulse
        {
            StartMs = nowMs,
            DurationMs = durationMs,
            Strength = strength,
        });
    }

    /// <summary>当前应输出的强度 0..1（多路取最大值）。</summary>
    public double Evaluate(double nowMs)
    {
        double result = 0;

        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            var p = _pulses[i];
            double progress = (nowMs - p.StartMs) / p.DurationMs;
            if (progress >= 1.0)
            {
                _pulses.RemoveAt(i);
                continue;
            }

            if (progress < 0)
            {
                progress = 0;
            }

            // 平方衰减比线性更接近"一下就没"的冲击感，同时尾巴更细腻。
            double value = p.Strength * (1.0 - progress) * (1.0 - progress);
            if (value > result)
            {
                result = value;
            }
        }

        return result;
    }

    public void Clear() => _pulses.Clear();
}
