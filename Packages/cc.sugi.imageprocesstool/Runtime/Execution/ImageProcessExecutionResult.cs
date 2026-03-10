using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    public sealed class ImageProcessExecutionResult : IDisposable
    {
        private readonly Dictionary<string, ImageProcessValue> nodeOutputs;
        private readonly List<string> outputNodeIds;

        public IReadOnlyDictionary<string, ImageProcessValue> NodeOutputs => nodeOutputs;
        public IReadOnlyList<string> OutputNodeIds => outputNodeIds;

        public ImageProcessExecutionResult(Dictionary<string, ImageProcessValue> nodeOutputs, List<string> outputNodeIds)
        {
            this.nodeOutputs = nodeOutputs ?? new Dictionary<string, ImageProcessValue>();
            this.outputNodeIds = outputNodeIds ?? new List<string>();
        }

        public bool TryGetNodeOutput(string nodeId, out ImageProcessValue value)
        {
            return nodeOutputs.TryGetValue(nodeId, out value);
        }

        public bool TryGetNodeOutputTexture(string nodeId, out RenderTexture texture)
        {
            texture = null;
            return nodeOutputs.TryGetValue(nodeId, out var value) && value != null && value.TryGetTexture(out texture);
        }

        public bool TryGetFirstOutput(out string nodeId, out RenderTexture texture)
        {
            texture = null;
            nodeId = outputNodeIds.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            return nodeOutputs.TryGetValue(nodeId, out var value) && value != null && value.TryGetTexture(out texture);
        }

        public void Dispose()
        {
            foreach (var kv in nodeOutputs)
            {
                kv.Value?.Dispose();
            }

            nodeOutputs.Clear();
            outputNodeIds.Clear();
        }
    }
}
