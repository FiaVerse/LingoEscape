// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal;
using Nova.Internal.Collections;
using Nova.Internal.Core;
using Nova.Internal.DataBinding;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    internal interface IScrollableView
    {
        int ItemCount { get; }

        /// <summary>
        /// The estimated size of content along the <see cref="UIBlock"/>'s <see cref="AutoLayout"/>.<see cref="AutoLayout.Axis">Axis</see> if all virtualized list items were actually loaded into view.
        /// </summary>
        /// 
        /// <remarks>
        /// The more variation in size of list items along the <see cref="UIBlock"/>'s <see cref="AutoLayout"/>.<see cref="AutoLayout.Axis">Axis</see>, the less accurate this value will be.
        /// Meaning if all list items are uniform in size,<br/> this value can be very accurate. If the list item sizes vary greatly, this value will be less accurate but will attempt to adjust as content is scrolled
        /// </remarks>
        float EstimatedTotalContentSize { get; }
        float EstimatedScrollableRange { get; }

        /// <summary>
        /// Estimated virtualized position in transform space
        /// </summary>
        float EstimatedPosition { get; }
        /// <summary>
        /// Offset in Layout.Alignment space
        /// </summary>
        float EstimatedOffset { get; }
        Vector3 ContentInViewSize { get; }
        Vector3 ContentInViewPosition { get; }
        int ItemsPerRow { get; }
        Vector3 ScrollRange { get; }
        Vector3 ClipSize { get; }
        float OutOfViewDistance { get; }

        void Scroll(float delta);
        void JumpToIndex(int index);
        float JumpToIndexPage(int index);
        void FinalizeItemsForView();
        IUIBlockBase TryPageInItems(bool nextItem, float emptySpace);
        bool TryPageOutItem(bool fromFront);
        bool HasContentInDirection(float direction);
        bool TryGetItemAtIndex(int index, out IUIBlock item);
        bool TryGetIndexOfItem(IUIBlock item, out int index);
        bool TryGetPrimaryAxisItemFromSourceIndex(int index, out IUIBlock item);

        ReadOnlyList<DataStoreID> ChildIDs { get; }
    }

    internal class VirtualizedView : System.IDisposable
    {
        private IScrollableView view = null;
        private UIBlock root = null;

        private bool initialized = false;
        private bool scrolling = false;

        private CheckInView inView;
        private unsafe static BurstedMethod<BurstMethod> checkInView;
        private NovaHashMap<DataStoreID, ViewItem> viewItems;

        private HashSet<DataStoreID> inViewElements = new HashSet<DataStoreID>();
        private HashSet<DataStoreID> processedIDs = new HashSet<DataStoreID>();

        public Vector3 ScrolledContentSize => (float3)activeScrollMetrics.ContentSize;
        public Vector3 ScrolledContentPosition { get; private set; }

        private ActiveScrollMetrics activeScrollMetrics = default;

        public void SyncToRoot()
        {
            _ = TryCheckInView(0, ref activeScrollMetrics);
        }

        public void Scroll(float scrollDelta)
        {
            if (!TryCheckInView(scrollDelta, ref activeScrollMetrics))
            {
                return;
            }

            scrolling = true;

            root.AutoLayout.Offset = GetScrolledPosition(ref activeScrollMetrics);

            scrolling = false;
        }

        private bool TryCheckInView(float scrollDelta, ref ActiveScrollMetrics scroll)
        {
            if (!initialized)
            {
                Debug.LogError("Attempting to scroll before processor is initialized.");
                return false;
            }

            AutoLayout layout = root.GetAutoLayoutReadOnly();

            if (!layout.Axis.TryGetIndex(out int scrollAxis))
            {
                return false;
            }

            float contentSize = root.ContentSize[scrollAxis];
            float position = root.ContentCenter[scrollAxis];

            float sizeChange = root.ContentSize[scrollAxis] - (float)activeScrollMetrics.ContentSize[scrollAxis];
            float positionChange = root.ContentCenter[scrollAxis] - ScrolledContentPosition[scrollAxis];

            int scrollDirection = (int)math.sign(scrollDelta);

            if (scrollDirection == 0)
            {
                float viewportSize = root.CalculatedSize.Value[scrollAxis];
                if (contentSize <= viewportSize)
                {
                    scrollDirection = position == 0 ? -1 : (int)math.sign(position);
                }
                else 
                {
                    scrollDirection = (int)math.sign(Math.ApproximatelyZeroToZero(positionChange - 0.5f * sizeChange));
                }
            }

            scroll = new ActiveScrollMetrics(ref layout, root.ContentSize, scrollDelta, root.CalculatedSpacing.Value, scrollDirection, scrollAxis);
            ScrolledContentPosition = root.ContentCenter;

            if (scrolling || scrollDirection == 0)
            {
                return false;
            }

            inView.OutOfViewDistance = view.OutOfViewDistance;
            inView.ScrollAmount = scrollDelta;
            inView.ScrollAxis = scrollAxis;
            inView.ScrollDirection = scrollDirection;
            inView.Alignment = layout.Alignment;

            unsafe
            {
                checkInView.Method.Invoke(UnsafeUtility.AddressOf(ref inView));
            }

            return true;
        }

        private float GetScrolledPosition(ref ActiveScrollMetrics scroll)
        {
            ProcessItems(ref scroll);

            return GetNewPosition(ref scroll);
        }

        private float GetNewPosition(ref ActiveScrollMetrics scroll)
        {
            view.FinalizeItemsForView();

            double contentSize = scroll.ContentSize[scroll.ScrollAxis];
            double contentLayoutOffset = scroll.Offset;
            double viewportSize = view.ScrollRange[scroll.ScrollAxis];
            double paddingOffset = root.CalculatedPadding.Offset[scroll.ScrollAxis];
            int alignment = root.GetAutoLayoutReadOnly().Alignment;

            double newPosition = LayoutUtils.LayoutOffsetToLocalPosition(contentLayoutOffset, contentSize, viewportSize, paddingOffset, alignment);

            Vector3 scrolledContentPosition = Vector3.zero;
            scrolledContentPosition[scroll.ScrollAxis] = (float)newPosition;

            ScrolledContentPosition = scrolledContentPosition;

            return (float)LayoutUtils.LocalPositionToLayoutOffset(newPosition, contentSize, viewportSize, paddingOffset, alignment);
        }

        private bool HandleItemScrolledOutOfView(ref ActiveScrollMetrics scrollInfo, Vector3 childSize)
        {
            if (!view.TryPageOutItem(fromFront: scrollInfo.PagingInHigherOrderElements))
            {
                return false;
            }

            scrollInfo.UpdatePositionAndContentSize(-childSize);

            return true;
        }

        private void ProcessItems(ref ActiveScrollMetrics scroll)
        {
            int processedActiveCount = 0;

            processedIDs.Clear();
            ReadOnlyList<DataStoreID> readOnlyNodeIDs = view.ChildIDs;
            int viewCount = readOnlyNodeIDs.Count;

            int start = scroll.PagingInHigherOrderElements ? 0 : readOnlyNodeIDs.Count - 1;
            for (int i = start; i >= 0 && i < readOnlyNodeIDs.Count; i += scroll.NextElementIncrement)
            {
                DataStoreID childID = readOnlyNodeIDs[i];

                if (!processedIDs.Add(childID))
                {
                    // means this item has been pooled and recycled while iterating through this loop,
                    // so we just want to skip it this time around and continue processing unseen elements
                    continue;
                }

                if (!viewItems.TryGetValue(childID, out ViewItem child))
                {
                    // element is disabled, stop tracking.
                    inViewElements.Remove(childID);
                    continue;
                }

                processedActiveCount++;

                switch (child.State)
                {
                    case ViewState.OutOfView:
                        if (view.HasContentInDirection(scroll.ScrollDirection > 0 ? -1 : 1))
                        {
                            inViewElements.Remove(childID);
                            HandleItemScrolledOutOfView(ref scroll, child.Size);
                        }
                        break;
                    case ViewState.Partial:
                        if (view.HasContentInDirection(scroll.ScrollDirection > 0 ? -1 : 1))
                        {
                            inViewElements.Remove(childID);
                        }
                        break;
                    case ViewState.InView:
                        bool bypassSizeCheck = processedActiveCount == viewCount;

                        if (inViewElements.Add(childID) || bypassSizeCheck)
                        {
                            HandleItemScrolledIntoView(ref scroll, bypassSizeCheck: bypassSizeCheck);
                        }
                        break;
                }
            }

            // if we're scrolling really fast, and a lot of content has been
            // paged out, try paging in until we fill the viewport
            while (HandleItemScrolledIntoView(ref scroll, bypassSizeCheck: false)) ;

            if (scroll.ScrollDelta == 0)
            {
                // flip and fill content on the other side if applicable
                scroll.FlipDirection();
                while (HandleItemScrolledIntoView(ref scroll, bypassSizeCheck: false)) ;
            }
        }

        private bool HandleItemScrolledIntoView(ref ActiveScrollMetrics scrollInfo, bool bypassSizeCheck)
        {
            if (!scrollInfo.ViewportHasEmptySpace(view.ClipSize, out double emptySpace) && !bypassSizeCheck)
            {
                return false;
            }

            IUIBlockBase addedBlock = view.TryPageInItems(nextItem: scrollInfo.PagingInHigherOrderElements, (float)emptySpace);

            if (addedBlock == null)
            {
                return false;
            }

            scrollInfo.UpdatePositionAndContentSize(addedBlock.LayoutSize);

            return true;
        }

        public void ItemRemovedFromView(IUIBlockBase removedPrefab)
        {
            if (scrolling)
            {
                return;
            }

            IHierarchyBlock parent = HierarchyDataStore.Instance.GetHierarchyParent(removedPrefab.UniqueID);

            // if the added prefab is parented to a virtual block (in a grid view), we don't want to track it
            if (parent == null || parent.IsVirtual)
            {
                return;
            }

            ItemRemovedFromView(removedPrefab.UniqueID);
        }

        public void ItemRemovedFromView(DataStoreID dataStoreID)
        {
            inViewElements.Remove(dataStoreID);
        }

        public void ItemAddedToView(DataStoreID id)
        {
            if (scrolling)
            {
                return;
            }

            IHierarchyBlock parent = HierarchyDataStore.Instance.GetHierarchyParent(id);

            // if the added prefab is parented to a virtual block (in a grid view), we don't want to track it
            if (parent == null || parent.IsVirtual)
            {
                return;
            }

            inViewElements.Add(id);
        }

        public void Init(IScrollableView view, UIBlock root, DataStoreID rootID)
        {
            if (initialized)
            {
                return;
            }

            this.view = view;
            this.root = root;

            viewItems = new NovaHashMap<DataStoreID, ViewItem>(16, Allocator.Persistent);

            HierarchyDataStore hierarchy = HierarchyDataStore.Instance;
            LayoutDataStore layouts = LayoutDataStore.Instance;

            inView = new CheckInView()
            {
                ViewItems = viewItems,

                Lengths = layouts.CalculatedLengths,

                TransformRotations = layouts.TransformLocalRotations,
                UseRotations = layouts.UseRotations,

                Hierarchy = hierarchy.ReadOnlyHierarchy,

                ParentID = rootID,
            };

            if (checkInView.Method == null)
            {
                unsafe
                {
                    checkInView = new BurstedMethod<BurstMethod>(CheckInView.Run);
                }
            }

            initialized = true;
        }

        public void Dispose()
        {
            if (!initialized)
            {
                return;
            }

            view = null;

            viewItems.Dispose();

            inViewElements.Clear();
            processedIDs.Clear();

            initialized = false;
        }

        private struct ActiveScrollMetrics
        {
            public double Offset;
            public double3 ContentSize;

            public readonly int ScrollAxis;
            public readonly int ScrollAxisDirection;
            public readonly double SpaceBetweenElements;
            public readonly int Alignment;

            public double ScrollDelta;
            public int NextElementIncrement;
            public int ScrollDirection;

            /// <summary>
            /// Indicates whether the content in view is being shifted towards the point of alignment 
            ///
            /// E.g. 
            /// Alignment == Left
            /// if this true, the content moving to the left
            /// if this is false, the content is moving to the right
            /// </summary>
            public bool ContentMovingTowardsAlignment;

            /// <summary>
            /// Indicates that moving content in the scrolled direction will pull higher order elements into view
            /// </summary>
            public bool PagingInHigherOrderElements;

            public void UpdatePositionAndContentSize(Vector3 childSize)
            {
                float addedSize = childSize[ScrollAxis];
                float signAddedSize = Math.Sign(addedSize);

                UpdatePositionAndContentSize(addedSize + signAddedSize * (float)SpaceBetweenElements, signAddedSize);
            }

            public bool ViewportHasEmptySpace(Vector3 viewportSize, out double emptySpace)
            {
                float viewport = viewportSize[ScrollAxis];
                float content = (float)ContentSize[ScrollAxis];

                float position = LayoutUtils.LayoutOffsetToLocalPosition((float)Offset, content, viewport, 0, Alignment);

                if (ScrollDirection < 0)
                {
                    float viewportMax = viewport * 0.5f;
                    float contentMax = position + content * 0.5f;
                    emptySpace = viewportMax - contentMax;
                }
                else
                {
                    float viewportMin = viewport * -0.5f;
                    float contentMin = position - content * 0.5f;
                    emptySpace = contentMin - viewportMin;
                }

                return Math.Sign(emptySpace) > 0;
            }

            private void UpdatePositionAndContentSize(float totalSizeChange, float signAddedSize)
            {
                ContentSize[ScrollAxis] += totalSizeChange;

                if (Alignment == 0)
                {
                    Offset -= totalSizeChange * signAddedSize * 0.5f * ScrollDirection;
                }
                else if ((signAddedSize < 0 && ContentMovingTowardsAlignment) ||
                         (signAddedSize > 0 && !ContentMovingTowardsAlignment))
                {
                    Offset -= totalSizeChange;
                }
            }

            public ActiveScrollMetrics(ref AutoLayout layout, Vector3 contentSize, float scrollDelta, float spacing, int scrollDirection, int scrollAxis)
            {
                ScrollAxis = scrollAxis;
                ScrollAxisDirection = layout.AxisDirection;
                ScrollDelta = scrollDelta;
                ContentSize = (float3)contentSize;
                SpaceBetweenElements = spacing;
                Alignment = layout.Alignment;

                // move "scroll" into layout.Alignment coordinate space and apply to existing offset
                Offset = layout.Offset + layout.AlignmentPositiveDirection * ScrollDelta;

                ScrollDirection = scrollDirection;

                // Determine whether the content in view being is shifted towards the point of alignment
                ContentMovingTowardsAlignment = layout.AlignmentPositiveDirection != ScrollDirection;

                // Determine if moving content in the scrolled direction will pull higher order elements into view
                PagingInHigherOrderElements = layout.ContentDirection != ScrollDirection;
                NextElementIncrement = PagingInHigherOrderElements ? 1 : -1;
            }

            public void FlipDirection()
            {
                PagingInHigherOrderElements = !PagingInHigherOrderElements;
                ScrollDelta = -ScrollDelta;
                ScrollDirection = -ScrollDirection;
                ContentMovingTowardsAlignment = !ContentMovingTowardsAlignment;
                NextElementIncrement = -NextElementIncrement;
            }
        }
    }
}
