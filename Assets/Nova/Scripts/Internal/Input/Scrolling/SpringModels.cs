// Copyright (c) Supernova Technologies LLC
using Unity.Mathematics;

namespace Nova.Internal.Input.Scrolling
{
    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal class CriticalSpring : SpringModel
    {
        private double r, c1, c2;

        public override void Init(SpringDescription spring, double distance, double velocity)
        {
            r = -spring.Damping / (2.0f * spring.Mass);
            c1 = distance;
            c2 = velocity / (r * distance);
        }

        public override double GetPositionAtTime(double time)
        {
            return (c1 + c2 * time) * math.pow(math.E, r * time);
        }

        public override double GetDerivAtTime(double time)
        {
            double power = math.pow(math.E, r * time);
            return r * (c1 + c2 * time) * power + c2 * power;
        }
    }

    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal class UnderdampedSpring : SpringModel
    {
        private double w, r, c1, c2;

        public override void Init(SpringDescription description, double distance, double velocity)
        {
            double a = 4.0f * description.Mass * description.Stiffness;
            double b = description.Damping * description.Damping;

            w = math.sqrt(math.max(a - b, 1)) / (2.0f * description.Mass);
            r = -(description.Damping / 2.0f * description.Mass);
            c1 = distance;
            c2 = (velocity - r * distance) / w;
        }

        public override double GetPositionAtTime(double time)
        {
            return math.pow(math.E, r * time) * (c1 * math.cos(w * time) + c2 * math.sin(w * time));
        }

        public override double GetDerivAtTime(double time)
        {
            double power = math.pow(math.E, r * time);
            double cosine = math.cos(w * time);
            double sine = math.sin(w * time);
            return power * (c2 * w * cosine - c1 * w * sine) + r * power * (c2 * sine + c1 * cosine);
        }
    }

    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal class OverdampedSpring : SpringModel
    {
        private double r1, r2, c1, c2;

        public override void Init(SpringDescription spring, double distance, double velocity)
        {
            double cmk = spring.Damping * spring.Damping - 4.0f * spring.Mass * spring.Stiffness;

            r1 = (-spring.Damping - math.sqrt(cmk)) / (2.0f * spring.Mass);
            r2 = (-spring.Damping + math.sqrt(cmk)) / (2.0f * spring.Mass);
            c2 = (velocity - r1 * distance) / (r2 - r1);
            c1 = distance - c2;
        }

        public override double GetPositionAtTime(double time)
        {
            return c1 * math.pow(math.E, r1 * time) +
                   c2 * math.pow(math.E, r2 * time);
        }

        public override double GetDerivAtTime(double time)
        {
            return c1 * r1 * math.pow(math.E, r1 * time) +
                   c2 * r2 * math.pow(math.E, r2 * time);
        }
    }

    internal abstract class SpringModel : IPhysicsModel
    {
        public abstract double GetPositionAtTime(double time);
        public abstract double GetDerivAtTime(double time);

        public abstract void Init(SpringDescription spring, double distance, double velocity);
    }
}
