// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Editor.GUIs;
using Nova.Internal.Utilities;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
namespace Nova.Editor.Tools
{
    [FallbackTool(typeof(UIBlockTool))]
#if UNITY_2021_2_OR_NEWER
    [EditorTool(displayName: "Gradient Tool", componentToolTarget: typeof(UIBlock2D))]
#else
    [EditorTool(displayName: "Gradient Tool", targetType = typeof(UIBlock2D))]
#endif

    internal class GradientTool : UITool
    {
        public static GradientTool TypedInstance = null;

        private void OnEnable()
        {
            TypedInstance = this;
        }

        public override GUIContent toolbarIcon
        {
            get
            {
                return new GUIContent(EditorGUIUtility.IconContent($"{Labels.IconPath}/GradientToolIcon.png")) { tooltip = "Gradient Tool" };
            }
        }

        private struct Gradient
        {
            public Vector2 Size;
            public Vector2 Offset;
            public float Rotation;
        }

        public override bool IsAvailable()
        {
            UIBlock2D colorBlock = target as UIBlock2D;

            return colorBlock != null && colorBlock.Gradient.Enabled;
        }

        protected override void DoToolGUI()
        {
            if (!IsAvailable())
            {
                ToolManager.SetActiveTool<UIBlockTool>();
                return;
            }

            if (AdjustingMultipleObjects)
            {
                return;
            }

            UIBlock2D colorBlock = UIBlock as UIBlock2D;
            ref RadialGradient gradient = ref colorBlock.Gradient;
            Vector2 halfNodeSize = colorBlock.CalculatedSize.XY.Value;

            Gradient g = new Gradient()
            {
                Size = GetRawValue(ref gradient.Radius, ref halfNodeSize),
                Offset = GetRawValue(ref gradient.Center, ref halfNodeSize),
                Rotation = gradient.Rotation,
            };

            if (ColorBlock2D_TryUpdateGradient(colorBlock, ref g))
            {
                Undo.RecordObject(colorBlock, "Gradient");

                SetGradientValue(ref gradient.Radius, ref g.Size, ref halfNodeSize);
                SetGradientValue(ref gradient.Center, ref g.Offset, ref halfNodeSize);
                gradient.Rotation = g.Rotation;
                RenderingPropertyChanged = true;

                EditModeUtils.QueueEditorUpdateNextFrame();
            }
        }

        private Vector2 GetRawValue(ref Length2 length, ref Vector2 size)
        {
            return new Vector2(
                GetRawValue(ref length.X, ref size.x),
                GetRawValue(ref length.Y, ref size.y));
        }

        private float GetRawValue(ref Length length, ref float size)
        {
            if (length.Type == LengthType.Value)
            {
                return length.Value;
            }
            else
            {
                return length.Percent * size;
            }
        }

        private void SetGradientValue(ref Length2 gradientVal, ref Vector2 val, ref Vector2 nodeHalfSize)
        {
            SetSingleAxisGradientValue(ref gradientVal.X, ref val.x, ref nodeHalfSize.x);
            SetSingleAxisGradientValue(ref gradientVal.Y, ref val.y, ref nodeHalfSize.y);
        }

        private void SetSingleAxisGradientValue(ref Length length, ref float val, ref float nodeHalfSize)
        {
            if (length.Type == LengthType.Value)
            {
                length.Value = val;
            }
            else
            {
                length.Percent = val / nodeHalfSize;
            }
        }

        private bool ColorBlock2D_TryUpdateGradient(UIBlock2D uiBlock, ref Gradient gradient)
        {
            if (!uiBlock.Gradient.Enabled)
            {
                return false;
            }

            Quaternion rotation = Quaternion.AngleAxis(gradient.Rotation, Vector3.forward);
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;

            Vector2 size = uiBlock.CalculatedSize.XY.Value;

            Vector3 gradientCenter = gradient.Offset;
            Vector3 gradientHorizontalPoint = gradientCenter + (gradient.Size.x * right);
            Vector3 gradientVerticalPoint = gradientCenter + (gradient.Size.y * up);

            Vector2 rcpSize = new Vector2(size.x == 0 ? 0 : 1 / size.x, size.y == 0 ? 0 : 1 / size.y);

            using (new Handles.DrawingScope(Color.white, uiBlock.transform.localToWorldMatrix))
            {
                EditorGUI.BeginChangeCheck();
                Handles.color = XAxisColor;
                Handles.DrawAAPolyLine(BoldLineWidth, gradientCenter, gradientHorizontalPoint);

                Vector3 newGradientHorizontalPoint = HandleCompat.FreeMoveHandle(gradientHorizontalPoint, HandleSize * HandleUtility.GetHandleSize(gradientHorizontalPoint), Vector3.zero, Handles.SphereHandleCap);

                bool horizontalChanged = EditorGUI.EndChangeCheck();

                EditorGUI.BeginChangeCheck();
                Handles.color = YAxisColor;
                Handles.DrawAAPolyLine(BoldLineWidth, gradientCenter, gradientVerticalPoint);
                Vector3 newGradientVerticalPoint = HandleCompat.FreeMoveHandle(gradientVerticalPoint, HandleSize * HandleUtility.GetHandleSize(gradientVerticalPoint), Vector3.zero, Handles.SphereHandleCap);
                bool verticalChanged = EditorGUI.EndChangeCheck();

                float angle = 0;

                if (horizontalChanged)
                {
                    Vector3 from = gradientHorizontalPoint - gradientCenter;
                    Vector3 to = newGradientHorizontalPoint - gradientCenter;

                    // in perspective mode, z might change due to Unity's FreeMoveHandle 
                    from.z = to.z = 0;

                    angle = Vector3.SignedAngle(from, to, Vector3.forward);

                    // Will cause a flip, so ignore
                    if (Mathf.Abs(angle) == 180)
                    {
                        angle = 0;
                    }
                }

                if (verticalChanged)
                {
                    Vector3 from = gradientVerticalPoint - gradientCenter;
                    Vector3 to = newGradientVerticalPoint - gradientCenter;

                    // in perspective mode, z might change due to Unity's FreeMoveHandle
                    from.z = to.z = 0;

                    angle = Vector3.SignedAngle(from, to, Vector3.forward);

                    // Will cause a flip, so ignore
                    if (Mathf.Abs(angle) == 180)
                    {
                        angle = 0;
                    }
                }

                if (horizontalChanged || verticalChanged)
                {
                    // check to avoid flips across 270/90 threshold
                    gradient.Rotation = Handles.SnapValue(Math.Mod(gradient.Rotation + angle, 360f), EditorSnapSettings.rotate);

                    gradient.Size = new Vector2(Vector2.Distance(newGradientHorizontalPoint, gradientCenter), Vector2.Distance(newGradientVerticalPoint, gradientCenter));
                    return true;
                }

                EditorGUI.BeginChangeCheck();

                Handles.color = Color.white;
                gradientCenter = HandleCompat.FreeMoveHandle(gradientCenter, HandleSize * HandleUtility.GetHandleSize(gradientCenter), EditorSnapSettings.move, Handles.SphereHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    gradient.Offset = gradientCenter;
                    return true;
                }
            }

            return false;
        }

        protected override void OnToolActivated()
        {
        }
    }
}