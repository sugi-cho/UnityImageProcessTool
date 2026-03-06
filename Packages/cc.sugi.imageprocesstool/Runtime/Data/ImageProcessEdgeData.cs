using System;

namespace SugiCho.ImageProcessTool
{
    [Serializable]
    public sealed class ImageProcessEdgeData
    {
        public string outputNodeId;
        public string outputPortId;
        public string inputNodeId;
        public string inputPortId;

        public ImageProcessEdgeData(string outputNodeId, string outputPortId, string inputNodeId, string inputPortId)
        {
            this.outputNodeId = outputNodeId;
            this.outputPortId = outputPortId;
            this.inputNodeId = inputNodeId;
            this.inputPortId = inputPortId;
        }
    }
}
