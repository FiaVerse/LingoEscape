// Copyright (c) Supernova Technologies LLC
using Nova.Internal;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// A layout and 2D visual configuration of a "grid slice", a row <i> or </i> column in a <see cref="GridView"/> that's positioned along the <see cref="GridView.PrimaryAxis"/>
    /// </summary>
    /// <seealso cref="GridSlice"/>
    /// <seealso cref="GridSlice3D"/>
    [Serializable]
    public struct GridSlice2D : IEquatable<GridSlice2D>
    {
        /// <summary>
        /// The layout configuration of this grid slice
        /// </summary>
        /// <remarks>
        /// Identical to the <see cref="UIBlock.Layout"/> configuration on <see cref="UIBlock"/>s
        /// </remarks>
        public Layout Layout;

        /// <summary>
        /// The auto layout configuration of this grid slice
        /// </summary>
        /// <remarks>
        /// Identical to the <see cref="UIBlock.AutoLayout"/> configuration on <see cref="UIBlock"/>s
        /// </remarks>
        public AutoLayout AutoLayout;

        /// <summary>
        /// The fill color of this grid slice
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock.Color"/> configuration on <see cref="UIBlock"/>s</remarks>
        public Color Color;

        /// <summary>
        /// The surface effect when there's scene lighting
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock.Surface"/> configuration on <see cref="UIBlock"/>s</remarks>
        public Surface Surface;

        /// <summary>
        /// The corner radius of this grid slice
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock3D.CornerRadius"/> configuration on <see cref="UIBlock2D"/>s</remarks>
        public Length CornerRadius;

        /// <summary>
        /// The border around this grid slice
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock2D.Border"/> configuration on <see cref="UIBlock2D"/>s</remarks>
        public Border Border;

        /// <summary>
        /// The drop shadow/inner shadow of this grid slice
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock2D.Shadow"/> configuration on <see cref="UIBlock2D"/>s</remarks>
        public Shadow Shadow;

        /// <summary>
        /// The gradient fill of this grid slice
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock2D.Gradient"/> configuration on <see cref="UIBlock2D"/>s</remarks>
        public RadialGradient Gradient;
        /// <summary>
        /// Create a new <see cref="GridSlice2D"/> configuration
        /// </summary>
        /// <param name="primaryAxis">The scrollable axis of this grid slice's parent</param>
        /// <param name="crossAxis">The <see cref="AutoLayout">AutoLayout</see>.<see cref="AutoLayout.Axis">Axis</see> of this grid slice</param>
        public GridSlice2D(Axis primaryAxis, Axis crossAxis)
        {
            Layout = Layout.TwoD;
            AutoLayout = AutoLayout.Disabled;

            AutoLayout.Axis = crossAxis;
            Layout.AutoSize = AutoSize.Expand;

            if (primaryAxis.TryGetIndex(out int primaryAxisIndex))
            {
                Layout.AutoSize[primaryAxisIndex] = AutoSize.Shrink;
            }

            Color = Color.clear;
            Border = Border.Default;
            Shadow = Shadow.Default;
            CornerRadius = Length.Zero;
            Gradient = RadialGradient.Default;
            Surface = Surface.DefaultUnlit;
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if all fields of <paramref name="lhs"/> are equal to all field of <paramref name="rhs"/></returns>
        public static bool operator ==(GridSlice2D lhs, GridSlice2D rhs)
        {
            return lhs.Layout == rhs.Layout &&
                   lhs.AutoLayout == rhs.AutoLayout &&
                   lhs.Color == rhs.Color &&
                   lhs.Surface == rhs.Surface &&
                   lhs.CornerRadius == rhs.CornerRadius &&
                   lhs.Border == rhs.Border &&
                   lhs.Shadow == rhs.Shadow &&
                   lhs.Gradient == rhs.Gradient;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if any field of <paramref name="lhs"/> is <b>not</b> equal to its corresponding field of <paramref name="rhs"/></returns>
        public static bool operator !=(GridSlice2D lhs, GridSlice2D rhs)
        {
            return lhs.Layout != rhs.Layout ||
                   lhs.AutoLayout != rhs.AutoLayout ||
                   lhs.Color != rhs.Color ||
                   lhs.Surface != rhs.Surface ||
                   lhs.CornerRadius != rhs.CornerRadius ||
                   lhs.Border != rhs.Border ||
                   lhs.Shadow != rhs.Shadow ||
                   lhs.Gradient != rhs.Gradient;
        }

        /// <summary>
        /// The hashcode for this <see cref="GridSlice2D"/>
        /// </summary>
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Layout.GetHashCode();
            hash = (hash * 7) + AutoLayout.GetHashCode();
            hash = (hash * 7) + Color.GetHashCode();
            hash = (hash * 7) + Surface.GetHashCode();
            hash = (hash * 7) + CornerRadius.GetHashCode();
            hash = (hash * 7) + Border.GetHashCode();
            hash = (hash * 7) + Shadow.GetHashCode();
            hash = (hash * 7) + Gradient.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridSlice2D"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public override bool Equals(object other)
        {
            if (other is GridSlice2D slice)
            {
                return this == slice;
            }

            return false;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridSlice2D"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public bool Equals(GridSlice2D other)
        {
            return this == other;
        }
    }

    /// <summary>
    /// A layout and 3D visual configuration of a "grid slice", a row <i> or </i> column in a <see cref="GridView"/> that's positioned along the <see cref="GridView.PrimaryAxis"/>
    /// </summary>
    /// <seealso cref="GridSlice"/>
    /// <seealso cref="GridSlice2D"/>
    [Serializable]
    public struct GridSlice3D : IEquatable<GridSlice3D>
    {
        /// <summary>
        /// The layout configuration of this grid slice
        /// </summary>
        /// <remarks>
        /// Identical to the <see cref="UIBlock.Layout"/> configuration on <see cref="UIBlock"/>s
        /// </remarks>
        public Layout Layout;

        /// <summary>
        /// The auto layout configuration of this grid slice
        /// </summary>
        /// <remarks>
        /// Identical to the <see cref="UIBlock.AutoLayout"/> configuration on <see cref="UIBlock"/>s
        /// </remarks>
        public AutoLayout AutoLayout;

        /// <summary>
        /// The fill color of this grid slice
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock.Color"/> configuration on <see cref="UIBlock"/>s</remarks>
        public Color Color;
        /// <summary>
        /// The surface effect when there's scene lighting
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock.Surface"/> configuration on <see cref="UIBlock"/>s</remarks>
        public Surface Surface;
        /// <summary>
        /// The corner radius of this grid slice
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock3D.CornerRadius"/> configuration on <see cref="UIBlock3D"/>s</remarks>
        public Length CornerRadius;

        /// <summary>
        /// The edge radius of this grid slice
        /// </summary>
        /// <remarks>Identical to the <see cref="UIBlock3D.EdgeRadius"/> configuration on <see cref="UIBlock3D"/>s</remarks>
        public Length EdgeRadius;

        /// <summary>
        /// Create a new <see cref="GridSlice3D"/> configuration
        /// </summary>
        /// <param name="primaryAxis">The scrollable axis of this grid slice's parent</param>
        /// <param name="crossAxis">The <see cref="AutoLayout">AutoLayout</see>.<see cref="AutoLayout.Axis">Axis</see> of this grid slice</param>
        public GridSlice3D(Axis primaryAxis, Axis crossAxis)
        {
            Layout = Layout.ThreeD;
            AutoLayout = AutoLayout.Disabled;

            AutoLayout.Axis = crossAxis;
            Layout.AutoSize = AutoSize.Expand;
            
            if (primaryAxis.TryGetIndex(out int primaryAxisIndex))
            {
                Layout.AutoSize[primaryAxisIndex] = AutoSize.Shrink;
            }

            Color = Color.clear;
            CornerRadius = Length.Zero;
            EdgeRadius = Length.Zero;
            Surface = Surface.DefaultLit;
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if all fields of <paramref name="lhs"/> are equal to all field of <paramref name="rhs"/></returns>
        public static bool operator ==(GridSlice3D lhs, GridSlice3D rhs)
        {
            return lhs.Layout == rhs.Layout && 
                   lhs.AutoLayout == rhs.AutoLayout &&
                   lhs.Color == rhs.Color &&
                   lhs.Surface == rhs.Surface &&
                   lhs.CornerRadius == rhs.CornerRadius &&
                   lhs.EdgeRadius == rhs.EdgeRadius;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if any field of <paramref name="lhs"/> is <b>not</b> equal to its corresponding field of <paramref name="rhs"/></returns>
        public static bool operator !=(GridSlice3D lhs, GridSlice3D rhs)
        {
            return lhs.Layout != rhs.Layout ||
                   lhs.AutoLayout != rhs.AutoLayout ||
                   lhs.Color != rhs.Color ||
                   lhs.Surface != rhs.Surface ||
                   lhs.CornerRadius != rhs.CornerRadius ||
                   lhs.EdgeRadius != rhs.EdgeRadius;
        }

        /// <summary>
        /// The hashcode for this <see cref="GridSlice3D"/>
        /// </summary>
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Layout.GetHashCode();
            hash = (hash * 7) + AutoLayout.GetHashCode();
            hash = (hash * 7) + Color.GetHashCode();
            hash = (hash * 7) + Surface.GetHashCode();
            hash = (hash * 7) + CornerRadius.GetHashCode();
            hash = (hash * 7) + EdgeRadius.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridSlice3D"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public override bool Equals(object other)
        {
            if (other is GridSlice3D slice)
            {
                return this == slice;
            }

            return false;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridSlice3D"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public bool Equals(GridSlice3D other)
        {
            return this == other;
        }
    }

    /// <summary>
    /// A layout-only configuration of a "grid slice", a row <i> or </i> column in a <see cref="GridView"/> that's positioned along the <see cref="GridView.PrimaryAxis"/>
    /// </summary>
    /// <seealso cref="GridSlice2D"/>
    /// <seealso cref="GridSlice3D"/>
    [Serializable]
    public struct GridSlice : IEquatable<GridSlice>
    {
        /// <summary>
        /// The layout configuration of this grid slice
        /// </summary>
        /// <remarks>
        /// Identical to the <see cref="UIBlock.Layout"/> configuration on <see cref="UIBlock"/>s
        /// </remarks>
        public Layout Layout;

        /// <summary>
        /// The auto layout configuration of this grid slice
        /// </summary>
        /// <remarks>
        /// Identical to the <see cref="UIBlock.AutoLayout"/> configuration on <see cref="UIBlock"/>s
        /// </remarks>
        public AutoLayout AutoLayout;

        /// <summary>
        /// Create a new <see cref="GridSlice"/> configuration
        /// </summary>
        /// <param name="primaryAxis">The scrollable axis of this grid slice's parent</param>
        /// <param name="crossAxis">The <see cref="AutoLayout">AutoLayout</see>.<see cref="AutoLayout.Axis">Axis</see> of this grid slice</param>
        public GridSlice(Axis primaryAxis, Axis crossAxis)
        {
            Layout = Layout.TwoD;
            AutoLayout = AutoLayout.Disabled;
            AutoLayout.Alignment = 0;

            AutoLayout.Axis = crossAxis;
            Layout.AutoSize = AutoSize.Expand;
            
            if (primaryAxis.TryGetIndex(out int primaryAxisIndex))
            {
                Layout.AutoSize[primaryAxisIndex] = AutoSize.Shrink;
            }            
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if <c><paramref name="lhs"/>.Layout == <paramref name="rhs"/>.Layout &amp;&amp; <paramref name="lhs"/>.AutoLayout == <paramref name="rhs"/>.AutoLayout</c></returns>
        public static bool operator ==(GridSlice lhs, GridSlice rhs)
        {
            return lhs.Layout == rhs.Layout && lhs.AutoLayout == rhs.AutoLayout;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        /// <param name="lhs">Left hand side</param>
        /// <param name="rhs">Right hand side</param>
        /// <returns><see langword="true"/> if <c><paramref name="lhs"/>.Layout != <paramref name="rhs"/>.Layout || <paramref name="lhs"/>.AutoLayout != <paramref name="rhs"/>.AutoLayout</c></returns>
        public static bool operator !=(GridSlice lhs, GridSlice rhs)
        {
            return lhs.Layout != rhs.Layout || lhs.AutoLayout != rhs.AutoLayout;
        }

        /// <summary>
        /// The hashcode for this <see cref="GridSlice"/>
        /// </summary>
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Layout.GetHashCode();
            hash = (hash * 7) + AutoLayout.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridSlice"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public override bool Equals(object other)
        {
            if (other is GridSlice slice)
            {
                return this == slice;
            }

            return false;
        }

        /// <summary>
        /// Equality compare
        /// </summary>
        /// <param name="other">The other <see cref="GridSlice"/> to compare</param>
        /// <returns><see langword="true"/> if <c>this == <paramref name="other"/></c></returns>
        public bool Equals(GridSlice other)
        {
            return this == other;
        }
    }

    internal class VirtualBlockQueue : IDisposable
    {
        private Queue<VirtualUIBlock2D> twoDBlocks = new Queue<VirtualUIBlock2D>();
        private Queue<VirtualUIBlock3D> threeDBlocks = new Queue<VirtualUIBlock3D>();

        public VirtualUIBlock GetOrCreate(Type blockType)
        {
            if (TryDequeue(blockType, out VirtualUIBlock uiBlock))
            {
                return uiBlock;
            }

            switch (blockType)
            {
                case Type twoD when twoD == typeof(VirtualUIBlock2D):
                    return new VirtualUIBlock2D();
                case Type threeD when threeD == typeof(VirtualUIBlock3D):
                    return new VirtualUIBlock3D();
                default:
                    return null;
            }
        }

        public void Enqueue(VirtualUIBlock uiBlock)
        {
            if (uiBlock == null)
            {
                return;
            }

            switch (uiBlock)
            {
                case VirtualUIBlock2D twoDBlock:
                    twoDBlocks.Enqueue(twoDBlock);
                    break;
                case VirtualUIBlock3D threeDBlock:
                    threeDBlocks.Enqueue(threeDBlock);
                    break;
            }
        }

        public bool TryDequeue(out VirtualUIBlock uiBlock)
        {
            if (twoDBlocks.Count > 0)
            {
                uiBlock = twoDBlocks.Dequeue();
                return true;
            }

            if (threeDBlocks.Count > 0)
            {
                uiBlock = threeDBlocks.Dequeue();
                return true;
            }

            uiBlock = null;
            return false;
        }

        public bool TryDequeue(Type blockType, out VirtualUIBlock uiBlock)
        {
            switch (blockType)
            {
                case Type twoD when twoD == typeof(VirtualUIBlock2D) && twoDBlocks.Count > 0:
                    uiBlock = twoDBlocks.Dequeue();
                    return true;
                case Type threeD when threeD == typeof(VirtualUIBlock3D) && threeDBlocks.Count > 0:
                    uiBlock = threeDBlocks.Dequeue();
                    return true;
                default:
                    uiBlock = null;
                    return false;
            }
        }

        public int Count(Type blockType)
        {
            switch (blockType)
            {
                case Type twoD when twoD == typeof(VirtualUIBlock2D):
                    return twoDBlocks.Count;
                case Type threeD when threeD == typeof(VirtualUIBlock3D):
                    return threeDBlocks.Count;
                default:
                    return 0;
            }
        }

        public void Dispose()
        {
            while (TryDequeue(out VirtualUIBlock uiBlock))
            {
                uiBlock.Dispose();
            }
        }
    }
}
