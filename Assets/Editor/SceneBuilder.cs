using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatFight.EditorTools
{
    /// <summary>
    /// 一次性生成三个场景并写入 Build Settings。
    /// 菜单：CatFight → Scenes → Rebuild All
    /// 每个场景只放一个 bootstrap 物体（挂对应 Screen 脚本），界面由运行时代码构建，
    /// 这样接入正式美术资源时不需要重做场景。
    /// </summary>
    public static class SceneBuilder
    {
        private const string SceneDir = "Assets/Scenes";
        public const string MainMenuScene = SceneDir + "/MainMenu.unity";
        public const string MapScene = SceneDir + "/Map.unity";
        public const string BattleScene = SceneDir + "/Battle.unity";

        [MenuItem("CatFight/Scenes/Rebuild All")]
        public static void RebuildAll()
        {
            Directory.CreateDirectory(SceneDir);

            BuildScene(MainMenuScene, "MainMenuScreen", typeof(CatFight.Menu.MainMenuScreen));
            BuildScene(MapScene, "MapScreen", typeof(CatFight.MapScene.MapScreen));
            BuildScene(BattleScene, "BattleScreen", typeof(CatFight.Battle.BattleScreen));

            SetBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneBuilder] 三个场景已重建并写入 Build Settings");
        }

        private static void BuildScene(string path, string objectName, System.Type screenType)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 相机（UI 是 Overlay，相机只负责清屏背景色）
            var camGo = new GameObject("Main Camera", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.10f, 0.09f);
            cam.orthographic = true;
            camGo.tag = "MainCamera";

            // 事件系统（uGUI 点击必需）。项目用新版 Input System，
            // 必须用 InputSystemUIInputModule，否则所有点击都无效。
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            var uiModule = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();

            // 界面 bootstrap
            var screenGo = new GameObject(objectName);
            screenGo.AddComponent(screenType);

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void SetBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScene, true),
                new EditorBuildSettingsScene(MapScene, true),
                new EditorBuildSettingsScene(BattleScene, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("CatFight/Scenes/Open Main Menu")]
        public static void OpenMainMenu() => EditorSceneManager.OpenScene(MainMenuScene);

        [MenuItem("CatFight/Scenes/Open Map")]
        public static void OpenMap() => EditorSceneManager.OpenScene(MapScene);

        [MenuItem("CatFight/Scenes/Open Battle")]
        public static void OpenBattle() => EditorSceneManager.OpenScene(BattleScene);
    }
}
