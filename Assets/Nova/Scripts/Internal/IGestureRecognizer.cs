// Copyright (c) Supernova Technologies LLC
using System;
using UnityEngine;

namespace Nova.Internal
{
    internal enum GestureState
    {
        None = 0, // No state and won't capture gesture
        Pending = 1, // Not enough data to confirm gesture
        DetectedLowPri = 2, // Gesture recognized but not top priority
        DetectedHighPri = 3, // Gesture recognized and top priority
        Occurring = 4, // Receiving new input mid gesture
    }

    internal static class GestureStateExtensions
    {
        public static bool IsRequested(this GestureState state)
        {
            return state != GestureState.None;
        }

        public static bool IsCaptured(this GestureState state)
        {
            return state.IsDetected() || state == GestureState.Occurring;
        }

        public static bool IsDetected(this GestureState state)
        {
            return state == GestureState.DetectedHighPri || state == GestureState.DetectedLowPri;
        }

        public static int Level(this GestureState state)
        {
            return (int)state;
        }
    }

    internal interface IGestureRecognizer
    {
        bool ObstructDrags { get; }

        GestureState TryRecognizeGesture<T>(Ray startRay, Input<T> start, Input<T> sample, Transform top) where T : unmanaged, IEquatable<T>;
    }
}
