using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.Linq;

namespace sugi.cc.ImageProcessTool
{
    public enum ImageProcessRunnerUpdateTiming
    {
        Update,
        LateUpdate,
        Manual
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ImageProcessGraphRunner : MonoBehaviour
    {
        [System.Serializable]
        public sealed class OutputDestinationBinding
        {
            [SerializeField] private string outputNodeName;
            [SerializeField] private RenderTexture destination;
            [SerializeField] private bool previewExpanded;

            public string OutputNodeName
            {
                get => outputNodeName;
                set => outputNodeName = value;
            }

            public RenderTexture Destination
            {
                get => destination;
                set => destination = value;
            }

            public bool PreviewExpanded
            {
                get => previewExpanded;
                set => previewExpanded = value;
            }
        }

        [System.Serializable]
        public sealed class ParameterOverrideBinding
        {
            [SerializeField] private string parameterId;
            [SerializeField] private string parameterName;
            [SerializeField] private ImageProcessPortType parameterType;
            [SerializeField] private bool overrideEnabled;
            [SerializeField] private Texture textureValue;
            [SerializeField] private float floatValue;
            [SerializeField] private Vector4 vectorValue;
            [SerializeField] private Color colorValue = Color.white;

            public string ParameterId
            {
                get => parameterId;
                set => parameterId = value;
            }

            public string ParameterName
            {
                get => parameterName;
                set => parameterName = value;
            }

            public ImageProcessPortType ParameterType
            {
                get => parameterType;
                set => parameterType = value;
            }

            public bool OverrideEnabled
            {
                get => overrideEnabled;
                set => overrideEnabled = value;
            }

            public Texture TextureValue
            {
                get => textureValue;
                set => textureValue = value;
            }

            public float FloatValue
            {
                get => floatValue;
                set => floatValue = value;
            }

            public Vector4 VectorValue
            {
                get => vectorValue;
                set => vectorValue = value;
            }

            public Color ColorValue
            {
                get => colorValue;
                set => colorValue = value;
            }
        }

        [SerializeField] private ImageProcessGraphAsset graph;
        [SerializeField] private OutputDestinationBinding[] outputDestinations = System.Array.Empty<OutputDestinationBinding>();
        [SerializeField] private ParameterOverrideBinding[] parameterOverrideBindings = System.Array.Empty<ParameterOverrideBinding>();
        [SerializeField] private ImageProcessRunnerUpdateTiming updateTiming = ImageProcessRunnerUpdateTiming.Update;
        [SerializeField] private bool executeOnEnable = true;
        [FormerlySerializedAs("runInEditMode")]
        [SerializeField] private bool executeInEditMode = true;
        [SerializeField] private bool logErrors = true;

        private string lastError = string.Empty;
        private readonly Dictionary<string, ImageProcessGraphParameter> parameterOverrides = new();

        public ImageProcessGraphAsset Graph
        {
            get => graph;
            set
            {
                graph = value;
                SyncOutputDestinations();
            }
        }

        public OutputDestinationBinding[] OutputDestinations
        {
            get => outputDestinations;
        }

        public ParameterOverrideBinding[] ParameterOverrides => parameterOverrideBindings;

        public ImageProcessRunnerUpdateTiming UpdateTiming
        {
            get => updateTiming;
            set => updateTiming = value;
        }

        private void OnEnable()
        {
            SyncOutputDestinations();
            SyncParameterOverrides();

#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
#endif

            if (!executeOnEnable || !ShouldExecuteInCurrentContext())
            {
                return;
            }

            ExecuteAndLogIfNeeded();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
#endif
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (updateTiming != ImageProcessRunnerUpdateTiming.Update)
            {
                return;
            }

            ExecuteAndLogIfNeeded();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (updateTiming != ImageProcessRunnerUpdateTiming.LateUpdate)
            {
                return;
            }

            ExecuteAndLogIfNeeded();
        }

        [ContextMenu("Run Once")]
        public void RunOnce()
        {
            ExecuteAndLogIfNeeded();
        }

        public bool TryRunOnce(out string error)
        {
            SyncOutputDestinations();
            SyncParameterOverrides();
            RebuildParameterOverrideMap();

            if (graph == null)
            {
                error = "ImageProcessGraphAsset is not assigned.";
                return false;
            }

            if (!TryEvaluateGraph(out var result, out error))
            {
                return false;
            }

            try
            {
                foreach (var binding in outputDestinations)
                {
                    if (binding?.Destination == null || string.IsNullOrWhiteSpace(binding.OutputNodeName))
                    {
                        continue;
                    }

                    var outputNode = FindOutputNodeByName(binding.OutputNodeName);
                    if (outputNode == null)
                    {
                        error = $"Output node not found: {binding.OutputNodeName}";
                        return false;
                    }

                    if (!result.TryGetNodeOutputTexture(outputNode.nodeId, out var output) || output == null)
                    {
                        error = $"Output node result not found: {binding.OutputNodeName}";
                        return false;
                    }

                    if (!binding.Destination.IsCreated())
                    {
                        binding.Destination.Create();
                    }

                    Graphics.Blit(output, binding.Destination);
                }

                error = string.Empty;
                return true;
            }
            finally
            {
                result.Dispose();
            }
        }

        public bool TryEvaluateGraph(out ImageProcessExecutionResult result, out string error)
        {
            RebuildParameterOverrideMap();

            if (graph == null)
            {
                result = null;
                error = "ImageProcessGraphAsset is not assigned.";
                return false;
            }

            return ImageProcessGraphExecutor.TryExecute(graph, parameterOverrides, out result, out error);
        }

        private void OnValidate()
        {
            SyncOutputDestinations();
            SyncParameterOverrides();
            RebuildParameterOverrideMap();
        }

#if UNITY_EDITOR
        private void OnEditorUpdate()
        {
            if (Application.isPlaying || !isActiveAndEnabled || !executeInEditMode)
            {
                return;
            }

            if (updateTiming == ImageProcessRunnerUpdateTiming.Manual)
            {
                return;
            }

            ExecuteAndLogIfNeeded();
        }
#endif

        private bool ShouldExecuteInCurrentContext()
        {
            if (Application.isPlaying)
            {
                return true;
            }

#if UNITY_EDITOR
            return executeInEditMode;
#else
            return false;
#endif
        }

        private void ExecuteAndLogIfNeeded()
        {
            if (TryRunOnce(out var error))
            {
                lastError = string.Empty;
                return;
            }

            if (!logErrors || string.IsNullOrWhiteSpace(error) || error == lastError)
            {
                return;
            }

            lastError = error;
            Debug.LogWarning($"ImageProcessGraphRunner on '{name}' failed: {error}", this);
        }

        public bool SyncOutputDestinations()
        {
            var previous = outputDestinations ?? System.Array.Empty<OutputDestinationBinding>();
            if (graph == null)
            {
                var hadBindings = previous.Length > 0;
                if (hadBindings)
                {
                    outputDestinations = System.Array.Empty<OutputDestinationBinding>();
                }

                return hadBindings;
            }

            var previousMap = new System.Collections.Generic.Dictionary<string, OutputDestinationBinding>();
            foreach (var binding in previous)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.OutputNodeName))
                {
                    continue;
                }

                previousMap[binding.OutputNodeName] = binding;
            }

            var outputNodes = new System.Collections.Generic.List<ImageProcessNodeData>();
            foreach (var node in graph.Nodes)
            {
                if (node != null && node.nodeKind == ImageProcessNodeKind.Output)
                {
                    outputNodes.Add(node);
                }
            }

            var bindings = new OutputDestinationBinding[outputNodes.Count];
            for (var i = 0; i < outputNodes.Count; i++)
            {
                var node = outputNodes[i];
                previousMap.TryGetValue(node.displayName, out var previousBinding);

                bindings[i] = new OutputDestinationBinding
                {
                    OutputNodeName = string.IsNullOrWhiteSpace(node.displayName) ? "Output" : node.displayName,
                    Destination = previousBinding?.Destination,
                    PreviewExpanded = previousBinding?.PreviewExpanded ?? false
                };
            }

            var changed = previous.Length != bindings.Length;
            if (!changed)
            {
                for (var i = 0; i < bindings.Length; i++)
                {
                    var previousBinding = previous[i];
                    var currentBinding = bindings[i];
                    if (previousBinding == null ||
                        previousBinding.OutputNodeName != currentBinding.OutputNodeName ||
                        previousBinding.Destination != currentBinding.Destination ||
                        previousBinding.PreviewExpanded != currentBinding.PreviewExpanded)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                outputDestinations = bindings;
            }

            return changed;
        }

        public bool SyncParameterOverrides()
        {
            var previous = parameterOverrideBindings ?? System.Array.Empty<ParameterOverrideBinding>();
            if (graph == null)
            {
                var hadOverrides = previous.Length > 0;
                if (hadOverrides)
                {
                    parameterOverrideBindings = System.Array.Empty<ParameterOverrideBinding>();
                }

                parameterOverrides.Clear();
                return hadOverrides;
            }

            var reachableNodeIds = ImageProcessGraphTopology.CollectReachableNodeIdsFromOutputs(graph);
            var activeParameterIds = new HashSet<string>(
                graph.Nodes
                    .Where(node =>
                        node != null &&
                        node.nodeKind == ImageProcessNodeKind.Parameter &&
                        !string.IsNullOrWhiteSpace(node.parameterId) &&
                        reachableNodeIds.Contains(node.nodeId))
                    .Select(node => node.parameterId));

            var previousMap = new Dictionary<string, ParameterOverrideBinding>();
            foreach (var binding in previous)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.ParameterId))
                {
                    continue;
                }

                previousMap[binding.ParameterId] = binding;
            }

            var activeParameters = graph.Parameters
                .Where(parameter => parameter != null && activeParameterIds.Contains(parameter.parameterId))
                .ToList();

            var bindings = new ParameterOverrideBinding[activeParameters.Count];
            for (var i = 0; i < activeParameters.Count; i++)
            {
                var parameter = activeParameters[i];
                previousMap.TryGetValue(parameter.parameterId, out var previousBinding);
                bindings[i] = new ParameterOverrideBinding
                {
                    ParameterId = parameter.parameterId,
                    ParameterName = parameter.parameterName,
                    ParameterType = parameter.parameterType,
                    OverrideEnabled = previousBinding?.OverrideEnabled ?? false,
                    TextureValue = previousBinding?.TextureValue ?? parameter.textureValue,
                    FloatValue = previousBinding?.FloatValue ?? parameter.floatValue,
                    VectorValue = previousBinding?.VectorValue ?? parameter.vectorValue,
                    ColorValue = previousBinding?.ColorValue ?? parameter.colorValue
                };
            }

            var changed = previous.Length != bindings.Length;
            if (!changed)
            {
                for (var i = 0; i < bindings.Length; i++)
                {
                    var before = previous[i];
                    var after = bindings[i];
                    if (before == null ||
                        before.ParameterId != after.ParameterId ||
                        before.ParameterName != after.ParameterName ||
                        before.ParameterType != after.ParameterType ||
                        before.OverrideEnabled != after.OverrideEnabled ||
                        before.TextureValue != after.TextureValue ||
                        !Mathf.Approximately(before.FloatValue, after.FloatValue) ||
                        before.VectorValue != after.VectorValue ||
                        before.ColorValue != after.ColorValue)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                parameterOverrideBindings = bindings;
            }

            return changed;
        }

        private ImageProcessNodeData FindOutputNodeByName(string outputNodeName)
        {
            if (graph == null || string.IsNullOrWhiteSpace(outputNodeName))
            {
                return null;
            }

            foreach (var node in graph.Nodes)
            {
                if (node != null &&
                    node.nodeKind == ImageProcessNodeKind.Output &&
                    node.displayName == outputNodeName)
                {
                    return node;
                }
            }

            return null;
        }

        public bool HasParameter(string parameterName)
        {
            return graph != null &&
                   !string.IsNullOrWhiteSpace(parameterName) &&
                   graph.FindParameterByName(parameterName) != null;
        }

        public bool SetFloat(string parameterName, float value)
        {
            return SetParameterOverride(parameterName, ImageProcessPortType.Float, binding =>
            {
                binding.FloatValue = value;
            });
        }

        public bool SetVector(string parameterName, Vector4 value)
        {
            return SetParameterOverride(parameterName, ImageProcessPortType.Vector4, binding =>
            {
                binding.VectorValue = value;
            });
        }

        public bool SetVector4(string parameterName, Vector4 value)
        {
            return SetVector(parameterName, value);
        }

        public bool SetColor(string parameterName, Color value)
        {
            return SetParameterOverride(parameterName, ImageProcessPortType.Color, binding =>
            {
                binding.ColorValue = value;
            });
        }

        public bool SetTexture(string parameterName, Texture value)
        {
            return SetParameterOverride(parameterName, ImageProcessPortType.Texture, binding =>
            {
                binding.TextureValue = value;
            });
        }

        public void ClearParameterOverride(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            SyncParameterOverrides();
            foreach (var binding in parameterOverrideBindings)
            {
                if (binding == null || binding.ParameterName != parameterName)
                {
                    continue;
                }

                binding.OverrideEnabled = false;
                break;
            }

            parameterOverrides.Remove(parameterName);
        }

        public void ClearAllParameterOverrides()
        {
            SyncParameterOverrides();
            foreach (var binding in parameterOverrideBindings)
            {
                if (binding != null)
                {
                    binding.OverrideEnabled = false;
                }
            }

            parameterOverrides.Clear();
        }

        private bool SetParameterOverride(string parameterName, ImageProcessPortType expectedType, System.Action<ParameterOverrideBinding> apply)
        {
            if (graph == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            SyncParameterOverrides();
            foreach (var binding in parameterOverrideBindings)
            {
                if (binding == null || binding.ParameterName != parameterName || binding.ParameterType != expectedType)
                {
                    continue;
                }

                binding.OverrideEnabled = true;
                apply?.Invoke(binding);
                RebuildParameterOverrideMap();
                return true;
            }

            return false;
        }

        private void RebuildParameterOverrideMap()
        {
            parameterOverrides.Clear();
            if (graph == null)
            {
                return;
            }

            foreach (var binding in parameterOverrideBindings)
            {
                if (binding == null || !binding.OverrideEnabled || string.IsNullOrWhiteSpace(binding.ParameterName))
                {
                    continue;
                }

                parameterOverrides[binding.ParameterName] = new ImageProcessGraphParameter(binding.ParameterId, binding.ParameterName, binding.ParameterType)
                {
                    textureValue = binding.TextureValue,
                    floatValue = binding.FloatValue,
                    vectorValue = binding.VectorValue,
                    colorValue = binding.ColorValue
                };
            }
        }
    }
}
