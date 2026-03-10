using UnityEditor;
using UnityEngine;

namespace sugi.cc.ImageProcessTool.Editor
{
    [CustomEditor(typeof(ImageProcessGraphRunner))]
    internal sealed class ImageProcessGraphRunnerEditor : UnityEditor.Editor
    {
        private SerializedProperty graphProperty;
        private SerializedProperty parameterOverrideBindingsProperty;
        private SerializedProperty updateTimingProperty;
        private SerializedProperty executeOnEnableProperty;
        private SerializedProperty executeInEditModeProperty;
        private SerializedProperty logErrorsProperty;
        private readonly System.Collections.Generic.Dictionary<string, RenderTexture> previewCache = new();
        private bool previewRefreshQueued;
        private string previewError = string.Empty;
        private double lastPreviewRefreshTime;

        private void OnEnable()
        {
            graphProperty = serializedObject.FindProperty("graph");
            parameterOverrideBindingsProperty = serializedObject.FindProperty("parameterOverrideBindings");
            updateTimingProperty = serializedObject.FindProperty("updateTiming");
            executeOnEnableProperty = serializedObject.FindProperty("executeOnEnable");
            executeInEditModeProperty = serializedObject.FindProperty("executeInEditMode");
            logErrorsProperty = serializedObject.FindProperty("logErrors");
        }

        private void OnDisable()
        {
            if (RenderTexture.active != null)
            {
                RenderTexture.active = null;
            }

            ClearPreviewCache();
            previewRefreshQueued = false;
            previewError = string.Empty;
        }

        public override void OnInspectorGUI()
        {
            var runner = (ImageProcessGraphRunner)target;
            var synchronized = runner.SyncOutputDestinations();
            synchronized |= runner.SyncParameterOverrides();
            if (synchronized)
            {
                EditorUtility.SetDirty(runner);
            }

            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(graphProperty);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                runner.SyncOutputDestinations();
                runner.SyncParameterOverrides();
                EditorUtility.SetDirty(runner);
                serializedObject.Update();
            }

            DrawParameterOverrides();
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            DrawOutputDestinations(runner);

            EditorGUILayout.PropertyField(updateTimingProperty);
            EditorGUILayout.PropertyField(executeOnEnableProperty);
            if (executeInEditModeProperty != null)
            {
                EditorGUILayout.PropertyField(executeInEditModeProperty);
            }
            EditorGUILayout.PropertyField(logErrorsProperty);

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed)
            {
                EditorUtility.SetDirty(runner);
            }
        }

        private void DrawOutputDestinations(ImageProcessGraphRunner runner)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output Destinations", EditorStyles.boldLabel);

            var bindings = runner.OutputDestinations ?? System.Array.Empty<ImageProcessGraphRunner.OutputDestinationBinding>();
            var outputNodeNames = new string[bindings.Length];
            var previewExpandedStates = new bool[bindings.Length];

            if (graphProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("GraphAsset を設定すると Output 一覧が表示されます。", MessageType.Info);
                return;
            }

            if (bindings.Length == 0)
            {
                EditorGUILayout.HelpBox("GraphAsset に Output ノードがありません。", MessageType.Info);
                return;
            }

            var bindingsChanged = false;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var outputNodeName = string.IsNullOrWhiteSpace(binding.OutputNodeName) ? $"Output {i + 1}" : binding.OutputNodeName;
                    outputNodeNames[i] = outputNodeName;
                    EditorGUILayout.LabelField(
                        outputNodeName,
                        EditorStyles.boldLabel);

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("Output Node Name", binding.OutputNodeName);
                    }

                    EditorGUI.BeginChangeCheck();
                    var destination = (RenderTexture)EditorGUILayout.ObjectField(
                        "Destination",
                        binding.Destination,
                        typeof(RenderTexture),
                        false);
                    var previewExpanded = EditorGUILayout.Toggle("Preview Expanded", binding.PreviewExpanded);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(runner, "Edit Output Destination");
                        binding.Destination = destination;
                        binding.PreviewExpanded = previewExpanded;
                        EditorUtility.SetDirty(runner);
                        bindingsChanged = true;
                    }

                    previewExpandedStates[i] = previewExpanded;
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Output Previews", EditorStyles.boldLabel);
            var hasExpandedPreview = false;

            for (var i = 0; i < bindings.Length; i++)
            {
                if (previewExpandedStates[i])
                {
                    hasExpandedPreview = true;
                }
            }

            if (hasExpandedPreview)
            {
                RequestPreviewRefresh(runner, GUI.changed || bindingsChanged);
            }

            for (var i = 0; i < bindings.Length; i++)
            {
                if (!previewExpandedStates[i])
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var outputNodeName = outputNodeNames[i];
                    EditorGUILayout.LabelField(outputNodeName, EditorStyles.boldLabel);

                    if (previewCache.TryGetValue(outputNodeName, out var previewTexture) && previewTexture != null)
                    {
                        EditorGUILayout.LabelField($"Preview ({previewTexture.width}x{previewTexture.height})", EditorStyles.miniLabel);
                        var previewRect = GUILayoutUtility.GetRect(1f, 140f, GUILayout.ExpandWidth(true));
                        DrawAlphaPreview(previewRect, previewTexture);
                        continue;
                    }

                    if (previewRefreshQueued)
                    {
                        EditorGUILayout.HelpBox("Preview を更新中です。", MessageType.Info);
                        continue;
                    }

                    var message = string.IsNullOrWhiteSpace(previewError)
                        ? "Preview を生成できませんでした。"
                        : $"Preview を生成できませんでした。{previewError}";
                    EditorGUILayout.HelpBox(message, MessageType.Info);
                }
            }

            if (!hasExpandedPreview)
            {
                EditorGUILayout.HelpBox("Preview Expanded をオンにすると、ここに Preview が表示されます。", MessageType.Info);
            }

            if (bindingsChanged)
            {
                GUI.changed = true;
            }
        }

        private void RequestPreviewRefresh(ImageProcessGraphRunner runner, bool forceRefresh)
        {
            if (previewRefreshQueued || runner == null || runner.Graph == null)
            {
                return;
            }

            if (!forceRefresh &&
                previewCache.Count > 0 &&
                EditorApplication.timeSinceStartup - lastPreviewRefreshTime < 0.5d)
            {
                return;
            }

            previewRefreshQueued = true;
            var editor = this;
            EditorApplication.delayCall += () =>
            {
                if (editor == null)
                {
                    return;
                }

                editor.previewRefreshQueued = false;
                var currentRunner = editor.target as ImageProcessGraphRunner;
                if (currentRunner == null)
                {
                    return;
                }

                editor.RefreshPreviewCache(currentRunner);
                editor.Repaint();
            };
        }

        private void RefreshPreviewCache(ImageProcessGraphRunner runner)
        {
            previewError = string.Empty;
            var result = default(ImageProcessExecutionResult);
            try
            {
                if (!runner.TryEvaluateGraph(out result, out var error))
                {
                    previewError = string.IsNullOrWhiteSpace(error) ? "Preview を生成できませんでした。" : error;
                    ClearPreviewCache();
                    return;
                }

                var activeOutputNames = new System.Collections.Generic.HashSet<string>();
                foreach (var node in runner.Graph.Nodes)
                {
                    if (node == null || node.nodeKind != ImageProcessNodeKind.Output)
                    {
                        continue;
                    }

                    var outputNodeName = string.IsNullOrWhiteSpace(node.displayName) ? "Output" : node.displayName;
                    if (!result.TryGetNodeOutputTexture(node.nodeId, out var texture) || texture == null)
                    {
                        continue;
                    }

                    CachePreviewTexture(outputNodeName, texture);
                    activeOutputNames.Add(outputNodeName);
                }

                TrimPreviewCache(activeOutputNames);
                if (activeOutputNames.Count == 0)
                {
                    previewError = "Output Preview が取得できませんでした。";
                }
            }
            catch (System.Exception ex)
            {
                previewError = ex.Message;
                ClearPreviewCache();
            }
            finally
            {
                lastPreviewRefreshTime = EditorApplication.timeSinceStartup;
                result?.Dispose();
            }
        }

        private void CachePreviewTexture(string outputNodeName, RenderTexture source)
        {
            if (string.IsNullOrWhiteSpace(outputNodeName) || source == null)
            {
                return;
            }

            if (!previewCache.TryGetValue(outputNodeName, out var cached) ||
                cached == null ||
                cached.width != source.width ||
                cached.height != source.height)
            {
                ReleasePreviewCacheTexture(outputNodeName);
                cached = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf)
                {
                    name = $"RunnerPreview_{outputNodeName}",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                cached.Create();
                previewCache[outputNodeName] = cached;
            }

            Graphics.Blit(source, cached);
        }

        private void TrimPreviewCache(System.Collections.Generic.HashSet<string> activeOutputNames)
        {
            var removeKeys = new System.Collections.Generic.List<string>();
            foreach (var key in previewCache.Keys)
            {
                if (!activeOutputNames.Contains(key))
                {
                    removeKeys.Add(key);
                }
            }

            foreach (var key in removeKeys)
            {
                ReleasePreviewCacheTexture(key);
            }
        }

        private void ClearPreviewCache()
        {
            if (RenderTexture.active != null)
            {
                RenderTexture.active = null;
            }

            var keys = new System.Collections.Generic.List<string>(previewCache.Keys);
            foreach (var key in keys)
            {
                ReleasePreviewCacheTexture(key);
            }
        }

        private void ReleasePreviewCacheTexture(string outputNodeName)
        {
            if (!previewCache.TryGetValue(outputNodeName, out var texture) || texture == null)
            {
                previewCache.Remove(outputNodeName);
                return;
            }

            if (RenderTexture.active != null)
            {
                RenderTexture.active = null;
            }

            texture.Release();
            Object.DestroyImmediate(texture);
            previewCache.Remove(outputNodeName);
        }

        private static void DrawAlphaPreview(Rect rect, Texture texture)
        {
            EditorGUI.DrawTextureTransparent(rect, Texture2D.grayTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        }

        private void DrawParameterOverrides()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Parameter Overrides", EditorStyles.boldLabel);

            if (graphProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("GraphAsset を設定すると Parameter 一覧が表示されます。", MessageType.Info);
                return;
            }

            if (parameterOverrideBindingsProperty == null || parameterOverrideBindingsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("GraphAsset に Parameter がありません。", MessageType.Info);
                return;
            }

            for (var i = 0; i < parameterOverrideBindingsProperty.arraySize; i++)
            {
                var element = parameterOverrideBindingsProperty.GetArrayElementAtIndex(i);
                var parameterNameProperty = element.FindPropertyRelative("parameterName");
                var parameterTypeProperty = element.FindPropertyRelative("parameterType");
                var overrideEnabledProperty = element.FindPropertyRelative("overrideEnabled");
                var textureValueProperty = element.FindPropertyRelative("textureValue");
                var floatValueProperty = element.FindPropertyRelative("floatValue");
                var vectorValueProperty = element.FindPropertyRelative("vectorValue");
                var colorValueProperty = element.FindPropertyRelative("colorValue");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(
                        overrideEnabledProperty,
                        new GUIContent(string.IsNullOrWhiteSpace(parameterNameProperty.stringValue) ? $"Parameter {i + 1}" : parameterNameProperty.stringValue));

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.EnumPopup("Type", (ImageProcessPortType)parameterTypeProperty.enumValueIndex);
                    }

                    using (new EditorGUI.DisabledScope(!overrideEnabledProperty.boolValue))
                    {
                        switch ((ImageProcessPortType)parameterTypeProperty.enumValueIndex)
                        {
                            case ImageProcessPortType.Texture:
                                EditorGUILayout.PropertyField(textureValueProperty, new GUIContent("Value"));
                                break;

                            case ImageProcessPortType.Float:
                                EditorGUILayout.PropertyField(floatValueProperty, new GUIContent("Value"));
                                break;

                            case ImageProcessPortType.Vector4:
                                EditorGUILayout.PropertyField(vectorValueProperty, new GUIContent("Value"));
                                break;

                            case ImageProcessPortType.Color:
                                colorValueProperty.colorValue = EditorGUILayout.ColorField(
                                    new GUIContent("Value"),
                                    colorValueProperty.colorValue,
                                    true,
                                    true,
                                    true);
                                break;
                        }
                    }
                }
            }
        }
    }
}
