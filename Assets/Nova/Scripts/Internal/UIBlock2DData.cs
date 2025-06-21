// Copyright (c) Supernova Technologies LLC
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct UIBlock2DData
    {
        [SerializeField]
        public Color Color;
        [SerializeField]
        public Length CornerRadius;
        [SerializeField]
        public RadialFill RadialFill;
        [SerializeField]
        public RadialGradient Gradient;
        [SerializeField]
        public Border Border;
        [SerializeField]
        public Shadow Shadow;
        [SerializeField]
        public ImageData Image;
        [SerializeField]
        public bool SoftenEdges;
        [SerializeField]
        public bool FillEnabled;

        internal Calculated Calc(Vector2 relativeTo)
        {
            return new Calculated(this, relativeTo);
        }

        [Obfuscation]
        internal readonly struct Calculated
        {
            public readonly Length.Calculated CornerRadius;
            public readonly Border.Calculated Border;
            public readonly RadialGradient.Calculated Gradient;
            public readonly Shadow.Calculated Shadow;
            public readonly RadialFill.Calculated RadialFill;

            internal Calculated(UIBlock2DData data, Vector2 relativeTo)
            {
                float relative1D = math.cmin(relativeTo) * 0.5f;

                var cornerRadius = new Internal.Length.Calculated(data.CornerRadius.ToInternal(), Internal.Length.MinMax.Positive, relative1D);
                CornerRadius = cornerRadius.ToPublic();

                Border = data.Border.Calc(relative1D);
                Gradient = data.Gradient.Calc(relativeTo);
                Shadow = data.Shadow.Calc(relativeTo);
                RadialFill = data.RadialFill.Calc(relativeTo);
            }
        }

        internal static readonly UIBlock2DData Default = new UIBlock2DData()
        {
            Color = Color.grey,
            CornerRadius = Length.Zero,
            RadialFill = RadialFill.Default,
            Border = Border.Default,
            Shadow = Shadow.Default,
            Gradient = RadialGradient.Default,
            Image = ImageData.Default,
            SoftenEdges = true,
            FillEnabled = true,
        };
    }
}