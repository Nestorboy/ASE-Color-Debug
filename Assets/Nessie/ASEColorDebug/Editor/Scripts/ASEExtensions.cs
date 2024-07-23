using System.Collections.Generic;
using AmplifyShaderEditor;
using UnityEngine;

namespace Nessie.ASE.Editor
{
    public static class ASEExtensions
    {
        public static ParentNode GetActiveNode(Vector2 mousePos, List<ParentNode> nodes)
        {
            int nodeCount = nodes.Count;
            for (int i = nodeCount - 1; i >= 0; i--)
            {
                ParentNode node = nodes[i];
                if (!node.IsVisible || node.IsMoving) continue;
                if (node.GlobalPosition.Contains(mousePos)) return node;
            }

            return null;
        }

        public static Rect GetPreviewRect(this ParentNode node)
        {
            return ReflectionUtils.GetPrivateField<Rect>(node, "m_previewRect");
        }

        public static bool[] GetPreviewChannels(this ParentNode node)
        {
            return ReflectionUtils.GetPrivateField<bool[]>(typeof(ParentNode), node, "m_previewChannels");
        }
        
        public static int GetActiveChannels(this ParentNode node)
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

        public static bool IsPreviewVisible(this ParentNode node)
        {
            if (!node.ShowPreview && !node.ContainerGraph.ParentWindow.GlobalPreview)
            {
                return false;
            }
            
            bool isTextureNode = node.GetType().IsSubclassOf(typeof(TexturePropertyNode));
            return isTextureNode ? ((TexturePropertyNode)node).IsValid : ReflectionUtils.GetPrivateField<bool>(node, "m_drawPreview");
        }
    }
}
