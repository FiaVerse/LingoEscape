// Copyright (c) Supernova Technologies LLC
using Nova.Internal;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Rendering;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Nova
{
    

    [Serializable]
    internal class VirtualUIBlock : VirtualBlock, IUIBlock
    {
        [SerializeField]
        private Layout layout = Layout.TwoD;
        [SerializeField]
        private AutoLayout autoLayout = AutoLayout.Horizontal;
        [SerializeField]
        private BaseRenderInfo visualBase = BaseRenderInfo.Default(BlockType.UIBlock2D);
        [SerializeField]
        private Surface surface = Surface.DefaultUnlit;

        public IInputTarget InputTarget => null;
        ref Internal.Layout ILayoutBlock.SerializedLayout => ref UnsafeUtility.As<Layout, Internal.Layout>(ref layout);
        ref Internal.AutoLayout ILayoutBlock.SerializedAutoLayout => ref UnsafeUtility.As<AutoLayout, Internal.AutoLayout>(ref autoLayout);

        Vector3 ILayoutBlock.PreviewSize 
        {
            get
            {
                return Vector3.zero;
            }
            set 
            { 
                // Do Nothing 
            } 
        }

        ref readonly Internal.Length3.Calculated ILayoutBlock.CalculatedSize => ref LayoutDataStore.Instance.AccessCalc(this).Size;
        ref readonly Internal.Length3.Calculated ILayoutBlock.CalculatedPosition => ref LayoutDataStore.Instance.AccessCalc(this).Position;
        ref readonly Internal.LengthBounds.Calculated ILayoutBlock.CalculatedPadding => ref LayoutDataStore.Instance.AccessCalc(this).Padding;
        ref readonly Internal.LengthBounds.Calculated ILayoutBlock.CalculatedMargin => ref LayoutDataStore.Instance.AccessCalc(this).Margin;

        public ref Layout Layout
        {
            get
            {
                if (!Self.Index.IsValid)
                {
                    return ref layout;
                }

                unsafe
                {
                    return ref UnsafeUtility.AsRef<Layout>(LayoutDataStore.Instance.Access(this).Layout);
                }
            }
        }

        public ref AutoLayout AutoLayout
        {
            get
            {
                if (!Self.Index.IsValid)
                {
                    return ref autoLayout;
                }

                unsafe
                {
                    return ref UnsafeUtility.AsRef<AutoLayout>(LayoutDataStore.Instance.Access(this).AutoLayout);
                }
            }
        }

        /// <summary>
        /// The calculated output of <see cref="AutoLayout.Spacing"/>
        /// </summary>
        public ref readonly Length.Calculated CalculatedAutoLayoutSpacing
        {
            get
            {
                return ref UnsafeUtility.As<Internal.Length.Calculated, Length.Calculated>(ref LayoutDataStore.Instance.AccessCalcSpacing(this).First);
            }
        }

        public ref Length3 Size => ref Layout.Size;

        public ref MinMax3 SizeMinMax => ref Layout.SizeMinMax;

        public ref Length3 Position => ref Layout.Position;

        public ref MinMax3 PositionMinMax => ref Layout.PositionMinMax;

        public ref LengthBounds Padding => ref Layout.Padding;

        public ref MinMaxBounds PaddingMinMax => ref Layout.PaddingMinMax;

        public ref LengthBounds Margin => ref Layout.Margin;

        public ref MinMaxBounds MarginMinMax => ref Layout.MarginMinMax;

        public ref ThreeD<AutoSize> AutoSize => ref Layout.AutoSize;

        public ref Axis AspectRatioAxis => ref Layout.AspectRatioAxis;

        public ref Alignment Alignment => ref Layout.Alignment;

        public ref bool RotateSize => ref Layout.RotateSize;

        public Vector3 PaddedSize => LayoutDataStore.Instance.AccessCalc(this).PaddedSize;

        public Vector3 ContentSize => Self.Index.IsValid ? (Vector3)LayoutDataStore.Instance.GetContentSize(this) : Vector3.zero;

        public Vector3 ContentCenter => Self.Index.IsValid ? (Vector3)LayoutDataStore.Instance.GetContentCenter(this) : Vector3.zero;

        public Vector3 HierarchySize => Self.Index.IsValid ? (Vector3)LayoutDataStore.Instance.GetHierarchySize(this) : Vector3.zero;

        public Vector3 HierarchyCenter => Self.Index.IsValid ? (Vector3)LayoutDataStore.Instance.GetHierarchyCenter(this) : Vector3.zero;

        public Vector3 RotatedSize => LayoutDataStore.Instance.AccessCalc(this).Size.Value;

        public Vector3 LayoutSize => RotatedSize + (Vector3)((ILayoutBlock)this).CalculatedMargin.Size;

        public void CalculateLayout() => EngineManager.Instance.UpdateElement(ID);

        private protected override void Register()
        {
            HierarchyDataStore.Instance.Register(this);
            LayoutDataStore.Instance.Register(this);
            RenderingDataStore.Instance.Register(this);
        }

        private protected override void Unregister()
        {
            RenderingDataStore.Instance.Unregister(this);
            LayoutDataStore.Instance.Unregister(this);
            HierarchyDataStore.Instance.Unregister(this);
        }

        private protected override void HandleParentChanged() { }

        ref Internal.BaseRenderInfo IRenderBlock.BaseRenderInfo
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<BaseRenderInfo, Internal.BaseRenderInfo>(ref visualBase);
        }

        ref Internal.Surface IRenderBlock.Surface
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<Surface, Internal.Surface>(ref surface);
        }

        public bool Visible
        {
            get
            {
                return visualBase.Visible;
            }
            set
            {
                visualBase.Visible = value;
                RenderingDataStore.Instance.CopyBaseInfoToStore(this);
            }
        }

        public void CopyToDataStore()
        {
            if (!Self.IsRegistered)
            {
                return;
            }

            HierarchyDataStore.Instance.CopyToStore(this);
            LayoutDataStore.Instance.CopyToStore(this);
            RenderingDataStore.Instance.CopyToStore(this);
        }

        internal override void CopyFromDataStore()
        {
            HierarchyDataStore.Instance.CopyFromStore(this);
            LayoutDataStore.Instance.CopyFromStore(this);
            RenderingDataStore.Instance.CopyFromStore(this);
        }

        private protected override void CloneFromDataStore(DataStoreID sourceID)
        {
            HierarchyDataStore.Instance.Clone(sourceID, this);
            LayoutDataStore.Instance.Clone(sourceID, this);
            RenderingDataStore.Instance.Clone(sourceID, this);
        }

        internal override VirtualBlock Clone()
        {
            VirtualUIBlock clone = new VirtualUIBlock();

            if (Self.IsRegistered)
            {
                clone.CloneFromDataStore(ID);
                clone.Visible = Visible;
            }
            else
            {
                CopyBasePropertiesTo(clone);
            }

            return clone;
        }

        private protected void CopyBasePropertiesTo(VirtualUIBlock clone)
        {
            clone.layout = layout;
            clone.autoLayout = autoLayout;
            clone.surface = surface;
            clone.visualBase = visualBase;
            clone.Visible = Visible;
        }
    }
}
