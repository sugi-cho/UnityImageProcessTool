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
            return TryExecute(graph, null, out result, out error);
        }

        public static bool TryExecute(
            ImageProcessGraphAsset graph,
            IReadOnlyDictionary<string, ImageProcessGraphParameter> parameterOverrides,
            out ImageProcessExecutionResult result,
            out string error)
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

            var outputs = new Dictionary<string, ImageProcessValue>();
            var outputNodeIds = new List<string>();
            if (!ImageProcessGraphTopology.TryCollectExecutableSubgraph(graph, out _, out var executableEdges, out error))
            {
                return false;
            }

            var incomingEdges = executableEdges.GroupBy(e => e.inputNodeId).ToDictionary(g => g.Key, g => g.ToList());

            try
            {
                foreach (var node in orderedNodes)
                {
                    if (!TryExecuteSingleNode(graph, node, parameterOverrides, incomingEdges, outputs, out var nodeOutput, out error))
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

        public static bool TryExecuteToRenderTexture(ImageProcessGraphAsset graph, string outputNodeId, RenderTexture destination, out string error)
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
                if (!result.TryGetNodeOutputTexture(outputNodeId, out var output) || output == null)
                {
                    error = $"Output node result not found: {outputNodeId}";
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
            ImageProcessGraphAsset graph,
            ImageProcessNodeData node,
            IReadOnlyDictionary<string, ImageProcessGraphParameter> parameterOverrides,
            Dictionary<string, List<ImageProcessEdgeData>> incomingEdges,
            Dictionary<string, ImageProcessValue> outputs,
            out ImageProcessValue output,
            out string error)
        {
            output = null;
            switch (node.nodeKind)
            {
                case ImageProcessNodeKind.Parameter:
                    return TryExecuteParameterNode(graph, node, parameterOverrides, out output, out error);

                case ImageProcessNodeKind.ShaderOperator:
                    return TryExecuteShaderNode(node, incomingEdges, outputs, out output, out error);

                case ImageProcessNodeKind.BlurOperator:
                    return TryExecuteBlurNode(node, incomingEdges, outputs, out output, out error);

                case ImageProcessNodeKind.IterativeFilterOperator:
                    return TryExecuteIterativeFilterNode(node, incomingEdges, outputs, out output, out error);

                case ImageProcessNodeKind.Output:
                    return TryExecuteOutputNode(node, incomingEdges, outputs, out output, out error);

                default:
                    error = $"Unsupported node kind: {node.nodeKind}";
                    return false;
            }
        }

        private static bool TryExecuteParameterNode(
            ImageProcessGraphAsset graph,
            ImageProcessNodeData node,
            IReadOnlyDictionary<string, ImageProcessGraphParameter> parameterOverrides,
            out ImageProcessValue output,
            out string error)
        {
            output = null;
            var parameter = ResolveGraphParameter(graph, node.parameterId, parameterOverrides);
            if (parameter == null)
            {
                error = $"Parameter not found: {node.displayName}";
                return false;
            }

            switch (parameter.parameterType)
            {
                case ImageProcessPortType.Texture:
                    if (parameter.textureValue == null)
                    {
                        error = $"Texture parameter is missing: {parameter.parameterName}";
                        return false;
                    }

                    if (parameter.textureValue is RenderTexture renderTexture && !renderTexture.IsCreated())
                    {
                        renderTexture.Create();
                    }

                    if (parameter.textureValue.width <= 0 || parameter.textureValue.height <= 0)
                    {
                        error = $"Texture parameter size is invalid: {parameter.parameterName}";
                        return false;
                    }

                    var texture = CreateOutputTexture(parameter.textureValue.width, parameter.textureValue.height);
                    Graphics.Blit(parameter.textureValue, texture);
                    output = ImageProcessValue.FromTexture(texture);
                    break;

                case ImageProcessPortType.Float:
                    output = ImageProcessValue.FromFloat(parameter.floatValue);
                    break;

                case ImageProcessPortType.Vector4:
                    output = ImageProcessValue.FromVector(parameter.vectorValue);
                    break;

                case ImageProcessPortType.Color:
                    output = ImageProcessValue.FromColor(parameter.colorValue);
                    break;

                default:
                    error = $"Unsupported parameter type: {parameter.parameterType}";
                    return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryExecuteShaderNode(
            ImageProcessNodeData node,
            Dictionary<string, List<ImageProcessEdgeData>> incomingEdges,
            Dictionary<string, ImageProcessValue> outputs,
            out ImageProcessValue output,
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
                    var edge = edges.FirstOrDefault(e => e.inputPortId == inputPort.portId);
                    if (edge == null)
                    {
                        if (inputPort.optional)
                        {
                            continue;
                        }

                        error = $"Missing input: {node.displayName}.{inputPort.displayName}";
                        return false;
                    }

                    if (!outputs.TryGetValue(edge.outputNodeId, out var inputValue) || inputValue == null)
                    {
                        error = $"Missing upstream output: {edge.outputNodeId}";
                        return false;
                    }

                    switch (inputPort.portType)
                    {
                        case ImageProcessPortType.Texture:
                            if (!inputValue.TryGetTexture(out var inputTexture) || inputTexture == null)
                            {
                                error = $"Upstream output is not texture: {edge.outputNodeId}";
                                return false;
                            }

                            material.SetTexture(inputPort.portId, inputTexture);
                            firstInputTexture ??= inputTexture;
                            break;

                        case ImageProcessPortType.Float:
                            if (inputValue.ValueType != ImageProcessPortType.Float)
                            {
                                error = $"Upstream output type mismatch: {edge.outputNodeId}";
                                return false;
                            }

                            material.SetFloat(inputPort.portId, inputValue.FloatValue);
                            break;

                        case ImageProcessPortType.Vector4:
                            if (inputValue.ValueType != ImageProcessPortType.Vector4)
                            {
                                error = $"Upstream output type mismatch: {edge.outputNodeId}";
                                return false;
                            }

                            material.SetVector(inputPort.portId, inputValue.VectorValue);
                            break;

                        case ImageProcessPortType.Color:
                            if (inputValue.ValueType != ImageProcessPortType.Color)
                            {
                                error = $"Upstream output type mismatch: {edge.outputNodeId}";
                                return false;
                            }

                            material.SetColor(inputPort.portId, inputValue.ColorValue);
                            break;
                    }
                }

                var width = firstInputTexture != null ? firstInputTexture.width : 512;
                var height = firstInputTexture != null ? firstInputTexture.height : 512;
                var texture = CreateOutputTexture(width, height);
                Texture blitSource = firstInputTexture != null ? firstInputTexture : Texture2D.blackTexture;
                if (firstInputTexture != null && material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", firstInputTexture);
                }

                Graphics.Blit(blitSource, texture, material);
                output = ImageProcessValue.FromTexture(texture);
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
            Dictionary<string, ImageProcessValue> outputs,
            out ImageProcessValue output,
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

            if (!outputs.TryGetValue(inputEdge.outputNodeId, out var sourceValue) ||
                sourceValue == null ||
                !sourceValue.TryGetTexture(out var source) ||
                source == null)
            {
                error = $"Output source is missing: {inputEdge.outputNodeId}";
                return false;
            }

            var texture = CreateOutputTexture(source.width, source.height);
            Graphics.Blit(source, texture);
            output = ImageProcessValue.FromTexture(texture);
            error = string.Empty;
            return true;
        }

        private static bool TryExecuteBlurNode(
            ImageProcessNodeData node,
            Dictionary<string, List<ImageProcessEdgeData>> incomingEdges,
            Dictionary<string, ImageProcessValue> outputs,
            out ImageProcessValue output,
            out string error)
        {
            output = null;
            if (!TryGetTextureInput(node, incomingEdges, outputs, "in_rgba", out var source, out error))
            {
                return false;
            }

            var settings = BuildIterativeFilterSettings(node, out error);
            if (settings == null)
            {
                output = null;
                return false;
            }

            if (!ImageProcessIterativeFilterExecutor.TryExecute(source, settings, out var filtered, out error))
            {
                output = null;
                return false;
            }

            output = ImageProcessValue.FromTexture(filtered);
            error = string.Empty;
            return true;
        }

        private static bool TryExecuteIterativeFilterNode(
            ImageProcessNodeData node,
            Dictionary<string, List<ImageProcessEdgeData>> incomingEdges,
            Dictionary<string, ImageProcessValue> outputs,
            out ImageProcessValue output,
            out string error)
        {
            output = null;
            if (!TryGetTextureInput(node, incomingEdges, outputs, "in_rgba", out var source, out error))
            {
                return false;
            }

            var settings = BuildIterativeFilterSettings(node, out error);
            if (settings == null)
            {
                return false;
            }

            if (!ImageProcessIterativeFilterExecutor.TryExecute(source, settings, out var filtered, out error))
            {
                return false;
            }

            output = ImageProcessValue.FromTexture(filtered);
            error = string.Empty;
            return true;
        }

        private static ImageProcessIterativeFilterSettings BuildIterativeFilterSettings(ImageProcessNodeData node, out string error)
        {
            error = string.Empty;

            if (node == null)
            {
                error = "Node is null.";
                return null;
            }

            if (node.nodeKind == ImageProcessNodeKind.BlurOperator ||
                node.iterativeFilterKind == ImageProcessIterativeFilterKind.Blur)
            {
                return ImageProcessIterativeFilterSettings.CreateBlur(
                    node.blurMode,
                    node.blurIterations,
                    node.blurRadius,
                    node.blurDownsample);
            }

            if (node.iterativeFilterKind == ImageProcessIterativeFilterKind.Dilate)
            {
                return ImageProcessIterativeFilterSettings.CreateDilate(
                    node.blurIterations,
                    node.blurRadius,
                    node.blurDownsample);
            }

            if (node.iterativeFilterKind == ImageProcessIterativeFilterKind.Erode)
            {
                return ImageProcessIterativeFilterSettings.CreateErode(
                    node.blurIterations,
                    node.blurRadius,
                    node.blurDownsample);
            }

            error = $"Unsupported iterative filter kind: {node.iterativeFilterKind}";
            return null;
        }

        private static ImageProcessGraphParameter ResolveGraphParameter(
            ImageProcessGraphAsset graph,
            string parameterId,
            IReadOnlyDictionary<string, ImageProcessGraphParameter> parameterOverrides)
        {
            var parameter = graph.FindParameter(parameterId);
            if (parameter == null)
            {
                return null;
            }

            if (parameterOverrides == null || !parameterOverrides.TryGetValue(parameter.parameterName, out var overrideParameter) || overrideParameter == null)
            {
                return parameter;
            }

            return overrideParameter;
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

        private static bool TryGetTextureInput(
            ImageProcessNodeData node,
            Dictionary<string, List<ImageProcessEdgeData>> incomingEdges,
            Dictionary<string, ImageProcessValue> outputs,
            string inputPortId,
            out RenderTexture texture,
            out string error)
        {
            texture = null;
            incomingEdges.TryGetValue(node.nodeId, out var edges);
            var edge = edges?.FirstOrDefault(e => e.inputPortId == inputPortId);
            if (edge == null)
            {
                error = $"Missing input: {node.displayName}.{inputPortId}";
                return false;
            }

            if (!outputs.TryGetValue(edge.outputNodeId, out var inputValue) ||
                inputValue == null ||
                !inputValue.TryGetTexture(out texture) ||
                texture == null)
            {
                error = $"Upstream output is not texture: {edge.outputNodeId}";
                return false;
            }

            error = string.Empty;
            return true;
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

        private static void ReleaseAll(Dictionary<string, ImageProcessValue> outputs)
        {
            var result = new ImageProcessExecutionResult(outputs, new List<string>());
            result.Dispose();
        }
    }
}
