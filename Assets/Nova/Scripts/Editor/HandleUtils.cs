// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Nova.Editor.Utilities
{
    internal enum HandleManipulation { Started, Updated, Stopped, None };

    internal static class HandleUtils
    {
        private const float MinAngle = 45;
        private const float MaxAngle = 135;

        private const float LineShadowScalar = 1f;

        // The amount to scale screen space direction when convertion between
        // screen space directions and world space directions. 
        private const float GUISpaceDirScalar = 10;

        /// <summary>
        /// https://stackoverflow.com/questions/1734745/how-to-create-circle-with-b%C3%A9zier-curves
        /// 
        /// A constant to draw an approximation of a quarter circle with a bezier curve
        /// </summary>
        public const float BezierCurveCornerRadiusTangent = 4 * (Unity.Mathematics.math.SQRT2 - 1) / 3f;

        /// <summary>
        /// bounds.center + Vector3.Scale(bounds.extents, corner[i]) 
        /// is the given corner position on the given bounds
        /// </summary>
        public static readonly Vector3[] Corners3D = new Vector3[] { new Vector3(-1, -1, -1),
                                                                   new Vector3(-1, -1, 1),
                                                                   new Vector3(-1, 1, -1),
                                                                   new Vector3(-1, 1, 1),
                                                                   new Vector3(1, -1, -1),
                                                                   new Vector3(1, -1, 1),
                                                                   new Vector3(1, 1, -1),
                                                                   new Vector3(1, 1, 1) };

        /// <summary>
        /// The scalar for all 3D snappable positions on a 3D bounds
        /// </summary>
        public static readonly Vector3[] SnapPoints3D = new Vector3[] { new Vector3(-1, -1, -1),
                                                                        new Vector3(-1, -1, 1),
                                                                        new Vector3(-1, 1, -1),
                                                                        new Vector3(-1, 1, 1),
                                                                        new Vector3(1, -1, -1),
                                                                        new Vector3(1, -1, 1),
                                                                        new Vector3(1, 1, -1),
                                                                        new Vector3(1, 1, 1),
                                                                        new Vector3(0, -1, -1),
                                                                        new Vector3(0, -1, 1),
                                                                        new Vector3(0, 1, -1),
                                                                        new Vector3(0, 1, 1),
                                                                        new Vector3(1, 0, 1),
                                                                        new Vector3(1, 0, -1),
                                                                        new Vector3(-1, 0, -1),
                                                                        new Vector3(-1, 0, 1),
                                                                        new Vector3(1, 1, 0),
                                                                        new Vector3(1, -1, 0),
                                                                        new Vector3(-1, -1, 0),
                                                                        new Vector3(-1, 1, 0),
                                                                        new Vector3(1, 0, 0),
                                                                        new Vector3(-1, 0, 0),
                                                                        new Vector3(0, 1, 0),
                                                                        new Vector3(0, -1, 0),
                                                                        new Vector3(0, 0, 1),
                                                                        new Vector3(0, 0, -1),
                                                                            Vector3.zero};

        /// <summary>
        /// The scalar for all 2D snappable corners on a 2D bounds
        /// </summary>
        public static readonly Vector3[] Corners2D = new Vector3[] { new Vector2(-1, -1),
                                                                     new Vector2(-1, 1),
                                                                     new Vector2(1, 1),
                                                                     new Vector2(1, -1)};

        /// <summary>
        /// The scalar for all 2D snappable positions on a 2D bounds
        /// </summary>
        public static readonly Vector3[] SnapPoints2D = new Vector3[] { Corners2D[0],
                                                                        Corners2D[1],
                                                                        Corners2D[2],
                                                                        Corners2D[3],
                                                                        new Vector2(1, 0),
                                                                        new Vector2(-1, 0),
                                                                        new Vector2(0, 1),
                                                                        new Vector2(0, -1),
                                                                        Vector2.zero};

        /// <summary>
        /// Creates a new bounds from a min and max position
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private static Bounds MinMaxBounds(Vector3 min, Vector3 max)
        {
            Bounds toBounds = default;
            toBounds.SetMinMax(min, max);
            return toBounds;
        }

        /// <summary>
        /// Transforms the given "fromBounds" into the given "To" coordinate space
        /// </summary>
        /// <param name="fromBounds"></param>
        /// <param name="fromTo"></param>
        /// <returns></returns>
        public static Bounds TransformBounds(Bounds fromBounds, Matrix4x4 fromTo)
        {
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;

            for (int i = 0; i < Corners3D.Length; ++i)
            {
                Vector3 fromPoint = fromBounds.center + Vector3.Scale(Corners3D[i], fromBounds.extents);
                Vector3 toPoint = fromTo.MultiplyPoint(fromPoint);

                min = Vector3.Min(min, toPoint);
                max = Vector3.Max(max, toPoint);
            }

            return MinMaxBounds(min, max);
        }


        /// <summary>
        /// Transforms the given "fromRay" into the given "To" coordinate space
        /// </summary>
        /// <param name="fromBounds"></param>
        /// <param name="fromTo"></param>
        /// <returns></returns>
        public static Ray TransformRay(Ray fromRay, Matrix4x4 fromTo)
        {
            return new Ray(fromTo.MultiplyPoint(fromRay.origin), fromTo.MultiplyVector(fromRay.direction));
        }

        /// <summary>
        /// Rounds each component of the given vector to the nearest decimal point provided by "precision"
        /// </summary>
        /// <param name="v"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static Vector3 RoundToNearest(Vector3 v, Vector3 precision)
        {
            Vector3 scalar = Math.RoundWithinEpsilon(math.rcp(precision));

            v.x = Mathf.Round(v.x * scalar.x) * precision.x;
            v.y = Mathf.Round(v.y * scalar.y) * precision.y;
            v.z = Mathf.Round(v.z * scalar.z) * precision.z;

            return v;
        }

        /// <summary>
        /// Rounds each component of the given vector to the nearest decimal point provided by "precision"
        /// </summary>
        /// <param name="v"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public static Vector3 RoundToNearest(Vector3 v, float precision = 0.001f)
        {
            return RoundToNearest(v, Vector3.one * precision);
        }

        /// <summary>
        /// Gets the mouse cursor visual pointing in the direction of the given gui space vector
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="axisAdjustedDirection"></param>
        /// <param name="angleForCorners"></param>
        /// <returns></returns>
        public static MouseCursor GetCursor(Vector2 direction, out Vector2 axisAdjustedDirection, float angleForCorners = 45)
        {
            float cornerOffset = Mathf.Clamp(angleForCorners * 0.5f, 0, 45);
            float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

            if (angle < 0f)
            {
                angle = 360f + angle;
            }

            if (angle < 45 - cornerOffset)
            {
                axisAdjustedDirection = Vector2.up;
                return MouseCursor.ResizeVertical;
            }

            if (angle < 45 + cornerOffset)
            {
                axisAdjustedDirection = Vector2.one.normalized;
                return MouseCursor.ResizeUpRight;
            }

            if (angle < 135 - cornerOffset)
            {
                axisAdjustedDirection = Vector2.right;
                return MouseCursor.ResizeHorizontal;
            }

            if (angle < 135 + cornerOffset)
            {
                axisAdjustedDirection = new Vector2(1, -1).normalized;
                return MouseCursor.ResizeUpLeft;
            }

            if (angle < 225 - cornerOffset)
            {
                axisAdjustedDirection = Vector2.down;
                return MouseCursor.ResizeVertical;
            }

            if (angle < 225 + cornerOffset)
            {
                axisAdjustedDirection = new Vector2(-1, -1).normalized;
                return MouseCursor.ResizeUpRight;
            }

            if (angle < 315 - cornerOffset)
            {
                axisAdjustedDirection = Vector3.left;
                return MouseCursor.ResizeHorizontal;
            }

            if (angle < 315 + cornerOffset)
            {
                axisAdjustedDirection = new Vector2(-1, 1).normalized;
                return MouseCursor.ResizeUpLeft;
            }

            axisAdjustedDirection = Vector2.up;
            return MouseCursor.ResizeVertical;
        }

        /// <summary>
        /// Given a raw gui space direction, snaps the direction vector to 45 degree rotations based on predetermined snap thresholds
        /// </summary>
        /// <param name="rawDirection"></param>
        /// <returns></returns>
        public static Vector2 GetAxisSnappedDirection(Vector2 rawDirection, float angleForCorners = 10)
        {
            float cornerOffset = Mathf.Clamp(angleForCorners * 0.5f, 0, 45);

            float angle = Mathf.Atan2(rawDirection.x, rawDirection.y) * Mathf.Rad2Deg;

            if (angle < 0f)
            {
                angle = 360f + angle;
            }

            if (angle < 45 - cornerOffset)
            {
                return Vector2.up;
            }

            if (angle < 45 + cornerOffset)
            {
                return Vector2.one.normalized;
            }

            if (angle < 135 - cornerOffset)
            {
                return Vector2.right;
            }

            if (angle < 135 + cornerOffset)
            {
                return new Vector2(1, -1).normalized;
            }

            if (angle < 225 - cornerOffset)
            {
                return Vector2.down;
            }

            if (angle < 225 + cornerOffset)
            {
                return new Vector2(-1, -1).normalized;
            }

            if (angle < 315 - cornerOffset)
            {
                return Vector2.left;
            }

            if (angle < 315 + cornerOffset)
            {
                return new Vector2(-1, 1).normalized;
            }

            return Vector2.up;
        }

        /// <summary>
        /// Snaps a world space vector to 
        /// </summary>
        /// <param name="rawDirection"></param>
        /// <param name="angleForCorners"></param>
        /// <returns></returns>
        public static Vector3 GetAxisSnappedDirection(Vector3 rawDirection, float angleForCorners = 10)
        {
            Vector2 xy = GetAxisSnappedDirection(new Vector2(rawDirection.x, rawDirection.y), angleForCorners);
            Vector2 yz = GetAxisSnappedDirection(new Vector2(rawDirection.y, rawDirection.z), angleForCorners);
            Vector2 xz = GetAxisSnappedDirection(new Vector2(rawDirection.x, rawDirection.z), angleForCorners);

            return new Vector3(xy.x * xz.x, xy.y * yz.x, yz.y * xz.y).normalized;
        }

        /// <summary>
        /// Given a camera and a localToWorld matrix, returns the pair of axes that are closest to prependicular to the camera forward vector.
        /// 
        /// E.g. if camera is facing Vector3.forward, this will return flags: Axes.X | Axes.Y
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="localToWorld"></param>
        /// <returns></returns>
        public static PrimitiveBoundsHandle.Axes GetCameraPlaneAxes(Camera camera, Matrix4x4 localToWorld)
        {
            Vector3 axisMask = Vector3.zero;
            PrimitiveBoundsHandle.Axes cameraPlaneAxes = PrimitiveBoundsHandle.Axes.None;

            Vector3 right = localToWorld.MultiplyVector(Vector3.right);
            Vector3 up = localToWorld.MultiplyVector(Vector3.up);

            float xAngle = Vector3.Angle(camera.transform.forward, right);
            float yAngle = Vector3.Angle(camera.transform.forward, up);

            int cameraFacingAxisIndex = 2;

            if (xAngle > MinAngle && xAngle < MaxAngle)
            {
                cameraPlaneAxes |= PrimitiveBoundsHandle.Axes.X;
            }
            else
            {
                cameraFacingAxisIndex = 0;
            }

            if (yAngle > MinAngle && yAngle < MaxAngle)
            {
                cameraPlaneAxes |= PrimitiveBoundsHandle.Axes.Y;
            }
            else
            {
                cameraFacingAxisIndex = 1;
            }

            if (cameraFacingAxisIndex != 2)
            {
                axisMask[2] = 1;
                cameraPlaneAxes |= PrimitiveBoundsHandle.Axes.Z;
            }

            return cameraPlaneAxes;
        }

        /// <summary>
        /// Given an external point and line segment, returns the point on the line segment closest to the provided external point
        /// </summary>
        /// <param name="fromPoint"></param>
        /// <param name="lineStart"></param>
        /// <param name="lineEnd"></param>
        /// <returns></returns>
        public static Vector3 ClosestPointOnLineSegment(Vector3 fromPoint, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 startToEnd = lineEnd - lineStart;
            Vector3 lineDirection = startToEnd.normalized;
            Vector3 fromPointToLine = fromPoint - lineStart;

            float distanceAlongLine = Mathf.Clamp(Vector3.Dot(fromPointToLine, lineDirection), 0, startToEnd.magnitude);
            return lineStart + lineDirection * distanceAlongLine;
        }

        /// <summary>
        /// Draws an anti-aliased wire cube (will draw a rect if size.z == 0)
        /// </summary>
        /// <param name="position"></param>
        /// <param name="size"></param>
        /// <param name="wireWidth"></param>
        public static void DrawWireCube(Vector3 position, Vector3 size, float wireWidth, bool withShadow = true)
        {
            if (size.z == 0)
            {
                DrawWireRect(position, size, wireWidth, withShadow);
                return;
            }

            Vector3 extents = 0.5f * size;

            Vector3 point1 = position + Vector3.Scale(new Vector3(-1, -1, -1), extents); // bottom left front
            Vector3 point2 = position + Vector3.Scale(new Vector3(1, -1, -1), extents); // bottom right front
            Vector3 point3 = position + Vector3.Scale(new Vector3(1, -1, 1), extents); // bottom right back
            Vector3 point4 = position + Vector3.Scale(new Vector3(-1, -1, 1), extents); // bottom left back

            Vector3 point5 = position + Vector3.Scale(new Vector3(-1, 1, 1), extents); // top left back
            Vector3 point6 = position + Vector3.Scale(new Vector3(1, 1, 1), extents); // top right back
            Vector3 point7 = position + Vector3.Scale(new Vector3(1, 1, -1), extents); // top right front
            Vector3 point8 = position + Vector3.Scale(new Vector3(-1, 1, -1), extents); // top left front

            DrawPolyLine(wireWidth, withShadow, point1, point2, point3, point4, point5, point6, point7, point8, point1);

            DrawPolyLine(wireWidth, withShadow, point1, point4);
            DrawPolyLine(wireWidth, withShadow, point2, point7);
            DrawPolyLine(wireWidth, withShadow, point3, point6);
            DrawPolyLine(wireWidth, withShadow, point5, point8);
        }

        /// <summary>
        /// Draws an anti-aliased wire rectangle
        /// </summary>
        /// <param name="position"></param>
        /// <param name="size"></param>
        /// <param name="wireWidth"></param>
        public static void DrawWireRect(Vector3 position, Vector3 size, float wireWidth, bool withShadow = true)
        {
            Vector3 extents = 0.5f * size;

            Vector3 point1 = position + Vector3.Scale(new Vector3(-1, -1, 0), extents); // bottom left front
            Vector3 point2 = position + Vector3.Scale(new Vector3(1, -1, 0), extents); // bottom right front

            Vector3 point3 = position + Vector3.Scale(new Vector3(1, 1, 0), extents); // top right front
            Vector3 point4 = position + Vector3.Scale(new Vector3(-1, 1, 0), extents); // top left front

            DrawPolyLine(wireWidth, withShadow, point1, point2, point3, point4, point1);
        }

        public static void DrawPolyLine(float wireWidth, bool withShadow = true, params Vector3[] verts)
        {
            Color color = Handles.color;

            if (withShadow)
            {
                Handles.color = Color.black;

                Handles.DrawAAPolyLine(LineShadowScalar * wireWidth, verts);
            }

            Handles.color = color;

            Handles.DrawAAPolyLine(wireWidth, verts);
        }

        /// <summary>
        /// Given a world position and direction, returns the direction in gui space
        /// </summary>
        /// <param name="worldPos"></param>
        /// <param name="worldDir"></param>
        /// <returns></returns>
        public static Vector2 WorldToGUIDirection(Vector3 worldPos, Vector3 worldDir)
        {
            Vector3 screenPos = HandleUtility.WorldToGUIPoint(worldPos);
            Vector3 screenPosPlusDirection = HandleUtility.WorldToGUIPoint(worldPos + worldDir);
            Vector2 screenSpaceDir = screenPosPlusDirection - screenPos;
            // Y is flipped in gui space
            screenSpaceDir.y *= -1;

            return screenSpaceDir.normalized;
        }

        /// <summary>
        /// If a given flag is not set on the provided axes, this will 0 out the value of that axis. 
        /// </summary>
        /// <param name="v"></param>
        /// <param name="axes"></param>
        /// <returns></returns>
        public static Vector3 AxisMasked(Vector3 v, PrimitiveBoundsHandle.Axes axes)
        {
            Vector3 mask = new Vector3((axes & PrimitiveBoundsHandle.Axes.X) != 0 ? 1 : 0, (axes & PrimitiveBoundsHandle.Axes.Y) != 0 ? 1 : 0, (axes & PrimitiveBoundsHandle.Axes.Z) != 0 ? 1 : 0);

            return Vector3.Scale(v, mask);
        }

        /// <summary>
        /// Returns true if the axis of the given index is set on the axes flags
        /// </summary>
        /// <param name="axes"></param>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static bool AxisIsSet(PrimitiveBoundsHandle.Axes axes, int axis)
        {
            switch (axis)
            {
                case 0:
                    return (axes & PrimitiveBoundsHandle.Axes.X) != 0;
                case 1:
                    return (axes & PrimitiveBoundsHandle.Axes.Y) != 0;
                case 2:
                    return (axes & PrimitiveBoundsHandle.Axes.Z) != 0;
            }

            return false;
        }

        /// <summary>
        /// Given a two gui space positions, returns the direction between the points in world space
        /// </summary>
        /// <param name="screenPosStart"></param>
        /// <param name="screenPosCurrent"></param>
        /// <param name="axisAligned"></param>
        /// <returns></returns>
        public static Vector3 GUIToWorldDirection(Vector2 screenPosStart, Vector2 screenPosCurrent, bool axisAligned = false)
        {
            Vector2 screenSpaceDir = GetAxisSnappedDirection((screenPosStart - screenPosCurrent).normalized, axisAligned ? 0 : 45);

            Vector3 worldPosStart = HandleUtility.GUIPointToWorldRay(screenPosStart).origin;
            Vector3 worldPosCurrent = HandleUtility.GUIPointToWorldRay(screenPosStart + screenSpaceDir * GUISpaceDirScalar).origin;
            Vector3 worldSpaceDir = worldPosCurrent - worldPosStart;
            worldSpaceDir.y *= -1;
            return worldSpaceDir.normalized;
        }

        /// <summary>
        /// Converts the bounds to gui space and returns the center, bottom position (max.y because gui space is flipped on y) of that screen space rect
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static Vector2 LowestCenterPointInGUISpace(Bounds bounds)
        {
            Vector2 min = Vector2.one * float.MaxValue;
            Vector2 max = Vector2.one * float.MinValue;

            Vector3[] corners = bounds.size.z == 0 ? Corners2D : Corners3D;

            for (int i = 0; i < corners.Length; ++i)
            {
                Vector3 corner = corners[i];
                Vector2 point = HandleUtility.WorldToGUIPoint(bounds.center + Vector3.Scale(bounds.extents, corner));

                min = Vector2.Min(point, min);
                max = Vector2.Max(point, max);
            }

            Vector2 center = 0.5f * (min + max);
            return new Vector2(center.x, max.y);
        }

        private static Stack<int> controlStack = new Stack<int>();

        /// <summary>
        /// Begins a handle manipulation check
        /// </summary>
        public static void BeginManipulationCheck()
        {
            controlStack.Push(GUIUtility.hotControl);
        }

        /// <summary>
        /// Ends a handle manipulation check and returns the change state, if any, of the manipulation
        /// </summary>
        /// <returns></returns>
        public static HandleManipulation EndManipulationCheck()
        {
            HandleManipulation state = HandleManipulation.None;

            if (controlStack.Count == 0)
            {
                Debug.LogError("Mismatch between Begin/End handle manipulation checks.");
                return state;
            }

            int beginControl = controlStack.Pop();
            int endControl = GUIUtility.hotControl;

            if (endControl == 0 && beginControl == 0)
            {
                state = HandleManipulation.None;
            }
            else if (endControl != 0 && beginControl == 0)
            {
                state = HandleManipulation.Started;
            }
            else if (endControl == 0 && beginControl != 0)
            {
                state = HandleManipulation.Stopped;
            }
            else if (endControl == beginControl)
            {
                state = HandleManipulation.Updated;
            }

            return state;
        }

        public static void DrawWireDisc(Vector3 position, Vector3 normal, float radius, float lineWidth = 3)
        {
            Vector3 up = Vector3.Cross(Vector3.Cross(normal, Vector3.up), normal);

            Matrix4x4 rotation = Matrix4x4.TRS(position, Quaternion.LookRotation(normal, up), Vector3.one);

            using (new Handles.DrawingScope(Handles.color, Handles.matrix * rotation))
            {
                DrawRoundedCornerRectOutline(new Bounds(Vector3.zero, Vector2.one * radius * 2), radius, lineWidth);
            }
        }

        public static void DrawRoundedCornerRectOutline(Bounds bounds, float cornerRadius, float lineWidth = 3)
        {
            Vector3 extents = bounds.extents;
            cornerRadius = Mathf.Min(cornerRadius, extents.x, extents.y);

            DrawEdgeWithRadius(bounds, Vector2.left, cornerRadius, lineWidth + 1);
            DrawEdgeWithRadius(bounds, Vector2.up, cornerRadius, lineWidth + 1);
            DrawEdgeWithRadius(bounds, Vector2.down, cornerRadius, lineWidth + 1);
            DrawEdgeWithRadius(bounds, Vector2.right, cornerRadius, lineWidth + 1);

            if (Math.ApproximatelyZero(cornerRadius))
            {
                return;
            }

            DrawRoundedCornerOutline(bounds, new Vector2(-1, -1), cornerRadius, lineWidth);
            DrawRoundedCornerOutline(bounds, new Vector2(-1, 1), cornerRadius, lineWidth);
            DrawRoundedCornerOutline(bounds, new Vector2(1, -1), cornerRadius, lineWidth);
            DrawRoundedCornerOutline(bounds, new Vector2(1, 1), cornerRadius, lineWidth);
        }

        private static void DrawRoundedCornerOutline(Bounds bounds, Vector2 corner, float cornerRadius, float lineWidth)
        {
            Vector3 cornerPos = bounds.center + Vector3.Scale(bounds.extents, corner);

            Vector3 start = new Vector3(cornerPos.x - corner.x * cornerRadius, cornerPos.y, cornerPos.z);
            Vector3 end = new Vector3(cornerPos.x, cornerPos.y - corner.y * cornerRadius, cornerPos.z);

            Vector3 startTangent = start;
            startTangent.x += corner.x * cornerRadius * BezierCurveCornerRadiusTangent;

            Vector3 endTangent = end;
            endTangent.y += corner.y * cornerRadius * BezierCurveCornerRadiusTangent;

            Handles.DrawBezier(start, end, startTangent, endTangent, Handles.color, null, lineWidth);
        }

        private static void DrawEdgeWithRadius(Bounds bounds, Vector2 edge, float cornerRadius, float lineWidth)
        {
            Vector3 center = bounds.center + Vector3.Scale(bounds.extents, edge);

            Vector3 cross = Vector2.one - Vector2.Max(edge, -edge);

            Vector3 offset = Vector3.Scale(cross, bounds.extents - new Vector3(cornerRadius, cornerRadius, cornerRadius));
            Vector3 p1 = center + offset;
            Vector3 p2 = center - offset;

            Handles.DrawAAPolyLine(lineWidth, p1, p2);
        }

        public static Vector3 WorldPositionFromBoundsPixelOffset(Vector3 positionRelativeToBounds, PrimitiveBoundsHandle.Axes axes, float offsetDirection, Bounds bounds, float guiPixelOffset)
        {
            Vector3 centerToPosition = (positionRelativeToBounds - bounds.center).normalized;

            Vector3 corner = new Vector3(Math.Sign(centerToPosition.x), Math.Sign(centerToPosition.y), Math.Sign(centerToPosition.z));
            Vector3 cornerOnBounds = bounds.center + 0.5f * Vector3.Scale(corner, bounds.size);

            Vector2 positionInGUISpace = HandleUtility.WorldToGUIPoint(positionRelativeToBounds);
            Vector2 cornerInGUISpace = HandleUtility.WorldToGUIPoint(cornerOnBounds);

            float distance = Vector2.Distance(positionInGUISpace, cornerInGUISpace);

            int cameraFacingAxis = !AxisIsSet(axes, 0) ? 0 : !AxisIsSet(axes, 1) ? 1 : 2;

            Vector3 normal = Vector3.zero;
            normal[cameraFacingAxis] = 1;

            if (distance < guiPixelOffset)
            {
                Matrix4x4 worldToLocal = Handles.inverseMatrix;

                Vector2 guiDir = WorldToGUIDirection(cornerOnBounds, AxisMasked((-corner).normalized, axes));
                guiDir.y *= -1;

                Vector2 newGuiPos = cornerInGUISpace + guiDir * guiPixelOffset * offsetDirection;

                Ray handleRayWorldSpace = HandleUtility.GUIPointToWorldRay(newGuiPos);
                Ray handleRayLocalSpace = TransformRay(handleRayWorldSpace, worldToLocal);

                Plane handlePlane = new Plane(normal, cornerOnBounds);

                if (handlePlane.Raycast(handleRayLocalSpace, out float distanceToPlane))
                {
                    positionRelativeToBounds = handleRayLocalSpace.GetPoint(distanceToPlane);
                }
            }

            return positionRelativeToBounds;
        }

        /// <summary>
        /// Remaps a Vector2 into a Vector3 based on the active adjustable axes
        /// </summary>
        /// <param name="v"></param>
        /// <param name="axes"></param>
        /// <returns></returns>
        public static Vector3 Vector2ToAdjustmentAxesVector3(Vector2 v, PrimitiveBoundsHandle.Axes axes, float nonAdjustedAxisValue = 0)
        {
            return new Vector3((axes & PrimitiveBoundsHandle.Axes.X) != 0 ? v.x : nonAdjustedAxisValue,
                               (axes & PrimitiveBoundsHandle.Axes.Y) != 0 ? (axes & PrimitiveBoundsHandle.Axes.X) != 0 ? v.y : v.x : nonAdjustedAxisValue,
                               (axes & PrimitiveBoundsHandle.Axes.Z) != 0 ? v.y : nonAdjustedAxisValue);
        }

        /// <summary>
        /// Remaps a Vector3 into a Vector2 based on the active adjustable axes
        /// </summary>
        /// <param name="v"></param>
        /// <param name="axes"></param>
        /// <returns></returns>
        public static Vector2 AdjustmentAxesVector3ToVector2(Vector3 v, PrimitiveBoundsHandle.Axes axes)
        {
            return new Vector2((axes & PrimitiveBoundsHandle.Axes.X) == 0 ? v.z : v.x,
                               (axes & PrimitiveBoundsHandle.Axes.Y) == 0 ? v.z : v.y);
        }

        [System.NonSerialized]
        private static Vector3[] innerHandleVerts = new Vector3[4];
        [System.NonSerialized]
        private static Vector3[] outerHandleVerts = new Vector3[4];
        [System.NonSerialized]
        private static Vector3[] shadowHandleVerts = new Vector3[4];

        /// <summary>
        /// Draws a rectangle with a border at the given position facing the given direction
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="size"></param>
        /// <param name="faceColor"></param>
        /// <param name="outlineColor"></param>
        /// <param name="shadowColor"></param>
        public static void OutlinedRectHandleCap(Vector3 position, Quaternion rotation, float size, Color faceColor, Color outlineColor, Color shadowColor)
        {
            Matrix4x4 handleToWorld = Matrix4x4.Rotate(rotation);

            const float innerScale = 0.5f;
            const float outerScale = 0.9f;
            const float shadowScale = 1f;

            Vector3 bottomLeft = handleToWorld.MultiplyVector(new Vector3(-1, -1, 0).normalized);
            Vector3 bottomRight = handleToWorld.MultiplyVector(new Vector3(1, -1, 0).normalized);
            Vector3 topRight = handleToWorld.MultiplyVector(new Vector3(1, 1, 0).normalized);
            Vector3 topLeft = handleToWorld.MultiplyVector(new Vector3(-1, 1, 0).normalized);

            innerHandleVerts[0] = position + (innerScale * size * bottomLeft);
            innerHandleVerts[1] = position + (innerScale * size * bottomRight);
            innerHandleVerts[2] = position + (innerScale * size * topRight);
            innerHandleVerts[3] = position + (innerScale * size * topLeft);

            outerHandleVerts[0] = position + (outerScale * size * bottomLeft);
            outerHandleVerts[1] = position + (outerScale * size * bottomRight);
            outerHandleVerts[2] = position + (outerScale * size * topRight);
            outerHandleVerts[3] = position + (outerScale * size * topLeft);

            shadowHandleVerts[0] = position + (shadowScale * size * bottomLeft);
            shadowHandleVerts[1] = position + (shadowScale * size * bottomRight);
            shadowHandleVerts[2] = position + (shadowScale * size * topRight);
            shadowHandleVerts[3] = position + (shadowScale * size * topLeft);

            using (new Handles.DrawingScope(Color.white, Handles.matrix))
            {
                Handles.color = shadowColor;
                Handles.DrawAAConvexPolygon(outerHandleVerts);
                Handles.color = outlineColor;
                Handles.DrawAAConvexPolygon(outerHandleVerts);
                Handles.color = faceColor;
                Handles.DrawAAConvexPolygon(innerHandleVerts);
            }
        }
    }
}
