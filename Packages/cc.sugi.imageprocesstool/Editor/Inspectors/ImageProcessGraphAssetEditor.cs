using UnityEditor;
using UnityEngine;

namespace sugi.cc.ImageProcessTool.Editor
{
    [CustomEditor(typeof(ImageProcessGraphAsset))]
    internal sealed class ImageProcessGraphAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // GraphAsset is edited only through the dedicated editor window.
        }
    }
}
