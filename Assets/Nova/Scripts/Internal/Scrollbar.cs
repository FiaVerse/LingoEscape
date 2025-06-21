// Copyright (c) Supernova Technologies LLC
using Nova.Internal;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    internal struct Scrollbar
    {
        private IScrollableView scrollView;
        private UIBlock viewport;
        private UIBlock scrollbar;

        private float previousPosition;

        private AutoLayout previousLayout;
        private float previousScrollableSize;
        private float previousContentSize;
        private float previousTotalContentSize;
        private float previousContentOffset;
        private float previousNormalizedPosition;

        private int previousItemCount;

        public void Init(UIBlock viewport, IScrollableView scrollView, UIBlock scrollbar)
        {
            this.viewport = viewport;
            this.scrollView = scrollView;
            this.scrollbar = scrollbar;

            previousLayout = default;
            previousScrollableSize = 0;
            previousContentSize = 0;
            previousTotalContentSize = 0;
            previousContentOffset = 0;
            previousPosition = 0;

            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            if (scrollbar == null || viewport == null)
            {
                return;
            }

            AutoLayout layout = viewport.GetAutoLayoutReadOnly();

            if (!layout.Axis.TryGetIndex(out int index))
            {
                return;
            }

            bool hasVirtualizedContent = (scrollView as MonoBehaviour) != null && scrollView.ItemCount > 0;

            float viewportSize = viewport.CalculatedSize[index].Value;
            float scrollableSize = viewport.PaddedSize[index];
            float contentSize = hasVirtualizedContent ? scrollView.ContentInViewSize[index] : viewport.ContentSize[index];
            float totalContentSize = hasVirtualizedContent ? scrollView.EstimatedTotalContentSize : contentSize;
            float contentOffset = viewport.ContentCenter[index] - viewport.CalculatedPadding.Offset[index];
            float position = hasVirtualizedContent ? -scrollView.EstimatedPosition : -contentOffset;
            float positionPercent = scrollbar.CalculatedPosition[index].Percent;
            int itemCount = hasVirtualizedContent ? scrollView.ItemCount : viewport.BlockCount;

            bool needsUpdate = !Math.ApproximatelyEqual(previousPosition, position);
            needsUpdate |= !Math.ApproximatelyEqual(previousNormalizedPosition, positionPercent);
            needsUpdate |= !Math.ApproximatelyEqual(previousContentSize, contentSize);
            needsUpdate |= !Math.ApproximatelyEqual(previousTotalContentSize, totalContentSize);
            needsUpdate |= !Math.ApproximatelyEqual(previousContentOffset, contentOffset);
            needsUpdate |= !Math.ApproximatelyEqual(previousScrollableSize, scrollableSize);
            needsUpdate |= AutoLayout.ToInternal(ref previousLayout).IsDifferent(ref AutoLayout.ToInternal(ref layout));
            needsUpdate |= !Math.ApproximatelyEqual(previousItemCount, itemCount);

            if (!needsUpdate)
            {
                return;
            }

            float sizeAdjustment = 0;
            float overscroll = 0;

            if (!hasVirtualizedContent || 
                !scrollView.HasContentInDirection(1) || 
                !scrollView.HasContentInDirection(-1))
            {
                float extent = totalContentSize * 0.5f;
                float minExtent = position - extent;
                float maxExtent = position + extent;

                overscroll = totalContentSize > scrollableSize ? maxExtent - scrollableSize * 0.5f : layout.Offset * -layout.AlignmentPositiveDirection;
                float underscroll = totalContentSize > scrollableSize ? (-scrollableSize * 0.5f) - minExtent : layout.Offset * layout.AlignmentPositiveDirection;

                sizeAdjustment = math.min(overscroll, math.min(underscroll, 0));
            }

            Length scrollbarSize = scrollbar.Size[index];

            float baseSize = math.max(totalContentSize, viewportSize);
            float scrollbarPercent = (viewportSize + sizeAdjustment) / baseSize;
            scrollbarSize.Percent = math.clamp(scrollbarPercent, 0, 1 - math.max(scrollbar.CalculatedMargin[index].Sum().Percent, 0));
            scrollbar.Size[index] = scrollbarSize;

            UIBlock parent = scrollbar.Parent;
            float scrollbarParentSize = parent == null ? scrollableSize : parent.PaddedSize[index];
            scrollbarPercent = scrollbar.SizeMinMax[index].Clamp(scrollbarSize.Percent * scrollbarParentSize) / scrollbarParentSize;

            if (!LayoutDataStore.Instance.HasReceivedFullEngineUpdate(scrollbar))
            {
                scrollbar.CalculateLayout();
            }

            float relativeSize = scrollbarPercent + math.min(scrollbar.CalculatedMargin[index].Sum().Percent, 0);

            Length scrollbarPosition = scrollbar.Position[index];

            float max = 1 - relativeSize;
            float range = 0.5f * max;

            float normalizedPosition = totalContentSize >= scrollableSize ? max * position / (totalContentSize - scrollableSize) : overscroll / scrollableSize;
            float clampedPosition = math.clamp(normalizedPosition, -range, range);

            scrollbarPosition.Percent = LayoutUtils.LocalPositionToLayoutOffset(clampedPosition, relativeSize, 1, 0, scrollbar.Alignment[index]);

            scrollbar.Position[index] = scrollbarPosition;

            previousLayout = layout;
            previousScrollableSize = scrollableSize;
            previousContentSize = contentSize;
            previousTotalContentSize = totalContentSize;
            previousContentOffset = contentOffset;
            previousPosition = position;
            previousNormalizedPosition = scrollbarPosition.Percent;
            previousItemCount = itemCount;
        }

        public bool ContentChanged
        {
            get
            {
                int itemCount = (scrollView as MonoBehaviour) == null || scrollView.ItemCount == 0 ? viewport.BlockCount : scrollView.ItemCount;


                if (itemCount != previousItemCount)
                {
                    return true;
                }

                if (!viewport.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int axis))
                {
                    return false;
                }

                if (previousContentSize != viewport.ContentSize[axis])
                {
                    return true;
                }

                if (previousContentOffset != viewport.ContentCenter[axis])
                {
                    return true;
                }

                return false;
            }
        }

    }
}
