// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Utilities
{
    internal static class Math
    {
        /// <summary>
        /// math.EPSILON is so small that sometimes things that are still pretty close to zero
        /// end up being viewed as greater than
        /// </summary>
        public const float Epsilon = .0001f;
        public const double EpsilonDBL = .0001;
        public const float Sin45 = 0.70710678118f;
        public const float OneMinusSin45 = 1f - Sin45;

        public static readonly int3 AxisIndices = new int3(0, 1, 2);
        public static readonly float3x2 Extents = new float3x2(new float3(-0.5f), new float3(0.5f));
        public static readonly float3x3 BoundsSlices = new float3x3(new float3(-0.5f), new float3(0.5f), new float3(-0.5f));

        public static readonly float3 Right = new float3(1, 0, 0);
        public static readonly float3 Up = new float3(0, 1, 0);
        public static readonly float3 Forward = new float3(0, 0, 1);
        public static readonly float3 Left = new float3(-1, 0, 0);
        public static readonly float3 Down = new float3(0, -1, 0);
        public static readonly float3 Back = new float3(0, 0, -1);

        public static readonly float4 ZeroAsPosition = new float4(0, 0, 0, 1f);
        public static readonly float4 BackAsVector = new float4(0, 0, -1f, 0);

        public static readonly int2 int2_Two = new int2(2);
        public static readonly int2 int2_One = new int2(1);
        public static readonly int2 int2_Zero = int2.zero;
        public static readonly int2 int2_NegativeOne = new int2(-1);
        public static readonly int2 int2_NegativeTwo = new int2(-2);

        public static readonly int3 int3_Two = new int3(2);
        public static readonly int3 int3_One = new int3(1);
        public static readonly int3 int3_Zero = int3.zero;
        public static readonly int3 int3_NegativeOne = new int3(-1);
        public static readonly int3 int3_NegativeTwo = new int3(-2);

        public static readonly float2 float2_NegativeOne = new float2(-1f);
        public static readonly float2 float2_Zero = float2.zero;
        public static readonly float2 float2_Half = new float2(0.5f);
        public static readonly float2 float2_One = new float2(1f);
        public static readonly float2 float2_Two = new float2(2f);
        public static readonly float2 float2_Epsilon = new float2(Epsilon);
        public static readonly float2 float2_NegativeInfinity = new float2(float.NegativeInfinity);
        public static readonly float2 float2_PositiveInfinity = new float2(float.PositiveInfinity);

        public static readonly float3 float3_Half = new float3(0.5f);
        public static readonly float3 float3_Quarter = new float3(0.25f);
        public static readonly float3 float3_NegativeHalf = new float3(-0.5f);
        public static readonly float3 float3_NegativeOne = new float3(-1);
        public static readonly float3 float3_Zero = float3.zero;
        public static readonly float3 float3_One = new float3(1);
        public static readonly float3 float3_Two = new float3(2);
        public static readonly float3 float3_PositiveInfinity = new float3(float.PositiveInfinity);
        public static readonly float3 float3_NegativeInfinity = new float3(float.NegativeInfinity);
        public static readonly float3 float3_NaN = new float3(float.NaN);
        public static readonly float3 float3_Max = new float3(float.MaxValue);
        public static readonly float3 float3_Min = new float3(float.MinValue);
        public static readonly float3 float3_Epsilon = new float3(Epsilon);

        public static readonly float4 float4_Half = new float4(0.5f);
        public static readonly float4 float4_One = new float4(1f);
        public static readonly float4 float4_Two = new float4(2f);
        public static readonly float4 float4_Epsilon = new float4(Epsilon);
        public static readonly float4 float4_255 = new float4(255f);
        public static readonly float4 float4_NAN = new float4(float.NaN);
        public static readonly float4 float4_QuaternionIdentity = new float4(0, 0, 0, 1);
        public static readonly quaternion quaterion_Indentity = quaternion.identity;

        public static readonly double3 double3_Half = new double3(0.5);
        public static readonly double3 double3_One = new double3(1);
        public static readonly double3 double3_Two = new double3(2);

        public static readonly bool2 bool2_False = new bool2(false);
        public static readonly bool3 bool3_False = new bool3(false);
        public static readonly bool3 bool3_True = new bool3(true);
        public static readonly bool4 bool4_True = new bool4(true);

        public static readonly Vector3 Vector3_NaN = new Vector3(float.NaN, float.NaN, float.NaN);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaxComponentAbs(float3 f)
        {
            return MaxAbs(MaxAbs(f.x, f.y), f.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MinComponentAbs(float3 f)
        {
            return MinAbs(MinAbs(f.x, f.y), f.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MinAbs(float a, float b)
        {
            float absA = math.select(a, -a, a < 0);
            float absB = math.select(b, -b, b < 0);

            return math.select(math.select(b, a, absA < absB), math.min(a, b), absA == absB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MaxAbs(float a, float b)
        {
            float absA = math.select(a, -a, a < 0);
            float absB = math.select(b, -b, b < 0);

            return math.select(math.select(b, a, absA > absB), math.max(a, b), absA == absB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Abs(float f)
        {
            return math.select(f, -f, f < 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Abs(double f)
        {
            return math.select(f, -f, f < 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 Abs(float2 f)
        {
            return math.select(f, -f, f < float2.zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Abs(float3 f)
        {
            return math.select(f, -f, f < float3.zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 Abs(float4 f)
        {
            return math.select(f, -f, f < float4.zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 MinComponent(ref float3x2 f)
        {
            return new float2(math.cmin(f[0]), math.cmin(f[1]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 MaxComponent(ref float3x2 f)
        {
            return new float2(math.cmax(f[0]), math.cmax(f[1]));
        }

        /// <summary>
        /// Round to nearest integer if f is math.round(f) +/- Epsilon
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 RoundWithinEpsilon(float3 f)
        {
            float3 round = math.round(f);
            return math.select(f, round, ApproximatelyEqual3(ref f, ref round));
        }

        /// <summary>
        /// Round to nearest increment
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 RoundToNearest(float2 f, float increment)
        {
            return math.round(f / increment) * increment;
        }

        /// <summary>
        /// Round to nearest increment
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 RoundToNearest(float3 f, float increment)
        {
            return math.round(f / increment) * increment;
        }

        /// <summary>
        /// Round to nearest integer if f is math.round(f) +/- Epsilon
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 RoundWithinEpsilon(float4 f)
        {
            float4 round = math.round(f);
            return math.select(f, round, ApproximatelyEqual4(ref f, ref round));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ApproximatelyZeroToZero(float3 f, float epsilon = Epsilon)
        {
            return math.select(f, 0, ApproximatelyZero3(ref f, epsilon));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproximatelyZeroToZero(float f)
        {
            return math.select(f, 0, ApproximatelyZero(f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int f, int min, int max)
        {
            return f < min ? min :
                   f > max ? max :
                   f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float f, float min, float max)
        {
            return f < min ? min :
                   f > max ? max :
                   f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double d, double min, double max)
        {
            return d < min ? min :
                   d > max ? max :
                   d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 Clamp(float2 f, float2 min, float2 max)
        {
            return new float2(Clamp(f.x, min.x, max.x), Clamp(f.y, min.y, max.y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Clamp(float3 f, float3 min, float3 max)
        {
            return new float3(Clamp(f.x, min.x, max.x), Clamp(f.y, min.y, max.y), Clamp(f.z, min.z, max.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Mask(bool3 mask)
        {
            return math.select(float3_Zero, float3_One, mask);
        }

        /// <summary>
        /// returns sign of f, but sign == 0 => 1
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignNonZero(float f)
        {
            return math.sign((2 * math.sign(f)) + 1);
        }

        /// <summary>
        /// returns sign of f, but sign == 0 => 1
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SignNonZero(float3 f)
        {
            return math.sign((float3_Two * math.sign(f)) + float3_One);
        }

        /// <summary>
        /// returns sign of f, but sign == 0 => 1
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 SignNonZero(double3 d)
        {
            return math.sign((double3_Two * math.sign(d)) + double3_One);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EqualsAccountForNaN(float3 a, float3 b)
        {
            bool3 aIsNan = IsNaN(a);
            bool3 bIsNan = IsNaN(b);

            return math.all(a == b | (aIsNan & bIsNan));
        }

        /// <summary>
        /// returns sign of f. Exists because Mathf.Sign(0) returns 1, which is unexpected.
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sign(float f)
        {
            return f > 0 ? 1 : f < 0 ? -1 : 0;
        }

        /// <summary>
        /// returns sign of f. Exists because Mathf.Sign(0) returns 1, which is unexpected.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sign(double d)
        {
            return d > 0 ? 1 : d < 0 ? -1 : 0;
        }

        /// <summary>
        /// returns the scale component of the given matrix
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Scale(ref float4x4 f)
        {
            return new float3(math.length(f.c0.xyz), math.length(f.c1.xyz), math.length(f.c2.xyz));
        }

        /// <summary>
        /// returns a 3x3 matrix such that c0 == f, c1 == f, and c2 == f
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToFloat3x3(float3 f, out float3x3 f3x3)
        {
            f3x3 = new float3x3(f, f, f);
        }

        /// <summary>
        /// returns a 2x3 matrix such that c0 == c1 == f
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToFloat3x2(float3 f, out float3x2 f3x2)
        {
            f3x2 = new float3x2(f, f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 GetRow(ref this float4x4 vals, int row)
        {
            return new float4(vals.c0[row], vals.c1[row], vals.c2[row], vals.c3[row]);
        }

        /// <summary>
        /// Translates the given matrix by the given translation and returns the total translation of the output matrix
        /// </summary>
        /// <param name="matrix"></param>
        /// <param name="translation"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 TotalTranslation(ref float4x4 matrix, float3 translation)
        {
            float4 translated = matrix.c0 * translation.x + matrix.c1 * translation.y + matrix.c2 * translation.z + matrix.c3;
            return translated.xyz;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanIntersectBounds(ref float3 rayOrigin, ref float3 rayDirection, ref float3 boundsCenter, ref float3 boundsSize)
        {
            float3 extents = boundsSize * float3_Half;
            float squareRadius = math.lengthsq(extents);

            float distanceAlongRay = math.dot(boundsCenter - rayOrigin, rayDirection);
            float3 pointOnRay = rayOrigin + (distanceAlongRay * rayDirection);

            float squareDistanceToCenter = math.lengthsq(boundsCenter - pointOnRay);
            return squareDistanceToCenter <= squareRadius;
        }

        /// <summary>
        /// https://gamedev.stackexchange.com/questions/18436/most-efficient-aabb-vs-ray-collision-algorithms
        /// http://www.cs.cornell.edu/courses/cs4620/2013fa/lectures/03raytracing1.pdf
        /// </summary>
        /// <param name="rayOrigin"></param>
        /// <param name="rcpRayDirection">Precalculated 1 / ray.direction so we can hit-test multiple bounds objects against same ray faster</param>
        /// <param name="boundsCenter"></param>
        /// <param name="boundsSize"></param>
        /// <param name="distance">If ray intersects the given bounds, this is the distance from the ray to the point of intersection</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsBounds(ref float3x2 rayOrigin, ref float3x2 rcpRayDirection, ref float3 boundsCenter, ref float3 boundsSize, out float distance)
        {
            GetRayBoundsIntersectionPoints(ref rayOrigin, ref rcpRayDirection, ref boundsCenter, ref boundsSize, out float3 entry, out float3 exit);

            float distanceToEntry = math.cmax(entry);
            float distanceToExit = math.cmin(exit);

            // failure case 1:
            // if distanceToExit < 0, then the ray might intersect the bounds,
            // but the whole bounds is in the opposite direction of the ray.
            // failure case 2:
            // if distanceToEntry > distanceToExit, ray doesn't intersect the given bounds
            bool intersectionPassed = distanceToExit >= 0 && distanceToEntry <= distanceToExit;

            // if intersectionPassed == true, but distancetoEntry < 0, then the ray origin is inside the given bounds
            bool rayOriginOutsideBounds = distanceToEntry >= 0;

            distance = math.select(distanceToExit, distanceToEntry, intersectionPassed && rayOriginOutsideBounds);

            return intersectionPassed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsBoundsPlanes(ref float3x2 rayOrigin, ref float3x2 rcpRayDirection, ref float3 boundsCenter, ref float3 boundsSize, out float distance)
        {
            GetRayBoundsIntersectionPoints(ref rayOrigin, ref rcpRayDirection, ref boundsCenter, ref boundsSize, out float3 entry, out float3 exit);

            // because in this context we only care that at least one axis is valid, we need to remove invalid values
            entry = math.select(entry, float3_Zero, math.isnan(entry) | math.isinf(entry));
            exit = math.select(exit, float3_PositiveInfinity, math.isnan(exit));

            float distanceToEntry = MaxComponentAbs(entry);
            float distanceToExit = MinComponentAbs(exit);

            distance = MinAbs(distanceToEntry, distanceToExit);

            return true;
        }

        public static bool OverlapsViewport(ref float4x4 worldToViewport, float3 minPoint, float3 maxPoint)
        {
            float3 minInViewport = WorldToViewportPoint(ref worldToViewport, minPoint);
            float3 maxInViewport = WorldToViewportPoint(ref worldToViewport, maxPoint);

            if ((minInViewport.x < 0 && maxInViewport.x < 0) || (minInViewport.x > 1 && maxInViewport.x > 1) ||
                (minInViewport.y < 0 && maxInViewport.y < 0) || (minInViewport.y > 1 && maxInViewport.y > 1) ||
                (minInViewport.z < -1 && maxInViewport.z < -1))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Given a position in world space and a worldToViewport matrix, returns the position in viewport space
        /// https://answers.unity.com/questions/1461036/worldtoviewportpoint-and-viewporttoworldpointworld.html
        /// </summary>
        /// <param name="worldToViewport"></param>
        /// <param name="world"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 WorldToViewportPoint(ref float4x4 worldToViewport, float3 world)
        {
            float4 result4 = math.mul(worldToViewport, new float4(world, 1.0f));  // multiply 4 components

            float3 result = result4.xyz;  // store 3 components of the resulting 4 components

            // normalize by "-w"
            result /= -result4.w;

            // clip space => view space
            result.x = result.x / 2 + 0.5f;
            result.y = result.y / 2 + 0.5f;

            // "The z position is in world units from the camera."
            result.z = -result4.w;

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetRayBoundsIntersectionPoints(ref float3x2 rayOrigin, ref float3x2 rcpRayDirection, ref float3 boundsCenter, ref float3 boundsSize, out float3 entry, out float3 exit)
        {
            ToFloat3x2(boundsSize, out float3x2 size);
            ToFloat3x2(boundsCenter, out float3x2 center);

            float3x2 pointRange = (size * Extents) + center;
            float3x2 intersectionPoints = (pointRange - rayOrigin) * rcpRayDirection;

            entry = math.min(intersectionPoints[0], intersectionPoints[1]);
            exit = math.max(intersectionPoints[0], intersectionPoints[1]);
        }

        /// <summary>
        /// Tests for Ray/Bounds intersection by splitting bounds in half along the given axis and returns result of intersection test for each bounds half.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2 IntersectsBoundsHalves(ref float3x3 rayOrigin, ref float3x3 oneOverRayDirection, ref float3x3 boundsCenter, ref float3x3 boundsSize, int axis)
        {
            float3x3 slices = BoundsSlices;
            slices[1][axis] = float.Epsilon;
            slices[2][axis] = 0.5f;

            float3x3 slicePoints = (slices * boundsSize) + boundsCenter;
            float3x3 intersectionPoints = (slicePoints - rayOrigin) * oneOverRayDirection;

            float3x2 rayEnterPointsOnBounds = new float3x2(math.min(intersectionPoints[0], intersectionPoints[1]), math.min(intersectionPoints[1], intersectionPoints[2]));
            float3x2 rayExitPointsOnBounds = new float3x2(math.max(intersectionPoints[0], intersectionPoints[1]), math.max(intersectionPoints[1], intersectionPoints[2]));

            float2 distanceToEntries = MaxComponent(ref rayEnterPointsOnBounds);
            float2 distanceToExits = MinComponent(ref rayExitPointsOnBounds);

            return (distanceToExits >= float2_Zero) & (distanceToEntries <= distanceToExits);
        }

        /// <summary>
        /// Given a "from" vector, a "to" vector, and a "rotation axis" vector
        /// returns the unsigned angle between from and to around the rotation axis
        /// https://forum.unity.com/threads/is-vector3-signedangle-working-as-intended.694105/
        /// </summary>
        /// <param name="to"></param>
        /// <param name="from"></param>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static float AngleBetweenAroundAxis(float3 from, float3 to, float3 axis)
        {
            float3 right = math.cross(axis, from);
            from = math.cross(right, axis);

            return Abs(math.degrees(math.atan2(math.dot(to, right), math.dot(to, from))));
        }

        /// <summary>
        /// Rounds up to the next nearest power of 2
        /// http://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CeilPowerOf2(int x)
        {
            if (x < 0) { return 0; }
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        public static int2 CeilPowerOf2(int2 x)
        {
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        /// <summary>
        /// https://zalo.github.io/blog/closest-point-between-segments/
        /// </summary>
        /// <param name="p"></param>
        /// <param name="p1"></param>
        /// <param name="q"></param>
        /// <param name="q1"></param>
        /// <param name="intersection"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryIntersectLines(ref float3 p, ref float3 p1, ref float3 q, ref float3 q1, out float3 intersection)
        {
            float3 qVec = q1 - q;
            float lineDirSqrMag = math.dot(qVec, qVec);
            float3 inPlaneA = p - ((math.dot(p - q, qVec) / lineDirSqrMag) * qVec);
            float3 inPlaneB = p1 - ((math.dot(p1 - q, qVec) / lineDirSqrMag) * qVec);

            if (math.all(ApproximatelyEqual3(ref inPlaneA, ref inPlaneB)))
            {
                // They are parallel
                intersection = float3.zero;
                return false;
            }

            float3 inPlaneBA = inPlaneB - inPlaneA;
            float t = math.dot(q - inPlaneA, inPlaneBA) / math.dot(inPlaneBA, inPlaneBA);

            if (!ApproximatelyBetween0And1(t))
            {
                intersection = float3.zero;
                return false;
            }

            float3 closestPointOnP = p + t * (p1 - p);

            float u = math.dot(closestPointOnP - q, qVec) / lineDirSqrMag;
            if (!ApproximatelyBetween0And1(u))
            {
                intersection = float3.zero;
                return false;
            }

            float3 closestPointOnQ = q + u * qVec;
            if (!math.all(ApproximatelyEqual3(ref closestPointOnP, ref closestPointOnQ)))
            {
                intersection = float3.zero;
                return false;
            }

            intersection = closestPointOnP;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryProjectOntoLineSegment(ref float3 linePoint1, ref float3 linePoint2, ref float3 point, out float3 intersection)
        {
            float3 lineVec = linePoint2 - linePoint1;
            float ratio = math.dot(point - linePoint1, lineVec) / math.dot(lineVec, lineVec);
            if (ratio < 0 || ratio > 1)
            {
                intersection = float3.zero;
                return false;
            }

            intersection = linePoint1 + ratio * lineVec;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 MinOverColumns(ref float4x4 mat)
        {
            return math.min(math.min(mat.c0, mat.c1), math.min(mat.c2, mat.c3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 MaxOverColumns(ref float4x4 mat)
        {
            return math.max(math.max(mat.c0, mat.c1), math.max(mat.c2, mat.c3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyZero(float val, float epsilon = Epsilon)
        {
            float absVal = Abs(val);
            return absVal < epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyZero(double val, double epsilon = EpsilonDBL)
        {
            double absVal = Abs(val);
            return absVal <= epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyZero(ref float2 val)
        {
            return math.all(Abs(val) < float2_Epsilon);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyZero(ref float3 val)
        {
            return math.all(Abs(val) < float3_Epsilon);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2 ApproximatelyZero2(ref float2 val)
        {
            return Abs(val) < float2_Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 ApproximatelyZero3(ref float3 val, float epsilon = Epsilon)
        {
            return Abs(val) < epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool4 ApproximatelyZero4(ref float4 val)
        {
            return Abs(val) < float4_Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyZero(ref float4 val)
        {
            return math.all(Abs(val) < float4_Epsilon);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyEqual(float a, float b)
        {
            return ApproximatelyZero(a - b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyEqual(double a, double b, double epsilon = double.Epsilon)
        {
            return ApproximatelyZero(a - b, epsilon);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyEqual(float2 a, float2 b) => ApproximatelyEqual(ref a, ref b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyEqual(ref float2 a, ref float2 b)
        {
            float2 diff = a - b;
            return ApproximatelyZero(ref diff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyEqual(ref float3 a, ref float3 b)
        {
            float3 diff = a - b;
            return ApproximatelyZero(ref diff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2 ApproximatelyEqual2(ref float2 a, ref float2 b)
        {
            float2 diff = a - b;
            return ApproximatelyZero2(ref diff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 ApproximatelyEqual3(ref float3 a, ref float3 b)
        {
            float3 diff = a - b;
            return ApproximatelyZero3(ref diff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyEqual(ref float4 a, ref float4 b)
        {
            float4 diff = a - b;
            return ApproximatelyZero(ref diff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool4 ApproximatelyEqual4(ref float4 a, ref float4 b)
        {
            float4 diff = a - b;
            return ApproximatelyZero4(ref diff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyBetween0And1(float val)
        {
            return val.ApproximatelyGreaterThan(0) && 1f.ApproximatelyGreaterThan(val) &&
                !ApproximatelyEqual(val, 0) && !ApproximatelyEqual(val, 1f);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RotationMaintainsCoplanarity(this ref quaternion quat)
        {
            float2 rotated = math.rotate(quat, Forward).xy;
            return ApproximatelyZero(ref rotated);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidAndFinite(float f)
        {
            return math.isfinite(f) && !float.IsNaN(f);
        }

        /// <summary>
        /// Assumes uniform scale
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetRoughScale(ref float4x4 m)
        {
            return math.length(m.c0.xyz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 IsNaN(float3 f)
        {
            return new bool3(float.IsNaN(f.x), float.IsNaN(f.y), float.IsNaN(f.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidAndFinite(ref float3 f)
        {
            return math.all(math.isfinite(f)) && !math.any(IsNaN(f));
        }

        /// <summary>
        /// Modulo, except handles negative numbers correctly
        /// </summary>
        /// <param name="val"></param>
        /// <param name="mod"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mod(int val, int mod)
        {
            int r = val % mod;
            return r < 0 ? r + mod : r;
        }

        /// <summary>
        /// Modulo, except handles negative numbers correctly
        /// </summary>
        /// <param name="val"></param>
        /// <param name="mod"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Mod(float val, float mod)
        {
            float r = val % mod;
            return r < 0 ? r + mod : r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool4 ApproximatelyGreaterThan(this ref float4 x, ref float4 y)
        {
            return (x - y) > -float4_Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyGreaterThan(this float x, float y)
        {
            return (x - y) > -Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2 ApproximatelyGreaterThan(ref this float2 x, ref float2 y)
        {
            return (x - y) > -float2_Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyLessThan(float x, float y)
        {
            return (y - x) > -Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2 ApproximatelyLessThan(ref this float2 x, ref float2 y)
        {
            return (y - x) > -float2_Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 ApproximatelyLessThan(ref this float3 x, ref float3 y)
        {
            return (y - x) > -float3_Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShorterThan(this ref float3 a, ref float3 b)
        {
            return math.lengthsq(a) < math.lengthsq(b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyParallel(ref float3 a, ref float3 b)
        {
            return ApproximatelyZero(math.lengthsq(math.cross(a, b)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Transform(ref float4x4 second, ref float4x4 first, float3 vec)
        {
            return math.transform(second, math.transform(first, vec));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreCoplanar(ref float4x4 xFromCommon, ref float4x4 commonFromY)
        {
            // Check position and rotations if the origins do not overlap
            float3 zeroInLhsSpace = Transform(ref xFromCommon, ref commonFromY, float3.zero);

            // Using a slightly larger epsilon here because 0.0001
            // was leading to false negatives after transforming between
            // coordinate spaces (due to floating point precision)
            if (!ApproximatelyZero(zeroInLhsSpace.z, epsilon: 0.001f))
            {
                return false;
            }

            // Check rest of rotations for if their origins overlap, but the coplanar sets are
            // rotated on x or y relative to eachother
            quaternion xFromYRotation = new quaternion(math.mul(xFromCommon, commonFromY));

            return xFromYRotation.RotationMaintainsCoplanarity();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Area(ref float2 dimensions)
        {
            return dimensions.x * dimensions.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Area(ref int2 dimensions)
        {
            return dimensions.x * dimensions.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SumLengthSq(ref float3x4 points)
        {
            return math.lengthsq(points.c0) +
                math.lengthsq(points.c1) +
                math.lengthsq(points.c2) +
                math.lengthsq(points.c3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApproximatelyIdentity(ref quaternion a)
        {
            float4 diff = a.value - quaternion.identity.value;
            return math.all(ApproximatelyZero4(ref diff));
        }
    }
}
