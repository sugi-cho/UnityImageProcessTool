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
        private readonly Action<ImageProcessNodeData> onNodeDataChanged;
        private readonly Action onNodeViewStateChanged;
        private readonly Action<ImageProcessNodeData> onShaderSyncRequested;
        private readonly Func<IReadOnlyList<ImageProcessGraphParameter>> parameterProvider;

        public ImageProcessNodeData NodeData { get; }

        private readonly Dictionary<string, Port> inputPorts = new();
        private readonly Dictionary<string, Port> outputPorts = new();
        private Foldout previewFoldout;
        private Image previewImage;
        private RenderTexture previewTexture;
        private TextField titleEditField;
        private bool isEditingTitle;

        public ImageProcessNodeView(
            ImageProcessNodeData nodeData,
            Action<ImageProcessNodeData> onNodeDataChanged,
            Action onNodeViewStateChanged,
            Action<ImageProcessNodeData> onShaderSyncRequested,
            Func<IReadOnlyList<ImageProcessGraphParameter>> parameterProvider)
        {
            NodeData = nodeData;
            this.onNodeDataChanged = onNodeDataChanged;
            this.onNodeViewStateChanged = onNodeViewStateChanged;
            this.onShaderSyncRequested = onShaderSyncRequested;
            this.parameterProvider = parameterProvider;

            title = BuildTitle(nodeData);
            viewDataKey = nodeData.nodeId;

            BuildTitleEditor();
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
            if (texture == null)
            {
                ReleasePreviewTexture();
                if (previewImage != null)
                {
                    previewImage.image = null;
                }

                if (previewFoldout != null)
                {
                    previewFoldout.text = "Preview (No Output)";
                }

                return;
            }

            EnsurePreviewTexture(texture.width, texture.height);
            Graphics.Blit(texture, previewTexture);
            if (previewImage != null)
            {
                previewImage.image = previewTexture;
            }

            if (previewFoldout != null)
            {
                previewFoldout.text = $"Preview ({texture.width}x{texture.height})";
            }
        }

        public void DisposePreview()
        {
            ReleasePreviewTexture();
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

        private void BuildTitleEditor()
        {
            titleContainer.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.clickCount != 2)
                {
                    return;
                }

                BeginTitleEdit();
                evt.StopPropagation();
            });

            titleEditField = new TextField
            {
                value = NodeData.displayName
            };
            titleEditField.style.display = DisplayStyle.None;
            titleEditField.style.flexGrow = 1f;
            titleEditField.style.marginLeft = 0f;
            titleEditField.style.marginRight = 0f;
            titleEditField.RegisterValueChangedCallback(evt =>
            {
                if (!isEditingTitle)
                {
                    return;
                }

                NodeData.displayName = evt.newValue;
            });
            titleEditField.RegisterCallback<FocusOutEvent>(_ => EndTitleEdit(applyChanges: true));
            titleEditField.RegisterCallback<KeyDownEvent>(evt =>
            {
                switch (evt.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        EndTitleEdit(applyChanges: true);
                        evt.StopPropagation();
                        break;

                    case KeyCode.Escape:
                        EndTitleEdit(applyChanges: false);
                        evt.StopPropagation();
                        break;
                }
            });
            titleContainer.Add(titleEditField);
        }

        private void BuildInspectorFields()
        {
            extensionContainer.Clear();

            if (NodeData.nodeKind == ImageProcessNodeKind.Parameter)
            {
                BuildParameterNodeFields();
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
                    if (NodeData.shader != null)
                    {
                        NodeData.displayName = NodeData.shader.name;
                        UpdateTitleDisplay();
                    }

                    onNodeDataChanged?.Invoke(NodeData);
                    onShaderSyncRequested?.Invoke(NodeData);
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
            else if (NodeData.nodeKind == ImageProcessNodeKind.BlurOperator)
            {
                BuildBlurSettingsFields(includeFilterKind: false);
            }
            else if (NodeData.nodeKind == ImageProcessNodeKind.IterativeFilterOperator)
            {
                BuildBlurSettingsFields(includeFilterKind: true);
            }
            BuildPreviewSection();
            RefreshExpandedState();
        }

        private void BuildBlurSettingsFields(bool includeFilterKind)
        {
            var filterKind = includeFilterKind ? NodeData.iterativeFilterKind : ImageProcessIterativeFilterKind.Blur;
            if (includeFilterKind)
            {
                var filterKindField = new EnumField("Filter", NodeData.iterativeFilterKind);
                filterKindField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue is ImageProcessIterativeFilterKind filterKind)
                    {
                        NodeData.iterativeFilterKind = filterKind;
                        onNodeDataChanged?.Invoke(NodeData);
                    }
                });
                extensionContainer.Add(filterKindField);
            }

            if (filterKind == ImageProcessIterativeFilterKind.Blur)
            {
                var modeField = new EnumField("Mode", NodeData.blurMode);
                modeField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue is ImageProcessBlurMode mode)
                    {
                        NodeData.blurMode = mode;
                        onNodeDataChanged?.Invoke(NodeData);
                    }
                });
                extensionContainer.Add(modeField);
            }

            var radiusField = new FloatField("Radius")
            {
                value = NodeData.blurRadius
            };
            radiusField.RegisterValueChangedCallback(evt =>
            {
                NodeData.blurRadius = Mathf.Max(0.001f, evt.newValue);
                radiusField.SetValueWithoutNotify(NodeData.blurRadius);
                onNodeDataChanged?.Invoke(NodeData);
            });
            extensionContainer.Add(radiusField);

            var iterationField = new IntegerField("Iterations")
            {
                value = NodeData.blurIterations
            };
            iterationField.RegisterValueChangedCallback(evt =>
            {
                NodeData.blurIterations = Mathf.Max(1, evt.newValue);
                iterationField.SetValueWithoutNotify(NodeData.blurIterations);
                onNodeDataChanged?.Invoke(NodeData);
            });
            extensionContainer.Add(iterationField);

            var downsampleField = new IntegerField("Downsample")
            {
                value = NodeData.blurDownsample
            };
            downsampleField.RegisterValueChangedCallback(evt =>
            {
                NodeData.blurDownsample = Mathf.Clamp(evt.newValue, 0, 4);
                downsampleField.SetValueWithoutNotify(NodeData.blurDownsample);
                onNodeDataChanged?.Invoke(NodeData);
            });
            extensionContainer.Add(downsampleField);
        }

        private void BuildParameterNodeFields()
        {
            var parameters = parameterProvider?.Invoke();
            var options = new List<string>();
            var selectedIndex = -1;
            ImageProcessGraphParameter selectedParameter = null;

            if (parameters != null)
            {
                for (var i = 0; i < parameters.Count; i++)
                {
                    var parameter = parameters[i];
                    options.Add(parameter.parameterName);
                    if (parameter.parameterId == NodeData.parameterId)
                    {
                        selectedIndex = i;
                    }
                }
            }

            if (options.Count == 0)
            {
                extensionContainer.Add(new HelpBox("GraphAsset に Parameter がありません。", HelpBoxMessageType.Info));
                return;
            }

            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            selectedParameter = parameters[selectedIndex];
            var popup = new PopupField<string>("Parameter", options, selectedIndex);
            popup.RegisterValueChangedCallback(evt =>
            {
                var index = options.IndexOf(evt.newValue);
                if (index < 0)
                {
                    return;
                }

                NodeData.parameterId = parameters[index].parameterId;
                NodeData.displayName = parameters[index].parameterName;
                UpdateTitleDisplay();
                onNodeDataChanged?.Invoke(NodeData);
            });
            extensionContainer.Add(popup);

            if (selectedParameter == null)
            {
                return;
            }

            switch (selectedParameter.parameterType)
            {
                case ImageProcessPortType.Texture:
                    var textureField = new ObjectField("Default Value")
                    {
                        objectType = typeof(Texture),
                        allowSceneObjects = false,
                        value = selectedParameter.textureValue
                    };
                    textureField.RegisterValueChangedCallback(evt =>
                    {
                        selectedParameter.textureValue = evt.newValue as Texture;
                        onNodeDataChanged?.Invoke(NodeData);
                    });
                    extensionContainer.Add(textureField);
                    break;

                case ImageProcessPortType.Float:
                    var floatField = new FloatField("Default Value")
                    {
                        value = selectedParameter.floatValue
                    };
                    floatField.RegisterValueChangedCallback(evt =>
                    {
                        selectedParameter.floatValue = evt.newValue;
                        onNodeDataChanged?.Invoke(NodeData);
                    });
                    extensionContainer.Add(floatField);
                    break;

                case ImageProcessPortType.Vector4:
                    var vectorField = new Vector4Field("Default Value")
                    {
                        value = selectedParameter.vectorValue
                    };
                    vectorField.RegisterValueChangedCallback(evt =>
                    {
                        selectedParameter.vectorValue = evt.newValue;
                        onNodeDataChanged?.Invoke(NodeData);
                    });
                    extensionContainer.Add(vectorField);
                    break;

                case ImageProcessPortType.Color:
                    var colorField = new ColorField("Default Value")
                    {
                        value = selectedParameter.colorValue
                    };
                    colorField.hdr = true;
                    colorField.RegisterValueChangedCallback(evt =>
                    {
                        selectedParameter.colorValue = evt.newValue;
                        onNodeDataChanged?.Invoke(NodeData);
                    });
                    extensionContainer.Add(colorField);
                    break;
            }
        }

        private void BuildPreviewSection()
        {
            var hasTextureOutput = NodeData.outputPorts.Exists(p =>
                p.portType == ImageProcessPortType.Texture &&
                p.direction == ImageProcessPortDirection.Output);

            if (!hasTextureOutput)
            {
                previewFoldout = null;
                previewImage = null;
                ReleasePreviewTexture();
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
                    if (parameter.useRange)
                    {
                        var lowValue = Mathf.Min(parameter.rangeMin, parameter.rangeMax);
                        var highValue = Mathf.Max(parameter.rangeMin, parameter.rangeMax);
                        parameter.floatValue = Mathf.Clamp(parameter.floatValue, lowValue, highValue);

                        var rangeContainer = new VisualElement();
                        rangeContainer.style.marginBottom = 4f;

                        var label = new Label(parameter.parameterName);
                        label.style.unityFontStyleAndWeight = FontStyle.Bold;
                        label.style.marginBottom = 2f;
                        rangeContainer.Add(label);

                        var inputRow = new VisualElement();
                        inputRow.style.flexDirection = FlexDirection.Row;
                        inputRow.style.alignItems = Align.Center;

                        var slider = new Slider(lowValue, highValue)
                        {
                            value = parameter.floatValue
                        };
                        slider.style.flexGrow = 1f;
                        slider.style.marginRight = 6f;

                        var floatField = new FloatField
                        {
                            value = parameter.floatValue
                        };
                        floatField.style.width = 72f;

                        void ApplyRangeValue(float value, bool updateSlider, bool updateFloatField)
                        {
                            parameter.floatValue = Mathf.Clamp(value, lowValue, highValue);
                            if (updateSlider && !Mathf.Approximately(slider.value, parameter.floatValue))
                            {
                                slider.SetValueWithoutNotify(parameter.floatValue);
                            }

                            if (updateFloatField && !Mathf.Approximately(floatField.value, parameter.floatValue))
                            {
                                floatField.SetValueWithoutNotify(parameter.floatValue);
                            }

                            onNodeDataChanged?.Invoke(NodeData);
                        }

                        slider.RegisterValueChangedCallback(evt => ApplyRangeValue(evt.newValue, false, true));
                        floatField.RegisterValueChangedCallback(evt => ApplyRangeValue(evt.newValue, true, false));

                        inputRow.Add(slider);
                        inputRow.Add(floatField);
                        rangeContainer.Add(inputRow);
                        extensionContainer.Add(rangeContainer);
                    }
                    else
                    {
                        var floatField = new FloatField(parameter.parameterName) { value = parameter.floatValue };
                        floatField.RegisterValueChangedCallback(evt =>
                        {
                            parameter.floatValue = evt.newValue;
                            onNodeDataChanged?.Invoke(NodeData);
                        });
                        extensionContainer.Add(floatField);
                    }
                    break;

                case ImageProcessPortType.Vector4:
                    var vectorField = new Vector4Field(parameter.parameterName) { value = parameter.vectorValue };
                    vectorField.RegisterValueChangedCallback(evt =>
                    {
                        parameter.vectorValue = evt.newValue;
                        onNodeDataChanged?.Invoke(NodeData);
                    });
                    extensionContainer.Add(vectorField);
                    break;

                case ImageProcessPortType.Color:
                    var colorField = new ColorField(parameter.parameterName) { value = parameter.colorValue };
                    colorField.hdr = true;
                    colorField.RegisterValueChangedCallback(evt =>
                    {
                        parameter.colorValue = evt.newValue;
                        onNodeDataChanged?.Invoke(NodeData);
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

        private void BeginTitleEdit()
        {
            if (titleEditField == null || isEditingTitle)
            {
                return;
            }

            isEditingTitle = true;
            titleEditField.SetValueWithoutNotify(NodeData.displayName);
            titleEditField.style.display = DisplayStyle.Flex;
            SetTitleLabelVisible(false);
            titleEditField.schedule.Execute(() =>
            {
                titleEditField.Focus();
                titleEditField.SelectAll();
            });
        }

        private void EndTitleEdit(bool applyChanges)
        {
            if (titleEditField == null || !isEditingTitle)
            {
                return;
            }

            isEditingTitle = false;

            if (!applyChanges)
            {
                titleEditField.SetValueWithoutNotify(NodeData.displayName);
            }
            else
            {
                var nextName = string.IsNullOrWhiteSpace(titleEditField.value)
                    ? NodeData.nodeKind.ToString()
                    : titleEditField.value.Trim();

                if (NodeData.displayName != nextName)
                {
                    NodeData.displayName = nextName;
                    onNodeDataChanged?.Invoke(NodeData);
                }
            }

            titleEditField.style.display = DisplayStyle.None;
            UpdateTitleDisplay();
            SetTitleLabelVisible(true);
        }

        private void UpdateTitleDisplay()
        {
            title = BuildTitle(NodeData);
        }

        private void SetTitleLabelVisible(bool visible)
        {
            var titleLabel = titleContainer.Q<Label>();
            if (titleLabel == null)
            {
                return;
            }

            titleLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

        private void EnsurePreviewTexture(int width, int height)
        {
            if (previewTexture != null && previewTexture.width == width && previewTexture.height == height)
            {
                if (!previewTexture.IsCreated())
                {
                    previewTexture.Create();
                }

                return;
            }

            ReleasePreviewTexture();
            previewTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf)
            {
                name = $"NodePreview_{NodeData.nodeId}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            previewTexture.Create();
        }

        private void ReleasePreviewTexture()
        {
            if (previewTexture == null)
            {
                return;
            }

            if (RenderTexture.active == previewTexture)
            {
                RenderTexture.active = null;
            }

            previewTexture.Release();
#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(previewTexture);
#else
            UnityEngine.Object.Destroy(previewTexture);
#endif
            previewTexture = null;
        }
    }
}
