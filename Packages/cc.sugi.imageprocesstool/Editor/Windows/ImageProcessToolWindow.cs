using System.Linq;
using UnityEditor;
using UnityEngine;

namespace sugi.cc.ImageProcessTool.Editor
{
    public sealed class ImageProcessToolWindow : EditorWindow
    {
        private ImageProcessGraphAsset graphAsset;
        private Vector2 scrollPosition;
        private string statusMessage = "Select a graph asset to start.";
        private MessageType statusMessageType = MessageType.Info;

        [MenuItem("Tools/sugi.cc/Image Process Tool")]
        public static void Open()
        {
            var window = GetWindow<ImageProcessToolWindow>();
            window.titleContent = new GUIContent("Image Process Tool");
            window.minSize = new Vector2(980f, 640f);
        }

        private void OnGUI()
        {
            DrawHeader();

            if (graphAsset == null)
            {
                EditorGUILayout.HelpBox("Create or assign ImageProcessGraphAsset.", MessageType.Info);
                return;
            }

            DrawCommandBar();
            DrawSerializedGraph();
            EditorGUILayout.HelpBox(statusMessage, statusMessageType);
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                var selected = (ImageProcessGraphAsset)EditorGUILayout.ObjectField("Graph", graphAsset, typeof(ImageProcessGraphAsset), false);
                if (selected != graphAsset)
                {
                    graphAsset = selected;
                    statusMessage = graphAsset == null ? "Select a graph asset to start." : "Graph selected.";
                    statusMessageType = MessageType.Info;
                }

                if (GUILayout.Button("New", GUILayout.Width(80f)))
                {
                    CreateGraphAsset();
                }

                using (new EditorGUI.DisabledScope(graphAsset == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(80f)))
                    {
                        EditorGUIUtility.PingObject(graphAsset);
                        Selection.activeObject = graphAsset;
                    }
                }
            }
        }

        private void DrawCommandBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Source Node"))
                {
                    AddNode(ImageProcessNodeKind.Source, "Source");
                }

                if (GUILayout.Button("Add Shader Node"))
                {
                    AddNode(ImageProcessNodeKind.ShaderOperator, "Shader");
                }

                if (GUILayout.Button("Add Output Node"))
                {
                    AddNode(ImageProcessNodeKind.Output, "Output");
                }

                if (GUILayout.Button("Sync Shader Ports"))
                {
                    SyncShaderNodes();
                }

                if (GUILayout.Button("Validate Order"))
                {
                    ValidateExecutionOrder();
                }
            }
        }

        private void DrawSerializedGraph()
        {
            var serializedObject = new SerializedObject(graphAsset);
            serializedObject.Update();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("nodes"), true);
            EditorGUILayout.Space(8f);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("edges"), true);
            EditorGUILayout.EndScrollView();

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(graphAsset);
            }
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

            graphAsset = asset;
            statusMessage = "Graph asset created.";
            statusMessageType = MessageType.Info;
        }

        private void AddNode(ImageProcessNodeKind kind, string defaultName)
        {
            if (graphAsset == null)
            {
                return;
            }

            Undo.RecordObject(graphAsset, $"Add {kind} node");
            graphAsset.AddNode(kind, defaultName);
            EditorUtility.SetDirty(graphAsset);
            AssetDatabase.SaveAssets();

            statusMessage = $"Added {kind} node.";
            statusMessageType = MessageType.Info;
        }

        private void SyncShaderNodes()
        {
            if (graphAsset == null)
            {
                return;
            }

            var shaderNodes = graphAsset.Nodes.Where(x => x.nodeKind == ImageProcessNodeKind.ShaderOperator).ToList();
            if (shaderNodes.Count == 0)
            {
                statusMessage = "No shader node found.";
                statusMessageType = MessageType.Warning;
                return;
            }

            Undo.RecordObject(graphAsset, "Sync shader node ports");

            var syncedCount = 0;
            foreach (var node in shaderNodes)
            {
                if (ShaderNodePortSynchronizer.TrySync(node, out _))
                {
                    syncedCount++;
                }
            }

            EditorUtility.SetDirty(graphAsset);
            AssetDatabase.SaveAssets();

            statusMessage = $"Synced {syncedCount}/{shaderNodes.Count} shader node(s).";
            statusMessageType = syncedCount == shaderNodes.Count ? MessageType.Info : MessageType.Warning;
        }

        private void ValidateExecutionOrder()
        {
            if (graphAsset == null)
            {
                return;
            }

            if (ImageProcessGraphTopology.TryBuildExecutionOrder(graphAsset, out var ordered, out var error))
            {
                statusMessage = $"Graph valid. Execution node count: {ordered.Count}";
                statusMessageType = MessageType.Info;
                return;
            }

            statusMessage = $"Graph invalid: {error}";
            statusMessageType = MessageType.Error;
        }
    }
}

