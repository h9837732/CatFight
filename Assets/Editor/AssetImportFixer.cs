using UnityEditor;
using UnityEngine;

namespace CatFight.EditorTools
{
    /// <summary>
    /// 素材导入设置器。把 Resources 下的图/音频一次性配置成游戏需要的格式：
    ///   · 立绘 / 卡图 → Sprite（Single），不压缩，保持清晰
    ///   · 卡面底纹    → Sprite（Tiled 用 Repeat）
    ///   · 背景        → Sprite，最大 2048 降采样
    ///   · mp3         → 流式播放（音乐用 Streaming 省内存）
    /// 菜单：CatFight → Assets → Fix Import Settings
    /// </summary>
    public static class AssetImportFixer
    {
        [MenuItem("CatFight/Assets/Fix Import Settings")]
        public static void FixAll()
        {
            int changed = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;

                var before = (ti.textureType, ti.spritePixelsPerUnit, ti.maxTextureSize, ti.mipmapEnabled);

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100f;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;

                if (path.Contains("/Bg/"))
                {
                    ti.maxTextureSize = 2048;
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;
                }
                else
                {
                    ti.maxTextureSize = 2048;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                }

                // 卡面底纹平铺用
                if (path.EndsWith("card_bg.jpg"))
                    ti.wrapMode = TextureWrapMode.Repeat;

                var after = (ti.textureType, ti.spritePixelsPerUnit, ti.maxTextureSize, ti.mipmapEnabled);
                if (!before.Equals(after)) changed++;

                ti.SaveAndReimport();
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter ai) continue;

                var settings = ai.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                ai.defaultSampleSettings = settings;
                ai.forceToMono = false;
                ai.SaveAndReimport();
                changed++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[AssetImportFixer] 已处理 {changed} 处导入设置改动");
        }
    }
}
