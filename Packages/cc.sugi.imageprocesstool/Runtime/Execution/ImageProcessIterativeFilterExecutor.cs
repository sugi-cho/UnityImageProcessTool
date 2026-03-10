using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    public static class ImageProcessIterativeFilterExecutor
    {
        public static bool TryExecute(
            RenderTexture source,
            ImageProcessIterativeFilterSettings settings,
            out RenderTexture output,
            out string error)
        {
            output = null;

            if (source == null)
            {
                error = "Source texture is null.";
                return false;
            }

            if (settings == null)
            {
                error = "Iterative filter settings are null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.shaderName))
            {
                error = "Iterative filter shader name is empty.";
                return false;
            }

            if (settings.passes.Count == 0)
            {
                error = "Iterative filter has no passes.";
                return false;
            }

            var shader = Shader.Find(settings.shaderName);
            if (shader == null)
            {
                error = $"Iterative filter shader not found: {settings.shaderName}";
                return false;
            }

            var workingWidth = Mathf.Max(1, source.width >> Mathf.Clamp(settings.downsample, 0, 4));
            var workingHeight = Mathf.Max(1, source.height >> Mathf.Clamp(settings.downsample, 0, 4));
            var current = CreateOutputTexture(workingWidth, workingHeight);
            RenderTexture next = null;
            var material = new Material(shader);

            try
            {
                foreach (var parameter in settings.floatParameters)
                {
                    material.SetFloat(parameter.Name, parameter.Value);
                }

                Graphics.Blit(source, current);
                next = CreateOutputTexture(workingWidth, workingHeight);

                var iterations = Mathf.Max(1, settings.iterations);
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    foreach (var pass in settings.passes)
                    {
                        var offset = BuildOffset(pass, iteration, workingWidth, workingHeight);
                        material.SetVector(pass.VectorPropertyName, new Vector4(offset.x, offset.y, 0f, 0f));
                        Graphics.Blit(current, next, material, pass.ShaderPass);
                        Swap(ref current, ref next);
                    }
                }

                output = CreateOutputTexture(source.width, source.height);
                Graphics.Blit(current, output);
                error = string.Empty;
                return true;
            }
            finally
            {
                ReleaseTexture(current);
                ReleaseTexture(next);
#if UNITY_EDITOR
                Object.DestroyImmediate(material);
#else
                Object.Destroy(material);
#endif
            }
        }

        private static Vector2 BuildOffset(ImageProcessIterativeFilterPass pass, int iteration, int width, int height)
        {
            var radius = pass.BaseRadius;
            if (pass.ScaleWithIteration)
            {
                radius *= iteration + 1;
            }

            radius += pass.IterationOffset * iteration;
            return new Vector2(
                pass.Direction.x * radius / width,
                pass.Direction.y * radius / height);
        }

        private static RenderTexture CreateOutputTexture(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            var rt = new RenderTexture(descriptor)
            {
                name = $"ImageProcessRT_{width}x{height}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            if (RenderTexture.active == texture)
            {
                RenderTexture.active = null;
            }

            texture.Release();
#if UNITY_EDITOR
            Object.DestroyImmediate(texture);
#else
            Object.Destroy(texture);
#endif
        }

        private static void Swap(ref RenderTexture lhs, ref RenderTexture rhs)
        {
            (lhs, rhs) = (rhs, lhs);
        }
    }
}
