// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Internal
{
    internal class InputModule<T> : IInputTarget where T : MonoBehaviour, IUIBlock
    {
        private T owner;

        private INavigationNode navigationNode = null;
        private IGestureRecognizer gestureRecognizer = null;

        private Dictionary<uint, UID<InputID>> inputStateIDs = null;
        private HashList<uint> inputSourceIDs = null;
        private Dictionary<uint, Interaction> inputSources = null;

        public event RawInput.PointerInputChangeEvent OnPointerInputChanged = null;
        public event RawInput.VectorInputChangeEvent OnVector3InputChanged = null;
        public event RawInput.InputCanceledEvent OnInputCanceled = null;

        bool IInputTarget.CaptureNavInput
        {
            get
            {
                INavigationNode nav = ((IInputTarget)this).Nav;

                return nav != null && nav.CaptureInput;
            }
        }

        bool IInputTarget.ScopeNavigation
        {
            get
            {
                INavigationNode nav = ((IInputTarget)this).Nav;

                return nav != null && nav.ScopeNavigation;
            }
        }

        INavigationNode IInputTarget.Nav => navigationNode == null || !navigationNode.Enabled ? null : navigationNode;
        IGestureRecognizer IInputTarget.GestureRecognizer => gestureRecognizer;

        void IInputTarget.SetGestureRecognizer(IGestureRecognizer recognizer)
        {
            gestureRecognizer = recognizer;
        }

        void IInputTarget.ClearGestureRecognizer()
        {
            gestureRecognizer = null;
        }

        void IInputTarget.SetNavigationNode(INavigationNode node)
        {
            navigationNode = node;
        }

        void IInputTarget.ClearNavigationNode()
        {
            navigationNode = null;
        }

        void IInputTarget.CancelInput(Interaction source)
        {
            if (inputStateIDs == null || inputSources == null)
            {
                return;
            }

            RawInput.OnCanceled canceled = RawInput.Cancel(owner, source);

            if (!inputStateIDs.TryGetValue(source.ID, out UID<InputID> inputStateID) || !inputStateID.IsValid)
            {
                OnInputCanceled?.Invoke(ref canceled);

                return;
            }

            StructHandle<InputID>.Remove(inputStateID);

            if (inputSources.Remove(source.ID))
            {
                inputSourceIDs.Remove(source.ID);
            }

            inputStateIDs.Remove(source.ID);

            OnInputCanceled?.Invoke(ref canceled);
        }

        void IInputTarget.CancelInput()
        {
            if (inputSources == null)
            {
                return;
            }

            ReadOnlyList<uint> activeSourceIDs = inputSourceIDs.List;

            while (activeSourceIDs.Count > 0)
            {
                uint id = activeSourceIDs[activeSourceIDs.Count - 1];

                ((IInputTarget)this).CancelInput(inputSources[id]);
            }
        }

        bool IInputTarget.CapturesInput<TInput>()
        {
            // This type matching is a bit gross here, but basically
            // the goal is just to fast path the input types we're actually
            // using without needing to rework the entire RawInput stack

            Type inputType = typeof(TInput);

            if (inputType == typeof(bool))
            {
                return OnPointerInputChanged != null;
            }

            if (inputType == typeof(UniqueValue<Vector3>))
            {
                return OnVector3InputChanged != null;
            }

            return false;
        }

        void IInputTarget.SetInput<TInput>(Interaction source, Input<TInput>? input)
        {
            if (inputStateIDs == null)
            {
                inputStateIDs = new Dictionary<uint, UID<InputID>>(1);
                inputSources = new Dictionary<uint, Interaction>(1);
                inputSourceIDs = new HashList<uint>(1);
            }

            Input<TInput>? previous = null;

            uint sourceID = source.ID;

            if (inputStateIDs.TryGetValue(sourceID, out UID<InputID> inputStateID) && inputStateID.IsValid)
            {
                // compare for equality
                if (StructHandle<InputID>.Storage<Input<TInput>>.TryGetValue(inputStateID, out Input<TInput> previousInput))
                {
                    if (input.Value == previousInput)
                    {
                        return;
                    }

                    previous = previousInput;

                    // same type removal
                    StructHandle<InputID>.Storage<Input<TInput>>.Remove(inputStateID);
                }
                else
                {
                    // untyped removal
                    StructHandle<InputID>.Remove(inputStateID);
                }
            }
            else if (!input.HasValue)
            {
                return;
            }

            if (input.HasValue)
            {
                inputStateIDs[sourceID] = StructHandle<InputID>.Storage<Input<TInput>>.Add(input.Value);
                inputSourceIDs.Add(sourceID);
                inputSources[sourceID] = source;
            }
            else
            {
                inputSources.Remove(sourceID);
                inputSourceIDs.Remove(sourceID);
                inputStateIDs[sourceID] = UID<InputID>.Invalid;
            }

            // This type matching is a bit gross here, but basically
            // the goal is just to fast path the input types we're actually
            // using without needing to rework the entire RawInput stack

            Type inputType = typeof(TInput);

            if (inputType == typeof(bool))
            {
                Input<bool>? previousPointer = null;
                Input<bool>? newPointer = null;

                if (previous.HasValue)
                {
                    _ = previous.Value.TryConvertIfSameType(out previousPointer);
                }

                if (input.HasValue)
                {
                    _ = input.Value.TryConvertIfSameType(out newPointer);
                }

                RawInput.OnChanged<bool> pointerChange = RawInput.Change(owner, previousPointer, newPointer, source);
                OnPointerInputChanged?.Invoke(ref pointerChange);

                return;
            }

            if (inputType == typeof(UniqueValue<Vector3>))
            {
                Input<UniqueValue<Vector3>>? previousVector = null;
                Input<UniqueValue<Vector3>>? newVector = null;

                if (previous.HasValue)
                {
                    _ = previous.Value.TryConvertIfSameType(out previousVector);
                }

                if (input.HasValue)
                {
                    _ = input.Value.TryConvertIfSameType(out newVector);
                }

                RawInput.OnChanged<UniqueValue<Vector3>> vectorChange = RawInput.Change(owner, previousVector, newVector, source);
                OnVector3InputChanged?.Invoke(ref vectorChange);

                return;
            }
        }

        Input<TInput>? IInputTarget.GetInput<TInput>(uint contextID)
        {
            if (inputStateIDs != null && inputStateIDs.TryGetValue(contextID, out UID<InputID> inputStateID) && inputStateID.IsValid)
            {
                return StructHandle<InputID>.Storage<Input<TInput>>.Get(inputStateID);
            }

            return null;
        }

        bool IInputTarget.TryGetInputSource(uint sourceID, out Interaction source)
        {
            if (inputSources == null)
            {
                source = default;
                return false;
            }

            return inputSources.TryGetValue(sourceID, out source);
        }

        public InputModule(T owner)
        {
            this.owner = owner;
        }
    }
}
