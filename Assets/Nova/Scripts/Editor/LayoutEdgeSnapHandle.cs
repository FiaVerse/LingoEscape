// Copyright (c) Supernova Technologies LLC
//#define DEBUG_VISUALS

using Nova.Editor.Utilities;
using Nova.Internal.Collections;
using Nova.Internal.Input;
using Nova.Internal.Utilities;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Nova.Editor.Tools
{
    /// <summary>
    /// Snaps adjusted layout properties to other layout property edges. Caller provides edge detection event.
    /// </summary>
    internal class LayoutEdgeSnapHandle : LayoutBoundsHandle
    {
        public delegate bool ClosestPointQuery(Bounds bounds, Vector3 movementAxis, Ray worldSpaceRay, out Vector3 worldSpaceSnapPoint, out UID<EdgeHitResult> hitID);

        public const float GUISpaceSnapDistance = 3f;

        protected override Bounds OnChanged(Vector3 changedDirection, Vector3 changedEdge, Bounds newBounds) => GetEdgeSnappedBounds(changedDirection, changedEdge, newBounds);

        private Bounds GetEdgeSnappedBounds(Vector3 changedDirection, Vector3 changedEdge, Bounds newBounds)
        {
            Vector3 origin = newBounds.center + Vector3.Scale(changedEdge, newBounds.extents);

            Matrix4x4 rayLocalToWorld = Handles.matrix;
            Matrix4x4 rayWorldToLocal = rayLocalToWorld.inverse;

            Ray worldSpaceRay = new Ray(rayLocalToWorld.MultiplyPoint(origin), Vector3.forward);

            UID<EdgeHitResult> hitID = UID<EdgeHitResult>.Invalid;
            Vector3 worldSpaceSnapPoint = Vector3.zero;

            worldSpaceRay.direction = rayLocalToWorld.MultiplyVector(changedDirection);

            bool foundCollision = TryGetClosestPoint != null ? TryGetClosestPoint.Invoke(newBounds, changedEdge, worldSpaceRay, out worldSpaceSnapPoint, out hitID) : false;

            Vector3 localSpaceSnapPoint = rayWorldToLocal.MultiplyPoint(worldSpaceSnapPoint);

            Vector2 guiSpaceHandlePosition = HandleUtility.WorldToGUIPoint(origin);
            Vector2 guiSpaceSnapPoint = HandleUtility.WorldToGUIPoint(localSpaceSnapPoint);
            float screenSpaceDistance = Vector2.Distance(guiSpaceSnapPoint, guiSpaceHandlePosition);

            if (foundCollision && screenSpaceDistance < GUISpaceSnapDistance)
            {
                Vector3 min = newBounds.min;
                Vector3 max = newBounds.max;

                Vector3 prevMin = min;
                Vector3 prevMax = max;

                for (int axis = 0; axis < 3; ++axis)
                {
                    if (changedEdge[axis] < 0)
                    {
                        min[axis] = localSpaceSnapPoint[axis];
                    }

                    if (changedEdge[axis] > 0)
                    {
                        max[axis] = localSpaceSnapPoint[axis];
                    }
                }

                newBounds.SetMinMax(min, max);

                if (prevMin != min || prevMax != max)
                {
                    OnSnapToHitID?.Invoke(hitID);
                }
            }

            return newBounds;
        }

        public ClosestPointQuery TryGetClosestPoint = null;
        public System.Action<UID<EdgeHitResult>> OnSnapToHitID = null;

        public LayoutEdgeSnapHandle()
        {
            midpointHandleDrawFunction = null;
        }
    }

    internal class LayoutBoundsHandle : PrimitiveBoundsHandle
    {
        public const float BoldLineWidth = 4f;
        /// <summary>
        /// This is *our* cutoff threshold for showing the rect
        /// </summary>
        public const float MouseCursorDetectionRegion = 30; 
        /// <summary>
        /// Unity 2021 changed the logic for the rect. For some reason, a rect size of 30 no longer works.
        /// </summary>
        public const float MouseCursorSwapRectHalfSize = 100;

        public System.Action<Vector3, Quaternion, float, bool> DrawHandles = null;

        private protected Vector2 MousePositionOnClick { get; private set; }
        private protected Matrix4x4 MatrixOnClick { get; private set; }

        public bool Interactive = true;
        public bool IsSelected => adjustmentActive && Interactive;
        private bool hovering = false;
        public bool Hovering => hovering && Interactive;

        private bool adjustmentActive = false;
        private Vector2 mouseMoveDirectionGuiSpace = Vector2.zero;
        private Vector3 movedCorner = Vector3.zero;
        private Vector3 closestPointToMouse = Vector3.zero;
        private Vector2 cursorDirectionGuiSpace = Vector2.up;
        private int controlID;
        public int ControlID => controlID;

        private Vector3 startingSize = Vector3.zero;
        private Vector3 startingCenter = Vector3.zero;

        private Vector3 assignedSize;
        public Vector3 size
        {
            get
            {
                Vector3 adjustableSize = GetSize();
                return new Vector3((axes & Axes.X) != 0 ? adjustableSize.x : assignedSize.x,
                                   (axes & Axes.Y) != 0 ? adjustableSize.y : assignedSize.y,
                                   (axes & Axes.Z) != 0 ? adjustableSize.z : assignedSize.z);
            }
            set
            {
                float x = Math.ValidAndFinite(value.x) ? value.x : 0;
                float y = Math.ValidAndFinite(value.y) ? value.y : 0;
                float z = Math.ValidAndFinite(value.z) ? value.z : 0;

                Vector3 newSize = new Vector3(x, y, z);

                assignedSize = newSize;

                SetSize(newSize);
            }
        }

        public Bounds BoundsToDraw;
        public Bounds? InteractiveBounds;

        public Vector3 AdjustmentHandle { get; private set; }

        public int AdjustedDirection { get; private set; }

        private bool AspectRatioLocked => Event.current.shift || DefaultToAspectLocked;

        public bool DefaultToAspectLocked { get; set; }

        private int CameraFacingAxis => (axes & Axes.X) == 0 ? 0 : (axes & Axes.Y) == 0 ? 1 : 2;

        private Vector3[] cornerVerts = new Vector3[5];

        private float offsetToCamera = 0;
        private Vector3 nearCameraCenterPositionWorldSpace = Vector3.zero;
        public Vector3 NearCameraCenterPositionWorldSpace => nearCameraCenterPositionWorldSpace;

        protected virtual Bounds OnChanged(Vector3 changedDirection, Vector3 changedEdge, Bounds newBounds) { return newBounds; }

        public new void DrawHandle()
        {
            UpdateNearCameraProperties();

            bool repainting = Event.current.type == EventType.Repaint;
            bool canManipulate = Interactive && (GUIUtility.hotControl == 0 || adjustmentActive);

            if (canManipulate && TryDrawMouseCursorVisuals() && !repainting)
            {
                UpdateHandleManipulation();
            }

            if (!adjustmentActive)
            {
                controlID = 0;
            }

            DrawHandleVisuals();
        }

        private void UpdateNearCameraProperties()
        {
            Bounds interactiveBounds = InteractiveBounds.HasValue ? InteractiveBounds.Value : BoundsToDraw;

            Matrix4x4 worldToLocal = Handles.inverseMatrix;
            Vector3 cameraPosition = worldToLocal.MultiplyPoint(Camera.current.transform.position);
            Vector3 centerToCamera = cameraPosition - center;
            offsetToCamera = center[CameraFacingAxis] + (0.5f * size[CameraFacingAxis] * Math.Sign(centerToCamera[CameraFacingAxis]));

            Vector3 boundsCenter = interactiveBounds.center;
            boundsCenter[CameraFacingAxis] = offsetToCamera;

            Vector3 boundsExtents = interactiveBounds.extents;
            boundsExtents[CameraFacingAxis] = 0;

            Vector3 normal = Vector3.zero;
            normal[CameraFacingAxis] = 1;

            Vector2 mousePosition = Event.current.mousePosition;

            Vector3[] corners = HandleUtils.Corners2D;

            cornerVerts[4] = boundsCenter + Vector3.Scale(HandleUtils.Vector2ToAdjustmentAxesVector3(corners[0], axes), boundsExtents);
            Vector2 corner1GUISpace = HandleUtility.WorldToGUIPoint(cornerVerts[4]);

            int cornerIndex = 4;
            float minDistance = float.MaxValue;

            for (int i = corners.Length - 1; i >= 0; --i)
            {
                cornerVerts[i] = boundsCenter + Vector3.Scale(HandleUtils.Vector2ToAdjustmentAxesVector3(corners[i], axes), boundsExtents);
                Vector2 corner2GUISpace = HandleUtility.WorldToGUIPoint(cornerVerts[i]);

                Vector2 pointOnLineGUI = HandleUtils.ClosestPointOnLineSegment(mousePosition, corner1GUISpace, corner2GUISpace);

                float sqDistance = (mousePosition - pointOnLineGUI).sqrMagnitude;

                if (sqDistance < minDistance)
                {
                    minDistance = sqDistance;
                    cornerIndex = i;
                }

                corner1GUISpace = corner2GUISpace;
            }

            Ray worldRay = HandleUtility.GUIPointToWorldRay(mousePosition);
            Ray localPointRay = new Ray(worldToLocal.MultiplyPoint(worldRay.origin), worldToLocal.MultiplyVector(worldRay.direction));

            Plane frontPlane = new Plane(normal, boundsCenter);

            if (frontPlane.Raycast(localPointRay, out float distanceToPlane))
            {
                Vector3 pointOnPlane = localPointRay.GetPoint(distanceToPlane);

                closestPointToMouse = HandleUtils.ClosestPointOnLineSegment(pointOnPlane, cornerVerts[cornerIndex], cornerVerts[cornerIndex + 1]);
            }
            else
            {
                closestPointToMouse = Vector3.one * float.NegativeInfinity;
            }

            nearCameraCenterPositionWorldSpace = Handles.matrix.MultiplyPoint(boundsCenter);
        }

        private bool TryDrawMouseCursorVisuals()
        {
            Vector2 guiMin = HandleUtility.WorldToGUIPoint(BoundsToDraw.min);
            Vector2 guiMax = HandleUtility.WorldToGUIPoint(BoundsToDraw.max);
            Vector2 guiSize = guiMax - guiMin;
            guiSize = Vector2.Max(-guiSize, guiSize);

            Vector2 mousePosition = Event.current.mousePosition;

            float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(closestPointToMouse), mousePosition);

            hovering = distance <= 0.5f * MouseCursorDetectionRegion;

            if (!hovering && !adjustmentActive)
            {
                return false;
            }

            Rect mouseScreenRect = new Rect(mousePosition - (Vector2.one * MouseCursorSwapRectHalfSize), Vector2.one * MouseCursorSwapRectHalfSize * 2);

            if (!adjustmentActive)
            {
                Vector2 guiBoundsCenter = HandleUtility.WorldToGUIPoint(BoundsToDraw.center);

                if (Vector2.Distance(guiBoundsCenter, mousePosition) < distance) 
                {
                    return false;
                }

                Vector3 centerOnBounds = center;
                centerOnBounds[CameraFacingAxis] = offsetToCamera;

                Vector3 movedDirection = (closestPointToMouse - centerOnBounds);

                movedDirection = new Vector3(assignedSize.x == 0 ? 0 : movedDirection.x / assignedSize.x,
                                             assignedSize.y == 0 ? 0 : movedDirection.y / assignedSize.y,
                                             assignedSize.z == 0 ? 0 : movedDirection.z / assignedSize.z);

                Vector3 sign = new Vector3(Math.Sign(movedDirection.x), Math.Sign(movedDirection.y), Math.Sign(movedDirection.z));

                movedDirection = Vector3.Scale(HandleUtils.GetAxisSnappedDirection(movedDirection), sign);

                movedCorner = HandleUtils.AxisMasked(movedDirection, axes);
                mouseMoveDirectionGuiSpace = HandleUtils.WorldToGUIDirection(centerOnBounds, movedCorner.normalized);
            }

            controlID = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.AddCursorRect(mouseScreenRect, HandleUtils.GetCursor(mouseMoveDirectionGuiSpace, out cursorDirectionGuiSpace), controlID);

            Vector3 handle = new Vector3(Math.Sign(movedCorner.x), Math.Sign(movedCorner.y), Math.Sign(movedCorner.z));
            handle[CameraFacingAxis] = Math.Sign(offsetToCamera - center[CameraFacingAxis]);
            AdjustmentHandle = handle;

            return true;
        }

        private void UpdateHandleManipulation()
        {
            HandleUtils.BeginManipulationCheck();

            EditorGUI.BeginChangeCheck();

            Vector3 movedPoint = Handles.Slider(controlID, closestPointToMouse, HandleUtils.Vector2ToAdjustmentAxesVector3(cursorDirectionGuiSpace, axes), 0, Handles.ArrowHandleCap, 0);

            bool changed = EditorGUI.EndChangeCheck();

            switch (HandleUtils.EndManipulationCheck())
            {
                case HandleManipulation.Started:
                    adjustmentActive = true;

                    startingSize = size;
                    startingCenter = center;

                    MousePositionOnClick = Event.current.mousePosition;
                    MatrixOnClick = Handles.matrix;
                    break;
                case HandleManipulation.Stopped:
                    adjustmentActive = false;
                    break;
            }

            if (!changed)
            {
                return;
            }

            Vector3 mouseMoveDirectionWorldSpace = HandleUtils.GetAxisSnappedDirection(HandleUtils.GUIToWorldDirection(MousePositionOnClick, MousePositionOnClick + cursorDirectionGuiSpace));
            Vector3 mouseMoveDirectionLocalSpace = HandleUtils.AxisMasked(Handles.matrix.inverse.MultiplyVector(mouseMoveDirectionWorldSpace), axes).normalized;

            Bounds boundsOnClick = new Bounds(startingCenter, startingSize);
            Vector3 startPosition = startingCenter + Vector3.Scale(startingSize * 0.5f, AdjustmentHandle);

            Vector3 translation = Vector3.zero;

            if (AspectRatioLocked)
            {
                Vector3 scalar = HandleUtils.AxisMasked(Vector3.one, axes).normalized;

                translation = scalar * HandleUtility.CalcLineTranslation(MousePositionOnClick,
                                                                         Event.current.mousePosition,
                                                                         startPosition,
                                                                         AdjustmentHandle);
            }
            else
            {
                for (int i = 0; i < 3; ++i)
                {
                    if (AdjustmentHandle[i] == 0 || i == CameraFacingAxis)
                    {
                        continue;
                    }

                    Vector3 dir = Vector3.zero;
                    dir[i] = AdjustmentHandle[i];

                    translation[i] = HandleUtility.CalcLineTranslation(MousePositionOnClick,
                                                                       Event.current.mousePosition,
                                                                       startPosition,
                                                                       dir);
                }
            }

            if (translation == Vector3.zero)
            {
                return;
            }

            Vector3 adjustedPoint = startPosition + Vector3.Scale(translation, AdjustmentHandle);
            Vector3 adjustedMin = boundsOnClick.min;
            Vector3 adjustedMax = boundsOnClick.max;

            for (int i = 0; i < 3; ++i)
            {
                if (AdjustmentHandle[i] < 0)
                {
                    adjustedMin[i] = Handles.SnapValue(adjustedPoint[i], EditorSnapSettings.move[i]);
                }

                if (AdjustmentHandle[i] > 0)
                {
                    adjustedMax[i] = Handles.SnapValue(adjustedPoint[i], EditorSnapSettings.move[i]);
                }
            }

            Bounds adjustedBounds = default(Bounds);

            adjustedBounds.SetMinMax(adjustedMin, adjustedMax);

            Vector3 adjustedSize = adjustedBounds.size;
            Vector3 adjustedCenter = adjustedBounds.center;

            if (adjustedSize == size)
            {
                return;
            }

            // if shift is pressed, constrain aspect ratio
            if (AspectRatioLocked)
            {
                MaintainAspectRatio(startingSize, startingCenter, AdjustmentHandle, ref adjustedSize, ref adjustedCenter);

                adjustedBounds = new Bounds(adjustedCenter, adjustedSize);
            }

            size = adjustedBounds.size;
            center = adjustedBounds.center;
        }

        public bool TryGetEdgeSnappedBounds(Bounds unsnappedBounds, out Bounds snappedBounds)
        {
            Vector3 mouseMoveDirectionWorldSpace = HandleUtils.GetAxisSnappedDirection(HandleUtils.GUIToWorldDirection(MousePositionOnClick, MousePositionOnClick + cursorDirectionGuiSpace));
            Vector3 mouseMoveDirectionLocalSpace = HandleUtils.AxisMasked(Handles.matrix.inverse.MultiplyVector(mouseMoveDirectionWorldSpace), axes).normalized;

            Vector2 snappedMouseDirection = HandleUtils.GetAxisSnappedDirection(mouseMoveDirectionGuiSpace);

            Vector3 min = unsnappedBounds.min;
            Vector3 max = unsnappedBounds.max;

            bool axisAligned = snappedMouseDirection == Vector2.left || snappedMouseDirection == Vector2.right || snappedMouseDirection == Vector2.up || snappedMouseDirection == Vector2.down;

            if (axisAligned)
            {
                Bounds newBounds = OnChanged(mouseMoveDirectionLocalSpace, AdjustmentHandle, unsnappedBounds);
                min = newBounds.min;
                max = newBounds.max;
            }
            else
            {
                for (int i = 0; i < 3; ++i)
                {
                    if (mouseMoveDirectionLocalSpace[i] == 0)
                    {
                        continue;
                    }

                    Vector3 localDirection = Vector3.zero;
                    localDirection[i] = Math.Sign(mouseMoveDirectionLocalSpace[i]);

                    Bounds newBounds = OnChanged(localDirection, AdjustmentHandle, unsnappedBounds);

                    min[i] = newBounds.min[i];
                    max[i] = newBounds.max[i];
                }
            }

            unsnappedBounds.SetMinMax(min, max);

            Vector3 adjustedCenter = Math.RoundWithinEpsilon(unsnappedBounds.center);
            Vector3 adjustedSize = Math.RoundWithinEpsilon(unsnappedBounds.size);

            // if shift is pressed, constrain aspect ratio
            if (AspectRatioLocked)
            {
                MaintainAspectRatio(startingSize, startingCenter, AdjustmentHandle, ref adjustedSize, ref adjustedCenter);
            }

            // only update center if size changed
            Unity.Mathematics.float3 adjusted = adjustedSize;
            Unity.Mathematics.float3 current = size;

            Unity.Mathematics.bool3 changed = adjusted == current;

            snappedBounds = new Bounds(Unity.Mathematics.math.select(center, adjustedCenter, changed), adjustedSize);

            return Unity.Mathematics.math.any(changed);
        }

        private static void MaintainAspectRatio(Vector3 startingSize, Vector3 startingCenter, Vector3 adjustmentDirection, ref Vector3 adjustedSize, ref Vector3 adjustedCenter)
        {
            Vector3 change = adjustedSize - startingSize;
            Vector3 relativeChange = new Vector3(startingSize.x == 0 ? 0 : change.x / startingSize.x,
                                                 startingSize.y == 0 ? 0 : change.y / startingSize.y,
                                                 startingSize.z == 0 ? 0 : change.z / startingSize.z);

            float scalar = Math.MaxAbs(Math.MaxAbs(relativeChange.x, relativeChange.y), relativeChange.z);

            adjustedSize = startingSize + (startingSize * scalar);
            adjustedCenter = startingCenter + Vector3.Scale(adjustedSize - startingSize, adjustmentDirection) * 0.5f;
        }

        private static Vector3 CrossAdjustmentAxes(Axes axes)
        {
            Vector3 dir1 = (axes & Axes.X) != 0 ? Vector3.right : Vector3.up;
            Vector3 dir2 = (axes & Axes.X) != 0 && (axes & Axes.Y) != 0 ? Vector3.up : Vector3.forward;

            return Vector3.Cross(dir1, dir2);
        }

        protected void DrawHandleVisuals()
        {
            bool repainting = Event.current.type == EventType.Repaint;

            if (!repainting)
            {
                return;
            }

            HandleUtils.DrawWireCube(BoundsToDraw.center, BoundsToDraw.size, BoldLineWidth);

            Vector3[] handlePoints = HandleUtils.Corners2D;

            Vector3 handleForward = CrossAdjustmentAxes(axes);

            float nonAdjustedAxisDir = offsetToCamera - center[CameraFacingAxis];

            int control = adjustmentActive ? controlID : 0;

            for (int i = 0; i < handlePoints.Length; ++i)
            {
                Vector3 handle = HandleUtils.Vector2ToAdjustmentAxesVector3(handlePoints[i], axes, Math.Sign(nonAdjustedAxisDir));
                Vector3 point = center + Vector3.Scale(0.5f * size, handle);
                float handleSize = midpointHandleSizeFunction != null ? midpointHandleSizeFunction.Invoke(point) : DefaultMidpointHandleSizeFunction(point);

                DrawHandles?.Invoke(point, Quaternion.LookRotation(handleForward), handleSize, handle == AdjustmentHandle && adjustmentActive);
            }
        }

        protected override void DrawWireframe() { }
    }
}
