// Copyright (c) Supernova Technologies LLC
//#define DEBUG_GESTURES

using Nova.Events;
using Nova.Extensions;
using Nova.Internal;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using UnityEngine;
using Navigator = Nova.Internal.Navigator<Nova.UIBlock>;

namespace Nova
{
    /// <summary>
    /// Defines on which event a "click" should be triggered.
    /// </summary>
    public enum ClickBehavior
    {
        /// <summary>
        /// On release, correlated with a pointer up event.
        /// </summary>
        OnRelease,
        /// <summary>
        /// On press, correlated with a pointer down event.
        /// </summary>
        OnPress,
        /// <summary>
        /// Don't trigger "click" events.
        /// </summary>
        None,
    }

    /// <summary>
    /// The animation applied to content once it's scrolled past the edges of its viewport.
    /// </summary>
    public enum OverscrollEffect
    {
        /// <summary>
        /// An iOS-like bounce effect.
        /// </summary>
        Bounce,
        /// <summary>
        /// A stay-in-bounds effect.
        /// </summary>
        Clamp
    };

    /// <summary>
    /// The coordinate space to use when tracking gesture positions across frames.
    /// </summary>
    public enum GestureSpace
    {
        /// <summary>
        /// The object's UIBlock root's local space.
        /// </summary>
        Root,

        /// <summary>
        /// World space.
        /// </summary>
        World
    }

    /// <summary>
    /// An abstract base class for gesture/interaction receiver components
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UIBlock))]
    public abstract class GestureRecognizer : MonoBehaviour
    {
        /// <summary>
        /// When <c>ObstructDrags == <see langword="true"/></c>, drag gestures will not be routed to content behind the attached <see cref="UIBlock"/>.<br/>
        /// When false, content behind the attached <see cref="UIBlock"/> can receive drag gestures if it is <i>draggable</i> in a direction this component is not.
        /// </summary>
        /// <remarks>Does not impact the <i>draggable</i> state of this component.</remarks>
        [SerializeField]
        public bool ObstructDrags = false;

        /// <summary>
        /// The threshold that must be surpassed to initiate a drag event.
        /// </summary>
        /// <remarks>This threshold is generally used for high precision input devices, E.g. mouse, touchscreen, XR controller, and most other <b>ray</b> based input.</remarks>
        /// <see cref="InputAccuracy"/>
        /// <see cref="LowAccuracyDragThreshold"/>
        [SerializeField]
        public float DragThreshold = 1;

        /// <summary>
        /// The threshold that must be surpassed to initiate a drag event with a less physically stable input device.
        /// </summary>
        /// <remarks>
        /// This "Low Accuracy" threshold is generally used for lower accuracy input devices, E.g. XR hand tracking and other <b>sphere collider</b> based input.<br/><br/>
        /// To get the most reliable behavior between potentially conflicting press, drag,
        /// and scroll gestures while using the <see cref="Interaction.Point(Sphere, uint, object, int, InputAccuracy)"/> path,
        /// the entry point of the overlapping target <see cref="UIBlock"/>s
        /// <b>must</b> all be coplanar. If the entry points aren't coplanar, say a scrollable <see cref="ListView"/>'s 
        /// front face is positioned behind a draggable list item's front face, attempts to scroll the <see cref="ListView"/>
        /// will likely fail, and the list item will be dragged instead.
        /// </remarks>
        /// <see cref="InputAccuracy"/>
        /// <see cref="DragThreshold"/>
        [SerializeField]
        public float LowAccuracyDragThreshold = 30;

        /// <summary>
        /// Determines when this component should trigger click events.
        /// </summary>
        /// <seealso cref="Gesture.OnClick"/>
        [SerializeField]
        public ClickBehavior ClickBehavior = ClickBehavior.OnRelease;

        [SerializeField]
        private bool navigable = true;

        /// <summary>
        /// Is this component navigable?
        /// </summary>
        public bool Navigable
        {
            get => navigable;
            set
            {
                if (navigable == value)
                {
                    return;
                }

                bool wasNavigable = IsNavigable;

                navigable = value;

                bool isNavigable = IsNavigable;

                if (wasNavigable == isNavigable)
                {
                    return;
                }

                if (wasNavigable && !isNavigable)
                {
                    Navigator.Untrack(UIBlock);
                }
                else if (!wasNavigable && isNavigable)
                {
                    Navigator.Track(UIBlock);
                }
            }
        }

        [SerializeField]
        private protected SelectBehavior onSelect = SelectBehavior.Click;

        [SerializeField]
        private protected bool autoSelect = false;

        /// <summary>
        /// Determines if this component should automatically be selected whenever it's navigated to.
        /// </summary>
        /// <remarks>
        /// If <see cref="OnSelect"/> is set to <see cref="SelectBehavior.ScopeNavigation"/>,
        /// setting this to <c>true</c> will effectively allow navigation moves to pass through
        /// the attached <see cref="UIBlock"/> onto the navigable descendant closest to the navigation
        /// source.
        /// </remarks>
        public bool AutoSelect
        {
            get => autoSelect;
            set
            {
                if (autoSelect == value)
                {
                    return;
                }

                autoSelect = value;

                if (IsNavigable && OnSelect != SelectBehavior.Click)
                {
                    Navigator.RegisterNavigationScope(UIBlock, value);
                }
            }
        }

        /// <summary>
        /// Determines how this component should handle select events.
        /// </summary>
        /// <seealso cref="Navigate.OnSelect"/>
        public SelectBehavior OnSelect
        {
            get => onSelect;
            set
            {
                if (onSelect == value)
                {
                    return;
                }

                bool wasNavigationScope = onSelect != SelectBehavior.Click;
                bool isNavigationScope = value != SelectBehavior.Click;

                onSelect = value;

                if (!IsNavigable || wasNavigationScope == isNavigationScope)
                {
                    return;
                }

                if (wasNavigationScope && !isNavigationScope)
                {
                    Navigator.UnregisterNavigationScope(UIBlock);
                }
                else if (!wasNavigationScope && isNavigationScope)
                {
                    Navigator.RegisterNavigationScope(UIBlock, AutoSelect);
                }
            }
        }

        /// <summary>
        /// Defines a <see cref="NavLink"/> per axis-aligned direction.
        /// </summary>
        [SerializeField]
        public NavNode Navigation = NavNode.TwoD;

        /// <summary>
        /// The attached <see cref="Nova.UIBlock"/> receiving the interaction events, <see cref="IEvent.Receiver"/>
        /// </summary>
        public UIBlock UIBlock
        {
            get
            {
                if (_uiBlock == null)
                {
                    _uiBlock = GetComponent<UIBlock>();
                }

                return _uiBlock;
            }
        }

        /// <summary>
        /// Prevent users from inherting
        /// </summary>
        internal GestureRecognizer() { }

        #region Cached Event Handler Actions
        private RawInput.PointerInputChangeEvent inputChangedHandler = null;
        private protected RawInput.PointerInputChangeEvent HandlePointerInput
        {
            get
            {
                if (inputChangedHandler == null)
                {
                    inputChangedHandler = ProcessInput;
                }

                return inputChangedHandler;
            }
        }

        private RawInput.InputCanceledEvent inputCanceledHandler = null;
        private protected RawInput.InputCanceledEvent InputCanceledHandler
        {
            get
            {
                if (inputCanceledHandler == null)
                {
                    inputCanceledHandler = ProcessInputCanceled;
                }

                return inputCanceledHandler;
            }
        }
        #endregion

        [System.NonSerialized]
        private UIBlock _uiBlock = null;

        private protected bool ActiveAndEnabled => UIBlock.Activated && isActiveAndEnabled;

        private protected virtual bool Cancelable => false;

        private protected struct InputState
        {
            public bool Hovered;
            public bool Pressed;
            public bool Dragged;

            public Vector3 PreviousPositionRootSpace;

            public override string ToString()
            {
                return $"State(H: {Hovered}, P: {Pressed}, D: {Dragged})";
            }
        }

        [System.NonSerialized, HideInInspector]
        private Dictionary<uint, InputState> inputStates = new Dictionary<uint, InputState>();
        private protected Dictionary<uint, InputState> InputStates => inputStates;

        [System.NonSerialized, HideInInspector]
        private List<uint> sourceIDs = new List<uint>();

        [System.NonSerialized, HideInInspector]
        private int pressTime = int.MaxValue;

        private void OnEnable()
        {
            if (Navigable)
            {
                Navigator.Track(UIBlock);

                if (OnSelect != SelectBehavior.Click)
                {
                    Navigator.RegisterNavigationScope(UIBlock, AutoSelect);
                }
            }

            Init();
        }

        private void OnDisable()
        {
            while (sourceIDs.Count > 0)
            {
                uint sourceID = sourceIDs[sourceIDs.Count - 1];

                if (!UIBlock.InputTarget.TryGetInputSource(sourceID, out Internal.Interaction source))
                {
                    source = new Internal.Interaction(sourceID);
                }

                RawInput.OnCanceled canceled = RawInput.Cancel(UIBlock, source);
                ProcessInputCanceled(ref canceled);
            }

            Deinit();

            Navigator.Untrack(UIBlock);

            if (OnSelect != SelectBehavior.Click)
            {
                Navigator.UnregisterNavigationScope(UIBlock);
            }
        }

        private void ProcessInput(ref RawInput.OnChanged<bool> evt)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Input<bool>? currentInput = evt.Current;
            Input<bool> current = currentInput.GetValueOrDefault();

            uint sourceID = evt.Interaction.ID;

            bool wasTrackingPointer = inputStates.TryGetValue(sourceID, out InputState inputState);

            bool valid = currentInput.HasValue;
            bool currentlyHasValue = current.UserInput;

            Matrix4x4 rootToWorld = Matrix4x4.identity;
            Vector3 currentPositionRootSpace = current.HitPoint;

            if (PointerSpace == GestureSpace.Root)
            {
                Transform root = UIBlock.Root.transform;

                rootToWorld = root.localToWorldMatrix;
                Matrix4x4 worldToRoot = Unity.Mathematics.math.inverse(rootToWorld);
                currentPositionRootSpace = worldToRoot.MultiplyPoint(current.HitPoint);
            }
            
            if (current.IsHit)
            {
                Hover(current.HitPoint, ref inputState, ref evt.Interaction);
            }

            bool hitPointChanged = inputState.PreviousPositionRootSpace != currentPositionRootSpace;

            if (valid && hitPointChanged && inputState.Hovered && (current.IsHit || currentlyHasValue))
            {
                Move(current.HitPoint, ref evt.Interaction);
            }

            if (inputState.Hovered && currentlyHasValue)
            {
                Press(current.HitPoint, ref inputState, ref evt.Interaction);
            }

            ProcessUniqueGesture(ref evt, ref inputState, ref currentPositionRootSpace, ref rootToWorld);

            if (inputState.Pressed && !currentlyHasValue)
            {
                Release(ref inputState, current.IsHit, valid, ref evt.Interaction);
            }

            if (inputState.Hovered && !current.IsHit && !currentlyHasValue)
            {
                Unhover(ref inputState, ref evt.Interaction);
            }

            // Possible an event handler disabled this object, so confirm 
            // this is still an interaction source that should be tracked
            if (valid && sourceIDs.Contains(sourceID) == wasTrackingPointer)
            {
                if (!wasTrackingPointer)
                {
                    sourceIDs.Add(sourceID);
                }

                inputState.PreviousPositionRootSpace = currentPositionRootSpace;

                inputStates[sourceID] = inputState;
            }
            else if (wasTrackingPointer)
            {
                inputStates.Remove(sourceID);
                sourceIDs.Remove(sourceID);
            }
        }

        private void ProcessInputCanceled(ref RawInput.OnCanceled evt)
        {
            if (!inputStates.TryGetValue(evt.Source.ID, out InputState state))
            {
                return;
            }

            inputStates.Remove(evt.Source.ID);
            sourceIDs.Remove(evt.Source.ID);

            if (state.Hovered || state.Pressed || state.Dragged || Cancelable)
            {
                Canceled(ref evt.Source);

                UIBlock.FireEvent(Gesture.Cancel(evt.Source.ToPublic(), UIBlock));
            }
        }

        private void Hover(Vector3 worldPosition, ref InputState state, ref Internal.Interaction source)
        {
            if (state.Hovered)
            {
                return;
            }

            state.Hovered = true;


            UIBlock.FireEvent(Gesture.Hover(source.ToPublic(), UIBlock, worldPosition));
        }

        private void Unhover(ref InputState state, ref Internal.Interaction source)
        {
            if (!state.Hovered)
            {
                return;
            }


            UIBlock.FireEvent(Gesture.Unhover(source.ToPublic(), UIBlock));
            state.Hovered = false;
        }

        private void Press(Vector3 worldPosition, ref InputState state, ref Internal.Interaction source)
        {
            if (state.Pressed)
            {
                return;
            }

            pressTime = Time.frameCount;

            state.Pressed = true;


            UIBlock.FireEvent(Gesture.Press(source.ToPublic(), UIBlock, worldPosition));

            if (!state.Dragged && ClickBehavior == ClickBehavior.OnPress)
            {

                UIBlock.FireEvent(Gesture.Click(source.ToPublic(), UIBlock));
            }
        }

        private void Release(ref InputState state, bool hovering, bool valid, ref Internal.Interaction source)
        {
            if (!state.Pressed)
            {
                return;
            }

            Gesture.OnRelease releaseEvt = Gesture.Release(source.ToPublic(), UIBlock, hovering, state.Dragged);

            UIBlock.FireEvent(releaseEvt);

            if (!hovering)
            {
                Unhover(ref state, ref source);
            }

            bool clickable = Time.frameCount - pressTime >= Internal.NovaSettings.Config.ClickFrameDeltaThreshold;

            pressTime = int.MaxValue;

            state.Pressed = false;

            if (valid && hovering && !state.Dragged && ClickBehavior == ClickBehavior.OnRelease)
            {
                if (clickable)
                {

                    UIBlock.FireEvent(Gesture.Click(source.ToPublic(), UIBlock));
                }
                else
                {
                    RawInput.OnCanceled canceled = RawInput.Cancel(UIBlock, source);
                    ProcessInputCanceled(ref canceled);
                }
            }

            Released(ref state, ref releaseEvt);
            state.Dragged = false;
        }

        private protected virtual void Released(ref InputState state, ref Gesture.OnRelease evt) { }
        private protected virtual void Canceled(ref Internal.Interaction source) { }

        private void Move(Vector3 worldPosition, ref Internal.Interaction source)
        {

            UIBlock.FireEvent(Gesture.Move(source.ToPublic(), UIBlock, worldPosition));
        }

        private protected abstract void ProcessUniqueGesture(ref RawInput.OnChanged<bool> evt, ref InputState inputState, ref Vector3 currentPositionRootSpace, ref Matrix4x4 rootToWorld);

        private protected abstract void Init();
        private protected abstract void Deinit();

        internal bool IsNavigable => ActiveAndEnabled && Navigable;

        private protected virtual GestureSpace PointerSpace => GestureSpace.Root;
    }

    internal interface IInteractable : IEventTargetProvider, IEventTarget, IGestureRecognizer, INavigationNode { }

    /// <summary>
    /// Triggers pointer-based gesture events (e.g. hover, click, drag, etc.) on the attached <see cref="UIBlock"/>
    /// </summary>
    /// <seealso cref="Gesture.OnClick"/>
    /// <seealso cref="Gesture.OnPress"/>
    /// <seealso cref="Gesture.OnRelease"/>
    /// <seealso cref="Gesture.OnHover"/>
    /// <seealso cref="Gesture.OnUnhover"/>
    /// <seealso cref="Gesture.OnMove"/>
    /// <seealso cref="Gesture.OnDrag"/>
    /// <seealso cref="Gesture.OnCancel"/>

    [AddComponentMenu("Nova/Interactable")]
    [HelpURL("https://novaui.io/manual/InputOverview.html#interactable--scroller")]
    public sealed class Interactable : GestureRecognizer, IInteractable
    {
        [SerializeField, NotKeyable]
        private ThreeD<bool> draggable = false;

        /// <summary>
        /// Acts as a bit-mask indicating which axes can trigger drag events once a "drag threshold" is surpassed.
        /// </summary>
        /// <remarks>if <c><see cref="Draggable"/>[axis]</c> is <c><see langword="false"/></c>, the "drag threshold" along that <c>axis</c> is infinite and will never trigger a drag event.</remarks>
        /// <seealso cref="GestureRecognizer.DragThreshold"/>
        /// <seealso cref="GestureRecognizer.LowAccuracyDragThreshold"/>
        /// <seealso cref="Gesture.OnDrag"/>
        public ThreeD<bool> Draggable
        {
            get
            {
                return draggable;
            }
            set
            {
                if (draggable.Equals(value))
                {
                    return;
                }

                if (isActiveAndEnabled)
                {
                    if (draggable.Any(true) && value.All(false))
                    {
                        UIBlock.InputTarget.ClearGestureRecognizer();
                    }

                    if (draggable.All(false) && value.Any(true))
                    {
                        UIBlock.InputTarget.SetGestureRecognizer(this);
                    }
                }

                draggable = value;
            }
        }

        /// <summary>
        /// The coordinate space to use when tracking gesture positions across frames.
        /// </summary>
        /// <remarks>
        /// For scenarios where the local position, local rotation, or local scale of 
        /// the <see cref="UIBlock"/>.<see cref="UIBlock.Root">Root</see> may change 
        /// while a gesture is active, this should be set to <see cref="GestureSpace.World"/>. 
        /// For most other cases, <see cref="GestureSpace.Root"/> is recommended.
        /// </remarks>
        [SerializeField]
        public GestureSpace GestureSpace = GestureSpace.Root;

        private protected override GestureSpace PointerSpace => GestureSpace;

        [System.NonSerialized, HideInInspector]
        private Dictionary<uint, DragPoint> dragStartPoints = new Dictionary<uint, DragPoint>();

        private protected override void Init()
        {
            UIBlock.RegisterEventTargetProvider(this);

            if (Draggable.Any(true))
            {
                UIBlock.InputTarget.SetGestureRecognizer(this);
            }

            UIBlock.InputTarget.SetNavigationNode(this);
            UIBlock.InputTarget.OnPointerInputChanged += HandlePointerInput;
            UIBlock.InputTarget.OnInputCanceled += InputCanceledHandler;
        }

        private protected override void Deinit()
        {
            UIBlock.UnregisterEventTargetProvider(this);

            if (Draggable.Any(true))
            {
                UIBlock.InputTarget.ClearGestureRecognizer();
            }

            UIBlock.InputTarget.ClearNavigationNode();
            UIBlock.InputTarget.OnPointerInputChanged -= HandlePointerInput;
            UIBlock.InputTarget.OnInputCanceled -= InputCanceledHandler;
        }

        private protected override void ProcessUniqueGesture(ref RawInput.OnChanged<bool> evt, ref InputState inputState, ref Vector3 currentPosRootSpace, ref Matrix4x4 rootToWorld)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Input<bool>? currentInput = evt.Current;
            Input<bool> current = currentInput.GetValueOrDefault();
            Input<bool>? previousInput = evt.Previous;
            Input<bool> previous = previousInput.GetValueOrDefault();

            bool valid = currentInput.HasValue;
            bool hitPointChanged = inputState.PreviousPositionRootSpace != currentPosRootSpace;

            if (valid && hitPointChanged && inputState.Pressed && current.GestureDetected)
            {
                Vector3 previousPos = previousInput.HasValue ? previous.HitPoint : current.HitPoint;
                Vector3 previousPosRootSpace = previousInput.HasValue ? inputState.PreviousPositionRootSpace : currentPosRootSpace;
                Drag(ref inputState, ref previousPos, ref current.HitPoint, ref previousPosRootSpace, ref currentPosRootSpace, ref rootToWorld, ref evt.Interaction);
            }
        }

        private protected override void Released(ref InputState state, ref Gesture.OnRelease evt)
        {
            dragStartPoints.Remove(evt.Interaction.ControlID);
        }

        private protected override void Canceled(ref Internal.Interaction interaction)
        {
            dragStartPoints.Remove(interaction.ID);
        }

        private void Drag(ref InputState state, ref Vector3 previousWorldPosition, ref Vector3 worldPosition, ref Vector3 previousRootSpacePosition, ref Vector3 rootSpacePosition, ref Matrix4x4 rootToWorld, ref Internal.Interaction interaction)
        {
            if (!state.Dragged)
            {
                dragStartPoints.Add(interaction.ID, new DragPoint()
                {
                    WorldPosition = previousWorldPosition,
                    RootSpacePosition = previousRootSpacePosition,
                });

                state.Dragged = true;
            }

            DragPoint dragPoint = dragStartPoints[interaction.ID];

            Gesture.Positions rawPositions = new Gesture.Positions()
            {
                Start = dragPoint.WorldPosition,
                Previous = previousWorldPosition,
                Current = worldPosition
            };

            Gesture.Positions adjustedPositions = rawPositions;

            if (PointerSpace == GestureSpace.Root)
            {
                adjustedPositions = new Gesture.Positions()
                {
                    Start = rootToWorld.MultiplyPoint(dragPoint.RootSpacePosition),
                    Previous = rootToWorld.MultiplyPoint(previousRootSpacePosition),
                    Current = worldPosition
                };
            }


            UIBlock.FireEvent(Gesture.Drag(interaction.ToPublic(), UIBlock, ref rawPositions, ref adjustedPositions, Draggable));
        }

        GestureState IGestureRecognizer.TryRecognizeGesture<T>(Ray startRay, Input<T> start, Input<T> sample, Transform top)
        {
            GestureState state = GestureState.None;

            if (typeof(T) != typeof(bool))
            {
                return state;
            }

            Vector3 dragAxis = Util.Mask(Draggable);

            if (dragAxis != Vector3.zero)
            {
                Vector3 dragAxisWorldSpace = UIBlock.transform.TransformDirection(dragAxis.normalized);
                Vector3 gestureNormal = startRay.direction;
                Vector3 gestureStartPoint = startRay.origin;
                Vector3 axis = Vector3.Cross(gestureNormal, dragAxisWorldSpace);

                if (axis == Vector3.zero)
                {
                    // Just pick a non draggable axis
                    dragAxis = !Draggable.X ? Vector3.right : !Draggable.Y ? Vector3.up : Vector3.forward;
                    gestureNormal = UIBlock.transform.TransformDirection(dragAxis);
                    gestureStartPoint = start.HitPoint;
                    axis = Vector3.Cross(gestureNormal, dragAxisWorldSpace);
                }

                Vector3 translationDirection = Math.ApproximatelyZeroToZero(sample.HitPoint - gestureStartPoint);
                float degrees = Math.AngleBetweenAroundAxis(gestureNormal, translationDirection, axis);
                degrees = Mathf.Min(degrees, 180 - degrees);

                state = degrees >= GetDragThreshold(ref sample) ? GestureState.DetectedHighPri : GestureState.Pending;
            }

            return state;
        }

        bool INavigationNode.Enabled => IsNavigable;
        bool INavigationNode.CaptureInput => OnSelect == SelectBehavior.FireEvents;
        bool INavigationNode.ScopeNavigation => OnSelect == SelectBehavior.ScopeNavigation;
        bool INavigationNode.UseTargetNotFoundFallback(Vector3 direction) => true;
        bool INavigationNode.TryGetNext(Vector3 direction, out IUIBlock toUIBlock)
        {
            toUIBlock = null;

            if (!IsNavigable)
            {
                return false;
            }

            return Navigation.TryGetNavigation(direction, out toUIBlock);
        }

        bool INavigationNode.TryHandleScopedMove(IUIBlock previousChild, IUIBlock nextChild, Vector3 direction) => false;
        void INavigationNode.EnsureInView(IUIBlock descendant) { }

        private float GetDragThreshold<T>(ref Input<T> input) where T : unmanaged, System.IEquatable<T> => input.Noisy ? LowAccuracyDragThreshold : DragThreshold;

        bool IGestureRecognizer.ObstructDrags => ObstructDrags;

        System.Type IEventTargetProvider.BaseTargetableType => typeof(UIBlock);

        bool IEventTargetProvider.TryGetTarget(IEventTarget receiver, System.Type _, out IEventTarget target)
        {
            target = UIBlock;
            return true;
        }

        private struct DragPoint
        {
            public Vector3 WorldPosition;
            public Vector3 RootSpacePosition;
        }
    }
}
