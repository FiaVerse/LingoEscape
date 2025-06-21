// Copyright (c) Supernova Technologies LLC
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor.GUIs
{
    internal static class Labels
    {
        private static string _iconPath = null;
        public static string IconPath
        {
            get
            {
                if (_iconPath == null)
                {
                    string[] paths = AssetDatabase.FindAssets("UIBlock2DIcon t:texture2D");
                    if (paths.Length == 0)
                    {
                        Debug.LogWarning("Failed to find Nova icons path");
                    }

                    _iconPath = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(paths[0]));
                }

                return _iconPath;
            }
        }

        private const string DarkIconExtension = "_DarkIcon.png";
        private const string LightIconExtension = "_LightIcon.png";

        public const string WarningIconString = "Warning@2x";
        public static readonly GUIContent WarningIcon = EditorGUIUtility.TrIconContent(WarningIconString);

        private static readonly GUIContent[][] AlignmentLight = new GUIContent[][]
        {
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignLeft{LightIconExtension}")) { tooltip = "Left Aligned"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignCenterX{LightIconExtension}")) { tooltip = "X Center Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignRight{LightIconExtension}")) { tooltip = "Right Aligned" }
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignBottom{LightIconExtension}")) { tooltip = "Bottom Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignCenterY{LightIconExtension}")) { tooltip = "Y Center Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignTop{LightIconExtension}")) { tooltip = "Top Aligned" }
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignFront{LightIconExtension}")) { tooltip = "Front Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignCenterZ{LightIconExtension}")) { tooltip = "Z Center Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignBack{LightIconExtension}")) { tooltip = "Back Aligned" }
            }
        };

        private static readonly GUIContent[][] AlignmentDark = new GUIContent[][]
        {
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignLeft{DarkIconExtension}")) { tooltip = "Left Aligned"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignCenterX{DarkIconExtension}")) { tooltip = "X Center Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignRight{DarkIconExtension}")) { tooltip = "Right Aligned" }
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignBottom{DarkIconExtension}")) { tooltip = "Bottom Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignCenterY{DarkIconExtension}")) { tooltip = "Y Center Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignTop{DarkIconExtension}")) { tooltip = "Top Aligned" }
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignFront{DarkIconExtension}")) { tooltip = "Front Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignCenterZ{DarkIconExtension}")) { tooltip = "Z Center Aligned" },
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/AlignBack{DarkIconExtension}")) { tooltip = "Back Aligned" }
            }
        };

        public static GUIContent[][] Alignment => EditorGUIUtility.isProSkin ? AlignmentLight : AlignmentDark;

        public static readonly GUIContent[][] OrderLight = new GUIContent[][]
        {
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/LeftToRight{LightIconExtension}")) { tooltip = "Order Left to Right"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/RightToLeft{LightIconExtension}")) { tooltip = "Order Right to Left"}
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/TopToBottom{LightIconExtension}")) { tooltip = "Order Top to Bottom"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/BottomToTop{LightIconExtension}")) { tooltip = "Order Bottom to Top"}
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/FrontToBack{LightIconExtension}")) { tooltip = "Order Front to Back"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/BackToFront{LightIconExtension}")) { tooltip = "Order Back to Front"}
            }
        };


        public static readonly GUIContent[][] TMPAlignment = new GUIContent[][]
        {
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent("align_horizontally_left")) { tooltip = "Left"},
                new GUIContent(EditorGUIUtility.TrIconContent("align_horizontally_center")) { tooltip = "Center" },
                new GUIContent(EditorGUIUtility.TrIconContent("align_horizontally_right")) { tooltip = "Right" }
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent("align_vertically_bottom")) { tooltip = "Bottom" },
                new GUIContent(EditorGUIUtility.TrIconContent("align_vertically_center")) { tooltip = "Middle" },
                new GUIContent(EditorGUIUtility.TrIconContent("align_vertically_top")) { tooltip = "Top" }
            }
        };

        public static readonly GUIContent[][] OrderDark = new GUIContent[][]
        {
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/LeftToRight{DarkIconExtension}")) { tooltip = "Order Left to Right"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/RightToLeft{DarkIconExtension}")) { tooltip = "Order Right to Left"}
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/TopToBottom{DarkIconExtension}")) { tooltip = "Order Top to Bottom"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/BottomToTop{DarkIconExtension}")) { tooltip = "Order Bottom to Top"}
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/FrontToBack{DarkIconExtension}")) { tooltip = "Order Front to Back"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/BackToFront{DarkIconExtension}")) { tooltip = "Order Back to Front"}
            }
        };

        public static GUIContent[][] Order => EditorGUIUtility.isProSkin ? OrderLight : OrderDark;

        public static readonly GUIContent[][] AutoSizeDark = new GUIContent[][]
        {
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ExpandX{DarkIconExtension}")) { tooltip = "Expand to Parent"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ShrinkX{DarkIconExtension}")) { tooltip = "Shrink to Children"}
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ExpandY{DarkIconExtension}")) { tooltip = "Expand to Parent"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ShrinkY{DarkIconExtension}")) { tooltip = "Shrink to Children"}
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ExpandZ{DarkIconExtension}")) { tooltip = "Expand to Parent"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ShrinkZ{DarkIconExtension}")) { tooltip = "Shrink to Children"}
            },
        };

        public static readonly GUIContent[][] AutoSizeLight = new GUIContent[][]
        {
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ExpandX{LightIconExtension}")) { tooltip = "Expand to Parent"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ShrinkX{LightIconExtension}")) { tooltip = "Shrink to Children"}
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ExpandY{LightIconExtension}")) { tooltip = "Expand to Parent"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ShrinkY{LightIconExtension}")) { tooltip = "Shrink to Children"}
            },
            new GUIContent[]
            {
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ExpandZ{LightIconExtension}")) { tooltip = "Expand to Parent"},
                new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/ShrinkZ{LightIconExtension}")) { tooltip = "Shrink to Children"}
            }
        };

        public static GUIContent[][] AutoSize => EditorGUIUtility.isProSkin ? AutoSizeLight : AutoSizeDark;

        public static readonly GUIContent[] LengthType = new GUIContent[]
        {
            EditorGUIUtility.TrTextContent("V", "Value Length"),
            EditorGUIUtility.TrTextContent("%", "Percent Length")
        };

        public static readonly GUIContent[] LockAspectToolbarLabels3D = new GUIContent[]
        {
            EditorGUIUtility.TrTextContent("X", "Width controls Height and Depth"),
            EditorGUIUtility.TrTextContent("Y", "Height controls Width and Depth"),
            EditorGUIUtility.TrTextContent("Z", "Depth controls Width and Height")
        };

        public static readonly GUIContent[] LockAspectToolbarLabels2D = new GUIContent[]
        {
            EditorGUIUtility.TrTextContent("X", "Width controls Height"),
            EditorGUIUtility.TrTextContent("Y", "Height controls Width")
        };

        public static readonly GUIContent[] AxisToolbarLabels = new GUIContent[]
        {
            EditorGUIUtility.TrTextContent("X", "Position Horizontally"),
            EditorGUIUtility.TrTextContent("Y", "Position Vertically"),
            EditorGUIUtility.TrTextContent("Z", "Position in Z")
        };

        public static readonly GUIContent X = EditorGUIUtility.TrTextContent("X");
        public static readonly GUIContent Y = EditorGUIUtility.TrTextContent("Y");
        public static readonly GUIContent Z = EditorGUIUtility.TrTextContent("Z");

        public static readonly GUIContent Left = EditorGUIUtility.TrTextContent("Left");
        public static readonly GUIContent Right = EditorGUIUtility.TrTextContent("Right");
        public static readonly GUIContent Top = EditorGUIUtility.TrTextContent("Top");
        public static readonly GUIContent Bottom = EditorGUIUtility.TrTextContent("Bottom");
        public static readonly GUIContent Front = EditorGUIUtility.TrTextContent("Front");
        public static readonly GUIContent Back = EditorGUIUtility.TrTextContent("Back");

        public static readonly GUIContent[] XYZ = new GUIContent[] { X, Y, Z };

        public static readonly GUIContent Min = EditorGUIUtility.TrTextContent("Min");
        public static readonly GUIContent Max = EditorGUIUtility.TrTextContent("Max");
        public static readonly GUIContent MinMax = EditorGUIUtility.TrTextContent("Min-Max");
        public static readonly GUIContent Opacity = EditorGUIUtility.TrTextContent(" ", "Opacity");

        public static readonly GUIContent Logo = EditorGUIUtility.TrIconContent($"{IconPath}/NovaLogo.png");

        public class Settings
        {
            private const string LightingModelIncludeTooltip = "The lighting models to include in builds. Including lighting models increases both build time and the size of the final build due to the number of shader variants. Only select models that you know you use in the final build.";

            public static readonly GUIContent LogFlags = EditorGUIUtility.TrTextContent("Log Flags", "Enables or disables warnings that may be logged by Nova.");
            public static readonly GUIContent PackedImages = EditorGUIUtility.TrTextContent("Packed images", "Global toggle for packed images, which reduce the number of draw calls by batching images with the same dimensions, format, and mip count.");
            public static readonly GUIContent SuperSampleText = EditorGUIUtility.TrTextContent("Super Sample Text", "Improves quality of text (especially in VR).");
            public static readonly GUIContent EdgeSoftenWidth = EditorGUIUtility.TrTextContent("Edge Soften Width", "The width (in pixels) of the softening for edges (block edges, clip mask edges, etc.).");
            public static readonly GUIContent PackedImageCopyMode = EditorGUIUtility.TrTextContent("Packed Image Copy Mode", "Specifies how to copy packed images into the texture array if the source texture is compressed using a block based format. Certain older versions of the Nvidia OpenGL driver may crash if a certain mip level of the source texture is not a multiple of the block size. Setting copy mode to \"Skip\" will skip over these mip levels.");
            public static readonly GUIContent UIBlock3DCornerDivisions = EditorGUIUtility.TrTextContent("UIBlock3D Corner Divisions", "The number of divisions for a UIBlock3D's corner radius. A larger value has a greater performance cost but leads to a higher quality mesh.");
            public static readonly GUIContent UIBlock3DEdgeDivisions = EditorGUIUtility.TrTextContent("UIBlock3D Edge Divisions", "The number of divisions for a UIBlock3D's edge radius. A larger value has a greater performance cost but leads to a higher quality mesh.");
            public static readonly GUIContent LightingModelsToBuild = EditorGUIUtility.TrTextContent("Included Lighting Models", LightingModelIncludeTooltip);
            public static readonly GUIContent UIBlock2DLightingModels = EditorGUIUtility.TrTextContent("UIBlock2D", LightingModelIncludeTooltip);
            public static readonly GUIContent UIBlock3DLightingModels = EditorGUIUtility.TrTextContent("UIBlock3D", LightingModelIncludeTooltip);
            public static readonly GUIContent TextBlockLightingModels = EditorGUIUtility.TrTextContent("TextBlock", LightingModelIncludeTooltip);
            public static readonly GUIContent ClickThreshold = EditorGUIUtility.TrTextContent("Click Frame Threshold", "The number of frames that must separate a \"Press\" and \"Release\" Gesture in order to trigger a Click. For low-accuracy input devices (e.g. VR hand tracking), a higher value (such as 3) might be required to reduce noise. For high-accuracy input devices (e.g. mouse and touch), 1 should be sufficient.");
            public static readonly GUIContent EdgeSnapping = EditorGUIUtility.TrTextContent("Edge Snapping", "Enables or disables edge detection and snapping for all Nova editor tools (e.g. UIBlock Tool and Padding/Margin Tool).");
            public static readonly GUIContent HierarchyGizmos = EditorGUIUtility.TrTextContent("Hierarchy Gizmos", "Enables or disables outlining every UIBlock in the selection hierarchy. Only applicable while scene Gizmos are enabled.");
        }

        public static class Tools
        {
            private static readonly GUIContent ThreeDToggleLight = new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/3DToggle{LightIconExtension}")) { tooltip = "Show All 3D Properties." };
            private static readonly GUIContent ThreeDToggleDark = new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/3DToggle{DarkIconExtension}")) { tooltip = "Show All 3D Properties." };

            public static GUIContent ThreeDToggle => EditorGUIUtility.isProSkin ? ThreeDToggleLight : ThreeDToggleDark;
            public static readonly GUIContent PreviewSize = EditorGUIUtility.TrTextContent("Preview", "An Edit-Mode-Only utility for UI Block scene and prefab roots to preview percent-based properties when detached from a parent UI Block.");
        }

        public static class AutoLayout
        {
            public static readonly GUIContent Enabled = EditorGUIUtility.TrTextContent("Enabled", "Enable to select an Axis.");
            public static readonly GUIContent PrimaryAxis = EditorGUIUtility.TrTextContent("Primary Axis", "Position all child elements sequentially along this axis. \n\nWhen applicable, content scrolls along this axis.");
            public static readonly GUIContent Spacing = EditorGUIUtility.TrTextContent("Spacing", "The space to insert between children.");
            public static readonly GUIContent AutoSpace = EditorGUIUtility.TrTextContent("Auto", "Automatically adjust spacing so that the child UI Blocks fill the available space in the parent container.");
            public static readonly GUIContent AutoSpaceDisabled = EditorGUIUtility.TrTextContent("Auto", "Auto spacing cannot be used with a ListView or GridView.\n\nAutomatically adjust spacing so that the child UI Blocks fill the available space in the parent container.");
            public static readonly GUIContent Axis = EditorGUIUtility.TrTextContent("Axis", "The axis along which children are positioned. Must be \"Enabled\" to configure.");
            public static readonly GUIContent Alignment = EditorGUIUtility.TrTextContent("Alignment", "Alignment of the children.");
            public static readonly GUIContent Order = EditorGUIUtility.TrTextContent("Order", "The order of the children.");
            public static readonly GUIContent Offset = EditorGUIUtility.TrTextContent("Offset", "An offset applied to all children.");
            public static readonly GUIContent CrossAxis = EditorGUIUtility.TrTextContent("Cross Axis", "Create a 2D layout which will first position as many children as will fit along the cross axis before wrapping to the primary axis.");
            public static readonly GUIContent CrossAxisDisabled = EditorGUIUtility.TrTextContent("Cross Axis", "Cross Axis cannot be used with a ListView or GridView.\n\nCreate a 2D layout which will first position as many children as will fit along the cross axis before wrapping to the primary axis.");
            public static readonly GUIContent ExpandToGrid = EditorGUIUtility.TrTextContent("Expand to Grid", "Makes the cross axis size of \"Expanded\" elements as well as \"Auto\" spacing more uniform, so the remaining overflow items better align to implicit grid cells. \n\nIf the configured minimum size of a given \"Expanded\" child is larger than the configured minimum size of one or more \"Expanded\" siblings, the child will span across multiple grid cells.\n\nDoes not impact the size of elements not set to \"Expand\".");
        }

        public static class UIBlock2D
        {
            public static readonly GUIContent Color = EditorGUIUtility.TrTextContent("Color", "The color of the body.");
            public static readonly GUIContent CornerRadius = EditorGUIUtility.TrTextContent("Corner Radius", "The radius of the corners of the body, border, and shadow.");
            public static readonly GUIContent SoftenEdges = EditorGUIUtility.TrTextContent("Soften Edges", "In certain situations, like when rendering a texture that has transparency which handles softening edges, having Nova add additional edge softening may not be desired.");
        }

        public static class UIBlock3D
        {
            public static readonly GUIContent Color = EditorGUIUtility.TrTextContent("Color", "The color of the UI Block.");
            public static readonly GUIContent CornerRadius = EditorGUIUtility.TrTextContent("Corner Radius", "The radius of the front and back face corners.");
            public static readonly GUIContent EdgeRadius = EditorGUIUtility.TrTextContent("Edge Radius", "The radius of the front and back face edges.");
        }

        public static class Rendering
        {
            public static readonly GUIContent Visible = EditorGUIUtility.TrTextContent("Visible", "The visibility of all rendered visuals on this UI Block.");
            public static readonly GUIContent ZIndex = EditorGUIUtility.TrTextContent("Z-Index", "Overrides the default render order for coplanar, 2D elements within a Sort Group. Higher values are drawn on top.");
        }

        public static class Size
        {
            public static readonly GUIContent Label = EditorGUIUtility.TrTextContent("Size", "The size of the UI Block.");
            public static readonly GUIContent AutoSize = EditorGUIUtility.TrTextContent("Auto Size", "Make size adapt to size of parent or children.");
            public static readonly GUIContent RotateSize = EditorGUIUtility.TrTextContent("Rotate Size", "Specifies whether or not to include the UI Block's rotation when calculating size.");
        }

        public static class Position
        {
            public static readonly GUIContent Label = EditorGUIUtility.TrTextContent("Position", "The position of the UI Block.");
            public static readonly GUIContent Alignment = EditorGUIUtility.TrTextContent("Alignment", "The alignment relative to parent's padded size.");
        }

        public static class ItemView
        {
            public static readonly GUIContent Visuals = EditorGUIUtility.TrTextContent("Visuals", "The set of visual fields.");
        }

        public static class GestureRecognizer
        {
            public static readonly GUIContent ObstructDragsLabel = EditorGUIUtility.TrTextContent("Obstruct Drags", "When true, drag gestures will not be routed to content behind the attached UIBlock.\nWhen false, content behind the attached UIBlock can receive drag gestures if it is \"draggable\" in a direction this component is not.\nDoes not impact the \"draggable\" state of this component.");
            public static readonly GUIContent DragThresholdLabel = EditorGUIUtility.TrTextContent("Drag Threshold", "The threshold that must be surpassed to initiate a drag event.\n\nThis threshold is generally used for high precision input devices, E.g. mouse, touchscreen, VR controller, and most other ray based input.");
            public static readonly GUIContent LowAccuracyDragThresholdLabel = EditorGUIUtility.TrTextContent("Low Accuracy Drag Threshold", "The threshold that must be surpassed to initiate a drag event.\n\nThis \"Low Accuracy\" threshold is generally used for noisier (i.e. less physically stable) input devices, E.g. VR handtracking and other sphere collider based input.");
            public static readonly GUIContent ClickBehaviorLabel = EditorGUIUtility.TrTextContent("Click Behavior", "Determines when this component should trigger click events.");
            public static readonly GUIContent NavigableLabel = EditorGUIUtility.TrTextContent("Navigable", "Is this component navigable?");
            public static readonly GUIContent NavigationLabel = EditorGUIUtility.TrTextContent("Navigation", "Defines a navigation target per axis-aligned direction.");

            public static readonly GUIContent OnSelectLabel = EditorGUIUtility.TrTextContent("On Select", "Determines how this component should handle navigation \"Select\" events.");
            public static readonly GUIContent AutoSelectLabel = EditorGUIUtility.TrTextContent("Auto Select", "Determines if this component should automatically be selected whenever it's navigated to.\n\nIf \"On Select\" is set to \"Scope Navigation\", setting this to True will effectively allow navigation moves to pass through the attached UIBlock onto the navigable descendant closest to the navigation source.");

            private static readonly GUIContent navGraphEnabled = EditorGUIUtility.TrIconContent("scenevis_visible_hover", "Hide Navigation Graph");
            private static readonly GUIContent navGraphDisabled = EditorGUIUtility.TrIconContent("scenevis_hidden_hover", "Show Navigation Graph");

            private static readonly GUIContent navGraphFilterToSelection = EditorGUIUtility.TrIconContent("scenepicking_pickable_hover", "Filter Graph to Selection");
            private static readonly GUIContent navGraphNoFilter = EditorGUIUtility.TrIconContent("scenepicking_notpickable_hover", "Show Full Graph");

            public static GUIContent GetNavGraphFilterLabel(bool filtered)
            {
                return filtered ? navGraphNoFilter : navGraphFilterToSelection;
            }

            public static GUIContent GetNavGraphLabel(bool graphEnabled)
            {
               return graphEnabled ? navGraphEnabled : navGraphDisabled;
            }
        }

        public static class Interactable
        {
            public static readonly GUIContent DraggableLabel = EditorGUIUtility.TrTextContent("Draggable", "Acts as a bit - mask indicating which axes can trigger drag events once a \"drag threshold\" is surpassed.");
            public static readonly GUIContent GestureSpaceLabel = EditorGUIUtility.TrTextContent("Gesture Space", "The coordinate space used to track gesture positions across frames.\n\nIf the local position, local rotation, or local scale of the UIBlock root may change while a gesture on this Interactable is active, use \"World\", otherwise \"Root\" is recommended.");
        }

        public static class Scroller
        {
            public static readonly GUIContent OverscrollEffectLabel = EditorGUIUtility.TrTextContent("Overscroll Effect", "The behavior applied when there's no more content to scroll in the scrolling direction");
            public static readonly GUIContent DragScrollingLabel = EditorGUIUtility.TrTextContent("Drag Scrolling", "Allow Interactions.Point drag events to trigger a scroll.\n\nE.g. Dragging on a touch screen.");
            public static readonly GUIContent VectorScrollingLabel = EditorGUIUtility.TrTextContent("Vector Scrolling", "Allow Interactions.Scroll vector events to trigger a scroll.\n\nE.g. Scroll wheel on a mouse.");
            public static readonly GUIContent VectorScrollMultiplierLabel = EditorGUIUtility.TrTextContent("Vector Scroll Multiplier", "The speed multiplier for vector scrolling");
            public static readonly GUIContent DragScrollbarLabel = EditorGUIUtility.TrTextContent("Draggable Scrollbar", "Indicates whether or not the Scroller should handle drag events on the Scrollbar Visual automatically.");
            public static readonly GUIContent ScrollbarVisualLabel = EditorGUIUtility.TrTextContent("Scrollbar Visual", "The visual representing the scroll position of content relative to the viewport. The Scroller component will adjust this visual's size/position along the scrolling axis");
        }

        public static class NavLink
        {
            public static readonly GUIContent TypeLabel = EditorGUIUtility.TrTextContent(nameof(Nova.NavLink.Type), "The type of navigation to perform in this direction.");
            public static readonly GUIContent TargetLabel = EditorGUIUtility.TrTextContent(nameof(Nova.NavLink.Target), "The Interactable or Scroller to navigate to when \"Type\" is set to \"Manual\".");
            public static readonly GUIContent FallbackLabel = EditorGUIUtility.TrTextContent(nameof(Nova.NavLink.Fallback), "Designates the fallback behavior in the event a navigation target isn't found or is not configured to be navigable.");
            public static readonly GUIContent TargetNotNavigableWarningLabel = EditorGUIUtility.TrIconContent(WarningIconString, "\"Target\" not configured to be navigable. Will result in \"Fallback\" behavior.");
        }

        public static class NavNode
        {
            private static readonly GUIContent ThreeDToggleLight = new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/3DToggle{LightIconExtension}")) { tooltip = "Show navigation directions for Z axis." };
            private static readonly GUIContent ThreeDToggleDark = new GUIContent(EditorGUIUtility.TrIconContent($"{IconPath}/3DToggle{DarkIconExtension}")) { tooltip = "Show navigation directions for Z axis." };

            public static GUIContent ThreeDToggle => EditorGUIUtility.isProSkin ? ThreeDToggleLight : ThreeDToggleDark;
        }

        public static class ClipMask
        {
            public static readonly GUIContent Tint = EditorGUIUtility.TrTextContent("Tint", "The tint color to apply to this block and its descendants.");
            public static readonly GUIContent Clip = EditorGUIUtility.TrTextContent("Clip", "Enables or disables clipping. Can be used to make the clip mask exclusively apply a tint.");
            public static readonly GUIContent Mask = EditorGUIUtility.TrTextContent("Mask", "The texture to use as a mask, if \"Clip\" is enabled.");
        }

        public static class SortGroup
        {
            private const string RenderQueueTitle = "Render Queue";
            private const string RenderOverOpaqueGeometryTitle = "Render Over Opaque Geometry";
            private const string OverriddenTooltip = "This value is inherited from the Screen Space root.";

            public static readonly GUIContent SortingOrder = EditorGUIUtility.TrTextContent("Sorting Order", "The sorting order of this hierarchy relative to other coplanar Nova content. Higher values render on top.");
            public static readonly GUIContent RenderQueue = EditorGUIUtility.TrTextContent(RenderQueueTitle, "The value that will be assigned to the material's render queue for the hierarchy.");
            public static readonly GUIContent RenderQueue_Overridden = EditorGUIUtility.TrTextContent(RenderQueueTitle, OverriddenTooltip);
            public static readonly GUIContent RenderOverOpaqueGeometry = EditorGUIUtility.TrTextContent(RenderOverOpaqueGeometryTitle, "Whether or not the content in the sort group should render over geometry rendered in the opaque render queue. This is useful for rendering in screen space.");
            public static readonly GUIContent RenderOverOpaqueGeometry_Overridden = EditorGUIUtility.TrTextContent(RenderOverOpaqueGeometryTitle, OverriddenTooltip);
        }

        public static class ScreenSpace
        {
            public static readonly GUIContent TargetCamera = EditorGUIUtility.TrTextContent("Target Camera", "The target camera used to render the Nova content.");
            public static readonly GUIContent AdditionalCameras = EditorGUIUtility.TrTextContent("Additional Cameras", "Additional cameras that the ScreenSpace content will be rendered to.\nNOTE: The content will still be positioned and size based on the Target Camera, it will simply also render to these additional cameras.");
            public static readonly GUIContent Mode = EditorGUIUtility.TrTextContent("Fill Mode", $"The mode used to render the content:\n-{nameof(Nova.ScreenSpace.FillMode.FixedWidth)}: Maintains the {nameof(Nova.ScreenSpace.ReferenceResolution)} width on the root UIBlock, adjusting the height to match the camera's aspect ratio.\n-{nameof(Nova.ScreenSpace.FillMode.FixedHeight)}: Maintains the {nameof(Nova.ScreenSpace.ReferenceResolution)} height on the root UIBlock, adjusting the width to match the camera's aspect ratio.\n-{nameof(Nova.ScreenSpace.FillMode.MatchCameraResolution)}: Sets the root UIBlock's size to match the pixel-dimensions of the camera.\n-{nameof(Nova.ScreenSpace.FillMode.Manual)}: Does not modify the size or scale of the UIBlock. Useful if a custom resize behavior is desired.");
            public static readonly GUIContent ReferenceResolution = EditorGUIUtility.TrTextContent("Reference Resolution", "The resolution to use as a reference when resizing the root UIBlock to match the camera's aspect ratio.");
            public static readonly GUIContent PlaneDistance = EditorGUIUtility.TrTextContent("Plane Distance", "The distance in front of the camera at which to render the Nova content.");
        }

        public static class PaddingAndMargin
        {
            public static readonly GUIContent Padding = EditorGUIUtility.TrTextContent("Padding", "The padding of the UI Block. Expands inward.");
            public static readonly GUIContent Margin = EditorGUIUtility.TrTextContent("Margin", "The margin of the UI Block. Expands outward.");
        }

        public static class Surface
        {
            public const string DisabledSurfaceSRPWarning = "Lit surfaces are currently only supported in the built-in render pipeline.";
            public static readonly GUIContent SurfaceEffect = EditorGUIUtility.TrTextContent("Surface", "The appearance of this UI Block's mesh surface under scene lighting.");
            public static readonly GUIContent LightingModel = EditorGUIUtility.TrTextContent("Lighting Model", "The lighting model to apply to this UI Block's mesh surface.");
            public static readonly GUIContent ShadowCasting = EditorGUIUtility.TrTextContent("Shadow Casting", "Specifies if the UI Block should cast shadows.");
            public static readonly GUIContent ReceiveShadows = EditorGUIUtility.TrTextContent("Receive Shadows", "Specifies whether or not the UI Block should receive shadows. NOTE: Only opaque blocks can receive shadows.");
            public static readonly GUIContent Specular = EditorGUIUtility.TrTextContent("Specular", "Specular power.");
            public static readonly GUIContent Gloss = EditorGUIUtility.TrTextContent("Gloss", "Specular intensity.");
            public static readonly GUIContent Metallic = EditorGUIUtility.TrTextContent("Metallic", "How metallic the surface appears.");
            public static readonly GUIContent Smoothness = EditorGUIUtility.TrTextContent("Smoothness", "How smooth the surface appears.");
            public static readonly GUIContent SpecularColor = EditorGUIUtility.TrTextContent("Specular Color", "The color of the specular reflections.");
        }

        public static class RadialFill
        {
            public static readonly GUIContent Enabled = EditorGUIUtility.TrTextContent("Radial Fill", "Enable radial fill.");
            public static readonly GUIContent Center = EditorGUIUtility.TrTextContent("Center", "The center position/origin of the radial cutout.");
            public static readonly GUIContent Rotation = EditorGUIUtility.TrTextContent("Rotation", "The angle (in degrees from the positive x-axis) that serves as the basis rotation from which Fill Angle will apply.\n- Positive => counter-clockwise\n- Negative => clockwise");
            public static readonly GUIContent FillAngle = EditorGUIUtility.TrTextContent("Fill Angle", "The arc angle (in degrees) of the fill.\n- Positive => counter-clockwise\n- Negative => clockwise");
        }

        public static class Gradient
        {
            public static readonly GUIContent Label = EditorGUIUtility.TrTextContent("Gradient", "Enable Gradient.");
            public static readonly GUIContent Color = EditorGUIUtility.TrTextContent(string.Empty, "Gradient color.");
            public static readonly GUIContent Center = EditorGUIUtility.TrTextContent("Center", "The center position of the gradient.");
            public static readonly GUIContent Radius = EditorGUIUtility.TrTextContent("Radius", "The radii along the gradient's X and Y axes. Determines the gradient's size.");
            public static readonly GUIContent Rotation = EditorGUIUtility.TrTextContent("Rotation", "The counter-clockwise rotation of the gradient (in degrees) around its center.");
        }

        public static class Image
        {
            public static readonly GUIContent Label = EditorGUIUtility.TrTextContent("Image", "The image to render in the body of this UI Block.");
            public static readonly GUIContent ImageMode = EditorGUIUtility.TrTextContent("Mode", "Specifies how the Nova Engine should store and attempt to batch the image.");
            public static readonly GUIContent ImageScaleMode = EditorGUIUtility.TrTextContent("Scale Mode", "Specifies how to render the image based on the aspect ratio of the image and the UI Block.");
            public static readonly GUIContent ImageCenter = EditorGUIUtility.TrTextContent("Center", "The center position of the image in UV space, where UVs go from (-1, -1) in the bottom-left to (1, 1) in the top-right.");
            public static readonly GUIContent ImageScale = EditorGUIUtility.TrTextContent("Scale", "How much to scale the image in UV space, where UVs go from (-1, -1) in the bottom-left to (1, 1) in the top-right.");
            public static readonly GUIContent PixelsPerUnit = EditorGUIUtility.TrTextContent("Pixels Per Unit Multiplier", "Determines how many pixels from the target image fit into a 1x1 square in the UIBlock2D's local space.");
            public static readonly GUIContent[] TypeLabels = new GUIContent[] { EditorGUIUtility.TrTextContent("T", "Texture"), EditorGUIUtility.TrTextContent("S", "Sprite") };
            public const string SlicedWarningTooltip = "\"Sliced\" Scale Mode isn't compatible with \"Texture\" images. Change the Image Type to \"Sprite\" to used \"Sliced\" Scale Mode.";
        }

        public static class Shadow
        {
            public static readonly GUIContent Color = EditorGUIUtility.TrTextContent("Color", "The color of the shadow. Darker colors create a more shadow-like effect, whereas brighter colors create a more glow-like effect.");
            public static readonly GUIContent Direction = EditorGUIUtility.TrTextContent("Direction", "The direction the shadow will expand.");
            public static readonly GUIContent Width = EditorGUIUtility.TrTextContent("Width", "The width of the shadow, before blur is applied.");
            public static readonly GUIContent Blur = EditorGUIUtility.TrTextContent("Blur", "The blur of the shadow. A larger blur leads to a softer effect, whereas a smaller blur leads to a sharper effect.");
            public static readonly GUIContent Offset = EditorGUIUtility.TrTextContent("Offset", "The offset of the shadow.");
        }

        public static class Border
        {
            public static readonly GUIContent Color = EditorGUIUtility.TrTextContent("Color", "The color of the border.");
            public static readonly GUIContent Width = EditorGUIUtility.TrTextContent("Width", "The width of the border.");
            public static readonly GUIContent Direction = EditorGUIUtility.TrTextContent("Direction", "The direction the border will expand.");
        }

        public static class TMP
        {
            public static readonly GUIContent Alignment = EditorGUIUtility.TrTextContent("Align", "Sets the alignment on the attached Text Mesh Pro component.");
            public static readonly GUIContent Text = EditorGUIUtility.TrTextContent("Text", "Sets the text on the attached Text Mesh Pro component.");
            public static readonly GUIContent Color = EditorGUIUtility.TrTextContent("Color", "Sets the vertex color on the attached Text Mesh Pro component.");
            public static readonly GUIContent Font = EditorGUIUtility.TrTextContent("Font", "Sets the Font Asset on the attached Text Mesh Pro component.");
            public static readonly GUIContent FontSize = EditorGUIUtility.TrTextContent("Font Size", "Sets the font size on the attached Text Mesh Pro component.");
            public static readonly GUIContent Info = new GUIContent(EditorGUIUtility.TrIconContent("d_console.infoicon.inactive.sml")) { tooltip = "[Text] and its expanded properties write directly to the attached Text Mesh Pro object. They are not controlled by this Text Block at runtime." };
        }
    }
}

