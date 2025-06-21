// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;

namespace Nova.Internal.Input.Scrolling
{ 
    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal class SpringSimulation : IPhysicsSimulation
    {
        private OverdampedSpring overdamped = new OverdampedSpring();
        private UnderdampedSpring underdamped = new UnderdampedSpring();
        private CriticalSpring critical = new CriticalSpring();

        protected ToleranceConfig tolerance = ToleranceConfig.Default;

        protected double endPosition;

        protected SpringModel springModel;

        public void Init(SpringDescription spring, double start, double end, double initialVelocity)
        {
            endPosition = end;
            springModel = InitModel(spring, start - end, initialVelocity);
        }

        public virtual double GetDerivAtTime(double time) => springModel.GetDerivAtTime(time);

        public virtual double GetPositionAtTime(double time) => IsDone(time) ? endPosition : endPosition + springModel.GetPositionAtTime(time);

        public bool IsDone(double time)
        {
            double x = springModel.GetPositionAtTime(time);
            double dx = springModel.GetDerivAtTime(time);

            return Math.ApproximatelyZero(x, tolerance.Distance) && Math.ApproximatelyZero(dx, tolerance.Velocity);
        }

        private SpringModel InitModel(SpringDescription spring, double distance, double initialVelocity)
        {
            double cmk = spring.Damping * spring.Damping - 4 * spring.Mass * spring.Stiffness;

            if (cmk == 0.0f)
            {
                critical.Init(spring, distance, initialVelocity);
                return critical;
            }

            if (cmk > 0.0f)
            {
                overdamped.Init(spring, distance, initialVelocity);
                return overdamped;
            }

            underdamped.Init(spring, distance, initialVelocity);
            return underdamped;
        }
    }
}
