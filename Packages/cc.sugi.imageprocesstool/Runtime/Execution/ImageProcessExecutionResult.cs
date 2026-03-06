using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    public sealed class ImageProcessExecutionResult : IDisposable
    {
        private readonly Dictionary<string, RenderTexture> nodeOutputs;
        private readonly List<string> outputNodeIds;

        public IReadOnlyDictionary<string, RenderTexture> NodeOutputs => nodeOutputs;
        public IReadOnlyList<string> OutputNodeIds => outputNodeIds;

        public ImageProcessExecutionResult(Dictionary<string, RenderTexture> nodeOutputs, List<string> outputNodeIds)
        {
            this.nodeOutputs = nodeOutputs ?? new Dictionary<string, RenderTexture>();
            this.outputNodeIds = outputNodeIds ?? new List<string>();
        }

        public bool TryGetNodeOutput(string nodeId, out RenderTexture texture)
        {
            return nodeOutputs.TryGetValue(nodeId, out texture);
        }

        public bool TryGetFirstOutput(out string nodeId, out RenderTexture texture)
        {
            nodeId = outputNodeIds.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                texture = null;
                return false;
            }

            return nodeOutputs.TryGetValue(nodeId, out texture);
        }

        public void Dispose()
        {
            var released = new HashSet<RenderTexture>();
            foreach (var kv in nodeOutputs)
            {
                var rt = kv.Value;
                if (rt == null || released.Contains(rt))
                {
                    continue;
                }

                released.Add(rt);
                rt.Release();
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(rt);
#else
                UnityEngine.Object.Destroy(rt);
#endif
            }

            nodeOutputs.Clear();
            outputNodeIds.Clear();
        }
    }
}
