using System.Collections;
using System.Collections.Generic;
using CatFight.Core;
using CatFight.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CatFight.Battle
{
    /// <summary>
    /// 战斗界面。布局遵循战斗需求文档 §4：
    ///   左下 = 玩家（立绘位 + 生命 + 格挡 + 状态）
    ///   右上 = 敌人（血条 + 意图）
    ///   底部居中 = 手牌（叠压排列）
    ///   右下 = 结束回合
    /// </summary>
    public class BattleScreen : MonoBehaviour
    {
        private const float CardW = 200f;
        private const float CardH = 280f;
        private const float HandSpan = 1240f;
        private const float CardGap = 10f;
        private const float ArcHeight = 22f;     // 中间牌擑起高度，让叠压有层次
        private const float EnemyWidth = 240f;

        private Canvas _canvas;
        private RectTransform _root;

        private BattleState _state;
        private GameSession.BattleRequest _request;

        // HUD 引用
        private Image _playerHpFill;
        private Text _playerHpText;
        private Text _playerBlockText;
        private Text _playerStatusText;
        private Text _energyText;
        private RectTransform _energyRow;
        private Text _turnText;
        private Text _logText;
        private RectTransform _handRow;
        private Button _endTurnBtn;
        private RectTransform _enemyRow;
        private RectTransform _overlay;

        private readonly List<EnemyView> _enemyViews = new List<EnemyView>();
        private readonly List<CardView> _cardViews = new List<CardView>();
        private RectTransform _relicTip;
        private RectTransform _itemBar;
        private RectTransform _hintBar;
        private Text _hintText;
        private int _selectedItemSlot = -1;

        private int _selectedHandIndex = -1;
        private bool _busy;
        private bool _endHandled;

        private class EnemyView
        {
            public int index;
            public RectTransform root;
            public Image hpFill;
            public Text hpText;
            public Text nameText;
            public Text intentText;
            public Text statusText;
            public Button button;
        }

        private class CardView
        {
            public int handIndex;
            public RectTransform root;
            public Image bg;
            public Text cost;
            public Text name;
            public Text desc;
            public Text hint;        // “再点一次打出 / 选择目标”提示
            public Button button;
            public float baseY;      // 未选中时的原始 y
        }

        // ------------------------------------------------------------------

        private void Start()
        {
            if (GameSession.Instance == null || GameSession.Instance.Run == null)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameSession.SceneMainMenu);
                return;
            }

            _request = GameSession.Instance.PendingBattle;
            if (_request == null)
            {
                _request = new GameSession.BattleRequest
                {
                    encounterId = "normal_encounter",
                    sourceNodeId = -1,
                    kind = GameSession.NodeKind.Battle,
                    streak = GameSession.Instance.Run.visitedPath.Count
                };
            }

            var run = GameSession.Instance.Run;
            var deck = new List<CardRefLite>();
            foreach (var c in run.deck) deck.Add(new CardRefLite(c.id, c.upgraded));

            var encounter = EnemyDb.BuildEncounter(_request.encounterId, _request.streak);

            _state = new BattleState();
            BuildUi();
            _state.OnStateChanged += RefreshAll;
            _state.OnLogged += OnLog;

            _state.Start(run.seed + run.visitedPath.Count * 13, run.hp, run.maxHp, deck, encounter);

            // 战斗音乐：开场音 → 循环曲
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayBattle();

            // 遗物：战斗开始时生效
            ApplyBattleStartRelics(run);

            RebuildEnemies();
            RefreshAll();

            // 入场过渡：全屏先模糊后清晰（仅在从地图进入战斗时播放）
            if (_request != null && _request.sourceNodeId >= 0)
            {
                var intro = gameObject.AddComponent<ScreenBlurIntro>();
                intro.Play(_canvas.transform);
            }
        }

        /// <summary>战斗开始时的遗物钩子。</summary>
        private void ApplyBattleStartRelics(Core.RunState run)
        {
            if (run == null || run.relics == null) return;

            foreach (var id in run.relics)
            {
                switch (id)
                {
                    case "relic_vajra":
                        _state.Player.AddStatus(StatusType.Strength, 1);
                        break;

                    case "relic_pocket_watch":
                        _state.Player.AddStatus(StatusType.Dexterity, 1);
                        break;

                    case "relic_anchor":
                        _state.GainBlock(_state.Player, 10);
                        break;

                    case "relic_iron_plate":
                        _state.GainBlock(_state.Player, 4);
                        break;

                    case "relic_blood_vial":
                        _state.Player.hp = Mathf.Min(_state.Player.maxHp, _state.Player.hp + 2);
                        break;

                    case "relic_lantern":
                        // 仅在每场战斗的第一回合生效
                        _state.AddEnergy(1);
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnStateChanged -= RefreshAll;
                _state.OnLogged -= OnLog;
            }
            if (AudioDirector.Instance != null) AudioDirector.Instance.StopBattle();
        }

        // ------------------------------------------------------------------
        // UI 构建
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            _canvas = UiKit.CreateCanvas("BattleCanvas", 10);

            // 战斗背景（街道照片）。素材缺失时降级为纸色
            var bgSprite = UI.Assets2.BattleBg;
            var bg = UiKit.CreateImage(_canvas.transform, "Bg",
                bgSprite != null ? Color.white : Theme.Paper, bgSprite);
            bg.preserveAspect = false;
            UiKit.Stretch(bg.rectTransform);

            // 顶部/底部压暗，保证文字可读
            var topShade = UiKit.CreateImage(_canvas.transform, "ShadeTop", new Color(0f, 0f, 0f, 0.42f));
            var tsrt = topShade.rectTransform;
            tsrt.anchorMin = new Vector2(0f, 1f);
            tsrt.anchorMax = new Vector2(1f, 1f);
            tsrt.pivot = new Vector2(0.5f, 1f);
            tsrt.sizeDelta = new Vector2(0f, 260f);
            tsrt.anchoredPosition = Vector2.zero;

            var bottomShade = UiKit.CreateImage(_canvas.transform, "ShadeBottom", new Color(0f, 0f, 0f, 0.5f));
            var bsrt = bottomShade.rectTransform;
            bsrt.anchorMin = new Vector2(0f, 0f);
            bsrt.anchorMax = new Vector2(1f, 0f);
            bsrt.pivot = new Vector2(0.5f, 0f);
            bsrt.sizeDelta = new Vector2(0f, 340f);
            bsrt.anchoredPosition = Vector2.zero;

            _root = UiKit.CreateRect(_canvas.transform, "Root");
            UiKit.Stretch(_root);

            BuildTopBar();
            BuildRelicBar();
            BuildItemBar();
            BuildPlayerPanel();
            BuildEnemyArea();
            BuildHandArea();
            BuildLogPanel();

            _overlay = UiKit.CreateRect(_canvas.transform, "Overlay");
            UiKit.Stretch(_overlay);
        }

        private void BuildTopBar()
        {
            var bar = UiKit.CreatePanel(_root, "TopBar", Theme.PanelBg.WithAlpha(0.9f));
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 84f);
            rt.anchoredPosition = Vector2.zero;

            _turnText = UiKit.CreateText(bar.transform, "Turn", "第 1 回合", Theme.FontBody, Theme.Paper,
                TextAnchor.MiddleLeft);
            var trt = _turnText.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.anchoredPosition = new Vector2(36f, 0f);
            trt.sizeDelta = new Vector2(400f, 50f);

            var encName = _request.kind == GameSession.NodeKind.Boss ? "首领战"
                        : _request.kind == GameSession.NodeKind.Elite ? "精英战"
                        : "普通战斗";
            var enc = UiKit.CreateText(bar.transform, "Enc", encName, Theme.FontSmall, Theme.InkFaint,
                TextAnchor.MiddleRight);
            var ert = enc.rectTransform;
            ert.anchorMin = ert.anchorMax = new Vector2(1f, 0.5f);
            ert.pivot = new Vector2(1f, 0.5f);
            ert.anchoredPosition = new Vector2(-36f, 0f);
            ert.sizeDelta = new Vector2(400f, 40f);
        }

        /// <summary>
        /// 玩家区：立绘（图片）→ 血条 → 行动点数，自上而下。
        /// 不使用任何底板 / 状态框，直接绘制在场景背景上。
        /// </summary>
        private void BuildPlayerPanel()
        {
            // 玩家区扎根在屏幕左下角：立绘左边缘贴屏（略出屏裁切）、向上撑满，
            // 血条与行动点压在底部，中间不留空隙。
            var root = UiKit.CreateRect(_root, "PlayerArea");
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(0f, 0f);
            root.sizeDelta = new Vector2(380f, 1080f);

            const float W = 236f;
            const float BarY = 96f;        // 血条底边高度
            const float EnergyY = 34f;     // 行动点区域底边高度
            const float PortraitH = 380f;  // 立绘高度（不超过血条上方空间）

            // --- 立绘（图片）。左下角、左边缘微出屏、底边贴血条上方 ---
            var portrait = UiKit.CreateImage(root, "PlayerPortrait", Color.white, UiKit.Portrait);
            portrait.preserveAspect = true;
            UI.Assets2.ApplyWhiteKey(portrait, 0.80f, 0.14f);
            var prt = portrait.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0f);
            prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = new Vector2(-10f, BarY + 44f);
            prt.sizeDelta = new Vector2(W + 10f, PortraitH);

            // 格挡徽章（叠在立绘右上角）
            var blockCircle = UiKit.CreateImage(root, "BlockCircle", Theme.BlockBlue, UiKit.Circle);
            var brt = blockCircle.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(W - 40f, BarY + 44f + PortraitH - 76f);
            brt.sizeDelta = new Vector2(86f, 86f);

            _playerBlockText = UiKit.CreateText(blockCircle.transform, "Val", "0", Theme.FontHead, Color.white);
            UiKit.Stretch(_playerBlockText.rectTransform);

            var blockCap = UiKit.CreateText(root, "BlockCap", "格挡", Theme.FontTiny, Theme.InkFaint);
            var bct = blockCap.rectTransform;
            bct.anchorMin = bct.anchorMax = new Vector2(0f, 0f);
            bct.pivot = new Vector2(0.5f, 1f);
            bct.anchoredPosition = new Vector2(W - 40f, BarY + 44f + PortraitH - 80f);
            bct.sizeDelta = new Vector2(86f, 22f);

            // --- 血条（贴屏幕底部区域，位于立绘下方）---
            // 加一层深色底板，避免与立绘的橘猫毛色混在一起看不清
            var hpPlate = UiKit.CreateImage(root, "HpPlate", new Color(0f, 0f, 0f, 0.55f), UiKit.RoundRect);
            var plrt = hpPlate.rectTransform;
            plrt.anchorMin = plrt.anchorMax = new Vector2(0f, 0f);
            plrt.pivot = new Vector2(0f, 0f);
            plrt.anchoredPosition = new Vector2(12f, BarY - 6f);
            plrt.sizeDelta = new Vector2(264f, 46f);
            hpPlate.raycastTarget = false;

            var hbrt = UiKit.CreateRect(root, "HpBarRoot");
            hbrt.anchorMin = hbrt.anchorMax = new Vector2(0f, 0f);
            hbrt.pivot = new Vector2(0f, 0f);
            hbrt.anchoredPosition = new Vector2(20f, BarY);
            hbrt.sizeDelta = new Vector2(248f, 34f);

            _playerHpFill = UiKit.CreateProgressBar(hbrt, "Hp", new Vector2(248f, 34f),
                Theme.HpBack, Theme.HpRed, segments: 8);
            var barRoot = _playerHpFill.transform.parent.GetComponent<RectTransform>();
            UiKit.Stretch(barRoot);

            _playerHpText = UiKit.CreateText(hbrt, "HpText", "80/80", Theme.FontSmall, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_playerHpText.rectTransform);

            // --- 行动点数（血条下方，屏幕最底）---
            var energyCap = UiKit.CreateText(root, "EnergyCap", "行动点", Theme.FontTiny, Theme.InkFaint,
                TextAnchor.MiddleLeft);
            var ecrt = energyCap.rectTransform;
            ecrt.anchorMin = ecrt.anchorMax = new Vector2(0f, 0f);
            ecrt.pivot = new Vector2(0f, 0f);
            ecrt.anchoredPosition = new Vector2(20f, EnergyY + 40f);
            ecrt.sizeDelta = new Vector2(90f, 22f);

            _energyText = UiKit.CreateText(root, "EnergyVal", "3/3", Theme.FontTiny, Theme.InkFaint,
                TextAnchor.MiddleRight);
            var evrt = _energyText.rectTransform;
            evrt.anchorMin = evrt.anchorMax = new Vector2(0f, 0f);
            evrt.pivot = new Vector2(1f, 0f);
            evrt.anchoredPosition = new Vector2(268f, EnergyY + 40f);
            evrt.sizeDelta = new Vector2(90f, 22f);

            _energyRow = UiKit.CreateRect(root, "EnergyRow");
            _energyRow.anchorMin = _energyRow.anchorMax = new Vector2(0f, 0f);
            _energyRow.pivot = new Vector2(0f, 0f);
            _energyRow.anchoredPosition = new Vector2(20f, EnergyY);
            _energyRow.sizeDelta = new Vector2(248f, 38f);

            // --- 状态（叠在血条上方，不要底板）---
            _playerStatusText = UiKit.CreateText(root, "Statuses", "", Theme.FontTiny,
                Theme.Gold, TextAnchor.MiddleLeft);
            var srt = _playerStatusText.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0f);
            srt.pivot = new Vector2(0f, 0f);
            srt.anchoredPosition = new Vector2(20f, BarY + 44f);
            srt.sizeDelta = new Vector2(248f, 34f);
            _playerStatusText.lineSpacing = 1.15f;
        }

        /// <summary>敌人区。不再是居中矩形，而是覆盖全屏的坐标容器，
        /// 具体站位由 EnemyLayout 按背景地平线计算（脚底落地）。</summary>
        /// <summary>
        /// 战斗界面左上角的遗物栏。玩家立绘缩小后左下腾出的空间留给它，
        /// 后续装备/道具栏也接在这排下方。
        /// </summary>
        private void BuildRelicBar()
        {
            var root = UiKit.CreateRect(_root, "RelicBar");
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(32f, -104f);
            root.sizeDelta = new Vector2(420f, 64f);

            var run = GameSession.Instance != null ? GameSession.Instance.Run : null;
            if (run == null || run.relics.Count == 0) return;

            const float IconSize = 56f;
            const float Spacing = 8f;
            const int MaxVisible = 6;

            int shown = Mathf.Min(run.relics.Count, MaxVisible);
            for (int i = 0; i < shown; i++)
            {
                string id = run.relics[i];

                var bg = UiKit.CreateImage(root, "Relic" + i, Theme.PanelBg.WithAlpha(0.78f), UiKit.Circle);
                var rt = bg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(i * (IconSize + Spacing), 0f);
                rt.sizeDelta = new Vector2(IconSize, IconSize);

                // 遗物无图标素材：用首字占位
                string name = Battle.RelicDb.NameOf(id);
                var glyph = UiKit.CreateText(bg.transform, "Glyph",
                    string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1),
                    Theme.FontSmall, Theme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(glyph.rectTransform);

                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                string desc = Battle.RelicDb.DescOf(id);
                string title = name;
                btn.onClick.AddListener(() => ShowRelicTip(title, desc));
            }

            if (run.relics.Count > MaxVisible)
            {
                var more = UiKit.CreateText(root, "More", $"+{run.relics.Count - MaxVisible}",
                    Theme.FontSmall, Theme.Paper, TextAnchor.MiddleCenter);
                var mrt = more.rectTransform;
                mrt.anchorMin = mrt.anchorMax = new Vector2(0f, 1f);
                mrt.pivot = new Vector2(0f, 1f);
                mrt.anchoredPosition = new Vector2(MaxVisible * (IconSize + Spacing), 12f);
                mrt.sizeDelta = new Vector2(70f, 32f);
            }
        }

        /// <summary>点遗物弹出的说明（战斗内轻量提示，不阻断操作）。</summary>
        private void ShowRelicTip(string title, string desc)
        {
            if (_relicTip != null) Destroy(_relicTip);

            var go = UiKit.CreateRect(_canvas.transform, "RelicTip");
            _relicTip = go;
            go.anchorMin = go.anchorMax = new Vector2(0f, 1f);
            go.pivot = new Vector2(0f, 1f);
            go.anchoredPosition = new Vector2(32f, -176f);
            go.sizeDelta = new Vector2(360f, 96f);

            var bg = UiKit.CreatePanel(go, "Bg", Theme.PanelBg.WithAlpha(0.95f));
            UiKit.Stretch(bg.rectTransform);

            var t = UiKit.CreateText(go, "Title", title, Theme.FontSmall, Theme.Gold,
                TextAnchor.UpperLeft, FontStyle.Bold);
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(14f, -40f);
            trt.offsetMax = new Vector2(-14f, -8f);

            var d = UiKit.CreateText(go, "Desc", desc, Theme.FontTiny, Theme.Paper, TextAnchor.UpperLeft);
            var drt = d.rectTransform;
            UiKit.Stretch(drt, 14f);
            drt.offsetMax = new Vector2(-14f, -40f);
            d.lineSpacing = 1.15f;

            CancelInvoke(nameof(HideRelicTip));
            Invoke(nameof(HideRelicTip), 2.5f);
        }

        private void HideRelicTip()
        {
            if (_relicTip != null) { Destroy(_relicTip); _relicTip = null; }
        }

        /// <summary>
        /// 消耗型道具栏（纵向，挂在遗物栏下方）。
        /// 布局按草图：遗物横排、道具纵排。每格 64×64，最多 ItemDb.MaxSlots 格。
        /// 点道具 → 无目标直接使用；需要目标则进入选择态，再点敌人。
        /// </summary>
        private void BuildItemBar()
        {
            var root = UiKit.CreateRect(_root, "ItemBar");
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(32f, -206f);
            root.sizeDelta = new Vector2(72f, 260f);
            _itemBar = root;

            // 栏目分隔标题，避免和上方遗物栏看成同一列
            var cap = UiKit.CreateOutlinedText(root, "Cap", "道具", Theme.FontTiny,
                Theme.Paper.WithAlpha(0.75f), new Color(0f, 0f, 0f, 0.6f), TextAnchor.MiddleLeft);
            var crt = cap.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(4f, 22f);
            crt.sizeDelta = new Vector2(80f, 20f);

            RefreshItemBar();
        }

        /// <summary>重建道具格（使用道具后需要刷新）。</summary>
        private void RefreshItemBar()
        {
            if (_itemBar == null) return;

            // 只清格子，保留标题
            for (int k = _itemBar.childCount - 1; k >= 0; k--)
            {
                var child = _itemBar.GetChild(k);
                if (child.name.StartsWith("Item") || child.name.StartsWith("Empty"))
                    Destroy(child.gameObject);
            }

            var run = GameSession.Instance != null ? GameSession.Instance.Run : null;
            int count = run != null && run.items != null ? run.items.Count : 0;

            const float Cell = 56f;
            const float Gap = 8f;

            for (int i = 0; i < ItemDb.MaxSlots; i++)
            {
                bool filled = i < count;
                string id = filled ? run.items[i] : null;

                var cell = UiKit.CreateImage(_itemBar, "Item" + i,
                    filled ? Theme.PanelBg.WithAlpha(0.86f) : new Color(0f, 0f, 0f, 0.20f),
                    UiKit.RoundRect, raycast: filled);
                cell.type = Image.Type.Sliced;
                var rt = cell.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(4f, -i * (Cell + Gap));
                rt.sizeDelta = new Vector2(Cell, Cell);

                if (!filled)
                {
                    // 空格：只画一个淡框
                    var plus = UiKit.CreateText(cell.transform, "Empty", "·", Theme.FontSmall,
                        Theme.Paper.WithAlpha(0.25f));
                    UiKit.Stretch(plus.rectTransform);
                    continue;
                }

                // 无图标素材：用道具名首字占位
                string name = ItemDb.NameOf(id);
                var glyph = UiKit.CreateText(cell.transform, "Glyph",
                    string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1),
                    Theme.FontSmall, Theme.EnergyCyan, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(glyph.rectTransform);

                if (_selectedItemSlot == i)
                    cell.color = Theme.EnergyCyan.WithAlpha(0.35f);

                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = cell;
                int slot = i;
                string itemId = id;
                btn.onClick.AddListener(() => OnItemClicked(slot, itemId));
            }
        }

        private void OnItemClicked(int slot, string itemId)
        {
            if (_busy || _state == null || _state.Phase != BattlePhase.PlayerAction) return;

            var item = ItemDb.Get(itemId);
            if (item == null) return;

            // 再点同一个 → 取消
            if (_selectedItemSlot == slot)
            {
                ClearItemSelection();
                return;
            }

            // 先清掉卡牌选中，两者互斥
            ClearSelection();

            if (item.target == TargetMode.SingleEnemy)
            {
                _selectedItemSlot = slot;
                RefreshItemBar();
                ShowHintBar($"{item.name}：点击一个敌人");
                return;
            }

            // 无目标：直接使用
            UseItemAt(slot, item, 0);
        }

        private void UseItemAt(int slot, ItemDef item, int targetIndex)
        {
            if (!_state.UseItem(item, targetIndex)) return;

            // 从存档列表里移除
            var run = GameSession.Instance.Run;
            if (run != null && slot >= 0 && slot < run.items.Count)
                run.items.RemoveAt(slot);

            ClearItemSelection();
            SaveService.SaveRun(run);
            RefreshItemBar();
            AfterPlayerAction();
        }

        private void ClearItemSelection()
        {
            _selectedItemSlot = -1;
            HideHintBar();
            RefreshItemBar();
        }

        /// <summary>遗物/道具在屏幕中上的操作提示。</summary>
        private void ShowHintBar(string text)
        {
            if (_hintBar == null)
            {
                var rt = UiKit.CreateRect(_root, "HintBar");
                _hintBar = rt;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -62f);
                rt.sizeDelta = new Vector2(560f, 42f);

                var bg = UiKit.CreateImage(rt, "Bg", Theme.PanelBg.WithAlpha(0.88f), UiKit.RoundRect);
                bg.type = Image.Type.Sliced;
                UiKit.Stretch(bg.rectTransform);

                _hintText = UiKit.CreateText(rt, "Text", "", Theme.FontSmall, Theme.Gold);
                UiKit.Stretch(_hintText.rectTransform, 8f);
            }
            _hintBar.gameObject.SetActive(true);
            _hintText.text = text;
        }

        private void HideHintBar()
        {
            if (_hintBar != null) _hintBar.gameObject.SetActive(false);
        }

        private void BuildEnemyArea()
        {
            _enemyRow = UiKit.CreateRect(_root, "EnemyRow");
            UiKit.Stretch(_enemyRow);   // 全屏参考坐标系，归一化定位
        }

        private void BuildHandArea()
        {
            _handRow = UiKit.CreateRect(_root, "HandRow");
            _handRow.anchorMin = _handRow.anchorMax = new Vector2(0.5f, 0f);
            _handRow.pivot = new Vector2(0.5f, 0f);
            _handRow.anchoredPosition = new Vector2(0f, 20f);
            _handRow.sizeDelta = new Vector2(HandSpan + CardW, CardH + 40f);

            _endTurnBtn = UiKit.CreateButton(_root, "BtnEndTurn", "结束回合", Theme.FontBody,
                Theme.Gold, Theme.Ink, OnEndTurn);
            var brt = _endTurnBtn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(1f, 0f);
            brt.anchoredPosition = new Vector2(-40f, 40f);
            brt.sizeDelta = new Vector2(240f, 88f);
        }

        private void BuildLogPanel()
        {
            // 完全不用底板 / 圆角框：文字直接落在背景上，靠描边保证可读
            var root = UiKit.CreateRect(_root, "LogArea");
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-40f, -108f);
            root.sizeDelta = new Vector2(400f, 170f);

            _logText = UiKit.CreateOutlinedText(root, "Log", "", Theme.FontTiny,
                Theme.InkSoft, Theme.Paper, TextAnchor.LowerRight);
            UiKit.Stretch(_logText.rectTransform, 6f);
            _logText.lineSpacing = 1.2f;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // ------------------------------------------------------------------
        // 敌人视图
        // ------------------------------------------------------------------

        private void RebuildEnemies()
        {
            UiKit.DestroyChildren(_enemyRow);
            _enemyViews.Clear();

            int n = _state.Enemies.Count;
            var slots = EnemyLayout.Compute(n);

            for (int i = 0; i < n; i++)
            {
                var enemy = _state.Enemies[i];
                var slot = slots[i];

                var root = UiKit.CreateRect(_enemyRow, "Enemy" + i);

                // 脚底锚点 → 归一化定位（相对全屏）
                root.anchorMin = root.anchorMax = slot.groundAnchor;
                root.pivot = new Vector2(0.5f, 0f);      // pivot 在底边中点
                root.anchoredPosition = Vector2.zero;
                root.sizeDelta = new Vector2(slot.size.x, slot.size.y);

                // 立绘高度占 root 的约 66%，上方留意图气泡、下方留名字/血条
                float portH = slot.size.y * 0.66f;

                // 意图气泡（紧贴立绘上方）
                var intentBg = UiKit.CreatePanel(root, "Intent", Theme.PanelBg.WithAlpha(0.85f));
                var ibrt = intentBg.rectTransform;
                ibrt.anchorMin = ibrt.anchorMax = new Vector2(0.5f, 1f);
                ibrt.pivot = new Vector2(0.5f, 0f);
                ibrt.anchoredPosition = new Vector2(0f, -10f);
                ibrt.sizeDelta = new Vector2(168f, 52f);

                var intentText = UiKit.CreateText(intentBg.transform, "IntentText", "", Theme.FontSmall, Theme.Paper);
                UiKit.Stretch(intentText.rectTransform, 4f);

                // 敌人立绘：底边对齐 root 底边（即脚底落在地面线上），向上生长
                var enemySprite = UI.Assets2.Enemy(i);
                var portrait = enemySprite != null
                    ? UiKit.CreateImage(root, "Portrait", Color.white, enemySprite, raycast: true)
                    : UiKit.CreateImage(root, "Portrait", Theme.PaperDark, UiKit.Circle, raycast: true);
                portrait.preserveAspect = true;
                var prt = portrait.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);   // 底边对齐
                prt.pivot = new Vector2(0.5f, 0f);
                prt.anchoredPosition = new Vector2(0f, 56f);             // 抬高一点，给名字/血条让位
                prt.sizeDelta = new Vector2(slot.size.x * 0.86f, portH);

                if (enemySprite == null)
                {
                    var glyph = UiKit.CreateText(portrait.transform, "Glyph", "敌", Theme.FontTitle, Theme.Ink);
                    UiKit.Stretch(glyph.rectTransform);
                }
                else
                {
                    UI.Assets2.ApplyWhiteKey(portrait, 0.80f, 0.14f);
                }

                var nameT = UiKit.CreateOutlinedText(root, "Name", enemy.name, Theme.FontSmall, Color.white,
                    new Color(0f, 0f, 0f, 0.8f), TextAnchor.MiddleCenter, FontStyle.Bold);
                var nrt = nameT.rectTransform;
                nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0f);
                nrt.pivot = new Vector2(0.5f, 0f);
                nrt.anchoredPosition = new Vector2(0f, 30f);
                nrt.sizeDelta = new Vector2(slot.size.x, 28f);

                // 血条（名字下面）
                var hprt = UiKit.CreateRect(root, "HpRoot");
                hprt.anchorMin = hprt.anchorMax = new Vector2(0.5f, 0f);
                hprt.pivot = new Vector2(0.5f, 0f);
                hprt.anchoredPosition = new Vector2(0f, 8f);
                hprt.sizeDelta = new Vector2(slot.size.x * 0.82f, 22f);

                var fill = UiKit.CreateProgressBar(hprt, "Hp", new Vector2(slot.size.x * 0.82f, 22f),
                    Theme.HpBack, Theme.HpRed);
                UiKit.Stretch(fill.transform.parent.GetComponent<RectTransform>());

                var hpText = UiKit.CreateOutlinedText(hprt, "HpText", "", Theme.FontTiny, Color.white,
                    new Color(0f, 0f, 0f, 0.85f));
                UiKit.Stretch(hpText.rectTransform);

                var statusT = UiKit.CreateOutlinedText(root, "Status", "", Theme.FontTiny, Theme.Gold,
                    new Color(0f, 0f, 0f, 0.85f));
                var strt = statusT.rectTransform;
                strt.anchorMin = strt.anchorMax = new Vector2(0.5f, 0f);
                strt.pivot = new Vector2(0.5f, 1f);
                strt.anchoredPosition = new Vector2(0f, 2f);
                strt.sizeDelta = new Vector2(slot.size.x, 30f);

                var btn = portrait.gameObject.AddComponent<Button>();
                btn.targetGraphic = portrait;
                portrait.gameObject.name = "EnemyHit" + i;
                int idx = i;
                btn.onClick.AddListener(() => OnEnemyClicked(idx));

                _enemyViews.Add(new EnemyView
                {
                    index = i,
                    root = root,
                    hpFill = fill,
                    hpText = hpText,
                    nameText = nameT,
                    intentText = intentText,
                    statusText = statusT,
                    button = btn
                });
            }
        }

        // ------------------------------------------------------------------
        // 手牌视图
        // ------------------------------------------------------------------

        private void RebuildHand()
        {
            UiKit.DestroyChildren(_handRow);
            _cardViews.Clear();

            int n = _state.Hand.Count;

            // ---- 布局：一排错叠。张数越多叠得越紧，但每张仍露出左侧费用与顶部卡名 ----
            float step = n <= 1 ? 0f : Mathf.Min(CardW + CardGap, (HandSpan - CardW) / (n - 1));
            float total = CardW + step * Mathf.Max(0, n - 1);

            for (int i = 0; i < n; i++)
            {
                var card = _state.Hand[i];

                float x = -total * 0.5f + CardW * 0.5f + i * step;

                // 中间擑起：让叠压的牌也能看出层次（越靠中间越高）
                float p = n <= 1 ? 0.5f : (float)i / (n - 1);
                float arc = ArcHeight * (1f - Mathf.Pow(2f * p - 1f, 2f));

                var root = UiKit.CreateRect(_handRow, "Card" + i);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
                root.pivot = new Vector2(0.5f, 0f);
                root.anchoredPosition = new Vector2(x, arc);
                root.sizeDelta = new Vector2(CardW, CardH);

                Color typeColor = card.Data.type == CardType.Attack ? Theme.Attack
                                : card.Data.type == CardType.Skill ? Theme.Skill
                                : Theme.Power;

                // --- 卡面底纹（猫毛纹理）---
                var bg = UiKit.CreateImage(root, "Bg", Color.white, UI.Assets2.CardBg);
                var bgrt = bg.rectTransform;
                UiKit.Stretch(bgrt);
                bg.type = Image.Type.Simple;
                bg.raycastTarget = true;

                if (bg.sprite == null)
                {
                    // 素材缺失时的降级：纯色底
                    bg.sprite = UiKit.RoundRect;
                    bg.type = Image.Type.Sliced;
                    bg.color = typeColor;
                }

                bool affordable = _state.Energy >= card.Cost && _state.PlayerTurnActive;
                float dim = affordable ? 1f : (_state.PlayerTurnActive ? 0.5f : 0.62f);
                bg.color = new Color(dim, dim, dim, 1f);

                // --- 卡面主图（固定占上半部，不拉伸变形）---
                var artSprite = UI.Assets2.CardArt(card.Data.type);
                if (artSprite != null)
                {
                    var artArea = UiKit.CreateRect(root, "ArtArea");
                    artArea.anchorMin = new Vector2(0f, 0.5f);   // 只占上半部分
                    artArea.anchorMax = new Vector2(1f, 1f);
                    artArea.offsetMin = new Vector2(4f, 2f);
                    artArea.offsetMax = new Vector2(-4f, -4f);

                    var art = UiKit.CreateImage(artArea, "Art", bg.color, artSprite);
                    art.preserveAspect = true;      // 保持比例（不拉伸）
                    art.raycastTarget = false;
                    UI.Assets2.ApplyWhiteKey(art, 0.80f, 0.14f);
                    UiKit.Stretch(art.rectTransform);
                }

                // 上半部淡入的暗色渐变，保证白字可读
                var scrim = UiKit.CreateImage(root, "Scrim", new Color(0f, 0f, 0f, 0.16f * dim));
                var srt = scrim.rectTransform;
                srt.anchorMin = new Vector2(0f, 0.42f);
                srt.anchorMax = new Vector2(1f, 1f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                scrim.raycastTarget = false;

                // 左右下三边的类型色描边（不加额外底框）
                var frame = UiKit.CreateImage(root, "Frame", typeColor.WithAlpha(0.85f));
                var frt = frame.rectTransform;
                UiKit.Stretch(frt);
                frame.type = Image.Type.Sliced;
                frame.sprite = UiKit.RoundRect;
                frame.color = new Color(typeColor.r, typeColor.g, typeColor.b, 0f);
                frame.raycastTarget = false;

                // --- 费用圆标 ---
                var costCircle = UiKit.CreateImage(root, "CostCircle", Theme.EnergyCyan, UiKit.Circle);
                var ccrt = costCircle.rectTransform;
                ccrt.anchorMin = ccrt.anchorMax = new Vector2(0f, 1f);
                ccrt.pivot = new Vector2(0f, 1f);
                ccrt.anchoredPosition = new Vector2(8f, -8f);
                ccrt.sizeDelta = new Vector2(52f, 52f);

                var costT = UiKit.CreateText(costCircle.transform, "Cost", card.Cost.ToString(),
                    Theme.FontBody, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(costT.rectTransform);

                // --- 卡名（下半部顶端）---
                var nameT = UiKit.CreateOutlinedText(root, "Name", card.Name, Theme.FontSmall,
                    Color.white, new Color(0f, 0f, 0f, 0.75f), TextAnchor.MiddleCenter, FontStyle.Bold);
                var nrt = nameT.rectTransform;
                nrt.anchorMin = new Vector2(0f, 0.5f);
                nrt.anchorMax = new Vector2(1f, 0.5f);
                nrt.pivot = new Vector2(0.5f, 1f);
                nrt.anchoredPosition = new Vector2(0f, -6f);
                nrt.sizeDelta = new Vector2(-16f, 40f);

                // --- 描述（下半部）---
                var descT = UiKit.CreateOutlinedText(root, "Desc", card.Description, Theme.FontTiny,
                    Color.white, new Color(0f, 0f, 0f, 0.75f), TextAnchor.UpperCenter);
                var drt = descT.rectTransform;
                drt.anchorMin = new Vector2(0f, 0f);
                drt.anchorMax = new Vector2(1f, 0.5f);
                drt.offsetMin = new Vector2(12f, 12f);
                drt.offsetMax = new Vector2(-12f, -48f);

                // 选中提示条（默认隐藏）
                var hint = UiKit.CreateOutlinedText(root, "Hint",
                    card.Data.target == TargetMode.SingleEnemy ? "选择目标" : "再点一次打出",
                    Theme.FontTiny, Theme.Gold, new Color(0f, 0f, 0f, 0.85f),
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                var hintRt = hint.rectTransform;
                hintRt.anchorMin = new Vector2(0f, 1f);
                hintRt.anchorMax = new Vector2(1f, 1f);
                hintRt.pivot = new Vector2(0.5f, 0f);
                hintRt.anchoredPosition = new Vector2(0f, 8f);
                hintRt.sizeDelta = new Vector2(-8f, 26f);
                hint.gameObject.SetActive(false);

                var btn = root.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                int handIdx = i;
                btn.onClick.AddListener(() => OnCardClicked(handIdx));

                _cardViews.Add(new CardView
                {
                    handIndex = i, root = root, bg = bg, cost = costT, name = nameT, desc = descT,
                    hint = hint, button = btn, baseY = arc
                });
            }

            ApplySelectionHighlight();
        }

        private void ApplySelectionHighlight()
        {
            foreach (var cv in _cardViews)
            {
                bool selected = cv.handIndex == _selectedHandIndex;

                cv.root.anchoredPosition = new Vector2(cv.root.anchoredPosition.x, selected ? cv.baseY + 56f : cv.baseY);
                cv.root.localScale = Vector3.one * (selected ? 1.08f : 1f);

                // 选中牌置顶，避免被右侧牌遮住；未选中的卡保持原图层
                cv.root.SetSiblingIndex(selected ? _cardViews.Count - 1 : cv.handIndex);

                if (cv.hint != null)
                    cv.hint.gameObject.SetActive(selected);
            }
        }

        // ------------------------------------------------------------------
        // 刷新
        // ------------------------------------------------------------------

        private void RefreshAll()
        {
            if (_state == null) return;

            _turnText.text = $"第 {_state.TurnNumber} 回合";

            // 玩家
            UiKit.SetBarFill(_playerHpFill, _state.Player.maxHp > 0 ? (float)_state.Player.hp / _state.Player.maxHp : 0f);
            _playerHpText.text = $"{_state.Player.hp}/{_state.Player.maxHp}";
            _playerBlockText.text = _state.Player.block.ToString();
            _playerStatusText.text = FormatStatuses(_state.Player);

            // 能量
            RefreshEnergy();

            // 敌人
            for (int i = 0; i < _enemyViews.Count && i < _state.Enemies.Count; i++)
            {
                var ev = _enemyViews[i];
                var en = _state.Enemies[i];

                UiKit.SetBarFill(ev.hpFill, en.maxHp > 0 ? (float)en.hp / en.maxHp : 0f);
                ev.hpText.text = en.IsDead ? "已击败" : $"{en.hp}/{en.maxHp}";
                ev.intentText.text = en.IsDead ? "—" : en.IntentText();
                ev.statusText.text = en.IsDead ? "" : FormatStatuses(en);

                if (!ev.root.TryGetComponent<CanvasGroup>(out var cg))
                    cg = ev.root.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = en.IsDead ? 0.35f : 1f;
                ev.button.interactable = !en.IsDead;
            }

            // 手牌与按钮
            RebuildHand();
            UiKit.SetButtonEnabled(_endTurnBtn, _state.Phase == BattlePhase.PlayerAction && !_busy);

            // 胜负结算钩子：幂等，无论从哪条路径进入终局都会触发
            if (!_endHandled)
            {
                if (_state.Phase == BattlePhase.Victory)
                {
                    _endHandled = true;
                    StartCoroutine(OnVictory());
                }
                else if (_state.Phase == BattlePhase.Defeat)
                {
                    _endHandled = true;
                    StartCoroutine(OnDefeat());
                }
            }
        }

        private void RefreshEnergy()
        {
            UiKit.DestroyChildren(_energyRow);

            // 行动点：圆点从左到右排列（不占用 _energyText，因为它已固定在玩家区）
            int max = Mathf.Max(_state.EnergyMax, 1);
            float size = Mathf.Min(38f, 300f / max - 10f);
            float step = size + 10f;

            for (int i = 0; i < max; i++)
            {
                bool filled = i < _state.Energy;
                var orb = UiKit.CreateImage(_energyRow, "Orb" + i,
                    filled ? Theme.EnergyCyan : Theme.PanelBg2.WithAlpha(0.55f), UiKit.Circle);
                var rt = orb.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2((i + 0.5f) * step, 0f);
            }

            if (_energyText != null)
                _energyText.text = $"{_state.Energy}/{_state.EnergyMax}";
        }

        private static string FormatStatuses(BattleUnit u)
        {
            if (u.statuses.Count == 0) return "";
            var parts = new List<string>();
            foreach (var kv in u.statuses)
                parts.Add($"{CardData.StatusName(kv.Key)} {kv.Value}");
            return string.Join("  ", parts);
        }

        private void OnLog(string line)
        {
            if (_logText == null) return;
            var all = _state.Log;
            int from = Mathf.Max(0, all.Count - 7);
            var sb = new System.Text.StringBuilder();
            for (int i = from; i < all.Count; i++) sb.AppendLine(all[i]);
            _logText.text = sb.ToString();
        }

        // ------------------------------------------------------------------
        // 交互
        // ------------------------------------------------------------------

        private void OnCardClicked(int handIndex)
        {
            if (_busy || _state.Phase != BattlePhase.PlayerAction) return;
            if (handIndex < 0 || handIndex >= _state.Hand.Count) return;

            // 卡牌与道具选中互斥
            if (_selectedItemSlot >= 0) ClearItemSelection();

            var card = _state.Hand[handIndex];

            // ---- 防误触（0.8 秒确认窗口）----
            // 第一次点击：选中并抬手，同时开始计时
            //   · 0.8 秒内再点同一张 → 确认打出
            //   · 超过 0.8 秒再点同一张 → 视为取消（不打出）
            //   · 点其他牌 → 只切换选中，任何牌都不打出
            if (_selectedHandIndex != handIndex)
            {
                SelectCard(handIndex);
                return;
            }

            if (!IsWithinConfirmWindow())
            {
                // 窗口已过：本次点击只做“取消”
                ClearSelection();
                return;
            }

            // 窗口内二次点击 = 确认
            if (card.Data.target == TargetMode.SingleEnemy)
            {
                // 需要目标：保持选中，等待点击敌人
                _selectedAt = Time.unscaledTime;   // 重新计时，方便瞄准
                ApplySelectionHighlight();
                return;
            }

            // 无目标牌：打出
            ClearSelection();
            _state.PlayCard(handIndex, 0);
            AfterPlayerAction();
        }

        /// <summary>选中一张牌，并在 0.8 秒后自动取消。</summary>
        private void SelectCard(int handIndex)
        {
            _selectedHandIndex = handIndex;
            _selectedAt = Time.unscaledTime;
            ApplySelectionHighlight();
            CancelInvoke(nameof(ClearSelection));
            Invoke(nameof(ClearSelection), ConfirmWindow);
        }

        /// <summary>取消选中（超时或手动）。</summary>
        private void ClearSelection()
        {
            CancelInvoke(nameof(ClearSelection));
            if (_selectedHandIndex < 0) return;
            _selectedHandIndex = -1;
            ApplySelectionHighlight();
        }

        private bool IsWithinConfirmWindow()
            => Time.unscaledTime - _selectedAt <= ConfirmWindow;

        /// <summary>选中后的确认时间窗口（秒）。见需求：“0.8 秒内点同一张算确认”。</summary>
        private const float ConfirmWindow = 0.8f;
        private float _selectedAt;

        private void OnEnemyClicked(int enemyIndex)
        {
            if (_busy || _state.Phase != BattlePhase.PlayerAction) return;

            // 道具优先：处于道具选目标状态时，点敌人 = 使用道具
            if (_selectedItemSlot >= 0)
            {
                var run = GameSession.Instance.Run;
                if (run != null && _selectedItemSlot < run.items.Count)
                {
                    var item = ItemDb.Get(run.items[_selectedItemSlot]);
                    if (item != null) { UseItemAt(_selectedItemSlot, item, enemyIndex); return; }
                }
                ClearItemSelection();
                return;
            }

            if (_selectedHandIndex < 0) return;

            int handIdx = _selectedHandIndex;
            ClearSelection();

            if (!_state.PlayCard(handIdx, enemyIndex))
            {
                ApplySelectionHighlight();
                return;
            }
            AfterPlayerAction();
        }

        private void AfterPlayerAction()
        {
            ApplySelectionHighlight();
            RefreshAll();
        }

        private void OnEndTurn()
        {
            if (_busy || _state.Phase != BattlePhase.PlayerAction) return;
            ClearSelection();
            if (_selectedItemSlot >= 0) ClearItemSelection();
            StartCoroutine(EnemyTurnRoutine());
        }

        private IEnumerator EnemyTurnRoutine()
        {
            _busy = true;
            _state.EndTurn();
            RefreshAll();

            yield return new WaitForSeconds(0.35f);

            _state.BeginEnemyTurn();
            RefreshAll();

            for (int i = 0; i < _state.Enemies.Count; i++)
            {
                if (_state.Enemies[i].IsDead) continue;
                if (_state.Phase == BattlePhase.Defeat) break;

                _state.EnemyAct(i);
                RefreshAll();
                yield return new WaitForSeconds(0.55f);
            }

            yield return new WaitForSeconds(0.2f);

            if (_state.Phase == BattlePhase.Defeat)
            {
                _busy = false;
                yield break;
            }
            if (_state.Phase == BattlePhase.Victory)
            {
                _busy = false;
                yield break;
            }

            _state.FinishEnemyTurn();
            _busy = false;
            RefreshAll();
        }

        private void Update()
        {
            if (InputCompat.KeyDown(Key.Escape))
            {
                if (_selectedItemSlot >= 0) ClearItemSelection();
                else if (_selectedHandIndex >= 0) ClearSelection();
            }

            if (InputCompat.KeyDown(Key.E) && _state != null && _state.Phase == BattlePhase.PlayerAction && !_busy)
                OnEndTurn();
        }

        // ------------------------------------------------------------------
        // 调试接口（开发期用，不参与玩法）
        // ------------------------------------------------------------------

        /// <summary>把手上能打的牌全部打掉，返回打出张数。</summary>
        public int DebugPlayAllCards()
        {
            if (_state == null) return 0;
            int played = 0;

            for (int guard = 0; guard < 30; guard++)
            {
                if (_state.Phase != BattlePhase.PlayerAction) break;

                int pick = -1;
                int target = -1;
                for (int i = 0; i < _state.Hand.Count; i++)
                {
                    var card = _state.Hand[i];
                    if (card.Cost > _state.Energy) continue;

                    if (card.Data.target == TargetMode.SingleEnemy)
                    {
                        for (int e = 0; e < _state.Enemies.Count; e++)
                        {
                            if (!_state.Enemies[e].IsDead) { target = e; break; }
                        }
                        if (target < 0) continue;
                    }
                    pick = i;
                    break;
                }

                if (pick < 0) break;

                var chosen = _state.Hand[pick];
                int tgt = chosen.Data.target == TargetMode.SingleEnemy ? target : 0;

                if (!_state.PlayCard(pick, tgt)) break;
                played++;
            }

            RefreshAll();
            return played;
        }

        /// <summary>调试：额外抽指定张数（用于验证 6~10 张手牌布局）。</summary>
        public int DebugDraw(int count)
        {
            if (_state == null) return 0;
            int drawn = _state.DrawCards(count);
            RefreshAll();
            return drawn;
        }

        /// <summary>当前战斗概况（调试用）。</summary>
        public string DebugSnapshot()
        {
            if (_state == null) return "no state";
            var sb = new System.Text.StringBuilder();
            sb.Append($"phase={_state.Phase} turn={_state.TurnNumber} energy={_state.Energy}/{_state.EnergyMax} ");
            sb.Append($"hp={_state.Player.hp}/{_state.Player.maxHp} block={_state.Player.block} ");
            sb.Append($"hand={_state.Hand.Count} draw={_state.DrawPile.Count} discard={_state.DiscardPile.Count}");
            for (int i = 0; i < _state.Enemies.Count; i++)
            {
                var e = _state.Enemies[i];
                sb.Append($" | [{i}]{e.name} {e.hp}/{e.maxHp} blk={e.block}");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // 结算
        // ------------------------------------------------------------------

        private IEnumerator OnVictory()
        {
            _busy = true;
            yield return new WaitForSeconds(0.6f);

            var run = GameSession.Instance.Run;
            int hpAfter = _state.Player.hp;

            // 遗物：燃烧之血 —— 战斗结束回复 6 点
            if (run != null && run.relics.Contains("relic_burning_blood"))
                hpAfter = Mathf.Min(run.maxHp, hpAfter + 6);

            int goldGained = _state.Enemies.Count * 12 + (_request.kind == GameSession.NodeKind.Elite ? 40 : 0)
                           + (_request.kind == GameSession.NodeKind.Boss ? 80 : 0);

            ShowVictoryOverlay(hpAfter, goldGained);
        }

        private void ShowVictoryOverlay(int hpAfter, int goldGained)
        {
            UiKit.DestroyChildren(_overlay);
            UiKit.CreateModalShade(_overlay);

            var panel = UiKit.CreateDialog(_overlay, "战斗胜利", new Vector2(1100f, 680f));

            var info = UiKit.CreateText(panel, "Info",
                $"恢复生命 +6（燃烧之血）　获得 {goldGained} 金币", Theme.FontBody, Theme.Paper);
            UiKit.AnchorCenter(info.rectTransform, new Vector2(0f, 250f), new Vector2(900f, 50f));

            var pick = UiKit.CreateText(panel, "Pick", "选择一张卡加入卡组（或跳过）",
                Theme.FontSmall, Theme.Gold);
            UiKit.AnchorCenter(pick.rectTransform, new Vector2(0f, 190f), new Vector2(900f, 40f));

            var rng = new System.Random(GameSession.Instance.Run.seed + 999 + _state.TurnNumber);
            var rewards = CardDb.RollRewards(rng);

            float x = -380f;
            for (int i = 0; i < rewards.Count; i++)
            {
                var data = rewards[i];
                var btn = UiKit.CreateButton(panel, "Reward" + i,
                    $"{data.name}\n\n{data.Describe(false)}\n\n[{CardTypeName(data.type)}]",
                    Theme.FontSmall, ColorForCardType(data.type), Color.white, () =>
                    {
                        GameSession.Instance.Run.deck.Add(new CardRef(data.id));
                        FinishAndReturn(hpAfter, goldGained);
                    });
                UiKit.AnchorCenter(btn.GetComponent<RectTransform>(), new Vector2(x, 0f), new Vector2(320f, 300f));
                x += 340f;
            }

            var skip = UiKit.CreateButton(panel, "Skip", "跳过奖励", Theme.FontBody, Theme.InkSoft, Theme.Paper,
                () => FinishAndReturn(hpAfter, goldGained));
            UiKit.AnchorCenter(skip.GetComponent<RectTransform>(), new Vector2(0f, -240f), new Vector2(300f, 72f));
        }

        private static Color ColorForCardType(CardType t)
        {
            switch (t)
            {
                case CardType.Attack: return Theme.Attack;
                case CardType.Skill: return Theme.Skill;
                case CardType.Power: return Theme.Power;
                default: return Theme.InkSoft;
            }
        }

        private static string CardTypeName(CardType t)
        {
            switch (t)
            {
                case CardType.Attack: return "攻击";
                case CardType.Skill: return "技能";
                case CardType.Power: return "能力";
                default: return "其他";
            }
        }

        private void FinishAndReturn(int hpAfter, int goldGained)
        {
            GameSession.Instance.FinishBattle(hpAfter, goldGained, victory: true);
            GameSession.Instance.GoMap();
        }

        private IEnumerator OnDefeat()
        {
            _busy = true;
            yield return new WaitForSeconds(0.5f);

            UiKit.DestroyChildren(_overlay);
            UiKit.CreateModalShade(_overlay);

            var panel = UiKit.CreateDialog(_overlay, "你倒下了", new Vector2(820f, 520f));

            var info = UiKit.CreateText(panel, "Info",
                $"坚持了 {_state.TurnNumber} 个回合\n\n这一局到此为止。",
                Theme.FontBody, Theme.Paper);
            UiKit.AnchorCenter(info.rectTransform, new Vector2(0f, 40f), new Vector2(700f, 140f));

            var back = UiKit.CreateButton(panel, "BackToMenu", "返回主菜单", Theme.FontBody,
                Theme.Bad, Theme.Paper, () =>
                {
                    SaveService.DeleteRun();
                    GameSession.Instance.GoMainMenu();
                });
            UiKit.AnchorCenter(back.GetComponent<RectTransform>(), new Vector2(0f, -150f), new Vector2(340f, 76f));
        }
    }
}
