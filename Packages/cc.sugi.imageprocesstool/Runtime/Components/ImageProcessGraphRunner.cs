using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    public enum ImageProcessRunnerUpdateTiming
    {
        Update,
        LateUpdate,
        Manual
    }

    [DisallowMultipleComponent]
    public sealed class ImageProcessGraphRunner : MonoBehaviour
    {
        [SerializeField] private ImageProcessGraphAsset graph;
        [SerializeField] private RenderTexture destination;
        [SerializeField] private ImageProcessRunnerUpdateTiming updateTiming = ImageProcessRunnerUpdateTiming.Update;
        [SerializeField] private bool executeOnEnable = true;
        [SerializeField] private bool logErrors = true;

        private string lastError = string.Empty;

        public ImageProcessGraphAsset Graph
        {
            get => graph;
            set => graph = value;
        }

        public RenderTexture Destination
        {
            get => destination;
            set => destination = value;
        }

        public ImageProcessRunnerUpdateTiming UpdateTiming
        {
            get => updateTiming;
            set => updateTiming = value;
        }

        private void OnEnable()
        {
            if (!executeOnEnable)
            {
                return;
            }

            ExecuteAndLogIfNeeded();
        }

        private void Update()
        {
            if (updateTiming != ImageProcessRunnerUpdateTiming.Update)
            {
                return;
            }

            ExecuteAndLogIfNeeded();
        }

        private void LateUpdate()
        {
            if (updateTiming != ImageProcessRunnerUpdateTiming.LateUpdate)
            {
                return;
            }

            ExecuteAndLogIfNeeded();
        }

        [ContextMenu("Run Once")]
        public void RunOnce()
        {
            ExecuteAndLogIfNeeded();
        }

        public bool TryRunOnce(out string error)
        {
            if (graph == null)
            {
                error = "ImageProcessGraphAsset is not assigned.";
                return false;
            }

            if (destination != null)
            {
                return ImageProcessGraphExecutor.TryExecuteToRenderTexture(graph, destination, out error);
            }

            if (!ImageProcessGraphExecutor.TryExecute(graph, out var result, out error))
            {
                return false;
            }

            result.Dispose();
            return true;
        }

        private void ExecuteAndLogIfNeeded()
        {
            if (TryRunOnce(out var error))
            {
                lastError = string.Empty;
                return;
            }

            if (!logErrors || string.IsNullOrWhiteSpace(error) || error == lastError)
            {
                return;
            }

            lastError = error;
            Debug.LogWarning($"ImageProcessGraphRunner on '{name}' failed: {error}", this);
        }
    }
}
