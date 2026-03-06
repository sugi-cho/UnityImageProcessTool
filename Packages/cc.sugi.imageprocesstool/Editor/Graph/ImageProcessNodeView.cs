using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace sugi.cc.ImageProcessTool.Editor
{
    internal sealed class ImageProcessNodeView : Node
    {
        private const float DefaultNodeWidth = 280f;
        private readonly Action onNodeDataChanged;
        private readonly Action onNodeViewStateChanged;
        private readonly Action<ImageProcessNodeData> onShaderSyncRequested;

        public ImageProcessNodeData NodeData { get; }

        private readonly Dictionary<string, Port> inputPorts = new();
        private readonly Dictionary<string, Port> outputPorts = new();
        private Foldout previewFoldout;
        private Image previewImage;
        private RenderTexture previewTexture;

        public ImageProcessNodeView(
            ImageProcessNodeData nodeData,
            Action onNodeDataChanged,
            Action onNodeViewStateChanged,
            Action<ImageProcessNodeData> onShaderSyncRequested)
        {
            NodeData = nodeData;
            this.onNodeDataChanged = onNodeDataChanged;
            this.onNodeViewStateChanged = onNodeViewStateChanged;
            this.onShaderSyncRequested = onShaderSyncRequested;

            title = BuildTitle(nodeData);
            viewDataKey = nodeData.nodeId;

            BuildPorts();
            BuildInspectorFields();

            var rect = new Rect(nodeData.position, new Vector2(DefaultNodeWidth, 180f));
            SetPosition(rect);
            style.minWidth = DefaultNodeWidth;
            style.maxWidth = DefaultNodeWidth;

            RefreshExpandedState();
            RefreshPorts();
        }

        public Port GetInputPort(string portId)
        {
            inputPorts.TryGetValue(portId, out var port);
            return port;
        }

        public Port GetOutputPort(string portId)
        {
            outputPorts.TryGetValue(portId, out var port);
            return port;
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            NodeData.position = newPos.position;
        }

        public void SetPreviewTexture(RenderTexture texture)
        {
            previewTexture = texture;
            if (previewImage != null)
            {
                previewImage.image = texture;
            }

            if (previewFoldout != null)
            {
                previewFoldout.text = texture == null ? "Preview (No Output)" : $"Preview ({texture.width}x{texture.height})";
            }
        }

        private string BuildTitle(ImageProcessNodeData nodeData)
        {
            var baseName = string.IsNullOrWhiteSpace(nodeData.displayName)
                ? nodeData.nodeKind.ToString()
                : nodeData.displayName;
            return $"{baseName} ({nodeData.nodeKind})";
        }

        private void BuildPorts()
        {
            inputContainer.Clear();
            outputContainer.Clear();
            inputPorts.Clear();
            outputPorts.Clear();

            foreach (var portDef in NodeData.inputPorts)
            {
                var port = CreatePort(portDef, Direction.Input);
                inputContainer.Add(port);
                inputPorts[portDef.portId] = port;
            }

            foreach (var portDef in NodeData.outputPorts)
            {
                var port = CreatePort(portDef, Direction.Output);
                outputContainer.Add(port);
                outputPorts[portDef.portId] = port;
            }
        }

        private void BuildInspectorFields()
        {
            extensionContainer.Clear();

            var nameField = new TextField("Name")
            {
                value = NodeData.displayName
            };
            nameField.RegisterValueChangedCallback(evt =>
            {
                NodeData.displayName = evt.newValue;
                title = BuildTitle(NodeData);
                onNodeDataChanged?.Invoke();
            });
            extensionContainer.Add(nameField);

            if (NodeData.nodeKind == ImageProcessNodeKind.Source)
            {
                var sourceField = new ObjectField("Texture")
                {
                    objectType = typeof(Texture),
                    allowSceneObjects = false,
                    value = NodeData.sourceTexture
                };
                sourceField.RegisterValueChangedCallback(evt =>
                {
                    NodeData.sourceTexture = evt.newValue as Texture;
                    onNodeDataChanged?.Invoke();
                });
                extensionContainer.Add(sourceField);
            }
            else if (NodeData.nodeKind == ImageProcessNodeKind.ShaderOperator)
            {
                var shaderField = new ObjectField("Shader")
                {
                    objectType = typeof(Shader),
                    allowSceneObjects = false,
                    value = NodeData.shader
                };
                shaderField.RegisterValueChangedCallback(evt =>
                {
                    NodeData.shader = evt.newValue as Shader;
                    onNodeDataChanged?.Invoke();
                });
                extensionContainer.Add(shaderField);

                var syncButton = new Button(() => onShaderSyncRequested?.Invoke(NodeData))
                {
                    text = "Sync Shader Ports"
                };
                extensionContainer.Add(syncButton);

                foreach (var parameter in NodeData.parameters)
                {
                    AddParameterField(parameter);
                }
            }
            else if (NodeData.nodeKind == ImageProcessNodeKind.Output)
            {
                var targetField = new ObjectField("Target RT")
                {
                    objectType = typeof(RenderTexture),
                    allowSceneObjects = false,
                    value = NodeData.outputRenderTexture
                };
                targetField.RegisterValueChangedCallback(evt =>
                {
                    NodeData.outputRenderTexture = evt.newValue as RenderTexture;
                    onNodeDataChanged?.Invoke();
                });
                extensionContainer.Add(targetField);
            }

            BuildPreviewSection();
            RefreshExpandedState();
        }

        private void BuildPreviewSection()
        {
            var hasRgbaOutput = NodeData.outputPorts.Exists(p =>
                p.portId == "out_rgba" &&
                p.portType == ImageProcessPortType.Texture &&
                p.direction == ImageProcessPortDirection.Output);

            if (!hasRgbaOutput)
            {
                previewFoldout = null;
                previewImage = null;
                previewTexture = null;
                return;
            }

            previewFoldout = new Foldout
            {
                text = "Preview (No Output)",
                value = NodeData.previewExpanded
            };
            previewFoldout.RegisterValueChangedCallback(evt =>
            {
                if (NodeData.previewExpanded == evt.newValue)
                {
                    return;
                }

                NodeData.previewExpanded = evt.newValue;
                onNodeViewStateChanged?.Invoke();
            });

            previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            previewImage.style.width = Length.Percent(100f);
            previewImage.style.height = 140f;
            previewImage.style.marginTop = 4f;
            previewImage.style.marginBottom = 4f;
            previewImage.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            previewFoldout.Add(previewImage);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;

            var savePngButton = new Button(() => SavePreview(ImageProcessExportFormat.Png))
            {
                text = "Save PNG"
            };
            savePngButton.style.marginRight = 4f;
            var saveExrButton = new Button(() => SavePreview(ImageProcessExportFormat.Exr))
            {
                text = "Save EXR"
            };
            saveExrButton.style.marginRight = 4f;
            var saveAssetButton = new Button(SavePreviewAsAsset)
            {
                text = "Save Asset"
            };

            buttonRow.Add(savePngButton);
            buttonRow.Add(saveExrButton);
            buttonRow.Add(saveAssetButton);
            previewFoldout.Add(buttonRow);

            extensionContainer.Add(previewFoldout);
        }

        private Port CreatePort(ImageProcessPortDefinition portDef, Direction direction)
        {
            var capacity = direction == Direction.Input ? Port.Capacity.Single : Port.Capacity.Multi;
            var portType = GetGraphValueType(portDef.portType);
            var port = InstantiatePort(Orientation.Horizontal, direction, capacity, portType);
            port.portName = portDef.displayName;
            port.userData = new PortHandle(NodeData.nodeId, portDef.portId, direction);
            return port;
        }

        private void AddParameterField(ImageProcessNodeParameter parameter)
        {
            switch (parameter.parameterType)
            {
                case ImageProcessPortType.Float:
                    var floatField = new FloatField(parameter.parameterName) { value = parameter.floatValue };
                    floatField.RegisterValueChangedCallback(evt =>
                    {
                        parameter.floatValue = evt.newValue;
                        onNodeDataChanged?.Invoke();
                    });
                    extensionContainer.Add(floatField);
                    break;

                case ImageProcessPortType.Vector4:
                    var vectorField = new Vector4Field(parameter.parameterName) { value = parameter.vectorValue };
                    vectorField.RegisterValueChangedCallback(evt =>
                    {
                        parameter.vectorValue = evt.newValue;
                        onNodeDataChanged?.Invoke();
                    });
                    extensionContainer.Add(vectorField);
                    break;

                case ImageProcessPortType.Color:
                    var colorField = new ColorField(parameter.parameterName) { value = parameter.colorValue };
                    colorField.RegisterValueChangedCallback(evt =>
                    {
                        parameter.colorValue = evt.newValue;
                        onNodeDataChanged?.Invoke();
                    });
                    extensionContainer.Add(colorField);
                    break;
            }
        }

        private void SavePreview(ImageProcessExportFormat format)
        {
            if (previewTexture == null)
            {
                EditorUtility.DisplayDialog("Image Process Tool", "プレビュー出力がありません。グラフ実行結果を確認してください。", "OK");
                return;
            }

            var extension = format == ImageProcessExportFormat.Exr ? "exr" : "png";
            var path = EditorUtility.SaveFilePanel(
                "Save Node Output",
                "",
                $"node_{MakeSafeFileName(NodeData.displayName)}",
                extension);

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!ImageProcessOutputExporter.TryExport(previewTexture, path, format, out var error))
            {
                EditorUtility.DisplayDialog("Image Process Tool", $"保存に失敗しました: {error}", "OK");
            }
        }

        private void SavePreviewAsAsset()
        {
            if (previewTexture == null)
            {
                EditorUtility.DisplayDialog("Image Process Tool", "プレビュー出力がありません。グラフ実行結果を確認してください。", "OK");
                return;
            }

            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Save Node Output As Asset",
                $"node_{MakeSafeFileName(NodeData.displayName)}",
                "asset",
                "保存先を選択してください。");

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            if (!ImageProcessOutputExporter.TryExportAsAsset(previewTexture, assetPath, out var error))
            {
                EditorUtility.DisplayDialog("Image Process Tool", $"Asset保存に失敗しました: {error}", "OK");
            }
        }

        private static string MakeSafeFileName(string value)
        {
            var safe = string.IsNullOrWhiteSpace(value) ? "output" : value;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }

            return safe;
        }

        private static Type GetGraphValueType(ImageProcessPortType portType)
        {
            return portType switch
            {
                ImageProcessPortType.Texture => typeof(Texture),
                ImageProcessPortType.Float => typeof(float),
                ImageProcessPortType.Vector4 => typeof(Vector4),
                ImageProcessPortType.Color => typeof(Color),
                _ => typeof(object)
            };
        }

        internal readonly struct PortHandle
        {
            public readonly string NodeId;
            public readonly string PortId;
            public readonly Direction Direction;

            public PortHandle(string nodeId, string portId, Direction direction)
            {
                NodeId = nodeId;
                PortId = portId;
                Direction = direction;
            }
        }
    }
}
