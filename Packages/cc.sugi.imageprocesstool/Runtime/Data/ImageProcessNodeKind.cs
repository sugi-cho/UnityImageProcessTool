using System;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    [Serializable]
    public enum ImageProcessNodeKind
    {
        // Keep numeric values stable for asset serialization.
        ShaderOperator = 0,
        Output = 1,
        Parameter = 2,
        BlurOperator = 3,
        IterativeFilterOperator = 4
    }
}
