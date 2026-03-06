using System;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
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

