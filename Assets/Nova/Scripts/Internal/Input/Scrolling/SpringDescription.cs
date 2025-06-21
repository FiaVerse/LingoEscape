// Copyright (c) Supernova Technologies LLC
using Unity.Mathematics;

namespace Nova.Internal.Input.Scrolling
{
    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal struct SpringDescription
    {
        public double Mass;
        public double Stiffness;
        public double Damping;

        public static SpringDescription Create(double mass, double stiffness, double ratio = 1)
        {
            return new SpringDescription()
            {
                Mass = mass,
                Stiffness = stiffness,
                Damping = ratio * 2.0f * math.sqrt(mass * stiffness)
            };
        }
    }
}
