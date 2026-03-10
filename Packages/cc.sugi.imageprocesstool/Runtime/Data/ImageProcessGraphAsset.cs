using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    [CreateAssetMenu(menuName = "sugi.cc/Image Process Graph", fileName = "ImageProcessGraph")]
    public sealed class ImageProcessGraphAsset : ScriptableObject
    {
        [SerializeField] private string graphVersion = "0.1.0";
        [SerializeField] private List<ImageProcessNodeData> nodes = new();
        [SerializeField] private List<ImageProcessEdgeData> edges = new();
        [SerializeField] private List<ImageProcessGraphParameter> parameters = new();

        public string GraphVersion => graphVersion;
        public IReadOnlyList<ImageProcessNodeData> Nodes => nodes;
        public IReadOnlyList<ImageProcessEdgeData> Edges => edges;
        public IReadOnlyList<ImageProcessGraphParameter> Parameters => parameters;

        public ImageProcessNodeData AddNode(ImageProcessNodeKind kind, string displayName)
        {
            var node = new ImageProcessNodeData();
            node.Initialize(kind, displayName);
            node.displayName = MakeUniqueNodeDisplayName(kind, node.displayName);

            if (kind == ImageProcessNodeKind.Output)
            {
                node.SetPorts(
                    new List<ImageProcessPortDefinition>
                    {
                        new("in_rgba", "RGBA", ImageProcessPortType.Texture, ImageProcessPortDirection.Input, false)
                    },
                    new List<ImageProcessPortDefinition>());
            }
            else if (kind == ImageProcessNodeKind.Parameter)
            {
                node.parameterId = parameters.FirstOrDefault()?.parameterId;
                SyncParameterNode(node);
            }
            else if (kind == ImageProcessNodeKind.BlurOperator || kind == ImageProcessNodeKind.IterativeFilterOperator)
            {
                node.SetPorts(
                    new List<ImageProcessPortDefinition>
                    {
                        new("in_rgba", "RGBA", ImageProcessPortType.Texture, ImageProcessPortDirection.Input, false)
                    },
                    new List<ImageProcessPortDefinition>
                    {
                        new("out_rgba", "RGBA", ImageProcessPortType.Texture, ImageProcessPortDirection.Output, false)
                    });
            }

            nodes.Add(node);
            return node;
        }

        public void RemoveNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            nodes.RemoveAll(n => n.nodeId == nodeId);
            edges.RemoveAll(e => e.inputNodeId == nodeId || e.outputNodeId == nodeId);
        }

        public bool AddEdge(string outputNodeId, string outputPortId, string inputNodeId, string inputPortId)
        {
            if (string.IsNullOrWhiteSpace(outputNodeId) || string.IsNullOrWhiteSpace(inputNodeId))
            {
                return false;
            }

            var alreadyExists = edges.Any(e =>
                e.outputNodeId == outputNodeId &&
                e.outputPortId == outputPortId &&
                e.inputNodeId == inputNodeId &&
                e.inputPortId == inputPortId);

            if (alreadyExists)
            {
                return false;
            }

            edges.Add(new ImageProcessEdgeData(outputNodeId, outputPortId, inputNodeId, inputPortId));
            return true;
        }

        public void RemoveEdgeAt(int index)
        {
            if (index < 0 || index >= edges.Count)
            {
                return;
            }

            edges.RemoveAt(index);
        }

        public bool RemoveEdge(string outputNodeId, string outputPortId, string inputNodeId, string inputPortId)
        {
            var removedCount = edges.RemoveAll(e =>
                e.outputNodeId == outputNodeId &&
                e.outputPortId == outputPortId &&
                e.inputNodeId == inputNodeId &&
                e.inputPortId == inputPortId);

            return removedCount > 0;
        }

        public int RemoveInvalidEdges()
        {
            var nodeMap = nodes.ToDictionary(n => n.nodeId, n => n);
            return edges.RemoveAll(edge =>
            {
                if (!nodeMap.TryGetValue(edge.outputNodeId, out var outputNode))
                {
                    return true;
                }

                if (!nodeMap.TryGetValue(edge.inputNodeId, out var inputNode))
                {
                    return true;
                }

                var outputPort = outputNode.outputPorts.FirstOrDefault(p => p.portId == edge.outputPortId);
                var inputPort = inputNode.inputPorts.FirstOrDefault(p => p.portId == edge.inputPortId);
                if (outputPort == null || inputPort == null)
                {
                    return true;
                }

                return outputPort.portType != inputPort.portType;
            });
        }

        public ImageProcessNodeData FindNode(string nodeId)
        {
            return nodes.FirstOrDefault(n => n.nodeId == nodeId);
        }

        public void EnsureNodeIds()
        {
            foreach (var node in nodes)
            {
                node?.EnsureNodeId();
            }
        }

        public ImageProcessGraphParameter AddParameter(string parameterName, ImageProcessPortType parameterType)
        {
            var parameter = new ImageProcessGraphParameter(Guid.NewGuid().ToString("N"), MakeUniqueParameterName(parameterName), parameterType);
            parameters.Add(parameter);
            SyncParameterNodes();
            return parameter;
        }

        public void RemoveParameter(string parameterId)
        {
            if (string.IsNullOrWhiteSpace(parameterId))
            {
                return;
            }

            parameters.RemoveAll(x => x.parameterId == parameterId);
            foreach (var node in nodes)
            {
                if (node == null || node.nodeKind != ImageProcessNodeKind.Parameter || node.parameterId != parameterId)
                {
                    continue;
                }

                node.parameterId = string.Empty;
                node.displayName = "Parameter";
                node.SetPorts(new List<ImageProcessPortDefinition>(), new List<ImageProcessPortDefinition>());
            }

            RemoveInvalidEdges();
        }

        public int RemoveUnusedParameters()
        {
            var removedCount = parameters.RemoveAll(parameter => parameter != null && !HasParameterNodeReference(parameter));
            if (removedCount > 0)
            {
                SyncParameterNodes();
                RemoveInvalidEdges();
            }

            return removedCount;
        }

        public ImageProcessGraphParameter FindParameter(string parameterId)
        {
            return parameters.FirstOrDefault(x => x.parameterId == parameterId);
        }

        public ImageProcessGraphParameter FindParameterByName(string parameterName)
        {
            return parameters.FirstOrDefault(x => x.parameterName == parameterName);
        }

        public string MakeUniqueParameterName(string parameterName, string excludeParameterId = null)
        {
            var baseName = string.IsNullOrWhiteSpace(parameterName) ? "Parameter" : parameterName.Trim();
            var usedNames = new HashSet<string>(
                parameters
                    .Where(x => x != null && x.parameterId != excludeParameterId && !string.IsNullOrWhiteSpace(x.parameterName))
                    .Select(x => x.parameterName));

            if (!usedNames.Contains(baseName))
            {
                return baseName;
            }

            var suffix = 2;
            while (true)
            {
                var candidate = $"{baseName} {suffix}";
                if (!usedNames.Contains(candidate))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        public bool HasDuplicateParameterNames(out string error)
        {
            var duplicate = parameters
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.parameterName))
                .GroupBy(x => x.parameterName)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                error = $"Duplicate parameter name detected: {duplicate.Key}";
                return true;
            }

            error = string.Empty;
            return false;
        }

        public void SyncParameterNodes()
        {
            foreach (var node in nodes)
            {
                if (node == null || node.nodeKind != ImageProcessNodeKind.Parameter)
                {
                    continue;
                }

                SyncParameterNode(node);
            }
        }

        public void SyncParameterNode(ImageProcessNodeData node)
        {
            if (node == null || node.nodeKind != ImageProcessNodeKind.Parameter)
            {
                return;
            }

            var parameter = FindParameter(node.parameterId);
            if (parameter == null)
            {
                node.displayName = "Parameter";
                node.SetPorts(new List<ImageProcessPortDefinition>(), new List<ImageProcessPortDefinition>());
                return;
            }

            node.displayName = parameter.parameterName;
            var outputPortId = parameter.parameterType == ImageProcessPortType.Texture ? "out_rgba" : "out_value";
            var displayName = parameter.parameterType == ImageProcessPortType.Texture ? "RGBA" : "Value";
            node.SetPorts(
                new List<ImageProcessPortDefinition>(),
                new List<ImageProcessPortDefinition>
                {
                    new(outputPortId, displayName, parameter.parameterType, ImageProcessPortDirection.Output, false)
                });
        }

        public string MakeUniqueNodeDisplayName(ImageProcessNodeKind kind, string displayName, string excludeNodeId = null)
        {
            var baseName = string.IsNullOrWhiteSpace(displayName) ? kind.ToString() : displayName.Trim();
            if (!RequiresUniqueDisplayName(kind))
            {
                return baseName;
            }

            var usedNames = new HashSet<string>(
                nodes
                    .Where(n => n != null && n.nodeKind == kind && n.nodeId != excludeNodeId)
                    .Select(n => n.displayName)
                    .Where(n => !string.IsNullOrWhiteSpace(n)));

            if (!usedNames.Contains(baseName))
            {
                return baseName;
            }

            var suffix = 2;
            while (true)
            {
                var candidate = $"{baseName} {suffix}";
                if (!usedNames.Contains(candidate))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        public bool HasDuplicateUniqueNodeNames(out string error)
        {
            foreach (var kind in new[] { ImageProcessNodeKind.Output })
            {
                var duplicate = nodes
                    .Where(n => n != null && n.nodeKind == kind && !string.IsNullOrWhiteSpace(n.displayName))
                    .GroupBy(n => n.displayName)
                    .FirstOrDefault(g => g.Count() > 1);

                if (duplicate != null)
                {
                    error = $"Duplicate {kind} node name detected: {duplicate.Key}";
                    return true;
                }
            }

            error = string.Empty;
            return false;
        }

        private static bool RequiresUniqueDisplayName(ImageProcessNodeKind kind)
        {
            return kind == ImageProcessNodeKind.Output;
        }

        private bool HasParameterNodeReference(ImageProcessGraphParameter parameter)
        {
            if (parameter == null)
            {
                return false;
            }

            return nodes.Any(node =>
                node != null &&
                node.nodeKind == ImageProcessNodeKind.Parameter &&
                (node.parameterId == parameter.parameterId ||
                 (!string.IsNullOrWhiteSpace(node.displayName) && node.displayName == parameter.parameterName)));
        }
    }
}
