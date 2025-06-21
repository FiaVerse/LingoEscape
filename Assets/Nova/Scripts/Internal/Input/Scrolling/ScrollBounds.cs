// Copyright (c) Supernova Technologies LLC
using Unity.Mathematics;

namespace Nova.Internal.Input.Scrolling
{
    /// <summary>
    /// Ported from Flutter. See ThirdPartyNotices.txt
    /// </summary>
    internal readonly struct ScrollBounds
    {
        public ScrollBounds(double2 minMax, double position, double viewportDimension)
        {
            MinMax = minMax;
            Position = position;
            ViewportDimension = viewportDimension;
        }

        public readonly double2 MinMax;
        public readonly double Position;
        public readonly double ViewportDimension;
        public readonly bool OutOfRange => Position < MinMax.x || Position > MinMax.y;

        public readonly void GetOverscroll(double offset, out double overscroll, out bool easing)
        {
            double overscrollPastStart = math.max(MinMax.x - Position, 0.0f);
            double overscrollPastEnd = math.max(Position - MinMax.y, 0.0f);

            overscroll = math.max(overscrollPastStart, overscrollPastEnd);
            easing = (overscrollPastStart > 0.0 && offset < 0.0) || (overscrollPastEnd > 0.0 && offset > 0.0);
        }

        public override string ToString()
        {
            return Position.ToString();
        }
    }
}
