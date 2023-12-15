using UnityEngine;

namespace Nessie.ASE.Editor
{
    public static class EditorStyles
    {
        public static GUIStyle ColorTooltip;
        
        static EditorStyles()
        {
            ColorTooltip = new GUIStyle(GUI.skin.textField)
            {
                padding = new RectOffset(2, 2, 2, 2)
            };
        }
    }
}
