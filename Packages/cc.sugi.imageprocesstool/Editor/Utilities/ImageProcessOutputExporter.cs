using System.IO;
using UnityEditor;
using UnityEngine;

namespace sugi.cc.ImageProcessTool.Editor
{
    public enum ImageProcessExportFormat
    {
        Png = 0,
        Exr = 1
    }

    public static class ImageProcessOutputExporter
    {
        public static bool TryExport(RenderTexture source, string outputPath, ImageProcessExportFormat format, out string error)
        {
            error = string.Empty;
            if (source == null)
            {
                error = "Source texture is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                error = "Output path is empty.";
                return false;
            }

            var previous = RenderTexture.active;
            Texture2D readback = null;
            try
            {
                RenderTexture.active = source;
                var textureFormat = format == ImageProcessExportFormat.Exr ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;
                readback = new Texture2D(source.width, source.height, textureFormat, false, true);
                readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readback.Apply(false, false);

                byte[] bytes;
                if (format == ImageProcessExportFormat.Exr)
                {
                    bytes = readback.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
                }
                else
                {
                    bytes = readback.EncodeToPNG();
                }

                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(outputPath, bytes);
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (readback != null)
                {
                    Object.DestroyImmediate(readback);
                }
            }
        }

        public static bool TryExportAsAsset(RenderTexture source, string assetPath, out string error)
        {
            error = string.Empty;
            if (source == null)
            {
                error = "Source texture is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "Asset path is empty.";
                return false;
            }

            if (!assetPath.StartsWith("Assets/"))
            {
                error = "Asset path must start with Assets/.";
                return false;
            }

            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                var texture = new Texture2D(source.width, source.height, TextureFormat.RGBAFloat, false, true);
                texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                texture.Apply(false, false);
                texture.name = Path.GetFileNameWithoutExtension(assetPath);

                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (existing != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                AssetDatabase.CreateAsset(texture, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }
    }
}
