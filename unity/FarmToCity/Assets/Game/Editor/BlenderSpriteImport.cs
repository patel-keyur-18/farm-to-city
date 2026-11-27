using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Import settings for the Blender renders (tools/blender/render.py, render_real.py): rendered at 4x (the realistic redesign at 8x) and shown
    /// smaller, so mipmaps with trilinear filtering keep edges smooth. Uncompressed in the Editor; on iOS every render is ASTC 4x4 (8 bits a pixel, spec 032 §3.2),
    /// which keeps the redesign's 8x sprites at about today's memory.
    /// </summary>
    public sealed class BlenderSpriteImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/Blender/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "iPhone", overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_4x4,
                textureCompression = TextureImporterCompression.Compressed, compressionQuality = 100,
            });
        }

        /// <summary>Raised when the import settings change, so Unity re-imports every render with them.</summary>
        public override uint GetVersion() { return 2; }
    }
}
