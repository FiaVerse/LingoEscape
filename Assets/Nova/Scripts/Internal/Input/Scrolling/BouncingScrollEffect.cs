// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Nova.Internal.Input.Scrolling
{
    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal class BouncingScrollEffect
    {
        private static readonly SpringDescription DefaultSpring = SpringDescription.Create(mass: 0.5f, stiffness: 100.0f, ratio: 1.1f);

        /// The default accuracy to which scrolling is computed.
        private static readonly ToleranceConfig DefaultTolerance = ToleranceConfig.Default;

        private RubberBandScrollSimulation scrollSimulation = new RubberBandScrollSimulation();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double GetFriction(double overscrollFraction) => 0.52f * math.pow(1 - overscrollFraction, 2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ApplyPhysicsToUserOffset(ScrollBounds bounds, double offset)
        {
            if (offset == 0 || !bounds.OutOfRange)
            {
                return offset;
            }

            bounds.GetOverscroll(offset, out double overscrollPast, out bool easing);

            double friction = easing
                // Apply less resistance when easing the overscroll vs tensioning.
                ? GetFriction((overscrollPast - math.abs(offset)) / bounds.ViewportDimension)
                : GetFriction(overscrollPast / bounds.ViewportDimension);
            double direction = math.sign(offset);

            return direction * ApplyFriction(overscrollPast, math.abs(offset), friction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static double ApplyFriction(double extentOutside, double absDelta, double gamma)
        {
            double total = 0.0f;

            if (extentOutside > 0)
            {
                double deltaToLimit = extentOutside / gamma;

                if (absDelta < deltaToLimit)
                {
                    return absDelta * gamma;
                }

                total = extentOutside;
                absDelta -= deltaToLimit;
            }

            return total + absDelta;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IPhysicsSimulation GetBallisticSimulation(ScrollBounds bounds, double velocity, double drag)
        {
            if (math.abs(velocity) >= DefaultTolerance.Velocity || bounds.OutOfRange)
            {
                scrollSimulation.Init(
                  spring: DefaultSpring,
                  minMax: bounds.MinMax,
                  position: bounds.Position,
                  velocity: velocity,
                  tolerance: DefaultTolerance,
                  drag
                );

                return scrollSimulation;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double CarriedMomentum(double existingVelocity) => math.sign(existingVelocity) * math.min(0.000816f * math.pow(math.abs(existingVelocity), 1.967f), 40000.0f);
    }
}
