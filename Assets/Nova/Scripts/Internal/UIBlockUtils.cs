// Copyright (c) Supernova Technologies LLC
using Nova.Internal;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using UnityEngine;

namespace Nova
{
    internal static class UIBlockUtils
    {
        public static void SetLayoutOffsetFromLocalPosition(UIBlock uiBlock, Vector3 localPosition)
        {
            IUIBlock parent = uiBlock.GetParentBlock();
            bool hasParent = parent != null;

            Vector3 parentSize = hasParent ? parent.PaddedSize : Vector3.zero;
            Vector3 paddingOffset = hasParent ? (Vector3)parent.CalculatedPadding.Offset : Vector3.zero;

            if (hasParent && parent.IsVirtual)
            {
                localPosition -= parent.GetCalculatedTransformLocalPosition();
            }

            Vector3 layoutOffset = LayoutUtils.LocalPositionToLayoutOffset(localPosition, uiBlock.LayoutSize, uiBlock.CalculatedMargin.Offset, parentSize, paddingOffset, (Vector3)uiBlock.Alignment);

            uiBlock.Position.Raw = Length3.GetRawValue(layoutOffset, uiBlock.Position, uiBlock.PositionMinMax, parentSize);
        }
    }
}
