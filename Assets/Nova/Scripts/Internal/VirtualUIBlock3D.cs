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
    internal class VirtualUIBlock3D : VirtualUIBlock, IUIBlock3D
    {
        [SerializeField]
        private UIBlock3DData visuals = UIBlock3DData.Default;
        ref Internal.UIBlock3DData IRenderBlock<Internal.UIBlock3DData>.RenderData
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<UIBlock3DData, Internal.UIBlock3DData>(ref visuals);
        }

        public ref Color Color
        {
            get
            {
                return ref RenderingDataStore.Instance.Access(this).Color;
            }
        }

        public ref Surface Surface
        {
            get
            {
                return ref UnsafeUtility.As<Internal.Surface, Surface>(ref RenderingDataStore.Instance.AccessSurface(this));
            }
        }

        public ref Length CornerRadius
        {
            get
            {
                return ref UnsafeUtility.As<Internal.Length, Length>(ref RenderingDataStore.Instance.Access(this).CornerRadius);
            }
        }

        public ref Length EdgeRadius
        {
            get
            {
                return ref UnsafeUtility.As<Internal.Length, Length>(ref RenderingDataStore.Instance.Access(this).EdgeRadius);
            }
        }

        internal override VirtualBlock Clone()
        {
            VirtualUIBlock3D clone = new VirtualUIBlock3D();

            if (Self.IsRegistered)
            {
                clone.CloneFromDataStore(ID);
            }
            else
            {
                CopyBasePropertiesTo(clone);
                clone.visuals = visuals;
            }

            return clone;
        }

        public VirtualUIBlock3D() : base()
        {
            Layout = Layout.ThreeD;
            AutoLayout = AutoLayout.Horizontal;

            BaseRenderInfo baseInfo = BaseRenderInfo.Default(BlockType.UIBlock3D);
            ((IRenderBlock)this).BaseRenderInfo = UnsafeUtility.As<BaseRenderInfo, Internal.BaseRenderInfo>(ref baseInfo);
            Surface = Surface.DefaultLit;
        }
    }
}
