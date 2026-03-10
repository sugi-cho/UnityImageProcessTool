using System.Collections.Generic;
using System.Linq;

namespace sugi.cc.ImageProcessTool
{
    public static class ImageProcessGraphValidator
    {
        public static bool TryValidateForExecution(ImageProcessGraphAsset graph, out string error)
        {
            if (graph == null)
            {
                error = "Graph asset is null.";
                return false;
            }

            graph.EnsureNodeIds();

            var nodes = graph.Nodes.ToList();
            var edges = graph.Edges.ToList();

            if (nodes.Count == 0)
            {
                error = "Graph has no node.";
                return false;
            }

            if (nodes.GroupBy(n => n.nodeId).Any(g => g.Count() > 1))
            {
                error = "Duplicate node id detected.";
                return false;
            }

            if (graph.HasDuplicateUniqueNodeNames(out error))
            {
                return false;
            }

            if (graph.HasDuplicateParameterNames(out error))
            {
                return false;
            }

            var byId = nodes.ToDictionary(n => n.nodeId, n => n);

            var inputConnectionCount = new Dictionary<string, int>();
            foreach (var edge in edges)
            {
                if (!byId.TryGetValue(edge.outputNodeId, out var outputNode))
                {
                    error = $"Missing output node: {edge.outputNodeId}";
                    return false;
                }

                if (!byId.TryGetValue(edge.inputNodeId, out var inputNode))
                {
                    error = $"Missing input node: {edge.inputNodeId}";
                    return false;
                }

                var outputPort = outputNode.outputPorts.FirstOrDefault(p => p.portId == edge.outputPortId);
                if (outputPort == null)
                {
                    error = $"Output port not found: {outputNode.displayName}.{edge.outputPortId}";
                    return false;
                }

                var inputPort = inputNode.inputPorts.FirstOrDefault(p => p.portId == edge.inputPortId);
                if (inputPort == null)
                {
                    error = $"Input port not found: {inputNode.displayName}.{edge.inputPortId}";
                    return false;
                }

                if (outputPort.direction != ImageProcessPortDirection.Output || inputPort.direction != ImageProcessPortDirection.Input)
                {
                    error = "Edge port direction is invalid.";
                    return false;
                }

                if (outputPort.portType != inputPort.portType)
                {
                    error = $"Port type mismatch: {outputPort.portType} -> {inputPort.portType}";
                    return false;
                }

                var key = $"{edge.inputNodeId}:{edge.inputPortId}";
                inputConnectionCount.TryGetValue(key, out var count);
                inputConnectionCount[key] = count + 1;
                if (inputConnectionCount[key] > 1)
                {
                    error = $"Multiple edges connected to one input port: {inputNode.displayName}.{edge.inputPortId}";
                    return false;
                }
            }

            foreach (var node in nodes)
            {
                switch (node.nodeKind)
                {
                    case ImageProcessNodeKind.ShaderOperator:
                        if (node.shader == null)
                        {
                            error = $"Shader node has no shader: {node.displayName}";
                            return false;
                        }
                        break;

                    case ImageProcessNodeKind.Output:
                        if (!node.inputPorts.Any(p => p.portId == "in_rgba"))
                        {
                            error = $"Output node has no in_rgba port: {node.displayName}";
                            return false;
                        }
                        break;

                    case ImageProcessNodeKind.Parameter:
                        var parameter = graph.FindParameter(node.parameterId);
                        if (parameter == null)
                        {
                            error = $"Parameter node has invalid parameter reference: {node.displayName}";
                            return false;
                        }

                        if (node.outputPorts.Count != 1 || node.outputPorts[0].portType != parameter.parameterType)
                        {
                            error = $"Parameter node port mismatch: {node.displayName}";
                            return false;
                        }
                        break;
                }

                foreach (var inputPort in node.inputPorts)
                {
                    if (inputPort.optional)
                    {
                        continue;
                    }

                    var key = $"{node.nodeId}:{inputPort.portId}";
                    if (!inputConnectionCount.ContainsKey(key))
                    {
                        if (node.nodeKind == ImageProcessNodeKind.ShaderOperator && inputPort.portType != ImageProcessPortType.Texture)
                        {
                            continue;
                        }

                        error = $"Required input is not connected: {node.displayName}.{inputPort.displayName}";
                        return false;
                    }
                }
            }

            if (!ImageProcessGraphTopology.TryBuildExecutionOrder(graph, out _, out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
