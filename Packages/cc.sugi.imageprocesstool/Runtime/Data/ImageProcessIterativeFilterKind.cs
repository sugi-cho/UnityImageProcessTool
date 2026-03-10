using System;

namespace sugi.cc.ImageProcessTool
{
    [Serializable]
    public enum ImageProcessIterativeFilterKind
    {
        Blur = 0,
        Dilate = 1,
        Erode = 2
    }
}
