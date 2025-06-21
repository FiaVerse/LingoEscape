// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Utilities;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Nova.Editor.Tools
{
#if UNITY_2021_2_OR_NEWER
    [EditorTool(displayName: "UI Block Tool", componentToolTarget: typeof(UIBlock))]
#else
    [EditorTool(displayName: "UI Block Tool", targetType = typeof(UIBlock))]
#endif
    internal class UIBlockTool : BlockTool
    {
        private CornerRadiusHandle radiusHandle = new CornerRadiusHandle();

        public static UIBlockTool TypedInstance = null;

        private void OnEnable()
        {
            TypedInstance = this;
        }

        protected override void OnToolActivated()
        {
            base.OnToolActivated();

            switch (UIBlock)
            {
                case UIBlock3D colorBlock3D:
                case UIBlock2D colorBlock:
                default:
                    ColorBlock_OnToolEnabled();
                    break;
            }
        }

        protected override void DoToolGUI()
        {
            base.DoToolGUI();

            if (AdjustingMultipleObjects || !SelectionHovered)
            {
                return;
            }

            Vector3 nodeSize = UIBlock.CalculatedSize.Value;
            float minXY = .5f * Mathf.Min(nodeSize.x, nodeSize.y);

            switch (UIBlock)
            {
                case UIBlock2D colorBlock:
                {
                    ref Length cornerRadius = ref colorBlock.CornerRadius;

                    if (TryUpdateCornerRadius(colorBlock, GetValue(ref cornerRadius, ref minXY), out float radius))
                    {
                        Undo.RecordObject(colorBlock, nameof(UIBlock2D.CornerRadius));

                        SetLength(ref cornerRadius, ref radius, ref minXY);
                    }
                    break;
                }
                case UIBlock3D colorBlock3D:
                {
                    if (SizeHandles.axes == Axes2D)
                    {
                        ref Length cornerRadius = ref colorBlock3D.CornerRadius;

                        if (TryUpdateCornerRadius(colorBlock3D, GetValue(ref cornerRadius, ref minXY), out float updatedCorner))
                        {
                            Undo.RecordObject(colorBlock3D, nameof(UIBlock3D.CornerRadius));

                            SetLength(ref cornerRadius, ref updatedCorner, ref minXY);
                        }
                    }
                    else
                    {
                        float minXYZ = Mathf.Min(minXY, 0.5f * nodeSize.z);

                        ref Length edgeRadius = ref colorBlock3D.EdgeRadius;

                        if (TryUpdateEdgeRadius(colorBlock3D, GetValue(ref edgeRadius, ref minXYZ), out float updatedEdge))
                        {
                            Undo.RecordObject(colorBlock3D, nameof(UIBlock3D.EdgeRadius));

                            SetLength(ref edgeRadius, ref updatedEdge, ref minXYZ);
                        }
                    }
                    break;
                }
            }
        }

        private float GetValue(ref Length length, ref float minSizeDimension)
        {
            return length.Type == LengthType.Value ? length.Raw : minSizeDimension * length.Raw;
        }

        private void SetLength(ref Length length, ref float val, ref float minSizeDimension)
        {
            length.Raw = Length.GetRawValue(val, length, MinMax.Positive, minSizeDimension);
            RenderingPropertyChanged = true;
        }

        private void ColorBlock_OnToolEnabled()
        {
            radiusHandle.MidpointHandleDrawFunction = DrawRadiusHandles;
            radiusHandle.MidpointHandleSizeFunction = SmallHandleSizeScaledParentSpace;
        }

        protected bool TryUpdateCornerRadius(UIBlock uiBlock, float currentRadius, out float updatedRadius)
        {
            updatedRadius = currentRadius;

            Vector3 extents = 0.5f * uiBlock.CalculatedSize.Value;

            float maxRadius = Mathf.Min(extents.x, extents.y);

            Vector3 center = extents.z == 0 ? Vector3.zero : LocalToWorld.inverse.MultiplyPoint(SizeHandles.NearCameraCenterPositionWorldSpace);
            Vector3 size = extents.z == 0 ? uiBlock.CalculatedSize.Value : HandleUtils.AxisMasked(uiBlock.CalculatedSize.Value, CameraPlaneAxes);

            radiusHandle.CameraPlaneAxes = SizeHandles.axes;
            radiusHandle.Radius = currentRadius;
            radiusHandle.Bounds = new Bounds(center, size);

            using (new Handles.DrawingScope(CornerColor, LocalToWorld))
            {
                EditorGUI.BeginChangeCheck();
                radiusHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    updatedRadius = Mathf.Clamp(radiusHandle.Radius, 0, maxRadius);
                    return true;
                }
            }

            return false;
        }

        protected bool TryUpdateEdgeRadius(UIBlock uiBlock, float edgeRadius, out float updatedEdgeRadius)
        {
            updatedEdgeRadius = edgeRadius;

            Vector3 extents = 0.5f * uiBlock.CalculatedSize.Value;

            float maxRadius = Mathf.Min(extents.x, extents.y, extents.z);

            Vector3 center = extents.z == 0 ? Vector3.zero : LocalToWorld.inverse.MultiplyPoint(SizeHandles.NearCameraCenterPositionWorldSpace);
            Vector3 size = extents.z == 0 ? uiBlock.CalculatedSize.Value : HandleUtils.AxisMasked(uiBlock.CalculatedSize.Value, CameraPlaneAxes);

            radiusHandle.CameraPlaneAxes = SizeHandles.axes;
            radiusHandle.Radius = edgeRadius;
            radiusHandle.Bounds = new Bounds(center, size);

            using (new Handles.DrawingScope(EdgeColor, LocalToWorld))
            {
                EditorGUI.BeginChangeCheck();
                radiusHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    updatedEdgeRadius = Mathf.Clamp(radiusHandle.Radius, 0, maxRadius);
                    return true;
                }
            }

            return false;
        }

        private void DrawRadiusHandles(int controlId, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            if (!SelectionHovered)
            {
                return;
            }

            RegisterControlTooltip(controlId, new Tooltip()
            {
                HandleGuiPoint = HandleUtils.LowestCenterPointInGUISpace(BlockBoundsLocalSpace),
                LabelGetter = ColorBlock2D_GetRadiusTooltip
            });

            const float innerScalar = 0.6f;
            const float outerScalar = 0.8f;
            const float outerWidth = 5;

            Vector3 normal = rotation * Vector3.forward;

            float sizeScalar = SelectionToWorld.ValidTRS() ? 1 / Unity.Mathematics.math.cmin(SelectionToWorld.lossyScale) : 1;

            HandleUtils.DrawWireDisc(position, normal, outerScalar * size, outerWidth);
            Color color = Handles.color;
            Handles.color = Color.white;
            Handles.DrawSolidDisc(position, normal, innerScalar * size);
            Handles.DrawWireDisc(position, normal, innerScalar * size);

            Handles.color = Color.clear;
            Handles.CircleHandleCap(controlId, position, rotation, outerScalar * size, eventType);
            Handles.color = color;
        }

        private string ColorBlock2D_GetRadiusTooltip()
        {
            bool twoD = UIBlock is UIBlock2D;
            string label = twoD ? "Radius" : radiusHandle.CameraPlaneAxes == Axes2D ? "Corner Radius" : "Edge Radius";
            return $"{label}: {radiusHandle.Radius.ToString("F2")}";
        }
    }
}
