using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace sugi.cc.ImageProcessTool
{
    [Serializable]
    public sealed class ImageProcessNodeData
    {
        public string nodeId;
        public string displayName;
        public ImageProcessNodeKind nodeKind;
        public Shader shader;
        public string parameterId;
        public bool previewExpanded;
        public Vector2 position;
        public List<ImageProcessPortDefinition> inputPorts = new();
        public List<ImageProcessPortDefinition> outputPorts = new();
        public List<ImageProcessNodeParameter> parameters = new();
        [FormerlySerializedAs("outputRenderTexture")]
        [SerializeField, HideInInspector] private RenderTexture legacyOutputRenderTexture;

        public ImageProcessNodeData()
        {
            EnsureNodeId();
        }

        public void Initialize(ImageProcessNodeKind kind, string name)
        {
            EnsureNodeId();
            nodeKind = kind;
            displayName = string.IsNullOrWhiteSpace(name) ? kind.ToString() : name;
        }

        public void EnsureNodeId()
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                nodeId = Guid.NewGuid().ToString("N");
            }
        }

        public void SetPorts(List<ImageProcessPortDefinition> inputs, List<ImageProcessPortDefinition> outputs)
        {
            inputPorts = inputs ?? new List<ImageProcessPortDefinition>();
            outputPorts = outputs ?? new List<ImageProcessPortDefinition>();
        }

        public void SetParameters(List<ImageProcessNodeParameter> values)
        {
            parameters = values ?? new List<ImageProcessNodeParameter>();
        }
    }
}
