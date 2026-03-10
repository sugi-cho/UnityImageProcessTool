using System;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    [Serializable]
    public sealed class ImageProcessGraphParameter
    {
        public string parameterId;
        public string parameterName;
        public ImageProcessPortType parameterType;
        public Texture textureValue;
        public float floatValue;
        public Vector4 vectorValue;
        public Color colorValue = Color.white;

        public ImageProcessGraphParameter()
        {
        }

        public ImageProcessGraphParameter(string parameterId, string parameterName, ImageProcessPortType parameterType)
        {
            this.parameterId = parameterId;
            this.parameterName = parameterName;
            this.parameterType = parameterType;
        }
    }
}
