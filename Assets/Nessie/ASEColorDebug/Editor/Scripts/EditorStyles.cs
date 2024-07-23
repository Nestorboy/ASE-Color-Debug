using UnityEngine;

namespace Nessie.ASE.Editor
{
    public static class EditorStyles
    {
        public static readonly GUIStyle ColorTooltip;
        
        static EditorStyles()
        {
            GUIStyle baseStyle = GUI.skin.textField;
            RectOffset oldPadding = baseStyle.padding;
            ColorTooltip = new GUIStyle(baseStyle)
            {
                padding = new RectOffset(oldPadding.left + 2, oldPadding.right + 2, oldPadding.top + 2, oldPadding.bottom + 2)
            };
        }
    }
}
