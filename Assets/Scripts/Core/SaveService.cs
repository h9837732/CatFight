using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CatFight.Core
{
    /// <summary>
    /// 存档服务。两个槽位：
    ///   save_slot0.json —— 进行中的 Run（地图 + 卡组 + 血量）
    ///   settings.json   —— 玩家设置（音量、动效开关等）
    ///
    /// 存档目录：Editor 下放在工程根的 Saves/（不占 C 盘、便于调试），
    /// 发行版放在 Application.persistentDataPath（平台标准位置）。
    /// </summary>
    public static class SaveService
    {
        public const string RunSlotFile = "save_slot0.json";
        public const string SettingsFile = "settings.json";

        private static string _dirOverride;

        public static string SaveDir
        {
            get
            {
                if (!string.IsNullOrEmpty(_dirOverride)) return _dirOverride;

                string root = Application.isEditor
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                    : Application.persistentDataPath;

                return Path.Combine(root, "Saves");
            }
        }

        /// <summary>测试用：重定向存档目录。</summary>
        public static void SetDirOverride(string dir) => _dirOverride = dir;

        private static string PathFor(string file)
        {
            Directory.CreateDirectory(SaveDir);
            return Path.Combine(SaveDir, file);
        }

        // ------------------------------------------------------------------
        // Run
        // ------------------------------------------------------------------

        public static bool HasRun() => File.Exists(Path.Combine(SaveDir, RunSlotFile));

        public static void SaveRun(RunState run)
        {
            if (run == null) return;
            try
            {
                string json = JsonUtility.ToJson(run, prettyPrint: true);
                File.WriteAllText(PathFor(RunSlotFile), json, new System.Text.UTF8Encoding(false));
                Debug.Log($"[Save] run saved -> {Path.Combine(SaveDir, RunSlotFile)}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] 保存失败：{ex.Message}");
            }
        }

        public static RunState LoadRun()
        {
            string path = Path.Combine(SaveDir, RunSlotFile);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var run = JsonUtility.FromJson<RunState>(json);
                if (run == null) return null;

                if (run.deck == null) run.deck = new List<CardRef>();
                if (run.relics == null) run.relics = new List<string>();
                if (run.visitedPath == null) run.visitedPath = new List<int>();
                if (run.maxHp < 1) run.maxHp = 80;
                run.hp = Mathf.Clamp(run.hp, 0, run.maxHp);
                return run;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] 读取失败：{ex.Message}");
                return null;
            }
        }

        public static void DeleteRun()
        {
            try
            {
                string path = Path.Combine(SaveDir, RunSlotFile);
                if (File.Exists(path)) File.Delete(path);
                Debug.Log("[Save] run slot cleared");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] 删除失败：{ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Settings
        // ------------------------------------------------------------------

        [Serializable]
        public class Settings
        {
            public int version = 1;
            public float masterVolume = 0.8f;
            public bool screenShake = true;
            public bool fastMode = false;      // 缩短动画时长
            public bool showDamagePreview = true;
        }

        public static Settings LoadSettings()
        {
            string path = Path.Combine(SaveDir, SettingsFile);
            if (!File.Exists(path)) return new Settings();
            try
            {
                var s = JsonUtility.FromJson<Settings>(File.ReadAllText(path, System.Text.Encoding.UTF8));
                return s ?? new Settings();
            }
            catch
            {
                return new Settings();
            }
        }

        public static void SaveSettings(Settings s)
        {
            if (s == null) return;
            try
            {
                File.WriteAllText(PathFor(SettingsFile), JsonUtility.ToJson(s, true), new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] 设置保存失败：{ex.Message}");
            }
        }
    }
}
