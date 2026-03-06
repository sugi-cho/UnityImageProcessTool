using System;
using UnityEngine;

namespace SugiCho.ImageProcessTool
{
    [Serializable]
    public sealed class ImageProcessNodeParameter
    {
        public string parameterName;
        public ImageProcessPortType parameterType;
        public float floatValue;
        public Vector4 vectorValue;
        public Color colorValue = Color.white;

        public ImageProcessNodeParameter(string parameterName, ImageProcessPortType parameterType)
        {
            this.parameterName = parameterName;
            this.parameterType = parameterType;
        }
    }
}
