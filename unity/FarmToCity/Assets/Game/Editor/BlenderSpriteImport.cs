using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Import settings for the Blender renders (tools/blender/render.py): rendered at 4x and shown smaller, so mipmaps with
    /// trilinear filtering keep edges smooth; uncompressed to keep the toon bands and outlines clean.
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
        }
    }
}
