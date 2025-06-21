// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Layouts;
using UnityEngine;

namespace Nova.Internal.Utilities.Extensions
{
    internal static class UIBlockExtensionsInternal
    {
        public static Matrix4x4 GetParentToWorldMatrix(this UIBlock uiBlock)
        {
            IUIBlock parent = uiBlock.GetParentBlock();

            if (parent != null)
            {
                return LayoutDataStore.Instance.GetLocalToWorldMatrix(parent);
            }

            return uiBlock.transform.parent != null ? uiBlock.transform.parent.localToWorldMatrix : Matrix4x4.identity;
        }

        public static Vector3 GetWorldSize(this UIBlock uIBlock)
        {
            return Vector3.Scale(uIBlock.RotatedSize, uIBlock.transform.lossyScale);
        }

        public static Vector3 GetScaledSize(this UIBlock uIBlock)
        {
            return Vector3.Scale(uIBlock.RotatedSize, uIBlock.transform.localScale);
        }

        public static Vector3 GetScaledLayoutSize(this UIBlock uiBlock)
        {
            return uiBlock.GetScaledSize() + uiBlock.CalculatedMargin.Size;
        }

        public static Vector3 GetCalculatedTransformLocalPosition(this IUIBlock uiBlock)
        {
            return LayoutDataStore.Instance.GetCalculatedTransformLocalPosition(uiBlock, excludeVirtualParentOffset: true);
        }
    }
}