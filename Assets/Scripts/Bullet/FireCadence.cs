using System;

/// <summary>保留发射时间余量；每帧最多补发 8 批，超额批次跳过但保留周期相位。</summary>
public static class FireCadence
{
    public const int MaxBurstsPerTick = 8;

    public static int Tick(ref float cooldown, float rate, float dt)
    {
        if (!(dt > 0f) || float.IsInfinity(dt)) return 0;
        if (!(rate > 0f) || float.IsInfinity(rate))
        {
            cooldown = 0f;
            return 0;
        }
        double interval = 1.0 / rate;
        double remaining = cooldown - (double)dt;
        if (remaining > 0.0)
        {
            cooldown = (float)remaining;
            return 0;
        }
        double due = Math.Floor(-remaining / interval) + 1.0;
        cooldown = (float)(interval + remaining % interval);
        return (int)Math.Min(due, MaxBurstsPerTick);
    }
}
