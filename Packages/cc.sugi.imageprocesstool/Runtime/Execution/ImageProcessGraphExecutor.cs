using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    public static class ImageProcessGraphExecutor
    {
        public static bool TryExecute(ImageProcessGraphAsset graph, out ImageProcessExecutionResult result, out string error)
        {
            result = null;

            if (!ImageProcessGraphValidator.TryValidateForExecution(graph, out error))
            {
                return false;
            }

            if (!ImageProcessGraphTopology.TryBuildExecutionOrder(graph, out var orderedNodes, out error))
            {
                return false;
            }

            var outputs = new Dictionary<string, RenderTexture>();
            var outputNodeIds = new List<string>();
            var incomingEdges = graph.Edges.GroupBy(e => e.inputNodeId).ToDictionary(g => g.Key, g => g.ToList());

            try
            {
                foreach (var node in orderedNodes)
                {
                    if (!TryExecuteSingleNode(node, incomingEdges, outputs, out var nodeOutput, out error))
                    {
                        ReleaseAll(outputs);
                        return false;
                    }

                    outputs[node.nodeId] = nodeOutput;
                    if (node.nodeKind == ImageProcessNodeKind.Output)
                    {
                        outputNodeIds.Add(node.nodeId);
                    }
                }
            }
            catch (Exception ex)
            {
                ReleaseAll(outputs);
                error = $"Execution failed: {ex.Message}";
                return false;
            }

            result = new ImageProcessExecutionResult(outputs, outputNodeIds);
            error = string.Empty;
            return true;
        }

        public static bool TryExecuteToRenderTexture(ImageProcessGraphAsset graph, RenderTexture destination, out string error)
        {
            error = string.Empty;
            if (destination == null)
            {
                error = "Destination RenderTexture is null.";
                return false;
            }

            if (!TryExecute(graph, out var result, out error))
            {
                return false;
            }

            try
            {
                if (!result.TryGetFirstOutput(out _, out var output) || output == null)
                {
                    error = "No output node result found.";
                    return false;
                }

                if (!destination.IsCreated())
                {
                    destination.Create();
                }

                Graphics.Blit(output, destination);
                return true;
            }
            finally
            {
                result.Dispose();
            }
        }

        private static bool TryExecuteSingleNode(
            ImageProcessNodeData node,
            Dictionary<string, List<ImageProcessEdgeData>> incomingEdges,
            Dictionary<string, RenderTexture> outputs,
            out RenderTexture output,
            out string error)
        {
            output = null;
            switch (node.nodeKind)
            {
                case ImageProcessNodeKind.Source:
                    return TryExecuteSourceNode(node, out output, out error);

                case ImageProcessNodeKind.ShaderOperator:
                    return TryExecuteShaderNode(node, incomingEdges, outputs, out output, out error);

                case ImageProcessNodeKind.Output:
                    return TryExecuteOutputNode(node, incomingEdges, outputs, out output, out error);

                default:
                    error = $"Unsupported node kind: {node.nodeKind}";
                    return false;
            }
        }

        private static bool TryExecuteSourceNode(ImageProcessNodeData node, out RenderTexture output, out string error)
        {
            output = null;
            if (node.sourceTexture == null)
            {
                error = $"Source texture is missing: {node.displayName}";
                return false;
            }

            output = CreateOutputTexture(node.sourceTexture.width, node.sourceTexture.height);
            Graphics.Blit(node.sourceTexture, output);
            error = string.Empty;
            return true;
        }

        private static bool TryExecuteShaderNode(
            ImageProcessNodeData node,
            Dictionary<string, List<ImageProcessEdgeData>> incomingEdges,
            Dictionary<string, RenderTexture> outputs,
            out RenderTexture output,
            out string error)
        {
            output = null;
            if (node.shader == null)
            {
                error = $"Shader is missing: {node.displayName}";
                return false;
            }

            incomingEdges.TryGetValue(node.nodeId, out var edges);
            edges ??= new List<ImageProcessEdgeData>();

            var material = new Material(node.shader);
            try
            {
                ApplyParameters(node, material);

                RenderTexture firstInputTexture = null;
                foreach (var inputPort in node.inputPorts)
                {
                    if (inputPort.portType != ImageProcessPortType.Texture)
                    {
                        continue;
                    }

                    var edge = edges.FirstOrDefault(e => e.inputPortId == inputPort.portId);
                    if (edge == null)
                    {
                        if (inputPort.optional)
                        {
                            continue;
                        }

                        error = $"Missing texture input: {node.displayName}.{inputPort.displayName}";
                        return false;
                    }

                    if (!outputs.TryGetValue(edge.outputNodeId, out var inputTexture) || inputTexture == null)
                    {
                        error = $"Missing upstream output: {edge.outputNodeId}";
                        return false;
                    }

                    material.SetTexture(inputPort.portId, inputTexture);
                    firstInputTexture ??= inputTexture;
                }

                var width = firstInputTexture != null ? firstInputTexture.width : 512;
                var height = firstInputTexture != null ? firstInputTexture.height : 512;
                output = CreateOutputTexture(width, height);
                Graphics.Blit(Texture2D.blackTexture, output, material);
            }
            finally
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(material);
#else
                UnityEngine.Object.Destroy(material);
#endif
            }

            error = string.Empty;
            return true;
        }

        private static bool TryExecuteOutputNode(
            ImageProcessNodeData node,
            Dictionary<string, List<ImageProcessEdgeData>> incomingEdges,
            Dictionary<string, RenderTexture> outputs,
            out RenderTexture output,
            out string error)
        {
            output = null;
            incomingEdges.TryGetValue(node.nodeId, out var edges);
            var inputEdge = edges?.FirstOrDefault(e => e.inputPortId == "in_rgba");
            if (inputEdge == null)
            {
                error = $"Output input is missing: {node.displayName}.in_rgba";
                return false;
            }

            if (!outputs.TryGetValue(inputEdge.outputNodeId, out var source) || source == null)
            {
                error = $"Output source is missing: {inputEdge.outputNodeId}";
                return false;
            }

            if (node.outputRenderTexture != null)
            {
                if (!node.outputRenderTexture.IsCreated())
                {
                    node.outputRenderTexture.Create();
                }

                Graphics.Blit(source, node.outputRenderTexture);
            }

            output = CreateOutputTexture(source.width, source.height);
            Graphics.Blit(source, output);
            error = string.Empty;
            return true;
        }

        private static void ApplyParameters(ImageProcessNodeData node, Material material)
        {
            foreach (var parameter in node.parameters)
            {
                switch (parameter.parameterType)
                {
                    case ImageProcessPortType.Float:
                        material.SetFloat(parameter.parameterName, parameter.floatValue);
                        break;

                    case ImageProcessPortType.Vector4:
                        material.SetVector(parameter.parameterName, parameter.vectorValue);
                        break;

                    case ImageProcessPortType.Color:
                        material.SetColor(parameter.parameterName, parameter.colorValue);
                        break;
                }
            }
        }

        private static RenderTexture CreateOutputTexture(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            var rt = new RenderTexture(descriptor)
            {
                name = $"ImageProcessRT_{width}x{height}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        private static void ReleaseAll(Dictionary<string, RenderTexture> outputs)
        {
            var result = new ImageProcessExecutionResult(outputs, new List<string>());
            result.Dispose();
        }
    }
}
