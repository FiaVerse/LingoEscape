// Copyright (c) Supernova Technologies LLC
using Unity.Mathematics;

namespace Nova.Internal.Input.Scrolling
{
    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal class FrictionSimulation : IPhysicsSimulation
    {
        private ToleranceConfig tolerance;
        private double drag;
        private double logDrag;
        private double position;
        private double velocity;

        public void Init(double drag, double position, double velocity, ToleranceConfig tolerance)
        {
            this.drag = drag;
            logDrag = math.log(drag);
            this.position = position;
            this.velocity = velocity;
            this.tolerance = tolerance;
        }

        public void Init(double startPosition, double endPosition, double startVelocity, double endVelocity)
        {
            Init(DragFor(startPosition, endPosition, startVelocity, endVelocity),
                 startPosition,
                 startVelocity,
                 tolerance: new ToleranceConfig() { Velocity = math.abs(endVelocity) });
        }

        static double DragFor(double startPosition, double endPosition, double startVelocity, double endVelocity)
        {
            double drag = math.pow(math.E, (startVelocity - endVelocity) / (startPosition - endPosition));

            return double.IsInfinity(drag) ? 0 : drag;
        }

        public double GetPositionAtTime(double time) => position + velocity * math.pow(drag, time) / logDrag - velocity / logDrag;

        public double GetDerivAtTime(double time) => velocity * math.pow(drag, time);

        public double FinalPosition => position - velocity / logDrag;

        public double TimeToReachPosition(double x)
        {
            if (x == position)
            {
                return 0.0f;
            }

            if (velocity == 0.0f || (velocity > 0 ? (x < position || x > FinalPosition) : (x > position || x < FinalPosition)))
            {
                return double.PositiveInfinity;
            }

            return math.log(logDrag * (x - position) / velocity + 1.0) / logDrag;
        }

        public bool IsDone(double time)
        {
            double currentVelocity = math.abs(GetDerivAtTime(time));
            return currentVelocity < tolerance.Velocity;
        }
    }
}
