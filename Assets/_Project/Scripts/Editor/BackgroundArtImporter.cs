using UnityEditor;
using UnityEngine;

namespace Roygbiv.EditorTools
{
    /// <summary>
    /// Every image in Art/Background imports the way Background.prefab needs it, even if the files (or their .meta)
    /// get replaced: a Single, Full Rect sprite (Tiled draw mode needs Full Rect), pivot in the center, ArtHeight
    /// world units tall whatever its resolution (so far_*.png at 1080p and mid_*.png at 4K line up).
    /// </summary>
    public class BackgroundArtImporter : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Art/Background/";
        const float ArtHeight = 16f; // world units; the levels' camera shows 14 (orthographic size 7)

        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.GetSourceTextureWidthAndHeight(out _, out int height);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            if (height > 0) settings.spritePixelsPerUnit = height / ArtHeight;
            settings.mipmapEnabled = false;
            settings.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(settings);
        }
    }
}
