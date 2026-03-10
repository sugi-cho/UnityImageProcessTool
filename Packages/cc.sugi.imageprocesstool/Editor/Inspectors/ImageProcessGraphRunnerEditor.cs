using UnityEditor;
using UnityEngine;

namespace sugi.cc.ImageProcessTool.Editor
{
    [CustomEditor(typeof(ImageProcessGraphRunner))]
    internal sealed class ImageProcessGraphRunnerEditor : UnityEditor.Editor
    {
        private SerializedProperty graphProperty;
        private SerializedProperty outputDestinationsProperty;
        private SerializedProperty parameterOverrideBindingsProperty;
        private SerializedProperty updateTimingProperty;
        private SerializedProperty executeOnEnableProperty;
        private SerializedProperty executeInEditModeProperty;
        private SerializedProperty logErrorsProperty;

        private void OnEnable()
        {
            graphProperty = serializedObject.FindProperty("graph");
            outputDestinationsProperty = serializedObject.FindProperty("outputDestinations");
            parameterOverrideBindingsProperty = serializedObject.FindProperty("parameterOverrideBindings");
            updateTimingProperty = serializedObject.FindProperty("updateTiming");
            executeOnEnableProperty = serializedObject.FindProperty("executeOnEnable");
            executeInEditModeProperty = serializedObject.FindProperty("executeInEditMode");
            logErrorsProperty = serializedObject.FindProperty("logErrors");
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
            DrawOutputDestinations();

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

        private void DrawOutputDestinations()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output Destinations", EditorStyles.boldLabel);

            if (graphProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("GraphAsset を設定すると Output 一覧が表示されます。", MessageType.Info);
                return;
            }

            if (outputDestinationsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("GraphAsset に Output ノードがありません。", MessageType.Info);
                return;
            }

            for (var i = 0; i < outputDestinationsProperty.arraySize; i++)
            {
                var element = outputDestinationsProperty.GetArrayElementAtIndex(i);
                var outputNodeNameProperty = element.FindPropertyRelative("outputNodeName");
                var destinationProperty = element.FindPropertyRelative("destination");
                var previewExpandedProperty = element.FindPropertyRelative("previewExpanded");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var label = string.IsNullOrWhiteSpace(outputNodeNameProperty.stringValue)
                        ? $"Output {i + 1}"
                        : outputNodeNameProperty.stringValue;
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(destinationProperty, new GUIContent("Destination"));

                    var destination = destinationProperty.objectReferenceValue as RenderTexture;
                    previewExpandedProperty.boolValue = EditorGUILayout.Foldout(
                        previewExpandedProperty.boolValue,
                        destination == null ? "Preview (No Output)" : $"Preview ({destination.width}x{destination.height})",
                        true);

                    if (!previewExpandedProperty.boolValue || destination == null)
                    {
                        continue;
                    }

                    var previewRect = GUILayoutUtility.GetRect(1f, 140f, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawPreviewTexture(previewRect, destination, null, ScaleMode.ScaleToFit);
                }
            }
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
