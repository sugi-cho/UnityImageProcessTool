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

        public string GraphVersion => graphVersion;
        public IReadOnlyList<ImageProcessNodeData> Nodes => nodes;
        public IReadOnlyList<ImageProcessEdgeData> Edges => edges;

        public ImageProcessNodeData AddNode(ImageProcessNodeKind kind, string displayName)
        {
            var node = new ImageProcessNodeData();
            node.Initialize(kind, displayName);

            if (kind == ImageProcessNodeKind.Source)
            {
                node.SetPorts(
                    new List<ImageProcessPortDefinition>(),
                    new List<ImageProcessPortDefinition>
                    {
                        new("out_rgba", "RGBA", ImageProcessPortType.Texture, ImageProcessPortDirection.Output, false)
                    });
            }
            else if (kind == ImageProcessNodeKind.Output)
            {
                node.SetPorts(
                    new List<ImageProcessPortDefinition>
                    {
                        new("in_rgba", "RGBA", ImageProcessPortType.Texture, ImageProcessPortDirection.Input, false)
                    },
                    new List<ImageProcessPortDefinition>());
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
    }
}

