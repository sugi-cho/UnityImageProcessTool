using System;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    public sealed class ImageProcessValue : IDisposable
    {
        public ImageProcessPortType ValueType { get; }
        public RenderTexture TextureValue { get; }
        public float FloatValue { get; }
        public Vector4 VectorValue { get; }
        public Color ColorValue { get; }

        private ImageProcessValue(ImageProcessPortType valueType, RenderTexture textureValue, float floatValue, Vector4 vectorValue, Color colorValue)
        {
            ValueType = valueType;
            TextureValue = textureValue;
            FloatValue = floatValue;
            VectorValue = vectorValue;
            ColorValue = colorValue;
        }

        public static ImageProcessValue FromTexture(RenderTexture texture)
        {
            return new ImageProcessValue(ImageProcessPortType.Texture, texture, default, default, default);
        }

        public static ImageProcessValue FromFloat(float value)
        {
            return new ImageProcessValue(ImageProcessPortType.Float, null, value, default, default);
        }

        public static ImageProcessValue FromVector(Vector4 value)
        {
            return new ImageProcessValue(ImageProcessPortType.Vector4, null, default, value, default);
        }

        public static ImageProcessValue FromColor(Color value)
        {
            return new ImageProcessValue(ImageProcessPortType.Color, null, default, default, value);
        }

        public bool TryGetTexture(out RenderTexture texture)
        {
            texture = TextureValue;
            return ValueType == ImageProcessPortType.Texture && texture != null;
        }

        public void Dispose()
        {
            if (TextureValue == null)
            {
                return;
            }

            if (RenderTexture.active == TextureValue)
            {
                RenderTexture.active = null;
            }

            TextureValue.Release();
#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(TextureValue);
#else
            UnityEngine.Object.Destroy(TextureValue);
#endif
        }
    }
}
