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
            bool tile = IsTile(System.IO.Path.GetFileNameWithoutExtension(assetPath));
            importer.textureType = tile ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            if (tile) SetTileSprite(importer);
            int max = System.IO.Path.GetFileName(assetPath).StartsWith("hill_", System.StringComparison.Ordinal) ? 4096 : 2048; // hill_2 is 2,318 px wide at 8x (spec 032 §3.2)
            importer.maxTextureSize = max;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "iPhone", overridden = true, maxTextureSize = max, format = TextureImporterFormat.ASTC_4x4,
                textureCompression = TextureImporterCompression.Compressed, compressionQuality = 100,
            });
        }

        /// <summary>The ground, its edges and the hills are whole tiles (US-273, ADR 0184); the same rule is in SpriteBank.</summary>
        public static bool IsTile(string name) => name.StartsWith("ground_", System.StringComparison.Ordinal) || name.StartsWith("edge_", System.StringComparison.Ordinal) || name.StartsWith("hill_", System.StringComparison.Ordinal);

        /// <summary>
        /// A tile is imported as a sprite with a coarse mesh: Unity's default tight mesh follows the staircase edge of an 8x render with about 190 triangles
        /// a tile where a hexagon of 4 will do. The pivot and scale come from the render manifest (sprites.json), which the map's sprites already follow.
        /// </summary>
        private void SetTileSprite(TextureImporter importer)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var manifest = UnityEngine.JsonUtility.FromJson<Manifest>(System.IO.File.ReadAllText("Assets/Resources/Art/Blender/sprites.json"));
            Info info = null;
            foreach (Info i in manifest.sprites) if (i.name == name) { info = i; break; }
            if (info == null) return;
            importer.spriteImportMode = SpriteImportMode.Single;
            var st = new TextureImporterSettings();
            importer.ReadTextureSettings(st);
            st.spriteMeshType = UnityEngine.SpriteMeshType.Tight;
            st.spriteExtrude = 0;
            st.spriteTessellationDetail = TileMeshDetail;
            st.spriteAlignment = (int)UnityEngine.SpriteAlignment.Custom;
            st.spritePivot = new UnityEngine.Vector2(info.pivotX, info.pivotY);
            st.spritePixelsPerUnit = info.ppu > 0f ? info.ppu : manifest.pixelsPerUnit;
            importer.SetTextureSettings(st);
        }

        /// <summary>Mesh detail of a tile's sprite, 0 (coarsest) to 1.</summary>
        public const float TileMeshDetail = 0f;

        [System.Serializable] private class Info { public string name; public float pivotX, pivotY, ppu; }
        [System.Serializable] private class Manifest { public float pixelsPerUnit; public Info[] sprites; }

        /// <summary>Raised when the import settings change, so Unity re-imports every render with them.</summary>
        public override uint GetVersion() { return 4; }
    }
}
