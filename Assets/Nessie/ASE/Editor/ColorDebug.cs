using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using HarmonyLib;
using AmplifyShaderEditor;

namespace Nessie.ASE
{
    [InitializeOnLoad]
    internal class ColorDebug
    {
        #region Private Fields

        private const string m_harmonyID = "Nessie.ASE.ColorDebugPatch";

        private static bool m_useHotkey = true;

        private static Texture2D m_previewPixel;

        #endregion Private Fields

        #region Public Fields

        public static bool UseHotkey { get { return m_useHotkey; } set { m_useHotkey = value; } }

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

        #endregion Public Fields

        #region Injection Methods

        static ColorDebug()
        {
            AssemblyReloadEvents.afterAssemblyReload += PostAssemblyReload;
        }

        static void PostAssemblyReload()
        {
            Harmony harmony = new Harmony(m_harmonyID);

            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(ParentGraph), nameof(ParentGraph.Draw))]
        static class PatchDrawPreview
        {
            static void Postfix(DrawInfo drawInfo, List<ParentNode> ___m_nodes)
            {
                if (drawInfo.CurrentEventType == EventType.Repaint && (!UseHotkey || Event.current.control))
                {
                    ParentNode node = GetActiveNode(drawInfo, ___m_nodes);
                    if (node)
                        ShowColorTooltip(drawInfo, node);
                }
            }
        }

        [HarmonyPatch(typeof(AmplifyShaderEditorWindow), nameof(AmplifyShaderEditorWindow.OnDisable))]
        static class PatchDisable
        {
            static void Postfix()
            {
                if (m_previewPixel == null)
                    UnityEngine.Object.DestroyImmediate(PreviewPixel);
            }
        }

        #endregion Injection Methods

        #region ASE Methods

        static void ShowColorTooltip(DrawInfo drawInfo, ParentNode node)
        {
            bool isTextureNode = node.GetType().IsSubclassOf(typeof(TexturePropertyNode));
            bool drawPreview = isTextureNode ? ((TexturePropertyNode)node).IsValid : GetPrivateField<bool>(node, "m_drawPreview");
            Rect previewRect = GetPrivateField<Rect>(node, "m_previewRect");
            if (previewRect.Contains(drawInfo.MousePosition) && (node.ShowPreview || node.ContainerGraph.ParentWindow.GlobalPreview) && drawPreview)
            {
                Vector2 mousePos = drawInfo.MousePosition;
                Vector2 mouseRel = mousePos - new Vector2(previewRect.x, previewRect.y);

                // Push a single pixel from a RT into a Tex2D.
                RenderTexture previousRT = RenderTexture.active;
                RenderTexture.active = node.PreviewTexture;
                PreviewPixel.ReadPixels(new Rect(mouseRel.x, mouseRel.y, 1, 1), 0, 0, false);
                RenderTexture.active = previousRT;

                // Each channel is represented with a full 32-bit float.
                byte[] bytes = PreviewPixel.GetRawTextureData();
                float colorR = BitConverter.ToSingle(bytes, 0);
                float colorG = BitConverter.ToSingle(bytes, 4);
                float colorB = BitConverter.ToSingle(bytes, 8);
                float colorA = BitConverter.ToSingle(bytes, 12);

                string[] colorValues = new string[]
                {
                    $"R: {colorR.ToString()}",
                    $"G: {colorG.ToString()}",
                    $"B: {colorB.ToString()}",
                    $"A: {colorA.ToString()}"
                };

                Array previewChannels = GetPrivateField<Array>(typeof(ParentNode), node, "m_previewChannels");
                int activeChannels = GetActiveChannels(node);
                int usedChannels = 0;
                string labelText = "";
                for (int i = 0; i < activeChannels; i++)
                {
                    if ((bool)previewChannels.GetValue(i))
                        labelText += usedChannels++ >= 1 ? $"\n{colorValues[i]}" : colorValues[i];
                }
                if (usedChannels == 0) return;

                GUIStyle labelStyle = new GUIStyle(UIUtils.Textfield);
                labelStyle.padding.left += 2;
                labelStyle.padding.right += 2;
                labelStyle.padding.top += 2;
                labelStyle.padding.bottom += 2;
                Vector2 rectSize = labelStyle.CalcSize(new GUIContent(labelText));
                Rect labelRect = new Rect(mousePos.x + EditorGUIUtility.singleLineHeight / 1.5f, mousePos.y + EditorGUIUtility.singleLineHeight / 1.5f, rectSize.x + 1, rectSize.y);

                GUI.Label(labelRect, labelText, labelStyle);
            }
        }

        static ParentNode GetActiveNode(DrawInfo drawInfo, List<ParentNode> nodes)
        {
            int nodeCount = nodes.Count;
            for (int i = nodeCount - 1; i >= 0; i--)
            {
                ParentNode node = nodes[i];
                if (node.IsVisible && !node.IsMoving)
                {
                    if (node.GlobalPosition.Contains(drawInfo.MousePosition)) return node;
                }
            }

            return null;
        }

        static int GetActiveChannels(ParentNode node)
        {
            switch (node.OutputPorts[0].DataType)
            {
                case WirePortDataType.FLOAT:
                    return 1;
                case WirePortDataType.FLOAT2:
                    return 2;
                case WirePortDataType.COLOR:
                case WirePortDataType.FLOAT4:
                case WirePortDataType.SAMPLER1D:
                case WirePortDataType.SAMPLER2D:
                case WirePortDataType.SAMPLER3D:
                case WirePortDataType.SAMPLERCUBE:
                case WirePortDataType.SAMPLER2DARRAY:
                    return 4;
                default:
                    return 3;
            }
        }

        #endregion ASE Methods

        #region Reflections

        static T GetPrivateField<T>(object obj, string fieldName)
        {
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = obj.GetType();
            FieldInfo finfo = type.GetField(fieldName, bindingFlags);

            return (T)finfo.GetValue(obj);
        }

        static T GetPrivateField<T>(Type type, object obj, string fieldName)
        {
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo finfo = type.GetField(fieldName, bindingFlags);

            return (T)finfo.GetValue(obj);
        }

        #endregion Reflections
    }
}
