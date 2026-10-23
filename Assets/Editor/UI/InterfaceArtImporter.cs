using UnityEditor;
using UnityEngine;

namespace GateRush.UI.Editor
{
    /// <summary>
    /// Imports the interface art and the avatars by
    /// <see cref="InterfaceArtImportRules"/> (Module 45): Sprite (2D and UI), Single, Full Rect so a sliced sprite
    /// packs whole in an atlas, Alpha Is Transparency, no mipmaps, the
    /// folder's Max Size, the rules' pixels per unit and the file's border.
    /// Files without a rule are left as they are.
    /// </summary>
    /// <remarks>
    /// It runs only when a file is imported; files imported before it existed
    /// take the settings when their folders are reimported.
    /// </remarks>
    public sealed class InterfaceArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!InterfaceArtImportRules.TryGetRule(assetPath, out var rule))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            settings.spritePixelsPerUnit = InterfaceArtImportRules.PixelsPerUnit;
            settings.spriteBorder = rule.Border;
            importer.SetTextureSettings(settings);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = rule.Border;
            importer.maxTextureSize = rule.MaxSize;
        }
    }
}
