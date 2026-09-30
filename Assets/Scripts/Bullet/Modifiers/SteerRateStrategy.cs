using System;

[Serializable]
public abstract class SteerRateStrategy
{
    public abstract float Sample();
    public virtual SteerRateStrategy Clone() => (SteerRateStrategy)MemberwiseClone();
}
