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
        private const float HandSpan = 1000f;
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
            public Button button;
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

            RebuildEnemies();
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnStateChanged -= RefreshAll;
                _state.OnLogged -= OnLog;
            }
        }

        // ------------------------------------------------------------------
        // UI 构建
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            _canvas = UiKit.CreateCanvas("BattleCanvas", 10);

            var bg = UiKit.CreateImage(_canvas.transform, "Bg", Theme.Paper);
            UiKit.Stretch(bg.rectTransform);

            _root = UiKit.CreateRect(_canvas.transform, "Root");
            UiKit.Stretch(_root);

            BuildTopBar();
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

        private void BuildPlayerPanel()
        {
            var panel = UiKit.CreatePanel(_root, "PlayerPanel", Theme.PanelBg.WithAlpha(0.88f));
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(40f, 380f);
            rt.sizeDelta = new Vector2(420f, 240f);

            // 立绘占位
            var portrait = UiKit.CreateImage(panel.transform, "Portrait", Theme.PaperDark, UiKit.Circle);
            var prt = portrait.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(20f, -20f);
            prt.sizeDelta = new Vector2(120f, 120f);

            var pchar = UiKit.CreateText(portrait.transform, "Char", "猫", Theme.FontTitle, Theme.Ink);
            UiKit.Stretch(pchar.rectTransform);

            var nameT = UiKit.CreateText(panel.transform, "Name", "玩家", Theme.FontBody, Theme.Paper, TextAnchor.MiddleLeft);
            var nrt = nameT.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0f, 1f);
            nrt.pivot = new Vector2(0f, 1f);
            nrt.anchoredPosition = new Vector2(160f, -28f);
            nrt.sizeDelta = new Vector2(240f, 44f);

            // 生命条
            var hbrt = UiKit.CreateRect(panel.transform, "HpBarRoot");
            hbrt.anchorMin = hbrt.anchorMax = new Vector2(0f, 1f);
            hbrt.pivot = new Vector2(0f, 1f);
            hbrt.anchoredPosition = new Vector2(160f, -84f);
            hbrt.sizeDelta = new Vector2(240f, 32f);

            _playerHpFill = UiKit.CreateProgressBar(hbrt.transform, "Hp", new Vector2(240f, 32f),
                Theme.HpBack, Theme.HpRed, segments: 8);
            var hprt = _playerHpFill.transform.parent.GetComponent<RectTransform>();
            UiKit.Stretch(hprt);

            _playerHpText = UiKit.CreateText(panel.transform, "HpText", "80/80", Theme.FontSmall, Theme.Paper);
            var hptrt = _playerHpText.rectTransform;
            hptrt.anchorMin = hptrt.anchorMax = new Vector2(0f, 1f);
            hptrt.pivot = new Vector2(0f, 1f);
            hptrt.anchoredPosition = new Vector2(160f, -120f);
            hptrt.sizeDelta = new Vector2(240f, 30f);

            // 格挡
            var blockCircle = UiKit.CreateImage(panel.transform, "BlockCircle", Theme.BlockBlue, UiKit.Circle);
            var brt = blockCircle.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-24f, -20f);
            brt.sizeDelta = new Vector2(84f, 84f);

            _playerBlockText = UiKit.CreateText(blockCircle.transform, "Val", "0", Theme.FontHead, Color.white);
            UiKit.Stretch(_playerBlockText.rectTransform);

            var blockCap = UiKit.CreateText(panel.transform, "BlockCap", "格挡", Theme.FontTiny, Theme.InkFaint);
            var bct = blockCap.rectTransform;
            bct.anchorMin = bct.anchorMax = new Vector2(0f, 1f);
            bct.pivot = new Vector2(1f, 1f);
            bct.anchoredPosition = new Vector2(-24f, -108f);
            bct.sizeDelta = new Vector2(84f, 24f);

            // 状态行
            _playerStatusText = UiKit.CreateText(panel.transform, "Statuses", "", Theme.FontTiny,
                Theme.Gold, TextAnchor.MiddleLeft);
            var srt = _playerStatusText.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0f);
            srt.pivot = new Vector2(0f, 0f);
            srt.anchoredPosition = new Vector2(20f, 16f);
            srt.sizeDelta = new Vector2(380f, 100f);
            _playerStatusText.lineSpacing = 1.15f;

            // 能量
            var energyLabel = UiKit.CreateText(panel.transform, "EnergyCap", "能量", Theme.FontTiny,
                Theme.InkFaint, TextAnchor.MiddleRight);
            var elrt = energyLabel.rectTransform;
            elrt.anchorMin = elrt.anchorMax = new Vector2(1f, 0f);
            elrt.pivot = new Vector2(1f, 0f);
            elrt.anchoredPosition = new Vector2(-160f, 18f);
            elrt.sizeDelta = new Vector2(80f, 26f);

            _energyRow = UiKit.CreateRect(panel.transform, "EnergyRow");
            _energyRow.anchorMin = _energyRow.anchorMax = new Vector2(1f, 0f);
            _energyRow.pivot = new Vector2(1f, 0f);
            _energyRow.anchoredPosition = new Vector2(-24f, 46f);
            _energyRow.sizeDelta = new Vector2(240f, 48f);
        }

        private void BuildEnemyArea()
        {
            _enemyRow = UiKit.CreateRect(_root, "EnemyRow");
            _enemyRow.anchorMin = _enemyRow.anchorMax = new Vector2(0.5f, 1f);
            _enemyRow.pivot = new Vector2(0.5f, 1f);
            _enemyRow.anchoredPosition = new Vector2(120f, -180f);
            _enemyRow.sizeDelta = new Vector2(1100f, 380f);
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
            var panel = UiKit.CreatePanel(_root, "LogPanel", Theme.PanelBg.WithAlpha(0.82f));
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-40f, -110f);
            rt.sizeDelta = new Vector2(400f, 190f);
            panel.raycastTarget = false;

            _logText = UiKit.CreateText(panel.transform, "Log", "", Theme.FontTiny, Theme.Paper, TextAnchor.LowerLeft);
            var lrt = _logText.rectTransform;
            UiKit.Stretch(lrt, 14f);
            _logText.lineSpacing = 1.1f;
        }

        // ------------------------------------------------------------------
        // 敌人视图
        // ------------------------------------------------------------------

        private void RebuildEnemies()
        {
            UiKit.DestroyChildren(_enemyRow);
            _enemyViews.Clear();

            int n = _state.Enemies.Count;
            float step = n <= 1 ? 0f : Mathf.Min(EnemyWidth + 60f, 900f / Mathf.Max(1, n - 1));
            float total = EnemyWidth + step * (n - 1);

            for (int i = 0; i < n; i++)
            {
                var enemy = _state.Enemies[i];
                float x = -total * 0.5f + EnemyWidth * 0.5f + i * step;

                var root = UiKit.CreateRect(_enemyRow, "Enemy" + i);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
                root.pivot = new Vector2(0.5f, 1f);
                root.anchoredPosition = new Vector2(x, 0f);
                root.sizeDelta = new Vector2(EnemyWidth, 360f);

                // 意图气泡
                var intentBg = UiKit.CreatePanel(root, "Intent", Theme.PanelBg.WithAlpha(0.85f));
                var ibrt = intentBg.rectTransform;
                ibrt.anchorMin = ibrt.anchorMax = new Vector2(0.5f, 1f);
                ibrt.pivot = new Vector2(0.5f, 1f);
                ibrt.anchoredPosition = new Vector2(0f, 0f);
                ibrt.sizeDelta = new Vector2(180f, 56f);

                var intentText = UiKit.CreateText(intentBg.transform, "IntentText", "", Theme.FontSmall, Theme.Paper);
                UiKit.Stretch(intentText.rectTransform, 4f);

                // 敌人立绘占位
                var portrait = UiKit.CreateImage(root, "Portrait", Theme.PaperDark, UiKit.Circle, raycast: true);
                var prt = portrait.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
                prt.pivot = new Vector2(0.5f, 1f);
                prt.anchoredPosition = new Vector2(0f, -70f);
                prt.sizeDelta = new Vector2(150f, 150f);

                var glyph = UiKit.CreateText(portrait.transform, "Glyph", "敌", Theme.FontTitle, Theme.Ink);
                UiKit.Stretch(glyph.rectTransform);

                var nameT = UiKit.CreateText(root, "Name", enemy.name, Theme.FontSmall, Theme.Ink,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                var nrt = nameT.rectTransform;
                nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 1f);
                nrt.pivot = new Vector2(0.5f, 1f);
                nrt.anchoredPosition = new Vector2(0f, -228f);
                nrt.sizeDelta = new Vector2(EnemyWidth, 30f);

                // 血条
                var hprt = UiKit.CreateRect(root, "HpRoot");
                hprt.anchorMin = hprt.anchorMax = new Vector2(0.5f, 1f);
                hprt.pivot = new Vector2(0.5f, 1f);
                hprt.anchoredPosition = new Vector2(0f, -262f);
                hprt.sizeDelta = new Vector2(EnemyWidth - 50f, 26f);

                var fill = UiKit.CreateProgressBar(hprt, "Hp", new Vector2(EnemyWidth - 50f, 26f),
                    Theme.HpBack, Theme.HpRed);
                UiKit.Stretch(fill.transform.parent.GetComponent<RectTransform>());

                var hpText = UiKit.CreateText(root, "HpText", "", Theme.FontTiny, Theme.Ink);
                var hptrt = hpText.rectTransform;
                hptrt.anchorMin = hptrt.anchorMax = new Vector2(0.5f, 1f);
                hptrt.pivot = new Vector2(0.5f, 1f);
                hptrt.anchoredPosition = new Vector2(0f, -290f);
                hptrt.sizeDelta = new Vector2(EnemyWidth, 26f);

                var statusT = UiKit.CreateText(root, "Status", "", Theme.FontTiny, Theme.Gold);
                var strt = statusT.rectTransform;
                strt.anchorMin = strt.anchorMax = new Vector2(0.5f, 1f);
                strt.pivot = new Vector2(0.5f, 1f);
                strt.anchoredPosition = new Vector2(0f, -316f);
                strt.sizeDelta = new Vector2(EnemyWidth, 40f);

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
            float step = n <= 1 ? 0f : Mathf.Min(CardW, (HandSpan - CardW) / (n - 1));
            float total = CardW + step * Mathf.Max(0, n - 1);

            for (int i = 0; i < n; i++)
            {
                var card = _state.Hand[i];
                float x = -total * 0.5f + CardW * 0.5f + i * step;

                var root = UiKit.CreateRect(_handRow, "Card" + i);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
                root.pivot = new Vector2(0.5f, 0f);
                root.anchoredPosition = new Vector2(x, 0f);
                root.sizeDelta = new Vector2(CardW, CardH);

                Color typeColor = card.Data.type == CardType.Attack ? Theme.Attack
                                : card.Data.type == CardType.Skill ? Theme.Skill
                                : Theme.Power;

                var bg = UiKit.CreatePanel(root, "Bg", typeColor);
                UiKit.Stretch(bg.rectTransform);
                bg.raycastTarget = true;

                bool affordable = _state.Energy >= card.Cost && _state.PlayerTurnActive;
                if (!affordable) bg.color = new Color(typeColor.r * 0.5f, typeColor.g * 0.5f, typeColor.b * 0.5f, 0.95f);

                // 费用圆标
                var costCircle = UiKit.CreateImage(root, "CostCircle", Theme.EnergyCyan, UiKit.Circle);
                var ccrt = costCircle.rectTransform;
                ccrt.anchorMin = ccrt.anchorMax = new Vector2(0f, 1f);
                ccrt.pivot = new Vector2(0f, 1f);
                ccrt.anchoredPosition = new Vector2(8f, -8f);
                ccrt.sizeDelta = new Vector2(52f, 52f);

                var costT = UiKit.CreateText(costCircle.transform, "Cost", card.Cost.ToString(),
                    Theme.FontBody, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(costT.rectTransform);

                var nameT = UiKit.CreateText(root, "Name", card.Name, Theme.FontSmall, Color.white,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                var nrt = nameT.rectTransform;
                nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 1f);
                nrt.pivot = new Vector2(0.5f, 1f);
                nrt.anchoredPosition = new Vector2(0f, -14f);
                nrt.sizeDelta = new Vector2(CardW - 76f, 40f);

                var descT = UiKit.CreateText(root, "Desc", card.Description, Theme.FontTiny, Color.white,
                    TextAnchor.UpperCenter);
                var drt = descT.rectTransform;
                drt.anchorMin = new Vector2(0f, 0f);
                drt.anchorMax = new Vector2(1f, 1f);
                drt.offsetMin = new Vector2(14f, 14f);
                drt.offsetMax = new Vector2(-14f, -66f);

                var btn = root.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                int handIdx = i;
                btn.onClick.AddListener(() => OnCardClicked(handIdx));

                _cardViews.Add(new CardView
                {
                    handIndex = i, root = root, bg = bg, cost = costT, name = nameT, desc = descT, button = btn
                });
            }

            ApplySelectionHighlight();
        }

        private void ApplySelectionHighlight()
        {
            foreach (var cv in _cardViews)
            {
                bool selected = cv.handIndex == _selectedHandIndex;
                cv.root.anchoredPosition = new Vector2(cv.root.anchoredPosition.x, selected ? 60f : 0f);
                cv.root.localScale = Vector3.one * (selected ? 1.1f : 1f);
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

            int max = Mathf.Max(_state.EnergyMax, 1);
            float size = Mathf.Min(46f, 240f / max - 6f);
            float step = size + 8f;
            float total = size * max + 8f * (max - 1);

            for (int i = 0; i < max; i++)
            {
                bool filled = i < _state.Energy;
                var orb = UiKit.CreateImage(_energyRow, "Orb" + i,
                    filled ? Theme.EnergyCyan : Theme.PanelBg2, UiKit.Circle);
                var rt = orb.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(-(total - (i + 0.5f) * step), 0f);
            }

            if (_energyText == null)
                _energyText = UiKit.CreateText(_energyRow, "EnergyVal", "", Theme.FontTiny, Theme.Ink);

            _energyText.text = $"{_state.Energy}/{_state.EnergyMax}";
            var trt = _energyText.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(1f, 1f);
            trt.anchoredPosition = new Vector2(0f, 26f);
            trt.sizeDelta = new Vector2(140f, 24f);
            _energyText.alignment = TextAnchor.MiddleRight;
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

            var card = _state.Hand[handIndex];

            if (_selectedHandIndex == handIndex)
            {
                _selectedHandIndex = -1;
                ApplySelectionHighlight();
                return;
            }

            if (card.Data.target == TargetMode.SingleEnemy)
            {
                _selectedHandIndex = handIndex;
                ApplySelectionHighlight();
                return;
            }

            // 无目标卡：直接打出
            _selectedHandIndex = -1;
            _state.PlayCard(handIndex, 0);
            AfterPlayerAction();
        }

        private void OnEnemyClicked(int enemyIndex)
        {
            if (_busy || _state.Phase != BattlePhase.PlayerAction) return;
            if (_selectedHandIndex < 0) return;

            int handIdx = _selectedHandIndex;
            _selectedHandIndex = -1;

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
            _selectedHandIndex = -1;
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
            if (InputCompat.KeyDown(Key.Escape) && _selectedHandIndex >= 0)
            {
                _selectedHandIndex = -1;
                ApplySelectionHighlight();
            }

            if (InputCompat.KeyDown(Key.E) && _state != null && _state.Phase == BattlePhase.PlayerAction && !_busy)
                OnEndTurn();
        }

        // ------------------------------------------------------------------
        // 调试接口（开发期用，不参与玩法）
        // ------------------------------------------------------------------

        /// <summary>尽可能把手上能打的牌全部打掉，返回打出张数。</summary>
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
