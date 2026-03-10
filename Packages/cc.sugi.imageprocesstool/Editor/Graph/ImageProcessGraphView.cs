using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace sugi.cc.ImageProcessTool.Editor
{
    internal sealed class ImageProcessGraphView : GraphView
    {
        private static readonly Vector2 DropNodeOffsetStep = new(28f, 28f);
        private ImageProcessGraphAsset graphAsset;
        private readonly Dictionary<string, ImageProcessNodeView> nodeViews = new();
        private readonly Dictionary<string, RenderTexture> previewCache = new();
        private bool suppressGraphEvents;

        public event System.Action GraphDataChanged;
        public bool HasGraph => graphAsset != null;

        public ImageProcessGraphView()
        {
            style.flexGrow = 1f;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        public void BindGraph(ImageProcessGraphAsset graph)
        {
            if (graphAsset != graph)
            {
                ClearPreviewCache();
            }

            graphAsset = graph;
            graphAsset?.SyncParameterNodes();
            Rebuild();
        }

        public void ApplyExecutionResult(ImageProcessExecutionResult result)
        {
            foreach (var nodeView in nodeViews.Values)
            {
                if (result != null && result.TryGetNodeOutputTexture(nodeView.NodeData.nodeId, out var outputTexture))
                {
                    CachePreviewTexture(nodeView.NodeData.nodeId, outputTexture);
                    nodeView.SetPreviewTexture(previewCache[nodeView.NodeData.nodeId]);
                }
                else
                {
                    if (previewCache.TryGetValue(nodeView.NodeData.nodeId, out var cachedTexture))
                    {
                        nodeView.SetPreviewTexture(cachedTexture);
                    }
                    else
                    {
                        nodeView.SetPreviewTexture(null);
                    }
                }
            }
        }

        public void AddNode(ImageProcessNodeKind kind, string displayName)
        {
            AddNode(kind, displayName, Vector2.zero);
        }

        public void AddNode(ImageProcessNodeKind kind, string displayName, Vector2 localPosition)
        {
            if (graphAsset == null)
            {
                return;
            }

            Undo.RecordObject(graphAsset, $"Add {kind} node");
            var node = graphAsset.AddNode(kind, displayName);
            if (node != null)
            {
                node.position = localPosition;
            }

            SaveGraphAsset();
            Rebuild();
            NotifyGraphDataChanged();
        }

        public void AddParameterNode(ImageProcessPortType parameterType, Vector2 localPosition)
        {
            if (graphAsset == null)
            {
                return;
            }

            Undo.RecordObject(graphAsset, $"Add {parameterType} parameter");
            var baseName = parameterType switch
            {
                ImageProcessPortType.Texture => "Texture",
                ImageProcessPortType.Float => "Float",
                ImageProcessPortType.Vector4 => "Vector",
                ImageProcessPortType.Color => "Color",
                _ => "Parameter"
            };

            var parameter = graphAsset.AddParameter(baseName, parameterType);
            var node = graphAsset.AddNode(ImageProcessNodeKind.Parameter, parameter.parameterName);
            node.parameterId = parameter.parameterId;
            graphAsset.SyncParameterNode(node);
            node.position = localPosition;

            SaveGraphAsset();
            Rebuild();
            NotifyGraphDataChanged();
        }

        public (int synced, int total, int removedEdges) SyncShaderPorts()
        {
            if (graphAsset == null)
            {
                return (0, 0, 0);
            }

            var shaderNodes = graphAsset.Nodes.Where(x => x.nodeKind == ImageProcessNodeKind.ShaderOperator).ToList();
            if (shaderNodes.Count == 0)
            {
                return (0, 0, 0);
            }

            Undo.RecordObject(graphAsset, "Sync shader node ports");

            var synced = 0;
            foreach (var node in shaderNodes)
            {
                if (ShaderNodePortSynchronizer.TrySync(node, out _))
                {
                    synced++;
                }
            }

            var removedEdges = graphAsset.RemoveInvalidEdges();
            SaveGraphAsset();
            Rebuild();
            NotifyGraphDataChanged();

            return (synced, shaderNodes.Count, removedEdges);
        }

        public void Rebuild()
        {
            suppressGraphEvents = true;
            try
            {
                foreach (var nodeView in nodeViews.Values)
                {
                    nodeView.DisposePreview();
                }

                DeleteElements(graphElements.ToList());
                nodeViews.Clear();

                if (graphAsset == null)
                {
                    return;
                }

                graphAsset.EnsureNodeIds();
                graphAsset.SyncParameterNodes();
                foreach (var nodeData in graphAsset.Nodes)
                {
                    var nodeView = CreateNodeView(nodeData);
                    nodeViews[nodeData.nodeId] = nodeView;
                    AddElement(nodeView);

                    if (previewCache.TryGetValue(nodeData.nodeId, out var cachedTexture))
                    {
                        nodeView.SetPreviewTexture(cachedTexture);
                    }
                }

                foreach (var edgeData in graphAsset.Edges)
                {
                    if (!nodeViews.TryGetValue(edgeData.outputNodeId, out var outputNode))
                    {
                        continue;
                    }

                    if (!nodeViews.TryGetValue(edgeData.inputNodeId, out var inputNode))
                    {
                        continue;
                    }

                    var outputPort = outputNode.GetOutputPort(edgeData.outputPortId);
                    var inputPort = inputNode.GetInputPort(edgeData.inputPortId);
                    if (outputPort == null || inputPort == null)
                    {
                        continue;
                    }

                    var edge = outputPort.ConnectTo(inputPort);
                    AddElement(edge);
                }

                TrimPreviewCache();
            }
            finally
            {
                suppressGraphEvents = false;
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            foreach (var port in ports.ToList())
            {
                if (port == startPort)
                {
                    continue;
                }

                if (port.node == startPort.node)
                {
                    continue;
                }

                if (port.direction == startPort.direction)
                {
                    continue;
                }

                if (port.portType != startPort.portType)
                {
                    continue;
                }

                compatible.Add(port);
            }

            return compatible;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            if (graphAsset == null)
            {
                return;
            }

            var localPosition = this.ChangeCoordinatesTo(contentViewContainer, evt.localMousePosition);
            evt.menu.AppendAction("Add/Parameter/Texture", _ => AddParameterNode(ImageProcessPortType.Texture, localPosition));
            evt.menu.AppendAction("Add/Parameter/Float", _ => AddParameterNode(ImageProcessPortType.Float, localPosition));
            evt.menu.AppendAction("Add/Parameter/Vector4", _ => AddParameterNode(ImageProcessPortType.Vector4, localPosition));
            evt.menu.AppendAction("Add/Parameter/Color", _ => AddParameterNode(ImageProcessPortType.Color, localPosition));
            evt.menu.AppendAction("Add/Shader Node", _ => AddNode(ImageProcessNodeKind.ShaderOperator, "Shader", localPosition));
            evt.menu.AppendAction("Add/Blur Node", _ => AddNode(ImageProcessNodeKind.BlurOperator, "Blur", localPosition));
            evt.menu.AppendAction("Add/Iterative Filter Node", _ => AddNode(ImageProcessNodeKind.IterativeFilterOperator, "Iterative Filter", localPosition));
            evt.menu.AppendAction("Add/Output Node", _ => AddNode(ImageProcessNodeKind.Output, "Output", localPosition));
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (graphAsset == null)
            {
                return;
            }

            if (!TryCollectSupportedDraggedObjects(out _))
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (graphAsset == null)
            {
                return;
            }

            if (!TryCollectSupportedDraggedObjects(out var droppedObjects))
            {
                return;
            }

            DragAndDrop.AcceptDrag();
            var localPosition = contentViewContainer.WorldToLocal(evt.mousePosition);
            AddNodesFromDrop(droppedObjects, localPosition);
            evt.StopPropagation();
        }

        private ImageProcessNodeView CreateNodeView(ImageProcessNodeData nodeData)
        {
            return new ImageProcessNodeView(
                nodeData,
                onNodeDataChanged: OnNodeDataChanged,
                onNodeViewStateChanged: OnNodeViewStateChanged,
                onShaderSyncRequested: SyncSingleShaderNode,
                parameterProvider: () => graphAsset?.Parameters);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (suppressGraphEvents || graphAsset == null)
            {
                return change;
            }

            var shouldSave = false;
            var shouldRebuild = false;
            var shouldExecute = false;

            if (change.elementsToRemove != null)
            {
                Undo.RecordObject(graphAsset, "Remove graph element");
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge && TryGetEdgeHandles(edge, out var output, out var input))
                    {
                        if (graphAsset.RemoveEdge(output.NodeId, output.PortId, input.NodeId, input.PortId))
                        {
                            shouldSave = true;
                            shouldExecute = true;
                        }
                    }
                    else if (element is ImageProcessNodeView nodeView)
                    {
                        graphAsset.RemoveNode(nodeView.NodeData.nodeId);
                        shouldSave = true;
                        shouldExecute = true;
                    }
                }

                if (graphAsset.RemoveUnusedParameters() > 0)
                {
                    shouldSave = true;
                    shouldRebuild = true;
                    shouldExecute = true;
                }
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                Undo.RecordObject(graphAsset, "Create edge");
                foreach (var edge in change.edgesToCreate)
                {
                    if (!TryGetEdgeHandles(edge, out var output, out var input))
                    {
                        continue;
                    }

                    if (graphAsset.AddEdge(output.NodeId, output.PortId, input.NodeId, input.PortId))
                    {
                        shouldSave = true;
                        shouldExecute = true;
                    }
                    else
                    {
                        shouldRebuild = true;
                    }
                }
            }

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                Undo.RecordObject(graphAsset, "Move node");
                foreach (var element in change.movedElements)
                {
                    if (element is ImageProcessNodeView nodeView)
                    {
                        var rect = nodeView.GetPosition();
                        nodeView.NodeData.position = rect.position;
                        shouldSave = true;
                    }
                }
            }

            if (shouldSave)
            {
                SaveGraphAsset();
            }

            if (shouldRebuild)
            {
                Rebuild();
            }

            if (shouldExecute)
            {
                NotifyGraphDataChanged();
            }

            return change;
        }

        private void SyncSingleShaderNode(ImageProcessNodeData node)
        {
            if (graphAsset == null || node == null || node.nodeKind != ImageProcessNodeKind.ShaderOperator)
            {
                return;
            }

            Undo.RecordObject(graphAsset, "Sync shader node ports");
            if (ShaderNodePortSynchronizer.TrySync(node, out _))
            {
                graphAsset.RemoveInvalidEdges();
                SaveGraphAsset();
                Rebuild();
                NotifyGraphDataChanged();
            }
        }

        private void OnNodeDataChanged(ImageProcessNodeData nodeData)
        {
            if (graphAsset != null && nodeData != null)
            {
                if (nodeData.nodeKind == ImageProcessNodeKind.Parameter)
                {
                    graphAsset.SyncParameterNode(nodeData);
                    graphAsset.RemoveUnusedParameters();
                    graphAsset.RemoveInvalidEdges();
                    Rebuild();
                }

                var uniqueName = graphAsset.MakeUniqueNodeDisplayName(nodeData.nodeKind, nodeData.displayName, nodeData.nodeId);
                if (nodeData.displayName != uniqueName)
                {
                    nodeData.displayName = uniqueName;
                    Rebuild();
                }
            }

            SaveGraphAsset();
            NotifyGraphDataChanged();
        }

        private void OnNodeViewStateChanged()
        {
            SaveGraphAsset();
        }

        private bool TryGetEdgeHandles(
            Edge edge,
            out ImageProcessNodeView.PortHandle output,
            out ImageProcessNodeView.PortHandle input)
        {
            output = default;
            input = default;

            if (edge.output?.userData is not ImageProcessNodeView.PortHandle outputHandle)
            {
                return false;
            }

            if (edge.input?.userData is not ImageProcessNodeView.PortHandle inputHandle)
            {
                return false;
            }

            if (outputHandle.Direction != Direction.Output || inputHandle.Direction != Direction.Input)
            {
                return false;
            }

            output = outputHandle;
            input = inputHandle;
            return true;
        }

        private void SaveGraphAsset()
        {
            if (graphAsset == null)
            {
                return;
            }

            if (RenderTexture.active != null)
            {
                RenderTexture.active = null;
            }

            EditorUtility.SetDirty(graphAsset);
        }

        private void NotifyGraphDataChanged()
        {
            GraphDataChanged?.Invoke();
        }

        private void CachePreviewTexture(string nodeId, RenderTexture source)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || source == null)
            {
                return;
            }

            if (!previewCache.TryGetValue(nodeId, out var cached) ||
                cached == null ||
                cached.width != source.width ||
                cached.height != source.height)
            {
                ReleasePreviewCacheTexture(nodeId);
                cached = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf)
                {
                    name = $"GraphPreviewCache_{nodeId}",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                cached.Create();
                previewCache[nodeId] = cached;
            }

            Graphics.Blit(source, cached);
        }

        private void TrimPreviewCache()
        {
            if (graphAsset == null)
            {
                ClearPreviewCache();
                return;
            }

            var validNodeIds = new HashSet<string>(graphAsset.Nodes.Where(x => x != null).Select(x => x.nodeId));
            var removeIds = previewCache.Keys.Where(x => !validNodeIds.Contains(x)).ToList();
            foreach (var nodeId in removeIds)
            {
                ReleasePreviewCacheTexture(nodeId);
            }
        }

        private void ClearPreviewCache()
        {
            foreach (var nodeId in previewCache.Keys.ToList())
            {
                ReleasePreviewCacheTexture(nodeId);
            }
        }

        private void ReleasePreviewCacheTexture(string nodeId)
        {
            if (!previewCache.TryGetValue(nodeId, out var texture) || texture == null)
            {
                previewCache.Remove(nodeId);
                return;
            }

            if (RenderTexture.active == texture)
            {
                RenderTexture.active = null;
            }

            texture.Release();
#if UNITY_EDITOR
            Object.DestroyImmediate(texture);
#else
            Object.Destroy(texture);
#endif
            previewCache.Remove(nodeId);
        }

        private void AddNodesFromDrop(List<Object> droppedObjects, Vector2 localPosition)
        {
            if (graphAsset == null || droppedObjects == null || droppedObjects.Count == 0)
            {
                return;
            }

            Undo.RecordObject(graphAsset, "Add nodes from drag and drop");

            var createdAny = false;
            var position = localPosition;

            foreach (var dropped in droppedObjects)
            {
                if (dropped is Texture texture)
                {
                    var parameter = graphAsset.AddParameter(texture.name, ImageProcessPortType.Texture);
                    parameter.textureValue = texture;
                    var parameterNode = graphAsset.AddNode(ImageProcessNodeKind.Parameter, parameter.parameterName);
                    parameterNode.parameterId = parameter.parameterId;
                    graphAsset.SyncParameterNode(parameterNode);
                    parameterNode.position = position;
                    createdAny = true;
                    position += DropNodeOffsetStep;
                    continue;
                }

                if (dropped is Shader shader)
                {
                    var shaderNode = graphAsset.AddNode(ImageProcessNodeKind.ShaderOperator, shader.name);
                    shaderNode.shader = shader;
                    shaderNode.position = position;
                    ShaderNodePortSynchronizer.TrySync(shaderNode, out _);
                    createdAny = true;
                    position += DropNodeOffsetStep;
                }
            }

            if (!createdAny)
            {
                return;
            }

            graphAsset.RemoveInvalidEdges();
            graphAsset.SyncParameterNodes();
            SaveGraphAsset();
            Rebuild();
            NotifyGraphDataChanged();
        }

        private static bool TryCollectSupportedDraggedObjects(out List<Object> droppedObjects)
        {
            droppedObjects = new List<Object>();
            var refs = DragAndDrop.objectReferences;
            if (refs == null || refs.Length == 0)
            {
                return false;
            }

            foreach (var obj in refs)
            {
                if (obj is Texture || obj is Shader)
                {
                    droppedObjects.Add(obj);
                }
            }

            return droppedObjects.Count > 0;
        }
    }
}
