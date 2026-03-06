using System.Collections.Generic;
using UnityEditor;

namespace SugiCho.ImageProcessTool.Editor
{
    public static class ShaderNodePortSynchronizer
    {
        public static bool TrySync(ImageProcessNodeData node, out string error)
        {
            if (node == null)
            {
                error = "Node is null.";
                return false;
            }

            if (node.nodeKind != ImageProcessNodeKind.ShaderOperator)
            {
                error = "Node kind must be ShaderOperator.";
                return false;
            }

            if (node.shader == null)
            {
                error = "Shader is not assigned.";
                return false;
            }

            var shader = node.shader;
            var inputPorts = new List<ImageProcessPortDefinition>();
            var outputPorts = new List<ImageProcessPortDefinition>
            {
                new("out_rgba", "RGBA", ImageProcessPortType.Texture, ImageProcessPortDirection.Output, false)
            };
            var parameters = new List<ImageProcessNodeParameter>();

            var materialScope = new MaterialScope(shader);
            try
            {
                var propertyCount = ShaderUtil.GetPropertyCount(shader);
                for (var i = 0; i < propertyCount; i++)
                {
                    var flags = ShaderUtil.GetShaderPropertyFlags(shader, i);
                    if ((flags & ShaderUtil.ShaderPropertyFlags.HideInInspector) != 0)
                    {
                        continue;
                    }

                    var propertyName = ShaderUtil.GetPropertyName(shader, i);
                    var displayName = ShaderUtil.GetPropertyDescription(shader, i);
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = propertyName;
                    }

                    var propertyType = ShaderUtil.GetPropertyType(shader, i);
                    switch (propertyType)
                    {
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            inputPorts.Add(new ImageProcessPortDefinition(propertyName, displayName, ImageProcessPortType.Texture, ImageProcessPortDirection.Input, false));
                            break;

                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            inputPorts.Add(new ImageProcessPortDefinition(propertyName, displayName, ImageProcessPortType.Float, ImageProcessPortDirection.Input, true));
                            parameters.Add(new ImageProcessNodeParameter(propertyName, ImageProcessPortType.Float)
                            {
                                floatValue = materialScope.Instance.GetFloat(propertyName)
                            });
                            break;

                        case ShaderUtil.ShaderPropertyType.Vector:
                            inputPorts.Add(new ImageProcessPortDefinition(propertyName, displayName, ImageProcessPortType.Vector4, ImageProcessPortDirection.Input, true));
                            parameters.Add(new ImageProcessNodeParameter(propertyName, ImageProcessPortType.Vector4)
                            {
                                vectorValue = materialScope.Instance.GetVector(propertyName)
                            });
                            break;

                        case ShaderUtil.ShaderPropertyType.Color:
                            inputPorts.Add(new ImageProcessPortDefinition(propertyName, displayName, ImageProcessPortType.Color, ImageProcessPortDirection.Input, true));
                            parameters.Add(new ImageProcessNodeParameter(propertyName, ImageProcessPortType.Color)
                            {
                                colorValue = materialScope.Instance.GetColor(propertyName)
                            });
                            break;
                    }
                }
            }
            finally
            {
                materialScope.Dispose();
            }

            node.SetPorts(inputPorts, outputPorts);
            node.SetParameters(parameters);

            error = string.Empty;
            return true;
        }

        private sealed class MaterialScope : System.IDisposable
        {
            public readonly UnityEngine.Material Instance;

            public MaterialScope(UnityEngine.Shader shader)
            {
                Instance = new UnityEngine.Material(shader);
            }

            public void Dispose()
            {
                if (Instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(Instance);
                }
            }
        }
    }
}
