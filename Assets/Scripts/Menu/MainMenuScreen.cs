using System;
using CatFight.Core;
using CatFight.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatFight.Menu
{
    /// <summary>
    /// 开始界面：标题 + 新游戏 / 继续 / 设置 / 退出。
    /// 全部 UI 运行时代码生成，占位阶段只用程序图形与系统字体。
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _settingsPanel;
        private Text _statusText;

        private void Start()
        {
            EnsureSession();
            Build();
            RefreshState();
        }

        private static void EnsureSession()
        {
            if (GameSession.Instance == null)
            {
                var go = new GameObject("GameSession");
                go.AddComponent<GameSession>();
            }
        }

        private void Build()
        {
            _canvas = UiKit.CreateCanvas("MainMenuCanvas", 10);

            // 背景：纸色 + 暗角
            var bg = UiKit.CreateImage(_canvas.transform, "Background", Theme.Paper);
            UiKit.Stretch(bg.rectTransform);

            var vignetteTop = UiKit.CreateImage(_canvas.transform, "VignetteTop", new Color(0f, 0f, 0f, 0.18f));
            var vtt = vignetteTop.rectTransform;
            vtt.anchorMin = new Vector2(0f, 0.82f);
            vtt.anchorMax = new Vector2(1f, 1f);
            vtt.offsetMin = Vector2.zero;
            vtt.offsetMax = Vector2.zero;

            var vignetteBottom = UiKit.CreateImage(_canvas.transform, "VignetteBottom", new Color(0f, 0f, 0f, 0.22f));
            var vbt = vignetteBottom.rectTransform;
            vbt.anchorMin = new Vector2(0f, 0f);
            vbt.anchorMax = new Vector2(1f, 0.18f);
            vbt.offsetMin = Vector2.zero;
            vbt.offsetMax = Vector2.zero;

            // 标题
            var title = UiKit.CreateOutlinedText(_canvas.transform, "Title", "CAT FIGHT",
                Theme.FontHuge, Theme.Ink, Theme.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.AnchorCenter(title.rectTransform, new Vector2(0f, 300f), new Vector2(1200f, 120f));

            var subtitle = UiKit.CreateText(_canvas.transform, "Subtitle",
                "一场关于猫、卡牌与塔的战斗", Theme.FontBody, Theme.InkFaint);
            UiKit.AnchorCenter(subtitle.rectTransform, new Vector2(0f, 215f), new Vector2(900f, 50f));

            var rule = UiKit.CreateImage(_canvas.transform, "Rule", Theme.InkFaint.WithAlpha(0.5f));
            UiKit.AnchorCenter(rule.rectTransform, new Vector2(0f, 170f), new Vector2(420f, 2f));

            // 按钮列
            float y = 40f;
            const float gap = 96f;

            var btnNew = UiKit.CreateButton(_canvas.transform, "BtnNew", "开始新游戏", Theme.FontBody,
                Theme.Ink, Theme.Paper, OnNewGame);
            UiKit.AnchorCenter(btnNew.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(420f, 84f));

            var btnContinue = UiKit.CreateButton(_canvas.transform, "BtnContinue", "继续游戏", Theme.FontBody,
                Theme.PanelBg, Theme.Paper, OnContinue);
            UiKit.AnchorCenter(btnContinue.GetComponent<RectTransform>(), new Vector2(0f, y - gap), new Vector2(420f, 84f));

            var btnSettings = UiKit.CreateButton(_canvas.transform, "BtnSettings", "设置", Theme.FontBody,
                Theme.InkSoft, Theme.Paper, ToggleSettings);
            UiKit.AnchorCenter(btnSettings.GetComponent<RectTransform>(), new Vector2(0f, y - gap * 2), new Vector2(420f, 84f));

            var btnQuit = UiKit.CreateButton(_canvas.transform, "BtnQuit", "退出游戏", Theme.FontBody,
                Theme.InkSoft, Theme.Paper, OnQuit);
            UiKit.AnchorCenter(btnQuit.GetComponent<RectTransform>(), new Vector2(0f, y - gap * 3), new Vector2(420f, 84f));

            // 状态行（存档提示）
            _statusText = UiKit.CreateText(_canvas.transform, "Status", "", Theme.FontSmall, Theme.InkFaint);
            UiKit.AnchorCenter(_statusText.rectTransform, new Vector2(0f, -420f), new Vector2(1100f, 40f));

            // 版本
            var ver = UiKit.CreateText(_canvas.transform, "Version", "v0.1 · 原型阶段", Theme.FontTiny,
                Theme.InkFaint.WithAlpha(0.8f), TextAnchor.LowerRight);
            UiKit.Anchor(ver.rectTransform, new Vector2(1f, 0f), new Vector2(-32f, 24f), new Vector2(400f, 30f));

            BuildSettingsPanel();
            _settingsPanel.SetActive(false);
        }

        private void BuildSettingsPanel()
        {
            _settingsPanel = new GameObject("SettingsRoot", typeof(RectTransform));
            var srt = _settingsPanel.GetComponent<RectTransform>();
            srt.SetParent(_canvas.transform, false);
            UiKit.Stretch(srt);

            UiKit.CreateModalShade(srt);

            var panel = UiKit.CreateDialog(srt, "设置", new Vector2(860f, 660f));

            var s = GameSession.Instance.Settings;

            // 面板 860×660，中心为原点；标题栏占顶部 72px
            const float RowY0 = 130f;
            const float RowGap = 78f;
            const float LabelX = -230f;
            const float LabelW = 340f;
            const float CtrlX = 265f;

            // --- 音量 ---
            var volCap = UiKit.CreateText(panel, "VolCap", "音量", Theme.FontBody, Theme.Paper,
                TextAnchor.MiddleLeft);
            UiKit.AnchorCenter(volCap.rectTransform, new Vector2(LabelX, RowY0), new Vector2(LabelW, 56f));

            var volVal = UiKit.CreateText(panel, "VolVal", "80%", Theme.FontBody, Theme.Gold,
                TextAnchor.MiddleCenter);
            UiKit.AnchorCenter(volVal.rectTransform, new Vector2(120f, RowY0), new Vector2(100f, 56f));

            CreateVolumeSlider(panel, new Vector2(-50f, RowY0), 240f, s.masterVolume, v =>
            {
                volVal.text = Mathf.RoundToInt(v * 100f) + "%";
            });
            volVal.text = Mathf.RoundToInt(s.masterVolume * 100f) + "%";

            var muteBtn = UiKit.CreateButton(panel, "Mute", "静音", Theme.FontSmall, Theme.InkSoft, Theme.Paper, null);
            UiKit.AnchorCenter(muteBtn.GetComponent<RectTransform>(), new Vector2(CtrlX, RowY0), new Vector2(130f, 56f));
            var muteLabel = muteBtn.transform.Find("Label").GetComponent<Text>();
            muteBtn.onClick.AddListener(() =>
            {
                var aud = AudioDirector.Instance;
                if (aud == null) return;
                bool next = !aud.IsMuted;
                aud.SetMuted(next);
                muteLabel.text = next ? "已静音" : "静音";
                muteBtn.GetComponent<Image>().color = next ? Theme.Bad : Theme.InkSoft;
                UiKit.SetButtonEnabled(muteBtn, true);
            });

            // --- 开关项 ---
            float y = RowY0 - 90f;
            MakeToggle(panel, "动画速度：快速模式", s.fastMode, LabelX, LabelW, CtrlX,
                v => { s.fastMode = v; SaveService.SaveSettings(s); }, ref y);
            MakeToggle(panel, "显示伤害预览", s.showDamagePreview, LabelX, LabelW, CtrlX,
                v => { s.showDamagePreview = v; SaveService.SaveSettings(s); }, ref y);
            MakeToggle(panel, "屏幕震动", s.screenShake, LabelX, LabelW, CtrlX,
                v => { s.screenShake = v; SaveService.SaveSettings(s); }, ref y);

            // --- 底部按钮 ---
            var close = UiKit.CreateButton(panel, "BtnClose", "关闭", Theme.FontBody, Theme.Gold, Theme.Ink, ToggleSettings);
            UiKit.AnchorCenter(close.GetComponent<RectTransform>(), new Vector2(160f, -235f), new Vector2(220f, 72f));

            var deleteSave = UiKit.CreateButton(panel, "BtnDeleteSave", "删除存档", Theme.FontSmall,
                Theme.Bad, Theme.Paper, () =>
                {
                    SaveService.DeleteRun();
                    RefreshState();
                    _statusText.text = "存档已删除";
                });
            UiKit.AnchorCenter(deleteSave.GetComponent<RectTransform>(), new Vector2(-160f, -235f), new Vector2(220f, 72f));
        }

        /// <summary>音量滑块：点条、拖柄、或按减/加按钮都行。</summary>
        private Slider CreateVolumeSlider(Transform parent, Vector2 pos, float width,
                                          float initial, System.Action<float> onChanged)
        {
            var root = UiKit.CreateRect(parent, "VolumeSlider");
            UiKit.AnchorCenter(root, pos, new Vector2(width, 44f));

            // 轨道
            var trackBg = UiKit.CreateImage(root, "TrackBg", new Color(1f, 1f, 1f, 0.16f), UiKit.RoundRect);
            trackBg.type = Image.Type.Sliced;
            var tb = trackBg.rectTransform;
            tb.anchorMin = new Vector2(0f, 0.5f);
            tb.anchorMax = new Vector2(1f, 0.5f);
            tb.pivot = new Vector2(0.5f, 0.5f);
            tb.sizeDelta = new Vector2(0f, 14f);
            tb.anchoredPosition = Vector2.zero;

            // 填充
            var fillArea = UiKit.CreateRect(root, "FillArea");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, 14f);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = UiKit.CreateImage(fillArea, "Fill", Theme.Gold, UiKit.RoundRect);
            fill.type = Image.Type.Sliced;
            UiKit.Stretch(fill.rectTransform);

            // 滑块柄
            var handleArea = UiKit.CreateRect(root, "HandleArea");
            UiKit.Stretch(handleArea);

            var handle = UiKit.CreateImage(handleArea, "Handle", Color.white, UiKit.Circle, raycast: true);
            var hr = handle.rectTransform;
            hr.sizeDelta = new Vector2(36f, 36f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = hr;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp01(initial);

            slider.onValueChanged.AddListener(v =>
            {
                if (AudioDirector.Instance != null) AudioDirector.Instance.SetMasterVolume(v);
                else
                {
                    GameSession.Instance.Settings.masterVolume = v;
                    SaveService.SaveSettings(GameSession.Instance.Settings);
                }
                onChanged?.Invoke(v);
            });

            return slider;
        }

        private void MakeToggle(Transform parent, string label, bool value,
                                float labelX, float labelW, float ctrlX,
                                Action<bool> onChange, ref float y)
        {
            var row = UiKit.CreateText(parent, "Row_" + label, label, Theme.FontBody, Theme.Paper,
                TextAnchor.MiddleLeft);
            UiKit.AnchorCenter(row.rectTransform, new Vector2(labelX, y), new Vector2(labelW, 56f));

            var state = value;
            var btn = UiKit.CreateButton(parent, "Toggle_" + label, value ? "开" : "关", Theme.FontBody,
                value ? Theme.Ok : Theme.InkSoft, Theme.Paper, null);
            UiKit.AnchorCenter(btn.GetComponent<RectTransform>(), new Vector2(ctrlX, y), new Vector2(130f, 56f));

            var labelText = btn.transform.Find("Label").GetComponent<Text>();
            btn.onClick.AddListener(() =>
            {
                state = !state;
                labelText.text = state ? "开" : "关";
                btn.GetComponent<Image>().color = state ? Theme.Ok : Theme.InkSoft;
                UiKit.SetButtonEnabled(btn, true);   // 保留底色
                onChange(state);
            });

            y -= 78f;
        }

        private void RefreshState()
        {
            bool hasSave = SaveService.HasRun();

            var btnContinue = _canvas.transform.Find("BtnContinue");
            if (btnContinue != null)
            {
                var btn = btnContinue.GetComponent<Button>();
                UiKit.SetButtonEnabled(btn, hasSave);
            }

            if (hasSave)
            {
                var run = SaveService.LoadRun();
                _statusText.text = run != null
                    ? $"检测到存档：第 {run.visitedPath.Count} 步 · 生命 {run.hp}/{run.maxHp} · 金币 {run.gold}"
                    : "检测到存档";
            }
            else
            {
                _statusText.text = "暂无存档";
            }
        }

        // ------------------------------------------------------------------
        // 交互
        // ------------------------------------------------------------------

        private void OnNewGame()
        {
            if (SaveService.HasRun())
            {
                ShowConfirmOverwrite();
                return;
            }
            StartNewRun();
        }

        private void StartNewRun()
        {
            GameSession.Instance.StartNewRun();
            GameSession.Instance.GoMap();
        }

        private void ShowConfirmOverwrite()
        {
            var root = new GameObject("ConfirmRoot", typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            UiKit.Stretch(rt);

            UiKit.CreateModalShade(rt);
            var panel = UiKit.CreateDialog(rt, "已有存档", new Vector2(720f, 380f));

            var msg = UiKit.CreateText(panel, "Msg", "开始新游戏会覆盖当前进度，确定吗？",
                Theme.FontBody, Theme.Paper);
            UiKit.AnchorCenter(msg.rectTransform, new Vector2(0f, 20f), new Vector2(620f, 80f));

            var yes = UiKit.CreateButton(panel, "Yes", "覆盖并开始", Theme.FontBody, Theme.Bad, Theme.Paper, () =>
            {
                SaveService.DeleteRun();
                Destroy(root);
                StartNewRun();
            });
            UiKit.AnchorCenter(yes.GetComponent<RectTransform>(), new Vector2(-150f, -110f), new Vector2(260f, 72f));

            var no = UiKit.CreateButton(panel, "No", "取消", Theme.FontBody, Theme.InkSoft, Theme.Paper,
                () => Destroy(root));
            UiKit.AnchorCenter(no.GetComponent<RectTransform>(), new Vector2(150f, -110f), new Vector2(260f, 72f));
        }

        private void OnContinue()
        {
            if (GameSession.Instance.ContinueRun())
                GameSession.Instance.GoMap();
            else
                _statusText.text = "无法读取存档";
        }

        private void ToggleSettings()
        {
            _settingsPanel.SetActive(!_settingsPanel.activeSelf);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
