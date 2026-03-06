using UnityEngine;

namespace SugiCho.ImageProcessTool
{
    [CreateAssetMenu(menuName = "SugiCho/Image Process Graph", fileName = "ImageProcessGraph")]
    public sealed class ImageProcessGraphAsset : ScriptableObject
    {
        [SerializeField] private string graphVersion = "0.1.0";

        public string GraphVersion => graphVersion;
    }
}
