// Copyright (c) Supernova Technologies LLC
using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.Events;

namespace Nova.Editor.Utilities
{
    internal class BoundsAnim : BaseAnimValueNonAlloc<Bounds>
    {
        private const float LargeEpsilon = 0.01f;

        public Matrix4x4 StartMatrix;

        private Matrix4x4 targetMatrix;
        private Matrix4x4 inverseTargetMatrix;
        public Matrix4x4 TargetMatrix
        {
            get
            {
                return targetMatrix;
            }
            set
            {
                targetMatrix = value;
                inverseTargetMatrix = value.inverse;
            }
        }

        public float PercentDone => lerpPosition;

        private Vector3 StartPosition => StartToTarget.MultiplyPoint(start.center);
        private Matrix4x4 StartToTarget => inverseTargetMatrix * StartMatrix;

        public BoundsAnim(Bounds start, Matrix4x4 startMatrix, UnityAction callback) : base(start, callback)
        {
            StartMatrix = startMatrix;
        }

        public new bool isAnimating
        {
            get
            {
                Bounds bounds = GetValue();

                if ((target.size - bounds.size).magnitude <= LargeEpsilon &&
                    (target.center - bounds.center).magnitude <= LargeEpsilon)
                {
                    return false;
                }

                return base.isAnimating;
            }
        }

        protected override bool AreEqual(Bounds a, Bounds b)
        {
            return base.AreEqual(a, b) && targetMatrix.Equals(StartMatrix);
        }

        protected override Bounds GetValue()
        {
            Vector3 position = Vector3.Lerp(StartPosition, target.center, lerpPosition);
            Vector3 size = Vector3.Lerp(start.size, target.size, lerpPosition);

            return new Bounds(position, size);
        }

        public void Reset()
        {
            BeginAnimating(target, start);
        }
    }
}
