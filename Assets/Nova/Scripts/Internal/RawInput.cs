// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using UnityEngine;

namespace Nova.Internal
{
    internal static partial class RawInput
    {
        public delegate void PointerInputChangeEvent(ref OnChanged<bool> evt);
        public delegate void VectorInputChangeEvent(ref OnChanged<UniqueValue<Vector3>> evt);
        
        public delegate void InputCanceledEvent(ref OnCanceled evt);

        internal static OnCanceled Cancel(IUIBlock receiver, Interaction source) => new OnCanceled() { Target = receiver, Receiver = receiver, Source = source };
        internal static OnChanged<TInput> Change<TInput>(IUIBlock receiver, Input<TInput>? previous, Input<TInput>? current, Interaction source) where TInput : unmanaged, System.IEquatable<TInput>
        {
            return new OnChanged<TInput>()
            {
                Target = receiver,
                Receiver = receiver,
                Previous = previous,
                Current = current,
                Interaction = source
            };
        }

        public struct OnCanceled
        {
            public Interaction Source;

            public IUIBlock Target { get; set; }
            public IUIBlock Receiver { get; set; }
        }

        public struct OnChanged<TInput> where TInput : unmanaged, System.IEquatable<TInput>
        {
            public Interaction Interaction;

            public Input<TInput>? Previous;
            public Input<TInput>? Current;

            public IUIBlock Target { get; set; }
            public IUIBlock Receiver { get; set; }

            public Vector3 GetHitLocalTranslation(Vector3 previousWorldPos)
            {
                if (!Previous.HasValue || !Current.HasValue || Receiver == null)
                {
                    return Vector3.zero;
                }

                Vector3 translation = Receiver.Transform.InverseTransformPoint(Current.Value.HitPoint) - Receiver.Transform.InverseTransformPoint(previousWorldPos);

                return Math.ApproximatelyZeroToZero(translation);
            }
        }
    }
}
