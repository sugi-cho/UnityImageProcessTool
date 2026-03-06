using UnityEditor;
using UnityEngine.UIElements;

namespace SugiCho.ImageProcessTool.Editor
{
    public sealed class ImageProcessToolWindow : EditorWindow
    {
        [MenuItem("Tools/SugiCho/Image Process Tool")]
        public static void Open()
        {
            var window = GetWindow<ImageProcessToolWindow>();
            window.titleContent = new UnityEngine.GUIContent("Image Process Tool");
            window.minSize = new UnityEngine.Vector2(900f, 600f);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            var header = new Label("SugiCho Image Process Tool");
            header.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            header.style.fontSize = 16;
            header.style.marginBottom = 8;

            var description = new HelpBox(
                "Package scaffold is ready. Next step is implementing graph editing and execution pipeline.",
                HelpBoxMessageType.Info);

            rootVisualElement.Add(header);
            rootVisualElement.Add(description);
        }
    }
}
