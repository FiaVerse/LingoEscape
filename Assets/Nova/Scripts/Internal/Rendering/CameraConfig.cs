// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal static partial class CameraSorting
    {
        internal struct CameraConfig
        {
            public float4x4 CameraFromWorld;
            public float4x4 WorldFromCamera;
            public float4x4 ProjectionMatrix;
            public int CameraInstanceID;
            public CameraType CameraType;
            private bool useOrthographicSorting;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float3 GetWorldSpaceOffsetDirection(ref CameraSorting.CoplanarSetLocation setInfo)
            {
                if (useOrthographicSorting)
                {
                    return math.normalize(math.rotate(WorldFromCamera, setInfo.ViewingFromBehind ? Math.Forward : Math.Back));
                }
                else
                {
                    float3 offsetDirection = math.rotate(WorldFromCamera, setInfo.ViewingFromBehind ? setInfo.Distances.CenterPoint : -setInfo.Distances.CenterPoint);
                    return math.any(offsetDirection) ? math.normalize(offsetDirection) : offsetDirection;
                }
            }

            /// <summary>
            /// Adjusts <paramref name="setInfo"/> parameters to ensure it renders over <paramref name="dependency"/>
            /// </summary>
            /// <param name="setInfo"></param>
            /// <param name="dependency"></param>
            /// <param name="dependencyDrawCallCount"></param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EnsureRenderOver(ref CameraSorting.CoplanarSetLocation setInfo, ref CameraSorting.CoplanarSetLocation dependency, int dependencyDrawCallCount)
            {
                // Get the maximum distance that the coplanar set could have in order to ensure
                // rendering over all of the dependency's draw calls
                float adjustmentAmountPerDrawCall = Constants.InverseDrawCallDistanceAdjustmentRatio * dependency.Distances.CenterDistance;
                float maxDistance = dependency.Distances.CenterDistance - adjustmentAmountPerDrawCall * dependencyDrawCallCount;
                if (maxDistance > setInfo.Distances.CenterDistance)
                {
                    // Already further away, so don't need to do anything
                    return;
                }

                float newCenterDistance = Constants.DrawCallDistanceAdjustmentRatio * maxDistance;
                setInfo.Distances.CenterDistance = newCenterDistance;

                if (useOrthographicSorting)
                {
                    // Bump the z
                    setInfo.Distances.CenterPoint.z = newCenterDistance;
                }
                else
                {
                    // Just make the centerpoint shorter
                    setInfo.Distances.CenterPoint = newCenterDistance * math.normalize(setInfo.Distances.CenterPoint);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public CameraSorting.CoplanarSetDistances GetDistances(ref Quadrilateral3D quad)
            {
                if (useOrthographicSorting)
                {
                    return GetDistancesOrthographic(ref quad);
                }
                else
                {
                    return GetDistancesPerspective(ref quad);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private CameraSorting.CoplanarSetDistances GetDistancesOrthographic(ref Quadrilateral3D quad)
            {
                CameraSorting.CoplanarSetDistances toRet = default;
                toRet.MaxDistance = float.MinValue;
                toRet.MinDistance = float.MaxValue;

                for (int i = 0; i < 4; ++i)
                {
                    toRet.CenterPoint += quad.Points[i];
                    float dist = quad.Points[i].z;
                    toRet.MaxDistance = math.max(dist, toRet.MaxDistance);
                    toRet.MinDistance = math.min(dist, toRet.MinDistance);
                }

                toRet.CenterPoint *= Math.float3_Quarter;
                toRet.CenterDistance = toRet.CenterPoint.z;
                return toRet;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private CameraSorting.CoplanarSetDistances GetDistancesPerspective(ref Quadrilateral3D quad)
            {
                CameraSorting.CoplanarSetDistances toRet = default;

                // Get closest and furthest points
                float runningMaxDistSquared = 0f;
                float runningMinDistSquared = float.MaxValue;
                int farthestPointIndex = 0;
                int closestPointIndex = 0;
                for (int i = 0; i < 4; ++i)
                {
                    toRet.CenterPoint += quad.Points[i];
                    float distSquared = math.lengthsq(quad.Points[i]);
                    if (distSquared > runningMaxDistSquared)
                    {
                        farthestPointIndex = i;
                        runningMaxDistSquared = distSquared;
                    }

                    if (distSquared < runningMinDistSquared)
                    {
                        runningMinDistSquared = distSquared;
                        closestPointIndex = i;
                    }
                }

                toRet.CenterPoint *= Math.float3_Quarter;

                float3 cameraPosProjectedOntoQuadPlane = Quadrilateral3D.ProjectOntoPlane(ref quad, Math.float3_Zero);

                float3 minPoint;
                if (quad.GetProjectionLocation(ref cameraPosProjectedOntoQuadPlane) != ProjectedLocation.Outside)
                {
                    // If the projection of the camera lies in the quadrilateral, that is the closest point
                    minPoint = cameraPosProjectedOntoQuadPlane;
                }
                else
                {

                    // The closest point is either one of the vertices or on one of the two edges
                    // of the quadrilateral that has the closest vertex as one of the end points
                    float3 commonPoint = quad.Points[closestPointIndex];
                    float3 otherA = quad.Points[Math.Mod(closestPointIndex + 1, 4)];
                    float3 otherB = quad.Points[Math.Mod(closestPointIndex - 1, 4)];
                    if (Math.TryProjectOntoLineSegment(ref commonPoint, ref otherA, ref cameraPosProjectedOntoQuadPlane, out minPoint))
                    {
                        // Check first edge
                    }
                    else if (Math.TryProjectOntoLineSegment(ref commonPoint, ref otherB, ref cameraPosProjectedOntoQuadPlane, out minPoint))
                    {
                        // Check second edge
                    }
                    else
                    {
                        // Not on either of the edges, so the closest point is the quadrilateral vertex itself
                        minPoint = quad.Points[closestPointIndex];
                    }
                }

                toRet.MinDistance = math.length(minPoint);

                // Max will (I think) always be one of the vertices of the quadrilateral
                toRet.MaxDistance = math.length(quad.Points[farthestPointIndex]);
                toRet.CenterDistance = math.length(toRet.CenterPoint);
                return toRet;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public CameraConfig(Camera cam)
            {
                CameraFromWorld = cam.transform.worldToLocalMatrix;
                ProjectionMatrix = cam.projectionMatrix;
                // This will be initialized later in burst compiled code since it requires
                // a matrix inverse
                WorldFromCamera = default;
                CameraInstanceID = cam.GetInstanceID();
                CameraType = cam.cameraType;

                switch (cam.transparencySortMode)
                {
                    case TransparencySortMode.Default:
                        useOrthographicSorting = cam.orthographic;
                        break;
                    case TransparencySortMode.Orthographic:
                        useOrthographicSorting = true;
                        break;
                    case TransparencySortMode.Perspective:
                    default:
                        useOrthographicSorting = false;
                        break;
                }
            }
        }
    }
}
