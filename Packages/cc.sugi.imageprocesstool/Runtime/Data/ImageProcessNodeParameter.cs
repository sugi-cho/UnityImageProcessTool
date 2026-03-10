using System;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    [Serializable]
    public sealed class ImageProcessNodeParameter
    {
        public string parameterName;
        public ImageProcessPortType parameterType;
        public float floatValue;
        public bool useRange;
        public float rangeMin;
        public float rangeMax = 1f;
        public Vector4 vectorValue;
        public Color colorValue = Color.white;

        public ImageProcessNodeParameter(string parameterName, ImageProcessPortType parameterType)
        {
            this.parameterName = parameterName;
            this.parameterType = parameterType;
        }
    }
}
