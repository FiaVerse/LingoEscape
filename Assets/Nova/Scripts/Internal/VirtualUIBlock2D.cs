// Copyright (c) Supernova Technologies LLC
using Nova.Internal;
using Nova.Internal.Rendering;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova
{
    
    [Serializable]
    internal class VirtualUIBlock2D : VirtualUIBlock, IUIBlock2D
    {
        [SerializeField]
        private UIBlock2DData visuals = UIBlock2DData.Default;

        ref Internal.UIBlock2DData IRenderBlock<Internal.UIBlock2DData>.RenderData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<UIBlock2DData, Internal.UIBlock2DData>(ref visuals);
        }

        public ref RadialGradient Gradient
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<Internal.RadialGradient, RadialGradient>(ref RenderingDataStore.Instance.Access(this).Gradient);
        }

        public ref Border Border
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<Internal.Border, Border>(ref RenderingDataStore.Instance.Access(this).Border);
        }

        public ref Shadow Shadow
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<Internal.Shadow, Shadow>(ref RenderingDataStore.Instance.Access(this).Shadow);
        }

        public ref Surface Surface
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<Internal.Surface, Surface>(ref RenderingDataStore.Instance.AccessSurface(this));
        }

        public ref Length CornerRadius
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref RenderingDataStore.Instance.Access(this).CornerRadius.ToPublic();
        }

        public Color Color
        {
            get
            {
                return RenderingDataStore.Instance.Access(this).Color;
            }
            set
            {
                RenderingDataStore.Instance.Access(this).Color = value;
            }
        }

        internal override VirtualBlock Clone()
        {
            VirtualUIBlock2D clone = new VirtualUIBlock2D();

            if (Self.IsRegistered)
            {
                clone.CloneFromDataStore(ID);
                clone.Visible = Visible;
            }
            else
            {
                CopyBasePropertiesTo(clone);
                clone.visuals = visuals;
            }

            return clone;
        }
    }
}