using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using HarmonyLib;
using AmplifyShaderEditor;

namespace Nessie.ASE.Editor
{
    [InitializeOnLoad]
    internal class ColorDebug
    {
        #region Private Fields

        private const string m_harmonyID = "Nessie.ASE.ColorDebugPatch";

        private static Texture2D m_previewPixel;

        private static bool m_didBindKeyPress;
        private static bool m_isHoldingHotkey;
        private static bool m_tooltipActive;
        
        #endregion Private Fields

        #region Public Properties

        public static bool UseHotkey => true;

        public static Texture2D PreviewPixel
        {
            get
            {
                if (m_previewPixel == null)
                    m_previewPixel = new Texture2D(
                        1, 1,
                        UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat,
                        UnityEngine.Experimental.Rendering.TextureCreationFlags.None);

                return m_previewPixel;
            }
        }

        #endregion Public Properties

        #region Injection Methods

        static ColorDebug()
        {
            AssemblyReloadEvents.afterAssemblyReload += PostAssemblyReload;
            
            System.Reflection.FieldInfo info = typeof(EditorApplication).GetField("globalEventHandler", ReflectionUtils.PrivateStatic);
            if (info != null)
            {
                EditorApplication.CallbackFunction value = (EditorApplication.CallbackFunction)info.GetValue(null);
                value += OnGlobalKeyPress;
                info.SetValue(null, value);
                m_didBindKeyPress = true;
            }
        }

        private static void PostAssemblyReload()
        {
            Harmony harmony = new Harmony(m_harmonyID);

            harmony.PatchAll();
        }

        private static void OnGlobalKeyPress()
        {
            m_isHoldingHotkey = Event.current != null && Event.current.control;
        }

        [HarmonyPatch(typeof(AmplifyShaderEditorWindow), nameof(AmplifyShaderEditorWindow.UpdateNodePreviewListAndTime))]
        private static class PatchWindowUpdate
        {
            private static void Prefix(AmplifyShaderEditorWindow __instance, ref bool ___m_repaintIsDirty)
            {
                if (!UseHotkey || !m_didBindKeyPress) return;
                
                if (UIUtils.CurrentWindow != __instance) return;
                
                if (m_isHoldingHotkey)
                {
                    m_tooltipActive = true;
                    ___m_repaintIsDirty = true;
                }
                else if (m_tooltipActive)
                {
                    m_tooltipActive = false;
                    ___m_repaintIsDirty = true;
                }
            }
        }

        [HarmonyPatch(typeof(AmplifyShaderEditorWindow), "OnGUI")]
        private static class PatchWindowOnGUI
        {
            private static void Postfix(ParentGraph ___m_customGraph, ParentGraph ___m_mainGraphInstance, Vector2 ___m_currentMousePos2D)
            {
                if (!m_tooltipActive) return;
                
                ParentGraph currentGraph = ___m_customGraph ? ___m_customGraph : ___m_mainGraphInstance;
                ParentNode node = ASEExtensions.GetActiveNode(___m_currentMousePos2D, currentGraph.AllNodes);
                if (!node) return;
                
                ShowColorTooltip(___m_currentMousePos2D, node);
            }
        }

        [HarmonyPatch(typeof(AmplifyShaderEditorWindow), nameof(AmplifyShaderEditorWindow.OnDisable))]
        private static class PatchDisable
        {
            private static void Postfix()
            {
                if (m_previewPixel == null)
                    UnityEngine.Object.DestroyImmediate(PreviewPixel);
            }
        }

        #endregion Injection Methods

        #region Tooltip Methods

        private static void ShowColorTooltip(Vector2 mousePos, ParentNode node)
        {
            Rect previewRect = node.GetPreviewRect();
            if (!previewRect.Contains(mousePos))
            {
                return;
            }

            if (!node.IsPreviewVisible())
            {
                return;
            }
            
            // Push a single pixel from a RT into a Tex2D.
            RenderTexture previousRT = RenderTexture.active;
            RenderTexture.active = node.PreviewTexture;
            Vector2 texelPos = PointToTexelCoordinate(mousePos, previewRect, node.PreviewTexture);
            PreviewPixel.ReadPixels(new Rect(texelPos.x, texelPos.y, 1, 1), 0, 0, false);
            RenderTexture.active = previousRT;

            // Each channel is represented with a full 32-bit float.
            Color color = UnpackColor(PreviewPixel.GetRawTextureData());
            DrawColorTooltip(node, mousePos, color);
        }

        private static Vector2 PointToTexelCoordinate(Vector2 point, Rect textureRect, Texture texture)
        {
            Vector2 normalizedRectPos = PointToRectNormalized(point, textureRect);
            if (!SystemInfo.graphicsUVStartsAtTop)
            {
                normalizedRectPos.y = 1f - normalizedRectPos.y;
            }

            return normalizedRectPos * new Vector2(texture.width, texture.height);
        }
        
        private static Vector2 PointToRectNormalized(Vector2 point, Rect rect)
        {
            return (point - new Vector2(rect.x, rect.y)) / new Vector2(rect.width, rect.height);
        }
        
        private static Color UnpackColor(byte[] bytes)
        {
            float r = BitConverter.ToSingle(bytes, 0);
            float g = BitConverter.ToSingle(bytes, 4);
            float b = BitConverter.ToSingle(bytes, 8);
            float a = BitConverter.ToSingle(bytes, 12);
            return new Color(r, g, b, a);
        }

        private static void DrawColorTooltip(ParentNode node, Vector2 mousePos, Color color)
        {
            string tooltip = FormatColorTooltip(node, color);

            GUIStyle tooltipStyle = EditorStyles.ColorTooltip;
            Vector2 rectSize = tooltipStyle.CalcSize(new GUIContent(tooltip));
            Rect labelRect = new Rect(
                mousePos.x + EditorGUIUtility.singleLineHeight / 1.5f, 
                mousePos.y + EditorGUIUtility.singleLineHeight / 1.5f,
                rectSize.x + 1,
                rectSize.y);

            GUI.Label(labelRect, tooltip, tooltipStyle);
        }
        
        private static string FormatColorTooltip(ParentNode node, Color color)
        {
            string[] colorPrefix = new string[] { "R", "G", "B", "A" };
            
            bool[] previewChannels = node.GetPreviewChannels();
            int activeChannels = node.GetActiveChannels();
            int usedChannels = 0;
            string labelText = "";
            for (int i = 0; i < activeChannels; i++)
            {
                if (!previewChannels[i]) continue;
                
                string colorString = $"{colorPrefix[i]}: {color[i].ToString(CultureInfo.InvariantCulture)}";
                labelText += usedChannels >= 1 ? $"\n{colorString}" : colorString;
                usedChannels++;
            }
            
            return usedChannels == 0 ? null : labelText;
        }

        #endregion Tooltip Methods
    }
}
