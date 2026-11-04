using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CatFight.UI
{
    /// <summary>
    /// 运行时 UI 工厂：全部界面由代码构建，不依赖任何美术资源或 Prefab。
    /// 占位素材阶段用它快速出可运行界面；接入正式素材后，把 Create* 内部换成
    /// Image.sprite 赋真实图集即可，调用方代码完全不用改。
    /// </summary>
    public static class UiKit
    {
        private static Sprite _white;
        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _roundRect;
        private static Sprite _portrait;
        private static Font _font;
        private static bool _fontWarned;

        // ------------------------------------------------------------------
        // 资源（程序生成）
        // ------------------------------------------------------------------

        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var px = new Color32[16];
                    for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(px);
                    tex.Apply();
                    tex.hideFlags = HideFlags.HideAndDontSave;
                    _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                    _white.hideFlags = HideFlags.HideAndDontSave;
                }
                return _white;
            }
        }

        /// <summary>抗锯齿实心圆（占位节点、能量点用）。</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = BuildCircle(128, 0f);
                return _circle;
            }
        }

        /// <summary>抗锯齿圆环（用于描边高亮）。</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null) _ring = BuildCircle(128, 0.82f);
                return _ring;
            }
        }

        /// <summary>圆角矩形（9-slice），卡片与面板底。</summary>
        public static Sprite RoundRect
        {
            get
            {
                if (_roundRect == null) _roundRect = BuildRoundRect(64, 16);
                return _roundRect;
            }
        }

        private static Sprite BuildCircle(int size, float innerRatio)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float outer = c - 1f;
            float inner = innerRatio > 0f ? outer * innerRatio : 0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = innerRatio > 0f
                        ? Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f)
                        : Mathf.Clamp01(outer - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        private static Sprite BuildRoundRect(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    int cx = Mathf.Clamp(x, radius, size - radius - 1);
                    int cy = Mathf.Clamp(y, radius, size - radius - 1);
                    float dx = x - cx, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            var border = new Vector4(radius, radius, radius, radius);
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                   0, SpriteMeshType.FullRect, border);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        /// <summary>
        /// 玩家立绘。优先读 Resources/Art/player_portrait（接入正式素材时放这里即可），
        /// 没有则程序生成一个猫头占位图，保证界面不空。
        /// </summary>
        public static Sprite Portrait
        {
            get
            {
                if (_portrait != null) return _portrait;

                var res = Resources.Load<Sprite>("Art/player_portrait");
                if (res != null)
                {
                    _portrait = res;
                    return _portrait;
                }

                _portrait = BuildCatPortrait(256);
                return _portrait;
            }
        }

        /// <summary>清缓存（换素材后调试用）。</summary>
        public static void InvalidatePortrait() => _portrait = null;

        private static Sprite BuildCatPortrait(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];

            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            Color32 fur = Theme.Hex("6B5544");
            Color32 furDark = Theme.Hex("4A3A2E");
            Color32 belly = Theme.Hex("D8C4A8");
            Color32 eye = Theme.Hex("2B2622");
            Color32 nose = Theme.Hex("C4707A");

            float cx = size * 0.5f;

            // 身体（下半椭圆）
            FillEllipseAA(px, size, cx, size * 0.20f, size * 0.29f, size * 0.20f, fur);
            // 头
            FillCircleAA(px, size, cx, size * 0.60f, size * 0.235f, fur);
            // 耳朵（两个三角）
            FillTriangleAA(px, size,
                new Vector2(cx - size * 0.20f, size * 0.70f),
                new Vector2(cx - size * 0.23f, size * 0.93f),
                new Vector2(cx - size * 0.03f, size * 0.78f), fur);
            FillTriangleAA(px, size,
                new Vector2(cx + size * 0.20f, size * 0.70f),
                new Vector2(cx + size * 0.23f, size * 0.93f),
                new Vector2(cx + size * 0.03f, size * 0.78f), fur);
            // 耳内
            FillTriangleAA(px, size,
                new Vector2(cx - size * 0.175f, size * 0.725f),
                new Vector2(cx - size * 0.195f, size * 0.875f),
                new Vector2(cx - size * 0.065f, size * 0.775f), nose);
            FillTriangleAA(px, size,
                new Vector2(cx + size * 0.175f, size * 0.725f),
                new Vector2(cx + size * 0.195f, size * 0.875f),
                new Vector2(cx + size * 0.065f, size * 0.775f), nose);
            // 肚皮（提亮）
            FillEllipseAA(px, size, cx, size * 0.155f, size * 0.18f, size * 0.12f, belly);
            // 眼睛
            FillCircleAA(px, size, cx - size * 0.085f, size * 0.625f, size * 0.030f, eye);
            FillCircleAA(px, size, cx + size * 0.085f, size * 0.625f, size * 0.030f, eye);
            // 瞳孔高光
            FillCircleAA(px, size, cx - size * 0.095f, size * 0.637f, size * 0.010f, new Color32(255, 255, 255, 235));
            FillCircleAA(px, size, cx + size * 0.075f, size * 0.637f, size * 0.010f, new Color32(255, 255, 255, 235));
            // 鼻子
            FillTriangleAA(px, size,
                new Vector2(cx - size * 0.025f, size * 0.545f),
                new Vector2(cx + size * 0.025f, size * 0.545f),
                new Vector2(cx, size * 0.520f), nose);
            // 胡须
            DrawLineAA(px, size, cx - size * 0.10f, size * 0.555f, cx - size * 0.26f, size * 0.575f, furDark, 2);
            DrawLineAA(px, size, cx - size * 0.10f, size * 0.525f, cx - size * 0.26f, size * 0.515f, furDark, 2);
            DrawLineAA(px, size, cx + size * 0.10f, size * 0.555f, cx + size * 0.26f, size * 0.575f, furDark, 2);
            DrawLineAA(px, size, cx + size * 0.10f, size * 0.525f, cx + size * 0.26f, size * 0.515f, furDark, 2);

            tex.SetPixels32(px);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        // ---- 抗锯齿填充基元 ----

        private static void Blend(Color32[] px, int size, int x, int y, Color32 c, float a)
        {
            if (x < 0 || x >= size || y < 0 || y >= size || a <= 0f) return;
            int i = y * size + x;
            Color32 d = px[i];
            float da = d.a / 255f;
            float outA = a + da * (1f - a);
            if (outA <= 0f) return;
            px[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((c.r * a + d.r * da * (1f - a)) / outA), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((c.g * a + d.g * da * (1f - a)) / outA), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((c.b * a + d.b * da * (1f - a)) / outA), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255));
        }

        private static void FillCircleAA(Color32[] px, int size, float cx, float cy, float r, Color32 col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + r + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + r + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    Blend(px, size, x, y, col, a);
                }
        }

        private static void FillEllipseAA(Color32[] px, int size, float cx, float cy, float rx, float ry, Color32 col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 1)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + rx + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 1)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + ry + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) + 0.5f);
                    Blend(px, size, x, y, col, a);
                }
        }

        private static void FillTriangleAA(Color32[] px, int size, Vector2 a, Vector2 b, Vector2 c, Color32 col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 1));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 1));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1));

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
                    bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
                    bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
                    if (hasNeg && hasPos) continue;
                    Blend(px, size, x, y, col, 1f);
                }
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        private static void DrawLineAA(Color32[] px, int size, float x0, float y0, float x1, float y1, Color32 col, int thickness)
        {
            float dx = x1 - x0, dy = y1 - y0;
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy) * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                FillCircleAA(px, size, x0 + dx * t, y0 + dy * t, thickness * 0.5f, col);
            }
        }

        /// <summary>中文字体：优先系统字体（雅黑 / 黑体 / 宋体），失败再退到内置字体。</summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                string[] candidates =
                {
                    "Microsoft YaHei", "微软雅黑", "Microsoft YaHei UI",
                    "SimHei", "黑体", "SimSun", "宋体",
                    "Noto Sans CJK SC", "Source Han Sans SC", "Arial Unicode MS"
                };

                try { _font = UnityEngine.Font.CreateDynamicFontFromOSFont(candidates, 32); }
                catch (Exception e) { Debug.LogWarning("[UiKit] 系统字体加载异常：" + e.Message); }

                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch { /* ignore */ }
                }

                if (_font == null && !_fontWarned)
                {
                    _fontWarned = true;
                    Debug.LogError("[UiKit] 找不到可用字体，UI 文字将不可见。请检查系统字体。");
                }
                return _font;
            }
        }

        // ------------------------------------------------------------------
        // Canvas / RectTransform
        // ------------------------------------------------------------------

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        /// <summary>铺满父级。</summary>
        public static RectTransform Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        /// <summary>锚定到某一点，并给定尺寸。anchor 用 0..1 表示父级相对位置。</summary>
        public static RectTransform Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform AnchorCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // ------------------------------------------------------------------
        // 图形
        // ------------------------------------------------------------------

        public static Image CreateImage(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            var rt = CreateRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : White;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite == RoundRect) img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>圆角面板。</summary>
        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            var img = CreateImage(parent, name, color, RoundRect);
            img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>纯色矩形块（血条填充等）。</summary>
        public static Image CreateBar(Transform parent, string name, Color color)
        {
            var img = CreateImage(parent, name, color);
            img.type = Image.Type.Simple;
            return img;
        }

        // ------------------------------------------------------------------
        // 文本
        // ------------------------------------------------------------------

        public static Text CreateText(Transform parent, string name, string content, int size,
                                      Color color, TextAnchor anchor = TextAnchor.MiddleCenter,
                                      FontStyle style = FontStyle.Normal)
        {
            var rt = CreateRect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        /// <summary>带描边的文字，用于压在深色/浅色背景上都清晰。</summary>
        public static Text CreateOutlinedText(Transform parent, string name, string content, int size,
                                              Color color, Color outline, TextAnchor anchor = TextAnchor.MiddleCenter,
                                              FontStyle style = FontStyle.Normal)
        {
            var t = CreateText(parent, name, content, size, color, anchor, style);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = outline;
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        // ------------------------------------------------------------------
        // 按钮
        // ------------------------------------------------------------------

        public static Button CreateButton(Transform parent, string name, string label, int fontSize,
                                          Color bg, Color textColor, Action onClick)
        {
            var rt = CreateRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundRect;
            img.type = Image.Type.Sliced;
            img.color = bg;
            img.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var txt = CreateText(rt, "Label", label, fontSize, textColor);
            Stretch(txt.rectTransform, 6f);

            if (onClick != null) btn.onClick.AddListener(new UnityAction(onClick));
            return btn;
        }

        public static void SetButtonEnabled(Button btn, bool enabled)
        {
            if (btn == null) return;
            btn.interactable = enabled;

            // 保留按钮原色，只调整明度——否则会把金色按钮刷成白色
            var img = btn.GetComponent<Image>();
            if (img == null) return;

            var marker = btn.GetComponent<ButtonBaseColor>();
            if (marker == null)
            {
                marker = btn.gameObject.AddComponent<ButtonBaseColor>();
                marker.baseColor = img.color;
            }

            img.color = enabled
                ? marker.baseColor
                : new Color(marker.baseColor.r * 0.55f, marker.baseColor.g * 0.55f, marker.baseColor.b * 0.55f, 0.75f);
        }

        /// <summary>记录按钮的原始底色，供启用/禁用时恢复。</summary>
        public class ButtonBaseColor : MonoBehaviour
        {
            public Color baseColor = Color.white;
        }

        // ------------------------------------------------------------------
        // 复合控件
        // ------------------------------------------------------------------

        /// <summary>
        /// 进度条（血条 / 格挡 / 经验）。返回填充 Image，调用方用 SetBarFill 控制比例。
        /// 支持分段：segments &gt; 1 时按分隔线切开（对应设计文档"分段血条"）。
        /// </summary>
        public static Image CreateProgressBar(Transform parent, string name, Vector2 size,
                                              Color back, Color fill, int segments = 1)
        {
            var root = CreateRect(parent, name);
            root.sizeDelta = size;

            var bg = CreateImage(root, "Back", back, RoundRect);
            bg.type = Image.Type.Sliced;
            Stretch(bg.rectTransform);

            var fillRt = CreateRect(root, "Fill");
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.sprite = RoundRect;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = fill;
            fillImg.raycastTarget = false;

            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);

            if (segments > 1)
            {
                for (int i = 1; i < segments; i++)
                {
                    var line = CreateImage(root, "Seg" + i, new Color(0f, 0f, 0f, 0.35f));
                    var lrt = line.rectTransform;
                    lrt.anchorMin = new Vector2((float)i / segments, 0f);
                    lrt.anchorMax = new Vector2((float)i / segments, 1f);
                    lrt.pivot = new Vector2(0.5f, 0.5f);
                    lrt.sizeDelta = new Vector2(2f, 0f);
                    lrt.anchoredPosition = Vector2.zero;
                }
            }
            return fillImg;
        }

        /// <summary>设置进度条填充比例（0..1）。</summary>
        public static void SetBarFill(Image fill, float ratio01)
        {
            if (fill == null) return;
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(ratio01), 1f);
        }

        /// <summary>全屏遮罩，用于弹窗背景。</summary>
        public static Image CreateModalShade(Transform parent, string name = "Shade")
        {
            var img = CreateImage(parent, name, new Color(0f, 0f, 0f, 0.72f));
            Stretch(img.rectTransform);
            img.raycastTarget = true;
            return img;
        }

        /// <summary>标题 + 正文的弹窗面板。返回面板 RectTransform，内容由调用方填充。</summary>
        public static RectTransform CreateDialog(Transform parent, string title, Vector2 size)
        {
            var panel = CreatePanel(parent, "Dialog", Theme.PanelBg);
            AnchorCenter(panel.rectTransform, Vector2.zero, size);

            var titleBar = CreatePanel(panel.transform, "TitleBar", Theme.PanelBg2);
            var trt = titleBar.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0f, 72f);
            trt.anchoredPosition = Vector2.zero;

            var t = CreateText(titleBar.transform, "Title", title, Theme.FontHead, Theme.Paper, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform, 8f);

            return panel.rectTransform;
        }

        public static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>把世界坐标（屏幕像素）换算成某 Canvas 下的 anchoredPosition。</summary>
        public static Vector2 ScreenToCanvas(Canvas canvas, Vector2 screenPos)
        {
            var rt = canvas.GetComponent<RectTransform>();
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, canvas.worldCamera, out local);
            return local;
        }
    }
}
