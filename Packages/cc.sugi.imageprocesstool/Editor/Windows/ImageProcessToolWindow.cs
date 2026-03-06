using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace sugi.cc.ImageProcessTool.Editor
{
    public sealed class ImageProcessToolWindow : EditorWindow
    {
        private ImageProcessGraphAsset graphAsset;
        private ImageProcessExecutionResult executionResult;

        private ObjectField graphObjectField;
        private HelpBox statusHelpBox;
        private ImageProcessGraphView graphView;

        [MenuItem("Tools/sugi.cc/Image Process Tool")]
        public static void Open()
        {
            var window = GetWindow<ImageProcessToolWindow>();
            window.titleContent = new GUIContent("Image Process Tool");
            window.minSize = new Vector2(980f, 640f);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            var toolbar = BuildToolbar();
            graphView = new ImageProcessGraphView();
            graphView.GraphDataChanged += OnGraphDataChanged;
            statusHelpBox = new HelpBox("Create or assign ImageProcessGraphAsset.", HelpBoxMessageType.Info);

            rootVisualElement.Add(toolbar);
            rootVisualElement.Add(graphView);
            rootVisualElement.Add(statusHelpBox);

            if (graphAsset == null && Selection.activeObject is ImageProcessGraphAsset selected)
            {
                SetGraphAsset(selected);
            }
            else
            {
                SetGraphAsset(graphAsset);
            }
        }

        private void OnDisable()
        {
            if (graphView != null)
            {
                graphView.GraphDataChanged -= OnGraphDataChanged;
            }

            DisposeExecutionResult();
        }

        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar();

            graphObjectField = new ObjectField("Graph")
            {
                objectType = typeof(ImageProcessGraphAsset),
                allowSceneObjects = false
            };
            graphObjectField.RegisterValueChangedCallback(evt => SetGraphAsset(evt.newValue as ImageProcessGraphAsset));
            toolbar.Add(graphObjectField);

            toolbar.Add(new ToolbarButton(CreateGraphAsset) { text = "New" });
            toolbar.Add(new ToolbarButton(() => AddNode(ImageProcessNodeKind.Source, "Source")) { text = "Add Source" });
            toolbar.Add(new ToolbarButton(() => AddNode(ImageProcessNodeKind.ShaderOperator, "Shader")) { text = "Add Shader" });
            toolbar.Add(new ToolbarButton(() => AddNode(ImageProcessNodeKind.Output, "Output")) { text = "Add Output" });
            toolbar.Add(new ToolbarButton(SyncShaderNodes) { text = "Sync Shader Ports" });
            toolbar.Add(new ToolbarButton(ValidateGraph) { text = "Validate" });
            toolbar.Add(new ToolbarButton(SaveGraphAsset) { text = "Save" });
            toolbar.Add(new ToolbarButton(() => graphView?.Rebuild()) { text = "Reload View" });

            return toolbar;
        }

        private void SetGraphAsset(ImageProcessGraphAsset asset)
        {
            graphAsset = asset;
            graphObjectField?.SetValueWithoutNotify(asset);
            graphView?.BindGraph(asset);
            DisposeExecutionResult();

            if (asset == null)
            {
                SetStatus("Create or assign ImageProcessGraphAsset.", HelpBoxMessageType.Info);
                return;
            }

            SetStatus("Graph loaded. Auto-run is enabled.", HelpBoxMessageType.Info);
            AutoExecuteGraph();
        }

        private void CreateGraphAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Image Process Graph",
                "ImageProcessGraph",
                "asset",
                "Select save location for ImageProcessGraph asset.");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var asset = CreateInstance<ImageProcessGraphAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;

            SetGraphAsset(asset);
            SetStatus("Graph asset created.", HelpBoxMessageType.Info);
        }

        private void AddNode(ImageProcessNodeKind kind, string defaultName)
        {
            if (!EnsureGraphAssigned())
            {
                return;
            }

            graphView.AddNode(kind, defaultName);
            SetStatus($"Added {kind} node.", HelpBoxMessageType.Info);
        }

        private void SyncShaderNodes()
        {
            if (!EnsureGraphAssigned())
            {
                return;
            }

            var (synced, total, removedEdges) = graphView.SyncShaderPorts();
            if (total == 0)
            {
                SetStatus("No shader node found.", HelpBoxMessageType.Warning);
                return;
            }

            var message = $"Synced {synced}/{total} shader node(s).";
            if (removedEdges > 0)
            {
                message += $" Removed invalid edges: {removedEdges}.";
            }

            SetStatus(message, synced == total ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning);
        }

        private void ValidateGraph()
        {
            if (!EnsureGraphAssigned())
            {
                return;
            }

            if (!ImageProcessGraphValidator.TryValidateForExecution(graphAsset, out var error))
            {
                SetStatus($"Graph invalid: {error}", HelpBoxMessageType.Error);
                return;
            }

            if (!ImageProcessGraphTopology.TryBuildExecutionOrder(graphAsset, out var ordered, out error))
            {
                SetStatus($"Graph invalid: {error}", HelpBoxMessageType.Error);
                return;
            }

            SetStatus($"Graph valid. Execution node count: {ordered.Count}", HelpBoxMessageType.Info);
        }

        private void SaveGraphAsset()
        {
            if (!EnsureGraphAssigned())
            {
                return;
            }

            EditorUtility.SetDirty(graphAsset);
            AssetDatabase.SaveAssets();
            SetStatus("Graph asset saved.", HelpBoxMessageType.Info);
        }

        private bool EnsureGraphAssigned()
        {
            if (graphAsset != null)
            {
                return true;
            }

            SetStatus("Graph asset is not assigned.", HelpBoxMessageType.Warning);
            return false;
        }

        private void OnGraphDataChanged()
        {
            AutoExecuteGraph();
        }

        private void AutoExecuteGraph()
        {
            if (graphAsset == null)
            {
                return;
            }

            DisposeExecutionResult();
            if (!ImageProcessGraphExecutor.TryExecute(graphAsset, out var result, out var error))
            {
                graphView?.ApplyExecutionResult(null);
                SetStatus($"Auto-run failed: {error}", HelpBoxMessageType.Warning);
                return;
            }

            executionResult = result;
            graphView?.ApplyExecutionResult(executionResult);
            SetStatus($"Auto-run completed. Output nodes: {executionResult.OutputNodeIds.Count}", HelpBoxMessageType.Info);
        }

        private void DisposeExecutionResult()
        {
            if (executionResult == null)
            {
                return;
            }

            executionResult.Dispose();
            executionResult = null;
        }

        private void SetStatus(string message, HelpBoxMessageType type)
        {
            if (statusHelpBox == null)
            {
                return;
            }

            statusHelpBox.text = message;
            statusHelpBox.messageType = type;
        }
    }
}
