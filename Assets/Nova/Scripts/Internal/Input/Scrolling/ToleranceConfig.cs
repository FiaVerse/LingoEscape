// Copyright (c) Supernova Technologies LLC
namespace Nova.Internal.Input.Scrolling
{
    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal struct ToleranceConfig
    {
        public ToleranceConfig(double distance = 1e-3, double time = 1e-3f, double velocity = 1e-3)
        {
            Distance = distance;
            Time = time;
            Velocity = velocity;
        }

        public static readonly ToleranceConfig Default = new ToleranceConfig(distance: 1e-3, time: 1e-3f, velocity: 1e-3);

        public double Distance;
        public double Time;
        public double Velocity;
    }
}
