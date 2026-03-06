using System;

namespace sugi.cc.ImageProcessTool
{
    [Serializable]
    public sealed class ImageProcessPortDefinition
    {
        public string portId;
        public string displayName;
        public ImageProcessPortType portType;
        public ImageProcessPortDirection direction;
        public bool optional;

        public ImageProcessPortDefinition(string portId, string displayName, ImageProcessPortType portType, ImageProcessPortDirection direction, bool optional)
        {
            this.portId = portId;
            this.displayName = displayName;
            this.portType = portType;
            this.direction = direction;
            this.optional = optional;
        }
    }
}

