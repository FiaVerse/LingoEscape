// Copyright (c) Supernova Technologies LLC
//#define DEBUG_SCROLL
using Nova.Events;
using Nova.Internal;
using Nova.Internal.Input.Scrolling;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    internal interface IScroller : IInteractable, IScrollBoundsProvider { }

    /// <summary>
    /// Scrolls the attached <see cref="UIBlock"/>'s content along its <see cref="AutoLayout"/>.<see cref="Axis">Axis</see> with an iOS-like inertia and bounce effect
    /// </summary>
    /// <remarks>
    /// If there's a <see cref="ListView"/> on the same <see cref="GameObject"/>, this component will also handle scrolling the virtualized <see cref="ListView"/>
    /// </remarks>
    /// <see cref="Interaction.Scroll(Interaction.Update, Vector3, float, int, InputAccuracy)"/>
    /// <seealso cref="Gesture.OnClick"/>
    /// <seealso cref="Gesture.OnPress"/>
    /// <seealso cref="Gesture.OnRelease"/>
    /// <seealso cref="Gesture.OnHover"/>
    /// <seealso cref="Gesture.OnUnhover"/>
    /// <seealso cref="Gesture.OnMove"/>
    /// <seealso cref="Gesture.OnScroll"/>
    /// <seealso cref="Gesture.OnCancel"/>
    [AddComponentMenu("Nova/Scroller")]
    [HelpURL("https://novaui.io/manual/Scroller.html")]
    public sealed class Scroller : GestureRecognizer, IScroller
    {
        #region Public
        /// <summary>
        /// The animation applied to content once it's scrolled past the edges of its viewport
        /// </summary>
        [SerializeField]
        public OverscrollEffect OverscrollEffect = OverscrollEffect.Bounce;

        /// <summary>
        /// The scroll speed multiplier for vector (e.g. mouse wheel) scrolling
        /// </summary>
        [SerializeField]
        public float VectorScrollMultiplier = 1;

        /// <summary>
        /// Allow pointer drag events to trigger a scroll
        /// </summary>
        /// <remarks>
        /// <see cref="Interaction.Point(Interaction.Update, bool, bool, float, int, InputAccuracy)"/><br/>
        /// <see cref="Interaction.Point(Sphere, uint, object, int, InputAccuracy)"/>
        /// </remarks>
        /// <see cref="GestureRecognizer.DragThreshold"/>
        /// <see cref="GestureRecognizer.LowAccuracyDragThreshold"/>
        public bool DragScrolling
        {
            get
            {
                return dragScrolling;
            }
            set
            {
                if (value == dragScrolling)
                {
                    return;
                }

                dragScrolling = value;

                if (!ActiveAndEnabled)
                {
                    return;
                }

                if (dragScrolling)
                {
                    if (!VectorScrolling)
                    {
                        RegisterBaseEvents();
                    }

                    UIBlock.InputTarget.OnPointerInputChanged += HandlePointerInput;
                }
                else
                {
                    UIBlock.InputTarget.OnPointerInputChanged -= HandlePointerInput;

                    if (!VectorScrolling)
                    {
                        UnregisterBaseEvents();
                    }
                }
            }
        }

        /// <summary>
        /// Allow mouse wheel or joystick vector events to trigger a scroll via <see cref="Interaction.Scroll(Interaction.Update, Vector3, float, int, InputAccuracy)"/>
        /// </summary>
        public bool VectorScrolling
        {
            get
            {
                return vectorScrolling;
            }
            set
            {
                if (value == vectorScrolling)
                {
                    return;
                }

                vectorScrolling = value;

                if (!ActiveAndEnabled)
                {
                    return;
                }

                if (vectorScrolling)
                {
                    if (!DragScrolling)
                    {
                        RegisterBaseEvents();
                    }

                    UIBlock.InputTarget.OnVector3InputChanged += HandleScrollVector;
                }
                else
                {
                    UIBlock.InputTarget.OnVector3InputChanged -= HandleScrollVector;

                    if (!DragScrolling)
                    {
                        UnregisterBaseEvents();
                    }
                }
            }
        }

        /// <summary>
        /// The <see cref="Nova.UIBlock"/> scrollbar root
        /// </summary>
        public UIBlock ScrollbarVisual => scrollbarVisual;

        /// <summary>
        /// Indicates whether or not the <see cref="Scroller"/> should handle drag events on the <see cref="ScrollbarVisual"/>.
        /// </summary>
        /// <remarks>
        /// Proper configuration requires the <see cref= "ScrollbarVisual"/> to have an <see cref="Interactable"/> attached to it which must have <see cref="Interactable.Draggable"/> set to <see langword="true"/> along the scrolling axis.
        /// </remarks>
        /// <seealso cref="Interactable"/>
        /// <seealso cref="Interactable.Draggable"/>
        public bool DraggableScrollbar
        {
            get
            {
                return draggableScrollbar;
            }
            set
            {
                if (value == draggableScrollbar)
                {
                    return;
                }

                draggableScrollbar = value;

                if (scrollbarVisual == null || !ActiveAndEnabled)
                {
                    return;
                }

                if (draggableScrollbar)
                {
                    scrollbarVisual.AddEventHandler(HandleScrollbarRelease, includeHierarchy: true);
                    scrollbarVisual.AddEventHandler(HandleScrollbarDrag, includeHierarchy: true);
                }
                else
                {
                    scrollbarVisual.RemoveEventHandler(HandleScrollbarRelease);
                    scrollbarVisual.RemoveEventHandler(HandleScrollbarDrag);
                }
            }
        }

        /// <summary>
        /// The number of child UIBlocks this scroller is capable of scrolling to.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>
        /// Value can be larger than <see cref="UIBlock.ChildCount"/> if this <see cref="GameObject"/> has an attached <see cref="ListView"/> component.
        /// </description></item>
        /// <item><description>
        /// This is the upper limit (exclusive) for the index that can be passed to <see cref="ScrollToIndex(int, bool)"/>.
        /// </description></item>
        /// </list>
        /// </remarks>
        public int ScrollableChildCount => HasVirtualizedContent ? scrollView.ItemCount : UIBlock.ChildCount;

        /// <summary>
        /// Abruptly stop the active scroll 
        /// </summary>
        /// <remarks>If <c>this.ActiveAndEnabled == <see langword="false"/></c>, this call won't do anything.</remarks>
        public void CancelScroll()
        {
            if (!ActiveAndEnabled)
            {
                return;
            }

            if (latestSource.Initialized)
            {
                UIBlock.InputTarget.CancelInput(latestSource);
            }
            else
            {
                Canceled(ref latestSource);
            }
        }

        /// <summary>
        /// Scrolls to the child item at the provided index <paramref name="index"/> and applies a brief "after scroll" animation.
        /// </summary>
        /// <remarks>If <c>this.ActiveAndEnabled == <see langword="false"/></c>, this call won't do anything.</remarks>
        /// <param name="index">The index of the child in the range [0, <see cref="ScrollableChildCount"/>) to scroll to</param>
        /// <exception cref="System.IndexOutOfRangeException">if <c><paramref name="index"/> &lt; 0 || <paramref name="index"/> &gt;= <see cref="ScrollableChildCount"/></c></exception>
        /// <exception cref="System.InvalidOperationException">if <c><see cref="UIBlock">UIBlock</see>.<see cref="UIBlock.AutoLayout">AutoLayout</see>.<see cref="AutoLayout.Axis">Axis</see> == <see cref="Axis"/>.<see cref="Axis.None">None</see></c></exception>
        public void ScrollToIndex(int index) => ScrollToIndex(index, animate: true);

        /// <summary>
        /// Scrolls to the child item at the provided index <paramref name="index"/> and optionally applies a brief "after scroll" animation.
        /// </summary>
        /// <remarks>If <c>this.ActiveAndEnabled == <see langword="false"/></c>, this call won't do anything.</remarks>
        /// <param name="index">The index of the child in the range [0, <see cref="ScrollableChildCount"/>) to scroll to</param>
        ///<param name="animate">Applies an "after scroll" animation when <c>true</c>. Jumps to the position without animating when <c>false</c>.</param>
        /// <exception cref="System.IndexOutOfRangeException">if <c><paramref name="index"/> &lt; 0 || <paramref name="index"/> &gt;= <see cref="ScrollableChildCount"/></c></exception>
        /// <exception cref="System.InvalidOperationException">if <c><see cref="UIBlock">UIBlock</see>.<see cref="UIBlock.AutoLayout">AutoLayout</see>.<see cref="AutoLayout.Axis">Axis</see> == <see cref="Axis"/>.<see cref="Axis.None">None</see></c></exception>
        public void ScrollToIndex(int index, bool animate)
        {
            if (!ActiveAndEnabled)
            {
                return;
            }

            float delta = 0;

            if (!HasVirtualizedContent)
            {
                ref AutoLayout layout = ref UIBlock.AutoLayout;

                if (!layout.Axis.TryGetIndex(out int axis))
                {
                    throw new System.InvalidOperationException($"{UIBlock.name}'s Auto Layout axis is unassigned.");
                }

                if (index < 0 || index >= UIBlock.ChildCount)
                {
                    throw new System.IndexOutOfRangeException($"Expected: [0, {UIBlock.ChildCount}). Actual: {index}.");
                }

                UIBlock listItem = UIBlock.GetChild(index);

                delta = LayoutUtils.GetMinDistanceToParentEdge(listItem, axis, layout.Alignment);

                if (!animate)
                {
                    layout.Offset += delta * layout.AlignmentPositiveDirection;
                    delta = 0;
                }
            }
            else
            {
                if (animate)
                {
                    delta = scrollView.JumpToIndexPage(index);
                }
                else
                {
                    scrollView.JumpToIndex(index);
                }
            }

            RefreshBasis();

            decelerate = false;

            ScrollBehavior.Start(currentTime - scrollEndTime, 0);

            float velocity = delta * -RubberBandScrollSimulation.LogLowDrag;

            ReleaseScroll(velocity, RubberBandScrollSimulation.LowDrag);
        }

        /// <summary>
        /// Scrolls content by the provided <paramref name="delta"/> along the <see cref="UIBlock"/>'s <see cref="AutoLayout"/>.<see cref="AutoLayout.Axis">Axis</see>
        /// </summary>
        /// <remarks>
        /// If <c>this.ActiveAndEnabled == <see langword="false"/></c>, this call won't do anything. 
        /// May require a call to <see cref="UIBlock.CalculateLayout"/> if attempting to scroll immediately 
        /// after child content has been modified.
        /// </remarks>
        /// <param name="delta">Value is in <c><see cref="UIBlock"/>.transform</c> local space</param>
        /// <exception cref="System.InvalidOperationException">if <c><see cref="UIBlock">UIBlock</see>.<see cref="UIBlock.AutoLayout">AutoLayout</see>.<see cref="AutoLayout.Axis">Axis</see> == <see cref="Axis"/>.<see cref="Axis.None">None</see></c></exception>
        public void Scroll(float delta)
        {
            if (!ActiveAndEnabled)
            {
                return;
            }

            if (!UIBlock.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int index))
            {
                throw new System.InvalidOperationException($"{UIBlock.name}'s Auto Layout axis is unassigned.");
            }

            Vector3 scroll = Vector3.zero;
            scroll[index] = delta;

            Internal.Interaction source = Internal.Interaction.Uninitialized;

            VectorScroll(scroll, ref source);
        }

        /// <summary>
        /// Moves the scrollbar to the <paramref name="newScrollbarWorldPosition"/> and scrolls the content accordingly
        /// </summary>
        /// <remarks>
        /// If <c><see cref="DraggableScrollbar"/> == <see langword="true"/></c>, this will be called automatically whenever the <see cref="ScrollbarVisual"/> fires <see cref="Gesture.OnDrag"/> events.<br/>
        /// If <c>this.ActiveAndEnabled == <see langword="false"/></c>, this call won't do anything.
        /// </remarks>
        /// <param name="newScrollbarWorldPosition">The position to move the scrollbar to in world space. This will clamp everything to move along the scrolling axis.</param>
        public void DragScrollbarToPosition(Vector3 newScrollbarWorldPosition)
        {
            if (!ActiveAndEnabled || scrollbarVisual == null || scrollbarVisual.Parent == null)
            {
                return;
            }

            decelerate = false;

            AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();

            if (!layout.Axis.TryGetIndex(out int axis))
            {
                return;
            }

            // Convert to local space
            Vector3 newScrollbarPositionLocalSpace = scrollbarVisual.transform.parent.InverseTransformPoint(newScrollbarWorldPosition);
            float scrollableSize = scrollbarVisual.Parent.PaddedSize[axis];
            float dragDelta = newScrollbarPositionLocalSpace[axis] - scrollbarVisual.transform.localPosition[axis];

            Length.Calculated size = scrollbarVisual.CalculatedSize[axis] + scrollbarVisual.CalculatedMargin[axis].Sum();

            // get total length as a percent
            float range = math.max(0, 0.5f - (size.Percent * 0.5f));

            if (Math.ApproximatelyZero(range))
            {
                // nowhere to scroll
                return;
            }

            float dragPercent = dragDelta / scrollableSize;
            float contentSize = HasVirtualizedContent ? scrollView.EstimatedTotalContentSize : UIBlock.ContentSize[axis];

            float scrollBy = -dragPercent * contentSize;

            Scroll(scrollBy);
        }
        #endregion

        #region Internal
        private const int ReleaseScrollAfterFrames = 2;

        [SerializeField, NotKeyable, HideInInspector]
        private bool dragScrolling = true;
        [SerializeField, NotKeyable, HideInInspector]
        private bool vectorScrolling = true;
        [SerializeField, HideInInspector]
        private UIBlock scrollbarVisual;
        [SerializeField, NotKeyable, HideInInspector]
        private bool draggableScrollbar = false;

        [System.NonSerialized, HideInInspector]
        private Scrollbar scrollbar = new Scrollbar();

        internal float Velocity => velocityTracker.Value;

        [System.NonSerialized, HideInInspector]
        private IScrollableView scrollView = null;

        private bool HasVirtualizedContent => (scrollView as MonoBehaviour) != null && scrollView.ItemCount > 0;

        [System.NonSerialized, HideInInspector]
        private bool decelerate = true;

        [System.NonSerialized, HideInInspector]
        private float totalScrollThisFrame = 0;

        [System.NonSerialized, HideInInspector]
        private bool immediateScrolled = false;
        [System.NonSerialized, HideInInspector]
        private int unscrolledFrames = 0;

        private RawInput.VectorInputChangeEvent handleScrollVector = null;
        private RawInput.VectorInputChangeEvent HandleScrollVector
        {
            get
            {
                if (handleScrollVector == null)
                {
                    handleScrollVector = HandleScroll;
                }

                return handleScrollVector;
            }
        }

        private UIEventHandler<Gesture.OnDrag> handleScrollbarDrag = null;
        private UIEventHandler<Gesture.OnDrag> HandleScrollbarDrag
        {
            get
            {
                if (handleScrollbarDrag == null)
                {
                    handleScrollbarDrag = HandleScrollbar;
                }

                return handleScrollbarDrag;
            }
        }

        private UIEventHandler<Gesture.OnRelease> handleScrollbarRelease = null;
        private UIEventHandler<Gesture.OnRelease> HandleScrollbarRelease
        {
            get
            {
                if (handleScrollbarRelease == null)
                {
                    handleScrollbarRelease = HandleScrollbar;
                }

                return handleScrollbarRelease;
            }
        }

        System.Type IEventTargetProvider.BaseTargetableType => typeof(UIBlock);
        bool IEventTargetProvider.TryGetTarget(IEventTarget receiver, System.Type _, out IEventTarget target)
        {
            target = UIBlock;
            return true;
        }

        [System.NonSerialized, HideInInspector]
        private SimpleMovingAverage velocityTracker = default;
        [System.NonSerialized, HideInInspector]
        private SimpleMovingAverage dragTracker = default;

        [System.NonSerialized, HideInInspector]
        private float scrollEndTime = 0;

        [System.NonSerialized, HideInInspector]
        private float currentTime = 0;

        [System.NonSerialized, HideInInspector]
        private float scrollBasis = 0;

        [System.NonSerialized, HideInInspector]
        private ScrollBehavior scrollBehavior = null;
        private ScrollBehavior ScrollBehavior
        {
            get
            {
                // Lazy construct because it may
                // be accessed before enabled
                if (scrollBehavior == null)
                {
                    scrollBehavior = new ScrollBehavior(this);
                }

                return scrollBehavior;
            }
        }


        [System.NonSerialized, HideInInspector]
        private Internal.Interaction latestSource = Internal.Interaction.Uninitialized;

        internal float ContentPosition
        {
            get
            {
                if (!ActiveAndEnabled)
                {
                    return 0f;
                }

                if (!HasVirtualizedContent)
                {
                    if (!UIBlock.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int axis))
                    {
                        return 0;
                    }

                    return UIBlock.ContentCenter[axis];
                }

                return scrollView.EstimatedPosition;
            }
        }

        private void RefreshBasis()
        {
            AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();
            if (!layout.Axis.TryGetIndex(out int axis))
            {
                return;
            }

            if (layout.Alignment == 1)
            {
                // already in the right coordinate space
                scrollBasis = layout.Offset;
                return;
            }

            float parent = UIBlock.PaddedSize[axis];
            float content = UIBlock.ContentSize[axis];
            float layoutOffset = layout.Offset;
            float paddingOffset = UIBlock.CalculatedPadding.Offset[axis];

            float localPosition = LayoutUtils.LayoutOffsetToLocalPosition(layoutOffset, content, parent, paddingOffset, layout.Alignment);

            scrollBasis = LayoutUtils.LocalPositionToLayoutOffset(localPosition, content, parent, paddingOffset, 1);
        }

        internal void ScrollToPosition(float position)
        {
            Scroll(position - ContentPosition);
        }

        private protected override void Init()
        {
            // We need to make sure to reset so that any scrolling that was happening before
            // doesn't kick off
            ScrollBehavior.Reset();

            if (scrollView == null)
            {
                scrollView = GetComponent<IScrollableView>();
            }

            velocityTracker = new SimpleMovingAverage(0);
            dragTracker = new SimpleMovingAverage(0);

            totalScrollThisFrame = 0;

            if (DragScrolling || VectorScrolling)
            {
                RegisterBaseEvents();
            }

            if (VectorScrolling)
            {
                UIBlock.InputTarget.OnVector3InputChanged += HandleScrollVector;
            }

            if (DragScrolling)
            {
                UIBlock.InputTarget.OnPointerInputChanged += HandlePointerInput;
            }

            UIBlock.InputTarget.SetNavigationNode(this);

            if (DraggableScrollbar && scrollbarVisual != null)
            {
                scrollbarVisual.AddEventHandler(HandleScrollbarRelease, includeHierarchy: true);
                scrollbarVisual.AddEventHandler(HandleScrollbarDrag, includeHierarchy: true);
            }

            scrollbar.Init(UIBlock, scrollView, scrollbarVisual);
        }

        private bool CompletelyWithinViewport(IUIBlock child, int axis)
        {
            Bounds bounds = new Bounds(child.GetCalculatedTransformLocalPosition(), child.CalculatedSize.Value);
            Vector3 extents = UIBlock.CalculatedSize.Value * 0.5f;

            return bounds.min[axis] >= -extents[axis] && bounds.max[axis] <= extents[axis];
        }

        private bool CompletelyOutsideViewport(IUIBlock child, int axis)
        {
            Bounds bounds = new Bounds(child.GetCalculatedTransformLocalPosition(), child.CalculatedSize.Value);
            Vector3 extents = UIBlock.CalculatedSize.Value * 0.5f;

            return bounds.max[axis] < -extents[axis] || bounds.min[axis] > extents[axis];
        }

        private protected override void Deinit()
        {
            if (DragScrolling || VectorScrolling)
            {
                UnregisterBaseEvents();
            }

            if (VectorScrolling)
            {
                UIBlock.InputTarget.OnVector3InputChanged -= HandleScrollVector;
            }

            if (DragScrolling)
            {
                UIBlock.InputTarget.OnPointerInputChanged -= HandlePointerInput;
            }

            UIBlock.InputTarget.ClearNavigationNode();

            if (DraggableScrollbar && scrollbarVisual != null)
            {
                scrollbarVisual.RemoveEventHandler(HandleScrollbarRelease);
                scrollbarVisual.RemoveEventHandler(HandleScrollbarDrag);
            }

            latestSource = default;
            RefreshBasis();
        }

        private void RegisterBaseEvents()
        {
            UIBlock.RegisterEventTargetProvider(this);
            UIBlock.InputTarget.SetGestureRecognizer(this);
            UIBlock.InputTarget.OnInputCanceled += InputCanceledHandler;
        }

        private void UnregisterBaseEvents()
        {
            UIBlock.UnregisterEventTargetProvider(this);
            UIBlock.InputTarget.ClearGestureRecognizer();
            UIBlock.InputTarget.OnInputCanceled -= InputCanceledHandler;
        }

        private void LateUpdate()
        {
            if (totalScrollThisFrame != 0)
            {
                unscrolledFrames = 0;
            }

            if (immediateScrolled && totalScrollThisFrame == 0 && ++unscrolledFrames >= ReleaseScrollAfterFrames)
            {
                unscrolledFrames = 0;
                immediateScrolled = false;

                if (OverScrolled()) // if overscrolled, release to recover
                {
                    InputState state = default;
                    Gesture.OnRelease release = Gesture.Release(latestSource.ToPublic(), UIBlock, hovering: true, wasDragged: false);
                    Released(ref state, ref release);
                }
                else // if not overscrolled, ensure no update occurs
                {
                    Canceled(ref latestSource);
                }
            }

            ScrollBehavior.ClampToBounds = OverscrollEffect == OverscrollEffect.Clamp;

            if (decelerate)
            {
                ScrollTo(ScrollBehavior.AutoUpdate(currentTime));

            }
            else if (UIBlock.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int scrollAxis))
            {
                float scroll = totalScrollThisFrame;
                float viewport = UIBlock.PaddedSize[scrollAxis] * 0.5f;

                dragTracker.AddSample(Math.Clamp(scroll, -viewport, viewport));

                float delta = immediateScrolled ? scroll : dragTracker.Value;
                velocityTracker.AddSample(delta / Time.unscaledDeltaTime);

                ScrollTo(ScrollBehavior.ManualUpdate(delta, currentTime));

            }

            if (UIBlock.LayoutIsDirty || scrollbar.ContentChanged)
            {
                scrollbar.UpdateVisuals();
            }

            currentTime += Time.unscaledDeltaTime;
            totalScrollThisFrame = 0;
        }

        private void HandleScrollbar(Gesture.OnDrag evt)
        {
            DragScrollbarToPosition(scrollbarVisual.transform.position + evt.DragDeltaWorldSpace);
            immediateScrolled = false;
        }

        private void HandleScrollbar(Gesture.OnRelease evt)
        {
            Internal.Interaction source = evt.Interaction.ToInternal();
            Canceled(ref source);
        }

        private void HandleScroll(ref RawInput.OnChanged<UniqueValue<Vector3>> evt)
        {
            if (!evt.Current.HasValue)
            {
                return;
            }

            Input<UniqueValue<Vector3>> scroll = evt.Current.Value;

            if (!scroll.IsHit || scroll.UserInput.Amount == Vector3.zero)
            {
                return;
            }

            VectorScroll(scroll.UserInput.Amount * VectorScrollMultiplier, ref evt.Interaction);
        }

        private void VectorScroll(Vector3 delta, ref Internal.Interaction source)
        {
            immediateScrolled = true;
            AddScrollAmount(delta, ref source);
        }

        private protected override void ProcessUniqueGesture(ref RawInput.OnChanged<bool> evt, ref InputState inputState, ref Vector3 currentPositionRootSpace, ref Matrix4x4 rootToWorld)
        {
            if (latestSource.Initialized && evt.Interaction.ID != latestSource.ID && InputStates.TryGetValue(latestSource.ID, out InputState previousState) && previousState.Dragged)
            {
                return;
            }

            Input<bool> previous = evt.Previous.GetValueOrDefault();
            Input<bool> current = evt.Current.GetValueOrDefault();

            bool wasActive = previous.UserInput;
            bool isActive = current.UserInput;

            bool started = current.GestureDetected && !previous.GestureDetected;
            bool gesturing = current.GestureDetected || previous.GestureDetected;
            bool ended = previous.GestureDetected && !isActive;

            if (!wasActive && isActive)
            {
                Canceled(ref evt.Interaction);
            }

            if (!gesturing)
            {
                return;
            }

            inputState.Dragged = true;

            immediateScrolled = false;

            Vector3 delta = Vector3.zero;

            float threshold = math.abs(math.sin(math.radians(GetDragThreshold(ref current)))) * Vector3.Distance(evt.Interaction.Ray.origin, current.HitPoint);

            Vector3 previousWorldPos = evt.Previous.HasValue ? rootToWorld.MultiplyPoint(inputState.PreviousPositionRootSpace) : current.HitPoint;

            if (started)
            {
                RefreshBasis();
                ScrollBehavior.Start(currentTime - scrollEndTime, threshold);
                dragTracker.Value = 0;

                if (wasActive)
                {
                    delta = evt.GetHitLocalTranslation(previousWorldPos);
                }
            }
            else
            {
                delta = evt.GetHitLocalTranslation(previousWorldPos);

            }

            if (!isActive)
            {
                return;
            }

            AddScrollAmount(delta, ref evt.Interaction);
        }

        private protected override bool Cancelable => IsMoving();

        private protected override void Canceled(ref Internal.Interaction source)
        {
            decelerate = true;
            scrollEndTime = currentTime;

            RefreshBasis();

            ScrollBehavior.Cancel(scrollEndTime);

            immediateScrolled = false;
            totalScrollThisFrame = 0;

            if (velocityTracker.Value == 0)
            {
                return;
            }

            velocityTracker.Value = 0;

        }

        private protected override void Released(ref InputState state, ref Gesture.OnRelease evt)
        {
            if (decelerate)
            {
                return;
            }

            ReleaseScroll(velocityTracker.Value);

            latestSource = evt.Interaction.ToInternal();
        }

        private void ReleaseScroll(float velocity, double drag = RubberBandScrollSimulation.Drag)
        {
            if (decelerate)
            {
                return;
            }

            decelerate = true;
            scrollEndTime = currentTime;
            ScrollBehavior.End(velocity, scrollEndTime, drag);

        }

        ScrollBounds IScrollBoundsProvider.GetBounds()
        {
            AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();

            if (!layout.Axis.TryGetIndex(out int index))
            {
                return default;
            }

            float scrollRange = UIBlock.PaddedSize[index];
            float contentSize = UIBlock.ContentSize[index];
            float paddingOffset = UIBlock.CalculatedPadding.Offset[index];
            float sizeDelta = scrollRange - contentSize;
            float contentOffset = UIBlock.ContentCenter[index];

            bool hasVirtualContent = HasVirtualizedContent;

            bool unboundedMin = hasVirtualContent && scrollView.HasContentInDirection(-1);
            bool unboundedMax = hasVirtualContent && scrollView.HasContentInDirection(1);

            if (sizeDelta < 0 || unboundedMax || unboundedMin)
            {
                float scrollSpaceOffset = LayoutUtils.LocalPositionToLayoutOffset(contentOffset, contentSize, scrollRange, paddingOffset, 1);
                float virtualizedShift = scrollBasis - scrollSpaceOffset;

                float totalContentSize = hasVirtualContent ? scrollView.EstimatedTotalContentSize : contentSize;

                double2 minMax = default;
                minMax.x = unboundedMin ? double.NegativeInfinity : math.max(sizeDelta + virtualizedShift, -totalContentSize);
                minMax.y = unboundedMax ? double.PositiveInfinity : math.min(virtualizedShift, totalContentSize);

                if (minMax.x > minMax.y)
                {
                    minMax = minMax.yx;
                }

                return new ScrollBounds(minMax, scrollBasis, scrollRange);
            }

            // 0 in the autolayout alignment space but
            // converted into alignment == 1 space, which is where we scroll
            float anchorScalar = (layout.Alignment - 1) * -0.5f;
            float anchor = sizeDelta * anchorScalar;

            return new ScrollBounds(anchor, scrollBasis, scrollRange);
        }

        private bool OverScrolled()
        {
            AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();

            if (layout.Axis.TryGetIndex(out int index) || layout.Offset == 0)
            {
                return false;
            }

            bool atPositiveEnd = !HasContentInDirection(1, out _);
            bool atNegativeEnd = !HasContentInDirection(-1, out _);

            if (!atPositiveEnd && !atNegativeEnd)
            {
                return false;
            }

            float contentOffset = UIBlock.ContentCenter[index];
            float contentSize = UIBlock.ContentSize[index];
            float viewportSize = UIBlock.PaddedSize[index];

            float contentExtent = contentSize * 0.5f;
            float contentMin = contentOffset - contentExtent;
            float contentMax = contentOffset + contentExtent;
            float viewportExtent = viewportSize * 0.5f;

            return (atPositiveEnd && contentMax < viewportExtent) ||
                   (atNegativeEnd && contentMin > -viewportExtent);
        }

        private void AddScrollAmount(Vector3 scroll, ref Internal.Interaction source)
        {


            decelerate = false;

            if (UIBlock.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int axis))
            {
                totalScrollThisFrame += scroll[axis];
            }

            latestSource = source;
        }

        private void ScrollTo(float newPosition)
        {
            float delta = (float)scrollBasis - newPosition;

            if (decelerate)
            {
                velocityTracker.Value = ScrollBehavior.GetSimulationVelocity(currentTime);
            }

            if (Math.ApproximatelyZero(delta))
            {
                return;
            }

            ref AutoLayout layout = ref UIBlock.AutoLayout;

            if (!layout.Axis.TryGetIndex(out int axis))
            {
                return;
            }

            float viewport = UIBlock.PaddedSize[axis];

            // Somewhat arbitrarily double the viewport so we can scroll by a full page
            delta = Math.MinAbs(delta, 2 * viewport * Math.Sign(delta));

            if (HasVirtualizedContent)
            {
                scrollView.Scroll(delta);
                scrollBasis = newPosition;
            }
            else
            {
                layout.Offset += delta * layout.AlignmentPositiveDirection;
                RefreshBasis();
            }

            Vector3 scrollDelta = Vector3.zero;
            scrollDelta[axis] = delta;

            ScrollType scrollType = decelerate ? ScrollType.Inertial : ScrollType.Manual;

            UIBlock.FireEvent(Gesture.Scroll(latestSource.ToPublic(), UIBlock, scrollType, scrollDelta));
        }

        GestureState IGestureRecognizer.TryRecognizeGesture<T>(Ray startRay, Input<T> start, Input<T> sample, Transform top)
        {
            int scrollAxisIndex = UIBlock.GetAutoLayoutReadOnly().Axis.Index();

            Vector3 scrollAxis = Vector3.zero;

            GestureState state = GestureState.None;

            if (scrollAxisIndex >= 0)
            {
                scrollAxis[scrollAxisIndex] = 1;
            }
            else
            {
                return state;
            }

            System.Type inputType = typeof(T);

            if (IsMoving() && top.IsChildOf(transform))
            {
                return GestureState.Occurring;
            }

            state = GestureState.Pending;

            if (inputType == typeof(bool))
            {
                Vector3 scrollAxisWorldSpace = UIBlock.transform.TransformDirection(scrollAxis);
                Vector3 gestureNormal = startRay.direction;
                Vector3 gestureStartPoint = startRay.origin;
                Vector3 axis = Vector3.Cross(gestureNormal, scrollAxisWorldSpace);

                if (axis == Vector3.zero)
                {
                    // Just pick a non scrollable axis
                    scrollAxis = scrollAxisIndex != 0 ? Vector3.right : scrollAxisIndex != 1 ? Vector3.up : Vector3.forward;
                    gestureNormal = UIBlock.transform.TransformDirection(scrollAxis);
                    gestureStartPoint = start.HitPoint;
                    axis = Vector3.Cross(gestureNormal, scrollAxisWorldSpace);
                }

                Vector3 translationDirection = Math.ApproximatelyZeroToZero(sample.HitPoint - gestureStartPoint);

                float degrees = Math.AngleBetweenAroundAxis(gestureNormal, translationDirection, axis);
                degrees = math.min(degrees, 180 - degrees);

                state = degrees >= GetDragThreshold(ref sample) ? GetDetectionPriority(ref start, ref sample, scrollAxisIndex) : state;
            }
            else if (inputType == typeof(UniqueValue<Vector3>))
            {
                _ = sample.TryConvertIfSameType(out Input<UniqueValue<Vector3>>? scrollSample);
                Vector3 delta = scrollSample.Value.UserInput.Amount;

                state = delta[scrollAxisIndex] != 0 ? GetDetectionPriority(Vector3.zero, delta, scrollAxisIndex) : state;
            }

            return state;
        }

        bool INavigationNode.Enabled => IsNavigable;
        bool INavigationNode.CaptureInput => OnSelect == SelectBehavior.FireEvents;
        bool INavigationNode.ScopeNavigation => OnSelect == SelectBehavior.ScopeNavigation;

        bool INavigationNode.UseTargetNotFoundFallback(Vector3 direction)
        {
            if (LayoutUtils.TryGetAxisDirection(UIBlock, direction, out int _, out int axisDirection))
            {
                return !HasContentInDirection(axisDirection, out _);
            }

            return true;
        }

        bool INavigationNode.TryGetNext(Vector3 direction, out IUIBlock toUIBlock)
        {
            toUIBlock = null;

            if (!IsNavigable)
            {
                return false;
            }

            return Navigation.TryGetNavigation(direction, out toUIBlock);
        }

        bool INavigationNode.TryHandleScopedMove(IUIBlock previousChild, IUIBlock nextChild, Vector3 direction)
        {
            if (!IsNavigable)
            {
                return false;
            }

            if (!LayoutUtils.TryGetAxisDirection(UIBlock, direction, out int axis, out int axisDirection))
            {
                return false;
            }

            if (previousChild == null)
            {
                if (HasContentInDirection(axisDirection, out float amount))
                {
                    float scroll = Math.MinAbs(UIBlock.GetChildAtIndex(0).LayoutSize[axis] + UIBlock.CalculatedSpacing.Value, amount);
                    Scroll(scroll * -axisDirection);

                    return true;
                }

                return false;
            }

            if (nextChild == null)
            {
                if (HasContentInDirection(axisDirection, out float amount))
                {
                    float scroll = Math.MinAbs(previousChild.LayoutSize[axis] + UIBlock.CalculatedSpacing.Value, amount);
                    Scroll(scroll * axisDirection);

                    return true;
                }

                return false;
            }

            if (CompletelyOutsideViewport(nextChild, axis))
            {
                float distanceFromEdge = LayoutUtils.GetMinDistanceToParentEdge(nextChild, axis, UIBlock.GetAutoLayoutReadOnly().Alignment);
                float toSize = (nextChild.LayoutSize[axis] + UIBlock.CalculatedSpacing.Value) * axisDirection;

                if (!Math.ApproximatelyLessThan(Math.Abs(distanceFromEdge), Math.Abs(toSize)))
                {
                    Scroll(toSize);

                    return true;
                }
            }

            if (!CompletelyWithinViewport(nextChild, axis))
            {
                int scrollIndex = 0;

                if (HasVirtualizedContent)
                {
                    IUIBlock listItem = nextChild.IsVirtual ? nextChild.GetChildAtIndex(0) : nextChild;
                    if (!scrollView.TryGetIndexOfItem(listItem, out scrollIndex))
                    {
                        // something's in a bad state, don't try to be clever
                        return false;
                    }
                }
                else
                {
                    scrollIndex = UIBlock.GetChildIndex(nextChild);
                }

                ScrollToIndex(scrollIndex, animate: false);
            }

            return true;
        }

        void INavigationNode.EnsureInView(IUIBlock descendant)
        {
            AutoLayout layout = UIBlock.GetAutoLayoutReadOnly();
            if (!layout.Axis.TryGetIndex(out int axis))
            {
                return;
            }

            if (descendant == null)
            {
                return;
            }

            Vector3 worldPosition = LayoutDataStore.Instance.GetLocalToWorldMatrix(descendant).c3.xyz;
            Vector3 localPosition = UIBlock.transform.InverseTransformPoint(worldPosition);

            float marginOffset = descendant.CalculatedMargin.Offset[axis];
            float spacingOffset = marginOffset + UIBlock.CalculatedPadding.Offset[axis];
            float position = localPosition[axis];
            float size = descendant.CalculatedSize[axis].Value;

            float layoutOffset = LayoutUtils.LocalPositionToLayoutOffset(position, size, UIBlock.PaddedSize[axis], spacingOffset, layout.Alignment);

            float scroll = LayoutUtils.GetMinDistanceToAncestorEdge(size, layoutOffset, marginOffset, UIBlock, axis, layout.Alignment);
            int direction = -(int)Math.Sign(scroll);

            if (!Math.ApproximatelyZero(scroll) && HasContentInDirection(direction, out _))
            {
                Scroll(scroll);
                LateUpdate();
            }
        }

        private bool IsMoving()
        {
            if (!UIBlock.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int axis))
            {
                return !Math.ApproximatelyZero(velocityTracker.Value);
            }

            double maxVelocity = 0.1f * math.pow(10, math.round(math.log10(UIBlock.CalculatedSize[axis].Value)));

            return !Math.ApproximatelyZero(velocityTracker.Value, epsilon: maxVelocity);
        }

        private bool HasContentInDirection(int direction, out float amount)
        {
            if (HasVirtualizedContent && scrollView.HasContentInDirection(direction))
            {
                amount = direction < 0 ? float.MinValue : float.MaxValue;

                return true;
            }

            if (!UIBlock.GetAutoLayoutReadOnly().Axis.TryGetIndex(out int index))
            {
                amount = 0;
                return false;
            }

            float extentPoint = UIBlock.CalculatedPadding.Offset[index] + 0.5f * direction * UIBlock.PaddedSize[index];
            float contentPoint = UIBlock.ContentCenter[index] + 0.5f * direction * UIBlock.ContentSize[index];

            amount = direction * (contentPoint - extentPoint);

            float content = Math.Abs(contentPoint);
            float extent = Math.Abs(extentPoint);

            return Math.ApproximatelyEqual(content, extent) ? false : extent < content;
        }

        private GestureState GetDetectionPriority<T>(ref Input<T> start, ref Input<T> sample, int scrollAxisIndex) where T : unmanaged, System.IEquatable<T>
        {
            Vector3 startPosition = UIBlock.transform.InverseTransformPoint(start.HitPoint);
            Vector3 samplePosition = UIBlock.transform.InverseTransformPoint(sample.HitPoint);
            return GetDetectionPriority(startPosition, samplePosition, scrollAxisIndex);
        }

        private GestureState GetDetectionPriority(Vector3 startPosition, Vector3 samplePosition, int scrollAxisIndex)
        {
            int gestureDirection = (int)Math.Sign((startPosition - samplePosition)[scrollAxisIndex]);

            return HasContentInDirection(gestureDirection, out _) ? GestureState.DetectedHighPri : GestureState.DetectedLowPri;
        }

        bool IGestureRecognizer.ObstructDrags => ObstructDrags;

        private float GetDragThreshold<T>(ref Input<T> input) where T : unmanaged, System.IEquatable<T> => input.Noisy ? LowAccuracyDragThreshold : DragThreshold;

        private Scroller() : base()
        {
            ClickBehavior = ClickBehavior.None;
            onSelect = SelectBehavior.ScopeNavigation;
        }
        #endregion
    }
}
