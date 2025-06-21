// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Events;
using Nova.Internal;
using Nova.Internal.Core;
using Nova.Internal.Hierarchy;
using Nova.Internal.Layouts;
using Nova.Internal.Rendering;
using Nova.Internal.Utilities;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

using InputModule = Nova.Internal.InputModule<Nova.UIBlock>; 

namespace Nova
{
    /// <summary>
    /// Applies a set of <see cref="Nova.Layout"/> properties and <see cref="Nova.AutoLayout"/> properties across a connected transform hierarchy of UIBlocks
    /// </summary>
    [ExecuteAlways, AddComponentMenu("Nova/UIBlock")]
    [HelpURL("https://novaui.io/manual/UIBlock.html")]
    public class UIBlock : CoreBlock, IUIBlock
    {
        #region Public
        /// <summary>
        /// The UIBlock on <c>transform.parent</c>. If <c>transform.parent</c> is <c>null</c> or there is no UIBlock on <c>transform.parent</c>, this value will be <c>null</c>.
        /// </summary>
        /// <remarks>If <c>gameObject.activeInHierarchy</c> is <c><see langword="false"/></c>, this value will be <c>null</c>.</remarks>
        public UIBlock Parent => Self.Parent as UIBlock;

        /// <summary>
        /// The root of this UIBlock's connected UIBlock hierarchy.
        /// </summary>
        /// <remarks>If <c>gameObject.activeInHierarchy</c> is <c><see langword="false"/></c>, this value will be <c>null</c>.</remarks>
        public UIBlock Root => Self.Root as UIBlock;

        /// <summary>
        /// Retrieves the child UIBlock at the provided <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The index of the child to retrieve</param>
        /// <returns>The child UIBlock at the provided <paramref name="index"/>.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Throws when <c><paramref name="index"/> &lt; 0</c> or <c><paramref name="index"/> &gt;= <see cref="ChildCount"/></c></exception>
        /// <seealso cref="ChildCount"/>
        public UIBlock GetChild(int index)
        {
            return GetChildBlock(index) as UIBlock;
        }

        /// <summary>
        /// The number of enabled child <see cref="GameObject"/>'s with a UIBlock component.
        /// </summary>
        /// <remarks>If <c>gameObject.activeInHierarchy</c> is <see langword="false"/>, this value will be <c>0</c>.</remarks>
        /// <seealso cref="GetChild(int)"/>
        public int ChildCount => ChildBlockIDs.Count;
        #region Rendering
        /// <summary>
        /// The primary body content color.
        /// </summary>
        [field: NonSerialized]
        public virtual Color Color { get; set; }

        /// <summary>
        /// The <see cref="Nova.Surface"/> configuration for this UIBlock, adjusts the mesh surface's appearance under scene lighting.
        /// </summary>
        public ref Surface Surface
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<Internal.Surface, Surface>(ref RenderingDataStore.Instance.AccessSurface(this));
        }

        /// <summary>
        /// Specifies whether any visual properties should render.
        /// </summary>
        /// <remarks>
        /// This is a global toggle for this <see cref="UIBlock"/> and, when set to <see langword="false"/>, will hide all visual properties. Layout behavior remains unchanged.
        /// </remarks>
        public bool Visible
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => visibility.Visible;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                visibility.Visible = value;
                RenderingDataStore.Instance.CopyBaseInfoToStore(this);
            }
        }

        /// <summary>
        /// Sets the GameObject's layer. Should be used instead of <c>gameobject.layer</c> to ensure that
        /// Nova is tracking the new layer.
        /// </summary>
        public int GameObjectLayer
        {
            get => gameObject.layer;
            set
            {
                gameObject.layer = value;

                if (gameObject.layer == visibility.GameObjectLayer)
                {
                    return;
                }

                visibility.GameObjectLayer = value;
                RenderingDataStore.Instance.CopyBaseInfoToStore(this);
            }
        }
        #endregion

        #region Layout
        /// <summary>
        /// The entire set of uncalculated UIBlock layout properties.
        /// </summary>
        public ref Layout Layout
        {
            get
            {
                if (!Self.IsRegistered)
                {
                    return ref layout;
                }

                unsafe
                {
                    return ref UnsafeUtility.AsRef<Layout>(LayoutDataStore.Instance.Access(this).Layout);
                }
            }
        }

        /// <summary>
        /// The <see cref="Length3"/> configuration used to calculate <see cref="CalculatedSize">Calculated Size</see>.
        /// </summary>
        /// <remarks><c>Size.<see cref="Length3.Percent">Percent</see></c> is a percentage of the <see cref="Parent">Parent's</see> <see cref="PaddedSize">Padded Size</see>.</remarks>
        /// <seealso cref="SizeMinMax"/>
        /// <seealso cref="CalculatedSize"/>
        public ref Length3 Size
        {
            get
            {
                return ref Layout.Size;
            }
        }

        /// <summary>
        /// The <see cref="MinMax3"/> used to clamp <see cref="Size">Size</see> and <see cref="AutoSize">Auto Size</see> when calculating <see cref="CalculatedSize">CalculatedSize.</see>.
        /// </summary>
        public ref MinMax3 SizeMinMax
        {
            get
            {
                return ref Layout.SizeMinMax;
            }
        }

        /// <summary>
        /// The size of the UIBlock. Calculated by the Nova Engine once per dirty frame and whenenever <see cref="CalculateLayout"/> is called explicitly.
        /// </summary>
        /// <remarks>The final value here accounts for a combination of inputs from <see cref="Size">Size</see>, <see cref="SizeMinMax">Size Min Max</see>, 
        /// <see cref="AutoSize">Auto Size</see>, <see cref="AspectRatioAxis">Aspect Ratio Axis</see>, and the <see cref="PaddedSize">Padded Size</see> of its <see cref="Parent">Parent</see>.
        /// </remarks>
        /// <seealso cref="Size"/>
        /// <seealso cref="SizeMinMax"/>
        /// <seealso cref="AutoSize"/>
        /// <seealso cref="AspectRatioAxis"/>
        public ref readonly Length3.Calculated CalculatedSize
        {
            get
            {
                return ref UnsafeUtility.As<Internal.Length3.Calculated, Length3.Calculated>(ref LayoutDataStore.Instance.AccessCalc(this).Size);
            }
        }

        /// <summary>
        /// An <see cref="Nova.AutoSize"/> value for each axis. Provides a way to have this UIBlock's <see cref="CalculatedSize">Calculated Size</see> adapt to the size of its <see cref="Parent">Parent</see> or size of its children automatically.
        /// </summary>
        /// <remarks>When set to a value other than <see cref="AutoSize.None"/> for a given axis, this will override any <see cref="Size">Size</see> configuration along that same axis.</remarks>
        public ref ThreeD<AutoSize> AutoSize
        {
            get
            {
                return ref Layout.AutoSize;
            }
        }

        /// <summary>
        /// When set to a value other than <see cref="Axis.None"/>, the aspect ratio of this UIBlock's <see cref="CalculatedSize">Calculated Size</see> will remain constant, even as <see cref="Size">Size</see> is modified.
        /// </summary>
        /// <remarks>Can be used in conjuction with <see cref="AutoSize">Auto Size</see>, but only <see cref="AutoSize">Auto Size</see> along the AspectRatioAxis will be honored.</remarks>
        public ref Axis AspectRatioAxis
        {
            get
            {
                return ref Layout.AspectRatioAxis;
            }
        }

        /// <summary>
        /// The <see cref="LengthBounds"/> configuration used to calculate <see cref="CalculatedMargin">CalculatedMargin</see>. Describes a spatial buffer applied outward from <see cref="CalculatedSize">Calculated Size</see>.
        /// </summary>
        /// <remarks>
        /// <c>Margin.<see cref="LengthBounds.Percent">Percent</see></c> is a percentage of the <see cref="Parent">Parent's</see> <see cref="PaddedSize">Padded Size</see>.
        /// </remarks>
        /// <seealso cref="MarginMinMax"/>
        /// <seealso cref="CalculatedMargin"/>
        /// <seealso cref="LayoutSize"/>
        public ref LengthBounds Margin
        {
            get
            {
                return ref Layout.Margin;
            }
        }

        /// <summary>
        /// The <see cref="MinMaxBounds"/> used to clamp <see cref="Margin">Margin</see> when calculating <see cref="CalculatedMargin">Calculated Margin</see>.
        /// </summary>
        /// <seealso cref="Margin"/>
        /// <seealso cref="CalculatedMargin"/>
        /// <seealso cref="LayoutSize"/>
        public ref MinMaxBounds MarginMinMax
        {
            get
            {
                return ref Layout.MarginMinMax;
            }
        }

        /// <summary>
        /// The amount of space applied <i>outward</i> from the bounds defined by <see cref="RotatedSize">Rotated Size</see>. Calculated by the Nova Engine once per dirty frame and whenenever <see cref="CalculateLayout"/> is called explicitly.
        /// </summary>
        /// <remarks>
        /// The final value here accounts for a combination of inputs from <see cref="Margin">Margin</see>, <see cref="MarginMinMax">Margin Min Max</see>, and the <see cref="PaddedSize">Padded Size</see> of its <see cref="Parent">Parent</see>.
        /// </remarks>
        /// <seealso cref="Margin"/>
        /// <seealso cref="MarginMinMax"/>
        /// <seealso cref="LayoutSize"/>
        public ref readonly LengthBounds.Calculated CalculatedMargin
        {
            get
            {
                return ref UnsafeUtility.As<Internal.LengthBounds.Calculated, LengthBounds.Calculated>(ref LayoutDataStore.Instance.AccessCalc(this).Margin);
            }
        }

        /// <summary>
        /// A per-axis alignment for this UIBlock relative to its <see cref="Parent">Parent's</see> bounds (<see cref="PaddedSize"/>). <see cref="CalculatedPosition">CalculatedPosition</see> is an offset in the <see cref="Alignment"/> coordinate space.
        /// </summary>
        /// <remarks>
        /// <list type="table">
        /// <listheader>
        /// <term>Axis Alignment</term>
        /// <description>Shift Direction</description>
        /// </listheader>
        /// <item>
        /// <term><see cref="HorizontalAlignment.Left"/></term>
        /// <description>A positive <c><see cref="CalculatedPosition">Calculated Position</see>.X</c> shifts right</description><br/></item>
        /// <item>
        /// <term><see cref="HorizontalAlignment.Center"/></term>
        /// <description>A positive <c><see cref="CalculatedPosition">Calculated Position</see>.X</c> shifts right</description><br/></item>
        /// <item>
        /// <term><see cref="HorizontalAlignment.Right"/></term>
        /// <description>A positive <c><see cref="CalculatedPosition">Calculated Position</see>.X</c> shifts left</description><br/>
        /// </item>
        /// <item>
        /// <term><see cref="VerticalAlignment.Bottom"/></term>
        /// <description>A positive <c><see cref="CalculatedPosition">Calculated Position</see>.Y</c> shifts up</description><br/></item>
        /// <item>
        /// <term><see cref="VerticalAlignment.Center"/></term>
        /// <description>A positive <c><see cref="CalculatedPosition">Calculated Position</see>.Y</c> shifts up</description><br/></item>
        /// <item>
        /// <term><see cref="VerticalAlignment.Top"/></term>
        /// <description>A positive <c><see cref="CalculatedPosition">Calculated Position</see>.Y</c> shifts down</description><br/>
        /// </item>
        /// <item>
        /// <term><see cref="DepthAlignment.Front"/></term>
        /// <description>A positive <c><see cref="CalculatedPosition">Calculated Position</see>.Z</c> shifts forward</description><br/></item>
        /// <item>
        /// <term><see cref="DepthAlignment.Center"/></term>
        /// <description>A positive <c><see cref="CalculatedPosition">Calculated Position</see>.Z</c> shifts forward</description><br/></item>
        /// <item>
        /// <term><see cref="DepthAlignment.Back"/></term>
        /// <description>A positive <c><see cref="CalculatedPosition">Calculated Position</see>.Z</c>  shifts backward</description><br/>
        /// </item>
        /// </list>
        /// </remarks>
        public ref Alignment Alignment
        {
            get
            {
                return ref Layout.Alignment;
            }
        }

        /// <summary>
        /// If <see langword="true"/>, the <see cref="LayoutSize">Layout Size</see> will account for the bounds of <see cref="CalculatedSize">Calculated Size</see> rotated by <c><see cref="Transform.localRotation">transform.localRotation</see></c>.<br/>
        /// If <see langword="false"/>, the UIBlock will still render rotated, but the <see cref="LayoutSize">Layout Size</see> will not account for <c><see cref="Transform.localRotation">transform.localRotation</see></c>.
        /// </summary>
        public ref bool RotateSize
        {
            get
            {
                return ref Layout.RotateSize;
            }
        }

        /// <summary>
        /// If <see cref="RotateSize">Rotate Size</see> is <see langword="true"/>, returns <see cref="CalculatedSize">Calculated Size</see> rotated by <c><see cref="Transform.localRotation">transform.localRotation</see></c>.<br/>
        /// If <see cref="RotateSize">Rotate Size</see> is <see langword="false"/>, returns <see cref="CalculatedSize">Calculated Size</see>.
        /// </summary>
        public Vector3 RotatedSize
        {
            get
            {
                if (RotateSize)
                {
                    return LayoutUtils.RotateSize(CalculatedSize.Value, transform.localRotation);
                }

                return CalculatedSize.Value;
            }
        }

        /// <summary>
        /// The total <see cref="Bounds"/> of this UIBlock's immediate children in local space. 
        /// </summary>
        /// <remarks>
        /// May require a call to <see cref="CalculateLayout"/> for up-to-date values if child content has changed within a frame.
        /// </remarks>
        public Bounds ChildBounds => new Bounds(ContentCenter, ContentSize);

        /// <summary>
        /// The total <see cref="Bounds"/> of this UIBlock's hierarchy, inclusive of all decendent HierarchyBounds, in local space.
        /// </summary>
        /// <remarks>
        /// May require a call to <see cref="CalculateLayout"/> for up-to-date values if hierarchy content has changed within a frame.
        /// </remarks>
        public Bounds HierarchyBounds => new Bounds(HierarchyCenter, HierarchySize);

        /// <summary>
        /// The <see cref="Length3"/> configuration used to calculate <see cref="CalculatedPosition">Calculated Position</see>. Describes a per-axis offset from its <see cref="Alignment">Alignment</see>.</summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>
        /// <c>Position.<see cref="Length3.Percent">Percent</see></c> is a percentage of
        /// the <see cref="Parent">Parent's</see> <see cref="PaddedSize">Padded Size</see>.
        /// </description></item>
        /// <item><description>
        /// This value will be converted and written to <c><see cref="Transform.localPosition">transform.localPosition</see></c>
        /// as part of the Nova Engine update at the end of the current frame.
        /// </description></item>
        /// <item><description>
        /// If the <see cref="Parent">Parent's</see> <c><see cref="AutoLayout">AutoLayout</see>.<see cref="AutoLayout.Enabled">Enabled</see> == <see langword="true"/></c>,
        /// the <see cref="AutoLayout"/> will override this <c>Position</c> along the <see cref="Nova.AutoLayout"/>.<see cref="AutoLayout.Axis">Axis.</see>
        /// </description></item>
        /// </list>
        /// </remarks>
        /// <seealso cref="Alignment"/>
        /// <seealso cref="PositionMinMax"/>
        /// <seealso cref="CalculatedPosition"/>
        public ref Length3 Position
        {
            get
            {
                return ref Layout.Position;
            }
        }

        /// <summary>
        /// The <see cref="MinMax3"/> used to clamp <see cref="Position">Position</see> when calculating <see cref="CalculatedPosition">Calculated Position</see>.
        /// </summary>
        public ref MinMax3 PositionMinMax
        {
            get
            {
                return ref Layout.PositionMinMax;
            }
        }

        /// <summary>
        /// The local position of the UIBlock, offset from its configured <see cref="Alignment">Alignment</see>. Calculated by the Nova Engine once per dirty frame and whenenever <see cref="CalculateLayout"/> is called explicitly.
        /// </summary>
        /// <remarks>
        /// The final value here accounts for a combination of inputs from <see cref="Position">Position</see>, <see cref="PositionMinMax">Position Min Max</see>, <c><see cref="Transform.localPosition">transform.localPosition</see></c>, 
        /// the <see cref="PaddedSize">Padded Size</see> of its <see cref="Parent">Parent</see>, and the <see cref="AutoLayout">AutoLayout</see> of its <see cref="Parent">Parent</see>.
        /// </remarks>
        /// <seealso cref="Position"/>
        /// <seealso cref="PositionMinMax"/>
        /// <seealso cref="AutoLayout"/>
        /// <seealso cref="Alignment"/>
        public ref readonly Length3.Calculated CalculatedPosition
        {
            get
            {
                return ref UnsafeUtility.As<Internal.Length3.Calculated, Length3.Calculated>(ref LayoutDataStore.Instance.AccessCalc(this).Position);
            }
        }

        /// <summary>
        /// The <see cref="LengthBounds"/> configuration used to calculate <see cref="CalculatedPadding">Calculated Padding</see>. Describes a spatial buffer applied inward from <see cref="CalculatedSize">Calculated Size</see>.
        /// </summary>
        /// <remarks><c>Padding.<see cref="LengthBounds.Percent">Percent</see></c> is a percentage of this UIBlock's <see cref="CalculatedSize">Calculated Size</see>.</remarks>
        /// <seealso cref="PaddingMinMax"/>
        /// <seealso cref="CalculatedPadding"/>
        /// <seealso cref="PaddedSize"/>
        public ref LengthBounds Padding
        {
            get
            {
                return ref Layout.Padding;
            }
        }

        /// <summary>
        /// The <see cref="MinMaxBounds"/> used to clamp <see cref="Padding">Padding</see> when calculating <see cref="CalculatedPadding">Calculated Padding</see>.
        /// </summary>
        /// <seealso cref="Padding"/>
        /// <seealso cref="CalculatedPadding"/>
        /// <seealso cref="PaddedSize"/>
        public ref MinMaxBounds PaddingMinMax
        {
            get
            {
                return ref Layout.PaddingMinMax;
            }
        }

        /// <summary>
        /// The amount of space applied <i>inward</i> from the bounds defined by <see cref="CalculatedSize">Calculated Size</see>. Calculated by the Nova Engine once per dirty frame and whenenever <see cref="CalculateLayout"/> is called explicitly.
        /// </summary>
        /// <remarks>
        /// The final value here accounts for a combination of inputs from <see cref="Padding">Padding</see>, <see cref="PaddingMinMax">Padding Min Max</see>, and <see cref="CalculatedSize">Calculated Size</see>.
        /// </remarks>
        /// <seealso cref="Padding"/>
        /// <seealso cref="PaddingMinMax"/>
        /// <seealso cref="PaddedSize"/>
        public ref readonly LengthBounds.Calculated CalculatedPadding
        {
            get
            {
                return ref UnsafeUtility.As<Internal.LengthBounds.Calculated, LengthBounds.Calculated>(ref LayoutDataStore.Instance.AccessCalc(this).Padding);
            }
        }

        /// <summary>
        /// Equivalent to <c><see cref="CalculatedSize">CalculatedSize</see> - <see cref="CalculatedPadding">CalculatedPadding</see>.<see cref="LengthBounds.Calculated.Size">Size</see></c>.
        /// </summary>
        /// <remarks>A UIBlock is laid-out (positioned, sized, autosized, etc.) relative to its <see cref="Parent">Parent's</see> <see cref="PaddedSize">Padded Size</see>.</remarks>
        public Vector3 PaddedSize
        {
            get
            {
                return LayoutDataStore.Instance.AccessCalc(this).PaddedSize;
            }
        }

        /// <summary>
        /// The final, unscaled size of this UIBlock in its <see cref="Parent">Parent's</see> local space, used for positioning.<br/>
        /// Equivalent to <c><see cref="RotatedSize">RotatedSize</see> + <see cref="CalculatedMargin">CalculatedMargin</see>.<see cref="LengthBounds.Calculated.Size">Size</see></c>.
        /// </summary>
        /// <seealso cref="RotateSize"/>
        public Vector3 LayoutSize
        {
            get
            {
                return RotatedSize + CalculatedMargin.Size;
            }
        }

        /// <summary>
        /// Position all direct child UIBlocks sequentially along the X, Y, or Z axis.
        /// </summary>
        public ref AutoLayout AutoLayout
        {
            get
            {
                if (!Self.IsRegistered)
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
        /// The calculated output of <see cref="AutoLayout.Spacing"/>. Calculated by the Nova Engine once per dirty frame and whenenever <see cref="CalculateLayout"/> is called explicitly.
        /// </summary>
        /// <remarks>
        /// The final value here accounts for a combination of inputs from <see cref="AutoLayout.Spacing"/>, <see cref="AutoLayout.SpacingMinMax"/>, and the <see cref="PaddedSize">Padded Size</see> of this UIBlock.</remarks>
        public ref readonly Length.Calculated CalculatedSpacing
        {
            get
            {
                return ref UnsafeUtility.As<Internal.Length.Calculated, Length.Calculated>(ref LayoutDataStore.Instance.AccessCalcSpacing(this).First);
            }
        }

        /// <summary>
        /// The calculated output of <see cref="AutoLayout.Cross"/>.<see cref="CrossLayout.Spacing">Spacing</see>. Calculated by the Nova Engine once per dirty frame and whenenever <see cref="CalculateLayout"/> is called explicitly.
        /// </summary>
        /// <remarks>
        /// The final value here accounts for a combination of inputs from <see cref="AutoLayout.Cross"/>.<see cref="CrossLayout.Spacing">Spacing</see>, <see cref="AutoLayout.Cross"/>.<see cref="CrossLayout.SpacingMinMax">SpacingMinMax</see>, and the <see cref="PaddedSize">Padded Size</see> of this UIBlock.</remarks>
        public ref readonly Length.Calculated CalculatedCrossSpacing
        {
            get
            {
                return ref UnsafeUtility.As<Internal.Length.Calculated, Length.Calculated>(ref LayoutDataStore.Instance.AccessCalcSpacing(this).Second);
            }
        }

        /// <summary>
        /// The Nova Engine will automatically process all modified layout properties at the end of each frame. However, some UI scenarios may require knowing the <see cref="CalculatedSize">Calculated Size</see>
        /// or another calculated layout value intra-frame, before the Nova Engine has had a chance to run. Calling this method will force an inline recalculation of all modified layout properties on this UIBlock. 
        /// </summary>
        /// <remarks>
        /// This method only guarantees up-to-date calculated values for this UIBlock alone, meaning other UIBlocks in this UIBlock's hierarchy may not be updated in their entirety until the Nova Engine runs without
        /// their own explicit call to <c>CalculateLayout()</c>.<br/>
        /// This call will always overwrite <c>transform.localPosition</c> with the calculated layout position.
        /// <c><see cref="GameObject.activeInHierarchy">gameObject.activeInHierarchy</see></c> must be <see langword="true"/>, otherwise nothing will be recalculated.
        /// </remarks>
        public void CalculateLayout()
        {
            if (!Activated)
            {
                return;
            }

            EngineManager.Instance.UpdateElement(ID);

            transform.localPosition = LayoutDataStore.Instance.GetCalculatedTransformLocalPosition(this);
        }

        /// <summary>
        /// Move this <see cref="UIBlock"/> to the given Transform <paramref name="worldPosition"/> 
        /// and update <see cref="Position"/> accordingly such that when the Nova Engine
        /// recalculates the modified layout properties, the <i>resulting</i>
        /// <c>transform.position</c> will equal <paramref name="worldPosition"/>.
        /// </summary>
        /// <remarks>
        /// Preserves <see cref="Alignment"/> and the current <see cref="Position"/>'s <see cref="LengthType"/>s.
        /// </remarks>
        /// <param name="worldPosition">The <c>transform.position</c> to convert to a layout position.</param>
        /// <returns>
        /// <see langword="false"/> if <c>gameObject.activeInHierarchy == false</c>, since layout properties 
        /// cannot be calculated in that state, otherwise returns <see langword="true"/>.
        /// </returns>
        /// <seealso cref="TrySetLocalPosition(Vector3)"/>
        public bool TrySetWorldPosition(Vector3 worldPosition)
        {
            if (!Activated)
            {
                return false;
            }

            Vector3 localPosition = worldPosition;

            if (transform.parent != null)
            {
                localPosition = transform.parent.InverseTransformPoint(worldPosition);
            }

            return TrySetLocalPosition(localPosition);
        }

        /// <summary>
        /// Move this <see cref="UIBlock"/> to the given Transform <paramref name="localPosition"/> 
        /// and update <see cref="Position"/> accordingly such that when the Nova Engine
        /// recalculates the modified layout properties, the <i>resulting</i> 
        /// <c>transform.localPosition</c> will equal <paramref name="localPosition"/>.
        /// </summary>
        /// <remarks>
        /// Preserves <see cref="Alignment"/> and the current <see cref="Position"/>'s <see cref="LengthType"/>s.
        /// </remarks>
        /// <param name="localPosition">The <c>transform.localPosition</c> to convert to a layout position.</param>
        /// <returns>
        /// <see langword="false"/> if <c>gameObject.activeInHierarchy == false</c>, since layout properties 
        /// cannot be calculated in that state, otherwise returns <see langword="true"/>.
        /// </returns>
        /// <seealso cref="TrySetWorldPosition(Vector3)"/>
        public bool TrySetLocalPosition(Vector3 localPosition)
        {
            if (!Activated)
            {
                return false;
            }

            CalculateLayout();

            UIBlockUtils.SetLayoutOffsetFromLocalPosition(this, localPosition);
            transform.localPosition = localPosition;

            return true;
        }
        #endregion

        #region Events
        /// <summary>
        /// Subscribe to a gesture event on this <see cref="UIBlock"/> and optionally on its descendent hierarchy, depending on the value of <paramref name="includeHierarchy"/>.
        /// </summary>
        /// <remarks>
        /// If <paramref name="includeHierarchy"/> is <see langword="true"/>, <paramref name="gestureHandler"/> will be invoked whenever the given gesture type is triggered on this <see cref="UIBlock"/> <i>or</i> one of its decendents.<br/>
        /// If <paramref name="includeHierarchy"/> is <see langword="false"/>, <paramref name="gestureHandler"/> will be invoked only when the given gesture type occurs on <i>this</i> <see cref="UIBlock"/> directy.
        /// </remarks>
        /// <typeparam name="TGesture">The type of gesture event to handle.</typeparam>
        /// <param name="gestureHandler">The callback invoked when the gesture event fires.</param>
        /// <param name="includeHierarchy">Capture gestures from the descendent hierarchy or scope to this <i>this</i> <see cref="UIBlock"/> directy.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="gestureHandler"/> is <c>null</c>.</exception>
        /// <seealso cref="Gesture.OnClick"/>
        /// <seealso cref="Gesture.OnPress"/>
        /// <seealso cref="Gesture.OnRelease"/>
        /// <seealso cref="Gesture.OnHover"/>
        /// <seealso cref="Gesture.OnUnhover"/>
        /// <seealso cref="Gesture.OnScroll"/>
        /// <seealso cref="Gesture.OnMove"/>
        /// <seealso cref="Gesture.OnDrag"/>
        /// <seealso cref="Gesture.OnCancel"/>
        /// <seealso cref="FireGestureEvent{TGesture}(TGesture)"/>
        public void AddGestureHandler<TGesture>(UIEventHandler<TGesture> gestureHandler, bool includeHierarchy = true) where TGesture : struct, IGestureEvent
        {
            if (gestureHandler == null)
            {
                throw new ArgumentNullException(nameof(gestureHandler));
            }

            AddEventHandler(gestureHandler, includeHierarchy);
        }

        /// <summary>
        /// Unsubscribe from a gesture event previously subscribed to via <see cref="AddGestureHandler{TGesture}(UIEventHandler{TGesture}, bool)"/>.
        /// </summary>
        /// <typeparam name="TGesture">The type of gesture event to handle.</typeparam>
        /// <param name="gestureHandler">The callback to remove from the subscription list.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="gestureHandler"/> is <c>null</c>.</exception>
        /// <seealso cref="Gesture.OnClick"/>
        /// <seealso cref="Gesture.OnPress"/>
        /// <seealso cref="Gesture.OnRelease"/>
        /// <seealso cref="Gesture.OnHover"/>
        /// <seealso cref="Gesture.OnUnhover"/>
        /// <seealso cref="Gesture.OnScroll"/>
        /// <seealso cref="Gesture.OnMove"/>
        /// <seealso cref="Gesture.OnDrag"/>
        /// <seealso cref="Gesture.OnCancel"/>
        /// <seealso cref="FireGestureEvent{TGesture}(TGesture)"/>
        public void RemoveGestureHandler<TGesture>(UIEventHandler<TGesture> gestureHandler) where TGesture : struct, IGestureEvent
        {
            if (gestureHandler == null)
            {
                throw new ArgumentNullException(nameof(gestureHandler));
            }

            RemoveEventHandler(gestureHandler);
        }

        /// <summary>
        /// Fire a gesture event on this <see cref="UIBlock"/>.
        /// </summary>
        /// <remarks>
        /// The event will traverse <i>up</i> the <see cref="UIBlock"/> hierarchy until it reaches a <see cref="UIBlock"/> ancestor (inclusive of this <see cref="UIBlock"/>) with a registered event handler for the given gesture type, <typeparamref name="TGesture"/>.
        /// </remarks>
        /// <typeparam name="TGesture">The type of gesture event to fire.</typeparam>
        /// <seealso cref="Gesture.OnClick"/>
        /// <seealso cref="Gesture.OnPress"/>
        /// <seealso cref="Gesture.OnRelease"/>
        /// <seealso cref="Gesture.OnHover"/>
        /// <seealso cref="Gesture.OnUnhover"/>
        /// <seealso cref="Gesture.OnScroll"/>
        /// <seealso cref="Gesture.OnMove"/>
        /// <seealso cref="Gesture.OnDrag"/>
        /// <seealso cref="Gesture.OnCancel"/>
        /// <seealso cref="AddGestureHandler{TGesture}(UIEventHandler{TGesture}, bool)"/>
        /// <seealso cref="RemoveGestureHandler{TGesture}(UIEventHandler{TGesture})"/>
        public void FireGestureEvent<TGesture>(TGesture gestureEvent) where TGesture : struct, IGestureEvent
        {
            gestureEvent.Receiver = this;
            gestureEvent.Target = this;
            FireEvent(gestureEvent);
        }
        #endregion

        #endregion

        #region Internal
        [SerializeField]
        private Layout layout = Layout.TwoD;

        [SerializeField]
        private AutoLayout autoLayout = AutoLayout.Disabled;

        ref Internal.Layout ILayoutBlock.SerializedLayout => ref UnsafeUtility.As<Layout, Internal.Layout>(ref layout);
        ref Internal.AutoLayout ILayoutBlock.SerializedAutoLayout => ref UnsafeUtility.As<AutoLayout, Internal.AutoLayout>(ref autoLayout);

        Vector3 ILayoutBlock.PreviewSize { get => PreviewSize; set { PreviewSize = value; } }

        ref readonly Internal.Length3.Calculated ILayoutBlock.CalculatedSize => ref LayoutDataStore.Instance.AccessCalc(this).Size;
        ref readonly Internal.Length3.Calculated ILayoutBlock.CalculatedPosition => ref LayoutDataStore.Instance.AccessCalc(this).Position;
        ref readonly Internal.LengthBounds.Calculated ILayoutBlock.CalculatedPadding => ref LayoutDataStore.Instance.AccessCalc(this).Padding;
        ref readonly Internal.LengthBounds.Calculated ILayoutBlock.CalculatedMargin => ref LayoutDataStore.Instance.AccessCalc(this).Margin;

        [SerializeField]
        private protected BaseRenderInfo visibility = BaseRenderInfo.Default(BlockType.Empty);

        ref Internal.BaseRenderInfo IRenderBlock.BaseRenderInfo => ref UnsafeUtility.As<BaseRenderInfo, Internal.BaseRenderInfo>(ref visibility);

        [SerializeField]
        private protected Surface surface = Surface.DefaultUnlit;
        ref Internal.Surface IRenderBlock.Surface
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.As<Surface, Internal.Surface>(ref surface);
        }

        [SerializeField, HideInInspector]
        [NotKeyable]
        internal Vector3 PreviewSize = Vector2.one;

        private protected override bool IsBatchRoot => RenderingDataStore.Instance.IsNonHierarchyBatchRoot(ID);
        private protected override void HandleParentChanged()
        {
            RenderingDataStore.Instance.HandleNodeParentChanged(ID, Self.IsHierarchyRoot);
        }

        private protected override void EditorOnly_EnsureTransformRegistration() => LayoutDataStore.Instance.TransformTracker.SetTransformTrackingState(this);

        private void Reset()
        {
            ILayoutBlock layoutBlock = this;

            if (!LayoutDataStore.Instance.HasReceivedFullEngineUpdate(layoutBlock))
            {
                // Persist transform position if being first added as a component and/or disabled
                Internal.Length3 position = layoutBlock.SerializedLayout.Position;
                position.Raw = transform.localPosition;
                position.IsRelative = false;
                layoutBlock.SerializedLayout.Position = position;
            }

            if (Self.IsRegistered)
            {
                CopyToDataStore();
            }
        }


        internal void CopyToDataStore()
        {
            visibility.GameObjectLayer = gameObject.layer;
            HierarchyDataStore.Instance.CopyToStore(this);
            LayoutDataStore.Instance.CopyToStore(this);
            RenderingDataStore.Instance.CopyToStore(this);
        }

        internal override void CloneFromSource(DataStoreID sourceID)
        {
            HierarchyDataStore.Instance.Clone(sourceID, this);
            LayoutDataStore.Instance.Clone(sourceID, this);
            RenderingDataStore.Instance.Clone(sourceID, this);
        }

        internal override void CopyFromDataStore()
        {
            HierarchyDataStore.Instance.CopyFromStore(this);
            LayoutDataStore.Instance.CopyFromStore(this);
            RenderingDataStore.Instance.CopyFromStore(this);
        }

        private protected override void Register()
        {
            visibility.GameObjectLayer = gameObject.layer;
            HierarchyDataStore.Instance.Register(this);
            LayoutDataStore.Instance.Register(this);
            RenderingDataStore.Instance.Register(this);
        }

        private protected override void Unregister()
        {
            // unregister with data stores in opposite order of register
            RenderingDataStore.Instance.Unregister(this);
            LayoutDataStore.Instance.Unregister(this);
            HierarchyDataStore.Instance.Unregister(this);
        }

        /// <summary>
        /// Some blocks need to do some extra work whenever the editors change
        /// things, so this function is used instead of <see cref="CopyToDataStore"/>.
        /// </summary>
        internal virtual void EditorOnly_MarkDirty()
        {
            CopyToDataStore();
        }

        private protected override void OnBlockEnabled() { }

        private protected override void OnBlockDisabled()
        {
            if (Application.isPlaying)
            {
                TryCancelInput();
            }
        }

        /// <summary>
        /// This is an undocumented event from Unity. It is called
        /// whenever an Animator component updates a serialized field
        /// on this object.
        /// </summary>
        [Obfuscation]
        private protected virtual void OnDidApplyAnimationProperties()
        {
            CopyToDataStore();
        }

        bool IRenderBlock.Visible
        {
            get => Visible;
            set => Visible = value;
        }

        internal CalculatedLayout CalculatedLayout => LayoutDataStore.Instance.AccessCalc(this).Reinterpret<Internal.Layouts.CalculatedLayout, CalculatedLayout>();

        internal Vector3 ContentSize => Self.Index.IsValid ? (Vector3)LayoutDataStore.Instance.GetContentSize(this) : Vector3.zero;
        internal Vector3 ContentCenter => Self.Index.IsValid ? (Vector3)LayoutDataStore.Instance.GetContentCenter(this) : Vector3.zero;

        internal Vector3 HierarchySize => Self.Index.IsValid ? (Vector3)LayoutDataStore.Instance.GetHierarchySize(this) : Vector3.zero;
        internal Vector3 HierarchyCenter => Self.Index.IsValid ? (Vector3)LayoutDataStore.Instance.GetHierarchyCenter(this) : Vector3.zero;


        /// <summary>
        /// World size of UIBlock, accounting for <c><see cref="Transform.lossyScale">transform.scale</see></c>
        /// </summary>
        internal Vector3 GetWorldSize(bool includeMargin)
        {
            if (!includeMargin)
            {
                return Vector3.Scale(RotatedSize, transform.lossyScale);
            }

            Vector3 parentScale = transform.parent == null ? Vector3.one : transform.parent.lossyScale;

            return Vector3.Scale(RotatedSize, transform.lossyScale) + Vector3.Scale(CalculatedMargin.Size, parentScale);
        }

        Vector3 ILayoutBlock.PaddedSize => PaddedSize;

        Vector3 ILayoutBlock.RotatedSize => RotatedSize;
        Vector3 ILayoutBlock.LayoutSize => LayoutSize;

        internal ref readonly AutoLayout GetAutoLayoutReadOnly()
        {
            if (!Self.IsRegistered)
            {
                return ref autoLayout;
            }

            unsafe
            {
                return ref UnsafeUtility.As<Internal.AutoLayout, AutoLayout>(ref LayoutDataStore.Instance.AccessAutoLayoutReadOnly(this));
            }
        }

        internal bool LayoutIsDirty
        {
            get
            {
                return LayoutDataStore.Instance.GetLayoutDirty(this);
            }
        }

        void ILayoutBlock.CalculateLayout() => CalculateLayout();

        IInputTarget IUIBlock.InputTarget => InputTarget;

        [HideInInspector, NonSerialized]
        private InputModule inputModule = null;
        internal IInputTarget InputTarget
        {
            get
            {
                if (inputModule == null)
                {
                    inputModule = new InputModule(this);
                }

                return inputModule;
            }
        }

        /// <summary>
        /// Accessing InputTarget directly will initialize it, which is generally fine (it's internal)
        /// since most accesses are for actual input processing, but sometimes it does mean the InputModule
        /// will get initialized before it's *really* needed. This is a way to check for CapturesInput
        /// without lazy-initializing the InputModule
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        internal bool CapturesInput<T>() where T : unmanaged, IEquatable<T>
        {
            if (inputModule == null)
            {
                return false;
            }

            return InputTarget.CapturesInput<T>();
        }

        /// <summary>
        /// Accessing InputTarget directly will initialize it, which is generally fine (it's internal)
        /// since most accesses are for actual input processing, but sometimes it does mean the InputModule
        /// will get initialized before it's *really* needed. This is a way to CancelInput without
        /// lazy-initializing the InputModule
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private protected void TryCancelInput()
        {
            if (inputModule == null)
            {
                return;
            }

            InputTarget.CancelInput();
        }

        [HideInInspector, NonSerialized]
        private EventModule eventModule = null;
        internal EventModule EventModule
        {
            get
            {
                if (eventModule == null)
                {
                    eventModule = new EventModule(this);
                }

                return eventModule;
            }
        }

        /// <summary>
        /// Prevent users from inheriting
        /// </summary>
        internal UIBlock() { }

        internal void AddEventHandler<TEvent>(UIEventHandler<TEvent> eventHandler, bool includeHierarchy) where TEvent : struct, IEvent => EventModule.RegisterHandler(eventHandler, !includeHierarchy);
        internal void RemoveEventHandler<TEvent>(UIEventHandler<TEvent> eventHandler) where TEvent : struct, IEvent => EventModule.UnregisterHandler(eventHandler);
        internal void AddEventHandler<TEvent, TTarget>(UIEventHandler<TEvent, TTarget> eventHandler) where TEvent : struct, IEvent where TTarget : class, IEventTarget => EventModule.RegisterHandler(eventHandler);
        internal void RemoveEventHandler<TEvent, TTarget>(UIEventHandler<TEvent, TTarget> eventHandler) where TEvent : struct, IEvent where TTarget : class, IEventTarget => EventModule.UnregisterHandler(eventHandler);

        internal void RegisterEventTargetProvider(IEventTargetProvider targetProvider) => EventModule.RegisterEventTargetProvider(targetProvider);
        internal void UnregisterEventTargetProvider(IEventTargetProvider targetProvider) => EventModule.UnregisterEventTargetProvider(targetProvider);

        internal void FireEvent<TEvent>(TEvent evt, Type targetTypeFilter = null) where TEvent : struct, IEvent => EventModule.Dispatch(this, evt, targetTypeFilter);
        #endregion
    }
}

