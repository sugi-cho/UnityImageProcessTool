using System;
using UnityEngine;

namespace SugiCho.ImageProcessTool
{
    [Serializable]
    public enum ImageProcessNodeKind
    {
        Source = 0,
        ShaderOperator = 1,
        ChannelSplit = 2,
        ChannelCombine = 3,
        Output = 4
    }
}
