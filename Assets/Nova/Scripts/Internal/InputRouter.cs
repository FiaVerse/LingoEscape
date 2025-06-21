// Copyright (c) Supernova Technologies LLC
//#define DEBUG_GESTURES

using Nova.Internal.Collections;
using Nova.Internal.Utilities;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova.Internal
{
    internal interface IBlockHit
    {
        IUIBlock UIBlock { get; set; }
        Vector3 Position { get; set; }
        Vector3 Normal { get; set; }
    }

    internal class GestureDetector<THit> where THit : struct, System.IEquatable<THit>, IBlockHit
    {
        private struct GestureHandle
        {
            public UID<InputID> ID;
            public int SampleCount;
            public Ray Origin;
            public GestureState Gesture;
        }

        private GestureHandle[] gestureHandles = new GestureHandle[InputRouter.MaxControls];
        private List<IUIBlock>[] gestureRecognizers = new List<IUIBlock>[InputRouter.MaxControls];

        public GestureDetector()
        {
            for (int i = 0; i < InputRouter.MaxControls; ++i)
            {
                gestureRecognizers[i] = new List<IUIBlock>();
            }
        }

        public void Begin<T>(Interaction source, Input<T> input, ReadOnlyList<THit> recognizers) where T : unmanaged, System.IEquatable<T>
        {
            ClearSamples(source.ID);

            List<IUIBlock> currentRecognizers = gestureRecognizers[source.ID];

            for (int i = 0; i < recognizers.Count; ++i)
            {
                currentRecognizers.Add(recognizers[i].UIBlock);
            }

            gestureHandles[source.ID] = new GestureHandle()
            {
                ID = StructHandle<InputID>.Storage<Input<T>>.Add(input),
                SampleCount = 0,
                Origin = source.Ray,
                Gesture = currentRecognizers.Count == 0 ? GestureState.None : GestureState.Pending
            };

        }

        public void ClearSamples(uint sourceID)
        {
            UID<InputID> currentID = gestureHandles[sourceID].ID;

            if (currentID.IsValid)
            {
                StructHandle<InputID>.Remove(currentID);
            }

            gestureHandles[sourceID] = default;

            List<IUIBlock> currentRecognizers = gestureRecognizers[sourceID];
            currentRecognizers.Clear();

        }

        public GestureState GetGestureState(uint sourceID) => gestureHandles[sourceID].Gesture;

        public GestureState TrySample<T>(uint sourceID, Input<T> sample, out IUIBlock gesturedBlock) where T : unmanaged, System.IEquatable<T>
        {
            List<IUIBlock> currentRecognizers = gestureRecognizers[sourceID];
            GestureHandle handle = gestureHandles[sourceID];

            gesturedBlock = null;

            if (currentRecognizers.Count == 0 || !handle.Gesture.IsRequested())
            {

                return GestureState.None;
            }

            if (currentRecognizers.Count == 1 && handle.Gesture.IsDetected())
            {
                gesturedBlock = currentRecognizers[0];
                return handle.Gesture;
            }

            Input<T> start = StructHandle<InputID>.Storage<Input<T>>.Get(handle.ID);

            GestureState maxGesture = GestureState.None;

            Transform top = currentRecognizers.Count > 0 ? currentRecognizers[0].Transform : null;

            for (int i = currentRecognizers.Count - 1; i >= 0; --i)
            {
                IUIBlock target = currentRecognizers[i];

                if (target == null)
                {
                    currentRecognizers.RemoveAt(i);
                    continue;
                }

                IGestureRecognizer recognizer = target.InputTarget.GestureRecognizer;

                bool keep = false;
                GestureState recognizerState = GestureState.None;

                if (recognizer != null)
                {
                    GestureState state = recognizer.TryRecognizeGesture(handle.Origin, start, sample, top);

                    if (state.IsRequested())
                    {
                        keep = true;

                        maxGesture = Max(maxGesture, state);
                        recognizerState = Max(recognizerState, state);
                    }
                }

                if (!keep || recognizerState != Max(recognizerState, maxGesture))
                {

                    currentRecognizers.RemoveAt(i);
                }
            }

            handle.Gesture = maxGesture;
            handle.SampleCount++;

            if (handle.Gesture.IsDetected())
            {
                currentRecognizers.RemoveRange(1, currentRecognizers.Count - 1);
            }


            gestureHandles[sourceID] = handle;

            gesturedBlock = currentRecognizers.Count == 1 && handle.Gesture.IsCaptured() ? currentRecognizers[0] : null;
            return gesturedBlock != null ? handle.Gesture : GestureState.None;
        }

        private static GestureState Max(GestureState a, GestureState b)
        {
            return a.Level() > b.Level() ? a : b;
        }
    }

    internal class InputRouter
    {
        public const int MaxControls = 31;
    }

    internal enum InteractionType
    {
        Default,
        RequiresHit,
        Gesturable
    }

    internal class InputState<THit> where THit : struct, System.IEquatable<THit>, IBlockHit
    {
        public class InputRouter : Internal.InputRouter
        {
            private struct Recipient
            {
                private THit? localHit;
                private THit worldHit;
                public THit HitInWorldSpace
                {
                    get
                    {
                        THit hit = worldHit;

                        if (localHit.HasValue)
                        {
                            THit local = localHit.Value;
                            IUIBlock uiBlock = local.UIBlock;
                            Transform transform = (uiBlock as MonoBehaviour) != null ? uiBlock.Transform : null;

                            if (transform != null)
                            {
                                hit.Normal = transform.TransformDirection(local.Normal);
                                hit.Position = transform.TransformPoint(local.Position);
                            }
                        }

                        return hit;
                    }
                    set
                    {
                        worldHit = value;

                        IUIBlock uiBlock = worldHit.UIBlock;
                        Transform transform = (uiBlock as MonoBehaviour) != null ? uiBlock.Transform : null;

                        if (transform == null)
                        {
                            localHit = null;
                            return;
                        }

                        THit local = worldHit;

                        local.Normal = transform.InverseTransformDirection(worldHit.Normal);
                        local.Position = transform.InverseTransformPoint(worldHit.Position);

                        localHit = local;
                    }
                }

                public bool HasValue;
            }

            private struct InputHandle
            {
                public UID<InputID> ID;
                public Vector3 Point;

                public static InputHandle Invalid => new InputHandle() { ID = UID<InputID>.Invalid, Point = Math.Vector3_NaN };
            }

            [System.NonSerialized]
            private static BitField32 pointerHits = new BitField32(0);

            [System.NonSerialized]
            private static BitField32 waitingForRelease = new BitField32(0);

            [System.NonSerialized]
            private static Recipient[] recipients = null;
            [System.NonSerialized]
            private static InputHandle[] inputHandles = null;
            [System.NonSerialized]
            private static GestureDetector<THit> gestureDetector = null;

            public void Update<T>(Interaction source, T input, ReadOnlyList<THit> sortedHits, InteractionType interaction, bool noisy) where T : unmanaged, System.IEquatable<T>
            {
                uint sourceID = source.ID;
                int sourceIndex = (int)sourceID;

                THit currentHit = sortedHits.Count > 0 ? sortedHits[0] : default(THit);

                Recipient previousRecipient = recipients[sourceIndex];
                THit previousHit = previousRecipient.HitInWorldSpace;
                IUIBlock previousHitBlock = previousHit.UIBlock;
                bool hadPreviousHitBlock = previousHitBlock != null;

                InputHandle previousHandle = inputHandles[sourceIndex];
                bool inputPreviouslyHadValue = previousRecipient.HasValue && interaction != InteractionType.RequiresHit;

                StructHandle<InputID>.Remove(previousHandle.ID);

                bool inputHasValue = !input.Equals(default(T));

                if (waitingForRelease.IsSet(sourceIndex))
                {
                    if (inputHasValue && interaction != InteractionType.RequiresHit)
                    {
                        return;
                    }

                    waitingForRelease.SetBits(sourceIndex, false);
                }

                if (inputPreviouslyHadValue != inputHasValue)
                {
                    // This is our clear event, so we dont want to
                    // wait if we get a cancel right after this
                    gestureDetector.ClearSamples(sourceID);
                }

                if (inputHasValue && hadPreviousHitBlock)
                {
                    currentHit.Normal = previousRecipient.HitInWorldSpace.Normal;
                }

                GestureState gesture = gestureDetector.GetGestureState(sourceID);

                Input<T>? previousBlockInput = hadPreviousHitBlock ? previousHitBlock.InputTarget.GetInput<T>(sourceID) : null;

                // update gesture if applicable
                if (interaction != InteractionType.Default && inputHasValue)
                {
                    Input<T> gestureSample = new Input<T>() { IsHit = true, UserInput = input, HitPoint = currentHit.Position, Noisy = noisy };

                    if (!inputPreviouslyHadValue)
                    {
                        gestureDetector.Begin(source, gestureSample, sortedHits);
                    }

                    GestureState updatedGesture = gestureDetector.TrySample(sourceID, gestureSample, out IUIBlock gesturedBlock);

                    if (updatedGesture.IsCaptured())
                    {
                        if (gesture.IsRequested() && hadPreviousHitBlock && gesturedBlock != previousHitBlock)
                        {
                            // Cancel old input
                            previousHitBlock.InputTarget.CancelInput(source);

                            // Act as if we've been continuously injecting input to the gestured block
                            previousHitBlock = gesturedBlock;

                            hadPreviousHitBlock = true;

                            // Begin the gesture from the previous position
                            // to ensure we get at least 2 gesture updates
                            gesturedBlock.InputTarget.SetInput<T>(source, new Input<T>() { IsHit = true, UserInput = input, HitPoint = previousHit.Position, GestureState = gesture, Noisy = noisy });
                        }
                        
                        gesture = updatedGesture;

                        currentHit.UIBlock = gesturedBlock;
                    }
                }

                bool currentBlockIsHit = true;

                // Update the input state of the previous hit. If the input value is non-gesturable but a "gesture"
                // is still being applied (e.g. a pointer down event, and then a drag event, no pointer up yet)
                // then we actually want to route the updated input value to the UI block that received the first event

                bool previousBlockIsHit = false;

                if (hadPreviousHitBlock && previousHitBlock != currentHit.UIBlock && previousBlockInput.HasValue)
                {
                    for (int i = 0; i < sortedHits.Count; ++i)
                    {
                        if (sortedHits[i].UIBlock == previousHitBlock)
                        {
                            previousBlockIsHit = true;
                            break;
                        }
                    }

                    if (inputPreviouslyHadValue)
                    {
                        // continue routing input to maintain gesture
                        currentHit.UIBlock = previousHitBlock;
                        currentBlockIsHit = previousBlockIsHit;
                    }
                    else if (!previousBlockIsHit || !inputHasValue)
                    {
                        previousHitBlock.InputTarget.SetInput<T>(source, new Input<T>() { IsHit = false, UserInput = default, HitPoint = currentHit.Position, Noisy = noisy });
                    }
                    else if (inputHasValue && currentHit.UIBlock != null)
                    {
                        previousHitBlock.InputTarget.CancelInput(source);
                    }
                }

                bool hasHitBlock = currentHit.UIBlock != null;

                if (hasHitBlock)
                {
                    Vector3 position = currentHit.Position;

                    if (pointerHits.Value == 0)
                    {
                        // send dummy point event to ensure state orders
                        currentHit.UIBlock.InputTarget.SetInput<T>(source, new Input<T>() { IsHit = true, UserInput = default, HitPoint = position, Noisy = noisy });
                    }

                    // update input state for new hit
                    currentHit.UIBlock.InputTarget.SetInput<T>(source, new Input<T>() { IsHit = currentBlockIsHit, UserInput = input, HitPoint = position, GestureState = gesture, Noisy = noisy });

                    // if, in the process of handling input, the UI Block was disabled, we need to update our internal state
                    if (!currentHit.UIBlock.InputTarget.GetInput<T>(source.ID).HasValue)
                    {
                        hasHitBlock = false;
                        currentHit.UIBlock = null;
                        gestureDetector.ClearSamples(source.ID);
                        waitingForRelease.SetBits(sourceIndex, inputHasValue);
                    }
                }
                else if (inputHasValue)
                {
                    waitingForRelease.SetBits(sourceIndex, true);
                }

                pointerHits.SetBits(sourceIndex, hasHitBlock);

                recipients[sourceID] = new Recipient() { HitInWorldSpace = currentHit, HasValue = inputHasValue };

                inputHandles[sourceIndex] = new InputHandle()
                {
                    ID = StructHandle<InputID>.Storage<T>.Add(input),
                    Point = currentHit.Position,
                };
            }

            public void Cancel(Interaction source)
            {
                if (recipients[source.ID].HitInWorldSpace.UIBlock != null)
                {
                    IUIBlock activeHitBlock = recipients[source.ID].HitInWorldSpace.UIBlock;
                    activeHitBlock.InputTarget.CancelInput(source);

                    recipients[source.ID] = default;
                }

                int sourceIndex = (int)source.ID;

                pointerHits.SetBits(sourceIndex, false);
                waitingForRelease.SetBits(sourceIndex, false);

                StructHandle<InputID>.Remove(inputHandles[source.ID].ID);
                inputHandles[source.ID] = InputHandle.Invalid;

                gestureDetector.ClearSamples(source.ID);
            }

            public bool TryGetCurrentCapturingInput<T>(uint sourceID, out THit lastHit) where T : unmanaged, System.IEquatable<T>
            {
                THit hit = recipients[sourceID].HitInWorldSpace;

                if (hit.UIBlock != null)
                {
                    lastHit = hit;
                    Input<T>? input = hit.UIBlock.InputTarget.GetInput<T>(sourceID);

                    return input.HasValue && !input.Value.UserInput.Equals(default);
                }

                lastHit = default;
                return false;
            }

            public bool TryGetLatestReceiver<T>(uint sourceID, out THit lastHit) where T : unmanaged, System.IEquatable<T>
            {
                THit hit = recipients[sourceID].HitInWorldSpace;

                if (hit.UIBlock != null && hit.UIBlock.ActiveInHierarchy)
                {
                    lastHit = hit;
                    Input<T>? input = hit.UIBlock.InputTarget.GetInput<T>(sourceID);

                    return input.HasValue;
                }

                lastHit = default;
                return false;
            }

            public void Init()
            {
                // workaround to avoid code stripping in IL2CPP Faster (smaller) builds
                ((StructHandle<InputID>.IStructWrapper)new StructHandle<InputID>.Storage<Input<bool>>()).Remove(default(UID<InputID>));
                ((StructHandle<InputID>.IStructWrapper)new StructHandle<InputID>.Storage<Input<UniqueValue<Vector3>>>()).Remove(default(UID<InputID>));

                pointerHits.Clear();
                waitingForRelease.Clear();
                inputHandles = new InputHandle[MaxControls];
                recipients = new Recipient[MaxControls];
                gestureDetector = new GestureDetector<THit>();
            }
        }
    }

    internal struct InputID { }

    internal struct Input<T> : System.IEquatable<Input<T>> where T : unmanaged, System.IEquatable<T>
    {
        public bool IsHit; // means an input ray is intersecting
        public T UserInput; // the input value associated with the Ray
        public Vector3 HitPoint; // the world position of the collision point
        public GestureState GestureState; // indicates the element has succesfully captured the gesture event
        public bool Noisy; // indicates the noise level of the input device

        public bool GestureDetected => GestureState.IsDetected();

        public bool Equals(Input<T> other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case Input<T> input:
                    return this.Equals(input);
                default:
                    return false;
            }
        }

        public static bool operator ==(in Input<T> lhs, in Input<T> rhs)
        {
            return lhs.UserInput.Equals(rhs.UserInput) &&
                   lhs.IsHit == rhs.IsHit &&
                   Math.EqualsAccountForNaN(lhs.HitPoint, rhs.HitPoint);
        }

        public static bool operator !=(in Input<T> lhs, in Input<T> rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + IsHit.GetHashCode();
            hash = (hash * 7) + UserInput.GetHashCode();
            hash = (hash * 7) + HitPoint.GetHashCode();
            hash = (hash * 7) + GestureState.GetHashCode();
            hash = (hash * 7) + Noisy.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return $"Hit:{IsHit}, Value: {UserInput}, {HitPoint}";
        }

        public unsafe bool TryConvertIfSameType<U>(out Input<U>? convertedInput) where U : unmanaged, System.IEquatable<U>
        {
            if (typeof(U) != typeof(T))
            {
                convertedInput = null;
                return false;
            }

            convertedInput = UnsafeUtility.As<Input<T>, Input<U>>(ref this);
            return true;
        }
    }

    internal readonly struct Interaction
    {
        public const uint UninitializedSourceID = uint.MaxValue;

        public readonly uint ID;
        public readonly Ray Ray;
        public readonly object UserData;

        public bool Initialized => ID != UninitializedSourceID;

        public static readonly Interaction Uninitialized = new Interaction(UninitializedSourceID);

        public Interaction(Ray ray, uint id = 0, object userData = null)
        {
            Ray = ray;
            ID = id;
            UserData = userData;
        }

        public Interaction(uint id)
        {
            ID = id;
            Ray = new Ray(Math.float3_NaN, Math.float3_NaN);
            UserData = null;
        }
    }

    /// <summary>
    /// A wrapper around a struct type that represents a unique input delta, means equality comparison will only be true
    /// if both values are "empty" otherwise, even if values match, x.Equals(y) == false.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal readonly struct UniqueValue<T> : System.IEquatable<UniqueValue<T>> where T : struct, System.IEquatable<T>
    {
        public readonly T Amount;

        public bool Equals(UniqueValue<T> other) => Amount.Equals(default) && other.Amount.Equals(default) ? true : false;

        public UniqueValue(ref T value)
        {
            Amount = value;
        }
    }
}
