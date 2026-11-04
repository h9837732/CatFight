using System.Collections.Generic;
using CatFight.Core;
using CatFight.Map;
using CatFight.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatFight.MapScene
{
    /// <summary>
    /// 地图界面。负责：
    ///   · 按 seed 重建地图（不存地图数据，只存种子 + 已走路径，见设计文档 §10）
    ///   · 渲染纸面、节点、墨线、已划掉的分支
    ///   · 处理点击推进，并按节点类型分发（战斗 / 休息 / 商店 / 宝箱 / 事件）
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        private const float NodeSize = 96f;
        private const float NodeSizeBoss = 132f;
        private const float EdgeWidth = 3f;
        private const float PanPaddingY = 260f;

        private Canvas _canvas;
        private RectTransform _mapRoot;      // 承载所有节点与边，平移它实现镜头移动
        private RectTransform _dialogLayer;

        private MapData _map;
        private MapConfig _config;
        private RunState _run;

        private int _currentId = -1;
        private readonly List<int> _available = new List<int>();
        private readonly Dictionary<int, NodeWidget> _widgets = new Dictionary<int, NodeWidget>();

        private Text _hudHp, _hudGold, _hudDeck, _hudRelic;
        private RectTransform _hud;
        private bool _inputLocked;

        private class NodeWidget
        {
            public RectTransform root;
            public Image circle;
            public Image ring;
            public Text glyph;
            public Image icon;
            public Text label;
            public Button button;
            public bool hasArt;    // 有正式猫图素材
            public RectTransform marker;   // 当前所在位置指示器
        }

        // ------------------------------------------------------------------

        private void Start()
        {
            if (GameSession.Instance == null || GameSession.Instance.Run == null)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameSession.SceneMainMenu);
                return;
            }

            _run = GameSession.Instance.Run;
            _config = MapConfig.CreateDefault();

            try
            {
                _map = MapGenerator.Generate(_run.seed, _config);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Map] 生成失败：" + ex.Message);
                return;
            }

            _currentId = _run.CurrentNodeId;

            // 新局：先站在起点（空节点）上，再开遗物三选一
            if (_run.visitedPath.Count == 0)
            {
                _run.visitedPath.Add(_map.StartId);
                _currentId = _map.StartId;
                SaveService.SaveRun(_run);
            }

            BuildUi();
            BuildMap();
            RefreshState();
            FocusOnCurrent(instant: true);

            if (_run.relics.Count == 0)
                ShowRelicChoice();
        }

        private void OnDestroy()
        {
            if (_config != null) Destroy(_config);
        }

        // ------------------------------------------------------------------
        // UI 骨架
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            _canvas = UiKit.CreateCanvas("MapCanvas", 10);

            var bg = UiKit.CreateImage(_canvas.transform, "PaperBg", Theme.Paper);
            UiKit.Stretch(bg.rectTransform);

            // 纸面暗角
            AddEdgeShade(new Vector2(0f, 1f), new Vector2(1f, 1f), 0.16f);
            AddEdgeShade(new Vector2(0f, 0f), new Vector2(1f, 0f), 0.22f);

            // 地图容器（裁剪视口内的内容）
            var viewport = UiKit.CreateRect(_canvas.transform, "MapViewport");
            UiKit.Stretch(viewport);
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.padding = new Vector4(0f, 120f, 0f, 130f);

            _mapRoot = UiKit.CreateRect(viewport, "MapRoot");
            _mapRoot.anchorMin = _mapRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _mapRoot.pivot = new Vector2(0.5f, 0.5f);
            _mapRoot.sizeDelta = new Vector2(10f, 10f);
            _mapRoot.anchoredPosition = Vector2.zero;

            BuildHud();

            _dialogLayer = UiKit.CreateRect(_canvas.transform, "DialogLayer");
            UiKit.Stretch(_dialogLayer);
        }

        private void AddEdgeShade(Vector2 aMin, Vector2 aMax, float alpha)
        {
            var img = UiKit.CreateImage(_canvas.transform, "Shade", new Color(0f, 0f, 0f, alpha));
            var rt = img.rectTransform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            if (aMin.y > 0.5f) rt.sizeDelta = new Vector2(0f, 220f);
            else rt.sizeDelta = new Vector2(0f, 150f);
        }

        private void BuildHud()
        {
            _hud = UiKit.CreatePanel(_canvas.transform, "Hud", Theme.PanelBg.WithAlpha(0.92f)).rectTransform;
            _hud.anchorMin = new Vector2(0f, 1f);
            _hud.anchorMax = new Vector2(1f, 1f);
            _hud.pivot = new Vector2(0.5f, 1f);
            _hud.sizeDelta = new Vector2(0f, 112f);
            _hud.anchoredPosition = Vector2.zero;

            _hudHp = MakeHudStat(180f, "生命", "80/80", Theme.HpRed);
            _hudGold = MakeHudStat(460f, "金币", "99", Theme.Gold);
            _hudDeck = MakeHudStat(700f, "卡组", "10", Theme.Paper);
            _hudRelic = MakeHudStat(940f, "遗物", "1", Theme.BlockBlue);

            var btnMenu = UiKit.CreateButton(_hud, "BtnMenu", "菜单", Theme.FontSmall,
                Theme.InkSoft, Theme.Paper, OpenPauseMenu);
            var brt = btnMenu.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-32f, 0f);
            brt.sizeDelta = new Vector2(150f, 68f);
        }

        private Text MakeHudStat(float x, string label, string value, Color valueColor)
        {
            var cap = UiKit.CreateText(_hud, "Cap_" + label, label, Theme.FontSmall, Theme.InkFaint, TextAnchor.MiddleLeft);
            var crt = cap.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.anchoredPosition = new Vector2(x, 16f);
            crt.sizeDelta = new Vector2(200f, 30f);

            var val = UiKit.CreateText(_hud, "Val_" + label, value, Theme.FontHead, valueColor, TextAnchor.MiddleLeft);
            var vrt = val.rectTransform;
            vrt.anchorMin = vrt.anchorMax = new Vector2(0f, 0.5f);
            vrt.pivot = new Vector2(0f, 0.5f);
            vrt.anchoredPosition = new Vector2(x, -18f);
            vrt.sizeDelta = new Vector2(240f, 40f);

            return val;
        }

        // ------------------------------------------------------------------
        // 地图渲染
        // ------------------------------------------------------------------

        private void BuildMap()
        {
            // 1) 边（先画，压在节点下面）
            var edgeRoot = UiKit.CreateRect(_mapRoot, "Edges");
            edgeRoot.anchorMin = edgeRoot.anchorMax = new Vector2(0.5f, 0.5f);
            edgeRoot.pivot = new Vector2(0.5f, 0.5f);
            edgeRoot.sizeDelta = Vector2.zero;

            foreach (var e in _map.edges)
            {
                var a = _map.GetNode(e.from);
                var b = _map.GetNode(e.to);
                if (a == null || b == null) continue;
                CreateEdge(edgeRoot, a.pos, b.pos, e);
            }

            // 2) 节点
            foreach (var node in _map.nodes)
                _widgets[node.id] = CreateNodeWidget(_mapRoot, node);
        }

        private void CreateEdge(Transform parent, Vector2 from, Vector2 to, MapEdge edge)
        {
            Vector2 delta = to - from;
            float len = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            var img = UiKit.CreateImage(parent, "Edge_" + edge.from + "_" + edge.to,
                Theme.InkFaint.WithAlpha(0.55f));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(len, EdgeWidth);
            rt.anchoredPosition = (from + to) * 0.5f;
            rt.localEulerAngles = new Vector3(0f, 0f, angle);
            img.gameObject.name = "Edge"; // 名称不含中文，避免后续查找问题

            // 记录到 widget 便于更新状态
            _edgeWidgets[edge.from + "->" + edge.to] = img;
        }

        private readonly Dictionary<string, Image> _edgeWidgets = new Dictionary<string, Image>();

        private NodeWidget CreateNodeWidget(Transform parent, MapNode node)
        {
            bool isBoss = node.type == NodeType.Boss;
            bool isStart = node.type == NodeType.Start;
            float size = isBoss ? NodeSizeBoss : (isStart ? NodeSize * 0.84f : NodeSize);
            Color typeColor = Theme.ColorFor(node.type);

            var root = UiKit.CreateRect(parent, "Node_" + node.id);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(size, size);
            root.anchoredPosition = node.pos;

            // 地图节点：有猫图素材时用图，背景保持透明（不要米色圆底）
            var nodeSprite = Assets2.NodeSprite(node.type);

            // 外发光环（可前往时显示）
            var ring = UiKit.CreateImage(root, "Ring", typeColor.WithAlpha(0f), UiKit.Ring);
            UiKit.Stretch(ring.rectTransform, -14f);
            ring.raycastTarget = false;

            // 圆底（普通节点用；有猫图时隐藏）
            var circle = UiKit.CreateImage(root, "Circle", Theme.Paper, UiKit.Circle, raycast: true);
            UiKit.Stretch(circle.rectTransform);
            if (nodeSprite != null) circle.color = Color.clear;

            // 描边（有猫图时也不要，避免图被圆环拑住）
            var border = UiKit.CreateImage(root, "Border", typeColor, UiKit.Ring);
            UiKit.Stretch(border.rectTransform, 0f);
            border.raycastTarget = false;
            if (nodeSprite != null) border.color = Color.clear;

            // 地图节点图标：有图用图，否则用类型字
            Text glyph = null;
            Image icon = null;

            if (nodeSprite != null)
            {
                icon = UiKit.CreateImage(root, "Icon", Color.white, nodeSprite);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Assets2.ApplyWhiteKey(icon, 0.78f, 0.16f);
                UiKit.Stretch(icon.rectTransform, -size * 0.10f);   // 略超出节点尺寸，让猫图更大
            }
            else
            {
                glyph = UiKit.CreateText(root, "Glyph", Theme.GlyphFor(node.type),
                    isBoss ? Theme.FontHead : Theme.FontBody, typeColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(glyph.rectTransform);
            }

            // 类型名（可前往 / 当前节点常显，见设计文档 §7.7）
            var label = UiKit.CreateText(parent, "Label_" + node.id, Theme.NameFor(node.type),
                Theme.FontTiny, Theme.InkSoft);
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.sizeDelta = new Vector2(200f, 24f);
            lrt.anchoredPosition = node.pos + new Vector2(0f, -(size * 0.5f + 6f));

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = circle;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.disabledColor = Color.white;
            btn.colors = colors;

            int nodeId = node.id;
            btn.onClick.AddListener(() => OnNodeClicked(nodeId));

            return new NodeWidget
            {
                root = root, circle = circle, ring = ring, glyph = glyph, icon = icon, label = label,
                button = btn, hasArt = nodeSprite != null, marker = CreateMarker(parent, node, size)
            };
        }

        /// <summary>
        /// “我在这里”指示器：节点下方一个下指三角 + “当前位置” 字样。
        /// 默认隐藏，只在玩家所在节点显示。
        /// </summary>
        private RectTransform CreateMarker(Transform parent, MapNode node, float size)
        {
            var marker = UiKit.CreateRect(parent, "Marker_" + node.id);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.pivot = new Vector2(0.5f, 1f);
            marker.sizeDelta = new Vector2(180f, 56f);
            marker.anchoredPosition = node.pos + new Vector2(0f, -(size * 0.5f + 34f));

            // 下指三角（用旋转的方形模拟）
            var tri = UiKit.CreateImage(marker, "Arrow", Theme.Boss);
            var trt = tri.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, 0f);
            trt.sizeDelta = new Vector2(17f, 17f);
            trt.localEulerAngles = new Vector3(0f, 0f, 45f);
            tri.raycastTarget = false;

            var txt = UiKit.CreateOutlinedText(marker, "Text", "当前位置", Theme.FontTiny,
                Theme.Boss, Theme.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            var xtrt = txt.rectTransform;
            xtrt.anchorMin = new Vector2(0f, 1f);
            xtrt.anchorMax = new Vector2(1f, 1f);
            xtrt.pivot = new Vector2(0.5f, 1f);
            xtrt.anchoredPosition = new Vector2(0f, -16f);
            xtrt.sizeDelta = new Vector2(0f, 26f);

            marker.gameObject.SetActive(false);
            return marker;
        }

        // ------------------------------------------------------------------
        // 状态刷新
        // ------------------------------------------------------------------

        /// <summary>给节点图标上色（文字与图片两种实现都兼容）。</summary>
        private static void TintGlyph(NodeWidget w, Color c)
        {
            if (w.hasArt)
            {
                // 有素材图时不染类型色（会毁掉猫的颜色），只通过透明度区分状态
                if (w.icon != null)
                {
                    float a = Mathf.Clamp01(c.a > 0.02f ? Mathf.Max(0.35f, c.a) : 0.35f);
                    w.icon.color = new Color(1f, 1f, 1f, a);
                }
            }
            else if (w.glyph != null)
            {
                w.glyph.color = c;
            }
        }

        private void RefreshState()
        {
            _available.Clear();
            if (_currentId < 0)
            {
                _available.Add(_map.StartId);
            }
            else
            {
                var cur = _map.GetNode(_currentId);
                if (cur != null) _available.AddRange(cur.next);
            }

            var visited = new HashSet<int>(_run.visitedPath);
            int currentRow = _currentId < 0 ? -1 : _map.GetNode(_currentId).row;

            foreach (var kv in _widgets)
            {
                var node = _map.GetNode(kv.Key);
                var w = kv.Value;
                if (node == null) continue;

                bool isCurrent = kv.Key == _currentId;
                bool isAvailable = _available.Contains(kv.Key);
                bool isVisited = visited.Contains(kv.Key);
                bool isReachable = isCurrent || isAvailable;
                // 已经走过更后面的行、但自己没被选中 → 墨迹划掉
                bool isCrossedOut = !isVisited && !isReachable && node.row < currentRow;

                Color typeColor = Theme.ColorFor(node.type);

                // 当前位置指示器：只在玩家所在节点显示
                if (w.marker != null) w.marker.gameObject.SetActive(isCurrent);

                if (isCrossedOut)
                {
                    if (!w.hasArt) w.circle.color = Theme.PaperDark.WithAlpha(0.55f);
                    TintGlyph(w, Theme.InkFaint.WithAlpha(0.45f));
                    w.root.localScale = Vector3.one * 0.82f;
                    w.label.text = "";
                    w.ring.color = typeColor.WithAlpha(0f);
                    w.button.interactable = false;
                }
                else if (isCurrent)
                {
                    if (!w.hasArt) w.circle.color = Theme.Paper;
                    TintGlyph(w, typeColor);
                    w.root.localScale = Vector3.one * 1.12f;
                    w.label.text = Theme.NameFor(node.type);
                    w.label.color = Theme.Ink;
                    w.ring.color = typeColor.WithAlpha(0.55f);
                    w.button.interactable = false;
                }                else if (isAvailable)
                {
                    if (!w.hasArt) w.circle.color = Theme.Paper;
                    TintGlyph(w, typeColor);
                    w.root.localScale = Vector3.one;
                    w.label.text = Theme.NameFor(node.type);
                    w.label.color = Theme.Ink;
                    w.ring.color = typeColor.WithAlpha(0.35f);
                    w.button.interactable = true;
                }
                else if (isVisited)
                {
                    if (!w.hasArt) w.circle.color = Theme.PaperDark;
                    TintGlyph(w, typeColor.WithAlpha(0.35f));
                    w.root.localScale = Vector3.one * 0.9f;
                    w.label.text = "";
                    w.ring.color = typeColor.WithAlpha(0f);
                    w.button.interactable = false;
                }
                else
                {
                    // 尚未到达的远端节点：淡墨轮廓，但要保留可辨认的几个类型图标
                    bool far = node.row > currentRow + 2;
                    if (!w.hasArt) w.circle.color = far ? Theme.PaperDark.WithAlpha(0.55f) : Theme.PaperDark;
                    TintGlyph(w, typeColor.WithAlpha(far ? 0.55f : 0.85f));
                    w.root.localScale = Vector3.one * (far ? 0.86f : 0.92f);
                    w.label.text = far ? "" : Theme.NameFor(node.type);
                    w.label.color = Theme.InkFaint;
                    w.ring.color = typeColor.WithAlpha(0f);
                    w.button.interactable = false;
                }
            }

            UpdateHud();
        }

        private void UpdateHud()
        {
            if (_run == null) return;
            _hudHp.text = $"{_run.hp}/{_run.maxHp}";
            _hudGold.text = _run.gold.ToString();
            _hudDeck.text = _run.deck.Count.ToString();
            _hudRelic.text = _run.relics.Count.ToString();
        }

        // ------------------------------------------------------------------
        // 镜头
        // ------------------------------------------------------------------

        private void FocusOnCurrent(bool instant)
        {
            // 路线自下而上：当前节点放在画面下方，上方留出将要走的路
            const float FocusScreenY = -250f;

            int id = _currentId >= 0 ? _currentId : _map.StartId;
            var node = _map.GetNode(id);
            Vector2 anchorPos = node != null
                ? new Vector2(-node.pos.x, FocusScreenY - node.pos.y)
                : Vector2.zero;

            _mapRoot.anchoredPosition = ClampPan(anchorPos);
        }

        private void Update()
        {
            if (_inputLocked || _mapRoot == null) return;

            // 滚轮 / 方向键平移
            float scroll = InputCompat.ScrollY;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var p = _mapRoot.anchoredPosition;
                p.y += scroll * 90f;
                _mapRoot.anchoredPosition = ClampPan(p);
            }

            float vy = InputCompat.VerticalAxis;
            if (Mathf.Abs(vy) > 0.01f)
            {
                var p = _mapRoot.anchoredPosition;
                p.y += vy * 900f * Time.deltaTime;
                _mapRoot.anchoredPosition = ClampPan(p);
            }
        }

        private Vector2 ClampPan(Vector2 p)
        {
            if (_map == null) return p;

            // 内容坐标：起点 y=0（屏幕下方），BOSS y=(rowCount-1)*rowSpacing（屏幕上方）
            const float FocusScreenY = -250f;
            float maxY = (_map.rowCount - 1) * _config.rowSpacing;

            float minPan = FocusScreenY - maxY;   // 看到 BOSS 时的极限
            float maxPan = FocusScreenY;          // 看到起点时的极限

            p.y = Mathf.Clamp(p.y, minPan, maxPan + 80f);
            p.x = Mathf.Clamp(p.x, -420f, 420f);
            return p;
        }

        // ------------------------------------------------------------------
        // 交互
        // ------------------------------------------------------------------

        private void OnNodeClicked(int nodeId)
        {
            if (_inputLocked) return;
            if (!_available.Contains(nodeId)) return;

            var node = _map.GetNode(nodeId);
            if (node == null) return;

            _inputLocked = true;

            // 记录已走路径（战斗节点也先记，输赢由战斗结算处理血量）
            _run.visitedPath.Add(nodeId);
            _currentId = nodeId;
            SaveService.SaveRun(_run);

            RefreshState();
            FocusOnCurrent(instant: false);

            // 用一帧延迟让按钮点击音效/视觉先反馈，再分发
            Invoke(nameof(DispatchCurrentNode), 0.35f);
        }

        private void DispatchCurrentNode()
        {
            var node = _map.GetNode(_currentId);
            if (node == null) { _inputLocked = false; return; }

            switch (node.type)
            {
                case NodeType.Battle:
                case NodeType.Elite:
                case NodeType.Boss:
                    EnterBattle(node);
                    break;

                case NodeType.Rest:
                    ShowRestDialog();
                    break;

                case NodeType.Treasure:
                    ShowTreasureDialog();
                    break;

                case NodeType.Shop:
                    ShowShopDialog();
                    break;

                case NodeType.Event:
                    ShowEventDialog();
                    break;

                default:
                    // 起点（Start）等无需分发的节点：直接解锁
                    _inputLocked = false;
                    break;
            }
        }

        private void EnterBattle(MapNode node)
        {
            var kind = node.type == NodeType.Boss ? GameSession.NodeKind.Boss
                     : node.type == NodeType.Elite ? GameSession.NodeKind.Elite
                     : GameSession.NodeKind.Battle;

            GameSession.Instance.GoBattle(new GameSession.BattleRequest
            {
                encounterId = kind == GameSession.NodeKind.Elite ? "elite_jaw_worm"
                            : kind == GameSession.NodeKind.Boss ? "boss_guardian"
                            : "normal_encounter",
                sourceNodeId = node.id,
                kind = kind,
                streak = _run.visitedPath.Count
            });
        }

        // ------------------------------------------------------------------
        // 各类节点的弹窗
        // ------------------------------------------------------------------

        private RectTransform _modal;

        private void OpenModal(string title, Vector2 size)
        {
            UiKit.DestroyChildren(_dialogLayer);
            UiKit.CreateModalShade(_dialogLayer);
            _modal = UiKit.CreateDialog(_dialogLayer, title, size);
        }

        private void CloseModalAndContinue()
        {
            UiKit.DestroyChildren(_dialogLayer);
            _modal = null;
            _inputLocked = false;
            SyncRun();
            RefreshState();
            SaveService.SaveRun(_run);
        }

        private void SyncRun()
        {
            GameSession.Instance.SetRun(_run);
        }

        /// <summary>开局：遗物三选一。选完才能选第一条路。</summary>
        private void ShowRelicChoice()
        {
            _inputLocked = true;
            OpenModal("选择一件遗物", new Vector2(1080f, 640f));

            var desc = UiKit.CreateText(_modal, "Desc", "仅此一次。选定后无法更换。",
                Theme.FontBody, Theme.Paper);
            UiKit.AnchorCenter(desc.rectTransform, new Vector2(0f, 218f), new Vector2(900f, 44f));

            var choices = Battle.RelicDb.RollChoices(_run.seed);
            float x = -340f;

            for (int i = 0; i < choices.Count; i++)
            {
                var relic = choices[i];
                Color c = RelicColor(relic.rarity);

                var btn = UiKit.CreateButton(_modal, "Relic" + i,
                    $"{relic.name}\n\n{relic.desc}",
                    Theme.FontSmall, c, Color.white, () =>
                    {
                        _run.relics.Add(relic.id);

                        // 立即生效型遗物
                        if (relic.id == "relic_purse") _run.gold += 100;

                        CloseModalAndContinue();
                        RefreshState();
                    });
                UiKit.AnchorCenter(btn.GetComponent<RectTransform>(), new Vector2(x, 10f), new Vector2(300f, 320f));

                var label = btn.transform.Find("Label")?.GetComponent<Text>();
                if (label != null)
                {
                    label.alignment = TextAnchor.UpperCenter;
                    label.rectTransform.offsetMin = new Vector2(14f, 14f);
                    label.rectTransform.offsetMax = new Vector2(-14f, -18f);
                    label.lineSpacing = 1.2f;
                }

                x += 340f;
            }
        }

        private static Color RelicColor(Battle.RelicRarity r)
        {
            switch (r)
            {
                case Battle.RelicRarity.Starter: return Theme.Rest;
                case Battle.RelicRarity.Uncommon: return Theme.Skill;
                case Battle.RelicRarity.Rare: return Theme.Power;
                default: return Theme.Shop;
            }
        }

        private void ShowRestDialog()
        {
            OpenModal("休息点", new Vector2(820f, 520f));

            var desc = UiKit.CreateText(_modal, "Desc", "篝火还在燃烧。你要做什么？",
                Theme.FontBody, Theme.Paper);
            UiKit.AnchorCenter(desc.rectTransform, new Vector2(0f, 40f), new Vector2(700f, 60f));

            int healAmount = Mathf.Max(1, Mathf.RoundToInt(_run.maxHp * 0.3f));

            var btnHeal = UiKit.CreateButton(_modal, "Heal", $"休息，恢复 {healAmount} 点生命", Theme.FontBody,
                Theme.Rest, Theme.Paper, () =>
                {
                    _run.hp = Mathf.Min(_run.maxHp, _run.hp + healAmount);
                    CloseModalAndContinue();
                });
            UiKit.AnchorCenter(btnHeal.GetComponent<RectTransform>(), new Vector2(0f, -60f), new Vector2(520f, 76f));

            var btnUpgrade = UiKit.CreateButton(_modal, "Upgrade", "升级一张卡（占位）", Theme.FontBody,
                Theme.InkSoft, Theme.Paper, () =>
                {
                    if (_run.deck.Count > 0)
                    {
                        for (int i = 0; i < _run.deck.Count; i++)
                        {
                            if (!_run.deck[i].upgraded) { _run.deck[i].upgraded = true; break; }
                        }
                    }
                    CloseModalAndContinue();
                });
            UiKit.AnchorCenter(btnUpgrade.GetComponent<RectTransform>(), new Vector2(0f, -160f), new Vector2(520f, 76f));
        }

        private void ShowTreasureDialog()
        {
            OpenModal("宝箱", new Vector2(720f, 460f));

            int gold = Random.Range(30, 70);
            string itemId = Battle.ItemDb.RollRandom(new System.Random(_run.seed + _run.visitedPath.Count * 31));
            string itemName = Battle.ItemDb.NameOf(itemId);
            bool canTakeItem = _run.items.Count < Battle.ItemDb.MaxSlots;

            var desc = UiKit.CreateText(_modal, "Desc",
                $"箱子里是一枚古旧的护符。\n\n获得 {gold} 金币\n获得遗物「占位护符」" +
                (canTakeItem ? $"\n获得道具「{itemName}」" : "\n（道具栏已满）"),
                Theme.FontBody, Theme.Paper);
            UiKit.AnchorCenter(desc.rectTransform, new Vector2(0f, 10f), new Vector2(620f, 180f));

            // 奖励在点击「收下」时才发放，避免打开弹窗就结算
            var ok = UiKit.CreateButton(_modal, "Ok", "收下", Theme.FontBody, Theme.Gold, Theme.Ink, () =>
            {
                _run.gold += gold;
                _run.relics.Add("relic_placeholder_" + _run.relics.Count);
                if (_run.items.Count < Battle.ItemDb.MaxSlots) _run.items.Add(itemId);
                CloseModalAndContinue();
            });
            UiKit.AnchorCenter(ok.GetComponent<RectTransform>(), new Vector2(0f, -170f), new Vector2(300f, 76f));
        }

        private void ShowShopDialog()
        {
            OpenModal("商店", new Vector2(980f, 620f));

            var desc = UiKit.CreateText(_modal, "Desc", $"金币：{_run.gold}",
                Theme.FontBody, Theme.Gold);
            UiKit.AnchorCenter(desc.rectTransform, new Vector2(0f, 200f), new Vector2(600f, 50f));

            string[] offers = { "cleave", "iron_wave", "shrug_it_off" };
            int[] prices = { 45, 60, 50 };

            float x = -320f;
            for (int i = 0; i < offers.Length; i++)
            {
                string cardId = offers[i];
                int price = prices[i];

                var btn = UiKit.CreateButton(_modal, "Offer" + i,
                    $"{CardName(cardId)}\n{price} 金币", Theme.FontSmall, Theme.PanelBg2, Theme.Paper, () =>
                    {
                        if (_run.gold < price) { return; }
                        _run.gold -= price;
                        _run.deck.Add(new CardRef(cardId));
                        CloseModalAndContinue();
                    });
                UiKit.AnchorCenter(btn.GetComponent<RectTransform>(), new Vector2(x, 70f), new Vector2(270f, 200f));
                x += 300f;
            }

            // 道具（药水）也卖，因为存档只存 id，价格按顺序固定
            string itemId = Battle.ItemDb.RollRandom(new System.Random(_run.seed + 500));
            int itemPrice = 60;
            bool slotFree = _run.items.Count < Battle.ItemDb.MaxSlots;

            var itemBtn = UiKit.CreateButton(_modal, "BuyItem",
                slotFree
                    ? $"道具\n{Battle.ItemDb.NameOf(itemId)}\n{itemPrice} 金币"
                    : $"道具栏已满\n（{Battle.ItemDb.MaxSlots}/{Battle.ItemDb.MaxSlots}）",
                Theme.FontSmall, slotFree ? Theme.Shop : Theme.PanelBg2, Theme.Paper, () =>
                {
                    if (!slotFree || _run.gold < itemPrice) return;
                    _run.gold -= itemPrice;
                    _run.items.Add(itemId);
                    CloseModalAndContinue();
                });
            UiKit.AnchorCenter(itemBtn.GetComponent<RectTransform>(), new Vector2(-320f, -150f), new Vector2(270f, 130f));

            var leave = UiKit.CreateButton(_modal, "Leave", "离开", Theme.FontBody, Theme.InkSoft, Theme.Paper,
                CloseModalAndContinue);
            UiKit.AnchorCenter(leave.GetComponent<RectTransform>(), new Vector2(200f, -170f), new Vector2(300f, 72f));
        }

        private static string CardName(string id)
        {
            switch (id)
            {
                case "strike": return "打击";
                case "defend": return "防御";
                case "bash": return "重击";
                case "cleave": return "顺劈斩";
                case "iron_wave": return "铁斩波";
                case "shrug_it_off": return "耸肩无视";
                default: return id;
            }
        }

        private void ShowEventDialog()
        {
            OpenModal("事件", new Vector2(900f, 560f));

            var desc = UiKit.CreateText(_modal, "Desc",
                "路边立着一只破旧的猫雕像，爪下压着一张字条：\n「喂它一枚金币，它会告诉你路。」",
                Theme.FontBody, Theme.Paper);
            UiKit.AnchorCenter(desc.rectTransform, new Vector2(0f, 60f), new Vector2(760f, 120f));

            var pay = UiKit.CreateButton(_modal, "Pay", "给 1 金币（获得 20 金币）", Theme.FontBody,
                Theme.Gold, Theme.Ink, () =>
                {
                    if (_run.gold >= 1) { _run.gold += 19; }
                    CloseModalAndContinue();
                });
            UiKit.AnchorCenter(pay.GetComponent<RectTransform>(), new Vector2(0f, -80f), new Vector2(560f, 76f));

            var ignore = UiKit.CreateButton(_modal, "Ignore", "无视它", Theme.FontBody,
                Theme.InkSoft, Theme.Paper, CloseModalAndContinue);
            UiKit.AnchorCenter(ignore.GetComponent<RectTransform>(), new Vector2(0f, -180f), new Vector2(560f, 76f));
        }

        private void OpenPauseMenu()
        {
            OpenModal("暂停", new Vector2(620f, 460f));

            var resume = UiKit.CreateButton(_modal, "Resume", "继续", Theme.FontBody, Theme.Ok, Theme.Paper,
                () => { UiKit.DestroyChildren(_dialogLayer); _modal = null; });
            UiKit.AnchorCenter(resume.GetComponent<RectTransform>(), new Vector2(0f, 40f), new Vector2(400f, 76f));

            var save = UiKit.CreateButton(_modal, "Save", "保存并返回主菜单", Theme.FontBody,
                Theme.InkSoft, Theme.Paper, () =>
                {
                    SaveService.SaveRun(_run);
                    GameSession.Instance.SetRun(_run);
                    GameSession.Instance.GoMainMenu();
                });
            UiKit.AnchorCenter(save.GetComponent<RectTransform>(), new Vector2(0f, -60f), new Vector2(400f, 76f));

            var abandon = UiKit.CreateButton(_modal, "Abandon", "放弃本局（删除存档）", Theme.FontSmall,
                Theme.Bad, Theme.Paper, () =>
                {
                    SaveService.DeleteRun();
                    GameSession.Instance.GoMainMenu();
                });
            UiKit.AnchorCenter(abandon.GetComponent<RectTransform>(), new Vector2(0f, -150f), new Vector2(400f, 64f));
        }
    }
}
