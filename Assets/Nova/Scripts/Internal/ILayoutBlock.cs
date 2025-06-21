// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Core;
using UnityEngine;

namespace Nova.Internal
{
    /// <summary>
    /// The interface to implement when an object has a set of layout properties
    /// that will be copied to/copied from the LayoutDataStore and processed by the LayoutEngine
    /// </summary>
    internal interface ILayoutBlock : IHierarchyBlock
    {
        /// <summary>
        /// The layout properties stored on the object itself. In the context of
        /// Unity Objects, this is the serialized field on the relevant component
        /// </summary>
        ref Layout SerializedLayout { get; }

        /// <summary>
        /// The auto layout properties stored on the object itself. In the context of
        /// Unity Objects, this is the serialized field on the relevant component
        /// </summary>
        ref AutoLayout SerializedAutoLayout { get; }

        ref readonly Length3.Calculated CalculatedSize { get; }
        ref readonly Length3.Calculated CalculatedPosition { get; }
        ref readonly LengthBounds.Calculated CalculatedPadding { get; }
        ref readonly LengthBounds.Calculated CalculatedMargin { get; }

        Vector3 PreviewSize { get; set; }

        // Calculated output
        Vector3 PaddedSize { get; }
        Vector3 RotatedSize { get; }
        Vector3 LayoutSize { get; }

        void CalculateLayout();
    }
}
