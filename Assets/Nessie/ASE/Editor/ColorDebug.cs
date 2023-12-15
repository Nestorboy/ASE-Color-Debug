﻿using System;
using System.Collections.Generic;
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

        #endregion Private Fields

        #region Public Properties

        public static bool UseHotkey { get; set; } = true;

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
        }

        private static void PostAssemblyReload()
        {
            Harmony harmony = new Harmony(m_harmonyID);

            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(ParentGraph), nameof(ParentGraph.Draw))]
        private static class PatchDrawPreview
        {
            private static void Postfix(DrawInfo drawInfo, List<ParentNode> ___m_nodes)
            {
                if (drawInfo.CurrentEventType != EventType.Repaint) return;

                if (UseHotkey && !Event.current.control) return;
                
                ParentNode node = ASEExtensions.GetActiveNode(drawInfo, ___m_nodes);
                if (!node) return;
                
                ShowColorTooltip(drawInfo.MousePosition, node);
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
            
            Vector2 mouseRel = mousePos - new Vector2(previewRect.x, previewRect.y);

            // Push a single pixel from a RT into a Tex2D.
            RenderTexture previousRT = RenderTexture.active;
            RenderTexture.active = node.PreviewTexture;
            PreviewPixel.ReadPixels(new Rect(mouseRel.x, mouseRel.y, 1, 1), 0, 0, false);
            RenderTexture.active = previousRT;

            // Each channel is represented with a full 32-bit float.
            byte[] bytes = PreviewPixel.GetRawTextureData();
            Color color = UnpackColor(bytes);
            DrawColorTooltip(node, mousePos, color);
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
            
            Array previewChannels = ReflectionUtils.GetPrivateField<Array>(typeof(ParentNode), node, "m_previewChannels");
            int activeChannels = node.GetActiveChannels();
            int usedChannels = 0;
            string labelText = "";
            for (int i = 0; i < activeChannels; i++)
            {
                if (!(bool)previewChannels.GetValue(i)) continue;
                
                string colorString = $"{colorPrefix[i]}: {color[i].ToString(CultureInfo.InvariantCulture)}";
                labelText += usedChannels >= 1 ? $"\n{colorString}" : colorString;
                usedChannels++;
            }
            
            return usedChannels == 0 ? null : labelText;
        }

        #endregion Tooltip Methods
    }
}
