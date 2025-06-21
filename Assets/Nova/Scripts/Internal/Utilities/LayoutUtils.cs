// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Utilities
{
    internal static class LayoutUtils
    {
        /// <summary>
        /// Converts the given layout properties and offset from layout space into a transform space local position
        /// </summary>
        /// <param name="layoutOffset"></param>
        /// <param name="childLayoutSize">Inclusive of rotation (when applicable) and margin</param>
        /// <param name="childMarginOffset"></param>
        /// <param name="parentSize"></param>
        /// <param name="parentPaddingOffset"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 LayoutOffsetToLocalPosition(float3 layoutOffset, float3 childLayoutSize, float3 childMarginOffset, float3 parentSize, float3 parentPaddingOffset, float3 alignment)
        {
            float3 basis = Math.float3_Half * (parentSize - childLayoutSize) * alignment;
            float3 direction = Math.SignNonZero(-alignment);
            float3 spacingOffset = childMarginOffset + parentPaddingOffset;

            float3 basisOffset = direction * layoutOffset;
            return basis + basisOffset + spacingOffset;
        }

        /// <summary>
        /// Converts the given transform space local position into a layout space offset using the provided layout properties
        /// </summary>
        /// <param name="localPosition"></param>
        /// <param name="childLayoutSize">Inclusive of rotation (when applicable) and margin</param>
        /// <param name="childMarginOffset"></param>
        /// <param name="parentSize"></param>
        /// <param name="parentPaddingOffset"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 LocalPositionToLayoutOffset(float3 localPosition, float3 childLayoutSize, float3 childMarginOffset, float3 parentSize, float3 parentPaddingOffset, float3 alignment)
        {
            float3 basis = Math.float3_Half * (parentSize - childLayoutSize) * alignment;
            float3 direction = Math.SignNonZero(-alignment);
            float3 spacingOffset = childMarginOffset + parentPaddingOffset;

            float3 basisOffset = localPosition - basis - spacingOffset;
            return direction * basisOffset;
        }

        /// <summary>
        /// Converts the given layout properties and offset from layout space into a transform space local position
        /// </summary>
        /// <param name="localPosition"></param>
        /// <param name="childLayoutSize">Inclusive of rotation (when applicable) and margin</param>
        /// <param name="spacingOffset">child.margin.offset + parent.padding.offset</param>
        /// <param name="parentSize"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double LayoutOffsetToLocalPosition(double layoutOffset, double childLayoutSize, double parentSize, double spacingOffset, int alignment)
        {
            double direction = Math.SignNonZero(-alignment);
            double basis = 0.5f * (parentSize - childLayoutSize) * alignment;

            double basisOffset = direction * layoutOffset;
            double localPosition = basis + spacingOffset + basisOffset;

            return localPosition;
        }

        /// <summary>
        /// Converts the given transform space local position into a layout space offset using the provided layout properties
        /// </summary>
        /// <param name="localPosition"></param>
        /// <param name="childLayoutSize">Inclusive of rotation (when applicable) and margin</param>
        /// <param name="spacingOffset">child.margin.offset + parent.padding.offset</param>
        /// <param name="parentSize"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double LocalPositionToLayoutOffset(double localPosition, double childLayoutSize, double parentSize, double spacingOffset, int alignment)
        {
            double direction = Math.SignNonZero(-alignment);
            double basis = 0.5f * (parentSize - childLayoutSize) * alignment;

            double basisOffset = localPosition - spacingOffset - basis;
            double layoutOffset = direction * basisOffset;

            return layoutOffset;
        }

        /// <summary>
        /// Converts the given layout properties and offset from layout space into a transform space local position
        /// </summary>
        /// <param name="localPosition"></param>
        /// <param name="childLayoutSize">Inclusive of rotation (when applicable) and margin</param>
        /// <param name="spacingOffset">child.margin.offset + parent.padding.offset</param>
        /// <param name="parentSize"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LayoutOffsetToLocalPosition(float layoutOffset, float childLayoutSize, float parentSize, float spacingOffset, int alignment)
        {
            float direction = Math.SignNonZero(-alignment);
            float basis = 0.5f * (parentSize - childLayoutSize) * alignment;

            float basisOffset = direction * layoutOffset;
            float localPosition = basis + spacingOffset + basisOffset;

            return localPosition;
        }

        /// <summary>
        /// Converts the given transform space local position into a layout space offset using the provided layout properties
        /// </summary>
        /// <param name="localPosition"></param>
        /// <param name="childLayoutSize">Inclusive of rotation (when applicable) and margin</param>
        /// <param name="spacingOffset">child.margin.offset + parent.padding.offset</param>
        /// <param name="parentSize"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LocalPositionToLayoutOffset(float localPosition, float childLayoutSize, float parentSize, float spacingOffset, int alignment)
        {
            float direction = Math.SignNonZero(-alignment);
            float basis = 0.5f * (parentSize - childLayoutSize) * alignment;

            float basisOffset = localPosition - spacingOffset - basis;
            float layoutOffset = direction * basisOffset;

            return layoutOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAxisDirection(IUIBlock uiBlock, Vector3 direction, out int axisIndex, out int axisDirection)
        {
            axisDirection = 0;
            axisIndex = -1;

            Axis axis = uiBlock.SerializedAutoLayout.Axis;

            switch (axis)
            {
                case Axis.X:
                    axisIndex = 0;
                    break;
                case Axis.Y:
                    axisIndex = 1;
                    break;
                case Axis.Z:
                    axisIndex = 2;
                    break;
                default:
                    return false;
            }

            float maxAbs = Math.MaxComponentAbs(direction);

            if (Mathf.Abs(direction[axisIndex]) != Mathf.Abs(maxAbs))
            {
                // Not along layout axis
                return false;
            }

            axisDirection = (int)Mathf.Sign(maxAbs);

            return true;
        }

        /// <summary>
        /// Returns the signed distance between the given <paramref name="uiBlock"/> and its parent's closest edge such that
        /// if the <paramref name="uiBlock"/> was to be moved by the returned value, it would be aligned to the interior edge of its parent.
        /// </summary>
        /// <remarks>
        /// Will return 0 if the child is entirely within the parent bounds.
        /// </remarks>
        /// <param name="uiBlock">The UIBlock to check</param>
        /// <param name="axis">The axis to check</param>
        /// <param name="alignment">The UIBlock's alignment on the given axis, not read off the UIBlock directly, so calling this method doesn't mark it dirty.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMinDistanceToParentEdge(IUIBlock uiBlock, int axis, int alignment)
        {
            IUIBlock parent = uiBlock.Parent as IUIBlock;

            if (uiBlock.Parent == null)
            {
                return 0;
            }

            float size = uiBlock.LayoutSize[axis];
            float offset = uiBlock.CalculatedPosition[axis].Value;
            float marginOffset = uiBlock.CalculatedMargin.Offset[axis];

            return GetMinDistanceToAncestorEdge(size, offset, marginOffset, parent, axis, alignment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMinDistanceToAncestorEdge(float size, float offset, float marginOffset, IUIBlock ancestor, int axis, int alignment)
        {
            float paddingOffset = ancestor.CalculatedPadding.Offset[axis];
            float ancestorSize = ancestor.PaddedSize[axis];
            float ancestorExtents = ancestorSize * 0.5f;
            float ancestorMin = paddingOffset - ancestorExtents;
            float ancestorMax = paddingOffset + ancestorExtents;

            float childPos = LayoutOffsetToLocalPosition(offset, size, ancestorSize, paddingOffset + marginOffset, alignment);

            float childExtents = size * 0.5f;
            float childMin = childPos - childExtents;
            float childMax = childPos + childExtents;

            float p1 = ancestorMin - childMin;
            float p2 = ancestorMax - childMax;

            if (p1 <= 0 && p2 >= 0)
            {
                // completely within bounds
                return 0;
            }

            return Math.MinAbs(p1, p2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 RotateSize(float3 size, quaternion rotation)
        {
            bool isIdentity = math.all(rotation.value == Math.float4_QuaternionIdentity);

            if (isIdentity)
            {
                return size;
            }

            float3 extents = size * Math.float3_Half;

            float3 x = math.rotate(rotation, new float3(extents.x, 0, 0));
            float3 y = math.rotate(rotation, new float3(0, extents.y, 0));
            float3 z = math.rotate(rotation, new float3(0, 0, extents.z));

            extents = Math.Abs(x) + Math.Abs(y) + Math.Abs(z);

            return Math.float3_Two * extents;
        }

        public static string GetAlignmentLabel(int axis, int alignment)
        {
            if (alignment == 0)
            {
                return axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
            }

            if (axis == 0) // X
            {
                if (alignment < 0)
                {
                    return "Left";
                }

                return "Right";
            }

            if (axis == 1) // Y
            {
                if (alignment < 0)
                {
                    return "Bottom";
                }

                return "Top";
            }

            if (axis == 2) // Z
            {
                if (alignment < 0)
                {
                    return "Front";
                }

                return "Back";
            }

            return string.Empty;
        }
    }
}
