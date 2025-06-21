// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)

namespace Nova.Internal.Rendering
{
    /// <summary>
    /// A plane using normal-distance from origin representation
    /// </summary>
    internal struct NovaPlane
    {
        public float3 Normal;
        /// <summary>
        /// A point on the plane
        /// </summary>
        public float3 Point;
        public float DistanceFromOrigin;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRaycast(ref float3 rayStart, ref float3 rayDirection, out float3 hit)
        {
            float denom = math.dot(rayDirection, Normal);
            if (Math.ApproximatelyZero(denom))
            {
                hit = float3.zero;
                return false;
            }

            float numerator = math.dot(Point - rayStart, Normal);
            float distance = numerator / denom;
            if (distance < 0)
            {
                hit = float3.zero;
                return false;
            }
            hit = rayStart + distance * rayDirection;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(NovaPlane a, NovaPlane b) => !(a == b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(NovaPlane a, NovaPlane b)
        {
            if (!Math.ApproximatelyParallel(ref a.Normal, ref b.Normal))
            {
                return false;
            }

            return Math.ApproximatelyEqual(a.DistanceFromOrigin, b.DistanceFromOrigin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NovaPlane Create(ref float4x4 aFromB)
        {
            float3 normalInA = math.normalize(math.rotate(aFromB, Math.Back));
            float3 pointOnPlaneInA = math.transform(aFromB, float3.zero);
            float distanceFromOrigin = math.dot(pointOnPlaneInA, normalInA);
            return new NovaPlane()
            {
                Normal = normalInA,
                Point = pointOnPlaneInA,
                DistanceFromOrigin = distanceFromOrigin
            };
        }
    }

    internal enum ProjectedLocation : byte
    {
        Outside = 1,
        Inside = 2,
        /// <summary>
        /// Raycasting might fail if, for example, the point is parallel with another plane
        /// </summary>
        Invalid = 4,
    }

    internal struct ProjectedLocation4
    {
        public ProjectedLocation A;
        public ProjectedLocation B;
        public ProjectedLocation C;
        public ProjectedLocation D;

        private ProjectedLocation AllOR
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => A | B | C | D;
        }

        public unsafe ProjectedLocation this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                fixed (ProjectedLocation4* array = &this) { return ((ProjectedLocation*)array)[index]; }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                fixed (ProjectedLocation4* array = &this) { ((ProjectedLocation*)array)[index] = value; }
            }
        }

        public bool AnyPointInBounds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (AllOR & ProjectedLocation.Inside) != 0;
            }
        }

        public bool Encaspulated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (A & B & C & D) == ProjectedLocation.Inside;
            }
        }
    }

    /// <summary>
    /// Tracks a quadrilateral in 3-space.
    /// </summary>
    internal struct Quadrilateral3D
    {
        public float3x4 Points;
        public NovaPlane Plane;
        private float3x4 edges;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quadrilateral3D(ref float4x4 points, ref NovaPlane plane)
        {
            Points = new float3x4(
                points.c0.xyz,
                points.c1.xyz,
                points.c2.xyz,
                points.c3.xyz
                );

            Plane = plane;
            edges = new float3x4(
                math.normalize(Points.c1 - Points.c0),
                math.normalize(Points.c2 - Points.c1),
                math.normalize(Points.c3 - Points.c2),
                math.normalize(Points.c0 - Points.c3)
                );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProjectedLocation4 TryRaycastAllPoints(ref float3x4 points, out float3x4 raycastHits)
        {
            raycastHits = default;

            float3 zero = float3.zero;
            ProjectedLocation4 toRet = default;
            for (int i = 0; i < 4; ++i)
            {
                if (Plane.TryRaycast(ref zero, ref points[i], out raycastHits[i]))
                {
                    toRet[i] = GetProjectionLocation(ref raycastHits[i]);
                }
                else
                {
                    toRet[i] = ProjectedLocation.Invalid;
                }
            }
            return toRet;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProjectedLocation GetProjectionLocation(ref float3 point)
        {
            for (int i = 0; i < 4; ++i)
            {
                float3 cross = math.cross(edges[i], math.normalize(point - Points[i]));
                if (Math.ApproximatelyZero(math.lengthsq(cross)))
                {
                    // On edge, don't check dot product
                    continue;
                }

                float dot = math.dot(Plane.Normal, cross);
                if (dot <= 0)
                {
                    return ProjectedLocation.Outside;
                }
            }
            return ProjectedLocation.Inside;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ProjectOntoPlane(ref Quadrilateral3D quad, float3 point)
        {
            float3 normal = math.normalize(math.cross(quad.Points.c1 - quad.Points.c0, quad.Points.c2 - quad.Points.c0));
            float3 quadToPoint = point - quad.Points.c0;
            float distanceToPlane = math.dot(normal, quadToPoint);
            return point - distanceToPlane * normal;
        }
    }
}
