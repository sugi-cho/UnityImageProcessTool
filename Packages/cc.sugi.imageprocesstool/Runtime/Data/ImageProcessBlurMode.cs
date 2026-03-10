using System;

namespace sugi.cc.ImageProcessTool
{
    [Serializable]
    public enum ImageProcessBlurMode
    {
        Box = 0,
        Gaussian = 1,
        Kawase = 2
    }
}
