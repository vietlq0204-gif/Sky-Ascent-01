using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Heroicsolo.MobileBuildOptimizer
{
    public class MobileBuildOptimizer : EditorWindow
    {
        private const long ThresholdAudioSizeInBytes = 200 * 1024; // 200 KB in bytes
        private const long MaxAudioSizeInBytes = 2048 * 1024; // 2 MB in bytes

        private float _optimizationProgress = 0f;
        private string _optimizationType = "";
        private string _imagesSizeStrBefore = "";
        private string _imagesSizeStrAfter = "";
        private int _selectedImageResolutionIdx = 1;

        private readonly List<int> _imageResolutions = new()
        {
            512,
            1024,
            2048,
            4096
        };

        [MenuItem("Tools/Mobile Build Optimizer", false, 21)]
        public static void ShowWindow()
        {
            GetWindow<MobileBuildOptimizer>("Mobile Build Optimizer");
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_imagesSizeStrBefore))
            {
                _imagesSizeStrBefore = ImagesSizeCalculator.CalculateImageSizes();
            }

            GUILayout.Label($"Images size (original): {_imagesSizeStrBefore}");

            if (!string.IsNullOrEmpty(_imagesSizeStrAfter))
            {
                GUILayout.Label($"Images size (optimized): {_imagesSizeStrAfter}");
            }
            
            GUILayout.Label("Image Resolution Limit:");
            _selectedImageResolutionIdx = GUILayout.SelectionGrid(_selectedImageResolutionIdx, new []{"512px", "1024px", "2048px", "4096px"}, 4);

            if (GUILayout.Button("Optimize Textures"))
            {
                OptimizeTextures(_imageResolutions[_selectedImageResolutionIdx]);
            }

            if (GUILayout.Button("Optimize Models"))
            {
                OptimizeModels();
            }

            if (GUILayout.Button("Optimize Audio"))
            {
                OptimizeAudio();
            }

            if (GUILayout.Button("Optimize Project Settings"))
            {
                _optimizationType = "Settings";
                OptimizeSettings();
                _optimizationProgress = 1f;
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Optimize All"))
            {
                OptimizeAll();
            }

            if (_optimizationProgress > 0f)
            {
                GUILayout.Space(20);

                if (_optimizationProgress < 1f)
                {
                    GUILayout.Label($"Optimizing {_optimizationType}: [{Mathf.CeilToInt(_optimizationProgress * 100f)}%]");
                }
                else
                {
                    GUILayout.Label($"Optimizing {_optimizationType}: DONE");
                }
            }
        }

        private void OptimizeTextures(int imageSize = 1024)
        {
            _optimizationProgress = 0f;
            _optimizationType = "Textures";

            // Get all texture assets in the project
            var textureGUIDs = AssetDatabase.FindAssets("t:Texture", new string[] { "Assets" });

            var idx = 0;

            foreach (var guid in textureGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    
                if (textureImporter != null)
                {
                    var fileBytes = File.ReadAllBytes(assetPath);
                    var tempTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tempTexture.LoadImage(fileBytes, markNonReadable: true);
                    var rawSize = Mathf.Max(tempTexture.width, tempTexture.height);
                    DestroyImmediate(tempTexture);
                    
                    // Ensure the texture is readable before processing
                    textureImporter.isReadable = true;
                    textureImporter.mipmapEnabled = true;
                    textureImporter.maxTextureSize = Mathf.Min(rawSize, imageSize);

                    // Enable crunch compression
                    textureImporter.textureCompression = TextureImporterCompression.Compressed;
                    textureImporter.crunchedCompression = true;
                    textureImporter.compressionQuality = 100;

                    // Reimport the texture to apply changes
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                    Debug.Log($"Processed texture: {assetPath}");
                }
                else
                {
                    Debug.LogWarning($"Could not process texture at {assetPath}, not a valid texture importer.");
                }

                idx++;
                _optimizationProgress = (float)idx / textureGUIDs.Length;
                Repaint();
            }

            // Refresh Asset Database to show changes
            AssetDatabase.Refresh();
            Debug.Log("Texture processing complete.");

            _imagesSizeStrAfter = ImagesSizeCalculator.CalculateImageSizes();
        }

        private void OptimizeModels()
        {
            _optimizationProgress = 0f;
            _optimizationType = "Models";

            // Get all model assets in the project
            var modelGUIDs = AssetDatabase.FindAssets("t:Model", new string[] { "Assets" });

            var idx = 0;

            foreach (var guid in modelGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;

                if (modelImporter != null)
                {
                    // Disable Tangents
                    modelImporter.importTangents = ModelImporterTangents.None;

                    // Enable Mesh Compression
                    modelImporter.meshCompression = ModelImporterMeshCompression.High;

                    // Reimport the model to apply changes
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                    Debug.Log($"Processed model: {assetPath}");
                }
                else
                {
                    Debug.LogWarning($"Could not process model at {assetPath}, not a valid model importer.");
                }

                idx++;
                _optimizationProgress = (float)idx / modelGUIDs.Length;
                Repaint();
            }

            // Refresh Asset Database to show changes
            AssetDatabase.Refresh();
            Debug.Log("Model processing complete.");
        }

        private void OptimizeAudio()
        {
            _optimizationProgress = 0f;
            _optimizationType = "Audio";

            // Get all audio assets in the project
            var audioGUIDs = AssetDatabase.FindAssets("t:AudioClip", new string[] { "Assets" });

            var idx = 0;

            foreach (var guid in audioGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var audioImporter = AssetImporter.GetAtPath(assetPath) as AudioImporter;

                if (audioImporter != null)
                {
                    // Check the size of the audio file
                    var fileInfo = new FileInfo(assetPath);
                    var fileSizeInBytes = fileInfo.Length;

                    var sampleSettings = audioImporter.defaultSampleSettings;

                    // Set Load Type based on the file size
                    if (fileSizeInBytes < ThresholdAudioSizeInBytes)
                    {
                        // Set to "Decompress on Load"
                        sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                    }
                    else if (fileSizeInBytes < MaxAudioSizeInBytes)
                    {
                        // Set to "Compressed in Memory"
                        sampleSettings.loadType = AudioClipLoadType.CompressedInMemory;
                    }
                    else
                    {
                        // Set to "Streaming"
                        sampleSettings.loadType = AudioClipLoadType.Streaming;
                    }

                    audioImporter.defaultSampleSettings = sampleSettings;

                    // Reimport the audio asset to apply changes
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                    Debug.Log($"Processed audio asset: {assetPath} (Size: {fileSizeInBytes / 1024f} KB)");
                }
                else
                {
                    Debug.LogWarning($"Could not process audio asset at {assetPath}, not a valid audio importer.");
                }

                idx++;
                _optimizationProgress = (float)idx / audioGUIDs.Length;
                Repaint();
            }

            // Refresh the Asset Database to reflect changes
            AssetDatabase.Refresh();
            Debug.Log("Audio asset processing complete.");
        }

        private void OptimizeSettings()
        {
            PlayerSettings.Android.minifyRelease = true;
            PlayerSettings.bakeCollisionMeshes = true;
            SetIL2CPP();
        }

        private void SetIL2CPP()
        {
            // Make sure we are running this in an active build target (e.g., Standalone, Android, iOS)
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows ||
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX ||
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
            {
                // Set the scripting backend to IL2CPP
                PlayerSettings.SetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup, ScriptingImplementation.IL2CPP);

                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                {
                    // Set the Android target architecture (you can customize this for other platforms)
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;
                }

                // Log the changes
                Debug.Log("IL2CPP mode has been set for the current build target: " + EditorUserBuildSettings.activeBuildTarget);
            }
            else
            {
                Debug.LogWarning("IL2CPP can only be set for valid build targets (Standalone, Android, iOS).");
            }
        }

        private void OptimizeAll()
        {
            OptimizeTextures(_imageResolutions[_selectedImageResolutionIdx]);
            OptimizeModels();
            OptimizeAudio();
            OptimizeSettings();
        }
    }
}