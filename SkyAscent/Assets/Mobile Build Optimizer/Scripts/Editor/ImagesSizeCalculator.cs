using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Heroicsolo.MobileBuildOptimizer
{
    public static class ImagesSizeCalculator
    {
        public static string CalculateImageSizes()
        {
            long totalSizeBytes = 0;

            // Get all texture assets in the project
            string[] texturePaths = AssetDatabase.FindAssets("t:Texture")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();

            foreach (string texturePath in texturePaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture != null)
                    {
                        totalSizeBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                    }
                }
            }

            return FormatBytes(totalSizeBytes);
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}