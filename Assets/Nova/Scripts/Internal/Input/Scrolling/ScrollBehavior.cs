// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using Unity.Mathematics;

namespace Nova.Internal.Input.Scrolling
{
    internal interface IScrollBoundsProvider
    {
        ScrollBounds GetBounds();
    }

    /// <summary>
    /// Mostly ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal class ScrollBehavior
    {
        #region Constants
        private const float MomentumRetainStationaryDurationThreshold = 0.2f;
        private const float MomentumRetainVelocityThresholdFactor = 0.5f;
        private const float MotionStoppedDurationThreshold = 0.05f;
        private const float BigThresholdBreakDistance = 24.0f;
        private const double UnclampedBoundsMaxOutOfViewPercent = 0.9;
        #endregion

        private IScrollBoundsProvider scroller;
        private BouncingScrollEffect physics = new BouncingScrollEffect();
        private ScrollBounds currentBounds;
        private IPhysicsSimulation simulation;

        private bool retainMomentum;
        private float lastNonStationaryTimestamp;
        private float adjustedSimulationAtTime;

        private bool inMotion = false;
        private float offsetSinceLastStop = 0;

        private float motionStartDistanceThreshold;
        private double carriedVelocity;

        public bool ClampToBounds = false;

        private double CarriedVelocity(float time) => simulation == null ? 0 : physics.CarriedMomentum(simulation.GetDerivAtTime(time));

        private void UpdateBounds()
        {
            currentBounds = scroller.GetBounds();
        }

        private bool IsSimulationRunning(float time) => simulation == null ? false : !simulation.IsDone(time);

        public float GetSimulationVelocity(float timestamp)
        {
            float localTime = timestamp - adjustedSimulationAtTime;

            if (!IsSimulationRunning(localTime))
            {
                return 0;
            }

            return (float)-simulation.GetDerivAtTime(localTime);
        }

        /// <summary>
        /// Returns the position for the given timestamp
        /// </summary>
        public float AutoUpdate(float timestamp)
        {
            if (simulation == null)
            {
                return (float)currentBounds.Position;
            }

            float localTime = timestamp - adjustedSimulationAtTime;

            if (simulation.IsDone(localTime))
            {
                simulation = default;
                UpdateBounds();
                return (float)currentBounds.Position;
            }

            ScrollBounds newBounds = scroller.GetBounds();

            if (math.any(math.isinf(newBounds.MinMax) != math.isinf(currentBounds.MinMax)))
            {
                End((float)-simulation.GetDerivAtTime(localTime), timestamp);

                localTime = 0;
            }

            return ClampPosition(simulation.GetPositionAtTime(localTime));
        }

        public float ManualUpdate(float delta, float timestamp)
        {
            MaybeLoseMomentum(delta, timestamp);

            delta = AdjustForScrollStartThreshold(delta, timestamp);

            lastNonStationaryTimestamp = timestamp;

            UpdateBounds();
            simulation = null;

            return ClampPosition(currentBounds.Position - physics.ApplyPhysicsToUserOffset(currentBounds, delta));
        }

        /// Determines whether to lose the existing incoming velocity when starting
        /// the drag.
        private void MaybeLoseMomentum(float delta, float timestamp)
        {
            if (retainMomentum && delta == 0f &&
                (timestamp == 0f || timestamp - lastNonStationaryTimestamp > MomentumRetainStationaryDurationThreshold))
            {
                // If pointer is stationary for too long, we lose momentum.
                retainMomentum = false;
            }
        }

        private float AdjustForScrollStartThreshold(float offset, float timestamp)
        {
            if (timestamp == 0)
            {
                return offset;
            }

            if (offset == 0.0f)
            {
                if (inMotion && timestamp - lastNonStationaryTimestamp > MotionStoppedDurationThreshold)
                {
                    // Enforce a new threshold.
                    inMotion = false;
                    offsetSinceLastStop = 0.0f;
                }

                // Not moving can't break threshold.
                return 0.0f;
            }
            else
            {
                if (inMotion)
                {
                    return offset;
                }

                offsetSinceLastStop = offsetSinceLastStop + offset;

                if (math.abs(offsetSinceLastStop) > motionStartDistanceThreshold)
                {
                    // Threshold broken.
                    inMotion = true;
                    offsetSinceLastStop = 0;

                    if (motionStartDistanceThreshold == 0 || math.abs(offset) > BigThresholdBreakDistance)
                    {
                        // This is heuristically a very deliberate fling. Leave the motion
                        // unaffected.
                        return offset;
                    }
                    else
                    {
                        // This is a normal speed threshold break.
                        // Ease into the motion when the threshold is initially broken
                        // to avoid a visible jump.
                        return math.min(motionStartDistanceThreshold / 3.0f, math.abs(offset)) * math.sign(offset);
                    }
                }
                else
                {
                    return 0.0f;
                }
            }
        }

        public void Start(float time, float threshold)
        {
            inMotion = false;
            motionStartDistanceThreshold = threshold;
            carriedVelocity = CarriedVelocity(time);
            retainMomentum = carriedVelocity != 0;
            offsetSinceLastStop = 0;
        }

        public void End(float currentVelocity, float timestamp, double drag = RubberBandScrollSimulation.Drag)
        {
            // We negate the velocity here because if the touch is moving downwards,
            // the scroll has to move upwards.
            double velocity = -currentVelocity;

            if (retainMomentum)
            {
                // Build momentum only if dragging in the same direction.
                bool isFlingingInSameDirection = math.sign(velocity) == math.sign(carriedVelocity);
                
                // Build momentum only if the velocity of the last drag was not
                // substantially lower than the carried momentum.
                bool isVelocityNotSubstantiallyLessThanCarriedMomentum = math.abs(velocity) > math.abs(carriedVelocity) * MomentumRetainVelocityThresholdFactor;
                
                if (isFlingingInSameDirection && isVelocityNotSubstantiallyLessThanCarriedMomentum)
                {
                    velocity += carriedVelocity!;
                }
            }

            adjustedSimulationAtTime = timestamp;
            UpdateBounds();
            simulation = physics.GetBallisticSimulation(currentBounds, velocity, drag);
        }

        public void Cancel(float timestamp)
        {
            End(0, timestamp);
        }

        public void Reset()
        {
            inMotion = false;
            retainMomentum = default;
            lastNonStationaryTimestamp = default;
            adjustedSimulationAtTime = default;
            simulation = default;
            motionStartDistanceThreshold = default;
            carriedVelocity = default;
            offsetSinceLastStop = default;
            UpdateBounds();
        }

        private float ClampPosition(double newPosition)
        {
            if (newPosition < currentBounds.Position && currentBounds.Position <= currentBounds.MinMax.x) // underscroll
            {
                newPosition = 2 * newPosition - currentBounds.Position;
            }
            if (currentBounds.MinMax.y <= currentBounds.Position && currentBounds.Position < newPosition) // overscroll
            {
                newPosition = 2 * newPosition - currentBounds.Position;
            }
            if (newPosition < currentBounds.MinMax.x && currentBounds.MinMax.x < currentBounds.Position) // hit top edge
            {
                newPosition = 2 * newPosition - currentBounds.MinMax.x;
            }
            if (currentBounds.Position < currentBounds.MinMax.y && currentBounds.MinMax.y < newPosition) // hit bottom edge
            {
                newPosition = 2 * newPosition - currentBounds.MinMax.y;
            }

            double clampPadding = ClampToBounds ? 0 : currentBounds.ViewportDimension * UnclampedBoundsMaxOutOfViewPercent;
            newPosition = Math.Clamp(newPosition, currentBounds.MinMax.x - clampPadding, currentBounds.MinMax.y + clampPadding);
            return (float)newPosition;
        }

        public ScrollBehavior(IScrollBoundsProvider scroller)
        {
            this.scroller = scroller;
        }
    }
}
