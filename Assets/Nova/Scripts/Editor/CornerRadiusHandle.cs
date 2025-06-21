// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using Nova.Internal.Utilities;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Nova.Editor.Tools
{
    internal class CornerRadiusHandle
    {
        private const float MinHandleGUISelectionDistance = 32f;

        public bool IsSelected { get; private set; } = false;
        public Handles.CapFunction MidpointHandleDrawFunction = null;
        public Handles.SizeFunction MidpointHandleSizeFunction = null;

        public Bounds Bounds;
        public float Radius;

        public PrimitiveBoundsHandle.Axes CameraPlaneAxes;

        public void DrawHandle()
        {
            if (!IsSelected && GUIUtility.hotControl != 0)
            {
                return;
            }

            bool drawHandles = IsSelected;

            bool twoD = Bounds.extents.z == 0;

            Vector3[] corners = HandleUtils.Corners2D;

            Vector2 guiMin = HandleUtility.WorldToGUIPoint(Bounds.min);
            Vector2 guiMax = HandleUtility.WorldToGUIPoint(Bounds.max);

            if (Math.Abs(guiMax.x - guiMin.x) < MinHandleGUISelectionDistance ||
                Math.Abs(guiMax.y - guiMin.y) < MinHandleGUISelectionDistance)
            {
                return;
            }

            for (int i = 0; i < corners.Length; ++i)
            {
                Vector3 corner = twoD ? corners[i] : HandleUtils.Vector2ToAdjustmentAxesVector3(corners[i], CameraPlaneAxes);

                Vector3 cornerOnBounds = Bounds.center + Vector3.Scale(Bounds.extents, corner);
                Vector3 positionOnRadius = cornerOnBounds - Vector3.Scale(Vector3.one * Radius, corner);
                Vector3 handlePosition = IsSelected ? positionOnRadius : HandleUtils.WorldPositionFromBoundsPixelOffset(positionOnRadius, CameraPlaneAxes, 1, Bounds, MinHandleGUISelectionDistance);

                Vector3 min = Vector3.Min(cornerOnBounds, Vector3.zero);
                Vector3 max = Vector3.Max(cornerOnBounds, Vector3.zero);

                handlePosition = Vector3.Max(Vector3.Min(handlePosition, max), min);

                float size = MidpointHandleSizeFunction != null ? MidpointHandleSizeFunction.Invoke(handlePosition) : PrimitiveBoundsHandle.DefaultMidpointHandleSizeFunction(handlePosition);

                EditorGUI.BeginChangeCheck();
                HandleUtils.BeginManipulationCheck();
                Vector3 pos = Handles.Slider(handlePosition, -corner.normalized, size, DrawFunction, EditorSnapSettings.scale);
                pos = Vector3.Max(Vector3.Min(Bounds.max, pos), Bounds.min);
                switch (HandleUtils.EndManipulationCheck())
                {
                    case HandleManipulation.Started:
                        IsSelected = true;
                        break;
                    case HandleManipulation.Stopped:
                        IsSelected = false;
                        break;
                }

                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 change = pos - cornerOnBounds;

                    Radius = Mathf.Max(Mathf.Max(Mathf.Abs(change.x), Mathf.Abs(change.y)), Mathf.Abs(change.z));
                }
            }

            Bounds twoDBounds = new Bounds(Bounds.center, HandleUtils.AdjustmentAxesVector3ToVector2(Bounds.size, CameraPlaneAxes));
            Matrix4x4 matrix = Handles.matrix;

            if ((CameraPlaneAxes & PrimitiveBoundsHandle.Axes.Z) != 0)
            {
                Quaternion rotation = GetLookRotation();
                twoDBounds.center = rotation * twoDBounds.center;
                matrix = Matrix4x4.TRS(matrix.GetColumn(3), matrix.rotation * rotation, matrix.lossyScale);
            }

            using (new Handles.DrawingScope(Handles.color, matrix))
            {
                HandleUtils.DrawRoundedCornerRectOutline(twoDBounds, Radius);
            }
        }

        private void DrawFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            MidpointHandleDrawFunction?.Invoke(controlID, position, GetLookRotation(), size, eventType);
        }

        private Quaternion GetLookRotation()
        {
            int cameraFacingAxis = (CameraPlaneAxes & PrimitiveBoundsHandle.Axes.X) == 0 ? 0 :
                                   (CameraPlaneAxes & PrimitiveBoundsHandle.Axes.Y) == 0 ? 1 :
                                   2;

            Vector3 normal = Vector3.zero;
            normal[cameraFacingAxis] = 1;

            Vector3 up = cameraFacingAxis == 0 ? Vector3.down :
                         cameraFacingAxis == 1 ? Vector3.forward :
                         Vector3.up;

            return Quaternion.LookRotation(normal, up);
        }
    }
}
