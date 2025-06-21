// Copyright (c) Supernova Technologies LLC
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct UIBlock3DData : IEquatable<UIBlock3DData>
    {
        [SerializeField]
        public Color Color;
        [SerializeField]
        public Length CornerRadius;
        [SerializeField]
        public Length EdgeRadius;

        public static bool operator ==(UIBlock3DData lhs, UIBlock3DData rhs)
        {
            return
                lhs.Color.Equals(rhs.Color) &&
                lhs.CornerRadius.Equals(rhs.CornerRadius) &&
                lhs.EdgeRadius.Equals(rhs.EdgeRadius);
        }
        public static bool operator !=(UIBlock3DData lhs, UIBlock3DData rhs) => !(rhs == lhs);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Color.GetHashCode();
            hash = (hash * 7) + CornerRadius.GetHashCode();
            hash = (hash * 7) + EdgeRadius.GetHashCode();
            return hash;
        }

        public override bool Equals(object other)
        {
            if (other is UIBlock3DData asType)
            {
                return this == asType;
            }

            return false;
        }

        public bool Equals(UIBlock3DData other) => this == other;

        internal Calculated Calc(Vector3 size)
        {
            return new Calculated(ref CornerRadius, ref EdgeRadius, ref size);
        }

        [Obfuscation]
        internal readonly struct Calculated
        {
            public readonly Length.Calculated CornerRadius;
            public readonly Length.Calculated EdgeRadius;

            public Calculated(ref Length cornerRadius, ref Length edgeRadius, ref Vector3 size)
            {
                float minXY = 0.5f * Mathf.Min(size.x, size.y);
                var cornerRadiusInternal = new Internal.Length.Calculated(cornerRadius.ToInternal(), new Internal.Length.MinMax()
                {
                    Min = 0f,
                    Max = minXY
                }, minXY);
                CornerRadius = cornerRadiusInternal.ToPublic();

                float maxEdge = Mathf.Min(CornerRadius.Value, 0.5f * size.z);
                var edgeRadiusInternal = new Internal.Length.Calculated(edgeRadius.ToInternal(), new Internal.Length.MinMax()
                {
                    Min = 0f,
                    Max = maxEdge
                }, maxEdge);
                EdgeRadius = edgeRadiusInternal.ToPublic();
            }
        }

        internal static readonly UIBlock3DData Default = new UIBlock3DData()
        {
            Color = Color.grey,
            CornerRadius = Length.Zero,
            EdgeRadius = Length.Zero,
        };
    }
}