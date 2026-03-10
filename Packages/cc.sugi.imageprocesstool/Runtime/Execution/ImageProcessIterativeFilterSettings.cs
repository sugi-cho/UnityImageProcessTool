using System.Collections.Generic;
using UnityEngine;

namespace sugi.cc.ImageProcessTool
{
    public sealed class ImageProcessIterativeFilterSettings
    {
        public string shaderName;
        public int iterations = 1;
        public int downsample;
        public readonly List<ImageProcessIterativeFilterFloatParameter> floatParameters = new();
        public readonly List<ImageProcessIterativeFilterPass> passes = new();

        public static ImageProcessIterativeFilterSettings CreateBlur(
            ImageProcessBlurMode blurMode,
            int iterations,
            float radius,
            int downsample)
        {
            var settings = new ImageProcessIterativeFilterSettings
            {
                shaderName = "Hidden/sugi.cc/ImageProcessTool/IterativeBlur",
                iterations = Mathf.Max(1, iterations),
                downsample = Mathf.Clamp(downsample, 0, 4)
            };

            switch (blurMode)
            {
                case ImageProcessBlurMode.Box:
                    settings.floatParameters.Add(new ImageProcessIterativeFilterFloatParameter("_BlurMode", 0f));
                    settings.passes.Add(new ImageProcessIterativeFilterPass(0, new Vector2(1f, 0f), radius, scaleWithIteration: true));
                    settings.passes.Add(new ImageProcessIterativeFilterPass(0, new Vector2(0f, 1f), radius, scaleWithIteration: true));
                    break;

                case ImageProcessBlurMode.Gaussian:
                    settings.floatParameters.Add(new ImageProcessIterativeFilterFloatParameter("_BlurMode", 1f));
                    settings.passes.Add(new ImageProcessIterativeFilterPass(0, new Vector2(1f, 0f), radius, scaleWithIteration: true));
                    settings.passes.Add(new ImageProcessIterativeFilterPass(0, new Vector2(0f, 1f), radius, scaleWithIteration: true));
                    break;

                case ImageProcessBlurMode.Kawase:
                    settings.passes.Add(new ImageProcessIterativeFilterPass(1, new Vector2(1f, 1f), radius, iterationOffset: 1f));
                    break;
            }

            return settings;
        }

        public static ImageProcessIterativeFilterSettings CreateDilate(
            int iterations,
            float radius,
            int downsample)
        {
            return CreateMorphology(2, iterations, radius, downsample);
        }

        public static ImageProcessIterativeFilterSettings CreateErode(
            int iterations,
            float radius,
            int downsample)
        {
            return CreateMorphology(3, iterations, radius, downsample);
        }

        private static ImageProcessIterativeFilterSettings CreateMorphology(
            int shaderPass,
            int iterations,
            float radius,
            int downsample)
        {
            var settings = new ImageProcessIterativeFilterSettings
            {
                shaderName = "Hidden/sugi.cc/ImageProcessTool/IterativeBlur",
                iterations = Mathf.Max(1, iterations),
                downsample = Mathf.Clamp(downsample, 0, 4)
            };
            settings.passes.Add(new ImageProcessIterativeFilterPass(shaderPass, new Vector2(1f, 1f), radius, scaleWithIteration: false, iterationOffset: 0.5f));
            return settings;
        }
    }

    public readonly struct ImageProcessIterativeFilterFloatParameter
    {
        public readonly string Name;
        public readonly float Value;

        public ImageProcessIterativeFilterFloatParameter(string name, float value)
        {
            Name = name;
            Value = value;
        }
    }

    public readonly struct ImageProcessIterativeFilterPass
    {
        public readonly int ShaderPass;
        public readonly Vector2 Direction;
        public readonly float BaseRadius;
        public readonly bool ScaleWithIteration;
        public readonly float IterationOffset;
        public readonly string VectorPropertyName;

        public ImageProcessIterativeFilterPass(
            int shaderPass,
            Vector2 direction,
            float baseRadius,
            bool scaleWithIteration = false,
            float iterationOffset = 0f,
            string vectorPropertyName = "_BlurOffset")
        {
            ShaderPass = shaderPass;
            Direction = direction;
            BaseRadius = Mathf.Max(0.001f, baseRadius);
            ScaleWithIteration = scaleWithIteration;
            IterationOffset = iterationOffset;
            VectorPropertyName = vectorPropertyName;
        }
    }
}
