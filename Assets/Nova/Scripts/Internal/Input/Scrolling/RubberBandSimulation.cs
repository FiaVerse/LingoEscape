// Copyright (c) Supernova Technologies LLC
using Unity.Mathematics;

namespace Nova.Internal.Input.Scrolling
{

    /// <summary>
    /// An implementation of scroll physics that matches iOS.
    /// Uses friction simulation until position goes past extents, then switches to rubberband to return to extent bounds.
    /// </summary>
    /// <remarks>    
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </remarks>
    internal class RubberBandScrollSimulation : IPhysicsSimulation
    {
        /// The maximum velocity that can be transferred from the inertia of a ballistic
        /// scroll into overscroll.
        private const double maxSpringTransferVelocity = 5000.0f;

        private double2 minMax;

        SpringDescription spring;

        private FrictionSimulation frictionSimulation = new FrictionSimulation();
        private SpringSimulation springSimulation = new SpringSimulation();

        /// <summary>
        /// The time at which to switch from friction to spring
        /// </summary>
        private double springStartTime;

        // Default drag value
        public const double Drag = 0.135;

        // A lesser drag vaue, arbitrarily Drag / 8 based on behavior
        public const double LowDrag = Drag / 8;
        public static readonly float LogLowDrag = (float)math.log(LowDrag);

        public void Init(SpringDescription spring, double2 minMax, double position, double velocity, ToleranceConfig tolerance, double drag)
        {
            // these must be assigned
            this.minMax = minMax;
            this.spring = spring;

            if (position < minMax.x)
            {
                // Already out of bounds
                InitSpringForUnderscroll(position, velocity);
                springStartTime = double.NegativeInfinity;
            }
            else if (position > minMax.y)
            {
                // Already out of bounds
                InitSpringForOverscroll(position, velocity);
                springStartTime = double.NegativeInfinity;
            }
            else
            {
                // In bounds, so start with friction
                frictionSimulation.Init(drag, position, velocity, tolerance);

                double finalX = frictionSimulation.FinalPosition;

                if (velocity > 0.0f && finalX > minMax.y)
                {
                    springStartTime = frictionSimulation.TimeToReachPosition(minMax.y);
                    InitSpringForOverscroll(minMax.y,
                      math.min(frictionSimulation.GetDerivAtTime(springStartTime), maxSpringTransferVelocity));
                }
                else if (velocity < 0.0f && finalX < minMax.x)
                {
                    springStartTime = frictionSimulation.TimeToReachPosition(minMax.x);
                    InitSpringForUnderscroll(minMax.x,
                      math.min(frictionSimulation.GetDerivAtTime(springStartTime), maxSpringTransferVelocity));
                }
                else
                {
                    springStartTime = double.PositiveInfinity;
                }
            }
        }

        private void InitSpringForUnderscroll(double x, double dx)
        {
            springSimulation.Init(spring, x, minMax.x, dx);
        }

        private void InitSpringForOverscroll(double x, double dx)
        {
            springSimulation.Init(spring, x, minMax.y, dx);
        }

        IPhysicsSimulation GetSimulationForTime(ref double time)
        {
            IPhysicsSimulation simulation;

            if (time > springStartTime)
            {
                if (math.isfinite(springStartTime))
                {
                    time -= springStartTime;
                }
                simulation = springSimulation;
            }
            else
            {
                simulation = frictionSimulation;
            }

            return simulation;
        }

        public double GetPositionAtTime(double time) => GetSimulationForTime(ref time).GetPositionAtTime(time);

        public double GetDerivAtTime(double time) => GetSimulationForTime(ref time).GetDerivAtTime(time);

        public bool IsDone(double time) => GetSimulationForTime(ref time).IsDone(time);
    }
}
